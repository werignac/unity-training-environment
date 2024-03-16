using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Pipes;
using System.IO;
using UnityEngine.Events;
using System;



namespace werignac.GeneticAlgorithm
{
	public struct DeserializedVector
	{
		public DeserializedVector(Vector3 fromVector)
		{
			x = fromVector.x;
			y = fromVector.y;
			z = fromVector.z;
		}

		public float x { get; set; }
		public float y { get; set; }
		public float z { get; set; }

		public Vector3 ToVector()
		{
			return new Vector3(x, y, z);
		}

		public static DeserializedVector RandomRanges(float xmin, float xmax, float ymin, float ymax, float zmin, float zmax)
		{
			DeserializedVector toReturn = new DeserializedVector(Vector3.zero);
			toReturn.x = UnityEngine.Random.Range(xmin, xmax);
			toReturn.y = UnityEngine.Random.Range(ymin, ymax);
			toReturn.z = UnityEngine.Random.Range(zmin, zmax);
			return toReturn;
		}

		public static DeserializedVector Random01()
		{
			return RandomRanges(0, 1, 0, 1, 0, 1);
		}

		public static DeserializedVector RandomAngle()
		{
			return RandomRanges(0, 360, 0, 360, 0, 360);
		}
	}

	/// <summary>
	/// Any data that is used to initialize sessions must inherit from this.
	/// </summary>
	public class SimulationInitializationData
	{
		public int Index
		{
			get;
			private set;
		}

		public SimulationInitializationData(int i)
		{
			Index = i;
		}
	}

	public interface RandomSimulationInitializationDataFactory<T> where T : SimulationInitializationData
	{
		T GenerateRandomData(int i);
	}

	public abstract class CreatureReader<T> where T : SimulationInitializationData
	{
		protected bool IsDoneReading = false;
		protected UnityEvent onIsDoneReading = new UnityEvent();

		public abstract IAsyncEnumerable<T> ReadCreatures(StreamReader sr);

		/// <summary>
		/// Reads a line and returns tuples representing a complete JSON object.
		/// </summary>
		/// <param name="past"></param>
		/// <param name="newline"></param>
		/// <param name="bracketCount"></param>
		/// <returns>String: JSON string, Bool: Whether the JSON string is complete, int: the number of { brackets that need to be closed.</returns>
		protected IEnumerable<Tuple<string, bool, int>> ReadClosedJSONObjects(string past, string newline, int bracketCount)
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
				}
				else if (bracket == '}')
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

		public UnityEvent GetOnIsDoneReading() { return onIsDoneReading; }

		public bool GetIsDoneReading()
		{
			return IsDoneReading;
		}
	}
}
