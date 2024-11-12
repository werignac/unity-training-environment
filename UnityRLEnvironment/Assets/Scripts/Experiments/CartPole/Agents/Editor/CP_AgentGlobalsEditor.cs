using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;


namespace werignac.CartPole.Agent.Editor
{
	// Register a SettingsProvider using IMGUI for the drawing framework:
	static class CP_AgentGlobalsRegister
	{
		internal static SerializedObject GetSerializedSettings()
		{
			return new SerializedObject(CP_AgentGlobals.GetOrCreateGlobals());
		}

		[SettingsProvider]
		public static SettingsProvider CreateMyCustomSettingsProvider()
		{
			// First parameter is the path in the Settings window.
			// Second parameter is the scope of this setting: it only appears in the Settings window for the Project scope.
			var provider = new SettingsProvider("Project/CartPole/Agents", SettingsScope.Project)
			{
				label = "Agents",
				// activateHandler is called when the user clicks on the Settings item in the Settings window.
				activateHandler = (searchContext, rootElement) =>
				{
					var settings = GetSerializedSettings();
					var properties = new VisualElement()
					{
						style =
					{
						flexDirection = FlexDirection.Column
					}
					};
					properties.AddToClassList("property-list");
					rootElement.Add(properties);

					properties.Add(new PropertyField(settings.FindProperty("m_agents")));

					rootElement.Bind(settings);
				},

				// Populate the search keywords to enable smart search filtering and label highlighting:
				keywords = new HashSet<string>(new[] { "Cart", "Pole", "CartPole", "Global", "Globals", "Agent", "Agents" })
			};

			return provider;
		}
	}
}
