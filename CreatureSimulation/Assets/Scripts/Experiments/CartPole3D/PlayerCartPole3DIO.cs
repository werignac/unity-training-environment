using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.CartPole3D
{
	public class PlayerCartPole3DIO : MonoBehaviour, ICartPole3DIO
	{
		private Action<CartPole3DCommand> callback;

		public void AssignCallback(Action<CartPole3DCommand> _callback)
		{
			callback = _callback;
		}

		public void SendFrameData(CartPole3DFrameData frameData)
		{
			CartPole3DCommand command = new CartPole3DCommand();
			float driveX = Input.GetKey(KeyCode.D) ? 1 : -1;
			float driveZ = Input.GetKey(KeyCode.W) ? 1 : -1;
			command.Drive = new Vector2(driveX, driveZ);
			callback(command);
		}
	}
}
