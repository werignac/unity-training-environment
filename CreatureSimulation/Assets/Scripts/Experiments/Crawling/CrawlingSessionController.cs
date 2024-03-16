using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;

namespace werignac.Crawling
{
	public class CrawlingSessionController : SimulationSessionController<CrawlerInitializationData>
	{

		protected override GameObject InitializeCreature()
		{
			GetComponent<CrawlerComponent>().Initialize(CreatureData);
			return transform.GetChild(0).gameObject;
		}
	}
}
