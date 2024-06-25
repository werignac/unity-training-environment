using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using werignac.Creatures;
using System.Threading.Tasks;
using System;
using System.Threading;
using werignac.Subsystem;
using werignac.GeneticAlgorithm.Subsystems;


namespace werignac.GeneticAlgorithm
{
	public class PopulationController<T_InitData> : MonoBehaviour where T_InitData : SimulationInitializationData
	{
		#region Fields
		[SerializeField, Tooltip("Simulation Session gameobject w/ SimulationSessionController (prefab).")]
		private GameObject simulationSessionPrefab;

		/// <summary>
		/// Next creatures that need to be simulated.
		/// Consider having a generic Population\<T\> controller and select
		/// T for single-creature simulation or multi-creature simulations.
		/// </summary>
		private Queue<T_InitData> population = new Queue<T_InitData>();

		// Instances of simulations currently running.
		private HashSet<SimulationSessionController<T_InitData>> simulations = new HashSet<SimulationSessionController<T_InitData>>();

		/// <summary>
		/// The physics layers the sessions may use to run multiple sessions in parallel.
		/// </summary>
		[SerializeField]
		private List<string> sessionLayers = new List<string>();
		private HashSet<string> _sessionLayers;

		#endregion Fields

		#region Events
		// --- Events ---
		public UnityEvent onOutOfCreatures = new UnityEvent();
		public UnityEvent<T_InitData, float> onCreatureFinishedSimulation = new UnityEvent<T_InitData, float>();
		public UnityEvent<T_InitData> onCreatureStartedSimulation = new UnityEvent<T_InitData>();
		public UnityEvent<Exception> onError = new UnityEvent<Exception>();
		#endregion Events

		// --- Debugging ---
		[Header("Debug")]
		[SerializeField, Tooltip("Whether to do certain Debug.Logs.")]
		private bool debug;

		private void Awake()
		{
			// Physics settings set in experiment.
			PhysicsUpdateSubsystem physicsSubsystem = SubsystemManagerComponent.Get().GetSubsystem<PhysicsUpdateSubsystem>();
			physicsSubsystem.onPhysicsStep.AddListener(SimulateStep);
		}

		public void Initialize()
		{
			_sessionLayers = new HashSet<string>(sessionLayers);
		}

		/// <summary>
		/// Adds a creature to the population queue.
		/// Expected to be called asynchornously (hence the lock).
		/// </summary>
		/// <param name="creature"></param>
		public void EnqueueCreature(T_InitData creature)
		{
			lock (population)
			{
				population.Enqueue(creature);
				PopulationStep();
			}
		}

		/// <summary>
		/// Simulates a physics step for all active simulation instances.
		/// </summary>
		private void SimulateStep(float deltaTime)
		{
			var toDestroys = new HashSet<SimulationSessionController<T_InitData>>();

			Physics.Simulate(Time.fixedDeltaTime);

			SimulationSessionController<T_InitData>[] simulationsArray = new SimulationSessionController<T_InitData>[simulations.Count];
			simulations.CopyTo(simulationsArray);

			// Perform the synchronous step of simulation.
			// e.g. Tell the creatures to do any work on this frame of the simulation.
			for (int i = 0; i < simulations.Count; i++)
			{
				simulationsArray[i].SimulateStep();
			}

			// Perform the asynchronous step of simulaiton.
			Task[] simulationTasks = new Task[simulationsArray.Length];

			for (int i = 0; i < simulations.Count; i++)
			{
				simulationTasks[i] = simulationsArray[i].SimulateStepAsync();
			}

			try
			{
				// Wait for all creatures to finish their work.
				bool success = Task.WaitAll(simulationTasks, 10000);

				if (!success)
					Debug.LogError("Async step timed out.");
			}
			catch (Exception e)
			{
				Debug.LogError(e);
				onError.Invoke(e);
			}

			// Perform the post-asynchronous step of simulation
			for (int i = 0; i < simulations.Count; i++)
			{
				var simulation = simulationsArray[i];
				simulation.PostAsyncStep();
				// If the simulation has run its course, mark it for deletion.
				if (simulation.GetHasFinished())
				{
					onCreatureFinishedSimulation.Invoke(simulation.CreatureData, simulation.GetScore());
					toDestroys.Add(simulation);
				}
			}
			
			// Destroy the simulations that have completed.
			foreach (SimulationSessionController<T_InitData> toDestroy in toDestroys)
			{
				simulations.Remove(toDestroy);
				DestroyImmediate(toDestroy.gameObject);
				// Locked because EnqueueCreature can be called asynchronously.
				lock (population)
				{
					PopulationStep();
				}
			}
		}

		private void CreateSimulationSessionInstance(T_InitData creature, string layer = null)
		{
			if (debug)
				Debug.LogFormat("Simulating Generation Creature: {0}", creature.Index);

			var _instance = Instantiate(simulationSessionPrefab);
			SimulationSessionController<T_InitData> _simulationSessionController = _instance.GetComponent<SimulationSessionController<T_InitData>>();
			_simulationSessionController.Initialize(creature, layer);

			simulations.Add(_simulationSessionController);
			onCreatureStartedSimulation.Invoke(creature);
		}

		/// <summary>
		/// Tries to pop a creature off the population queue and make a simulation instance.
		/// Fails if we're at max capacity for simulation instances.
		/// Fails and fires an event if we've run out of creatures to simulate and all our simulations have finished.
		/// </summary>
		private void PopulationStep()
		{
			if (population.Count == 0)
			{
				// Check we've gotten back from all simulations instead of looking at population.Count (whether we've started all simulaitons).
				if (simulations.Count == 0)
					onOutOfCreatures.Invoke();
				// Don't fire an event if we haven't finished simulating everything.
				return;
			}

			// If we're at max capacity for simulations, don't create another simulation
			// until one of the existing ones has finished.
			if (simulations.Count >= _sessionLayers.Count)
				return;

			// The previous check confirms that there's a simulation layer avilable.
			// Find and use it for the new simulation.
			HashSet<string> unusedSessionLayers = new HashSet<string>(_sessionLayers);
			foreach (SimulationSessionController<T_InitData> controller in simulations)
				unusedSessionLayers.Remove(controller.SimulationLayer);
			string layer = null;
			foreach (string _layer in unusedSessionLayers)
			{
				layer = _layer;
				break;
			}

			// If there are creatures to test and we still have slots
			// for simulation, create a new simulation instance.
			CreateSimulationSessionInstance(population.Dequeue(), layer);
			return;
		}

		/// <summary>
		/// Is not simulating creatures and has no creatures queued.
		/// </summary>
		/// <returns></returns>
		public bool IsOutOfCreatures()
		{
			return (population.Count == 0) && (simulations.Count == 0);
		}
	}
}
