/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;
using werignac.Subsystem;
using werignac.RLEnvironment.Subsystems;

namespace werignac.CartPole3D
{
	/// <summary>
	/// NOTE: This class does not trigger the async update loop.
	/// TODO: Change to generically use simulate session controller.
	/// </summary>
    public class DemoUpdateLoop : MonoBehaviour
    {
		private SimulationSession<CartPole3DInitializationData> simSess;

		private void Awake()
		{
			simSess = GetComponent<SimulationSession<CartPole3DInitializationData>>();
			simSess.Initialize(new RandomCartPole3D().GenerateRandomData(0));
			SubsystemManagerComponent.Get().GetSubsystem<SynchronizationContextSubsystem>().SetSynchronizationContextType(SynchronizationContextSubsystem.SynchronizationContextType.C_SHARP);
		}

		private void FixedUpdate()
		{
			simSess.SimulateStep();
			simSess.SimulateStepAsync().Wait();
			simSess.PostAsyncStep();
		}
	}
}
