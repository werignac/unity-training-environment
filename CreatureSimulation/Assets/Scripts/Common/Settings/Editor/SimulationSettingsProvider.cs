using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace werignac.GeneticAlgorithm
{
	// Register a SettingsProvider using IMGUI for the drawing framework:
	static class MyCustomSettingsIMGUIRegister
	{
		internal static SerializedObject GetSerializedSettings()
		{
			return new SerializedObject(SimulationSettings.GetOrCreateSettings());
		}

		[SettingsProvider]
		public static SettingsProvider CreateMyCustomSettingsProvider()
		{
			// First parameter is the path in the Settings window.
			// Second parameter is the scope of this setting: it only appears in the Project Settings window.
			var provider = new SettingsProvider("Project/Simulation Settings", SettingsScope.Project)
			{
				// By default the last token of the path is used as display name if no label is provided.
				label = "Simulation Settings",
				// Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
				guiHandler = (searchContext) =>
				{
					var settings = GetSerializedSettings();
					EditorGUILayout.PropertyField(settings.FindProperty("experimentNamesToScenes"), new GUIContent("Experiment Names to Scenes"));
					settings.ApplyModifiedPropertiesWithoutUndo();
				},

				// Populate the search keywords to enable smart search filtering and label highlighting:
				keywords = new HashSet<string>(new[] { "Simulation", "Experiment", "Name", "Scene" })
			};

			return provider;
		}
	}
}

