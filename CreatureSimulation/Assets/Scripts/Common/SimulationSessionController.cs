using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using werignac.Utils;
using werignac.Creatures;

namespace werignac.GeneticAlgorithm
{
	public class SimulationSessionController<T> : MonoBehaviour
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

		public T CreatureData
		{
			get;
			private set;
		}

		public void Initialize(T _creatureData, string layer = null)
		{
			CreatureData = _creatureData;

			_fitness = (IFitnessEvaluator)fitness;
			_fitness.Initialize(creatureObject);

			if (layer != null)
			{
				gameObject.ForEach((GameObject child) => { child.layer = LayerMask.NameToLayer(layer); });
			}

			SimulationLayer = LayerMask.LayerToName(gameObject.layer);

			InitializeCreature();
		}

		/// <summary>
		/// Sets up the Creature using CreatureData.
		/// </summary>
		protected virtual void InitializeCreature() {}

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

			// TODO: Fire Neurons
			score = _fitness.Evaluate(creatureObject);

			simulationProgress += Time.fixedDeltaTime;

			// Check session completion
			return simulationProgress >= simulationDuration;
		}
	}
}
