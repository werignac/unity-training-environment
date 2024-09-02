# Unity Training Environment

## Summary
The goal of this project is to develop a framework for turning a Unity project into a training environment to be used in Python for reinforcement learning (RL) from scratch. 

## Demos
Currently, there is a Cart Pole project showcasing an agent trained using this framework and imported into Unity.

![img](Documentation/Demo%20Results/cartpole_agent.gif)

To see the Cart Pole agent in action, please check out this project on [itch.io](https://werignac.itch.io/unity-reinforcement-learning-cart-pole).

## Setup
The following are instructions to run the Python scripts in src:
1. Open the Unity project in UnityRLEnvironment.
2. Set Player Settings > Physics > Simulation Mode to Script.
3. Open the Build settings.
4. Ensure that only scenes used for experiments are included (no demo scenes). The Dispatcher scene should be included and should be the topmost scene in the list.
5. Create a build for Windows, and copy the path of the build.
6. Create a virtual environment with the provided [requirements.txt](requirements.txt).
7. Activate the virtual environment, and set the environment variable "UNITY_SIMULATOR_PATH" to the copied path of your build from step 5.
8. Run your desired script in src.

To run in the Unity editor, instead of creating a build, ensure that testPipeName in [Dispatcher.cs](UnityRLEnvironment\Assets\Scripts\Common\Dispatcher.cs) matches the pipe name in the script you're running. Then, open and play the Dispatcher scene in Unity followed by running a Python script with -t as an argument. 

## Communication
In order for a Unity instance to act as a training environment, it must perform the following actions from Python:
- Load a requested environment.
- Create instances of experiments to run.
- Send updates on the states of each experiment.
- Receive mid-experiment commands from an agent-in-training and execute them.
- Notify when experiments have terminated and report the final scores of the agents.

To perform these functions, this project uses a Windows named pipe to communicate between Python and Unity. Because of this design choice, we can choose to train in either the Unity editor or on a build of the Unity project.

Here is an outline of the communication protocol used:

![img](Documentation/Diagrams/Reinforcement%20Learning%20Environment%20Communication%20Sequence.png)

To support reading commands from parallel agents-in-training in an arbitrary order (i.e. get a command from agent 3 before agent 1 in one update step), reading from the named pipe on the Unity side is done on a thread separate from the main thread.

## More
For more information on the development process behind this project, please visit the project's page [on my website](https://sites.google.com/view/william-erignac/engineering/unity-training-environment).
