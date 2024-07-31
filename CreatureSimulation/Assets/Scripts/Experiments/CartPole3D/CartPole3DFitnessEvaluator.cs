using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;
using werignac.Utils;

namespace werignac.CartPole3D
{
	public class CartPole3DFitnessEvaluator : MonoBehaviour, IFitnessEvaluator
	{
		[Header("Limits")]
		[SerializeField, Range(0, 90), Tooltip("How far the pole can tilt before the simulation ends in a loss.")]
		private float angleLimit = 24f;

		[Header("Fitness Evaluator Parameters")]
		[SerializeField, Min(0), Tooltip("Low value = strict; high value = tolerant. Similar to an STD of velocity reward.")]
		private float velocityTolerance = 1.0f;
		[SerializeField, Min(0), Tooltip("How many points to lose on a loss.")]
		private float lossPenalty = 10;

		private CartPole3D cartPole;
		private CartPole3DGoalGenerator goalGenerator;

		private float score = 0;

		public float Evaluate(GameObject creature, out bool terminateEarly)
		{
			terminateEarly = false;

			CartPole3DGoal goal = goalGenerator.Goal;
			Vector3 difference = cartPole.CartRigidbody.velocity - new Vector3(goal.CartVelocityX, 0, goal.CartVelocityZ);
			float differenceMag = difference.magnitude;

			// Bell Curve with mean=0 and max height is 1: e ^ (-0.5 * ((x / sigma) ^ 2))
			float deltaScore = Mathf.Exp(-0.5f * Mathf.Pow((differenceMag) / (velocityTolerance), 2));
			score += deltaScore;

			float poleAngle = Vector3.Angle(cartPole.PoleRigidbody.transform.up, Vector3.up);
			if (poleAngle > angleLimit)
			{
				terminateEarly = true;
				score -= lossPenalty;
			}

			return score;
		}

		public float GetScore()
		{
			return score;
		}

		public void Initialize(GameObject creature)
		{
			cartPole = creature.GetComponent<CartPole3D>();
			goalGenerator = creature.GetComponent<CartPole3DGoalGenerator>();
		}
	}
}
