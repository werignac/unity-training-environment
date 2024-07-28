using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;

namespace werignac.CartPole
{
    public class CartPoleSessionController : SimulationSessionController<CartPoleInitializationData>
	{
		protected override GameObject InitializeCreature()
		{
			GetComponent<CartPoleController>().Initialize(CreatureData.InitialAngle);
			GetComponent<RandomWind>().Initialize(CreatureData.WindSeed);
			GetComponent<ExternalAgentCartPoleInput>()?.Initialize(CreatureData.Index);
			return transform.GetChild(0).gameObject;
		}
	}
}
