/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace werignac.CartPole.Demo
{
    public class WindIndicator : MonoBehaviour
    {
		[SerializeField]
		private Slider arrowLeft;
		[SerializeField]
		private Slider arrowRight;

		[SerializeField]
		private bool reverseDirection = false;

		public void OnResetExperiment(CartPoleDemoSession session)
		{
			session.GetComponent<RandomWind>().onWindUpdate.AddListener(UpdateGFX);
		}

		public void UpdateGFX(float _, float normalizedWind)
		{
			Slider activeArrow = (reverseDirection ^ (normalizedWind < 0)) ? arrowLeft : arrowRight;
			Slider inactiveArrow = (reverseDirection ^ (normalizedWind < 0)) ? arrowRight : arrowLeft;

			inactiveArrow.gameObject.SetActive(false);
			activeArrow.gameObject.SetActive(true);

			inactiveArrow.value = 0;
			activeArrow.value = Mathf.Abs(normalizedWind);
		}
    }
}
