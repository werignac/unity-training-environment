"""
@author William Erignac
@version 2024-09-02

This script runs the cart pole experiment in Unity and uses a gradient descent algorithm on a neural net to learn
a cart pole controller that balances its pole while under the influence of a random force.

The neural net architecture and gradient descent code is from the first example for cart pole in "Deep
Reinforcement Learning In Action" by Zai, Alexander and Brown, Brandon (pg 106-108).

For more information, check the the Deep Reinforcement Learning In Action repo:
https://github.com/DeepReinforcementLearning/DeepReinforcementLearningInAction

MIT License

Copyright (c) 2018 DeepReinforcementLearning

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"""
from __future__ import annotations

import argparse
import itertools
import json
import os
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import random
import torch.multiprocessing as mp

from collections import deque
from collections.abc import Callable, Iterable

import torch
import torch.nn as nn
import torch.nn.functional as F
from tqdm import tqdm

from unity_instance import UnityInstance

#region Statics

PIPE_PATH = '\\\\.\\pipe\\'
SIMULATOR_PATH = os.environ["UNITY_SIMULATOR_PATH"]
SIMULATOR_ARGS = ["-batchmode", "-nographics"]

def pipe_name(pipe_number: int) -> str:
    return f"Unity_Training_Pipe_{pipe_number}"

#endregion Statics

#region Neural Net

class ActorCritic(nn.Module): #B
    def __init__(self):
        super(ActorCritic, self).__init__()
        self.l1 = nn.Linear(4,25)
        self.l2 = nn.Linear(25,50)
        self.actor_lin1 = nn.Linear(50,2)
        self.l3 = nn.Linear(50,25)
        self.critic_lin1 = nn.Linear(25,1)
    def forward(self,x):
        x = F.normalize(x,dim=0)
        y = F.relu(self.l1(x))
        y = F.relu(self.l2(y))
        actor = F.log_softmax(self.actor_lin1(y),dim=0) #C
        c = F.relu(self.l3(y.detach()))
        critic = torch.tanh(self.critic_lin1(c)) #D
        return actor, critic #E

CRITIC_LOSS_CONSTANT = 0.1
FUTURE_DISCOUNT_FACTOR = 0.95

#endregion Neural Net

#region Initialization Data

class CartPoleData:
    def __init__(self):
        self.wind_seed = np.random.randint(1, 1000000)
        self.initial_angle = (0.5 - np.random.rand()) * 2 * 5

    def serialize(self):
        return {"WindSeed": self.wind_seed,
                "InitialAngle": self.initial_angle}

#endregion InitializationData

#region Running Simulation

