using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using werignac.Utils;

namespace werignac.Crawling
{
	[CustomEditor(typeof(CrawlingBodyPartComponent))]
	[CanEditMultipleObjects]
	public class CrawlingBodyPartComponentEditor : Editor
	{
		public void OnSceneGUI()
		{
			var t = (target as CrawlingBodyPartComponent);

			Handles.color = Color.white;
			Handles.Label(t.GetRelativePointInWorld(Vector3.one * 0.5f), t.gameObject.name + "\n" + t.InitData.ToString());
		}
	}
}
