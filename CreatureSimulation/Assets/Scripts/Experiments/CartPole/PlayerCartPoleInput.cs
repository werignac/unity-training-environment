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
			command.MoveRight = Input.GetKey(KeyCode.Space);
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
