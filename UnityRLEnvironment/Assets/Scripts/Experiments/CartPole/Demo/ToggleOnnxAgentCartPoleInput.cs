/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Utils;

namespace werignac.CartPole.Demo
{
    public class ToggleOnnxAgentCartPoleInput : OnnxAgentCartPoleInput
	{
		bool isOn = true;

		System.Random rng = new System.Random();

		private void Start()
		{
			if (WerignacUtils.TryGetComponentInActiveScene(out CartPoleDemo demo))
			{
				SetIsOn(demo.EnableAgent);
				demo.onEnableAgentChanged.AddListener(SetIsOn);
			}
		}


		public void SetIsOn(bool _isOn)
		{
			isOn = _isOn;
		}

		/// <summary>
		/// If the Onnx agent is on, get the command from it.
		/// Otherwise, return NO_OP.
		/// </summary>
		/// <param name="state">The current state of the cart pole session.</param>
		/// <returns>The action the agent took, or NO_OP if the agent is toggled off.</returns>
		public override CartPoleCommand GetCommand(CartPoleState state)
		{
			return (isOn) ? base.GetCommand(state) : CartPoleCommand.NO_OP;
		}

        
    }
}
