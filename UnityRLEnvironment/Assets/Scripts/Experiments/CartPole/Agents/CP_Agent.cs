using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;

namespace werignac.CartPole.Agent
{
    public class CP_Agent : CP_IAgent
    {
		/// <summary>
		/// The worker that runs the NN.
		/// </summary>
		private IWorker m_worker;

		/// <summary>
		/// An object that converts the CartPole state into input for the worker.
		/// </summary>
		private CP_AgentInputType m_input;

		/// <summary>
		/// An object that maps tensor outputs to types of outputs.
		/// Outputs still need to be processed after.
		/// </summary>
		private CP_AgentOutputType m_output;

		/// <summary>
		/// Used for Dispose() handling.
		/// </summary>
		private bool m_disposedValue = false;

		public CP_Agent(IWorker worker, CP_AgentInputType input, CP_AgentOutputType output)
		{
			m_worker = worker;
			m_input = input;
			m_output = output;
		}

		/// <summary>
		/// Converts additional outputs of tensors into forms that are easier to use externally
		/// (floats, arrays, strings, etc.).
		/// </summary>
		/// <param name="toConvert">The additional outputs of tensors to convert.</param>
		/// <returns>The converted additional outputs.</returns>
		public Dictionary<string, object> TensorAdditionalOutputsToExternalAdditionalOutputs(Dictionary<CP_AgentOutputType.OutputFields, Tensor> toConvert)
		{
			Dictionary<string, object> converted = new Dictionary<string, object>();

			foreach (var pair in toConvert)
			{
				CP_AgentOutputType.OutputFields key = pair.Key;
				Tensor tensor = pair.Value;
				object convertedTensor = null;

				// Convert the tensor into a form that can be used later.
				// E.g. convert confidence into a float.
				switch(key)
				{
					case CP_AgentOutputType.OutputFields.STATE_VALUE:
						convertedTensor = tensor[0];
						break;
					default:
						Debug.LogError($"Missing conversion function for additional output type {key}.");
						break;
				}

				// Record the converted output.
				converted.Add(key.ToString(), convertedTensor);

				// Get rid of the tensor after conversion for memory.
				tensor.Dispose();
			}

			return converted;
		}

		public CartPoleCommand Evaluate(CartPoleState state, out Dictionary<string, object> additionalOutput)
		{
			// Create recepticle for additional output.
			additionalOutput = new Dictionary<string, object>();

			Tensor input = m_input.StateToTensor(state);

			// Have the worker create an output.
			IEnumerator manualSchedule = m_worker.StartManualSchedule(input);
			while (manualSchedule.MoveNext()) { }

			// Convert output into a command.
			Tensor output = m_output.WorkerToAction(m_worker, out var preconvertAdditionalOutput);
			CartPoleCommand command;
			command = output[0] > output[1] ? CartPoleCommand.RIGHT : CartPoleCommand.LEFT;

			// Convert additional outputs to external-friendly form.
			additionalOutput = TensorAdditionalOutputsToExternalAdditionalOutputs(preconvertAdditionalOutput);

			// Dispose of input and outputs for memory.
			input.Dispose();
			output.Dispose();

			return command;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!m_disposedValue)
			{
				if (disposing)
				{
					m_worker.Dispose();
				}
			}
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			System.GC.SuppressFinalize(this);
		}
	}
}
