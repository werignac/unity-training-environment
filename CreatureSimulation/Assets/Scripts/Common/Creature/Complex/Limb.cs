using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace werignac.Creatures
{
	public class Limb
	{
		private SignalSender[] inputs; // joint-angles, velocities, contacts, constants
		private Limb[] children;
		private Neuron[] neurons; // my neurons
		private SignalReceiver<float>[] muscles;

		public Limb()
		{

		}

		public void GenerateRandomNeurons(int count)
		{
			neurons = new Neuron[count];
			for (int i = 0; i < count; i++)
				neurons[i] = NeuronFactory.InstantiateRandom();
		}

		public void GenerateRandomConstants()
		{
			for (int i = inputs.Length - 1; i >= 0 && inputs[i] == null; i--)
			{
				inputs[i] = Constant.InstantiateRandom();
			}
		}

		public void GenerateRandomConnections()
		{
			int inputNodeCount = GetInputCount();

			foreach(Neuron neuron in neurons)
			{
				for (int i = 0; i < neuron.InputCount; i++)
				{
					SignalSender sender = GetInput(Random.Range(0, inputNodeCount));
					sender.SetOutConnection(neuron, i);
				}
			}

			foreach (SignalReceiver<float> muscle in muscles)
			{
				// TODO: get input count from SignlaReceiver 
				//for (int i = 0; i < muscle.InputCount; i++)
				{
					SignalSender sender = GetInput(Random.Range(0, inputNodeCount));
					//sender.SetOutConnection(muscle, i);
				}
			}
		}

		private int GetInputCount()
		{
			int length = inputs.Length + neurons.Length;

			foreach (Limb child in children)
				length += child.GetInputCount();
			
			return length;
		}

		private SignalSender GetInput(int index)
		{
			int error = GetInputRecursive(index, out SignalSender input);

			if (error > 0)
				throw new System.Exception($"Could not find input at index {input}. Only found {index - error} inputs.");

			return input;
		}

		private int GetInputRecursive(int index, out SignalSender input)
		{
			// Base Case
			if (index < inputs.Length + neurons.Length)
			{

				if (index < inputs.Length)
				{
					input = inputs[index];
				}
				else
				{
					input = neurons[index - inputs.Length];
				}
				return 0;
			}

			// Recursive Case
			index -= inputs.Length + neurons.Length;

			foreach(Limb child in children)
			{
				index = child.GetInputRecursive(index, out input);
				if (index == 0)
					return 0;
			}

			input = null;
			return index;
		}

		private SignalReceiver<float> GetOutput(int index)
		{
			int error = GetOutputRecursive(index, out SignalReceiver<float> input);

			if (error > 0)
				throw new System.Exception($"Could not find ooutput at index {input}. Only found {index - error} outputs.");

			return input;
		}

		private int GetOutputRecursive(int index, out SignalReceiver<float> output)
		{
			// Base Case
			if (index < neurons.Length + muscles.Length)
			{
				if (index < neurons.Length)
				{
					output = neurons[index];
				} else
				{
					output = muscles[index - neurons.Length];
				}
				return 0;
			}

			// Recursive Case
			index -= neurons.Length + muscles.Length;

			foreach (Limb child in children)
			{
				index = child.GetOutputRecursive(index, out output);
				if (index == 0)
					return 0;
			}

			output = null;
			return index;
		}
	}
}
