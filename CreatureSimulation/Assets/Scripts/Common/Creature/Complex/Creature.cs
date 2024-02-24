using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.Creatures
{
	public class Creature
	{
		public int Index
		{
			get;
			private set;
		}

		private SignalSender[] inputs;

		private Limb root; // Pseudo-Limb. Only constant inputs.

		public Creature(int _index)
		{
			Index = _index;
		}
	}
}
