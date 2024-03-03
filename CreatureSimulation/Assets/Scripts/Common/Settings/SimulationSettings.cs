using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using AYellowpaper.SerializedCollections;

namespace werignac.GeneticAlgorithm
{
	public class SimulationSettings : ScriptableObject
	{
		public const string simulationSettingsResourcesPath = "Settings/SimulatorSettings";
		public const string simulationSettingsPath = "Assets/Resources/Settings/SimulatorSettings.asset";

		[SerializedDictionary("Experiment Name", "Experiment Scene Name")]
		public SerializedDictionary<string, string> experimentNamesToScenes;

		public static SimulationSettings GetOrCreateSettings()
		{
			SimulationSettings settings;
#if UNITY_EDITOR
			settings = AssetDatabase.LoadAssetAtPath<SimulationSettings>(simulationSettingsPath);
#else
			settings = Resources.Load<SimulationSettings>(simulationSettingsResourcesPath);
#endif
			if (settings == null)
			{
				settings = ScriptableObject.CreateInstance<SimulationSettings>();
				settings.experimentNamesToScenes = new SerializedDictionary<string, string>();
#if UNITY_EDITOR
				AssetDatabase.CreateAsset(settings, simulationSettingsPath);
				AssetDatabase.SaveAssets();
#endif
			}
			return settings;
		}
	}
}
