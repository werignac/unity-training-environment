using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;


namespace werignac.CartPole
{
	public class CartPoleEvaluator : MonoBehaviour, IFitnessEvaluator
	{
		[Header("Bounds")]
		[SerializeField, Tooltip("How far (in degrees) the pole can tilt before the session terminates.")]
		private float angleLimit = 24f;

		[SerializeField, Tooltip("How far the cart can move before the session terminates.")]
		private float cartLimit = 5f;

		[Header("CartPole Parts")]
		[SerializeField]
		private ArticulationBody pole;

		[SerializeField]
		private ArticulationBody cart;

		/// <summary>
		/// Running score. +1 for each frame without losing.
		/// </summary>
		private int score = 0;

		public float Evaluate(GameObject creature, out bool terminateEarly)
		{
			terminateEarly = false;

			score += 1;

			if (Mathf.Abs(pole.jointPosition[0] * Mathf.Rad2Deg) > angleLimit)
				terminateEarly = true;

			if (Mathf.Abs(cart.transform.position.z) > cartLimit)
				terminateEarly = true;

			return score;
		}

		public float GetScore()
		{
			return score;
		}

		public void Initialize(GameObject creature)
		{
			
		}
	}
}
