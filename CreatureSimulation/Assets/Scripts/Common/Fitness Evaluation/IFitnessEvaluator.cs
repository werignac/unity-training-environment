using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.GeneticAlgorithm
{
	public interface IFitnessEvaluator
	{
		void Initialize(GameObject creature);
		float Evaluate(GameObject creature);
		float GetScore();
	}
}
