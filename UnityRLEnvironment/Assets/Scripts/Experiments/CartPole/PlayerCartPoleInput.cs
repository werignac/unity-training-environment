/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace werignac.CartPole
{
	public class PlayerCartPoleInput : MonoBehaviour, ICartPoleIOAsync, ICartPoleIO
	{
		CartPoleCommand command;

		private void Update()
		{
			// TODO: Use new input system.
			command = Input.GetKey(KeyCode.Space)? CartPoleCommand.RIGHT : CartPoleCommand.LEFT;
		}

		public async Task<CartPoleCommand> GetCommandAsync(CartPoleState state)
		{
			return await Task.Run(() => { return command; });
		}

		public CartPoleCommand GetCommand(CartPoleState state)
		{
			return command;
		}
	}
}
