using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Pipes;
using System.IO;
using UnityEngine.Events;



namespace werignac.GeneticAlgorithm
{
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

	public interface ICreatureReaderInterface<T> where T : SimulationInitializationData
	{
		IAsyncEnumerable<T> ReadCreatures(StreamReader sr);

		UnityEvent GetOnIsDoneReading();

		bool GetIsDoneReading();
	}
}
