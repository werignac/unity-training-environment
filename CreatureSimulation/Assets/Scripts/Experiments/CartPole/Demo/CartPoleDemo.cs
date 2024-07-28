using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Subsystem;
using werignac.GeneticAlgorithm.Subsystems;

namespace werignac.CartPole
{
    public class CartPoleDemo : MonoBehaviour
    {
		[SerializeField]
		private GameObject demoPrefab;

		private CartPoleSessionController activeSession = null;

		private RandomCartPole generator = new RandomCartPole();

		void Start()
        {
			SubsystemManagerComponent Subsystems = SubsystemManagerComponent.Get();

			Subsystems.GetSubsystem<PhysicsUpdateSubsystem>().SetFixedDeltaTime();
			Subsystems.GetSubsystem<SynchronizationContextSubsystem>().SetSynchronizationContextType(SynchronizationContextSubsystem.SynchronizationContextType.C_SHARP);

			CreateNewSession();
        }

		private void FixedUpdate()
		{
			activeSession.SimulateStep();
			activeSession.SimulateStepAsync().Wait();
			activeSession.PostAsyncStep();

			if (activeSession.GetHasFinished())
				CreateNewSession();
		}

		private void CreateNewSession()
		{
			if (activeSession != null)
			{
				Destroy(activeSession.gameObject);
			}

			GameObject _activeObject = Instantiate(demoPrefab);
			activeSession = demoPrefab.GetComponent<CartPoleSessionController>();
			activeSession.Initialize(generator.GenerateRandomData(0));
		}
		
	}
}
