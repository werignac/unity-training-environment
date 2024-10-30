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
import json
import os
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import random

from collections import deque
from collections.abc import Callable, Iterable

import torch
from tqdm import tqdm

from unity_instance import UnityInstance

#region Statics

PIPE_PATH = '\\\\.\\pipe\\'
PIPE_NAME = "PipeB"
SIMULATOR_PATH = os.environ["UNITY_SIMULATOR_PATH"]
DISPLAY_SIMULATOR_ARGS = ["-p", PIPE_NAME]
SIMULATOR_ARGS = ["-batchmode", "-nographics"] + DISPLAY_SIMULATOR_ARGS
CREATURE_PIPE_PREFIX = "Pipe"

#endregion Statics

#region Neural Net

l1 = 5
l2 = 150
l3 = 2

model = torch.nn.Sequential(
    torch.nn.Linear(l1, l2),
    torch.nn.LeakyReLU(),
    torch.nn.Linear(l2, l3),
    torch.nn.Softmax(dim=0)
)

learning_rate = 0.009
optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)

scores = []


def discount_rewards(rewards, gamma=0.99):
    """
    Discount the rewards for a series of rewards by a factor of gamma.
    """
    lenr = len(rewards)
    # 1 * rewards, gamma ** 2 * rewards, gamma ** 3 * rewards
    disc_return = torch.pow(gamma, torch.arange(lenr).float()) * rewards
    # Normalize
    disc_return /= disc_return.max()
    return disc_return


def loss_fn(preds, r):
    return -1 * torch.sum(r * torch.log(preds))

#endregion Neural Net

#region Initialization Data

class CartPoleData:
    def __init__(self):
        self.wind_seed = np.random.randint(1, 1000)
        self.initial_angle = (0.5 - np.random.rand()) * 2 * 5

    def serialize(self):
        return {"WindSeed": self.wind_seed,
                "InitialAngle": self.initial_angle}

#endregion InitializationData

#region Running Simulation

def execute_epoch(sessions, sim_inst: UnityInstance):
    # Send the session initialization data.
    serialize_v = np.vectorize(lambda c: json.dumps(c.serialize()))
    serializations = serialize_v(sessions["Initial Condition"].to_numpy())
    sim_inst.send_session_initialization_data(serializations)
    sim_inst.end_send_session_initialization_data()
    # Read the responses from the simulator and process them
    # this includes starting new sessions, reporting the final
    # scores of sessions, and data about the initial state of sessions.
    read_simulator_responses(sessions, sim_inst)
    # By now "sessions" is updated to have the true scores from the read_simulator_responses thread.
    sorted_sessions = sessions.sort_values("Score", ascending=False)
    print(f'Top Performers:\n{sorted_sessions.head(10)}')

    return sorted_sessions


def read_simulator_responses(starting_conditions: pd.DataFrame, sim_inst: UnityInstance):
    """
    Continuously reads the responses given by the simulator.
    Responds by making the agent choose actions.
    When a session has finished, puts the results into a replay buffer and performs gradient descent.
    """

    # Mapping of session indexes to running brains. The brains take in simulation frame
    # data and output actions for the running simulations.
    running_brains: dict[int, AgentBrain] = dict()

    replay: ExperienceReplay = ExperienceReplay(1500, 500)  # TODO: include ways to specify hyperparameters.

    with tqdm(range(starting_conditions.shape[0])) as progress:
        while True:
            line = sim_inst.read_line()

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
                    running_brains[index].on_session_end(replay)
                    del running_brains[index]
                    progress.update(1)
                else:
                    # Otherwise, if this is data about session in progress,
                    # give the running brain the new state and get the next
                    # command to run for that session.
                    brain: AgentBrain = running_brains[index]
                    command = brain.process_frame_data(json.loads(line_split[1]))
                    if not (command is None):
                        to_write = f"{index} {command}"
                        sim_inst.write_line(to_write)
                        sim_inst.flush_pipe()
            else:
                # Otherwise, a session is starting execution.
                index = int(line_split[0])
                running_brains[index] = AgentBrain(starting_conditions.loc[index, "Initial Condition"], index)

#region Brain Control

