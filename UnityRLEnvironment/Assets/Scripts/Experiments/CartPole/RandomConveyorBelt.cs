/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;

namespace werignac.CartPole
{
    public class RandomConveyorBelt : MonoBehaviour
    {
		[Header("Parameters")]
		[SerializeField, Min(0.001f)]
		private float perlinStepMultiplier = 0.1f;

		[SerializeField, Min(0)]
		private float maxConveyorSpeed = 10f;

		[SerializeField]
		private ArticulationBody cart;

		[Header("Events")]

		public UnityEvent<float, float> onConveyorUpdate = new UnityEvent<float, float>();

		private float conveyorState;

		public void Initialize(int seed)
		{
			System.Random rng = new System.Random(seed);
			conveyorState = (float)rng.NextDouble();
		}

		public virtual void OnSimulateStep(float deltaTime)
		{
			conveyorState += deltaTime * perlinStepMultiplier;
			ApplyConveyorVelocity(GetRandomNormalizedConveyorVelocity());
		}

		protected void ApplyConveyorVelocity(float normalizedConveyorVelocity)
		{
			// Get the velocity of the conveyor belt.
			float conveyorVelocity = normalizedConveyorVelocity * maxConveyorSpeed;
			// Move the cart in accordance with the conveyor belt's speed.
			// The conveyor belt moves the whole cart.
			cart.jointPosition = new ArticulationReducedSpace(cart.jointPosition[0] + conveyorVelocity * Time.fixedDeltaTime);
			onConveyorUpdate.Invoke(conveyorVelocity, normalizedConveyorVelocity);
		}

		public virtual float GetRandomNormalizedConveyorVelocity()
		{
			float conveyorRand = Mathf.PerlinNoise1D(conveyorState);
			return (0.5f - conveyorRand) * 2;
		}

		public float GetRandomConveyorVelocity()
		{
			return GetRandomNormalizedConveyorVelocity() * maxConveyorSpeed;
		}
	}
}
