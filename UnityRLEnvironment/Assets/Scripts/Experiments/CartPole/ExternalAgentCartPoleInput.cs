/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using werignac.RLEnvironment;
using werignac.RLEnvironment.Dispatch;
using werignac.Communication;
using werignac.Subsystem;
using werignac.Utils;
using System.Text.Json;

namespace werignac.CartPole
{
	public class ExternalAgentCartPoleInput : MonoBehaviour, ICartPoleIOAsync
	{
		private Dispatcher dispatcher;
		private JsonParser<CartPoleCommand> jsonParser;
		private CartPoleExperiment experiment;
		private int instance_id;

		public void Initialize(int index)
		{
			instance_id = index;

			dispatcher = SubsystemManagerComponent.Get().GetSubsystem<Dispatcher>();
			if (!WerignacUtils.TryGetComponentInActiveScene(out experiment))
				throw new System.Exception("Could not find experiment for crawler component");
			jsonParser = experiment.Multiplexer.GetParserFromIndex(instance_id);
		}

		public void WriteLine(string line)
		{
			string line_with_multiplex_prefix = $"{instance_id} {line}";
			dispatcher?.Communicator?.Write(line_with_multiplex_prefix);
		}

		public async Task<CartPoleCommand> GetCommandAsync(CartPoleState state)
		{
			WriteLine(JsonSerializer.Serialize(state));

			if (dispatcher != null)
			{
				JsonCommand<CartPoleCommand> command = await WerignacUtils.AwaitTimeout(jsonParser.GetCommandAsync(), 1000, $"wait for command in cart pole {instance_id}");
				dispatcher.CommunicatorBuffer.AcceptNext();
				// Get the last move instruction and save it for PostSimulateStepAsync.
				foreach (var _command in command.DeserializedObjects)
				{
					return _command;
				}
			}

			return new CartPoleCommand();
		}

		private void OnDestroy()
		{
			experiment.Multiplexer.RemoveParser(instance_id);
		}
	}
}
