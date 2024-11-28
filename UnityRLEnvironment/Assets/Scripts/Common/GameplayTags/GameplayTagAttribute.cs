using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.GameplayTags
{
	/// <summary>
	/// Attribute that allows GameplayTags to be drawn in-editor.
	/// Forces GameplayTags to be non-null.
	/// </summary>
    public class GameplayTagAttribute : PropertyAttribute
    {
		public GameplayTag commonAncestor;

		/// <summary>
		/// Creates a GameplayTagAttribute.
		/// </summary>
		/// <param name="commonAncestor">A common ancestor that all GameplayTags of this field must share. If null or GameplayTag, any tag is accepted.</param>
		public GameplayTagAttribute(System.Type commonAncestor = null)
		{
			if (commonAncestor == null)
				this.commonAncestor = new GameplayTag();
			else
			{
				string assertionMessage = $"Type {commonAncestor} is not a GameplayTag.";
				bool isSubclass = commonAncestor.IsSubclassOf(typeof(GameplayTag));
				Debug.Assert(isSubclass, assertionMessage);
				object instance = System.Activator.CreateInstance(commonAncestor);
				this.commonAncestor = instance as GameplayTag;
			}
		}
    }
}
