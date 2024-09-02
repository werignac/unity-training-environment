/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.CartPole.Demo
{
	[ExecuteAlways]
	public class RenderAngleRange : MonoBehaviour
    {
		[SerializeField]
		private LineRenderer lineRenderer;
		[SerializeField]
		private CartPoleEvaluator evaluator;
		[SerializeField]
		private int resolution = 10;
		[SerializeField]
		private float radius = 1.0f;

		private void Start()
		{
			Render();
		}

		private void OnGUI()
		{
			Render();
		}

		private void Render()
		{
			lineRenderer.positionCount = resolution;

			for (int i = 0; i < resolution; i++)
			{
				float progress = Mathf.InverseLerp(0, resolution - 1, i);
				float angle = Mathf.Lerp(-evaluator.AngleLimit, evaluator.AngleLimit, progress) * Mathf.Deg2Rad;
				Vector3 position = new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
				lineRenderer.SetPosition(i, position);
			}
		}
	}
}
