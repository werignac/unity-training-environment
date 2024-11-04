"""
@author William Erignac
@version 2024-09-02

This script runs the cart pole experiment in Unity and uses a gradient descent algorithm on a neural net to learn
a cart pole controller that balances its pole while under the influence of a randomly moving conveyor belt.

The neural net architecture and gradient descent code is from the first actor-critic example for cart pole in "Deep
Reinforcement Learning In Action" by Zai, Alexander and Brown, Brandon.

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

import os
import queue
import torch
import json
from torch import nn
from torch import optim
import numpy as np
from torch.nn import functional as F
import torch.multiprocessing as mp
from tqdm import tqdm
import matplotlib.pyplot as plt

from unity_instance import UnityInstance

PIPE_PATH = '\\\\.\\pipe\\'
SIMULATOR_PATH = os.environ["UNITY_SIMULATOR_PATH"]
SIMULATOR_ARGS = ["-batchmode", "-nographics"]


def extract_frame_data(data: dict):
    return np.array([
        data['CartPosition'],
        data['CartVelocity'],
        data['PoleAngle'],
        data['PoleAngularVelocity']  # ,
        # data['NormalizedWind']
    ]), data['Score']


class CartPoleData:
    def __init__(self):
        self.wind_seed = np.random.randint(1, 1000000)
        self.initial_angle = (0.5 - np.random.rand()) * 2 * 5

    def serialize(self):
        return {"WindSeed": self.wind_seed,
                "InitialAngle": self.initial_angle}

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
        actor = F.log_softmax(self.actor_lin1(y),dim=0) #C
        c = F.relu(self.l3(y.detach()))
        critic = torch.tanh(self.critic_lin1(c)) #D
        return actor, critic #E

def sum_of_model_weights(model):
    weight_sum = 0
    for parameter in model.parameters():
        weight_sum += parameter.data.sum().detach().item()
    return weight_sum

def worker(t, worker_model, counter, params, output_queue: mp.Queue):
    _pipe_name = f"Training_Unity_Pipe_{t}"
    worker_env = UnityInstance(
        os.path.join(PIPE_PATH, _pipe_name),
        {
            "simulator_path": SIMULATOR_PATH,
            "simulator_args": SIMULATOR_ARGS + ["-p", _pipe_name]
        }
    )

    worker_opt = optim.Adam(lr=1e-4,params=worker_model.parameters()) #A
    worker_opt.zero_grad()
    for i in range(params['epochs']):
        worker_opt.zero_grad()
        values, logprobs, rewards = run_episode(worker_env,worker_model) #B
        actor_loss,critic_loss,cumulative_loss,eplen = update_params(worker_opt,values,logprobs,rewards) #C
        counter.value = counter.value + 1 #D
        output_queue.put((eplen, actor_loss, critic_loss, cumulative_loss, sum_of_model_weights(worker_model)))

    worker_env.quit()

def run_episode(worker_env: UnityInstance, worker_model):
    worker_env.run_experiment('cart_pole')
    worker_env.send_session_initialization_data([json.dumps(CartPoleData().serialize())])
    worker_env.end_send_session_initialization_data()

    j = 0
    last_score = 0
    values, logprobs, rewards = [],[],[] #B
    while True: #C
        line = worker_env.read_line()

        if line is None:
            break

        line_split = line.split(" ")

        if len(line_split) == 1:
            continue

        score_parsed: bool = True
        score: float
        try:
            score = float(line_split[1])
        except Exception as e:
            score_parsed = False

        if score_parsed:
            rewards.append(score - last_score)
            continue
        else:
            state, score = extract_frame_data(json.loads(line_split[1]))
            state = torch.from_numpy(state).float()
            policy, value = worker_model(state) #D
            values.append(value)
            logits = policy.view(-1)
            action_dist = torch.distributions.Categorical(logits=logits)
            action = action_dist.sample() #E
            logprob_ = policy.view(-1)[action]
            logprobs.append(logprob_)

            command = json.dumps({'MoveRight': bool(action.detach().item() == 0)})

            if not (command is None):
                to_write = f"0 {command}"
                worker_env.write_line(to_write)
                worker_env.flush_pipe()

            if j > 0:
                rewards.append(score - last_score)

            last_score = score
            j += 1
    return values, logprobs, rewards

def update_params(worker_opt, values, logprobs, rewards, clc=0.1, gamma=0.95):
    rewards = torch.Tensor(rewards).flip(dims=(0,)).view(-1)  # A
    logprobs = torch.stack(logprobs).flip(dims=(0,)).view(-1)
    values = torch.stack(values).flip(dims=(0,)).view(-1)
    Returns = []
    ret_ = torch.Tensor([0])
    for r in range(rewards.shape[0]):  # B
        ret_ = rewards[r] + gamma * ret_
        Returns.append(ret_)
    Returns = torch.stack(Returns).view(-1)
    Returns = F.normalize(Returns, dim=0)
    actor_loss = -1 * logprobs * (Returns - values.detach())  # C
    critic_loss = torch.pow(values - Returns, 2)  # D
    loss = actor_loss.sum() + clc * critic_loss.sum()  # E
    loss.backward()
    worker_opt.step()
    return actor_loss.sum().detach().item(), critic_loss.sum().item(), loss.item(), len(rewards)

if __name__ == "__main__":
    print("Start")
    MasterNode = ActorCritic()  # A
    MasterNode.share_memory()  # B
    processes = []  # C
    params = {
        'epochs': 1000,
        'n_workers': 7,
    }
    counter: mp.Value = mp.Value('i', 0)  # D

    output_queue: mp.Queue = mp.Queue()
    print("Initializing Processes")
    for i in range(params['n_workers']):
        p = mp.Process(target=worker, args=(i, MasterNode, counter, params, output_queue))  # E
        p.start()
        processes.append(p)
    print("Joining Processes")

    statistics = ([], [], [], [], [])

    with tqdm(total=params["epochs"] * params["n_workers"]) as progress:
        last = 0

        processes_awaiting_join = list(processes)
        while len(processes_awaiting_join) > 0:
            to_remove = []
            for i, p in enumerate(processes_awaiting_join):
                p.join(1 / params["n_workers"])
                if p.exitcode is not None:
                    to_remove.append(i)

            current = counter.value
            progress.update(current - last)
            last = current

            for i in reversed(to_remove):
                processes_awaiting_join.pop(i)

            # Collect statistics outputted by the worker threads.
            while not output_queue.empty():
                try:
                    stats_values = output_queue.get(timeout=0.1)
                    for i in range(len(stats_values)):
                        statistics[i].append(stats_values[i])
                except queue.Empty:
                    break

    for p in processes:  # F
        p.join()
    for p in processes:  # G
        p.terminate()

    print(counter.value, processes[1].exitcode)  # H

    # Plot statistics from training.
    for i, stat_name in enumerate(("Score", "Actor Loss", "Critic Loss", "Cumulative Loss", "Model Weights")):
        plt.figure()
        plt.title(f"{stat_name} over Epochs")
        plt.xlabel("Epoch")
        plt.ylabel(stat_name)
        plt.plot(np.arange(1, params['epochs'] * params['n_workers'] + 1), statistics[i])
        plt.show()

    env = UnityInstance(
        os.path.join(PIPE_PATH, "Display_Unity_Pipe"),
        {
            "simulator_path": SIMULATOR_PATH,
            "simulator_args": ["-p", "Display_Unity_Pipe"]
        }
    )

    for i in range(5):
        env.run_experiment("cart_pole")

        env.send_session_initialization_data([json.dumps(CartPoleData().serialize())])
        env.end_send_session_initialization_data()

        while True:
            line = env.read_line()

            if line is None:
                break

            line_split = line.split(" ")

            if len(line_split) == 1:
                continue

            score_parsed: bool = True
            score: float
            try:
                score = float(line_split[1])
            except Exception as e:
                score_parsed = False

            if score_parsed:
                print(score)
                continue
            else:
                state, _ = extract_frame_data(json.loads(line_split[1]))
                state = torch.from_numpy(state).float()
                logits, value = MasterNode(state)
                action_dist = torch.distributions.Categorical(logits=logits)
                action = action_dist.sample()
                command = json.dumps({'MoveRight': bool(action.detach().item() == 0)})

                if not (command is None):
                    to_write = f"0 {command}"
                    env.write_line(to_write)
                    env.flush_pipe()

    env.quit()
