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
using werignac.GeneticAlgorithm.Dispatch;

namespace werignac.GeneticAlgorithm
{
	/// <summary>
	/// Interface used by Dispatcher to interact with Experiments.
	/// </summary>
	public interface IExperiment
	{
		public void StartReadCreatures();
		public UnityEvent<SimulationInitializationData, float, string> GetOnCreatureScoredEvent();
		public UnityEvent GetOnExperimentTerminatedEvent();
		public UnityEvent<SimulationInitializationData> GetOnCreatureStartedEvent();
	}

	/// <summary>
	/// TODO: Get rid of CreatureReader.
	/// </summary>
	/// <typeparam name="Init_Type">The type of the serialized objects read to initialize creatures.</typeparam>
	/// <typeparam name="Random_Init_Type">An type that generates random creatures.</typeparam>
	public abstract class Experiment<Init_Type, Random_Init_Type, Serializable_Init_Type> : MonoBehaviour, IExperiment where Init_Type : SimulationInitializationData where Random_Init_Type : RandomSimulationInitializationDataFactory<Init_Type>, new() where Serializable_Init_Type : new()
	{
		#region Parsing

		Dispatcher dispatcher = null;

		/// <summary>
		/// The stack of parsers that all objects share.
		/// TODO: replace with reference to Dispatcher as subsystem.
		/// </summary>
		ParserStack parserStack = null; // TODO: listen for IsFinishedReadingCreatures / OnFinishedReadingCreatures before adding a new parser to multiplex to creatures.

		/// <summary>
		/// The parser that is used whilst the experiment is 
		/// accepting new creatures.
		/// 
		/// null if the experiment is not accepting new creatures.
		/// </summary>
		private JsonParser<Serializable_Init_Type> jsonParser = null;

		/// <summary>
		/// Gets whether the JsonParser has finished reading creatures.
		/// If generating from a random list, this always returns true.
		/// 
		/// Used by children to see if they need to wait before pushing a new
		/// parser to the parser stack.
		/// </summary>
		public bool IsFinishedReadingCreatures { get { return jsonParser == null; } }

		/// <summary>
		/// The number of creatures read for this experiment. Resets
		/// when an experiment is re-run. Used to create the index of a creature.
		/// </summary>
		private int numberOfCreaturesRead = 0;

		#endregion Parsing

		[SerializeField]
		private PopulationController<Init_Type> populationController;

		[Header("Simulation Settings")]
		[SerializeField, Tooltip("The number of creatures to simulate if there is no pipe."), Min(0)]
		private int noPipePopulationSize;
		private Random_Init_Type randomDataFactory = new Random_Init_Type();

		private bool hasInitialized = false;

		[Header("Events")]
		[Tooltip("Called when a creature has finished being scored. Used to print results.")]
		public UnityEvent<SimulationInitializationData, float, string> OnCreatureScored = new UnityEvent<SimulationInitializationData, float, string>();
		public UnityEvent OnExpirementTerminated = new UnityEvent();
		/// <summary>
		/// All this does is convert the T type to the SimulationInitializationData type for Dispatcher.
		/// </summary>
		public UnityEvent<SimulationInitializationData> OnCreatureStarted = new UnityEvent<SimulationInitializationData>();
		/// <summary>
		/// Invoked when the JsonParser has detected it reached the end of the list of objects to parse.
		/// </summary>
		public UnityEvent OnFinishedReadingCreatures = new UnityEvent();

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
				StartReadCreatures();
		}
#endif

