/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;

namespace werignac.FallingRectangularPrism
{
	public class FRPSessionController : SimulationSession<FallingRectangularPrismData>
	{
		protected override GameObject InitializeSessionObject()
		{
			FallingRectuangularPrismComponent FRPComponent = sessionObject.GetComponentInChildren<FallingRectuangularPrismComponent>();
			FRPComponent.Initialize(InitData.Scale, InitData.EulerRotation);
			return sessionObject;
		}
	}
}
