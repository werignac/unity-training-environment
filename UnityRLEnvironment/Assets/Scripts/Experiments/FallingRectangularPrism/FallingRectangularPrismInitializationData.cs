/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using UnityEngine;
using System.Text.Json;
using System;
using UnityEngine.Events;
using werignac.RLEnvironment;
using werignac.Communication;

namespace werignac.FallingRectangularPrism
{
	public class DeserializedFallingRectangularPrismData
	{
		public float XScale { get; set; }
		public float YScale { get; set; }
		public float ZScale { get; set; }
		public float XRot { get; set; }
		public float YRot { get; set; }
		public float ZRot { get; set; }
	}

	public class FallingRectangularPrismData : SimulationInitializationData
	{
		public Vector3 Scale { get; private set; }
		public Vector3 EulerRotation { get; private set; }
		public FallingRectangularPrismData(int i) : base(i)
		{
			Scale = new Vector3(UnityEngine.Random.Range(0.01f, 1f), UnityEngine.Random.Range(0.01f, 1f), UnityEngine.Random.Range(0.01f, 1f));
			EulerRotation = UnityEngine.Random.rotation.eulerAngles;
		}

		public FallingRectangularPrismData(int i, DeserializedFallingRectangularPrismData DeserializedData) : base(i)
		{
			Scale = new Vector3(DeserializedData.XScale, DeserializedData.YScale, DeserializedData.ZScale);
			EulerRotation = new Vector3(DeserializedData.XRot, DeserializedData.YRot, DeserializedData.ZRot);
		}
	}

	public class RandomFallingRectangularPrismDataFactory : RandomSimulationInitializationDataFactory<FallingRectangularPrismData>
	{
		public FallingRectangularPrismData GenerateRandomData(int i)
		{
			return new FallingRectangularPrismData(i);
		}
	}
}