		/// <summary>
		/// Listen to events, read command line arguments, set whether the physics should
		/// be realtime, etc.
		/// </summary>
		private void Initialize()
		{
			string[] commandLineArgs = Environment.GetCommandLineArgs();

			Debug.LogFormat("Command Line Args: [{0}]", string.Join(", ", commandLineArgs));

			// Listen to events.
			populationController.onCreatureFinishedSimulation.AddListener(OnCreatureFinishedSimulation);
			populationController.onOutOfCreatures.AddListener(CheckOutOfAllCreatures);
			populationController.onCreatureStartedSimulation.AddListener(OnCreatureStartedListener);
			
			// Set the physics step rate to be either realtime, or as fast as possible.
			bool isBatchMode = Application.isBatchMode;
			bool realtime = !(isBatchMode || overrideRealtime);

			PhysicsUpdateSubsystem physicsSubsystem = SubsystemManagerComponent.Get().GetSubsystem<PhysicsUpdateSubsystem>();
			if (realtime)
			{
				physicsSubsystem.SetFixedDeltaTime();
			}
			else
			{
				physicsSubsystem.SetBatchFixedDeltaTime();
			}

			// Set up the population controller.
			populationController.Initialize();
			populationController.onError.AddListener(OnPopulationControlError);

			PostInitialize();

			hasInitialized = true;
		}

		/// <summary>
		/// A method for experiments to add their own initialization logic.
		/// </summary>
		protected virtual void PostInitialize() { }

		
		/// <summary>
		/// Sets up the experiment to start reading creatures.
		/// If a null communicator is passed, this just creates some randomly-generated
		/// creatures and tests them.
		/// If a valid communicator is passed, this starts the reading process, but doesn't finish it.
		/// Reading is performed in Update in this case.
		/// </summary>
		/// <param name="communicator"></param>
		public void StartReadCreatures()
		{
			dispatcher = SubsystemManagerComponent.Get().GetSubsystem<Dispatcher>();

			if (!hasInitialized)
				Initialize();

			if (dispatcher != null)
			{
				// The index of the first creature is zero.
				numberOfCreaturesRead = 0;
				Debug.Log("Reading creatures from pipe.");
				parserStack = dispatcher.ParserStack;
				jsonParser = parserStack.AddParser<JsonParser<Serializable_Init_Type>>();
			}
			else
			{
				Debug.Log($"ReadCreatures called, but no pipe detected. Generating {noPipePopulationSize} creatures.");
				for (int i = 0; i < noPipePopulationSize; i++)
				{
					populationController.EnqueueCreature(randomDataFactory.GenerateRandomData(i));
				}

				numberOfCreaturesRead = noPipePopulationSize;
				populationController.StartSimulations();

#if UNITY_EDITOR
				if (breakAfterReadCreatures)
				{
					Debug.Break();
				}
#endif
			}
		}

		/// <summary>
		/// Convert the json-serializable object into a useful object
		/// with useful types (e.g. x y z -> unity Vector).
		/// </summary>
		/// <param name="serializedInit"></param>
		/// <returns></returns>
		protected abstract Init_Type SerializedToInitData(int index, Serializable_Init_Type serializedInit);

		private void OnParsedJsonCreature(JsonCommand<Serializable_Init_Type> toEnqueue)
		{
			foreach (Serializable_Init_Type serializableCreatureData in toEnqueue.DeserializedObjects)
				populationController.EnqueueCreature(SerializedToInitData(numberOfCreaturesRead++, serializableCreatureData));

			// When we've reached the end of the list of objects,
			// remove the all-consuming json parser.
			if (toEnqueue.IsEnd)
			{
				populationController.StartSimulations();
				parserStack.PopParser(jsonParser);
				parserStack = null;
				jsonParser = null;
				OnFinishedReadingCreatures.Invoke();

#if UNITY_EDITOR
				if (breakAfterReadCreatures)
				{
					Debug.Break();
				}
#endif
			}
		}

		private void Update()
		{
			if (jsonParser != null && jsonParser.Next(out JsonCommand<Serializable_Init_Type> command))
			{
				OnParsedJsonCreature(command);
				dispatcher.CommunicatorBuffer.AcceptNext();
			}
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
			if (!(jsonParser == null && populationController.IsOutOfCreatures()))
				return;

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
			// TODO: Write an error using a reference to the dispatcher subsystem.
			//communicator?.WriteError(e.ToString().Replace("\r\n", "\n"));
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
