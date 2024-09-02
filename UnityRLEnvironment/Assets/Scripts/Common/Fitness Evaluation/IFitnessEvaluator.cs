/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.RLEnvironment
{
	public interface IFitnessEvaluator
	{
		void Initialize(GameObject sessionObject);
		float Evaluate(GameObject sessionObject, out bool terminateEarly);
		float GetScore();
	}
}
