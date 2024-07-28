using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace werignac.CartPole
{
	public interface ICartPoleIO
	{
		public CartPoleCommand GetCommand(CartPoleState state);
	}
	public interface ICartPoleIOAsync
    {
		public Task<CartPoleCommand> GetCommandAsync(CartPoleState state);
    }
}
