using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;

namespace werignac.CartPole3D
{
	/// <summary>
	/// NOTE: This class does not trigger the async update loop.
	/// TODO: Change to generically use simulate session controller.
	/// </summary>
    public class DemoUpdateLoop : MonoBehaviour
    {
		private void Awake()
		{
			GetComponent<IFitnessEvaluator>().Initialize(gameObject);
			GetComponent<CartPole3DInitialImpulse>().Initialize(Random.Range(1, 1000));
			GetComponent<CartPole3DGoalGenerator>().Initialize(Random.Range(1, 1000));
		}

		private void FixedUpdate()
		{
			BroadcastMessage("OnSimulateStep", Time.fixedDeltaTime);
			BroadcastMessage("OnPostSimulateStepAsync", Time.fixedDeltaTime);
			float score = GetComponent<IFitnessEvaluator>().Evaluate(gameObject, out bool terminateEarly);

			Debug.Log($"Score: {score}");
			Debug.Log($"Terminate Early: {terminateEarly}");
		}
	}
}
