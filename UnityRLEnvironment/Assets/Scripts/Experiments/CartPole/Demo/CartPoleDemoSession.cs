/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace werignac.CartPole.Demo
{
    public class CartPoleDemoSession : MonoBehaviour
    {
		[SerializeField]
		private RandomConveyorBelt randomWind;

		[SerializeField]
		private CartPoleEvaluator evaluator;

		[SerializeField]
		private ICartPoleIO io;

		public UnityEvent onSessionTerminate;

		private GameObject creature;

        // Start is called before the first frame update
        void Start()
        {
			randomWind.Initialize(Random.Range(0, 1000));
			creature = transform.GetChild(0).gameObject;
			// TODO: Use same float ranges as training sessions.
			List<float> positions = new List<float>() { 0, Random.Range(-5f * Mathf.Deg2Rad, 5f * Mathf.Deg2Rad) };
			creature.GetComponent<ArticulationBody>().SetJointPositions(positions);

			io = GetComponent<ICartPoleIO>();
        }

		private void FixedUpdate()
		{
			BroadcastMessage("OnSimulateStep", Time.fixedDeltaTime);
			float score = evaluator.Evaluate(creature, out bool terminate);

			CartPoleState state = new CartPoleState(creature, score);
			CartPoleCommand command = io.GetCommand(state);

			Vector3 moveDirection;
			
			switch (command)
			{
				case CartPoleCommand.RIGHT:
					moveDirection = Vector3.forward;
					break;
				case CartPoleCommand.LEFT:
					moveDirection = Vector3.back;
					break;
				default:
					moveDirection = Vector3.zero;
					break;
			}
				
			creature.GetComponent<ArticulationBody>().AddForce(moveDirection * 100f); // TODO: Get magnitude from shared source as sim.

			if (terminate)
				onSessionTerminate.Invoke();
		}
	}
}
