using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace werignac.CartPole.Agent.Editor
{
	[CustomEditor(typeof(CP_AgentAsset))]
    public class CP_AgentAssetEditor : UnityEditor.Editor
    {
		public override void OnInspectorGUI()
		{
			SerializedProperty modelProperty = serializedObject.FindProperty("m_modelAsset");
			Object modelAsset = modelProperty.objectReferenceValue;
			
			// If there is an asset, display fields as usual.
			if (modelAsset != null)
			{
				base.OnInspectorGUI();
			}
			// If there is no asset, ask for one first.
			else
			{
				EditorGUILayout.PropertyField(modelProperty);
				EditorGUILayout.HelpBox("An agent with no model will not perform any actions.", MessageType.Warning);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
