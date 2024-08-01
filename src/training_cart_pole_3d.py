import argparse
import json
import os
import win32file, win32pipe, win32event, pywintypes
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

import threading
import multiprocessing
import torch
import torch.nn as nn
import torch.functional as F
from tqdm import tqdm

from simulation_instance import SimulationInstance

#region Statics

PIPE_PATH = '\\\\.\\pipe\\'
PIPE_NAME = "PipeB"
SIMULATOR_PATH = "../CreatureSimulation/Builds/2024-07-27_00-46/CreatureSimulation.exe"
DISPLAY_SIMULATOR_ARGS = ["-p", PIPE_NAME]
SIMULATOR_ARGS = ["-batchmode", "-nographics"] + DISPLAY_SIMULATOR_ARGS
CREATURE_PIPE_PREFIX = "Pipe"

#endregion Statics

#region Neural Net

l1 = 4 #A
l2 = 150
l3 = 2 #B

model = torch.nn.Sequential(
    torch.nn.Linear(l1, l2),
    torch.nn.LeakyReLU(),
    torch.nn.Linear(l2, l3),
    torch.nn.Softmax(dim=0) #C
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
        self.goal_generator_seed = np.random.randint(1, 1000)
        self.initial_impulse_seed = np.random.randint(1, 1000)

    def serialize(self):
        return {"GoalGeneratorSeed": self.goal_generator_seed,
                "InitialImpulseSeed": self.initial_impulse_seed}

#endregion InitializationData

#region Running Simulation

def execute_epoch(organisms, sim_inst: SimulationInstance):
    # Send the creature initialization data.
    serialize_v = np.vectorize(lambda c: json.dumps(c.serialize()))
    serializations = serialize_v(organisms["Creature"].to_numpy())
    sim_inst.send_creatures(serializations)
    sim_inst.end_send_creatures()
    # Read the responses from the simulator and process them
    # this includes starting new creatures, reporting the final
    # scores of creatures, and data about the initial state of creatures.
    read_simulator_responses(organisms, sim_inst)
    # By now "organisms" is updated to have the true scores from the read_simulator_responses thread.
    sorted_organisms = organisms.sort_values("Score", ascending=False)
    print(f'Top Performers:\n{sorted_organisms.head(10)}')

    return sorted_organisms


def read_simulator_responses(organisms: pd.DataFrame, sim_inst: SimulationInstance):

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

        print(frame_data)

        """
        self._data_count += 1
        input, score = CreatureBrain._extract_frame_data(frame_data)

        act_prob = model(torch.from_numpy(input).float())
        action = np.random.choice(np.array([0, 1]), p=act_prob.data.numpy())

        if not self.last_state_action is None:
            self.transitions += [(self.last_state_action[0], self.last_state_action[1], score)]

        self.last_state_action = input, action
        """

        return json.dumps({'DriveX': 1.0, 'DriveZ': 0.5})

    @staticmethod
    def _extract_frame_data(data) -> tuple:
        return np.array([
            data['CartPosition'],
            data['CartVelocity'],
            data['PoleAngle'],
            data['PoleAngularVelocity']
        ]), data['Score']

    def on_session_end(self):
        """
        ep_len = len(self.transitions)  # I
        scores.append(ep_len)
        reward_batch = torch.Tensor([r for (s, a, r) in self.transitions]).flip(dims=(0,))  # J
        disc_returns = discount_rewards(reward_batch)  # K
        state_batch = torch.Tensor([s for (s, a, r) in self.transitions])  # L
        action_batch = torch.Tensor([a for (s, a, r) in self.transitions])  # M
        pred_batch = model(state_batch)  # N
        prob_batch = pred_batch.gather(dim=1, index=action_batch.long().view(-1, 1)).squeeze()  # O
        loss = loss_fn(prob_batch, disc_returns)
        optimizer.zero_grad()
        loss.backward()
        optimizer.step()
        """

#endregion Brain Control

def save_onnx():
    """
    random_input = torch.rand((4,), dtype=torch.float32)
    filename = f'cart_pole_agent.onnx'
    torch.onnx.export(model, random_input, filename, input_names=['input'], output_names=['output'])
    return filename
    """

def display_performance():
    display_exec_args = dict()
    display_exec_args["simulator_path"] = SIMULATOR_PATH # TODO: Cover for -t
    display_exec_args["simulator_args"] = DISPLAY_SIMULATOR_ARGS

    if RUN_EXECUTABLE:
        display_sim_inst = SimulationInstance(os.path.join(PIPE_PATH, PIPE_NAME), display_exec_args, no_timeout=True)
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
    organisms = pd.DataFrame(columns=["Creature", "Score"])
    for i in range(256):
        organisms.loc[len(organisms.index)] = [CartPoleData(), float(0)]

    if STATS > 0:
        avg_performance_per_epoch = [0]

    exec_args = dict()
    exec_args["simulator_path"] = SIMULATOR_PATH
    exec_args["simulator_args"] = SIMULATOR_ARGS
    sim_inst = SimulationInstance(os.path.join(PIPE_PATH, PIPE_NAME), exec_args if RUN_EXECUTABLE else None,
                                  no_timeout=True)

    for i in range(EPOCH_COUNT):
        print(f"\nEpoch {i + 1}")
        sim_inst.run_experiment("cart_pole_3d")
        execute_epoch(organisms, sim_inst)
        if STATS > 0:
            avg_performance_per_epoch.append(np.mean(organisms.head(10)["Score"]))

    sim_inst.quit()

    save_onnx()

    if STATS > 0:
        ax = plt.subplot(1, 1, 1)
        plt.title(f"Performance over Epochs")
        plt.ylabel(f"Score")
        plt.xlabel(f"Epoch (first epoch at 1)")
        plt.plot(np.arange(1, 256 + 1, 1), scores)
        ax.grid()

        plt.show()

    if DISPLAY_PERFORMANCE:
        display_performance()
