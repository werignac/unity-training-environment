/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Barracuda;
using System;
using werignac.CartPole.Agent;

namespace werignac.CartPole
{
    public class OnnxAgentCartPoleInput : MonoBehaviour, ICartPoleIO
    {
		[SerializeField]
		private CP_AgentAsset m_agentAsset;

		private CP_IAgent m_agent;

		// Start is called before the first frame update
		void Awake()
        {
			m_agent = m_agentAsset.LoadAgent();
        }

		public virtual CartPoleCommand GetCommand(CartPoleState state)
		{
			return m_agent.Evaluate(state, out var additionalOutput);
		}

		private void OnDestroy()
		{
			m_agent.Dispose();
		}
	}
}
