using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Unity.Barracuda;

namespace werignac.CartPole.Agent.Editor
{
	/// <summary>
	/// Inspector for CartPoleAgentAssets.
	/// Created using the following tutorial.
	/// https://docs.unity3d.com/2023.2/Documentation/Manual/UIE-HowTo-CreateCustomInspector.html
	/// https://docs.unity3d.com/ScriptReference/PropertyDrawer.html
	/// </summary>
	[CustomPropertyDrawer(typeof(CP_AgentEntry))]
    public class CP_AgentEntryPropertyDrawer : PropertyDrawer
    {	
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var container = new VisualElement();

			var popup = new UnityEngine.UIElements.PopupWindow();
			popup.text = "Agent Details";

			VisualTreeAsset inspectorXML = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Editor/UI/CartPole/CP_AgentEntryInspector.uxml");
			popup.Add(inspectorXML.CloneTree());

			container.Add(popup);

			return container;
		}
	}
}
