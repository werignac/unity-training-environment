using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;

namespace werignac.CartPole.Agent
{
	[CreateAssetMenu(fileName = "CartPoleAgentInput", menuName = "CartPole/Agent Input", order = 1)]
    public class CP_AgentInputType : ScriptableObject
    {
		public enum InputFields {
			CART_POSITION,
			CART_VELOCITY,
			POLE_ANGLE,
			POLE_ANGULAR_VELOCITY,
			NORMALIZED_CONVEYOR
		}

		[SerializeField]
		private InputFields[] m_fields = new InputFields[0];

		private void OnValidate()
		{
			if (m_fields.Length == 0)
			{
				Debug.LogWarningFormat($"AgentInput asset {name} is missing input fields.");
			}
		}

		public Tensor StateToTensor(CartPoleState state)
		{
			float[] tensorData = new float[m_fields.Length];

			for (int i = 0; i < m_fields.Length; i++)
			{
				InputFields field = m_fields[i];
				
				switch (field)
				{
					case InputFields.CART_POSITION:
						tensorData[i] = state.CartPosition;
						break;
					case InputFields.CART_VELOCITY:
						tensorData[i] = state.CartVelocity;
						break;
					case InputFields.POLE_ANGLE:
						tensorData[i] = state.PoleAngle;
						break;
					case InputFields.POLE_ANGULAR_VELOCITY:
						tensorData[i] = state.PoleAngularVelocity;
						break;
					case InputFields.NORMALIZED_CONVEYOR:
						tensorData[i] = state.NormalizedWind;
						break;
				}
			}

			return new Tensor(1, tensorData.Length, tensorData);
		}
    }
}
