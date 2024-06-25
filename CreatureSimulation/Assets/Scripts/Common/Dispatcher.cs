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
	/// 
	/// TODO: Make into subsystem.
	/// </summary>
	[Subsystem(SubsystemLifetime.GAME)]
	public class Dispatcher : MonoBehaviour
	{
		/// <summary>
		/// Object that communicates with an external process that sends
		/// commands. In theory the implementation is abstracted from the dispatcher,
		/// but currently the dispatcher initializes a PipeCommunicator in Start().
		/// </summary>
		public ICommunicator Communicator { get; private set; } = null;

		/// <summary>
		/// Object that 
		/// </summary>
		public PipeCommunicatorBuffer CommunicatorBuffer { get; private set; } = null;

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

		[Header("Debugging")]
		[SerializeField, Tooltip("Amount of time to wait before timing out on pipe connection (in seconds).")]
		private float pipeTimeout = 10.0f;

#if UNITY_EDITOR
		[SerializeField, Tooltip("If true, use a preset pipe name for testing. Only available in-editor.")]
		private bool useTestPipeName = true;

		[SerializeField, Tooltip("The name of the pipe name to use for testing if useSetPipeName is true. Only available in-editor.")]
		private string testPipeName = "PipeA";
#endif
		public ParserStack ParserStack { get; private set; } = null;
		private IParser<ParsedErrorWarning> errorWarningParser = null;
		private IParser<DispatchCommand> dispatchParser = null;

		// Start is called before the first frame update
		void Start()
		{
			// Override's Unity's SynchronizationContext with the default C# SynchronizationContext.
			// This enables async functions to truely run in parallel, which is important for creatures
			// who use individual pipes for communication.
			SynchronizationContextSubsystem SyncSubsystem = SubsystemManagerComponent.Get().GetSubsystem<SynchronizationContextSubsystem>();
			SyncSubsystem.SetSynchronizationContextType(SynchronizationContextSubsystem.SynchronizationContextType.C_SHARP);

			if (SceneManager.GetActiveScene().name != "Dispatcher")
			{
				DestroyImmediate(this);
				return;
			}

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
				CommunicatorBuffer = new PipeCommunicatorBuffer(OnBufferOut);
				Debug.Log($"Pipe name provided: {pipename}.");
				Communicator = new PipeCommunicator(pipename, pipeTimeout, CommunicatorBuffer.OnReadLine);
				Debug.Log($"Connected to pipe \"{pipename}\".");
				
				ParserStack = new ParserStack();
				// Listen for when an error or warning is parsed.
				errorWarningParser = ParserStack.AddParser<ErrorWarningParser>();
				// Listen for when commands are parsed.
				dispatchParser = ParserStack.AddParser<DispatchParser>();
			}
			else
				Debug.Log("No pipe name provided.");
		}

		/// <summary>
		/// Empties the buffer into the parser stack.
		/// </summary>
		private void OnBufferOut(string line)
		{
			if (! ParserStack.TryParse(line, out string cumulativeErrorMessage))
			{
				WriteWarning(cumulativeErrorMessage);
			}
		}

		#region Execution

		/// <summary>
		/// Gets the argument passed after a flag.
		/// e.g. -f value -> GetCommandLineArgumentValue(["-f", "value"], "-f") returns "value"
		/// </summary>
		/// <param name="commandLineArgs">Command line arguments split by spaces.</param>
		/// <param name="arg">Argument with the "-"</param>
		/// <returns>The value that corresponds to arg.</returns>
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
			if (errorWarningParser != null && errorWarningParser.Next(out ParsedErrorWarning errorWarningCommand))
			{
				OnErrorWarningParsed(errorWarningCommand);
				CommunicatorBuffer.AcceptNext();
			}

			if (dispatchParser != null && dispatchParser.Next(out DispatchCommand dispatchCommand))
			{
				ExecuteCommand(dispatchCommand);
				// Communication Buffer Accept Next is in ExecuteCommand (some finish asynchronously).
			}
		}

		/// <summary>
		/// Executes a single command. Ensure that CommunicatorBuffer.AcceptNext() is called when a command finishes.
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
					CommunicatorBuffer.AcceptNext();
					break;

				case DispatchCommandType.QUIT:
					Communicator.Write("QUIT");
					CommunicatorBuffer.AcceptNext();
					Application.Quit();
					break;
			}
		}

		/// <summary>
		/// Function called when an error or warning sent from
		/// outside is parsed.
		/// </summary>
		/// <param name="errorWarning">The parsed error or warning.</param>
		private void OnErrorWarningParsed(ParsedErrorWarning errorWarning)
		{
			if (errorWarning.IsError)
			{
				Debug.LogError(errorWarning);
				Application.Quit();
			}
			else
			{
				Debug.LogWarning(errorWarning);
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

			Communicator.Write("SUCCESS");

			experiment.StartReadCreatures();
			
			// Setup has finished. Read next command / parse creatures.
			CommunicatorBuffer.AcceptNext();
		}

		/// <summary>
		/// Send the name of a creature that has started simulation.
		/// </summary>
		/// <param name="creature"></param>
		private void OnCreatureStartedSimulation(SimulationInitializationData creature)
		{
			Debug.LogFormat("Creature {0} started simulation.", creature.Index);
			if (Communicator != null)
			{
				Debug.Log($"Writing \"{creature.Index}\" to pipe.");
				Communicator.Write(creature.Index.ToString());
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

			if (Communicator != null)
			{
				Debug.Log($"Writing \"{toSend}\" to pipe.");
				Communicator.Write(toSend);
			}
		}

		/// <summary>
		/// Called when all the simulations for a run command have finished.
		/// Sends a message singaling the end of the simulation.
		/// </summary>
		private void OnAllSimulationsFinished()
		{
			Communicator?.Write("END");
		}

		/// <summary>
		/// Write a warning to the log and pipe.
		/// </summary>
		/// <param name="message"></param>
		private void WriteWarning(string message)
		{
			Debug.LogWarning(message);
			Communicator?.WriteWarning(message);
		}

		/// <summary>
		/// Write an error to the log and pipe.
		/// </summary>
		/// <param name="message"></param>
		private void WriteError(string message)
		{
			Debug.LogError(message);
			Communicator?.WriteError(message);	
		}

		/// <summary>
		/// Called on Application.Quit / stopping running in UnityEditor.
		/// </summary>
		private void OnDestroy()
		{
			// Close pipe when tasks are done.
			if (Communicator != null)
			{
				Debug.Log("Cleaning pipe and streams.");
				CommunicatorBuffer.Close();
				Communicator.Close();
			}
		}

		#endregion
	}
}
