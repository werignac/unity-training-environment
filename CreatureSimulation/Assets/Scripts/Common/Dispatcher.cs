using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO.Pipes;
using System.IO;
using werignac.Creatures;
using werignac.Utils;
using werignac.GeneticAlgorithm.Settings;
using System.Threading;
using werignac.Communication.Dispatch.Commands;
using werignac.Communication.Dispatch;
using werignac.Communication;
using werignac.Subsystem;
using werignac.GeneticAlgorithm.Subsystems;

namespace werignac.GeneticAlgorithm.Dispatch
{
	/// <summary>
	/// Class that communicates with the external evolution script.
	/// </summary>
	public class Dispatcher : MonoBehaviour
	{
		/// <summary>
		/// Object that communicates with an external process that sends
		/// commands. In theory the implementation is abstracted from the dispatcher,
		/// but currently the dispatcher initializes a PipeCommunicator in Start().
		/// </summary>
		private ICommunicator communicator = null;

		/// <summary>
		/// Whether the Dispatcher should be readling lines from the communicator.
		/// Set to false whilst an experiment is running and the experiment is reading from the communicator.
		/// </summary>
		private bool hasReadPriviledge = true;

		// TODO: Use a dictionary instead of fields and make publicly accessible.
		private class DispatcherSettings
		{

			public bool flushEveryCreature = false;

			private static string SettingNameToField(string settingName)
			{
				switch (settingName)
				{
					case "flush_every_creature":
						return "flushEveryCreature"; // TODO: return field instead of string field name.
				}
				return null;
			}

			public void SetSetting(string settingName, string value)
			{
				// TODO: Use reflections to get the field and assign it.
				string fieldName = SettingNameToField(settingName);
			}
		}

		private DispatcherSettings runtimeSettings = new DispatcherSettings();

		[Header("Paralelism")]
		[SerializeField, Tooltip("If true, uses Unity's SynchronizationContext. May cause blocking for tasks that expect to be in parallel.")]
		private bool _useUnitySynchronizationContext = false;

		[Header("Debugging")]
		[SerializeField, Tooltip("Amount of time to wait before timing out on pipe connection (in seconds).")]
		private float pipeTimeout = 10.0f;

#if UNITY_EDITOR
		[SerializeField, Tooltip("If true, use a preset pipe name for testing. Only available in-editor.")]
		private bool useTestPipeName;

		[SerializeField, Tooltip("The name of the pipe name to use for testing if useSetPipeName is true. Only available in-editor.")]
		private string testPipeName;
#endif

		private IParser<DispatchCommand> parser = null;

		// Start is called before the first frame update
		void Start()
		{
			DontDestroyOnLoad(gameObject);

			// Override's Unity's SynchronizationContext with the default C# SynchronizationContext.
			// This enables async functions to truely run in parallel, which is important for creatures
			// who use individual pipes for communication.
			SynchronizationContextSubsystem SyncSubsystem = SubsystemManagerComponent.Get().GetSubsystem<SynchronizationContextSubsystem>();
			SyncSubsystem.SetSynchronizationContextType(SynchronizationContextSubsystem.SynchronizationContextType.C_SHARP);

			// TODO: Make communicator object that handles purely reading and writing to pipes.
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			Debug.LogFormat("Command Line Args: [{0}]", string.Join(", ", commandLineArgs));
			// -p <pipename> name of the named pipe to send / receive communications.
			string pipename = GetCommandLineArgumentValue(commandLineArgs, "-p");

#if UNITY_EDITOR
			if (useTestPipeName)
				pipename = testPipeName;
#endif
			// Connect to pipe.
			if (pipename != null)
			{
				Debug.Log($"Pipe name provided: {pipename}.");
				communicator = new PipeCommunicator(pipename, pipeTimeout);
				Debug.Log($"Connected to pipe \"{pipename}\".");
				parser = new DispatchParser(communicator);
			}
			else
				Debug.Log("No pipe name provided.");
		}

		#region Execution

		/// <summary>
		/// 
		/// </summary>
		/// <param name="commandLineArgs"></param>
		/// <param name="arg">Argument with the "-"</param>
		/// <returns></returns>
		private string GetCommandLineArgumentValue(string[] commandLineArgs, string arg)
		{
			int argIndex = Array.IndexOf(commandLineArgs, arg);

			if (argIndex < 0)
				return null;

			int valIndex = argIndex + 1;

			if (valIndex >= commandLineArgs.Length)
				throw new Exception($"Missing value for command line argument {arg} at the end of the arguments list.");

			string value = commandLineArgs[valIndex];

			if (value.StartsWith('-'))
				throw new Exception($"Missing value for command line argument {arg}.");

			return value;
		}

