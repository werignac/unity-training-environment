import numpy as np
import numpy.random
import pandas as pd
import random
import matplotlib.pyplot as plt
import os
import json
import win32file, win32pipe
import argparse
import subprocess

PIPE_PATH = '\\\\.\\pipe\\'
PIPE_NAME = "Pipe3"
TERMINATOR = "END"
SIMULATOR_PATH = "CreatureSimulation\\Builds\\03-02-2024_04-08\\CreatureSimulation.exe"
SIMULATOR_ARGS = ["-batchmode", "-nographics", "-p", PIPE_NAME]

#region Genetic Algorithm

class RectPrism:
    def __init__(self, scale_vector=None, rotation_vector=None):
        self.scale = np.random.random(3) if scale_vector is None else scale_vector
        self.rotation = np.random.random(3) * 90 if rotation_vector is None else rotation_vector

    def serialize(self):
        return {"XScale": self.scale[0], "YScale": self.scale[1], "ZScale": self.scale[2],
                "XRot": self.rotation[0], "YRot": self.rotation[1], "ZRot": self.rotation[2]}

    def __str__(self):
        return f"Location: <{self.scale}>, Rotation: <{self.rotation}>"

    def sexual_mutation(self, other) -> object:
        scale = np.empty(3, dtype=float)
        for i in range(scale.shape[0]):
            scale[i] = np.random.choice([self.scale[i], other.scale[i]])
        rotation = np.empty(3, dtype=float)
        for i in range(rotation.shape[0]):
            rotation[i] = np.random.choice([self.rotation[i], other.rotation[i]])
        return RectPrism(scale, rotation)

    def asexual_mutation(self, switch_chance=0.3, modify_chance=0.6):
        scale = np.copy(self.scale)
        rotation = np.copy(self.rotation)
        attributes = [scale, rotation]
        while random.random() < modify_chance:
            attribute = attributes[np.random.randint(0, len(attributes) - 1)]
            attribute[np.random.randint(0, 2)] *= np.random.rand() * 0.2 + 0.9
        while random.random() < switch_chance:
            attribute_1 = attributes[np.random.randint(0, len(attributes) - 1)]
            index_1 = np.random.randint(0, 2)
            attribute_2 = attributes[np.random.randint(0, len(attributes) - 1)]
            index_2 = np.random.randint(0, 2)
            temp = attribute_1[index_1]
            attribute_1[index_1] = attribute_2[index_2]
            attribute_2[index_2] = temp
        return RectPrism(scale, rotation)


def reproduction(scored_organisms: pd.DataFrame, new_population_count=None, sexual_to_asexual_percent=0.5):
    scored_organisms = scored_organisms.sort_values("Score", ascending=False)

    if new_population_count is None:
        new_population_count = scored_organisms.shape[0]

    sexual_reproductions = int(new_population_count * sexual_to_asexual_percent)
    asexual_reproductions = new_population_count - sexual_reproductions

    new_organisms = pd.DataFrame(columns=["Creature", "Score"])

    np_last_organisms = scored_organisms["Creature"].to_numpy()
    np_scores = scored_organisms["Score"].to_numpy()
    success_probability_distribution = (np_scores / np.sum(np_scores)).astype(float)

    sexual_pairs = numpy.random.choice(np_last_organisms, (sexual_reproductions, 2), p=success_probability_distribution)
    asexual_individuals = numpy.random.choice(np_last_organisms, asexual_reproductions, p=success_probability_distribution)

    to_add = np.apply_along_axis(lambda row: [row[0].sexual_mutation(row[1]), 0], 1, sexual_pairs)
    new_organisms = pd.concat([ new_organisms, pd.DataFrame(to_add, columns=["Creature", "Score"])])

    RectPrism().asexual_mutation()

    to_add = np.vectorize(lambda x: x.asexual_mutation())(asexual_individuals)
    scores_to_add = np.zeros(asexual_reproductions)
    new_organisms = pd.concat([ new_organisms, pd.DataFrame({"Creature": to_add, "Score": scores_to_add}, columns=["Creature", "Score"])])

    return new_organisms.reset_index(drop=True)

#endregion

#region Simulation Initialization

def run_simulator(simulator_path=SIMULATOR_PATH, simulator_args=SIMULATOR_ARGS):
    # From https://www.codeproject.com/Questions/5340484/How-to-send-back-data-through-Python-to-Csharp-thr
    pipe_handle = win32pipe.CreateNamedPipe(
        PIPE_PATH + PIPE_NAME,
        win32pipe.PIPE_ACCESS_DUPLEX,
        win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
        1, 65536, 65536,
        0,
        None)

    if RUN_EXECUTABLE:
        # Run on a different thread. run waits until the process has finished.
        subprocess.Popen([simulator_path] + simulator_args)

    win32pipe.ConnectNamedPipe(pipe_handle, None)

    return pipe_handle


