using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;

namespace werignac.GeneticAlgorithm
{
	public class HighVelocityEvaluation : MonoBehaviour, IFitnessEvaluator
	{
		private float highestVel = 0;

		public float Evaluate(GameObject creature)
		{
			foreach (ArticulationBody ab in creature.GetComponentsInChildren<ArticulationBody>())
			{
				highestVel = Mathf.Max(highestVel, ab.velocity.magnitude);
			}

			return highestVel;
		}

		public float GetScore()
		{
			return highestVel;
		}

		public void Initialize(GameObject _creature)
		{
		}
	}
}
