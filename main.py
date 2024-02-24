import numpy as np
import pandas as pd
import random
import os

import json

import win32file
import win32pipe

import argparse

import subprocess


class RectPrism:
    def __init__(self):
        self.XScale = random.random()
        self.YScale = random.random()
        self.ZScale = random.random()
        self.XRot = random.random() * 90
        self.YRot = random.random() * 90
        self.ZRot = random.random() * 90

    def __str__(self):
        return f"Location: <{self.XScale}, {self.YScale}, {self.ZScale}>, Rotation: <{self.XRot}, {self.YRot}, {self.ZRot}>"


if __name__ == "__main__":
    PIPE_PATH = '\\\\.\\pipe\\'
    PIPE_NAME = "Pipe"
    TERMINATOR = "END"

    parser = argparse.ArgumentParser()
    parser.add_argument("-t", help="if this flag is passed, don't run the Unity executable.", action="store_false")
    args = parser.parse_args()
    RUN_EXECUTABLE = args.t

    # From https://www.codeproject.com/Questions/5340484/How-to-send-back-data-through-Python-to-Csharp-thr
    pipe_handle = win32pipe.CreateNamedPipe(
        PIPE_PATH + PIPE_NAME,
        win32pipe.PIPE_ACCESS_DUPLEX,
        win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
        1, 65536, 65536,
        0,
        None)

    if RUN_EXECUTABLE:
        SIMULATOR_PATH = "CreatureSimulation\\Builds\\02-23-2024_07-36\\CreatureSimulation.exe"
        log_path = os.path.abspath(os.getcwd()).join("output.log")
        SIMULATOR_ARGS = ["-batchmode", "-nographics", "-p",  PIPE_NAME]
        #Run on a different thread. run waits until the process has finished.
        subprocess.Popen([SIMULATOR_PATH] + SIMULATOR_ARGS)

    win32pipe.ConnectNamedPipe(pipe_handle, None)

    experiment_results = pd.DataFrame(columns=["Creature", "Score"])

    for i in range(100):
        creature = RectPrism()
        experiment_results.loc[len(experiment_results.index)] = [creature, 0]
        message = json.dumps(creature.__dict__) + "\n"
        ret, length = win32file.WriteFile(pipe_handle, message.encode())
        print(f'{ret}, {length} from WriteFile')

    win32file.WriteFile(pipe_handle, f"{TERMINATOR}\n".encode())
    win32file.FlushFileBuffers(pipe_handle)

    composite_message = ""
    while not composite_message.endswith(TERMINATOR + '\r\n'):
        ret, read_message = win32file.ReadFile(pipe_handle, 1000)
        print(f'{ret} Received from c#: ' + read_message.decode('utf-8'))
        composite_message += read_message.decode()
        split_composite_message = composite_message.split('\r\n')
        next_substring_index = 0
        for i in range(len(split_composite_message) - 1):
            result_str = split_composite_message[i]
            if result_str == TERMINATOR:
                break
            result_str_split = result_str.split(' ')
            row = experiment_results.loc[int(result_str_split[0])]
            row["Score"] = float(result_str_split[1])
            experiment_results.loc[int(result_str_split[0])] = row
            next_substring_index += len(result_str) + 2

        composite_message = composite_message[next_substring_index:]

    print(f'Results:\n{experiment_results}')

    print(f'\n\nTop Performers:\n{experiment_results.sort_values("Score", ascending=False).head(10)}')

    win32file.FlushFileBuffers(pipe_handle)
    win32pipe.DisconnectNamedPipe(pipe_handle)
    win32file.CloseHandle(pipe_handle)

