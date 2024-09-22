/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;

namespace werignac.CartPole
{
    public class RandomWind : MonoBehaviour
    {
		[Header("Parameters")]
		[SerializeField, Min(0.001f)]
		private float perlinStepMultiplier = 0.1f;

		[SerializeField]
		private float maxForce = 100f;

		[SerializeField]
		private ArticulationBody pole;

		[Header("Events")]

		public UnityEvent<float, float> onWindUpdate = new UnityEvent<float, float>();

		private float windState;

		public void Initialize(int seed)
		{
			System.Random rng = new System.Random(seed);
			windState = (float)rng.NextDouble();
		}

		public virtual void OnSimulateStep(float deltaTime)
		{
			windState += deltaTime * perlinStepMultiplier;
			SetWind(GetNormalizedWind());
		}

		protected void SetWind(float normalizedWind)
		{
			float wind = normalizedWind * maxForce;
			pole.AddForce(Vector3.forward * wind);

			onWindUpdate.Invoke(wind, normalizedWind);
		}

		public virtual float GetNormalizedWind()
		{
			float windRand = Mathf.PerlinNoise1D(windState);
			return (0.5f - windRand) * 2;
		}

		public float GetWind()
		{
			return GetNormalizedWind() * maxForce;
		}
	}
}
