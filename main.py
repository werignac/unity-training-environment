import numpy as np
import numpy.random
import pandas as pd
import random
import os

import json

import win32file
import win32pipe

import argparse

import subprocess

PIPE_PATH = '\\\\.\\pipe\\'
PIPE_NAME = "Pipe"
TERMINATOR = "END"
SIMULATOR_PATH = "CreatureSimulation\\Builds\\02-23-2024_11-51\\CreatureSimulation.exe"
SIMULATOR_ARGS = ["-batchmode", "-nographics", "-p", PIPE_NAME]

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


def execute_epoch(organisms):
    # From https://www.codeproject.com/Questions/5340484/How-to-send-back-data-through-Python-to-Csharp-thr
    pipe_handle = run_simulator()

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

    win32file.FlushFileBuffers(pipe_handle)
    win32pipe.DisconnectNamedPipe(pipe_handle)
    win32file.CloseHandle(pipe_handle)

    return sorted_organisms


def display_performers(best_performers):
    for performer in best_performers:
        args = SIMULATOR_ARGS.copy()
        args.remove("-batchmode")
        args.remove("-nographics")
        pipe_handle = run_simulator(simulator_args=args)

        message = json.dumps(performer.serialize()) + "\n"
        ret, length = win32file.WriteFile(pipe_handle, message.encode())
        ret, length = win32file.WriteFile(pipe_handle, f"{TERMINATOR}\n".encode())
        win32file.FlushFileBuffers(pipe_handle)

        composite_message = ""
        while not composite_message.endswith(TERMINATOR + '\r\n'):
            ret, read_message = win32file.ReadFile(pipe_handle, 1000)
            composite_message += read_message.decode()

        win32file.FlushFileBuffers(pipe_handle)
        win32pipe.DisconnectNamedPipe(pipe_handle)
        win32file.CloseHandle(pipe_handle)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("-t", help="if this flag is passed, don't run the Unity executable.", action="store_false")
    parser.add_argument("-e", help="number of epochs that should be run.", type=int, default=10)
    args = parser.parse_args()
    RUN_EXECUTABLE = args.t
    EPOCH_COUNT = args.e

    # Create an initial population
    organisms = pd.DataFrame(columns=["Creature", "Score"])
    for i in range(256):
        organisms.loc[len(organisms.index)] = [RectPrism(), 0]

    best_performers = []
    for i in range(EPOCH_COUNT):
        print(f"\nEpoch {i}")
        execute_epoch(organisms)
        best_performers.append(organisms.head(1)["Creature"][0])
        organisms = reproduction(organisms)

    display_performers(best_performers)



