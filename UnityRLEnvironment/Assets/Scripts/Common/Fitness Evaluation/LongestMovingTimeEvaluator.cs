/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.RLEnvironment
{
    public class LongestMovingTimeEvaluator : MonoBehaviour, IFitnessEvaluator
    {
		private int framesSpentMoving;

		[SerializeField, Tooltip("The threshold for determining whether an object is moving.")]
		private float epsilon = 0.001f;
		
		[SerializeField, Tooltip("The number of frames the evaluator can wait before it terminates early. ")]
		private int stillnessLimit = 5;
		private int stillnessTimer = 0;
		public float Evaluate(GameObject creature, out bool terminateEarly)
		{
			terminateEarly = false;

			bool isMoving = false;

			foreach (ArticulationBody ab in creature.GetComponentsInChildren<ArticulationBody>())
			{
				if (ab.velocity.magnitude > epsilon)
				{
					isMoving = true;
					break;
				}
			}

			if (isMoving)
			{
				framesSpentMoving += 1;
				stillnessTimer = 0;
			}
			else
			{
				stillnessTimer += 1;
				terminateEarly = (stillnessTimer >= stillnessLimit);
			}

			return framesSpentMoving;
		}

		public float GetScore()
		{
			return framesSpentMoving;
		}

		public void Initialize(GameObject creature)
		{
		}
    }
}
