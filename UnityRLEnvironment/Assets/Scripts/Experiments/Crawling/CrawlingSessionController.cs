/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;

namespace werignac.Crawling
{
	public class CrawlingSessionController : SimulationSession<CrawlerInitializationData>
	{

		protected override GameObject InitializeSessionObject()
		{
			GetComponent<CrawlerComponent>().Initialize(InitData);
			return transform.GetChild(0).gameObject;
		}
	}
}
