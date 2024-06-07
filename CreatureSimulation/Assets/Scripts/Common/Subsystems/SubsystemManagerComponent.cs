using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Utils;
using UnityEngine.SceneManagement;
using System.Reflection;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace werignac.Subsystem
{
    public class SubsystemManagerComponent : MonoBehaviour
    {
		#region Fields
		/// <summary>
		/// The scene that contains the subsystem post-DontDestroyOnLoad;
		/// </summary>
		private static Scene SubsystemScene;

		/// <summary>
		/// Subsystems that exist while a scene is open (re-set when a new scene is loaded).
		/// </summary>
		private List<Component> SceneSubsystems = new List<Component>();
		/// <summary>
		/// Subsystems that exist over the duration of the game's lifetime.
		/// </summary>
		private List<Component> GameSubsystems = new List<Component>();
		#endregion Fields

		#region Methods

		/// <summary>
		/// Returns whether there exists a SubsystemManagerComponent in the scene.
		/// Gets the reference to the SubystemManagerComponent if there is one.
		/// </summary>
		/// <param name="SubsystemManager">The SubsystemManagerComponent reference.</param>
		/// <returns>Whether there exists a SubsystemManagerComponent in the scene.</returns>
		private static bool _Get(out SubsystemManagerComponent SubsystemManager)
		{
			if (!SubsystemScene.IsValid())
			{
				SubsystemManager = null;
				return false;
			}

			return WerignacUtils.TryGetComponentInScene(SubsystemScene, out SubsystemManager);
		}

		/// <summary>
		/// Gets a reference to a singleton SubsystemManagerComponent.
		/// Used to access singletons.
		/// </summary>
		/// <returns>A reference to a singleton SubsystemManagerComponent.</returns>
		public static SubsystemManagerComponent Get()
		{
			if (_Get(out var SubsystemManager))
				return SubsystemManager;

			throw new System.Exception("No SubsystemManagerComponent found. Started project in scene without subsystem.");
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void OnBeforeSceneLoad()
		{
			GameObject SubsystemInstance = new GameObject("Subsystems");
			SubsystemInstance.AddComponent<SubsystemManagerComponent>();

			// This object persists across multiple scenes.
			DontDestroyOnLoad(SubsystemInstance);
			SubsystemScene = SubsystemInstance.scene;
		}

		private void Awake()
		{
			// Singleton enforcement.
			if (_Get(out var SubsystemManager) &&  SubsystemManager != this)
			{
				DestroyImmediate(this);
			}

			// Instantiate all subsystems.
			InstantiateGameLifetimeSubsystems();
			InstantiateSceneSubsystems();

			// Instantiate Listeners (e.g. activeSceneChanged).
			AddListeners();
		}

		#region SubsystemManagement

		/// <summary>
		/// Gets the list of class types for each defined lifetime.
		/// TODO: Create this list in-editor when the project recomplies. Add to a scriptable asset. Change this to look for that scriptable asset.
		/// </summary>
		private static Dictionary<SubsystemLifetime, List<System.Type>> GetSubsystemTypesByLifetime()
		{
			// From https://forum.unity.com/threads/get-all-types-using-an-attribute-like-how-unity-made-add-component-menu.1399663/

			// Initialize lists for each subsystem lifetime type.
			Dictionary<SubsystemLifetime, List<System.Type>> SubsystemTypesByLifetime = new Dictionary<SubsystemLifetime, List<System.Type>>();
			foreach (SubsystemLifetime lifetime in System.Enum.GetValues(typeof(SubsystemLifetime)))
			{
				SubsystemTypesByLifetime.Add(lifetime, new List<System.Type>());
			}

			// Get the subsystems and add them to the corresponding list.
			Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); // Find ALL assemblies in current domain
			for (int i = 0; i < assemblies.Length; i++)
			{
				// TODO: Ensure type inherits from MonoBehaviour.
				var UnsortedSubsystemTypes = assemblies[i].GetTypes().Where(t => t.IsDefined(typeof(SubsystemAttribute))); // Filter Types with Serializable-Attribute

				foreach (System.Type SubsystemType in UnsortedSubsystemTypes)
				{
					// Get the subsystem lifetime and then add the subsystem type to the specified lifetime list.
					SubsystemAttribute SubsystemAttribute = SubsystemType.GetCustomAttribute<SubsystemAttribute>();
					SubsystemTypesByLifetime[SubsystemAttribute.Lifetime].Add(SubsystemType);
				}
			}

			return SubsystemTypesByLifetime;
		}

		/// <summary>
		/// Instantiates subsystems of the specified type and adds them to the provided list.
		/// </summary>
		/// <typeparam name="T">The type of subsystem to instantiate.</typeparam>
		/// <param name="SubsystemList">The list to put the subystems in post-instantiation.</param>
		private void InstantiateSubsystems(List<System.Type> SubsystemTypes, ref List<Component> SubsystemList)
		{
			foreach(System.Type SubsystemType in SubsystemTypes)
			{
				SubsystemList.Add(gameObject.AddComponent(SubsystemType));
			}
		}

		/// <summary>
		/// Destroys all the subsystems in the provided list. Clears the list afterwards.
		/// </summary>
		/// <typeparam name="T">The type of subsystem to destroy.</typeparam>
		/// <param name="SubsystemList">The list of all the subsystems that need to be destroyed. Gets cleared post-execution.</param>
		private void DestroySubsystems(ref List<Component> SubsystemList)
		{
			foreach (MonoBehaviour Subsystem in SubsystemList)
			{
				Destroy(Subsystem);
			}

			SubsystemList.Clear();
		}

		#region GameLifetimeSubsystemManagement

		/// <summary>
		/// Instantiates the subsystems that exist for the duration of the game.
		/// Gets the subsystems from attributes.
		/// </summary>
		private void InstantiateGameLifetimeSubsystems()
		{
			var SubsystemTypesByLifetime = GetSubsystemTypesByLifetime();
			InstantiateSubsystems(SubsystemTypesByLifetime[SubsystemLifetime.GAME], ref GameSubsystems);
		}

		/// <summary>
		/// Destroys all the game lifetime subsystems.
		/// Called when this component is destroyed.
		/// </summary>
		private void DestroyGameLifetimeSubsystems()
		{
			DestroySubsystems(ref GameSubsystems);
		}

		#endregion GameLifetimeSubsystemManagement

		#region SceneSubsystemManagement

		/// <summary>
		/// Instantiates the subsystems that exist for the duration of a scene.
		/// Gets the subsystems from attributes.
		/// </summary>
		private void InstantiateSceneSubsystems()
		{
			var SubsystemTypesByLifetime = GetSubsystemTypesByLifetime();
			InstantiateSubsystems(SubsystemTypesByLifetime[SubsystemLifetime.SCENE], ref SceneSubsystems);
		}

		/// <summary>
		/// Destroys all the scene subsystems.
		/// Called when this component is destroyed or when we switch scenes.
		/// </summary>
		private void DestroySceneSubsystems()
		{
			DestroySubsystems(ref SceneSubsystems);
		}

		#endregion SceneSubsystemManagement

		/// <summary>
		/// Initializes the listeners that this component should always be on.
		/// Should be called once in Awake.
		/// </summary>
		private void AddListeners()
		{
			SceneManager.activeSceneChanged += OnActiveSceneChanged;
		}

		/// <summary>
		/// When the active scene is changed, replace the scene subsystems.
		/// </summary>
		private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
		{
			DestroySceneSubsystems();
			InstantiateSceneSubsystems();
		}

		#endregion SubsystemManagement

		/// <summary>
		/// Gets a reference to a subsystem of the provided type.
		/// Checks the scene subsystems first, then the game subsystems.
		/// </summary>
		/// <typeparam name="T">The type of the subsystem to get.</typeparam>
		/// <returns>The reference to the subsystem if found. Null otherwise.</returns>
		public T GetSubsystem<T>() where T: Component
		{
			var sceneSubsystem = _GetSubsystemFromList<T>(SceneSubsystems);

			if (sceneSubsystem != null)
				return sceneSubsystem;

			return _GetSubsystemFromList<T>(GameSubsystems);
		}

		/// <summary>
		/// Internally searches for a subsystem with the specified type in an
		/// IEnumerable.
		/// </summary>
		private T _GetSubsystemFromList<T>(IEnumerable<Component> toIterate) where T: Component
		{
			System.Type genericType = typeof(T);

			foreach (MonoBehaviour subsystem in toIterate)
			{
				System.Type subsystemType = subsystem.GetType();

				if (subsystemType.IsSubclassOf(genericType) || subsystemType == genericType)
					return subsystem as T;
			}

			return null;
		}

		#endregion Methods
	}
}
