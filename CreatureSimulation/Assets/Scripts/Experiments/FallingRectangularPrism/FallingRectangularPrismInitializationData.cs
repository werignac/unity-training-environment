using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using UnityEngine;
using werignac.GeneticAlgorithm;
using System.Text.Json;
using System;
using UnityEngine.Events;

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

	public class FRPCreatureReader : CreatureReader<FallingRectangularPrismData>
	{
		private int creatureCount = 0;

		private const string TERMINATOR = "END";

		/// <summary>
		/// TODO: Asynchronously
		/// </summary>
		/// <param name="sr"></param>
		/// <returns></returns>
		public async override IAsyncEnumerable<FallingRectangularPrismData> ReadCreatures(StreamReader sr)
		{
			IsDoneReading = false;
			// TODO: Check when pipe has closed.

			string line;
			string incompleteJSONStr = "";
			int bracketCount = 0;

			while (!TERMINATOR.Equals(line = await sr.ReadLineAsync()))
			{
				foreach (var tuple in ReadClosedJSONObjects(incompleteJSONStr, line, bracketCount))
				{
					if (tuple.Item2)
					{
						DeserializedFallingRectangularPrismData DeserializedData = JsonSerializer.Deserialize<DeserializedFallingRectangularPrismData>(tuple.Item1);
						FallingRectangularPrismData Data = new FallingRectangularPrismData(creatureCount++, DeserializedData);
						yield return Data;
					}
					else
					{
						incompleteJSONStr = tuple.Item1;
						bracketCount = tuple.Item3;
					}
				}
			}

			IsDoneReading = true;
			onIsDoneReading.Invoke();
		}
	}
}
