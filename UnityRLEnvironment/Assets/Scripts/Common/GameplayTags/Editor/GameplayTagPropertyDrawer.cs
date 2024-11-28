using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace werignac.GameplayTags.Editor
{
	/// <summary>
	/// Draws a set of nested checkmarks and drop-down menues for gameplay tags.
	/// Can draw a restricted set of GameplayTags or all GameplayTags.
	/// 
	/// TODO: Implement GameplayTagContainers / Masks.
	/// </summary>
	[CustomPropertyDrawer(typeof(GameplayTagAttribute))]
    public class GameplayTagPropertyDrawer : PropertyDrawer
    {
		private const float ROW_HEIGHT = 20;
		private const float TOGGLE_WIDTH = 15;

		/// <summary>
		/// Value set when drawing rows. Will immediately change value.
		/// </summary>
		private int rowCount = 6;

		/// <summary>
		/// Set of tag dropdowns that should be open.
		/// </summary>
		private HashSet<GameplayTag> openDropdowns = new HashSet<GameplayTag>();

		
		/// <summary>
		/// Returns how much space is needed to draw the full property.
		/// </summary>
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return rowCount * ROW_HEIGHT;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			GameplayTag commonAncestor = (attribute as GameplayTagAttribute).commonAncestor;
			GameplayTag currentValue = (property.managedReferenceValue as GameplayTag);

			// If there is a common ancestor that is not GameplayTag, we need to ensure that the current value
			// is a child of the common ancestor.
			// TODO: Do this in OnValidate()?
			if (! commonAncestor.IsBaseGameplayTag())
			{
				if (currentValue == null || !currentValue.IsChildOf(commonAncestor))
				{
					currentValue = commonAncestor;
					property.managedReferenceValue = currentValue;
				}
			}
			else
			{
				// Force GameplayTags with the GameplayTagAttribute to never be null.
				if (currentValue == null)
				{
					currentValue = new GameplayTag();
					property.managedReferenceValue = currentValue;
				}
			}

			// If the open dropdowns has not been initialized, loop through
			if (openDropdowns.Count == 0)
			{
				GameplayTag iter = currentValue;

				if (iter.IsBaseGameplayTag())
					iter = null;

				while (iter != null)
				{
					openDropdowns.Add(iter);
					iter = iter.Parent;
				}
			}

			// From: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PropertyDrawer.html

			// Using BeginProperty / EndProperty on the parent property means that
			// prefab override logic works on the entire property.
			EditorGUI.BeginProperty(position, label, property);

			// Draw label
			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			// Don't make child fields be indented
			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			// Show the current value's name.
			EditorGUI.LabelField(new Rect(position.x, position.y, position.width, ROW_HEIGHT), currentValue.GetFullName());
			rowCount = 1;

			// If there is a common ancestor, draw the tags that belong to that ancestor.
			if (! commonAncestor.IsBaseGameplayTag())
			{
				// Draw commonAncestor recursively.
				currentValue = DrawTagRows(position, 0, rowCount, commonAncestor.Children, currentValue, out int addedRows);
				rowCount += addedRows;
			}
			// Otherwise, draw all the tags.
			else
			{
				currentValue = DrawTagRows(position, 0, rowCount, GameplayTagManager.RootTags, currentValue, out int addedRows);
				rowCount += addedRows;
			}

			if (property.managedReferenceValue != currentValue)
			{
				// Reset dropdowns after value change.
				openDropdowns = new HashSet<GameplayTag>();
			}

			// Store the user-selected value.
			property.managedReferenceValue = currentValue;

			// Set indent back to what it was
			EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}

		private GameplayTag DrawTagRows(
			Rect position,
			int indent,
			int rowNumber,
			GameplayTag[] toDraw,
			GameplayTag currentValue,
			out int _rowCount
			)
		{
			_rowCount = 0;
			for (int i = 0; i < toDraw.Length; i++)
			{
				GameplayTag child = toDraw[i];
				bool initialToggleValue = currentValue.IsChildOf(child);
				bool isToggleOn = initialToggleValue;
				string rowName = child.Name;
				bool hasChildren = child.HasChildren();
				DrawRow(position, indent, rowNumber + _rowCount, ref isToggleOn, rowName, hasChildren, out bool pressedDropdown);
				_rowCount += 1;

				// If the toggle was pressed for this row...
				if (isToggleOn != initialToggleValue)
				{
					// If this row is already part of the GameplayTag chain...
					if (initialToggleValue)
					{
						// Get rid of everything after this row.
						GameplayTag parent = child.Parent;
						if (parent == null)
							parent = new GameplayTag();

						currentValue = parent;
					}
					// Otherwise, this row was not already selected.
					else
					{
						// Select this row.
						currentValue = child;
					}
				}

				// Manage dropdowns.
				if (pressedDropdown)
				{
					if (openDropdowns.Contains(child))
					{
						// Doesn't remove recursively, allows you to save the opened child dropdowns.
						// TODO: Change if this behaviour is bad.
						openDropdowns.Remove(child);
					}
					else
					{
						openDropdowns.Add(child);
					}
				}

				if (hasChildren && openDropdowns.Contains(child))
				{
					currentValue = DrawTagRows(position, indent + 1, rowNumber + _rowCount, child.Children, currentValue, out int _additionalRowCount);
					_rowCount += _additionalRowCount;
				}
			}

			return currentValue;
		}

		/// <summary>
		/// Draws a row of the GameplayTag selector.
		/// The row has a toggle button and a dropdown button or label.
		/// The toggle indicates whether the tag is part of the full tag chain of the property.
		/// The dropdown is used to show the children of a tag. If the tag has no children,
		/// a label is used instead.
		/// </summary>
		/// <param name="position">Starting position of the property.</param>
		/// <param name="indent">The depth of this tag's row (is it a chind of a child of a child...).</param>
		/// <param name="rowNumber">The order of this tag's row.</param>
		/// <param name="toggleOn">Whether the toggle should be displayed as on. Gets set to the state of the toggle post-user-input.</param>
		/// <param name="tagName">The name of this tag.</param>
		/// <param name="hasChildren">Whether this tag has children (use a label or dropdown).</param>
		/// <param name="pressedDropdown">Whether the dropdown button was clicked.</param>
		private void DrawRow(
			Rect position,
			int indent,
			int rowNumber,
			ref bool toggleOn,
			string tagName,
			bool hasChildren,
			out bool pressedDropdown
			)
		{
			Rect toggleRect = new Rect(
				position.x + indent * TOGGLE_WIDTH,
				position.y + rowNumber * ROW_HEIGHT,
				TOGGLE_WIDTH,
				ROW_HEIGHT
			);
			Rect tagNameRect = new Rect(
				position.x + (1 + indent) * TOGGLE_WIDTH,
				position.y + rowNumber * ROW_HEIGHT,
				position.width - (1 + indent) * TOGGLE_WIDTH,
				ROW_HEIGHT
			);

			toggleOn = EditorGUI.Toggle(toggleRect, toggleOn);

			if (! hasChildren)
			{
				pressedDropdown = false;
				EditorGUI.LabelField(tagNameRect, tagName);
			}
			else
			{
				pressedDropdown = EditorGUI.DropdownButton(tagNameRect, new GUIContent(tagName), FocusType.Passive);
			}
		}

		/// <summary>
		/// Get the children of a GameplayTag.
		/// </summary>
		/// <param name="gameplayTag"></param>
		/// <returns></returns>
		private static string[] GetChildNamesFromGameplayTag(GameplayTag gameplayTag)
		{
			string[] names = new string[gameplayTag.Children.Length];

			for (int i = 0; i < gameplayTag.Children.Length; i++)
			{
				names[i] = gameplayTag.Children[i].Name;
			}

			return names;
		}

		/// <summary>
		/// Get the base GameplayTag types.
		/// </summary>
		/// <returns></returns>
		private static string[] GetBaseNamesForGameplayTags()
		{
			return new string[0];
		}
	}
}
