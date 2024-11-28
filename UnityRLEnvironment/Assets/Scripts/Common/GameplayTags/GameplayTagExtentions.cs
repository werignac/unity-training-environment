using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.GameplayTags
{
    public static class GameplayTagExtentions
    {
		/// <summary>
		/// Returns whether this tag has at least one child.
		/// </summary>
		public static bool HasChildren(this GameplayTag self)
		{
			return self.Children.Length > 0;
		}

		/// <summary>
		/// Walks backwards through the tags seeing other is a parent of self.
		/// For example, if self is:
		/// A = Actions.Player.Move.Forward
		/// and other is:
		/// B = Actions.Player
		/// 
		/// A.IsChildOf(B) returns true.
		/// 
		/// Returns true if the self == other.
		/// </summary>
		/// <param name="self">The potential child of other.</param>
		/// <param name="other">The potential parent of self.</param>
		/// <returns>Whether self id a child of other.</returns>
        public static bool IsChildOf(this GameplayTag self, GameplayTag other)
		{
			// TODO: Null checks.
			// TODO: In theory, we don't need to traverse up all the way. The moment we see a matching tag should be good
			// because the structure of the tags is a tree.

			// All GameplayTags are children of the base GameplayTag.
			if (other.IsBaseGameplayTag())
				return true;

			// Uses self as an iterator, traversing the GameplayTag heirarchy upstream through parents.
			while (self != null)
			{
				// If other == a prefix of self, then self is a child of other.
				if (self.GetType() == other.GetType())
				{
					return true;
				}

				self = self.Parent;
			}

			// If we didn't return in the loop, self is not a child of other.
			return false;
		}

		/// <summary>
		/// If self is:
		/// A = Actions.Player.Move.Forward
		/// and other is:
		/// B = Actions.Player
		/// 
		/// A.MatchesExact(A) returns true.
		/// A.MatchesExact(B) returns false.
		/// </summary>
		/// <returns>Whether two tags match each other exactly.</returns>
		public static bool MatchesExact(this GameplayTag self, GameplayTag other)
		{
			return self.GetType() == other.GetType();
		}

		/// <summary>
		/// Recursively get the dot-separated name of a tag.
		/// </summary>
		public static string GetFullName(this GameplayTag self)
		{ 
			if (self.Parent == null)
				return self.Name;
			else
				return self.Parent.GetFullName() + "." + self.Name;
		}

		/// <summary>
		/// Returns whether this gameplay tag is just the base class
		/// as opposed to a user-defined gameplay tag class.
		/// </summary>
		public static bool IsBaseGameplayTag(this GameplayTag self)
		{
			return self.GetType() == typeof(GameplayTag);
		}
    }
}
