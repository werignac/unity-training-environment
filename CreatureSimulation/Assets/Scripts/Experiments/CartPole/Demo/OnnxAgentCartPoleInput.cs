using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Barracuda;
using System;

namespace werignac.CartPole
{
    public class OnnxAgentCartPoleInput : MonoBehaviour, ICartPoleIO
    {
		[SerializeField]
		private NNModel modelAsset;
		private Model runtimeModel;
		private IWorker worker;

		// Start is called before the first frame update
		void Start()
        {
			string[] additionalOutputs = new string[] { "/2/Add_output_0" };
			runtimeModel = ModelLoader.Load(modelAsset);
			worker = WorkerFactory.CreateWorker( WorkerFactory.Type.CSharp, runtimeModel, additionalOutputs, true);
        }

		public CartPoleCommand GetCommand(CartPoleState state)
		{
			float[] tensorData = new float[] { state.CartPosition, state.CartVelocity, state.PoleAngle, state.PoleAngularVelocity};

			Tensor input = new Tensor(1, 4, tensorData);

			IEnumerator manualSchedule = worker.StartManualSchedule(input);
			while (manualSchedule.MoveNext()) { }
			Tensor output = worker.PeekOutput("/2/Add_output_0");

			System.Random rng = new System.Random();
			CartPoleCommand command = new CartPoleCommand();
			
			// TODO: Figure out why Softmax is applied on the wrong direction.
			Debug.LogFormat("Outputs Read: {0}, {1}", output[0], output[1]);
			command.MoveRight = output[0] > output[1];

			input.Dispose();
			output.Dispose();

			return command;
		}

		private void OnDestroy()
		{
			worker.Dispose();
		}
	}
}
