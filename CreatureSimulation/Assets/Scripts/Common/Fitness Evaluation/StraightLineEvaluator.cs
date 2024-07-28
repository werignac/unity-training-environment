using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;

namespace werignac.GeneticAlgorithm
{
	public class StraightLineEvaluator : MonoBehaviour, IFitnessEvaluator
	{
		Vector2 goodDirection = Vector2.up;
		Vector2 initialPoint;
		float score;

		public float Evaluate(GameObject creature, out bool terminateEarly)
		{
			terminateEarly = false;

			ArticulationBody body = creature.GetComponentInChildren<ArticulationBody>();
			Vector2 currentPoint = new Vector2(body.worldCenterOfMass.x, body.worldCenterOfMass.z);

			Vector2 difference = currentPoint - initialPoint;
			float goodProjection = Vector2.Dot(goodDirection, difference);
			float badProjection = (difference - (goodProjection * goodDirection)).magnitude;

			score = goodProjection - badProjection;

			return score;
		}

		public float GetScore()
		{
			return score;
		}

		public void Initialize(GameObject creature)
		{
			ArticulationBody body = creature.GetComponentInChildren<ArticulationBody>();
			initialPoint = new Vector2(body.worldCenterOfMass.x, body.worldCenterOfMass.z);
			goodDirection.Normalize();
		}
	}
}
