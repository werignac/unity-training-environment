using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.Creatures
{
	public class Signal<T>
	{
		public enum SignalState { CANCELLED=0, CONSUMABLE=1 }

		public T[] Inputs
		{
			get;
			private set;
		}

		public SignalState State
		{
			get;
			private set;
		}

		public Signal(int inputCount)
		{
			Inputs = new T[inputCount];
			State = SignalState.CONSUMABLE;
		}

		public void SetInput(T input, int index)
		{
			if (index >= Inputs.Length)
				throw new System.Exception($"Signal only has {Inputs.Length} inputs, but a connection was set for index {index}.");

			Inputs[index] = input;
		}

		public void Cancel()
		{
			State = SignalState.CANCELLED; // If a Neuron's input signal is cancelled, it's output is zero
		}
	}
}
