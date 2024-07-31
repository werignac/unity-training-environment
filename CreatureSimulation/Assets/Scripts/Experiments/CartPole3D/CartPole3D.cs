using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.CartPole3D
{
	/// <summary>
	/// A class that acts as a hub for accessing the Monobehaviours
	/// that are relevant to the 3D Cart Pole.
	/// </summary>
    public class CartPole3D : MonoBehaviour
    {
		[SerializeField]
		private GameObject cart;

		[SerializeField]
		private GameObject pole;

		public Rigidbody CartRigidbody
		{
			get
			{
				return cart.GetComponent<Rigidbody>();
			}
		}

		public Rigidbody PoleRigidbody
		{
			get
			{
				return pole.GetComponent<Rigidbody>();
			}
		}

		public ConfigurableJoint PoleJoint
		{
			get
			{
				return pole.GetComponent<ConfigurableJoint>();
			}
		}

		public Vector2 PoleAngularPosition
		{
			get
			{
				return new Vector2(CartRigidbody.rotation.eulerAngles.x, CartRigidbody.rotation.eulerAngles.z) * Mathf.Deg2Rad;
			}
		}

		public CartPole3DState State
		{
			get
			{
				return new CartPole3DState(this);
			}
		}
    }
}
