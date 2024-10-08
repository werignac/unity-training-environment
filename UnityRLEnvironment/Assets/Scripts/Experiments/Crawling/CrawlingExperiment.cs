/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;
using werignac.Communication;
using werignac.RLEnvironment.Dispatch;
using werignac.Subsystem;

namespace werignac.Crawling
{
	public class CrawlingExperiment : Experiment<CrawlerInitializationData, RandomCrawlerFactory, DeserializedCrawlerData>
	{
		public MultiplexedParserToSubParsers<JsonParser<CrawlerMoveInstruction>> Multiplexer { get; private set; } = new MultiplexedParserToSubParsers<JsonParser<CrawlerMoveInstruction>>();

		protected override CrawlerInitializationData SerializedToInitData(int index, DeserializedCrawlerData serializedInit)
		{
			return new CrawlerInitializationData(index, serializedInit);
		}

		protected override void PostInitialize()
		{
			OnFinishedReadingCreatures.AddListener(AddMultiplexingParser);
		}

		private void AddMultiplexingParser()
		{
			Dispatcher dispatcher = SubsystemManagerComponent.Get().GetSubsystem<Dispatcher>();
			dispatcher.ParserStack.AddParser(Multiplexer.MParser);
		}
	}
}
