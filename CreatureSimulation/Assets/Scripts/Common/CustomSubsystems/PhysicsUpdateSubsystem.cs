using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Subsystem;
using UnityEngine.Events;

namespace werignac.GeneticAlgorithm.Subsystems
{
	/// <summary>
	/// A subsystem that controls the rate at which updates are performed for SimulationMode.Script.
	/// </summary>
	[Subsystem(SubsystemLifetime.GAME)]
    public class PhysicsUpdateSubsystem : MonoBehaviour
    {
		#region Fields

		private float _minPhysicsUpdatePeriod = -1f;

		/// <summary>
		/// The minimum real time that must pass between frames before a physics update
		/// is called. If <= 0, then update every frame.
		/// </summary>
		public float MinPhysicsUpdatePeriod
		{
			get
			{
				if (!CheckPhysicsSimulationModeIsScript("get minimum physics update period"))
					return -1f;

				return _minPhysicsUpdatePeriod;
			}

			set
			{
				if (!CheckPhysicsSimulationModeIsScript("set minimum physics update period"))
					return;

				_minPhysicsUpdatePeriod = value;
			}
		} // By default go as fast as possible.


		private float _physicsTimeStep;
		/// <summary>
		/// The amount of time used to calculate a step of the physics simulation. If <= 0,
		/// used realtime.
		/// </summary>
		public float PhysicsTimeStep
		{
			get
			{
				if (!CheckPhysicsSimulationModeIsScript("get physics time step"))
					return -1f;
				return _physicsTimeStep;
			}

			set
			{
				if (!CheckPhysicsSimulationModeIsScript("set physics time step"))
					return;
				_physicsTimeStep = value;
			}
		}

		/// <summary>
		/// The time that has passed since the last physics update.
		/// </summary>
		private float _timeSinceLastPhysicsStep = 0.0f;

		#endregion

		#region Events

		/// <summary>
		/// Event invoked after a script-controlled physics step.
		/// </summary>
		public UnityEvent<float> onPhysicsStep = new UnityEvent<float>();

		#endregion

		#region Time Presets

		/// <summary>
		/// Sets this classes' fields to run the physics update loop as
		/// fast as possible with the step size being Time.fixedDeltaTime.
		/// </summary>
		public void SetBatchFixedDeltaTime()
		{
			MinPhysicsUpdatePeriod = -1f;
			PhysicsTimeStep = Time.fixedDeltaTime;
		}

		/// <summary>
		/// Sets this classes' fields to run the physics update loop
		/// to Time.fixedDeltaTime in realtime with a step size of
		/// Time.fixedDeltaTime.
		/// </summary>
		public void SetFixedDeltaTime()
		{
			MinPhysicsUpdatePeriod = Time.fixedDeltaTime;
			PhysicsTimeStep = Time.fixedDeltaTime;
		}

		/// <summary>
		/// Sets this classes' fields to run the physics update loop
		/// as fast as possible with the step size being the true delta time.
		/// </summary>
		public void SetDeltaTime()
		{
			MinPhysicsUpdatePeriod = -1f;
			PhysicsTimeStep = -1f;
		}

	#endregion

		
		/// <summary>
		/// Returns true if the physics simulation mode is in SimulationMode.Script.
		/// Otherwise, returns false and prints an error message.
		/// 
		/// This is used in functions that require the physics simulation mode be set to script.
		/// </summary>
		/// <param name="context">A part of the error message explaining what was trying to be done.</param>
		/// <returns>Whether the physics simulation mode is in SimulationMode.Script.</returns>
		private bool CheckPhysicsSimulationModeIsScript(string context)
		{
			if (Physics.simulationMode != SimulationMode.Script)
			{
				Debug.LogErrorFormat("Physics simulation mode expected to be {0}, but was {1}. Cannot {2}. Please check player settings.",
					SimulationMode.Script, Physics.simulationMode, context);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Updates the physics simulation in accordance to the above parameters.
		/// </summary>
		private void Update()
		{
			if (Physics.simulationMode != SimulationMode.Script)
				return;

			if (MinPhysicsUpdatePeriod <= 0)
			{
				float step = (PhysicsTimeStep <= 0) ? Time.deltaTime : PhysicsTimeStep;
				Physics.Simulate(step);
				onPhysicsStep.Invoke(step);
			}
			else
			{
				float step = (PhysicsTimeStep <= 0) ? MinPhysicsUpdatePeriod : PhysicsTimeStep;

				// See how much time has passed since the last frame.
				_timeSinceLastPhysicsStep += Time.deltaTime;
				// If more time has passed than the length of a physics simulation step,
				// advance the physics simulation.
				for (int i = 0; i < Mathf.FloorToInt(_timeSinceLastPhysicsStep / MinPhysicsUpdatePeriod); i++)
				{
					Physics.Simulate(step);
					onPhysicsStep.Invoke(step);
				}
				// Reset the timer if we simulated steps.
				_timeSinceLastPhysicsStep = _timeSinceLastPhysicsStep % MinPhysicsUpdatePeriod;
			}
		}
	}
}
