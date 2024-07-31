using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Subsystem;
using werignac.GeneticAlgorithm.Subsystems;

namespace werignac.CartPole
{
    public class CartPoleDemo : MonoBehaviour
    {
		[SerializeField]
		private GameObject demoPrefab;

		private GameObject activeSessionGO = null;

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
		}
		
	}
}
