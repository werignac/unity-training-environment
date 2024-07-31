using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace werignac.CartPole3D
{
    public class CartPole3DGoalGenerator : MonoBehaviour
    {
		[Header("Generator Parameters")]
		[SerializeField, Min(0)]
		private float maxVelocity = 10.0f;

		[SerializeField, Min(0)]
		private float directionStep = 1f;
		private float directionXPerlinPosition;
		private float directionZPerlinPosition;

		[SerializeField, Min(0)]
		private float magnitudeStep = 1f;
		private float magnitudePerlinPosition;
		
		Vector2 currentGoal;

		public CartPole3DGoal Goal
		{
			get
			{
				CartPole3DGoal goal = new CartPole3DGoal();
				goal.CartVelocityX = currentGoal.x;
				goal.CartVelocityZ = currentGoal.y;

				return goal;
			}
		}

		public void Initialize(int seed)
		{
			System.Random rng = new System.Random(seed);

			directionXPerlinPosition = (float)((rng.NextDouble() - 0.5) * 2 * 1000);
			directionZPerlinPosition = (float)((rng.NextDouble() - 0.5) * 2 * 1000);
			magnitudePerlinPosition = (float)((rng.NextDouble() - 0.5) * 2 * 1000);
		}

		private void OnSimulateStep(float deltaTime)
		{
			directionXPerlinPosition += deltaTime * directionStep;
			directionZPerlinPosition += deltaTime * directionStep;
			magnitudePerlinPosition += deltaTime * magnitudeStep;

			float directionX = (Mathf.PerlinNoise1D(directionXPerlinPosition) - 0.5f) * 2;
			float directionZ = (Mathf.PerlinNoise1D(directionZPerlinPosition) - 0.5f) * 2;
			float magnitude = Mathf.PerlinNoise1D(magnitudePerlinPosition);

			currentGoal = new Vector2(directionX, directionZ);
			currentGoal = currentGoal.normalized * magnitude * maxVelocity;
		}

#if UNITY_EDITOR
		private void Update()
		{
			Debug.DrawLine(transform.position, transform.position + new Vector3(Goal.CartVelocityX, 0, Goal.CartVelocityZ));
		}
#endif
	}
}
