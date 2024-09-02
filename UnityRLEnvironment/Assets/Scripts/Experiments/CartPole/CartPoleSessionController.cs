/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;

namespace werignac.CartPole
{
    public class CartPoleSessionController : SimulationSession<CartPoleInitializationData>
	{
		protected override GameObject InitializeSessionObject()
		{
			GetComponent<CartPoleController>().Initialize(InitData.InitialAngle);
			GetComponent<RandomWind>().Initialize(InitData.WindSeed);
			GetComponent<ExternalAgentCartPoleInput>()?.Initialize(InitData.Index);
			return transform.GetChild(0).gameObject;
		}
	}
}