def run_simulation(simulation_name, pipe_handle=None, *prelines, **simulator_args):
    if pipe_handle is None:
        pipe_handle = run_simulator(**simulator_args)

    for line in prelines:
        ret, length = win32file.WriteFile(pipe_handle, (line + "\n").encode())

    ret, length = win32file.WriteFile(pipe_handle, f"run {simulation_name}\n".encode())

    #TODO: Check for an error / warnings. I'll need to add a cornfirmation that the simulation was set up propertly.

    return pipe_handle

#endregion

#region Pipe
def write_line_pipe(pipe_handle, line, flush: bool = False):
    ret, length = win32file.WriteFile(pipe_handle, f"{line}\n".encode())
    if flush: #TODO: Add error checking?
        win32file.FlushFileBuffers(pipe_handle)

def close_pipe(pipe_handle):
    win32file.FlushFileBuffers(pipe_handle)
    win32pipe.DisconnectNamedPipe(pipe_handle)
    win32file.CloseHandle(pipe_handle)
#endregion

#region Running Simulation
def execute_epoch(organisms, pipe_handle):
    for i in range(organisms.shape[0]):
        creature = organisms["Creature"][i]
        message = json.dumps(creature.serialize()) + "\n"
        ret, length = win32file.WriteFile(pipe_handle, message.encode())

    ret, length = win32file.WriteFile(pipe_handle, f"{TERMINATOR}\n".encode())
    win32file.FlushFileBuffers(pipe_handle)

    composite_message = ""
    while not composite_message.endswith(TERMINATOR + '\r\n'):
        ret, read_message = win32file.ReadFile(pipe_handle, 1000)
        composite_message += read_message.decode()
        split_composite_message = composite_message.split('\r\n')
        next_substring_index = 0
        for i in range(len(split_composite_message) - 1):
            result_str = split_composite_message[i]
            if result_str == TERMINATOR:
                break
            result_str_split = result_str.split(' ')
            row = organisms.loc[int(result_str_split[0])]
            row["Score"] = float(result_str_split[1])
            organisms.loc[int(result_str_split[0])] = row
            next_substring_index += len(result_str) + 2

        composite_message = composite_message[next_substring_index:]

    sorted_organisms = organisms.sort_values("Score", ascending=False)

    print(f'Top Performers:\n{sorted_organisms.head(10)}')

    return sorted_organisms


def display_performers(best_performers):
    args = SIMULATOR_ARGS.copy()
    if "-batchmode" in args:
        args.remove("-batchmode")
    if "-nographics" in args:
        args.remove("-nographics")
    pipe_handle = run_simulator(simulator_args=args)

    for performer in best_performers:
        run_simulation("falling_rectangular_prism", pipe_handle=pipe_handle)

        message = json.dumps(performer.serialize()) + "\n"
        ret, length = win32file.WriteFile(pipe_handle, message.encode())
        ret, length = win32file.WriteFile(pipe_handle, f"{TERMINATOR}\n".encode())
        win32file.FlushFileBuffers(pipe_handle)

        composite_message = ""
        while not composite_message.endswith(TERMINATOR + '\r\n'):
            ret, read_message = win32file.ReadFile(pipe_handle, 1000)
            composite_message += read_message.decode()

    write_line_pipe(pipe_handle, "quit", flush=True)
    composite_message = ""
    while not composite_message.endswith('QUIT\r\n'):
        ret, read_message = win32file.ReadFile(pipe_handle, 1000)
        composite_message += read_message.decode()
    close_pipe(pipe_handle)

#endregion


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("-t", help="if this flag is passed, don't run the Unity executable.", action="store_false")
    parser.add_argument("-e", help="number of epochs that should be run.", type=int, default=10)
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
        organisms.loc[len(organisms.index)] = [RectPrism(), 0]

    if DISPLAY_BEST_PERFORMERS:
        best_performers = []

    if STATS > 0:
        avg_performance_per_epoch = [0]

    # From https://www.codeproject.com/Questions/5340484/How-to-send-back-data-through-Python-to-Csharp-thr
    pipe_handle = run_simulator()

    for i in range(EPOCH_COUNT):
        print(f"\nEpoch {i + 1}")
        run_simulation("falling_rectangular_prism", pipe_handle=pipe_handle)
        execute_epoch(organisms, pipe_handle)
        if DISPLAY_BEST_PERFORMERS:
            best_performers.append(organisms.head(1)["Creature"][0])
        if STATS > 0:
            avg_performance_per_epoch.append(np.mean(organisms.head(10)["Score"]))
        organisms = reproduction(organisms)

    write_line_pipe(pipe_handle, "quit", flush=True)
    composite_message = ""
    while not composite_message.endswith('QUIT\r\n'):
        ret, read_message = win32file.ReadFile(pipe_handle, 1000)
        composite_message += read_message.decode()
    close_pipe(pipe_handle)


    if DISPLAY_BEST_PERFORMERS:
        display_performers(best_performers)

    if STATS > 0:
        ax = plt.subplot(1, 1, 1)
        plt.title(f"Performance over Epochs")
        plt.ylabel(f"Score")
        plt.xlabel(f"Epoch (first epoch at 1)")
        plt.plot(np.arange(0, EPOCH_COUNT + 1, 1), avg_performance_per_epoch)
        plt.legend()
        ax.grid()

        plt.show()




