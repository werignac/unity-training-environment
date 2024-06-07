using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using werignac.Utils;

namespace werignac.Crawling.Editors
{
	[CustomEditor(typeof(CrawlerComponent))]
	[CanEditMultipleObjects]
	public class CrawlerComponentEditor : Editor
	{
		public void OnSceneGUI()
		{
			var t = (target as CrawlerComponent);

			Bounds aabb = t.gameObject.GetCompositeAABB();

			Handles.color = Color.green;
			Handles.DrawWireCube(aabb.center, aabb.size);
		}
	}
}
