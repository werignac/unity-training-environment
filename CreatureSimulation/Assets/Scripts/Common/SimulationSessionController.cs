using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using werignac.Utils;
using werignac.Creatures;

namespace werignac.GeneticAlgorithm
{
	public class SimulationSessionController<T_InitData> : MonoBehaviour
	{
		[SerializeField, Tooltip("Monobehaviour that implements IFitnessEvaluator. Judges the current state of one or more creatures and returns a score.")]
		private MonoBehaviour fitness;
		private IFitnessEvaluator _fitness;

		[SerializeField, Tooltip("How long a simulation session runs for (in seconds).")]
		private float simulationDuration = 10f;
		/// <summary>
		/// How long the current simulation has been running for.
		/// </summary>
		private float simulationProgress = 0f;

		/// <summary>
		/// The physics layer used for this simulation. Assumes that all
		/// children of this GameObject are on the same layer.
		/// </summary>
		public string SimulationLayer
		{
			get;
			private set;
		}

		[SerializeField]
		protected GameObject creatureObject;

		public T_InitData CreatureData
		{
			get;
			private set;
		}

		public void Initialize(T_InitData _creatureData, string layer = null)
		{
			CreatureData = _creatureData;

			creatureObject = InitializeCreature();

			_fitness = (IFitnessEvaluator)fitness;
			_fitness.Initialize(creatureObject);

			if (layer != null)
			{
				gameObject.ForEach((GameObject child) => { child.layer = LayerMask.NameToLayer(layer); });
			}

			SimulationLayer = LayerMask.LayerToName(gameObject.layer);
		}

		/// <summary>
		/// Override for creatures who need to create gameobjects / components.
		/// CreatureData is set up when this function is called and can be used for initialization.
		/// </summary>
		/// <returns>The root for the creature.</returns>
		protected virtual GameObject InitializeCreature()
		{
			creatureObject.BroadcastMessage("Initialize", CreatureData, SendMessageOptions.DontRequireReceiver);
			return creatureObject;
		}

		/// <summary>
		/// Called by Population Controller.
		/// </summary>
		/// <returns>Whether the simulation should continue running.</returns>
		public bool OnSimulateStep(out float score)
		{
			// If we've finished simulating, don't perform more simulation steps.
			if (simulationProgress >= simulationDuration)
			{
				score = _fitness.GetScore();
				return false;
			}

			// Tell children that a simulation step has occurred.
			creatureObject.BroadcastMessage("OnSimulateStep", Time.fixedDeltaTime, SendMessageOptions.DontRequireReceiver);

			score = _fitness.Evaluate(creatureObject);

			simulationProgress += Time.fixedDeltaTime;

			// Check session completion
			return simulationProgress >= simulationDuration;
		}
	}
}
