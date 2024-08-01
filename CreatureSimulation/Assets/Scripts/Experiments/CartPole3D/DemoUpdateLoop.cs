using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;
using werignac.Subsystem;
using werignac.GeneticAlgorithm.Subsystems;

namespace werignac.CartPole3D
{
	/// <summary>
	/// NOTE: This class does not trigger the async update loop.
	/// TODO: Change to generically use simulate session controller.
	/// </summary>
    public class DemoUpdateLoop : MonoBehaviour
    {
		private SimulationSessionController<CartPole3DInitializationData> simSess;

		private void Awake()
		{
			simSess = GetComponent<SimulationSessionController<CartPole3DInitializationData>>();
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
