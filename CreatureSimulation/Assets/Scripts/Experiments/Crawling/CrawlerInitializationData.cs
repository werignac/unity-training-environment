using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using werignac.GeneticAlgorithm;
using System.Text.Json;
using werignac.Communication;

namespace werignac.Crawling
{
	public struct DeserializedCrawlerData
	{
		public struct BodyPart
		{
			public DeserializedVector Size { get; set; }
			public DeserializedVector Rotation { get; set; }
			public DeserializedVector ConnectionPoint { get; set; }

			public static BodyPart Random()
			{
				BodyPart toReturn = new BodyPart();
				toReturn.Size = DeserializedVector.Random01();

				DeserializedVector ToCap = toReturn.Size;
				ToCap.x = Mathf.Max(ToCap.x, 0);
				ToCap.y = Mathf.Max(ToCap.y, 0);
				ToCap.z = Mathf.Max(ToCap.z, 0);
				toReturn.Size = ToCap;

				toReturn.Rotation = DeserializedVector.RandomAngle();
				toReturn.ConnectionPoint = DeserializedVector.Random01();

				return toReturn;
			}
		}

		public BodyPart First { get; set; }
		public BodyPart Second { get; set; }
		public string PipeName { get; set; }

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
			const float MIN_SIZE_LENGTH = 0.1f;
			public BodyPart(DeserializedCrawlerData.BodyPart other)
			{
				Size = Vector3.Max(other.Size.ToVector(), Vector3.one * MIN_SIZE_LENGTH);
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

		public BodyPart First { get; private set; }
		public BodyPart Second { get; private set; }

		public string PipeName { get; private set; }

		public CrawlerInitializationData(int i) : base(i) { }

		public CrawlerInitializationData(int i, DeserializedCrawlerData deserializedData) : base(i)
		{
			First = new BodyPart(deserializedData.First);
			Second = new BodyPart(deserializedData.Second);
			PipeName = deserializedData.PipeName;
		}
	}

	public class RandomCrawlerFactory : RandomSimulationInitializationDataFactory<CrawlerInitializationData>
	{
		public CrawlerInitializationData GenerateRandomData(int i)
		{
			return new CrawlerInitializationData(i, DeserializedCrawlerData.Random());
		}
	}
}