class Worker:
    """
    A class that handles simulation and training on one of many subprocesses.
    """

    def __init__(
            self,
            sim_args: tuple,
            sim_kwargs: dict,
            seed: int,
            simulator_instance: UnityInstance | None = None,
            quit_simulator_on_close: bool = True
    ):
        """
        model: the model to train or use to take actions.
        sim_args: args for creating a UnityInstance if none is provided (default behaviour).
        sim_kwargs: kwargs for creating a UnityInstance if none is provided (default behaviour).
        seed: a seed used to initialize rngs.
        simulator_instance: the UnityInstance to communicate with. None is provided by default.
        Worker will always close its UnityInstance when exiting a with statement, regardless of how
        the UnityInstance was provided.
        """

        self.model: ActorCritic | None = None
        self.sim_args = sim_args, sim_kwargs
        self.seed = seed
        self.quit_simulator_on_close = quit_simulator_on_close

        self.optimizer: torch.optim.Optimizer | None = None
        self.simulator_instance: UnityInstance | None = simulator_instance

        # Stats for plotting.
        self.actor_losses: list | None = None
        self.critic_losses: list | None = None
        self.cumulative_losses: list | None = None
        self.actor_sum_weights: list | None = None

    def set_model(self, model: ActorCritic):
        self.model = model

    def __enter__(self) -> Worker:
        """
        Sets up fields that should be process-local.
        - Optimizer
        - Simulator
        - RNGs
        """
        # Set up the optimizer.
        self.optimizer = torch.optim.Adam(lr=1e-4, params=self.model.parameters())
        self.optimizer.zero_grad()

        # Set up the simulator.
        if self.simulator_instance is None:
            args, kwargs = self.sim_args
            self.simulator_instance = UnityInstance(*args, **kwargs)

        # Set up rngs.
        random.seed(self.seed)
        np.random.seed(random.randint(0, 100000))
        torch.manual_seed(random.randint(0, 100000))

        # Set up stat collectors.
        self.actor_losses = []
        self.critic_losses = []
        self.cumulative_losses = []
        self.actor_sum_weights = []

        # Return self for enter.
        return self

    def _generate_sessions(self, session_count: int) -> pd.DataFrame:
        """
        Generates n session initialization objects and puts them into
        a dataframe with two columns. The first is the objects ("Initial Condition");
        the second is where the score will go ("Score"), which is initialized to zero.
        """
        # Generate random sessions.
        sessions = []
        for i in range(session_count):
            sessions.append(CartPoleData())
        scores = np.zeros(session_count)
        # Put altogether into a dataframe.
        return pd.DataFrame(zip(sessions, scores), columns=["Initial Condition", "Score"])

    def _gradient_descent_on_experiences(self, experiences: np.ndarray[Experience]) -> None:
        # Get the returns (stored where the rewards were previously stored).
        returns = torch.stack(tuple(Experience.v_get_reward(experiences))).view(-1)
        # Get the in states from the experience.
        states_in = torch.stack(tuple(Experience.v_get_state_in(experiences))).detach()
        # Get the actions that were chosen during the experience.
        actions = torch.tensor(tuple(Experience.v_get_action(experiences)), dtype=torch.int32).view(-1).detach()
        # Get the probability of the chosen actions for the current actor.
        action_probabilities, state_values = self.model(states_in.detach())
        action_probabilities = action_probabilities.gather(dim=1, index=actions.long().view(-1, 1)).squeeze()
        # Get the predicted value of the states for the current critic.
        state_values = state_values.squeeze()

        # Forces actions that performed better than expected to increase in probability.
        # Actions that performed worse than expected decrease in probability.

        # Note: The loss function given in Deep Reinforcement Learning in Action is different
        # from the one used here. In that book -1 * logprob * (return - learned_state_value) is used.
        # Here, -1 is applied only when return - learned_state_value > 0, and otherwise, the logprob
        # is replaced with log(1 - prob).
        # This always results in positive loss while inscentivising going towards 0 or 1 depending on
        # whether we overestimated or underestimated the value of the state.

        advantage = returns - state_values.detach()
        """
        underestimated_advantage = advantage > 0
        overestimated_advantage = ~underestimated_advantage
        underestimated_loss = (-1 * torch.log(action_probabilities[underestimated_advantage]) * (
        advantage[underestimated_advantage])).sum()
        overestimated_loss = (torch.log(1 - action_probabilities[overestimated_advantage]) * (
        advantage[overestimated_advantage])).sum()

        actor_loss = (underestimated_loss + overestimated_loss) # / advantage.shape[0]
        """
        actor_loss = (-1 * action_probabilities * advantage).sum()
        self.actor_losses.append(actor_loss.detach().tolist())  # Used for plotting. TODO: Don't use statics.
        # Squared Errors. Similar to linear regression.
        critic_loss = torch.pow(state_values - returns, 2).sum()
        self.critic_losses.append(critic_loss.detach().tolist())  # Used for plotting. TODO: Don't use statics.
        # Cumulative loss.
        loss = actor_loss + CRITIC_LOSS_CONSTANT * critic_loss
        self.cumulative_losses.append(loss.detach().tolist())  # Used for plotting. TODO: Don't use statics.
        # Sum of Model Weights
        actor_weights = 0
        for param in self.model.parameters():
            actor_weights += param.data.sum().detach().numpy()
        self.actor_sum_weights.append(actor_weights)

        # Backwards Propagation.
        loss.backward()
        self.optimizer.step()

        # Set up optimizer for next gradient TODO: Figure out a way to reset gradients while having multiple concurrent simulations.
        self.optimizer.zero_grad()

    def _read_simulator_responses(self, starting_conditions: pd.DataFrame, learn: bool):
        """
        Continuously reads the responses given by the simulator.
        Responds by making the agent choose actions.
        When a session has finished, puts the results into a replay buffer and performs gradient descent.
        """

        # Mapping of session indexes to running brains. The brains take in simulation frame
        # data and output actions for the running simulations.
        running_brains: dict[int, AgentBrain] = dict()

        while True:
            line = self.simulator_instance.read_line()

            if line is None:
                break

            line_split = line.split(" ")

            # If there is a space, this is either a score that is being reported,
            # or data about a frame that is being sent.
            if len(line_split) > 1:
                index = int(line_split[0])
                score_parsed: bool = True
                score: float
                try:
                    score = float(line_split[1])
                except Exception as e:
                    score_parsed = False

                if score_parsed:
                    # If this is a score, the session has ended.
                    # We can perform gradient descent.
                    starting_conditions.loc[index, "Score"] = score
                    _experiences = running_brains[index].on_session_end(score)
                    if learn:
                        self._gradient_descent_on_experiences(_experiences)
                    del running_brains[index]
                else:
                    # Otherwise, if this is data about session in progress,
                    # give the running brain the new state and get the next
                    # command to run for that session.
                    brain: AgentBrain = running_brains[index]
                    command = brain.process_frame_data(json.loads(line_split[1]))
                    if not (command is None):
                        to_write = f"{index} {command}"
                        self.simulator_instance.write_line(to_write)
                        self.simulator_instance.flush_pipe()
            else:
                # Otherwise, a session is starting execution.
                index = int(line_split[0])
                running_brains[index] = AgentBrain(starting_conditions.loc[index, "Initial Condition"], self.model, index)

    def execute_epoch(self, session_count, learn=True) -> pd.DataFrame:
        """
        Runs an epoch of sessions

        learn: whether to perform gradient descent.
        """
        # TODO: Figure out a way to leverage the simulator running multiple sessions at a time while reseting the gradient.
        # Set up the simulator to run an experiment.
        self.simulator_instance.run_experiment("cart_pole")

        sessions = self._generate_sessions(session_count)
        # Send the session initialization data.
        serialize_v = np.vectorize(lambda c: json.dumps(c.serialize()))
        serializations = serialize_v(sessions["Initial Condition"].to_numpy())
        self.simulator_instance.send_session_initialization_data(serializations)
        self.simulator_instance.end_send_session_initialization_data()
        # Read the responses from the simulator and process them
        # this includes starting new sessions, reporting the final
        # scores of sessions, and data about the initial state of sessions.
        # Perform gradient descent if learning.
        self._read_simulator_responses(sessions, learn)

        # By now "sessions" is updated to have the true scores from the read_simulator_responses thread.
        sorted_sessions = sessions.sort_values("Score", ascending=False)

        return sorted_sessions

    def __exit__(self, exc_type, exc_val, exc_tb):
        if self.simulator_instance is not None and self.quit_simulator_on_close:
            try:
                self.simulator_instance.quit()
            except Exception as e:
                print("The following exception occurred when closing a simulator instance. This may be caused by another error.")
                print(e, end="\n\n")

    def get_stats(self) -> tuple[list, list, list, list]:
        """
        Get list of actor losses, critic losses, cumulative losses, and sum of weights.
        """
        return self.actor_losses, self.critic_losses, self.cumulative_losses, self.actor_sum_weights


