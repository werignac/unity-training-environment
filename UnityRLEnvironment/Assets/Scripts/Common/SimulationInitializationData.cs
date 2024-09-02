/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Pipes;
using System.IO;
using UnityEngine.Events;
using System;
using werignac.Communication;

namespace werignac.RLEnvironment
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
}
