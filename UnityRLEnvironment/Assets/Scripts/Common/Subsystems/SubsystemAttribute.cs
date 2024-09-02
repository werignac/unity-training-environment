/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace werignac.Subsystem
{
	public enum SubsystemLifetime { GAME, SCENE }

	/// <summary>
	/// The attribute that labels a class as a subsystem.
	/// Note: for the subsystem to be picked up by the SubsystemManager, the subsystem must inherit from MonoBehaviour.
	/// </summary>
	[System.AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
	public sealed class SubsystemAttribute : Attribute
	{
		// See the attribute guidelines at 
		//  http://go.microsoft.com/fwlink/?LinkId=85236
		readonly SubsystemLifetime _lifetime;

		public SubsystemAttribute(SubsystemLifetime lifetime)
		{
			_lifetime = lifetime;
		}

		public SubsystemLifetime Lifetime
		{
			get { return _lifetime; }
		}
	}
}
