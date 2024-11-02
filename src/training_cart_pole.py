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

from collections import deque
from collections.abc import Callable, Iterable

import torch
import torch.nn as nn
import torch.nn.functional as F
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


"""
l1 = 4
l2 = 25
l3 = 50
l4 = 25

model = torch.nn.Sequential(
    torch.nn.Linear(l1, l2),
    torch.nn.LeakyReLU(),
    torch.nn.Linear(l2, l3),
    torch.nn.LeakyReLU(),
    torch.nn.Linear(l3, 2),
    torch.nn.Softmax(dim=0)
)

critic = torch.nn.Sequential(
    torch.nn.Linear(l1, l2),
    torch.nn.LeakyReLU(),
    torch.nn.Linear(l2, l3),
    torch.nn.LeakyReLU(),
    torch.nn.Linear(l3, l4),
    torch.nn.LeakyReLU(),
    torch.nn.Linear(l4, 1),
    torch.nn.Tanh()
)
"""

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
        actor = F.softmax(self.actor_lin1(y),dim=0) #C
        c = F.relu(self.l3(y.detach()))
        critic = torch.tanh(self.critic_lin1(c)) #D
        return actor, critic #E

model = ActorCritic()

CRITIC_LOSS_CONSTANT = 1
FUTURE_DISCOUNT_FACTOR = 0.95

learning_rate = 1e-4
optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)
optimizer.zero_grad()

scores = []
actor_losses = []
critic_losses = []
cumulative_losses = []
actor_sum_weights = []


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

def execute_epoch(sessions, sim_inst: UnityInstance, learn=True):
    """
    learn: whether to perform gradient descent.
    """
    # Send the session initialization data.
    serialize_v = np.vectorize(lambda c: json.dumps(c.serialize()))
    serializations = serialize_v(sessions["Initial Condition"].to_numpy())
    sim_inst.send_session_initialization_data(serializations)
    sim_inst.end_send_session_initialization_data()
    # Read the responses from the simulator and process them
    # this includes starting new sessions, reporting the final
    # scores of sessions, and data about the initial state of sessions.
    experiences = read_simulator_responses(sessions, sim_inst)
    # Perform gradient descent on the experiences that were had.
    if learn:
        gradient_descent_on_experiences(experiences)
    # By now "sessions" is updated to have the true scores from the read_simulator_responses thread.
    sorted_sessions = sessions.sort_values("Score", ascending=False)

    return sorted_sessions


def read_simulator_responses(starting_conditions: pd.DataFrame, sim_inst: UnityInstance) -> np.ndarray[Experience]:
    """
    Continuously reads the responses given by the simulator.
    Responds by making the agent choose actions.
    When a session has finished, puts the results into a replay buffer and performs gradient descent.
    """

    # Mapping of session indexes to running brains. The brains take in simulation frame
    # data and output actions for the running simulations.
    running_brains: dict[int, AgentBrain] = dict()

    experiences: list[np.ndarray[Experience]] = []

    with tqdm(range(starting_conditions.shape[0]), desc='Sessions', leave=False) as progress:
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
                    _experiences = running_brains[index].on_session_end(score)
                    experiences.append(_experiences)
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
    # Return all the experiences from this epoch.
    return np.concatenate(experiences)


