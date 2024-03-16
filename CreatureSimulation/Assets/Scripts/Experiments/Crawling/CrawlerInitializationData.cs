using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using werignac.GeneticAlgorithm;
using System.Text.Json;

namespace werignac.Crawling
{

	public struct DeserializedCrawlerData
	{
		public struct BodyPart
		{
			const float MIN_SIZE_LENGTH = 0.01f;

			public DeserializedVector Size { get; set; }
			public DeserializedVector Rotation { get; set; }
			public DeserializedVector ConnectionPoint { get; set; }

			public static BodyPart Random()
			{
				BodyPart toReturn = new BodyPart();
				toReturn.Size = DeserializedVector.Random01();

				DeserializedVector ToCap = toReturn.Size;
				ToCap.x = Mathf.Max(ToCap.x, MIN_SIZE_LENGTH);
				ToCap.y = Mathf.Max(ToCap.y, MIN_SIZE_LENGTH);
				ToCap.z = Mathf.Max(ToCap.z, MIN_SIZE_LENGTH);
				toReturn.Size = ToCap;

				toReturn.Rotation = DeserializedVector.RandomAngle();
				toReturn.ConnectionPoint = DeserializedVector.Random01();

				return toReturn;
			}
		}

		public BodyPart First { get; set; }
		public BodyPart Second { get; set; }

		public static DeserializedCrawlerData Random()
		{
			DeserializedCrawlerData toReturn = new DeserializedCrawlerData();
			toReturn.First = BodyPart.Random();
			toReturn.Second = BodyPart.Random();
			return toReturn;
		}
	}

	public class CrawlerInitializationData : SimulationInitializationData
	{
		public struct BodyPart
		{
			public BodyPart(DeserializedCrawlerData.BodyPart other)
			{
				Size = other.Size.ToVector();
				Rotation = other.Rotation.ToVector();
				ConnectionPoint = other.ConnectionPoint.ToVector();
			}

			public Vector3 Size { get; set; }
			public Vector3 Rotation { get; set; }
			public Vector3 ConnectionPoint { get; set; }

			public override string ToString()
			{
				return $"Size: {Size}\nRotation: {Rotation}\n ConnectionPoint: {ConnectionPoint}";
			}
		}

		public BodyPart First { get; set; }
		public BodyPart Second { get; set; }

		public CrawlerInitializationData(int i) : base(i) { }

		public CrawlerInitializationData(int i, DeserializedCrawlerData deserializedData) : base(i)
		{
			First = new BodyPart(deserializedData.First);
			Second = new BodyPart(deserializedData.Second);
		}
	}

	public class RandomCrawlerFactory : RandomSimulationInitializationDataFactory<CrawlerInitializationData>
	{
		public CrawlerInitializationData GenerateRandomData(int i)
		{
			return new CrawlerInitializationData(i, DeserializedCrawlerData.Random());
		}
	}

	public class CrawlerReader : CreatureReader<CrawlerInitializationData>
	{
		private const string TERMINATOR = "END";
		private int creatureCount = 0;

		public async override IAsyncEnumerable<CrawlerInitializationData> ReadCreatures(StreamReader sr)
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
						DeserializedCrawlerData DeserializedData = JsonSerializer.Deserialize<DeserializedCrawlerData>(tuple.Item1);
						CrawlerInitializationData Data = new CrawlerInitializationData(creatureCount++, DeserializedData);
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