def worker_process(worker: Worker, model: ActorCritic, counter: mp.Value, epoch_count: int, return_queue: mp.Queue):
    worker.set_model(model)

    scores = []
    with worker:
        for i in range(epoch_count):
            session_results: pd.DataFrame = worker.execute_epoch(1, learn=True)
            scores.append(session_results.iloc[0]["Score"])
            counter.value = counter.value + 1

    statistics = (scores, *worker.get_stats())
    return_queue.put(statistics)


#region Brain Control

class AgentBrain:
    def __init__(self, session_initialization_data: CartPoleData, model: ActorCritic, index: int):
        self._session_init = session_initialization_data
        self._session_index = index
        self._data_count = 0
        self._running_score = 0  # The last known score of this agent.

        self.last_state_action = None
        self.transitions = []

        self.model = model

    def process_frame_data(self, frame_data: dict) -> str:
        """
        Takes a json object sent specifically to this brain and
        returns a command. If None is returned, no command should
        be sent.
        """
        self._data_count += 1
        state, score = AgentBrain._extract_frame_data(frame_data)
        state = torch.from_numpy(state).float()
        # We need to know the change in score (reward) for actor-critic learning.
        reward = score - self._running_score
        # Update the running score for the next action.
        self._running_score = score

        # Choose an action using softmax.
        logits, state_value = self.model(state)
        logits = logits.view(-1)
        action = torch.distributions.Categorical(logits=logits).sample()

        if not self.last_state_action is None:
            self.transitions.append((*self.last_state_action, state, reward))

        self.last_state_action = state, state_value, action, logits[action]

        return json.dumps({'MoveRight': bool(action.detach().numpy() == 0)})

    @staticmethod
    def _extract_frame_data(data: dict) -> tuple[np.ndarray, float]:
        """
        Turns frame data into an array to be fed to the NN.
        Returns the score separately.
        """
        return np.array([
            data['CartPosition'],
            data['CartVelocity'],
            data['PoleAngle'],
            data['PoleAngularVelocity']#,
            #data['NormalizedWind']
        ]), data['Score']

    def on_session_end(self, score, learn: bool = True) -> np.ndarray[Experience]:
        """
        When a session has ended, gather all the rewards, states, and actions, and perform
        gradient descent.

        learn: whether to do any learning.
        """

        # TODO: See if we need this last step.
        _last_reward = score - self._running_score
        self._running_score = score
        self.transitions += [(
            *self.last_state_action,  # Prior state, prior state value, prior action.
            self.last_state_action[0],  # TODO: See if having the starting and ending state be the same causes problems.
            _last_reward
        )]

        # Construct experiences for replay.
        experiences = np.fromiter(
            itertools.starmap(Experience, self.transitions),
            dtype=object,
            count=len(self.transitions)
        )  # TODO: use df instead of objects? maybe zip function?

        # Calculate returns.
        # Returns are like scores in that they're cumulative rewards, but with future discounting.
        returns = []
        _ret = torch.tensor([0])
        for reward in reversed(Experience.v_get_reward(experiences)):
            _ret = reward + FUTURE_DISCOUNT_FACTOR * _ret
            returns.insert(0, _ret.detach())

        returns = torch.stack(tuple(returns)).view(-1)
        returns = F.normalize(returns, dim=0)

        # Replace experience rewards with returns.
        for i in range(experiences.shape[0]):
            experiences[i].reward = returns[i]

        return experiences


