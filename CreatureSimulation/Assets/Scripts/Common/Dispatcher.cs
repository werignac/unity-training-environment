using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using werignac.GeneticAlgorithm;
using System.IO.Pipes;
using System.IO;
using werignac.Creatures;

namespace werignac.GeneticAlgorithm
{
	/// <summary>
	/// Class that communicates with the C++ evolution script.
	/// </summary>
	public class Dispatcher<T, R, D> : MonoBehaviour where T : SimulationInitializationData where R : RandomSimulationInitializationDataFactory<T>, new() where D : ICreatureReaderInterface<T>, new()
	{
		[SerializeField]
		private PopulationController<T> populationController;

		[Header("Simulation Settings")]
		[SerializeField, Tooltip("The number of creatures to simulate. Is overwritten if a pipe is provided."), Min(0)]
		private int populationSize;
		private R randomDataFactory = new R();
		private D deserializer;

		// IPC fields
		private NamedPipeClientStream pipe = null;
		private StreamWriter sw = null;
		private StreamReader sr = null;

		[Header("Debugging")]
		[SerializeField, Tooltip("Amount of time to wait before timing out on pipe connection (in seconds).")]
		private float pipeTimeout = 10.0f;
		[SerializeField, Tooltip("If true, always sets realtime to false.")]
		private bool overrideRealtime = false;

#if UNITY_EDITOR
		[SerializeField, Tooltip("If true, use a preset pipe name for testing. Only available in-editor.")]
		private bool useTestPipeName;

		[SerializeField, Tooltip("The name of the pipe name to use for testing if useSetPipeName is true. Only available in-editor.")]
		private string testPipeName;
#endif

		// Start is called before the first frame update
		void Start()
		{
			bool isBatchMode = Application.isBatchMode;
			string[] commandLineArgs = Environment.GetCommandLineArgs();

			Debug.LogFormat("Command Line Args: [{0}]", string.Join(", ", commandLineArgs));

			// -p <pipename> name of the named pipe to send / receive communications.
			string pipename = GetCommandLineArgumentValue(commandLineArgs, "-p");

#if UNITY_EDITOR
			if (useTestPipeName)
				pipename = testPipeName;
#endif

			if (pipename != null)
			{
				Debug.Log($"Pipe name provided: {pipename}.");
				pipe = new NamedPipeClientStream(".", pipename);
				pipe.Connect((int) (pipeTimeout * 1000));
				Debug.Log($"Connected to pipe \"{pipename}\".");
				sw = new StreamWriter(pipe); // sw.WriteLine("What's your status?"); To write to pipe
				sr = new StreamReader(pipe); // temp = sr.ReadLine(); To read from pipe
			}
			else
				Debug.Log("No pipe name provided.");

			populationController.onCreatureFinishedSimulation.AddListener(OnCreatureFinishedSimulation);
			populationController.onOutOfCreatures.AddListener(CheckOutOfAllCreatures);
			// Initialize with passed or default parameters.
			bool realtime = !(isBatchMode || overrideRealtime);
			populationController.Initialize(realtime);

			// TODO: Start reading creatures from sw and enqueuing them onto the list
			if (pipe != null)
			{
				deserializer = new D();
				deserializer.GetOnIsDoneReading().AddListener(this.CheckOutOfAllCreatures);
				ReadCreatures();
			}
			// Generate examples if no pipe is provided.
			else
			{
				Debug.LogFormat($"Generating {populationSize} creatures by default.");
				for (int i = 0; i < populationSize; i++)
					populationController.EnqueueCreature(randomDataFactory.GenerateRandomData(i));
			}
		}

		private async void ReadCreatures()
		{
			Debug.Log("Reading creatures from pipe.");
			Debug.LogFormat($"Unknown number of creatures.");

			await foreach (T creatureData in deserializer.ReadCreatures(sr))
				populationController.EnqueueCreature(creatureData);
		}

		private void OnDestroy()
		{
			// Close pipe when tasks are done.
			if (sw != null || sr != null || pipe != null)
				Debug.Log("Cleaning pipe and streams.");
			
			sw?.Close();
			sr?.Close();
			pipe?.Close();
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

		private void OnCreatureFinishedSimulation(T creature, float score)
		{
			Debug.LogFormat("Creature {0} finished with score: {1}", creature.Index, score);

			if (pipe != null)
			{
				string toSend = $"{creature.Index} {score}";
				Debug.Log($"Writing \"{toSend}\" to pipe.");
				sw.WriteLine(toSend);
			}
		}

		/// <summary>
		/// Called when the population controller has finished all simulations
		/// and is out of creatures.
		/// </summary>
		private void CheckOutOfAllCreatures()
		{
			if (!(deserializer.GetIsDoneReading() && populationController.IsOutOfCreatures()))
				return;

			if (pipe != null)
			{
				// TODO: Check if we're still in the process of reading creatures from
				// our IPC pipe.
				sw.WriteLine("END");
				pipe.Flush();
			}

			// Calls OnDestroy (?)
			Application.Quit();
		}
	}
}
