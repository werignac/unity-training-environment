using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;

namespace werignac.FallingRectangularPrism
{
	public class FRPSessionController : SimulationSessionController<FallingRectangularPrismData>
	{
		protected override GameObject InitializeCreature()
		{
			FallingRectuangularPrismComponent FRPComponent = creatureObject.GetComponentInChildren<FallingRectuangularPrismComponent>();
			FRPComponent.Initialize(CreatureData.Scale, CreatureData.EulerRotation);
			return creatureObject;
		}
	}
}
