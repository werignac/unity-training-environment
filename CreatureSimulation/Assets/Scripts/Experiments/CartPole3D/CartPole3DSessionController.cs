using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;

namespace werignac.CartPole3D
{
    public class CartPole3DSessionController : SimulationSessionController<CartPole3DInitializationData>
    {
		protected override GameObject InitializeCreature()
		{
			GetComponent<ExternalAgentCarpole3DIO>()?.Initialize(CreatureData.Index);
			GetComponent<CartPole3DInitialImpulse>().Initialize(CreatureData.InitialImpulseSeed);
			GetComponent<CartPole3DGoalGenerator>().Initialize(CreatureData.GoalGeneratorSeed);
			return gameObject;
		}
	}
}
