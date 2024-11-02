/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;


namespace werignac.CartPole
{
	public class CartPoleEvaluator : MonoBehaviour, IFitnessEvaluator
	{
		[Header("Bounds")]
		[SerializeField, Tooltip("How far (in degrees) the pole can tilt before the session terminates.")]
		private float angleLimit = 24f;

		public float AngleLimit
		{
			get
			{
				return angleLimit;
			}
		}

		[SerializeField, Tooltip("How far the cart can move before the session terminates.")]
		private float cartLimit = 5f;

		public float CartLimit {
			get
			{
				return cartLimit;
			}
		}

		[Header("CartPole Parts")]
		[SerializeField]
		private ArticulationBody pole;

		[SerializeField]
		private ArticulationBody cart;

		[Header("Scoring")]
		[SerializeField, Min(0)]
		private int lossPenalty = 10;

		/// <summary>
		/// Running score. +1 for each frame without losing.
		/// </summary>
		private int score = 0;

		public float Evaluate(GameObject creature, out bool terminateEarly)
		{
			terminateEarly = false;

			bool exceedingAngleLimit = Mathf.Abs(pole.jointPosition[0] * Mathf.Rad2Deg) > angleLimit;
			bool exceedingTranslationLimit = Mathf.Abs(cart.transform.position.z) > cartLimit;

			if (exceedingAngleLimit || exceedingTranslationLimit)
			{
				terminateEarly = true;
				score -= lossPenalty;
			}
			else
			{
				score += 1;
			}

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
