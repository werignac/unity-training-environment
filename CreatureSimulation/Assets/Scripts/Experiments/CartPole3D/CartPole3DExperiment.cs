using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;
using werignac.Communication;
using werignac.Subsystem;
using werignac.GeneticAlgorithm.Dispatch;

namespace werignac.CartPole3D
{
    public class CartPole3DExperiment : Experiment<CartPole3DInitializationData, RandomCartPole3D, DeserializedCartPole3DInitializationData>
    {
		public MultiplexedParserToSubParsers<JsonParser<CartPole3DCommandDeserialized>> Multiplexer { get; private set; } = new MultiplexedParserToSubParsers<JsonParser<CartPole3DCommandDeserialized>>();

		protected override CartPole3DInitializationData SerializedToInitData(int index, DeserializedCartPole3DInitializationData serializedInit)
		{
			return new CartPole3DInitializationData(index, serializedInit);
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
