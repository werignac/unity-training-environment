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

		public override CartPoleCommand GetCommand(CartPoleState state)
		{
			if (isOn)
				return base.GetCommand(state);
			else
			{
				CartPoleCommand randomCommand = new CartPoleCommand();
				randomCommand.MoveRight = rng.NextDouble() > 0.5;
				return randomCommand;
			}
		}

        
    }
}
