using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.Events;
using System.IO.Pipes;
using System.IO;
using werignac.Creatures;

namespace werignac.GeneticAlgorithm
{
	/// <summary>
	/// Interface used by Dispatcher to interact with Experiments.
	/// </summary>
	public interface IExperiment
	{
		public void ReadCreatures(StreamReader sr);
		public UnityEvent<SimulationInitializationData, float, string> GetOnCreatureScoredEvent();
		public UnityEvent GetOnExperimentTerminatedEvent();
	}

	public class Experiment<T, R, D> : MonoBehaviour, IExperiment where T : SimulationInitializationData where R : RandomSimulationInitializationDataFactory<T>, new() where D : CreatureReader<T>, new()
	{
		[SerializeField]
		private PopulationController<T> populationController;

		[Header("Simulation Settings")]
		[SerializeField, Tooltip("The number of creatures to simulate if there is no pipe."), Min(0)]
		private int noPipePopulationSize;
		private R randomDataFactory = new R();
		private D deserializer;

		bool hasInitialized = false;

		[Header("Events")]
		[Tooltip("Called when a creature has finished being scored. Used to print results.")]
		public UnityEvent<SimulationInitializationData, float, string> OnCreatureScored = new UnityEvent<SimulationInitializationData, float, string>();
		public UnityEvent OnExpirementTerminated = new UnityEvent();

		[Header("Debugging")]
		[SerializeField, Tooltip("If true, always sets realtime to false.")]
		private bool overrideRealtime = false;
#if UNITY_EDITOR
		[SerializeField, Tooltip("If true, run the experiment on start. Will use randomly generated creatures [noPipePopulationSize].")]
		private bool readCreatureOnStart = false;
		[SerializeField, Tooltip("If true, break after instantiating all creatures.")]
		private bool breakAfterReadCreatures = false;
#endif


#if UNITY_EDITOR
		private void Start()
		{
			if (readCreatureOnStart)
				ReadCreatures(null);
		}
#endif

		private void Initialize()
		{
			bool isBatchMode = Application.isBatchMode;
			string[] commandLineArgs = Environment.GetCommandLineArgs();

			Debug.LogFormat("Command Line Args: [{0}]", string.Join(", ", commandLineArgs));

			populationController.onCreatureFinishedSimulation.AddListener(OnCreatureFinishedSimulation);
			populationController.onOutOfCreatures.AddListener(CheckOutOfAllCreatures);
			// Initialize with passed or default parameters.
			bool realtime = !(isBatchMode || overrideRealtime);
			populationController.Initialize(realtime);

			hasInitialized = true;
		}

		public async void ReadCreatures(StreamReader sr)
		{
			if (!hasInitialized)
				Initialize();

			deserializer = new D();

			if (sr != null)
			{
				Debug.Log("Reading creatures from pipe.");
				await foreach (T creatureData in deserializer.ReadCreatures(sr))
					populationController.EnqueueCreature(creatureData);
			} else
			{
				Debug.Log($"ReadCreatures called, but no pipe detected. Generating {noPipePopulationSize} creatures.");
				for (int i = 0; i < noPipePopulationSize; i++)
				{
					populationController.EnqueueCreature(randomDataFactory.GenerateRandomData(i));
				}
			}


#if UNITY_EDITOR
			if (breakAfterReadCreatures)
			{
				Debug.Break();
			}
#endif

		}

		private void OnCreatureFinishedSimulation(T creature, float score)
		{
			Debug.LogFormat("Creature {0} finished with score: {1}", creature.Index, score);
			string toSend = $"{creature.Index} {score}";
			OnCreatureScored.Invoke(creature, score, toSend);
		}

		/// <summary>
		/// Called when the population controller has finished all simulations
		/// and is out of creatures.
		/// </summary>
		private void CheckOutOfAllCreatures()
		{
			if (!(deserializer.GetIsDoneReading() && populationController.IsOutOfCreatures()))
				return;

			OnExpirementTerminated.Invoke();
		}

		public UnityEvent<SimulationInitializationData, float, string> GetOnCreatureScoredEvent()
		{
			return OnCreatureScored;
		}

		public UnityEvent GetOnExperimentTerminatedEvent()
		{
			return OnExpirementTerminated;
		}
	}
}
