/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace werignac.CartPole.Demo
{
    public class ResetCounter : MonoBehaviour
    {
		[SerializeField]
		private TextMeshProUGUI text;

		[SerializeField, Tooltip("The string to display. \"{0}\" is replaced with the score.")]
		private string _container = "Resets: {0}";

		private int ResetCount {
			get
			{
				return _resetCount;
			}
			set
			{
				_resetCount = value;
				text.text = string.Format(_container, _resetCount);
			}
		}

		private int _resetCount = -1;

        public void OnResetExperiment(CartPoleDemoSession session)
		{
			ResetCount++;
		}

		public void ClearCount()
		{
			ResetCount = 0;
		}
    }
}
