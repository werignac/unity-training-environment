/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Subsystem;
using System.Threading;

namespace werignac.RLEnvironment.Subsystems
{
	[Subsystem(SubsystemLifetime.GAME)]
    public class SynchronizationContextSubsystem : MonoBehaviour
    {
		public enum SynchronizationContextType { UNITY, C_SHARP };

		/// <summary>
		/// Mapping of synchronization context types to instances of synchronization contexts.
		/// </summary>
		private Dictionary<SynchronizationContextType, SynchronizationContext> _contexts = new Dictionary<SynchronizationContextType, SynchronizationContext>();

		/// <summary>
		/// The context type of the current synchronization context.
		/// Unity overrides the context by default, so it's Unity to start.
		/// </summary>
		private SynchronizationContextType _currentSynchronizationContextType = SynchronizationContextType.UNITY;

		/// <summary>
		/// True whilst the first update has yet to occurr.
		/// Used to for sure override Unity's context if needed.
		/// </summary>
		private bool firstUpdate = true;

		private void Awake()
		{
			InitializeContexts();
		}

		/// <summary>
		/// Gathers all the contexts and maps them to their type via the SynchronizationContextType enum.
		/// </summary>
		private void InitializeContexts()
		{
			// TODO: Assert this is truely the Unity context.
			_contexts.Add(SynchronizationContextType.UNITY, SynchronizationContext.Current);
			_contexts.Add(SynchronizationContextType.C_SHARP, new SynchronizationContext());
		}

		/// <summary>
		/// Public-facing way to change the synchronization context.
		/// </summary>
		public void SetSynchronizationContextType(SynchronizationContextType newType)
		{
			// TODO: Check that first frame has passed so that the synchronization type doesn't get re-overriden by Unity.

			if (_currentSynchronizationContextType == newType)
				return;
			
			_currentSynchronizationContextType = newType;
			SynchronizationContext.SetSynchronizationContext(_contexts[_currentSynchronizationContextType]);
		}
		
		// TODO: Find a better way to override the synchronization context after the first Update is called.
		// Maybe use coroutines?
		private void Update()
		{
			if (firstUpdate)
			{
				SynchronizationContext.SetSynchronizationContext(_contexts[_currentSynchronizationContextType]);
				firstUpdate = false;
			}
		}
	}
}
