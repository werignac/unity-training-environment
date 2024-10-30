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

		/// <summary>
		/// Initialize the cart pole, including the angle of the pole.
		/// </summary>
		/// <param name="initialAngle">The initial angle of the pole.</param>
		public void Initialize(float initialAngle)
		{
			List<float> initialPositions = new List<float>();
			initialPositions.Add(0);
			initialPositions.Add(initialAngle * Mathf.Deg2Rad);
			pole.SetJointPositions(initialPositions);

			evaluator = GetComponent<IFitnessEvaluator>();
			io = GetComponent<ICartPoleIOAsync>();
		}

		/// <summary>
		/// Get the state of the cart to be sent in OnSimulateStep Async.
		/// </summary>
		/// <param name="deltaTime"></param>
		public void OnSimulateStep(float deltaTime)
		{
			state = GetCartState();
		}

		/// <summary>
		/// Send the state of the cart, then get a command from IO, which may involve multithreading.
		/// </summary>
		/// <param name="deltaTime"></param>
		/// <returns></returns>
		public async Task OnSimulateStepAsync(float deltaTime)
		{
			command = await WerignacUtils.AwaitTimeout(io.GetCommandAsync(state), 1500, $"wait for command in cart pole controller");
		}

		/// <summary>
		/// After receiving a cart pole command, turn the command into movement.
		/// </summary>
		/// <param name="deltaTime"></param>
		public void OnPostSimulateStepAsync(float deltaTime)
		{

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
			
			cart.AddForce(moveDirection * cartForce);
		}

		private CartPoleState GetCartState()
		{
			return new CartPoleState(cart.gameObject, evaluator.GetScore());
		}
	}
}