def gradient_descent_on_experiences(experiences: np.ndarray[Experience]) -> None:
    # Get the returns (stored where the rewards were previously stored).
    returns = torch.stack(tuple(Experience.v_get_reward(experiences))).view(-1)
    returns = F.normalize(returns, dim=0)
    # Get the in states from the experience.
    states_in = torch.stack(tuple(Experience.v_get_state_in(experiences))).detach()
    # Get the actions that were chosen during the experience.
    actions = torch.tensor(tuple(Experience.v_get_action(experiences)), dtype=torch.int32).view(-1).detach()
    # Get the probability of the chosen actions for the current actor.
    action_probabilities, state_values = model(states_in.detach())
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
    actor_loss = -1 * (torch.log(action_probabilities) * advantage).sum()
    actor_losses.append(actor_loss.detach().tolist())  # Used for plotting. TODO: Don't use statics.
    # Squared Errors. Similar to linear regression.
    critic_loss = torch.pow(state_values - returns, 2).sum()  #.mean()
    critic_losses.append(critic_loss.detach().tolist())  # Used for plotting. TODO: Don't use statics.
    # Cumulative loss.
    loss = actor_loss + CRITIC_LOSS_CONSTANT * critic_loss
    cumulative_losses.append(loss.detach().tolist())  # Used for plotting. TODO: Don't use statics.
    # Sum of Model Weights
    actor_weights = 0
    for param in model.parameters():
        actor_weights += param.data.sum().detach()
    actor_sum_weights.append(actor_weights)

    # Backwards Propagation.
    optimizer.zero_grad()
    loss.backward()
    optimizer.step()
    optimizer.zero_grad()

#region Brain Control

class AgentBrain:
    def __init__(self, session_initialization_data: CartPoleData, index: int):
        self._session_init = session_initialization_data
        self._session_index = index
        self._data_count = 0
        self._running_score = 0  # The last known score of this agent.

        self.last_state_action = None
        self.transitions = []

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
        act_prob, state_value = model(state)
        action = np.random.choice(np.array([0, 1]), p=act_prob.data.numpy())

        if not self.last_state_action is None:
            self.transitions.append((*self.last_state_action, state, reward))

        self.last_state_action = state, state_value, action, act_prob[action]

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
            data['PoleAngularVelocity']#,
            #data['NormalizedWind']
        ]), data['Score']

    def on_session_end(self, score) -> np.ndarray[Experience]:
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

        # Add to the global list of scores. TODO: Don't make global.
        scores.append(score)

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

    def replay_experiences(self, model: Callable, critic: Callable) -> None:
        """
        Trains the agent by replaying a random batch of experiences.
        """

        # Get a random batch of experiences.
        _batch_size = min(self.mini_batch_size, len(self.replay))
        random_mini_batch = random.sample(self.replay, _batch_size)

        # Assumes rewards have been converted to returns.
        returns = torch.stack(tuple(Experience.v_get_reward(random_mini_batch)))
        # Get the in states from the experience.
        states_in = torch.stack(tuple(Experience.v_get_state_in(random_mini_batch))).detach()
        # Get the actions that were chosen during the experience.
        actions = torch.tensor(tuple(Experience.v_get_action(random_mini_batch)), dtype=torch.int32).view(-1).detach()
        # Get the probability of the chosen actions for the current actor.
        action_probabilities = model(states_in.detach()).gather(dim=1, index=actions.long().view(-1, 1)).squeeze()
        # Get the predicted value of the states for the current critic.
        state_values = critic(states_in.detach()).squeeze()

        # Forces actions that performed better than expected to increase in probability.
        # Actions that performed worse than expected decrease in probability.
        # TODO: Using pow forces each loss to be >= 0, but always inscentivises the action_prob to be 1 (log(1) == 0).
        #  Figure out a way to get always positive loss while inscentivising going towards 0 or 1.
        #  Maybe -1 * log(prob) when (return - state_value) > 0 and -1 * log(1 - prob) otherwise?
        advantage = returns - state_values.detach()

        underestimated_advantage = advantage > 0
        overestimated_advantage = ~underestimated_advantage
        underestimated_loss = (-1 * torch.log(action_probabilities[underestimated_advantage]) * (
        advantage[underestimated_advantage])).sum()
        overestimated_loss = (torch.log(1 - action_probabilities[overestimated_advantage]) * (
        advantage[overestimated_advantage])).sum()

        actor_loss = underestimated_loss + overestimated_loss
        actor_losses.append(actor_loss.detach().tolist())  # Used for plotting. TODO: Don't use statics.
        # Mean Squared Errors. Same as linear regression.
        critic_loss = torch.pow(state_values - returns, 2).sum()
        critic_losses.append(critic_loss.detach().tolist())  # Used for plotting. TODO: Don't use statics.
        # Cumulative loss.
        loss = actor_loss + CRITIC_LOSS_CONSTANT * critic_loss
        cumulative_losses.append(loss.detach().tolist())  # Used for plotting. TODO: Don't use statics.
        # Sum of Model Weights
        actor_weights = 0
        critic_weights = 0
        for param in model.parameters():
            actor_weights += param.data.sum().detach()
        for param in critic.parameters():
            critic_weights += param.data.sum().detach()


        # Backwards Propagation.
        optimizer.zero_grad()
        loss.backward()
        optimizer.step()


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
    display_exec_args["simulator_args"] = DISPLAY_SIMULATOR_ARGS

    if RUN_EXECUTABLE:
        display_sim_inst = UnityInstance(os.path.join(PIPE_PATH, PIPE_NAME), display_exec_args, no_timeout=True)
    else:
        display_sim_inst = sim_inst

    for i in range(5):
        display_sim_inst.run_experiment('cart_pole')
        execute_epoch(
            pd.DataFrame([[CartPoleData(), 0]], columns=["Initial Condition", "Score"]),
            display_sim_inst,
            learn=False  # Display should not change the model.
        )

    display_sim_inst.quit()

