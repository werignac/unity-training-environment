using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;
using System.Linq;

namespace werignac.CartPole.Agent
{
	[CreateAssetMenu(fileName = "CartPoleAgentAsset", menuName = "CartPole/CartPole Agent", order = 1)]
    public sealed class CP_AgentAsset : ScriptableObject
    {
		[SerializeField]
		private NNModel m_modelAsset;

		[SerializeField]
		private CP_AgentInputType m_inputs;

		[SerializeField]
		private CP_AgentOutputType m_outputs;

		/// <summary>
		/// Constructs an agent to be used at runtime.
		/// Called by the CartPole demo prefab.
		/// </summary>
		/// <returns>The runtime agent.</returns>
		public CP_IAgent LoadAgent()
		{
			// If no agent model was provided, just return a blank agent that makes no decisions.
			if (m_modelAsset == null)
				return new CP_AgentEmpty();

			// Otherwise, return an actual agent that makes decisions.
			Model runtimeModel = ModelLoader.Load(m_modelAsset);

			string[] additionalOutputs;
			IWorker worker;

			additionalOutputs = m_outputs.OutputLayers.ToArray();
			worker = WorkerFactory.CreateWorker(WorkerFactory.Type.CSharp, runtimeModel, additionalOutputs, false);
			
			return new CP_Agent(worker, m_inputs, m_outputs);
		}
	}
}
