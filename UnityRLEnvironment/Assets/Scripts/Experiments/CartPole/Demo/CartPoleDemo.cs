/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Subsystem;
using werignac.RLEnvironment.Subsystems;
using werignac.Utils;
using UnityEngine.Events;

namespace werignac.CartPole.Demo
{
    public class CartPoleDemo : MonoBehaviour
    {
		[SerializeField]
		private GameObject demoPrefab;

		private GameObject activeSessionGO = null;

		[SerializeField]
		private bool _useInputForWind = false;
		[SerializeField]
		private bool _enableAgent = true;

		public bool UseInputForWind
		{
			get
			{
				return _useInputForWind;
			}
			set
			{
				_useInputForWind = value;
				onUseInputForWindChanged.Invoke(_useInputForWind);
			}
		}

		public bool EnableAgent
		{
			get
			{
				return _enableAgent;
			}
			set
			{
				_enableAgent = value;
				onEnableAgentChanged.Invoke(_enableAgent);
			}
		}

		[Header("Events")]
		public UnityEvent<bool> onUseInputForWindChanged = new UnityEvent<bool>();
		public UnityEvent<bool> onEnableAgentChanged = new UnityEvent<bool>();

		void Start()
        {
			CreateNewSession();
        }

		private void CreateNewSession()
		{
			if (activeSessionGO != null)
			{
				Destroy(activeSessionGO);
			}

			activeSessionGO = Instantiate(demoPrefab);
			CartPoleDemoSession session = activeSessionGO.GetComponent<CartPoleDemoSession>();
			session.onSessionTerminate.AddListener(CreateNewSession);

			WerignacUtils.BroadcastToAll("OnResetExperiment", session);
		}
		
	}
}
