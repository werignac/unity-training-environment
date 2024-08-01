using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;

namespace werignac.CartPole3D
{
	public struct DeserializedCartPole3DInitializationData
	{
		public int GoalGeneratorSeed { get; set; }
		public int InitialImpulseSeed { get; set; }
	}

	public class CartPole3DInitializationData : SimulationInitializationData
	{
		public CartPole3DInitializationData(int i) : base(i) { }

		public CartPole3DInitializationData(int i, DeserializedCartPole3DInitializationData data) : base(i)
		{
			GoalGeneratorSeed = data.GoalGeneratorSeed;
			InitialImpulseSeed = data.InitialImpulseSeed;
		}

		public int GoalGeneratorSeed { get; private set; }

		public int InitialImpulseSeed { get; private set; }
	}

	public class RandomCartPole3D : RandomSimulationInitializationDataFactory<CartPole3DInitializationData>
	{
		public CartPole3DInitializationData GenerateRandomData(int i)
		{
			DeserializedCartPole3DInitializationData deserializedData = new DeserializedCartPole3DInitializationData();
			deserializedData.GoalGeneratorSeed = Random.Range(1, 1000);
			deserializedData.InitialImpulseSeed = Random.Range(1, 1000);

			return new CartPole3DInitializationData(i, deserializedData);
		}
	}

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
