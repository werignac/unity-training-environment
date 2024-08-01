using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;
using werignac.GeneticAlgorithm.Subsystems;
using werignac.GeneticAlgorithm.Dispatch;
using werignac.Communication;
using werignac.Subsystem;
using werignac.Utils;
using System.Threading.Tasks;
using System.Text.Json;
using System;

namespace werignac.CartPole3D
{
	public struct CartPole3DCommandDeserialized
	{
		/// <summary>
		/// Value between 0 and 1.
		/// </summary>
		public float DriveX { get; set; }
		/// <summary>
		/// Value between 0 and 1.
		/// </summary>
		public float DriveZ { get; set; }

		public CartPole3DCommand Convert()
		{
			CartPole3DCommand command = new CartPole3DCommand();
			command.Drive = new Vector2((0.5f - DriveX) * 2, (0.5f - DriveZ) * 2);
			return command;
		}
	}

	public class ExternalAgentCarpole3DIO : MonoBehaviour, ICartPole3DIOAsync
	{ 
		private Dispatcher dispatcher;
		private JsonParser<CartPole3DCommandDeserialized> jsonParser;
		private CartPole3DExperiment experiment;
		private int instance_id;
		private Action<CartPole3DCommand> callback;

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

		public async Task SendFrameDataAsync(CartPole3DFrameData state)
		{
			WriteLine(JsonSerializer.Serialize(state));

			if (dispatcher != null)
			{
				JsonCommand<CartPole3DCommandDeserialized> command = await WerignacUtils.AwaitTimeout(jsonParser.GetCommandAsync(), 1000, $"wait for command in cart pole {instance_id}");
				dispatcher.CommunicatorBuffer.AcceptNext();
				// Get the last move instruction and save it for PostSimulateStepAsync.
				foreach (var _command in command.DeserializedObjects)
				{
					callback(_command.Convert());
				}
			}
		}

		private void OnDestroy()
		{
			experiment.Multiplexer.RemoveParser(instance_id);
		}

		public void AssignCallback(Action<CartPole3DCommand> _callback)
		{
			callback = _callback;
		}
	}
}
