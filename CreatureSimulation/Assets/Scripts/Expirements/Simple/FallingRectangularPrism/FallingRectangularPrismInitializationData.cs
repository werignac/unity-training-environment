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

	public class FRPCreatureReader : ICreatureReaderInterface<FallingRectangularPrismData>
	{
		private int creatureCount = 0;

		private const string TERMINATOR = "END";

		public UnityEvent onIsDoneReading = new UnityEvent();

		public bool IsDoneReading { get; private set; }

		/// <summary>
		/// TODO: Asynchronously
		/// </summary>
		/// <param name="sr"></param>
		/// <returns></returns>
		public async IAsyncEnumerable<FallingRectangularPrismData> ReadCreatures(StreamReader sr)
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

		/// <summary>
		/// Reads a line and returns tuples representing a complete JSON object.
		/// </summary>
		/// <param name="past"></param>
		/// <param name="newline"></param>
		/// <param name="bracketCount"></param>
		/// <returns>String: JSON string, Bool: Whether the JSON string is complete, int: the number of { brackets that need to be closed.</returns>
		private IEnumerable<Tuple<string, bool, int>> ReadClosedJSONObjects(string past, string newline, int bracketCount)
		{
			string JSONStr = past;

			int index = -1;
			int nextAppendStart = 0;

			// TODO: skip text that is not included in a JSON object.

			// Parse the new line, counting brackets. If we detect that we've
			// reached the end of a JSON object, return the json object.
			do
			{
				index = newline.IndexOfAny(new char[] { '{', '}' }, index + 1);
				if (index < 0)
					continue;
				
				char bracket = newline[index];
				if (bracket == '{')
				{
					bracketCount++;
				} else if (bracket == '}')
				{
					bracketCount--;

					if (bracketCount < 0)
						throw new Exception($"JSON string started with closed bracket \n{JSONStr}");
					if (bracketCount == 0)
					{
						JSONStr += newline.Substring(nextAppendStart, index - nextAppendStart + 1);
						JSONStr.Trim();
						yield return new Tuple<string, bool, int>(JSONStr, true, bracketCount);
						JSONStr = "";
						nextAppendStart = index + 1;
						bracketCount = 0;
					}
				}
			} while (index >= 0);

			// Add the remaining contents to the JSONStr. Don't forget to trim.
			JSONStr += newline.Substring(nextAppendStart);
			JSONStr.Trim();
			yield return new Tuple<string, bool, int>(JSONStr, false, bracketCount);
		}

		public UnityEvent GetOnIsDoneReading()
		{
			return onIsDoneReading;
		}

		public bool GetIsDoneReading()
		{
			return IsDoneReading;
		}
	}
}