#endregion Brain Control

#region Experience Replay

class Experience:
    def __init__(
            self,
            state_in: torch.Tensor,
            state_in_value: torch.Value,
            action: torch.Value,
            action_probability: torch.Value,
            state_out: torch.Tensor,
            reward: torch.Value
    ):
        self.state_in = state_in
        self.state_in_value = state_in_value
        self.action = action
        self.action_probability = action_probability  # The probability that self.action was taken.
        self.state_out = state_out
        self.reward = reward

    def get_state_in(self) -> torch.Tensor:
        return self.state_in

    def get_state_in_value(self) -> torch.Value:
        return self.state_in_value

    def get_action(self) -> torch.Value:
        return self.action

    def get_action_probability(self) -> torch.Value:
        return self.action_probability

    def get_state_out(self) -> torch.Tensor:
        return self.state_out

    def get_reward(self) -> torch.Value:
        return self.reward

    def get_all(self) -> tuple[torch.Tensor, torch.Value, torch.Value, torch.Value, torch.Tensor, torch.Value]:
        return self.state_in, self.state_in_value, self.action, self.action_probability, self.state_out, self.reward

    # Vectorized Getters
    v_get_state_in: Callable[[Iterable[Experience]], Iterable[torch.Tensor]] = np.vectorize(
        get_state_in,
        otypes=[object]
    )
    v_get_state_in_value: Callable[[Iterable[Experience]], Iterable[torch.Value]] = np.vectorize(
        get_state_in_value,
        otypes=[object]
    )
    v_get_action: Callable[[Iterable[Experience]], Iterable[torch.Value]] = np.vectorize(
        get_action,
        otypes=[object]
    )
    v_get_action_probability: Callable[[Iterable[Experience]], Iterable[torch.Value]] = np.vectorize(
        get_action_probability,
        otypes=[object]
    )
    v_get_state_out: Callable[[Iterable[Experience]], Iterable[torch.Tensor]] = np.vectorize(
        get_state_out,
        otypes=[object]
    )
    v_get_reward: Callable[[Iterable[Experience]], Iterable[torch.Value]] = np.vectorize(
        get_reward,
        otypes=[object]
    )
    v_get_all: Callable[[Iterable[Experience]], Iterable[Iterable[torch.Value | torch.Tensor]]] = np.vectorize(
        get_all#,
        #otypes=[object, object, object, object],
        #signature='()->(),(),(),(),()'
    )

