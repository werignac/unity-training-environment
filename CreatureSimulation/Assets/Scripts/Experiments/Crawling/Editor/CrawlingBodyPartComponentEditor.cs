using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using werignac.Utils;
using System.Text.Json;

namespace werignac.Crawling.Editors
{
	[CustomEditor(typeof(CrawlingBodyPartComponent))]
	[CanEditMultipleObjects]
	public class CrawlingBodyPartComponentEditor : Editor
	{
		public void OnSceneGUI()
		{
			var t = (target as CrawlingBodyPartComponent);

			Handles.color = Color.white;
			Handles.Label(t.GetRelativePointInWorld(Vector3.one * 0.5f), GetCrawlingBodyPartLabel(t));
		}

		private static string GetCrawlingBodyPartLabel(CrawlingBodyPartComponent t)
		{
			string label = "";

			label += t.gameObject.name + "\n" + t.InitData.ToString();
			label += "\nSimulation Frame:\n\t" + t.GetDeserializedSimulationFrame(e_Children: false).ToString().Replace("\n", "\n\t");

			return label;
		}
	}
}
