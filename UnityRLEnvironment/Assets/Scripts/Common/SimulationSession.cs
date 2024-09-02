/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using werignac.Utils;
using System.Threading.Tasks;
using System;

namespace werignac.RLEnvironment
{
	public class SimulationSession<T_InitData> : MonoBehaviour
	{
		[SerializeField, Tooltip("Monobehaviour that implements IFitnessEvaluator. Judges the current state of one or more creatures and returns a score.")]
		private MonoBehaviour fitness;
		private IFitnessEvaluator _fitness;

		[SerializeField, Tooltip("How long a simulation session runs for (in seconds).")]
		private float simulationDuration = 10f;
		/// <summary>
		/// How long the current simulation has been running for.
		/// </summary>
		private float simulationProgress = 0f;

		/// <summary>
		/// Whether the simulation has terminated early as determined
		/// by the IFitnessEvaluator.
		/// </summary>
		private bool hasTerminatedEarly = false;

		/// <summary>
		/// The physics layer used for this simulation. Assumes that all
		/// children of this GameObject are on the same layer.
		/// </summary>
		public string SimulationLayer
		{
			get;
			private set;
		}

		[SerializeField]
		protected GameObject sessionObject;

		public T_InitData InitData
		{
			get;
			private set;
		}

		#region Between-Call Data
		/// <summary>
		/// Components that are awaiting completion during async step.
		/// Assigned in CollectAsyncComponents.
		/// </summary>
		private IAsyncSimulateStep[] ChildrenAsyncSimSteps;
		/// <summary>
		/// Tasks that will need to be completed on the async step.
		/// Assigned in CollectAsyncComponents.
		/// </summary>
		private Task[] ChildrenAsyncSimTasks;
		#endregion

		public void Initialize(T_InitData _initData, string layer = null)
		{
			InitData = _initData;

			sessionObject = InitializeSessionObject();

			_fitness = (IFitnessEvaluator)fitness;
			_fitness.Initialize(sessionObject);

			if (layer != null)
			{
				gameObject.ForEach((GameObject child) => { child.layer = LayerMask.NameToLayer(layer); });
			}

			SimulationLayer = LayerMask.LayerToName(gameObject.layer);
		}

		/// <summary>
		/// Override for creatures who need to create gameobjects / components.
		/// CreatureData is set up when this function is called and can be used for initialization.
		/// </summary>
		/// <returns>The root for the creature.</returns>
		protected virtual GameObject InitializeSessionObject()
		{
			sessionObject.BroadcastMessage("Initialize", InitData, SendMessageOptions.DontRequireReceiver);
			return sessionObject;
		}

		/// <summary>
		/// Called by Population Controller.
		/// </summary>
		/// <returns>Whether the simulation should continue running.</returns>
		public void SimulateStep()
		{
			// If we've finished simulating, don't perform more simulation steps.
			if (GetHasFinished())
				return;

			// Tell children that a simulation step has occurred.
			gameObject.BroadcastMessage("OnSimulateStep", Time.fixedDeltaTime, SendMessageOptions.DontRequireReceiver);

			// Collect the async components that will need to be invoked in the async simulation step.
			CollectAsyncComponents();
		}

		/// <summary>
		/// Call before SimulateSetAsync. Gets the components who need to have async calls.
		/// </summary>
		private void CollectAsyncComponents()
		{
			ChildrenAsyncSimSteps = GetComponentsInChildren<IAsyncSimulateStep>();
			ChildrenAsyncSimTasks = new Task[ChildrenAsyncSimSteps.Length];
			for (int i = 0; i < ChildrenAsyncSimSteps.Length; i++)
			{
				ChildrenAsyncSimTasks[i] = ChildrenAsyncSimSteps[i].OnSimulateStepAsync(Time.fixedDeltaTime);
			}
		}

		/// <summary>
		/// Performs all pending async tasks.
		/// </summary>
		/// <returns></returns>
		public async Task SimulateStepAsync()
		{
			if (!GetHasFinished() && ChildrenAsyncSimTasks != null)
			{
				// Wait for all children to perform their async tasks.
				await Task.WhenAll(ChildrenAsyncSimTasks);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void PostAsyncStep()
		{
			// If the simulaiton has finished, don't do anything.
			if (GetHasFinished())
				return;

			// Clear async lists for next step.
			ChildrenAsyncSimSteps = null;
			ChildrenAsyncSimTasks = null;

			// Tell children that a simulation step has occurred.
			gameObject.BroadcastMessage("OnPostSimulateStepAsync", Time.fixedDeltaTime, SendMessageOptions.DontRequireReceiver);

			// Update the amount of time spent on this simulation.
			simulationProgress += Time.fixedDeltaTime;
			// Evaluate the creature's score.
			_fitness.Evaluate(sessionObject, out hasTerminatedEarly);
		}

		public float GetScore()
		{
			return _fitness.GetScore();
		}

		public bool GetHasFinished()
		{
			return simulationProgress >= simulationDuration || hasTerminatedEarly;
		}
	}
}
