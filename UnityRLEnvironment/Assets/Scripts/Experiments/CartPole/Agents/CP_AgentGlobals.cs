using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using AYellowpaper.SerializedCollections;
using System;

namespace werignac.CartPole.Agent
{
    public class CP_AgentGlobals : ScriptableObject
    {
		public const string globalsResourcesPath = "Settings/CartPoleAgentGlobals";
		public const string globalsPath = "Assets/Resources/Settings/CartPoleAgentGlobals.asset";

		[SerializeField]
		private CP_AgentEntry[] m_agents;

		public CP_AgentEntry[] Agents
		{
			get { return m_agents; }
			private set { m_agents = value; }
		}

		public static CP_AgentGlobals GetOrCreateGlobals()
		{
			CP_AgentGlobals globals;
#if UNITY_EDITOR
			globals = AssetDatabase.LoadAssetAtPath<CP_AgentGlobals>(globalsPath);
#else
			globals = Resources.Load<CP_AgentGlobals>(globalsResourcesPath);
#endif
			// Create a default set of globals if none exists.
			if (globals == null)
			{
				globals = ScriptableObject.CreateInstance<CP_AgentGlobals>();
				globals.Agents = new CP_AgentEntry[0];
#if UNITY_EDITOR
				AssetDatabase.CreateAsset(globals, globalsPath);
				AssetDatabase.SaveAssets();
#endif
			}
			return globals;
		}
	}
}
