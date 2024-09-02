/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace werignac.CartPole3D
{
    public class CartPole3DInitialImpulse : MonoBehaviour
    {
		[SerializeField]
		private float maxImpulseMagnitude = 5.0f;

        public void Initialize(int seed)
		{
			System.Random rng = new System.Random(seed);

			float xDirection = (float)((rng.NextDouble() - 0.5) * 2);
			float zDirection = (float)((rng.NextDouble() - 0.5) * 2);
			float magnitude = (float)rng.NextDouble();

			Vector3 impulse = new Vector3(xDirection, 0, zDirection);
			impulse = impulse.normalized * magnitude * maxImpulseMagnitude;

			GetComponent<CartPole3D>().PoleRigidbody.AddForce(impulse, ForceMode.Impulse);
		}
    }
}
