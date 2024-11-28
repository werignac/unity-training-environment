using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace werignac.GameplayTags.Editor
{
    public class GameplayTagProvider
    {
		internal static SerializedObject GetSerializedSettings()
		{
			return new SerializedObject(GameplayTagSettings.GetOrCreateSettings());
		}

		[SettingsProvider]
		public static SettingsProvider CreateRLSettingsProvider()
		{
			// First parameter is the path in the Settings window.
			// Second parameter is the scope of this setting: it only appears in the Project Settings window.
			var provider = new SettingsProvider("Project/Gameplay Tags", SettingsScope.Project)
			{
				// By default the last token of the path is used as display name if no label is provided.
				label = "Gameplay Tags",
				// Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
				guiHandler = (searchContext) =>
				{
					var settings = GetSerializedSettings();
					EditorGUILayout.PropertyField(settings.FindProperty("m_gameplayTags"), new GUIContent("Gameplay Tag Chains"));
					settings.ApplyModifiedPropertiesWithoutUndo();
					settings.Dispose();

					if (GUILayout.Button("Save"))
					{
						GameplayTagSettings.GetOrCreateSettings().CompileGameplayTags();
					}
				},

				// Populate the search keywords to enable smart search filtering and label highlighting:
				keywords = new HashSet<string>(new[] { "Gameplay", "Tag", "Tags"})
			};

			return provider;
		}
	}
}
