using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace werignac.GeneticAlgorithm
{
    public interface IAsyncSimulateStep 
    {
		/// <summary>
		/// Asynchronous task to perform during a step.
		/// </summary>
		/// <param name="deltaTime"></param>
		/// <returns></returns>
		Task OnSimulateStepAsync(float deltaTime);
	}
}