#endregion Running Simulation


if __name__ == "__main__":
    # Parse Arguments
    parser = argparse.ArgumentParser()
    parser.add_argument("-t", help="if this flag is passed, don't run the Unity executable.", action="store_false")
    parser.add_argument("-e", help="number of epochs that should be run.", type=int, default=64)
    parser.add_argument("-s", hel="number of sessions per epoch.", type=int, default=16)
    parser.add_argument("-display", help="if this flag is passed, display the agent's performance after training.", action="store_true")
    parser.add_argument("-stats", help="what types of statistics to show.", type=int, default=0)
    args = parser.parse_args()
    RUN_EXECUTABLE = args.t
    EPOCH_COUNT = args.e
    SESSIONS_PER_EPOCH = args.s
    DISPLAY_PERFORMANCE = args.display
    STATS = args.stats

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

    with tqdm(range(EPOCH_COUNT), desc='Epochs') as progress:
        for i in progress:
            # Create the initial states of the sessions.
            sessions = pd.DataFrame(columns=["Initial Condition", "Score"])
            for i in range(SESSIONS_PER_EPOCH):
                sessions.loc[len(sessions.index)] = [CartPoleData(), 0]

            sim_inst.run_experiment("cart_pole")
            execute_epoch(sessions, sim_inst)

            avg_score = np.mean(sessions["Score"])
            progress.set_postfix_str(f'Last Mean Score: {avg_score}')
            if STATS > 0:
                avg_performance_per_epoch.append(avg_score)

    sim_inst.quit()

    save_onnx()

    if STATS > 0:
        ax = plt.subplot(1, 1, 1)
        plt.title(f"Performance over Sessions")
        plt.ylabel(f"Score")
        plt.xlabel(f"Session (first session at 1)")
        plt.plot(np.arange(1, SESSIONS_PER_EPOCH * EPOCH_COUNT + 1, 1), scores)
        ax.grid()

        plt.show()

    if STATS > 1:
        plot_data = [
            ('Actor', 'Loss', actor_losses),
            ('Critic', 'Loss', critic_losses),
            ('Cumulative', 'Loss', cumulative_losses),
            ('Actor', 'Sum Weights', actor_sum_weights)
        ]

        for label, label_type, data in plot_data:
            ax = plt.subplot(1, 1, 1)
            plt.title(f"{label} {label_type} over Epochs")
            plt.ylabel(label_type)
            plt.xlabel(f"Epoch (first session at 1)")
            plt.plot(np.arange(1, EPOCH_COUNT + 1, 1), data)
            ax.grid()

            plt.show()

    if DISPLAY_PERFORMANCE:
        display_performance()
