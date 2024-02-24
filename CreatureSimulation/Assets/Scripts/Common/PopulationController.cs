using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using werignac.Creatures;


namespace werignac.GeneticAlgorithm
{
	public class PopulationController<T> : MonoBehaviour where T : SimulationInitializationData
	{
		#region Fields
		[SerializeField, Tooltip("Simulation Session gameobject w/ SimulationSessionController (prefab).")]
		private GameObject simulationSessionPrefab;

		/// <summary>
		/// Next creatures that need to be simulated.
		/// TODO: Change "Object" to be simulation data.
		/// Consider having a generic Population\<T\> controller and select
		/// T for single-creature simulation or multi-creature simulations.
		/// </summary>
		private Queue<T> population = new Queue<T>();

		// Instances of simulations currently running.
		private HashSet<SimulationSessionController<T>> simulations = new HashSet<SimulationSessionController<T>>();

		/// <summary>
		/// The physics layers the sessions may use to run multiple sessions in parallel.
		/// </summary>
		[SerializeField]
		private List<string> sessionLayers = new List<string>();
		private HashSet<string> _sessionLayers;

		// Physics Simulation Parameters
		/// <summary>
		/// False: runs physics simulation as fast as possible.
		/// True: runs physics simulation at the same speed as realtime.
		/// </summary>
		private bool realtime;
		/// <summary>
		/// Amount of time since the last physics step. Used when realtime is true.
		/// </summary>
		private float timeSinceLastUpdate;

		#endregion Fields

		#region Events
		// --- Events ---
		public UnityEvent onOutOfCreatures = new UnityEvent();
		public UnityEvent<T, float> onCreatureFinishedSimulation = new UnityEvent<T, float>();
		#endregion Events

		// --- Debugging ---
		[Header("Debug")]
		[SerializeField, Tooltip("Whether to do certain Debug.Logs.")]
		private bool debug;

		public void Initialize(bool _realtime)
		{
			realtime = _realtime;
			_sessionLayers = new HashSet<string>(sessionLayers);
		}

		/// <summary>
		/// Adds a creature to the population queue.
		/// Expected to be called asynchornously (hence the lock).
		/// </summary>
		/// <param name="creature"></param>
		public void EnqueueCreature(T creature)
		{
			lock (population)
			{
				population.Enqueue(creature);
				PopulationStep();
			}
		}

		private void Update()
		{
			// If we're not in the correct simulation mode, some settings are not set up.
			if (Physics.simulationMode != SimulationMode.Script)
			{
				Debug.LogWarningFormat("Expected Physics.simulationMode to be {0}, but was {1}. Check player settings.", SimulationMode.Script, Physics.simulationMode);
				return;
			}

			// Don't do anything if we have nothing to simulate.
			if (simulations.Count == 0)
				return;

			// If running in realtime...
			if (realtime)
			{
				// See how much time has passed since the last frame.
				timeSinceLastUpdate += Time.deltaTime;
				// If more time has passed than the length of a physics simulation step,
				// advance the physics simulation.
				for (int i = 0; i < Mathf.FloorToInt(timeSinceLastUpdate / Time.fixedDeltaTime); i++)
				{
					SimulateStep();
				}
				// Reset the timer if we simulated steps.
				timeSinceLastUpdate = timeSinceLastUpdate % Time.fixedDeltaTime;
			}
			else
			{ // If not running in realtime...
				// Simulate as fast as possible.
				SimulateStep();
			}
		}

		/// <summary>
		/// Simulates a physics step for all active simulation instances.
		/// </summary>
		private void SimulateStep()
		{
			var toDestroys = new HashSet<SimulationSessionController<T>>();
			
			Physics.Simulate(Time.fixedDeltaTime);
			foreach(SimulationSessionController<T> controller in simulations)
			{
				bool finished = controller.OnSimulateStep(out float score);

				if (finished)
				{
					onCreatureFinishedSimulation.Invoke(controller.CreatureData, score);
					toDestroys.Add(controller);
				}
			}

			foreach(SimulationSessionController<T> toDestroy in toDestroys)
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

		private void CreateSimulationSessionInstance(T creature, string layer = null)
		{
			if (debug)
				Debug.LogFormat("Simulating Generation Creature: {0}", creature.Index);

			// TODO: Use active simulations to find a physics layer for new simulation.

			var _instance = Instantiate(simulationSessionPrefab);
			SimulationSessionController<T> _simulationSessionController = _instance.GetComponent<SimulationSessionController<T>>();
			_simulationSessionController.Initialize(creature, layer);

			simulations.Add(_simulationSessionController);
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
			foreach(SimulationSessionController<T> controller in simulations)
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
