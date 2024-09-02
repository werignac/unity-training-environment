/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using werignac.Utils;

namespace werignac.CartPole.Demo
{
    public class PlayerWind : RandomWind
    {
		private float currentWind = 0;
		
		[SerializeField]
		private float approachRate = 0.2f;

		[SerializeField]
		private bool usePlayerInput = true;

		private PlayerInput input;
		private InputAction windAction;
		private InputAction windJumpLeft;
		private InputAction windJumpRight;

		private void Start()
		{
			if (WerignacUtils.TryGetComponentInActiveScene(out PlayerInput _input))
			{
				SetPlayerInput(_input);
			}

			if (WerignacUtils.TryGetComponentInActiveScene(out CartPoleDemo demo))
			{
				SetUsePlayerInput(demo.UseInputForWind);
				demo.onUseInputForWindChanged.AddListener(SetUsePlayerInput);
			}
		}

		public void SetPlayerInput(PlayerInput _input)
		{
			input = _input;
			if (input == null)
			{
				windAction = null;
				windJumpLeft = null;
				windJumpRight = null;
			}
			else
			{
				windAction = input.actions.FindAction("WindForce");
				windJumpLeft = input.actions.FindAction("WindJumpLeft");
				windJumpLeft.performed += (context) => OnWindJump(true);
				windJumpRight = input.actions.FindAction("WindJumpRight");
				windJumpRight.performed += (context) => OnWindJump(false);
			}
		}

		public void SetUsePlayerInput(bool _usePlayerInput)
		{
			usePlayerInput = _usePlayerInput;
		}

		private void OnWindJump(bool isLeft)
		{
			if (isLeft)
				currentWind += -0.5f;
			else
				currentWind += 0.5f;
		}

		public override void OnSimulateStep(float deltaTime)
		{
			if (usePlayerInput)
			{
				if (windAction != null)
				{
					float windGoal = windAction.ReadValue<float>();
					float deltaWind = windGoal - currentWind;

					currentWind += deltaWind * approachRate;
					currentWind = Mathf.Clamp(currentWind, -1, 1);
				}

				SetWind(currentWind);
			}
			else
			{
				base.OnSimulateStep(deltaTime);
			}
		}
	}
}
