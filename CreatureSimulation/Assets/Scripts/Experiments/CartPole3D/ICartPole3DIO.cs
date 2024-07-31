using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;

namespace werignac.CartPole3D
{
	public interface ICartPole3DOutput
	{
		void AssignCallback(Action<CartPole3DCommand> callback);
	}

    public interface ICartPole3DInput
    {
		void SendFrameData(CartPole3DFrameData frameData);
    }

	public interface ICartPole3DInputAsync
	{
		Task SendFrameDataAsync(CartPole3DFrameData frameData);
	}

	/// <summary>
	/// Shorthand for input-output.
	/// </summary>
	public interface ICartPole3DIO: ICartPole3DInput, ICartPole3DOutput { }

	/// <summary>
	/// Shorthand for async-input and output.
	/// </summary>
	public interface ICartPole3DIOAsync : ICartPole3DInputAsync, ICartPole3DOutput { }
}
