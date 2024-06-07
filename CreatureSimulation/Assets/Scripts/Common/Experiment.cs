using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.Events;
using System.IO.Pipes;
using System.IO;
using werignac.Creatures;
using System.Threading;
using werignac.Subsystem;
using werignac.GeneticAlgorithm.Subsystems;
using werignac.Communication;

namespace werignac.GeneticAlgorithm
{
	/// <summary>
	/// Interface used by Dispatcher to interact with Experiments.
	/// </summary>
	public interface IExperiment
	{
		public void StartReadCreatures(ICommunicator communicator);
		public UnityEvent<SimulationInitializationData, float, string> GetOnCreatureScoredEvent();
		public UnityEvent GetOnExperimentTerminatedEvent();
		public UnityEvent<SimulationInitializationData> GetOnCreatureStartedEvent();
	}

	public class Experiment<Init_Type, Random_Init_Type, Deserializer_Type> : MonoBehaviour, IExperiment where Init_Type : SimulationInitializationData where Random_Init_Type : RandomSimulationInitializationDataFactory<Init_Type>, new() where Deserializer_Type : CreatureReader<Init_Type>, new()
	{
		/// <summary>
		/// The communicator to read lines from.
		/// </summary>
		private ICommunicator communicator = null;

		[SerializeField]
		private PopulationController<Init_Type> populationController;

		[Header("Simulation Settings")]
		[SerializeField, Tooltip("The number of creatures to simulate if there is no pipe."), Min(0)]
		private int noPipePopulationSize;
		private Random_Init_Type randomDataFactory = new Random_Init_Type();
		private Deserializer_Type deserializer;

		bool hasInitialized = false;

		[Header("Events")]
		[Tooltip("Called when a creature has finished being scored. Used to print results.")]
		public UnityEvent<SimulationInitializationData, float, string> OnCreatureScored = new UnityEvent<SimulationInitializationData, float, string>();
		public UnityEvent OnExpirementTerminated = new UnityEvent();
		/// <summary>
		/// All this does is convert the T type to the SimulationInitializationData type for Dispatcher.
		/// </summary>
		public UnityEvent<SimulationInitializationData> OnCreatureStarted = new UnityEvent<SimulationInitializationData>();

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
		/// <summary>
		/// If starting directly from this scene w/o a dispatcher, instantiate a bunch of random creatures.
		/// </summary>
		private void Start()
		{
			if (readCreatureOnStart)
				StartReadCreatures(null);
		}
#endif

		private void Initialize()
		{
			bool isBatchMode = Application.isBatchMode;
			string[] commandLineArgs = Environment.GetCommandLineArgs();

			Debug.LogFormat("Command Line Args: [{0}]", string.Join(", ", commandLineArgs));

			populationController.onCreatureFinishedSimulation.AddListener(OnCreatureFinishedSimulation);
			populationController.onOutOfCreatures.AddListener(CheckOutOfAllCreatures);
			populationController.onCreatureStartedSimulation.AddListener(OnCreatureStartedListener);
			// Initialize with passed or default parameters.
			// TODO: use subsystems to set update rate.
			PhysicsUpdateSubsystem physicsSubsystem = SubsystemManagerComponent.Get().GetSubsystem<PhysicsUpdateSubsystem>();
			
			bool realtime = !(isBatchMode || overrideRealtime);

			if (realtime)
			{
				physicsSubsystem.SetFixedDeltaTime();
			}
			else
			{
				physicsSubsystem.SetBatchFixedDeltaTime();
			}

			populationController.Initialize();
			populationController.onError.AddListener(OnPopulationControlError);

			hasInitialized = true;
		}

		
		/// <summary>
		/// Sets up the experiment to start reading creatures.
		/// If a null communicator is passed, this just creates some randomly-generated
		/// creatures and tests them.
		/// If a valid communicator is passed, this starts the reading process, but doesn't finish it.
		/// Reading is performed in Update in this case.
		/// </summary>
		/// <param name="communicator"></param>
		public void StartReadCreatures(ICommunicator communicator)
		{
			if (!hasInitialized)
				Initialize();

			deserializer = new Deserializer_Type();

			if (communicator != null)
			{
				Debug.Log("Reading creatures from pipe.");
				this.communicator = communicator;
			}
			else
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

		/// <summary>
		/// Every frame, check whether there are more creatures to read from a communicator.
		/// </summary>
		private void Update()
		{
			if (communicator == null || deserializer.GetIsDoneReading())
				return;

			foreach (Init_Type creatureData in deserializer.ReadCreatures(communicator))
				populationController.EnqueueCreature(creatureData);
		}

		/// <summary>
		/// When a creature has finished simulation, report back which
		/// creature has finished along with its score.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="score"></param>
		private void OnCreatureFinishedSimulation(Init_Type creature, float score)
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

			// Forfeit read control of the communicator once there are no more creatures to simulate.
			communicator = null;
			OnExpirementTerminated.Invoke();
		}

		/// <summary>
		/// Simply converts the T type to SimulationInitializationData for Dispatcher.
		/// </summary>
		/// <param name="creature"></param>
		private void OnCreatureStartedListener(Init_Type creature)
		{
			OnCreatureStarted.Invoke(creature);
		}

		/// <summary>
		/// Forwards an error to the communicator if one if raised in the populationController.
		/// </summary>
		/// <param name="e">The error gotten.</param>
		private void OnPopulationControlError(Exception e)
		{
			communicator?.WriteError(e.ToString().Replace("\r\n", "\n"));
		}

		// Getters for different Unity events that the dispatcher listens to.
		#region Event Getters
		public UnityEvent<SimulationInitializationData, float, string> GetOnCreatureScoredEvent()
		{
			return OnCreatureScored;
		}

		public UnityEvent GetOnExperimentTerminatedEvent()
		{
			return OnExpirementTerminated;
		}

		public UnityEvent<SimulationInitializationData> GetOnCreatureStartedEvent()
		{
			return OnCreatureStarted;
		}

		#endregion
	}
}
