using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.CartPole.Agent
{
	public class CP_AgentEmpty : CP_IAgent
	{
		public void Dispose()
		{
			return;
		}

		public CartPoleCommand Evaluate(CartPoleState state, out Dictionary<string, object> additionalOutput)
		{
			additionalOutput = new Dictionary<string, object>();
			return CartPoleCommand.NO_OP;
		}
	}
}
