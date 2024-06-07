import argparse
import json
import os
import win32file, win32pipe, win32event, pywintypes
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

import threading
import multiprocessing

from simulation_instance import SimulationInstance, SimulationTask

#region Statics

PIPE_PATH = '\\\\.\\pipe\\'
PIPE_NAME = "PipeA"
SIMULATOR_PATH = "../CreatureSimulation/Builds/2024-06-02_04-00/CreatureSimulation.exe"
SIMULATOR_ARGS = ["-batchmode", "-nographics", "-p", PIPE_NAME]
CREATURE_PIPE_PREFIX = "Pipe"

#endregion Statics

#region Initialization Data

class VectorData:
    def __init__(self, values=None):
        self.values = np.random.random(3) if values is None else values

    def serialize(self):
        return {"x": self.values[0], "y": self.values[1], "z": self.values[2]}


class PartData:
    def __init__(self, size=None, rotation=None, connection_point=None):
        self.size = VectorData(size)
        self.rotation = VectorData(rotation)
        self.connection_point = VectorData(connection_point)

    def serialize(self):
        return {"Size": self.size.serialize(),
                "Rotation": self.rotation.serialize(),
                "ConnectionPoint": self.connection_point.serialize()}


class CrawlerData:
    def __init__(self, first=(), second=(), pipe_name=""):
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

    # Start thread to read creatures.
    read_sim_responses_thread = threading.Thread(target=read_simulator_responses, args=(organisms, sim_inst))
    # read_simulator_responses changes "organisms" in-place.
    read_sim_responses_thread.start()

    serialize_v = np.vectorize(lambda c: json.dumps(c.serialize()))
    serializations = serialize_v(organisms["Creature"].to_numpy())
    sim_inst.send_creatures(serializations)
    sim_inst.end_send_creatures()

    read_sim_responses_thread.join()
    # by now "organisms" is updated to have the true scores from the read_simulator_responses thread.
    sorted_organisms = organisms.sort_values("Score", ascending=False)
    print(f'Top Performers:\n{sorted_organisms.head(10)}')

    return sorted_organisms


def read_simulator_responses(organisms, sim_inst):
    while True:
        line = sim_inst.read_line()

        if line is None:
            break

        line_split = line.split(" ")

        # If there is a space, this is a score that is being reported.
        if len(line_split) > 1:
            index = int(line_split[0])
            score = float(line_split[1])
            organisms.loc[index, "Score"] = score
        else:
            # Otherwise, a creature is starting execution.
            index = int(line_split[0])
            # Handle sending actions in parallel.
            p = multiprocessing.Process(target=run_brain_for_creature, args=(organisms["Creature"][index],))
            p.start()
            #t = threading.Thread(target=run_brain_for_creature, args=(organisms["Creature"][index],))
            #t.start()

#region Brain Control


def run_brain_for_creature(creature):
    brain = CreatureBrain(os.path.join(PIPE_PATH, creature.pipe_name))
    brain.continuous_read()
    brain.close()


# Inherits from SimulationTask to reduce redundant coding. Should probably inherit from a basic communication class.
class CreatureBrainTask(SimulationTask):
    def __init__(self, pipe_handle, overlap, **kwargs):
        SimulationTask.__init__(self, pipe_handle, overlap, **kwargs)

        self.has_received_end = False

    def _get_finished_pipe_reading(self):
        return self.has_received_end

    def _on_read_line_from_pipe(self, line):
        if not SimulationTask._on_read_line_from_pipe(self, line):
            return False

        if line == "END":
            self.has_received_end = True
            return False

        return True


class CreatureBrain:
    def __init__(self, pipe_path_and_name:str):
        self.pipe_and_path_name = pipe_path_and_name
        self.line_increment = 0

        print(f'Pipe "{self.pipe_and_path_name}" Creating...')
        # From https://www.codeproject.com/Questions/5340484/How-to-send-back-data-through-Python-to-Csharp-thr
        self.pipe_handle = win32pipe.CreateNamedPipe(
            pipe_path_and_name,
            win32pipe.PIPE_ACCESS_DUPLEX | win32file.FILE_FLAG_OVERLAPPED,
            win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
            1, 65536, 65536,
            0,
            None)

        self.overlap = pywintypes.OVERLAPPED()
        self.overlap.hEvent = win32event.CreateEvent(None, 0, 0, None)
        win32pipe.ConnectNamedPipe(self.pipe_handle, self.overlap)
        print(f'Pipe "{self.pipe_and_path_name}" Connected.')
        win32file.GetOverlappedResult(self.pipe_handle, self.overlap, True)

        self.task: SimulationTask = CreatureBrainTask(self.pipe_handle, self.overlap)

    def continuous_read(self):
        is_first_line = True
        line = ""

        # TODO: Remove. Used for testing true hanging on missing line.
        self.task.write_line("\r\n" * 499)
        self.task.flush()

        while True:
            try:
                line = self.task.read_line()
                self.line_increment += 1
                #print(f"Pipe {self.pipe_and_path_name} completed line {self.line_increment - 1}")
            except:
                print(f'Timeout for pipe {self.pipe_and_path_name} on line {self.line_increment}/500 : "{line}"')

            if not line:
                break

            if is_first_line:
                is_first_line = False
            else:
                '''
                self.task.write_line(f"{self.line_increment}")
                self.task.flush()
                '''


    def close(self):
        print(f'Pipe "{self.pipe_and_path_name}" Quitting...')
        self.task.write_line("QUIT")
        win32file.FlushFileBuffers(self.pipe_handle)
        win32pipe.DisconnectNamedPipe(self.pipe_handle)
        win32file.CloseHandle(self.pipe_handle)
        print(f'Pipe "{self.pipe_and_path_name}" Closed.')

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
        organisms.loc[len(organisms.index)] = [CrawlerData(
            ((1, 1, 1), (0, 0, 0), (0.5, 1, 0.5)),
            ((0.5, 1, 0.5), (45, 0, 0), (0.5, 0, 0.5)),
            CREATURE_PIPE_PREFIX + str(i)
        ), 0]

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