class AgentBrain:
    def __init__(self, session_initialization_data: CartPoleData, index: int):
        self._session_init = session_initialization_data
        self._session_index = index
        self._data_count = 0

        self.last_state_action = None
        self.transitions = []

    def process_frame_data(self, frame_data: dict) -> str:
        """
        Takes a json object sent specifically to this brain and
        returns a command. If None is returned, no command should
        be sent.
        """
        self._data_count += 1
        input, score = AgentBrain._extract_frame_data(frame_data)

        act_prob = model(torch.from_numpy(input).float())
        action = np.random.choice(np.array([0, 1]), p=act_prob.data.numpy())

        if not self.last_state_action is None:
            self.transitions += [(self.last_state_action[0], self.last_state_action[1], score)]

        self.last_state_action = input, action

        return json.dumps({'MoveRight': bool(action == 0)})

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
            data['PoleAngularVelocity'],
            data['NormalizedWind']
        ]), data['Score']

    def on_session_end(self, replay: ExperienceReplay):
        """
        When a session has ended, gather all the rewards, states, and actions, and perform
        gradient descent.
        """
        ep_len = len(self.transitions)
        scores.append(ep_len)
        # Isolate the rewards.
        reward_batch = torch.Tensor([r for (s, a, r) in self.transitions])
        # Reverse the rewards so that the earliest actions are rewarded the most, while later actions are not rewarded as much.
        # Apply a future discount based on a decay rate of gamma (0.99).
        disc_returns = discount_rewards(reward_batch.flip(dims=(0,)))
        # Isolate the states.
        state_batch = torch.Tensor([s for (s, a, r) in self.transitions])
        # Isolate teh actions.
        action_batch = torch.Tensor([a for (s, a, r) in self.transitions])

        """
        # Run gradient descent on results.
        # Predict actions, independently of the actions that were actually taken.
        pred_batch = model(state_batch)
        # Get the probabilities of actions that were previously taken.
        prob_batch = pred_batch.gather(dim=1, index=action_batch.long().view(-1, 1)).squeeze()
        # Calculate the loss for the new probabilities of old actions.
        loss = loss_fn(prob_batch, disc_returns)  # TODO: Make loss function an argument.

        # Perform Gradient Descent
        optimizer.zero_grad()
        loss.backward()
        optimizer.step()
        """

        # Construct experiences for replay.
        v_experience_constructor = np.vectorize(Experience, excluded="state_out", signature='(n),(),(),()->()')
        experiences = v_experience_constructor(state_batch, action_batch, None, disc_returns)

        # Add the experiences and try to replay.
        replay.add_experiences(experiences)
        # TODO: Uncomment
        replay.replay_experiences(model)

#endregion Brain Control

#region Experience Replay

class Experience:
    def __init__(
            self,
            state_in: torch.Value,
            action: torch.Value,
            state_out: torch.Value,
            reward: torch.Value
    ):
        self.state_in = state_in
        self.action = action
        self.state_out = state_out
        self.reward = reward

    def get_state_in(self) -> torch.Value:
        return self.state_in

    def get_action(self) -> torch.Value:
        return self.action

    def get_state_out(self) -> torch.Value:
        return self.state_out

    def get_reward(self) -> torch.Value:
        return self.reward

    def get_all(self) -> tuple[torch.Value, torch.Value, torch.Value, torch.Value]:
        return self.state_in, self.action, self.state_out, self.reward

    # Vectorized Getters
    v_get_state_in: Callable[[Iterable[Experience]], Iterable[torch.Value]] = np.vectorize(
        get_state_in,
        signature='()->(n)'
    )
    v_get_action: Callable[[Iterable[Experience]], Iterable[torch.Value]] = np.vectorize(get_action)
    v_get_state_out: Callable[[Iterable[Experience]], Iterable[torch.Value]] = np.vectorize(get_state_out)
    v_get_reward: Callable[[Iterable[Experience]], Iterable[torch.Value]] = np.vectorize(get_reward)
    v_get_all: Callable[[Iterable[Experience]], Iterable[Iterable[torch.Value]]] = np.vectorize(
        get_all,
        signature='()->(n),(),(),()'  # TODO: Change 3rd output to n when state is not None.
    )


