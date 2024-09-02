/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.CartPole3D
{
    public class CartPole3DController : MonoBehaviour
    {
		[Header("Controller Parameters")]
		[SerializeField, Min(0)]
		private float moveForce = 100.0f;

		private CartPole3D cartPole;
		
		/// <summary>
		/// Between-frame data.
		/// </summary>
		private CartPole3DCommand command;

		private void Awake()
		{
			cartPole = GetComponent<CartPole3D>();
			GetComponent<ICartPole3DOutput>().AssignCallback(AcceptInput);
		}

		private void AcceptInput(CartPole3DCommand _command)
		{
			// Store input temporarily.
			command = _command;
		}

		private void OnPostSimulateStepAsync(float deltaTime)
		{
			Vector3 drive3d = new Vector3(command.Drive.x, 0, command.Drive.y);
			cartPole.CartRigidbody.AddForce(drive3d.normalized * moveForce);
		}
	}
}