		/// <summary>
		/// On the main thread, poll and execute commands.
		/// </summary>
		private void Update()
		{
			// Check whether we've forefitted reading rights to an experiment.
			// If there are commands to execute, execute them.
			if (hasReadPriviledge && parser != null && parser.Next(out DispatchCommand command))
			{
				ExecuteCommand(command);
			}
		}

		/// <summary>
		/// Executes a single command.
		/// </summary>
		/// <param name="command">The command to execute.</param>
		/// <returns>Whether to continue execution of commands.</returns>
		private void ExecuteCommand(DispatchCommand command)
		{
			switch(command.Type)
			{
				case DispatchCommandType.RUN:
					DispatchRunCommand runCommand = command as DispatchRunCommand;

					// Convert the provided name into a scene name.
					SimulationSettings simSettings = SimulationSettings.GetOrCreateSettings();
					if (!simSettings.experimentNamesToScenes.TryGetValue(runCommand.ExperimentToRun, out string experimentSceneName))
					{
						WriteError($"Experiment name \"{runCommand.ExperimentToRun}\" was not recognized.");
						break;
					}

					RunExperiment(experimentSceneName);
					break;

				case DispatchCommandType.SET:
					// TODO: Implement.
					break;

				case DispatchCommandType.QUIT:
					communicator.Write("QUIT");
					Application.Quit();
					break;
			}
		}

		/// <summary>
		/// Opens a scene corresponding to an experiment and starts reading creatures.
		/// Runs on main thread.
		/// </summary>
		/// <param name="experimentSceneName"></param>
		private void RunExperiment(string experimentSceneName)
		{
			// If this is the current level, just re-run.
			bool alreadySetUp = SceneManager.GetActiveScene().name.Equals(experimentSceneName);

			// Load the correct level.
			if (!alreadySetUp)
			{
				SceneManager.activeSceneChanged += (Scene _, Scene __) => { ReadyExperiment(true); };
				SceneManager.LoadScene(experimentSceneName);
			}
			else
			{
				ReadyExperiment(false);
			}
		}

		private void ReadyExperiment(bool addListeners)
		{
			if (!WerignacUtils.TryGetComponentInActiveScene(out IExperiment experiment))
			{
				// Print an error message.
				WriteError($"Experiment scene for {SceneManager.GetActiveScene().name} is missing an IExperiment component.");
				return;
			}

			if (addListeners)
			{
				experiment.GetOnCreatureStartedEvent().AddListener(OnCreatureStartedSimulation);
				experiment.GetOnCreatureScoredEvent().AddListener(OnCreatureFinishedSimulation);
				experiment.GetOnExperimentTerminatedEvent().AddListener(OnAllSimulationsFinished);
			}

			communicator.Write("SUCCESS");

			// Forfeit control of communicator to experiment.
			hasReadPriviledge = false;
			experiment.StartReadCreatures(communicator);
		}

		/// <summary>
		/// Send the name of a creature that has started simulation.
		/// </summary>
		/// <param name="creature"></param>
		private void OnCreatureStartedSimulation(SimulationInitializationData creature)
		{
			Debug.LogFormat("Creature {0} started simulation.", creature.Index);
			if (communicator != null)
			{
				Debug.Log($"Writing \"{creature.Index}\" to pipe.");
				communicator.Write(creature.Index.ToString());
			}
		}

		/// <summary>
		/// Send the name and score of a creature who has finished simulation.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="score"></param>
		/// <param name="toSend"></param>
		private void OnCreatureFinishedSimulation(SimulationInitializationData creature, float score, string toSend)
		{
			Debug.LogFormat("Creature {0} finished with score: {1}", creature.Index, score);

			if (communicator != null)
			{
				Debug.Log($"Writing \"{toSend}\" to pipe.");
				communicator.Write(toSend);
			}
		}

		/// <summary>
		/// Called when all the simulations for a run command have finished.
		/// Sends a message singaling the end of the simulation.
		/// </summary>
		private void OnAllSimulationsFinished()
		{
			// Regain control of communications.
			hasReadPriviledge = true;
			communicator?.Write("END");
		}

		/// <summary>
		/// Write a warning to the log and pipe.
		/// </summary>
		/// <param name="message"></param>
		private void WriteWarning(string message)
		{
			Debug.LogWarning(message);
			communicator?.WriteWarning(message);
		}

		/// <summary>
		/// Write an error to the log and pipe.
		/// </summary>
		/// <param name="message"></param>
		private void WriteError(string message)
		{
			Debug.LogError(message);
			communicator?.WriteError(message);	
		}

		/// <summary>
		/// Called on Application.Quit / stopping running in UnityEditor.
		/// </summary>
		private void OnDestroy()
		{
			// Close pipe when tasks are done.
			if (communicator != null)
			{
				Debug.Log("Cleaning pipe and streams.");
				communicator.Close();
			}
		}

		#endregion
	}
}
