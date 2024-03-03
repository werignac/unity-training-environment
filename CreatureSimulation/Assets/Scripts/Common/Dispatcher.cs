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

namespace werignac.GeneticAlgorithm
{


	/// <summary>
	/// Class that communicates with the C++ evolution script.
	/// </summary>
	public class Dispatcher : MonoBehaviour
	{
		// IPC fields
		private NamedPipeClientStream pipe = null;
		private StreamWriter sw = null;
		private StreamReader sr = null;

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
		private bool useTestPipeName;

		[SerializeField, Tooltip("The name of the pipe name to use for testing if useSetPipeName is true. Only available in-editor.")]
		private string testPipeName;
#endif

		// Start is called before the first frame update
		void Start()
		{
			DontDestroyOnLoad(gameObject);

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
				pipe = new NamedPipeClientStream(".", pipename);
				pipe.Connect((int) (pipeTimeout * 1000));
				Debug.Log($"Connected to pipe \"{pipename}\".");
				sw = new StreamWriter(pipe);
				sr = new StreamReader(pipe);
			}
			else
				Debug.Log("No pipe name provided.");
			
			// Read commands from user.
			ReadCommand();
		}

		private async void ReadCommand()
		{
			string line = await sr.ReadLineAsync();
			line.Trim();

			string[] words = line.Split(" ");

			if (words.Length == 0)
			{
				ReadCommand();
				return;
			}

			switch (words[0])
			{
				// run <experiment_name> - Open a scene with a maching name from the settings map of experiments.
				case "run":
					if (words.Length == 1)
					{
						WriteError($"Run command requires an experiment name.");
						ReadCommand();
						return;
					}

					string experimentName = words[1];

					SimulationSettings simSettings = SimulationSettings.GetOrCreateSettings();
					if (!simSettings.experimentNamesToScenes.TryGetValue(experimentName, out string experimentSceneName))
					{
						WriteError($"Experiment name \"{experimentName}\" was not recognized.");
						ReadCommand();
						return;
					}

					Run(experimentSceneName);
					return;

				// set <setting_name> <value> - sets a value for the dispatcher
				case "set":
					// TODO: add checks for words[1], and words[2].
					runtimeSettings.SetSetting(words[1], words[2]);
					ReadCommand();
					return;
				
				// quit - closes the application.
				case "quit":
					Quit();
					return;

				default:
					WriteError($"Could not recognize command \"{line}\".");
					ReadCommand();
					return;
			}
		}

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

		private async void Run(string experimentSceneName)
		{
			// If this is the current level, just re-run.
			bool alreadySetUp = SceneManager.GetActiveScene().name.Equals(experimentSceneName);
			
			// Load the correct level.
			if (! alreadySetUp)
			{
				await SceneManager.LoadSceneAsync(experimentSceneName);
			}

			if (!WerignacUtils.TryGetComponentInAll(out IExperiment experiment))
			{
				// Print an error message.
				WriteError($"Experiment scene for {SceneManager.GetActiveScene().name} is missing an IExperiment component.");
				ReadCommand();
				return;
			}

			if (!alreadySetUp)
			{
				experiment.GetOnCreatureScoredEvent().AddListener(OnCreatureFinishedSimulation);
				experiment.GetOnExperimentTerminatedEvent().AddListener(OnAllSimulationsFinished);
			}

			experiment.ReadCreatures(sr);
		}


		private void OnCreatureFinishedSimulation(SimulationInitializationData creature, float score, string toSend)
		{
			Debug.LogFormat("Creature {0} finished with score: {1}", creature.Index, score);

			if (pipe != null)
			{
				Debug.Log($"Writing \"{toSend}\" to pipe.");
				sw.WriteLine(toSend);
				
				if (runtimeSettings.flushEveryCreature)
				{
					sw.Flush();
					pipe.Flush();
				}
			}
		}

		/// <summary>
		/// Called when all the simulations for a run command have finished.
		/// </summary>
		private void OnAllSimulationsFinished()
		{
			if (pipe != null)
			{
				sw.WriteLine("END");
				sw.Flush();
				pipe.Flush();
			}

			ReadCommand();
		}

		/// <summary>
		/// Write a warning to the log and pipe.
		/// </summary>
		/// <param name="message"></param>
		private void WriteWarning(string message)
		{
			message = $"Warning: {message}";
			Debug.LogWarning(message);
			if (pipe != null)
			{
				sw.WriteLine(message);
				sw.Flush();
				pipe.Flush();
			}
		}

		/// <summary>
		/// Write an error to the log and pipe.
		/// </summary>
		/// <param name="message"></param>
		private void WriteError(string message)
		{
			message = $"Error: {message}";
			Debug.LogError(message);
			if (pipe != null)
			{
				sw.WriteLine(message);
				sw.Flush();
				pipe.Flush();
			}
		}

		/// <summary>
		/// Called when the user wants to stop this instance.
		/// </summary>
		private void Quit()
		{
			if (pipe != null)
			{
				sw.WriteLine("QUIT");
				sw.Flush();
				pipe.Flush();
			}

			Application.Quit();
		}

		/// <summary>
		/// Called on Application.Quit / stopping running in UnityEditor.
		/// </summary>
		private void OnDestroy()
		{
			// Close pipe when tasks are done.
			if (sw != null || sr != null || pipe != null)
				Debug.Log("Cleaning pipe and streams.");

			sw?.Close();
			sr?.Close();
			pipe?.Close();
		}
	}
}