class ExperienceReplay:  # TODO: Include locks for parallelization.
    def __init__(
            self,
            saved_experiences_size: int = 10000,
            mini_batch_size: int = 500
    ):
        # Queue of saved experiences.
        self.replay: deque[Experience] = deque(maxlen=saved_experiences_size)
        # How many experiences are picked out of the replay buffer when we want to do
        self.mini_batch_size = mini_batch_size

        assert self.mini_batch_size <= saved_experiences_size, f"Cannot sample more experiences than the amount saved. {self.mini_batch_size} <= {saved_experiences_size}"

    def add_experiences(self, experiences: Iterable[Experience]) -> None:
        """
        Adds experiences to the experience replay buffer.
        """
        self.replay.extend(experiences)

    def replay_experiences(self, model: Callable) -> None:
        """
        Trains the agent by replaying a random batch of experiences.
        """

        # Get a random batch of experiences.
        random_mini_batch = random.sample(self.replay, min(self.mini_batch_size, len(self.replay)))
        # Separate the experiences out by parts.
        states_in, actions, states_out, rewards = Experience.v_get_all(random_mini_batch)

        # Convert to tensors.
        states_in = torch.Tensor(states_in)
        actions = torch.Tensor(actions)
        # TODO: Remove. Kept for completeness.
        # states_out = torch.Tensor(states_out)
        rewards = torch.Tensor(rewards)

        # Predict actions, independently of the actions that were actually taken.
        pred_batch = model(states_in)
        # Get the probabilities of actions that were previously taken.
        prob_batch = pred_batch.gather(dim=1, index=actions.long().view(-1, 1)).squeeze()
        # Calculate the loss for the new probabilities of old actions.
        # If the old action was bad, and we have a low probability for it, the probability should not change.
        # If the old action was good, and we have a low probability for it, the probability should change.
        # If the old action was bad, and we have a high probability for it, the probability should change.
        # If the old action was good, and we have a high probability for it, the probability should not change.
        loss = loss_fn(prob_batch, rewards)  # TODO: Make loss function an argument.

        # Perform Gradient Descent
        optimizer.zero_grad()
        loss.backward()
        optimizer.step()



#endregion Experience Replay

def save_onnx():
    """
    Save the cart pole agent as an onnx file.
    """
    random_input = torch.rand((l1,), dtype=torch.float32)
    filename = f'cart_pole_agent.onnx'
    torch.onnx.export(model, random_input, filename, input_names=['input'], output_names=['output'])
    return filename

def display_performance():
    """
    Show the final cart pole agent playing 5 rounds.
    """
    display_exec_args = dict()
    display_exec_args["simulator_path"] = SIMULATOR_PATH
    display_exec_args["simulator_args"] = DISPLAY_SIMULATOR_ARGS

    if RUN_EXECUTABLE:
        display_sim_inst = UnityInstance(os.path.join(PIPE_PATH, PIPE_NAME), display_exec_args, no_timeout=True)
    else:
        display_sim_inst = sim_inst

    for i in range(5):
        display_sim_inst.run_experiment('cart_pole')
        execute_epoch(pd.DataFrame([[CartPoleData(), 0]], columns=["Initial Condition", "Score"]), display_sim_inst)

    display_sim_inst.quit()

#endregion Running Simulation


if __name__ == "__main__":
    # Parse Arguments
    parser = argparse.ArgumentParser()
    parser.add_argument("-t", help="if this flag is passed, don't run the Unity executable.", action="store_false")
    parser.add_argument("-e", help="number of epochs that should be run.", type=int, default=1)
    parser.add_argument("-display", help="if this flag is passed, display the agent's performance after training.", action="store_true")
    parser.add_argument("-stats", help="what types of statistics to show.", type=int, default=0)
    args = parser.parse_args()
    RUN_EXECUTABLE = args.t
    EPOCH_COUNT = args.e
    DISPLAY_PERFORMANCE = args.display
    STATS = args.stats

    # Create the initial states of the sessions.
    sessions = pd.DataFrame(columns=["Initial Condition", "Score"])
    for i in range(1024 * 4):
        sessions.loc[len(sessions.index)] = [CartPoleData(), 0]

    if STATS > 0:
        avg_performance_per_epoch = [0]

    exec_args = dict()
    exec_args["simulator_path"] = SIMULATOR_PATH
    exec_args["simulator_args"] = SIMULATOR_ARGS
    sim_inst = UnityInstance(os.path.join(PIPE_PATH, PIPE_NAME), exec_args if RUN_EXECUTABLE else None,
                              no_timeout=True)

    if RUN_EXECUTABLE:
        print(f"Running Simulator: {SIMULATOR_PATH}")
    else:
        print(f"Connecting to Simulator without subprocess")

    for i in range(EPOCH_COUNT):
        print(f"\nEpoch {i + 1}")
        sim_inst.run_experiment("cart_pole")
        execute_epoch(sessions, sim_inst)
        if STATS > 0:
            avg_performance_per_epoch.append(np.mean(sessions.head(10)["Score"]))

    sim_inst.quit()

    save_onnx()

    if STATS > 0:
        ax = plt.subplot(1, 1, 1)
        plt.title(f"Performance over Epochs")
        plt.ylabel(f"Score")
        plt.xlabel(f"Epoch (first epoch at 1)")
        plt.plot(np.arange(1, 1024 * 4 + 1, 1), scores)
        ax.grid()

        plt.show()

    if DISPLAY_PERFORMANCE:
        display_performance()
