using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using Unity.Barracuda;

namespace werignac.CartPole.Agent
{
	[CreateAssetMenu(fileName = "CartPoleAgentOutput", menuName = "CartPole/Agent Output", order = 1)]

	public class CP_AgentOutputType : ScriptableObject
    {
		public enum OutputFields
		{
			MOVE_DECISION,
			STATE_VALUE
		}

		[SerializeField, Tooltip("Mappings of output types to the layers that")]
		private SerializedDictionary<OutputFields, string> m_fields;

		public IEnumerable<OutputFields> Fields
		{
			get { return m_fields.Keys; }
		}

		public IEnumerable<string> OutputLayers
		{
			get { return m_fields.Values; }
		}

		private void OnValidate()
		{
			// There should always be an output field for move decisions.
			if (!m_fields.ContainsKey(OutputFields.MOVE_DECISION))
			{
				Debug.LogWarning($"OutputFields asset {name} is missing a move decision field.");
				m_fields.Add(OutputFields.MOVE_DECISION, null);
			}
		}

		public Tensor WorkerToAction(IWorker worker, out Dictionary<OutputFields, Tensor> additionalOutputs)
		{
			additionalOutputs = new Dictionary<OutputFields, Tensor>();

			Tensor moveOutput = null;

			foreach (OutputFields field in m_fields.Keys)
			{
				string layer = m_fields[field];

				if (layer == null)
				{
					Debug.LogError($"Missing layer name for collecting NN output for field {field}. Skipping.");
					continue;
				}

				Tensor output = worker.PeekOutput(layer);

				switch(field)
				{
					case OutputFields.MOVE_DECISION:
						moveOutput = output;
						break;
					default:
						additionalOutputs.Add(field, output);
						break;
				}
			}

			return moveOutput;
		}

	}
}
