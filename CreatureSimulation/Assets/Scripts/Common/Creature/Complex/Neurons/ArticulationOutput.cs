using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.Creatures
{
	[RequireComponent(typeof(ArticulationBody))]
	public class ArticulationOutput : MonoBehaviour
	{
		private ArticulationBody articulationBody;

		private SignalReceiver<float> velocityDriveSetter;

		class VelocityDriveSetter : SignalReceiver<float>
		{
			private ArticulationBody articulationBody;
			private ArticulationDriveAxis axis;

			private Signal<float> lastSignal;
			private Signal<float> nextSignal;

			public VelocityDriveSetter(ArticulationBody _aBody, ArticulationDriveAxis _axis)
			{
				articulationBody = _aBody;
				axis = _axis;
			}

			public Signal<float> GetSignal()
			{
				return nextSignal;
			}

			public void Progress()
			{
				lastSignal = nextSignal;
				nextSignal = new Signal<float>(lastSignal.Inputs.Length);
			}

			public void Evaluate()
			{
				// TODO: Get three inputs and set drives.
			}
		}

		// Start is called before the first frame update
		void Start()
		{
			articulationBody = GetComponent<ArticulationBody>();

		}
	}
}
