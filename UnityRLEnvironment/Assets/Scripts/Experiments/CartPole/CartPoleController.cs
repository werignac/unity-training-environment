/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using werignac.RLEnvironment;
using werignac.Utils;

namespace werignac.CartPole
{
	public class CartPoleController : MonoBehaviour, IAsyncSimulateStep
	{
		[SerializeField]
		private float cartForce = 100f;

		[SerializeField]
		private ArticulationBody cart;

		[SerializeField]
		private ArticulationBody pole;

		private IFitnessEvaluator evaluator;

		[SerializeField]
		private ICartPoleIOAsync io;

		// Between-Frame Data
		private CartPoleState state;

		private CartPoleCommand command;


		public void Initialize(float initialAngle)
		{
			List<float> initialPositions = new List<float>();
			initialPositions.Add(0);
			initialPositions.Add(initialAngle * Mathf.Deg2Rad);
			pole.SetJointPositions(initialPositions);

			evaluator = GetComponent<IFitnessEvaluator>();
			io = GetComponent<ICartPoleIOAsync>();
		}

		public void OnSimulateStep(float deltaTime)
		{
			state = GetCartState();
		}

		public async Task OnSimulateStepAsync(float deltaTime)
		{
			command = await WerignacUtils.AwaitTimeout(io.GetCommandAsync(state), 1500, $"wait for command in cart pole controller");
		}

		public void OnPostSimulateStepAsync(float deltaTime)
		{
			if (command.MoveRight)
			{
				cart.AddForce(Vector3.forward * cartForce);
			} else {
				cart.AddForce(Vector3.back * cartForce);
			}
		}

		private CartPoleState GetCartState()
		{
			return new CartPoleState(cart.gameObject, evaluator.GetScore());
		}
	}
}
