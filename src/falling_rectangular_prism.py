"""
@author William Erignac
@version 2024-09-02

This script runs the falling rectangular prism experiment in Unity and uses a genetic algorithm to learn the dimensions
and initial rotation of a prism that result in movement for the longest amount of time.
"""

import numpy as np
import numpy.random
import pandas as pd
import random
import matplotlib.pyplot as plt
import json
import argparse
import os
from unity_instance import UnityInstance

PIPE_PATH = '\\\\.\\pipe\\'
PIPE_NAME = "PipeB"
SIMULATOR_PATH = os.environ["UNITY_SIMULATOR_PATH"]
SIMULATOR_ARGS = ["-batchmode", "-nographics", "-p", PIPE_NAME]

#region Genetic Algorithm

class RectPrism:
    """
    The scale and euler rotation of a rectangular prism.
    """
    def __init__(self, scale_vector=None, rotation_vector=None):
        """
        Can be initialized with Nones for random scales and rotations.
        """
        self.scale = np.random.random(3) if scale_vector is None else scale_vector
        self.rotation = np.random.random(3) * 90 if rotation_vector is None else rotation_vector

    def serialize(self):
        """
        Convert this object to a dict to be serialized into a json string.
        """
        return {"XScale": self.scale[0], "YScale": self.scale[1], "ZScale": self.scale[2],
                "XRot": self.rotation[0], "YRot": self.rotation[1], "ZRot": self.rotation[2]}

    def __str__(self):
        return f"Location: <{self.scale}>, Rotation: <{self.rotation}>"

    def sexual_mutation(self, other) -> object:
        """
        Create a new RectPrism by combining the genes of this
        prism and another prism.
        """
        scale = np.empty(3, dtype=float)
        for i in range(scale.shape[0]):
            scale[i] = np.random.choice([self.scale[i], other.scale[i]])
        rotation = np.empty(3, dtype=float)
        for i in range(rotation.shape[0]):
            rotation[i] = np.random.choice([self.rotation[i], other.rotation[i]])
        return RectPrism(scale, rotation)

    def asexual_mutation(self, switch_chance=0.3, modify_chance=0.6):
        """
        Create a new RectPrism by mutating the genes of this
        prim.
        """
        scale = np.copy(self.scale)
        rotation = np.copy(self.rotation)
        attributes = [scale, rotation]
        while random.random() < modify_chance:
            # Multiply either scale or rotation by a random amount from 0.9 - 1.1.
            attribute = attributes[np.random.randint(0, len(attributes) - 1)]
            attribute[np.random.randint(0, 2)] *= np.random.rand() * 0.2 + 0.9
        while random.random() < switch_chance:
            # Switch the values of two attributes randomly.
            attribute_1 = attributes[np.random.randint(0, len(attributes) - 1)]
            index_1 = np.random.randint(0, 2)
            attribute_2 = attributes[np.random.randint(0, len(attributes) - 1)]
            index_2 = np.random.randint(0, 2)
            temp = attribute_1[index_1]
            attribute_1[index_1] = attribute_2[index_2]
            attribute_2[index_2] = temp
        return RectPrism(scale, rotation)


def reproduction(scored_organisms: pd.DataFrame, new_population_count=None, sexual_to_asexual_percent=0.5):
    """
    Create a new mutated population, with successful prisms being more likely to reproduce.
    """
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

#region Running Simulation
def execute_epoch(organisms, sim_inst: UnityInstance):
    """
    Simulate a population of rectangular prisms and record their scores.
    """

    serialize_v = np.vectorize(lambda c: json.dumps(c.serialize()))

    serializations = serialize_v(organisms["Creature"].to_numpy())

    sim_inst.send_session_initialization_data(serializations)
    sim_inst.end_send_session_initialization_data()

    while True:
        line = sim_inst.read_line()

        if line is None:
            break

        line_split = line.split(" ")

        if len(line_split) < 2:
            continue

        index = int(line_split[0])
        score = float(line_split[1])

        organisms.loc[index, "Score"] = score

    sorted_organisms = organisms.sort_values("Score", ascending=False)

    print(f'Top Performers:\n{sorted_organisms.head(10)}')

    return sorted_organisms


def display_performers(best_performers):
    """
    Show the best-performing rectangular prisms.
    """

    args = SIMULATOR_ARGS.copy()
    if "-batchmode" in args:
        args.remove("-batchmode")
    if "-nographics" in args:
        args.remove("-nographics")

    exec_args = dict()
    exec_args["simulator_path"] = SIMULATOR_PATH
    exec_args["simulator_args"] = args
    sim_inst = UnityInstance(os.path.join(PIPE_PATH, PIPE_NAME), exec_args)

    for performer in best_performers:
        sim_inst.run_experiment("falling_rectangular_prism")

        sim_inst.send_session_initialization_data(json.dumps(performer.serialize()))
        sim_inst.end_send_session_initialization_data()

        while not (sim_inst.read_line() is None):
            pass

    sim_inst.quit()

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
    for i in range(64):
        organisms.loc[len(organisms.index)] = [RectPrism(), 0]

    if DISPLAY_BEST_PERFORMERS:
        best_performers = []

    if STATS > 0:
        avg_performance_per_epoch = []
    if STATS > 1:
        best_performers_scores = []

    exec_args = dict()
    exec_args["simulator_path"] = SIMULATOR_PATH
    exec_args["simulator_args"] = SIMULATOR_ARGS
    sim_inst = UnityInstance(os.path.join(PIPE_PATH, PIPE_NAME), exec_args if RUN_EXECUTABLE else None)

    for i in range(EPOCH_COUNT):
        print(f"\nEpoch {i + 1}")
        sim_inst.run_experiment("falling_rectangular_prism")
        organisms = execute_epoch(organisms, sim_inst)
        if DISPLAY_BEST_PERFORMERS:
            best_performers.append(organisms.head(1)["Creature"].iloc[0])
        if STATS > 0:
            avg_performance_per_epoch.append(np.mean(organisms["Score"]))
        if STATS > 1:
            best_performers_scores.append(np.mean(organisms.head(10)["Score"]))
        organisms = reproduction(organisms)

    sim_inst.quit()

    if DISPLAY_BEST_PERFORMERS:
        display_performers(best_performers)

    if STATS > 0:
        ax = plt.subplot(1, 1, 1)
        plt.title(f"Performance over Epochs")
        plt.ylabel(f"Score")
        plt.xlabel(f"Epoch (first epoch at 1)")
        plt.plot(np.arange(1, EPOCH_COUNT + 1, 1), avg_performance_per_epoch, label="Average Population Score")
        if STATS > 1:
            plt.plot(np.arange(1, EPOCH_COUNT + 1, 1), best_performers_scores, label="Top 10 Individuals Average Score")
            plt.legend()
        ax.grid()

        plt.show()




