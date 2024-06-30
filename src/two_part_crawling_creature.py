import argparse
import json
import os
import win32file, win32pipe, win32event, pywintypes
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

import threading
import multiprocessing

from simulation_instance import SimulationInstance

#region Statics

PIPE_PATH = '\\\\.\\pipe\\'
PIPE_NAME = "PipeA"
SIMULATOR_PATH = "../CreatureSimulation/Builds/2024-06-30_16-43/CreatureSimulation.exe"
SIMULATOR_ARGS = ["-batchmode", "-nographics", "-p", PIPE_NAME]
CREATURE_PIPE_PREFIX = "Pipe"

#endregion Statics

#region Initialization Data

class VectorData:
    def __init__(self, values: tuple = None):
        self.values = np.random.random(3) if values is None else values

    def serialize(self):
        return {"x": self.values[0], "y": self.values[1], "z": self.values[2]}


class PartData:
    def __init__(self, size: tuple = None, rotation: tuple = None, connection_point: tuple = None):
        self.size = VectorData(size)
        self.rotation = VectorData(rotation)
        if rotation is None:  # Convert random axes from 0-1 to 0-360
            self.rotation.values *= 360
        self.connection_point = VectorData(connection_point)

    def serialize(self):
        return {"Size": self.size.serialize(),
                "Rotation": self.rotation.serialize(),
                "ConnectionPoint": self.connection_point.serialize()}


class CrawlerData:
    def __init__(self, first: tuple = (), second: tuple = (), pipe_name=""):
        self.first = PartData(*first)
        self.second = PartData(*second)
        self.pipe_name = pipe_name

    def serialize(self):
        return {"First": self.first.serialize(),
                "Second": self.second.serialize(),
                "PipeName": self.pipe_name}

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

    # TODO: Remove
    # has_sent: bool = False

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
                del running_brains[index]
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
            #print(f"\tCreature {index} started")
            running_brains[index] = CreatureBrain(organisms.loc[index, "Creature"], index)
            """
            if not has_sent:
                sim_inst.write_line("0 {}\n1 {}\n2 {}\n3 {}\n4 {}\n5 {}\n6 {}\n7 {}\n8 {}\n9 {}\n10 {}\n11 {}\n12 {}\n13 {}\n14 {}\n15 {}\n"*500)
                sim_inst.flush_pipe()
                has_sent = True
                """

#region Brain Control

class CreatureBrain:
    def __init__(self, creature: CrawlerData, index: int):
        """
        TODO: embed index into CrawlerData (and don't include in json).
        """
        self._creature = creature
        self._creature_index = index
        self._unity_creature_data = None
        self._data_count = 0

    def process_frame_data(self, frame_data: object) -> str:
        """
        Takes a json object sent specifically to this brain and
        returns a command. If None is returned, no command should
        be sent.
        """
        # Called Once
        if self._unity_creature_data == None:
            self._unity_creature_data = frame_data
            return None
        self._data_count += 1
        #print(f"\t\t{self._creature_index} received line {self._data_count} with contents {frame_data}")
        # TODO: Use frame data to make a smart decision.
        return "{}"

#endregion Brain Control

#endregion Running Simulation


if __name__ == "__main__":
    # Set the start method to spawn because we use multithreading, and fork will cause problems.
    multiprocessing.set_start_method("spawn")

    # Parse Arguments
    parser = argparse.ArgumentParser()
    parser.add_argument("-t", help="if this flag is passed, don't run the Unity executable.", action="store_false")
    parser.add_argument("-e", help="number of epochs that should be run.", type=int, default=1)
    parser.add_argument("-display", help="if this flag is passed, display the best performers.", action="store_true")
    parser.add_argument("-stats", help="what types of statistics to show.", type=int, default=0)
    args = parser.parse_args()
    RUN_EXECUTABLE = args.t
    EPOCH_COUNT = args.e
    DISPLAY_BEST_PERFORMERS = args.display
    STATS = args.stats

    # Create an initial population
    organisms = pd.DataFrame(columns=["Creature", "Score"])
    for i in range(256):
        organisms.loc[len(organisms.index)] = [CrawlerData(), 0]

    if DISPLAY_BEST_PERFORMERS:
        best_performers = []

    if STATS > 0:
        avg_performance_per_epoch = [0]

    exec_args = dict()
    exec_args["simulator_path"] = SIMULATOR_PATH
    exec_args["simulator_args"] = SIMULATOR_ARGS
    sim_inst = SimulationInstance(os.path.join(PIPE_PATH, PIPE_NAME), exec_args if RUN_EXECUTABLE else None,
                                  no_timeout=True)

    for i in range(EPOCH_COUNT):
        print(f"\nEpoch {i + 1}")
        sim_inst.run_experiment("crawl")
        execute_epoch(organisms, sim_inst)
        if DISPLAY_BEST_PERFORMERS:
            best_performers.append(organisms.head(1)["Creature"][0])
        if STATS > 0:
            avg_performance_per_epoch.append(np.mean(organisms.head(10)["Score"]))

    sim_inst.quit()

    if DISPLAY_BEST_PERFORMERS:
        display_performers(best_performers)

    if STATS > 0:
        ax = plt.subplot(1, 1, 1)
        plt.title(f"Performance over Epochs")
        plt.ylabel(f"Score")
        plt.xlabel(f"Epoch (first epoch at 1)")
        plt.plot(np.arange(0, EPOCH_COUNT + 1, 1), avg_performance_per_epoch)
        ax.grid()

        plt.show()
