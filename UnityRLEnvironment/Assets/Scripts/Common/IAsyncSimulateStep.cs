/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace werignac.RLEnvironment
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
