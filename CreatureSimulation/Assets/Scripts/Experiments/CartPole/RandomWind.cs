using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace werignac.CartPole
{
    public class RandomWind : MonoBehaviour
    {
		private System.Random rng;

		[SerializeField]
		private float maxForce = 100f;

		[SerializeField]
		private ArticulationBody pole;

		public void Initialize(int seed)
		{
			rng = new System.Random(seed);
		}

		public void OnSimulateStep(float deltaTime)
		{
			float random = (float)rng.NextDouble();
			pole.AddForce(Vector3.forward * (0.5f - random) * 2 * maxForce);
		}
	}
}
