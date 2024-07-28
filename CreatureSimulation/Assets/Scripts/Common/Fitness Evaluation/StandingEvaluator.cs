using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using werignac.Utils;

namespace werignac.GeneticAlgorithm
{
	public class StandingEvaluator : MonoBehaviour, IFitnessEvaluator
	{
		/// <summary>
		/// How well the creature is performing.
		/// </summary>
		private float cumulativeScore = 0f;
		/// <summary>
		/// Keeps track of the spinal parts of the creature.
		/// The spinal parts should remain close to their initial rotations.
		/// </summary>
		private Dictionary<GameObject, Quaternion> spines = new Dictionary<GameObject, Quaternion>();

#if UNITY_EDITOR
		[Header("Debug")]
		[SerializeField, Tooltip("Whether to do Debug.Logs. Only applicable to in-editor.")]
		private bool debug = false;
#endif

		public void Initialize(GameObject creature)
		{
			creature.ForEach(this.RegisterBodyPart);
		}

		private void RegisterBodyPart(GameObject bodyPart)
		{
			if (bodyPart.CompareTag("Spine"))
			{
				RegisterSpine(bodyPart);
			}
		}

		private void RegisterSpine(GameObject spine)
		{
			spines.Add(spine, spine.transform.localRotation);
		}

		public float Evaluate(GameObject creature, out bool terminateEarly)
		{
			terminateEarly = false;

			creature.ForEach(this.EvaluateVelocity);
			creature.ForEach(this.EvaluateSpine);

#if UNITY_EDITOR
			if (debug)
				Debug.LogFormat("Creature Score: {0}", cumulativeScore);
#endif

			return cumulativeScore;
		}

		private void EvaluateVelocity(GameObject bodyPart)
		{
			ArticulationBody body = bodyPart.GetComponent<ArticulationBody>();

			if (!body)
			{
				// Uncommented for GFX bodies
				//Debug.LogWarningFormat("Cannot evaluate velocity of body part with no ArticulationBody {0}.", bodyPart.name);
				return;
			}

			float linearMomentum = Mathf.Abs(body.mass * body.velocity.magnitude);
			float angularMomentum = Mathf.Abs((InertiaTensorUtils.CalculateWorldInertialTensorMatrix(body) * body.angularVelocity).magnitude);

			float deltaScore = (0.5f - linearMomentum) + (0.5f - angularMomentum);

#if UNITY_EDITOR
			if (debug)
				Debug.LogFormat("Momentum Body Part: {0}\n\tLinear Momentum: {1}\n\tAngular Momentum: {2}\n\tDelta Score: {3}", bodyPart.name, linearMomentum, angularMomentum, deltaScore);
#endif

			cumulativeScore += deltaScore;
		}

		private void EvaluateSpine(GameObject bodyPart)
		{
			if (!bodyPart.CompareTag("Spine"))
				return;

			float angleDiff = Quaternion.Angle(bodyPart.transform.rotation, this.spines[bodyPart]);
			float deltaScore = 10f - angleDiff;

#if UNITY_EDITOR
			if (debug)
				Debug.LogFormat("Spine Body Part: {0}\n\tAngle Difference: {1}\n\tDelta Score: {2}", bodyPart.name, angleDiff, deltaScore);
#endif
			cumulativeScore += deltaScore;
		}

		public float GetScore()
		{
			return cumulativeScore;
		}
	}
}
