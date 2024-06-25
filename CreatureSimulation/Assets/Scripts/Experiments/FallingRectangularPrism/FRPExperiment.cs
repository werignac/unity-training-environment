using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.GeneticAlgorithm;

namespace werignac.FallingRectangularPrism
{
	public class FRPExperiment : Experiment<FallingRectangularPrismData, RandomFallingRectangularPrismDataFactory, DeserializedFallingRectangularPrismData>
	{
		protected override FallingRectangularPrismData SerializedToInitData(int index, DeserializedFallingRectangularPrismData serializedInit)
		{
			return new FallingRectangularPrismData(index, serializedInit);
		}
	}
}
