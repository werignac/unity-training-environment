/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;

namespace werignac.CartPole3D
{
    public class CartPole3DSessionController : SimulationSession<CartPole3DInitializationData>
    {
		protected override GameObject InitializeSessionObject()
		{
			GetComponent<ExternalAgentCarpole3DIO>()?.Initialize(InitData.Index);
			GetComponent<CartPole3DInitialImpulse>().Initialize(InitData.InitialImpulseSeed);
			GetComponent<CartPole3DGoalGenerator>().Initialize(InitData.GoalGeneratorSeed);
			return gameObject;
		}
	}
}
