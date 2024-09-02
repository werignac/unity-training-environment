/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using werignac.RLEnvironment;

namespace werignac.CartPole3D
{
    public class CartPole3DStateObserver : MonoBehaviour, IAsyncSimulateStep
    {
		private CartPole3D cartPole;
		private IFitnessEvaluator fitnessEvaluator;
		private CartPole3DGoalGenerator goalGenerator;
		private MonoBehaviour cartPoleIO;

		/// <summary>
		/// Between-frame data
		/// </summary>
		private CartPole3DFrameData frameData;

		void Awake()
        {
			cartPole = GetComponent<CartPole3D>();
			fitnessEvaluator = GetComponent<IFitnessEvaluator>();
			goalGenerator = GetComponent<CartPole3DGoalGenerator>();

			cartPoleIO = GetComponent<ICartPole3DInput>() as MonoBehaviour;
			if (cartPoleIO == null)
				cartPoleIO = GetComponent<ICartPole3DInputAsync>() as MonoBehaviour;
        }

		private void OnSimulateStep(float deltaTime)
		{
			CartPole3DState state = cartPole.State;
			CartPole3DGoal goal = goalGenerator.Goal;
			float score = fitnessEvaluator.GetScore();
			frameData = new CartPole3DFrameData(state, goal, score);

			if (cartPoleIO is ICartPole3DInput cartPoleIOSynchronous)
			{	
				cartPoleIOSynchronous.SendFrameData(frameData);
			}
		}

		public async Task OnSimulateStepAsync(float deltaTime)
		{
			bool alreadySentInput = cartPoleIO is ICartPole3DInput;

			if ((! alreadySentInput) && cartPoleIO is ICartPole3DInputAsync cartPoleIOAsync)
			{
				await cartPoleIOAsync.SendFrameDataAsync(frameData);
			}
		}
	}
}
