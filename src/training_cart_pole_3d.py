"""
@author William Erignac
@version 2024-09-02

This script runs the 3d cart pole experiment in Unity and uses a gradient descent algorithm on a neural net to learn
a cart pole controller that maximizes its score for the experiment.

Much of the neural net architecture and gradient descent code is from the first example for cart pole in "Deep
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

import argparse
import json
import os
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

import multiprocessing
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

l1 = 2
l2_1 = 32
l2_2 = 32
l3 = 4

model = torch.nn.Sequential(
    torch.nn.Linear(l1, l2_1),
    torch.nn.LeakyReLU(),
    torch.nn.Linear(l2_1, l2_2),
    torch.nn.LeakyReLU(),
    torch.nn.Linear(l2_2, l3),
    torch.nn.Softmax(dim=0)
)

learning_rate = 0.009
optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)

scores = []

def discount_rewards(rewards, gamma=0.99):
    lenr = len(rewards)
    disc_return = torch.pow(gamma,torch.arange(lenr).float()) * rewards #A
    disc_return /= disc_return.max() #B
    return disc_return

def loss_fn(preds, r): #A
    return -1 * torch.sum(r * torch.log(preds)) #B

#endregion Neural Net

#region Initialization Data

class CartPoleData:
    def __init__(self):
        self.goal_generator_seed = 2#np.random.randint(1, 1000)
        self.initial_impulse_seed = np.random.randint(1, 1000)

    def serialize(self):
        return {"GoalGeneratorSeed": self.goal_generator_seed,
                "InitialImpulseSeed": self.initial_impulse_seed}

#endregion InitializationData

#region Running Simulation

def execute_epoch(organisms, sim_inst: UnityInstance):
    # Send the creature initialization data.
    serialize_v = np.vectorize(lambda c: json.dumps(c.serialize()))
    serializations = serialize_v(organisms["Creature"].to_numpy())
    sim_inst.send_session_initialization_data(serializations)
    sim_inst.end_send_session_initialization_data()
    # Read the responses from the simulator and process them
    # this includes starting new creatures, reporting the final
    # scores of creatures, and data about the initial state of creatures.
    read_simulator_responses(organisms, sim_inst)
    # By now "organisms" is updated to have the true scores from the read_simulator_responses thread.
    sorted_organisms = organisms.sort_values("Score", ascending=False)
    print(f'Top Performers:\n{sorted_organisms.head(10)}')

    return sorted_organisms


def read_simulator_responses(organisms: pd.DataFrame, sim_inst: UnityInstance):

    """
    Mapping of creature indexes to running brains. The brains take in simulation frame
    data and create outputs for the running creature simulations.
    """
    running_brains: dict = dict()

    with tqdm(range(organisms.shape[0])) as progress:
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
                    organisms.loc[index, "Score"] = score
                    running_brains[index].on_session_end()
                    del running_brains[index]
                    progress.update(1)
                    #print(f"\tCreature {index} ended")
                else:
                    brain: CreatureBrain = running_brains[index]
                    command = brain.process_frame_data(json.loads(line_split[1]))
                    if not (command is None):
                        to_write = f"{index} {command}"
                        sim_inst.write_line(to_write)
                        sim_inst.flush_pipe()
            else:
                # Otherwise, a creature is starting execution.
                index = int(line_split[0])
                running_brains[index] = CreatureBrain(organisms.loc[index, "Creature"], index)

#region Brain Control

class CreatureBrain:
    def __init__(self, creature: CartPoleData, index: int):
        self._creature = creature
        self._creature_index = index
        self._unity_creature_data = None
        self._data_count = 0

        self.last_state_action = None
        self.transitions = []


    def process_frame_data(self, frame_data: object) -> str:
        """
        Takes a json object sent specifically to this brain and
        returns a command. If None is returned, no command should
        be sent.
        """

        self._data_count += 1
        input, score = CreatureBrain._extract_frame_data(frame_data)

        act_prob = model(torch.from_numpy(input).float())
        action = np.random.choice(range(4), p=act_prob.data.numpy())

        if not self.last_state_action is None:
            self.transitions += [(self.last_state_action[0], self.last_state_action[1], score)]

        self.last_state_action = input, action

        action_command = [(0, 0), (0, 1), (1, 0), (1, 1)][action]

        return json.dumps({'DriveX': float(action_command[0]), 'DriveZ': float(action_command[1])})

    @staticmethod
    def _extract_frame_data(data: dict) -> tuple:

        velocity_difference_x: float = data['Goal']['CartVelocityX'] - data['State']['CartVelocityX']
        velocity_difference_z: float = data['Goal']['CartVelocityZ'] - data['State']['CartVelocityZ']

        data_list = []
        data_list.extend(data['State'].values())
        data_list.extend(data['Goal'].values())
        data_list.extend((velocity_difference_x, velocity_difference_z))
        data_list[:-2] = [0] * (len(data_list) - 2) # Clear to help learn follow goal.

        # np.array(data_list)
        return np.array((velocity_difference_x, velocity_difference_z)), data['Score']

    def on_session_end(self):
        scores.append(self.transitions[-1][2])
        reward_batch = torch.Tensor(np.array([r for (s, a, r) in self.transitions])).flip(dims=(0,))  # J
        disc_returns = discount_rewards(reward_batch)  # K
        state_batch = torch.Tensor(np.array([s for (s, a, r) in self.transitions]))  # L
        action_batch = torch.Tensor(np.array([a for (s, a, r) in self.transitions]))  # M
        pred_batch = model(state_batch)  # N
        prob_batch = pred_batch.gather(dim=1, index=action_batch.long().view(-1, 1)).squeeze()  # O
        loss = loss_fn(prob_batch, disc_returns)
        optimizer.zero_grad()
        loss.backward()
        optimizer.step()

#endregion Brain Control

def save_onnx():
    random_input = torch.rand((l1,), dtype=torch.float32)
    filename = f'cart_pole_3d_agent.onnx'
    torch.onnx.export(model, random_input, filename, input_names=['input'], output_names=['output'])
    return filename


def display_performance():
    display_exec_args = dict()
    display_exec_args["simulator_path"] = SIMULATOR_PATH # TODO: Cover for -t
    display_exec_args["simulator_args"] = DISPLAY_SIMULATOR_ARGS

    if RUN_EXECUTABLE:
        display_sim_inst = UnityInstance(os.path.join(PIPE_PATH, PIPE_NAME), display_exec_args, no_timeout=True)
    else:
        display_sim_inst = sim_inst

    for i in range(5):
        display_sim_inst.run_experiment('cart_pole_3d')
        execute_epoch(pd.DataFrame([[CartPoleData(), 0]], columns=["Creature", "Score"]), display_sim_inst)

    display_sim_inst.quit()

#endregion Running Simulation


if __name__ == "__main__":
    # Set the start method to spawn because we use multithreading, and fork will cause problems.
    multiprocessing.set_start_method("spawn")

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

    # Create an initial population
    ORGANISM_COUNT = 256
    organisms = pd.DataFrame(columns=["Creature", "Score"])
    for i in range(ORGANISM_COUNT):
        organisms.loc[len(organisms.index)] = [CartPoleData(), float(0)]

    if STATS > 0:
        avg_performance_per_epoch = [0]

    exec_args = dict()
    exec_args["simulator_path"] = SIMULATOR_PATH
    exec_args["simulator_args"] = SIMULATOR_ARGS
    sim_inst = UnityInstance(os.path.join(PIPE_PATH, PIPE_NAME), exec_args if RUN_EXECUTABLE else None,
                              no_timeout=True)

    for i in range(EPOCH_COUNT):
        print(f"\nEpoch {i + 1}")
        sim_inst.run_experiment("cart_pole_3d")
        execute_epoch(organisms, sim_inst)
        if STATS > 0:
            avg_performance_per_epoch.append(np.mean(organisms.head(10)["Score"]))

    save_onnx()

    if STATS > 0:
        ax = plt.subplot(1, 1, 1)
        plt.title(f"Performance over Epochs")
        plt.ylabel(f"Score")
        plt.xlabel(f"Epoch (first epoch at 1)")
        plt.plot(np.arange(1, ORGANISM_COUNT + 1, 1), scores)
        ax.grid()

        plt.show()

    if DISPLAY_PERFORMANCE:
        if RUN_EXECUTABLE:
            sim_inst.quit()
        display_performance()
    else:
        sim_inst.quit()
