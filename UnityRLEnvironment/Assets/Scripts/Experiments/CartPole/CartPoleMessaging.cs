/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;


namespace werignac.CartPole
{
	public struct DeserializedCartPoleInitializationData
	{
		public int WindSeed { get; set; }
		public float InitialAngle { get; set; }
	}

	public class CartPoleInitializationData : SimulationInitializationData
    {
		public CartPoleInitializationData(int i) : base(i) { }

		public CartPoleInitializationData(int i, DeserializedCartPoleInitializationData data) : base(i)
		{
			WindSeed = data.WindSeed;
			InitialAngle = data.InitialAngle;
		}

        public int WindSeed { get; private set; }

		public float InitialAngle { get; private set; }
    }

	public class RandomCartPole : RandomSimulationInitializationDataFactory<CartPoleInitializationData>
	{
		public CartPoleInitializationData GenerateRandomData(int i)
		{
			DeserializedCartPoleInitializationData initData = new DeserializedCartPoleInitializationData();
			initData.WindSeed = Random.Range(1, 1000);
			initData.InitialAngle = Random.Range(-5f, 5f);

			return new CartPoleInitializationData(i, initData);
		}
	}

	public struct CartPoleCommand
	{
		public bool MoveRight { get; set; }
	}

	public struct CartPoleState
	{
		public CartPoleState(float cartPosition, float cartVelocity, float poleAngle, float poleAngularVelocity, float score)
		{
			CartPosition = cartPosition;
			CartVelocity = cartVelocity;
			PoleAngle = poleAngle;
			PoleAngularVelocity = poleAngularVelocity;
			Score = score;
		}

		public CartPoleState(GameObject creature, float score)
		{
			ArticulationBody cart = creature.GetComponent<ArticulationBody>();
			ArticulationBody pole = creature.transform.GetChild(1).GetComponent<ArticulationBody>();

			CartPosition = cart.transform.position.z;
			CartVelocity = cart.velocity.z;
			PoleAngle = pole.jointPosition[0] * Mathf.Rad2Deg;
			PoleAngularVelocity = pole.jointVelocity[0];
			Score = score;
		}

		public float CartPosition { get; set; }
		public float CartVelocity { get; set; }
		public float PoleAngle { get; set; }
		public float PoleAngularVelocity { get; set; }
		public float Score { get; set; }
	}
	
}
