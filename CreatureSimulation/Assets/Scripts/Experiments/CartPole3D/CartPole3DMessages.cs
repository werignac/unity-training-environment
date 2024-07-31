using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.CartPole3D
{
	public struct CartPole3DState
	{
		public CartPole3DState(CartPole3D cartPole)
		{
			CartVelocityX = cartPole.CartRigidbody.velocity.x;
			// NOTE: We use the z angle for x and x angle for z because rotation 
			// around the z axis turns into velocity along the x axis and vice versa.
			PoleAngularPositionX = cartPole.PoleAngularPosition.y;
			PoleAngularVelocityX = cartPole.PoleRigidbody.angularVelocity.z;

			CartVelocityZ = cartPole.CartRigidbody.velocity.z;
			PoleAngularPositionZ = cartPole.PoleAngularPosition.x;
			PoleAngularVelocityZ = cartPole.CartRigidbody.angularVelocity.z;
		}

		public float CartVelocityX { get; set; }
		public float PoleAngularPositionX { get; set; }
		public float PoleAngularVelocityX { get; set; }
		public float CartVelocityZ { get; set; }
		public float PoleAngularPositionZ { get; set; }
		public float PoleAngularVelocityZ { get; set; }
	}

	public struct CartPole3DGoal
	{
		public float CartVelocityX { get; set; }
		public float CartVelocityZ { get; set; }
	}

	public struct CartPole3DFrameData
	{
		public CartPole3DFrameData(CartPole3DState state, CartPole3DGoal goal, float score)
		{
			State = state;
			Goal = goal;
			Score = score;
		}

		public CartPole3DState State { get; set; }
		public CartPole3DGoal Goal { get; set; }
		public float Score { get; set; }
	}

	public struct CartPole3DCommand
	{
		public Vector2 Drive;
	}
}