#endregion Experience Replay

def save_onnx():
    """
    Save the cart pole agent as an onnx file.
    """
    random_input = torch.rand((4,), dtype=torch.float32)
    filename = f'cart_pole_agent.onnx'
    torch.onnx.export(model, random_input, filename, input_names=['input'], output_names=['output', 'state_value'])
    return filename


def display_performance():
    """
    Show the final cart pole agent playing 5 rounds.
    """
    display_exec_args = dict()
    display_exec_args["simulator_path"] = SIMULATOR_PATH
    display_exec_args["simulator_args"] = ["-p", pipe_name(0)]

    if RUN_EXECUTABLE:
        display_sim_inst = UnityInstance(os.path.join(PIPE_PATH, pipe_name(0)), display_exec_args, no_timeout=True)
    else:
        display_sim_inst = sim_inst

    worker = Worker(None, None, random.randint(0, 100000), display_sim_inst)
    worker.set_model(model)
    with worker:
        for i in range(5):
            worker.execute_epoch(1, learn=False)


#endregion Running Simulation


if __name__ == "__main__":
    model = ActorCritic()
    model.share_memory()

    # Parse Arguments
    parser = argparse.ArgumentParser()
    parser.add_argument("-t", help="if this flag is passed, don't run the Unity executable.", action="store_false")
    parser.add_argument("-e", help="number of epochs that should be run per process.", type=int, default=64)
    parser.add_argument("-display", help="if this flag is passed, display the agent's performance after training.", action="store_true")
    parser.add_argument("-stats", help="what types of statistics to show.", type=int, default=0)
    args = parser.parse_args()
    RUN_EXECUTABLE = args.t
    EPOCH_COUNT = args.e
    DISPLAY_PERFORMANCE = args.display
    STATS = args.stats

    PROCESS_COUNT = 32 if RUN_EXECUTABLE else 1

    if STATS > 0:
        avg_performance_per_epoch = [0]

    if RUN_EXECUTABLE:
        print(f"Running Simulator: {SIMULATOR_PATH}")
    else:
        print(f"Connecting to Simulator without subprocess")
        sim_inst = UnityInstance(os.path.join(PIPE_PATH, pipe_name(0)), None, no_timeout=True)

    exec_args = dict()
    exec_args["simulator_path"] = SIMULATOR_PATH

    # Create a worker for each process.
    mp_workers: list[Worker] = []
    for i in range(PROCESS_COUNT):
        if RUN_EXECUTABLE:
            _exec_args = dict(exec_args)
            _exec_args["simulator_args"] = SIMULATOR_ARGS + ["-p", pipe_name(i)]
            _sim_args = (os.path.join(PIPE_PATH, pipe_name(i)), _exec_args)
            _sim_kwargs = {"no_timeout": True}

            _worker = Worker(
                _sim_args,
                _sim_kwargs,
                random.randint(0, 1000000),
                quit_simulator_on_close=True
            )
        else:
            _worker = Worker(None, None, random.randint(0, 1000000), simulator_instance=sim_inst)
        mp_workers.append(_worker)

    if RUN_EXECUTABLE:
        # Variables for cross-process communication.
        counter = mp.Value('i', 0)
        return_queue = mp.Queue(PROCESS_COUNT)

        # Create the processes.
        processes: list[mp.Process] = []
        for i in range(PROCESS_COUNT):
            _p_args = (mp_workers[i], model, counter, EPOCH_COUNT, return_queue)
            _p: mp.Process = mp.Process(target=worker_process, args=_p_args)
            _p.start()
            processes.append(_p)

        per_process_statistics = []
        # Poll that the processes have finished, updating a progress bar as we wait.
        with tqdm(total=EPOCH_COUNT * PROCESS_COUNT, desc='Epochs') as progress:
            running_processes = list(processes)
            last_counter = 0
            while len(running_processes) > 0:
                _to_remove: list[int] = []
                # Poll the processes that have finished.
                for i, _p in enumerate(running_processes):
                    _p.join(1 / PROCESS_COUNT)
                    if _p.exitcode is not None:
                        _to_remove.append(i)

                # Update the progress bar on how many epochs have been completed.
                current_counter = counter.value
                progress.update(current_counter - last_counter)
                last_counter = current_counter

                # Remove the processes that have finished.
                for i in reversed(_to_remove):
                    running_processes.pop(i)
                    per_process_statistics.append(return_queue.get())

        # Print the exit codes to ensure that the processes worked and terminate the processes.
        print(f"Exit Codes: {list(_p.exitcode for _p in processes)}")
        for _p in processes:
            _p.terminate()

        # Collect statistics for plotting.
        statistics = [np.zeros(EPOCH_COUNT) for i in range(5)]
        for process_stats in per_process_statistics:
            for i, values in enumerate(process_stats):
                statistics[i] += values

        for i in range(len(statistics)):
            statistics[i] /= EPOCH_COUNT

    else:
        # Run the worker on this process.
        scores = []
        with mp_workers[0] as worker:
            for i in tqdm(range(EPOCH_COUNT)):
                session_results: pd.DataFrame = worker.execute_epoch(1, learn=True)
                scores.append(session_results.iloc[0]["Score"])

        # Collect statistics
        statistics = [scores, *worker.get_stats()]

    save_onnx()

    stat_names = []

    if STATS > 0:
        stat_names += ["Score"]
    if STATS > 1:
        stat_names += ["Actor Loss", "Critic Loss", "Cumulative Loss", "Weights"]

    for i, stat_name in enumerate(stat_names):
        ax = plt.subplot(1, 1, 1)
        plt.title(f"Mean {stat_name} over Epochs")
        plt.ylabel(f"Mean {stat_name}")
        plt.xlabel(f"Epoch (first epoch at 1)")
        plt.plot(np.arange(1, EPOCH_COUNT + 1, 1), statistics[i])
        ax.grid()

        plt.show()

    if DISPLAY_PERFORMANCE:
        display_performance()
