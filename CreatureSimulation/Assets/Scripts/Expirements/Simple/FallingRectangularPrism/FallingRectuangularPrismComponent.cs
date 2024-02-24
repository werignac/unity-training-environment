using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.FallingRectangularPrism
{
	public class FallingRectuangularPrismComponent : MonoBehaviour
	{
		public void Initialize(Vector3 localScale, Vector3 eulerAngles)
		{
			localScale = ReduceSize(localScale);

			SetOnFloor(localScale, Quaternion.Euler(eulerAngles));

			GetComponent<ArticulationBody>().enabled = true;
		}

		private static float MIN_SIDE_LENGTH = 0.01f;

		private static Vector3 ReduceSize(Vector3 toReduce)
		{

			float longestSide = 0f;

			for (int i = 0; i < 3; i++)
			{
				if (toReduce[i] == MIN_SIDE_LENGTH)
					toReduce[i] = MIN_SIDE_LENGTH;
				else if (toReduce[i] < 0)
					toReduce[i] = Mathf.Abs(toReduce[i]);

				if (toReduce[i] > longestSide)
					longestSide = toReduce[i];
			}

			for (int i = 0; i < 3; i++)
			{
				toReduce[i] = Mathf.Max(toReduce[i] / longestSide, MIN_SIDE_LENGTH);
			}

			return toReduce;
		}

		private float GetBottomWorldHeight()
		{
			float heightOfLowestPoint = float.PositiveInfinity;

			foreach (float x in new float[]{ -0.5f, 0.5f})
			{
				foreach (float y in new float[] { -0.5f, 0.5f})
				{
					foreach(float z in new float[] { -0.5f, 0.5f})
					{
						Vector3 point = transform.TransformPoint(x, y, z);
						if (point.y < heightOfLowestPoint)
							heightOfLowestPoint = point.y;
					}
				}
			}

			return heightOfLowestPoint;
		}

		private void SetOnFloor(Vector3 localScale, Quaternion rotation)
		{
			transform.localScale = localScale;
			transform.rotation = rotation;

			transform.Translate(Vector3.down * GetBottomWorldHeight(), Space.World);
		}
	}
}
