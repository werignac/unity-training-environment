using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.GameplayTags
{
	[System.Serializable]
    public class GameplayTag
    {
		/// <summary>
		/// The GameplayTag class that is the parent of this one in the GameplayTag chain (not via inheritance).
		/// </summary>
		public GameplayTag Parent
		{
			get
			{
				return GetParent();
			}
		}

		/// <summary>
		/// The GameplayTag classes that are the children of this tag in the GameplayTag chain (not via inheritance).
		/// </summary>
		public GameplayTag[] Children
		{
			get
			{
				return GetChildren();
			}
		}

		/// <summary>
		/// The name of the last GameplayTag in the chain. Not the full GameplayTag.
		/// </summary>
		public string Name
		{
			get
			{
				return GetName();
			}
		}

		/// <summary>
		/// The function that returns the parent of this GameplayTag.
		/// Overridden per GameplayTag.
		/// </summary>
		/// <returns></returns>
		protected virtual GameplayTag GetParent()
		{
			return null;
		}

		/// <summary>
		/// The function that returns the children of this GameplayTag.
		/// Overridden per GameplayTag.
		/// </summary>
		/// <returns></returns>
		protected virtual GameplayTag[] GetChildren()
		{
			return new GameplayTag[0];
		}

		/// <summary>
		/// The function that returns the name of this GameplayTag.
		/// Overriden per GameplayTag.
		/// </summary>
		/// <returns></returns>
		protected virtual string GetName()
		{
			return "";
		}

		/// <summary>
		/// Check whether two GameplayTags are the same.
		/// GameplayTags are not mutable, so checking their types is sufficient.
		/// </summary>
		/// <param name="obj">The other GameplayTag to check.</param>
		/// <returns>Whether the types of the GameplayTags match.</returns>
		public override bool Equals(object obj)
		{
			return GetType() == obj.GetType();
		}

		public override int GetHashCode()
		{
			return GetType().GetHashCode();
		}
	}
}
