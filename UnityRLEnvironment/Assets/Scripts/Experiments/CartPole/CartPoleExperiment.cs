/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;
using werignac.RLEnvironment.Dispatch;
using werignac.Communication;
using werignac.Subsystem;

namespace werignac.CartPole
{
	public class CartPoleExperiment : Experiment<CartPoleInitializationData, RandomCartPole, DeserializedCartPoleInitializationData>
	{
		public MultiplexedParserToSubParsers<JsonParser<CartPoleCommand>> Multiplexer { get; private set; } = new MultiplexedParserToSubParsers<JsonParser<CartPoleCommand>>();

		protected override CartPoleInitializationData SerializedToInitData(int index, DeserializedCartPoleInitializationData serializedInit)
		{
			return new CartPoleInitializationData(index, serializedInit);
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
