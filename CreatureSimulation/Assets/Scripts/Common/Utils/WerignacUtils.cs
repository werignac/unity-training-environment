using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace werignac.Utils
{
	public static class WerignacUtils
	{
		#region TryGetComponentInParent
		public static bool TryGetComponentInParent<T>(this Component inComponent, out T outComponent)
		{
			outComponent = inComponent.GetComponentInParent<T>();
			return outComponent != null;
		}

		public static bool TryGetComponentInParent<T>(this GameObject gameObject, out T component)
		{
			component = gameObject.GetComponentInParent<T>();
			return component != null;
		}
		#endregion

		#region TryGetComponentInAll
		public static bool TryGetComponentInActiveScene<T>(out T outComponent)
		{
			return TryGetComponentInScene<T>(SceneManager.GetActiveScene(), out outComponent);
		}

		public static bool TryGetComponentInScene<T>(Scene scene, out T outComponent)
		{
			outComponent = default(T);
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				outComponent = root.GetComponentInChildren<T>();
				if (outComponent != null)
					return true;
			}
			return false;
		}

		#endregion

		#region BroadcastToAll
		public static void BroadcastToAll(string methodName, object parameter = null)
		{
			foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
				root.BroadcastMessage(methodName, parameter, SendMessageOptions.DontRequireReceiver);
		}
		#endregion

		#region PercolateUp

		public class Percolation<T>
		{
			private bool halt = false;
			private T data;

			public Percolation(T _data)
			{
				data = _data;
			}

			public T GetData()
			{
				return data;
			}

			public void Halt()
			{
				halt = true;
			}

			public bool GetHalt()
			{
				return halt;
			}
		}

		public static void Percolate<T>(this GameObject obj, string recieverFunction, T data)
		{
			GameObject current = obj.transform.parent.gameObject;
			Percolation<T> percolation = new Percolation<T>(data);

			while (current && !percolation.GetHalt())
			{
				current.SendMessage(recieverFunction, percolation, SendMessageOptions.DontRequireReceiver);
				current = current.transform.parent.gameObject;
			}
		}

		#endregion

		#region ForEach Gameobject

		/// <summary>
		/// Performs a breadth-first search on a game object and its children invoking the passed
		/// function on each game object.
		/// </summary>
		public static void ForEach(this GameObject toIterateOver, Action<GameObject> onVisit)
		{
			Queue<GameObject> toVisit = new Queue<GameObject>();
			toVisit.Enqueue(toIterateOver);

			while (toVisit.Count > 0)
			{
				GameObject visit = toVisit.Dequeue();
				onVisit(visit);

				for (int i = 0; i < visit.transform.childCount; i++)
				{
					toVisit.Enqueue(visit.transform.GetChild(i).gameObject);
				}
			}
		}

		#endregion

		#region BoundsFromMinAndMax
		public static Bounds BoundsFromMinAndMax(Vector3 min, Vector3 max)
		{
			Vector3 center = (max + min) / 2;
			Vector3 size = max - min;
			return new Bounds(center, size);
		}

		#endregion

		#region GetCompositeAABB
		public static Bounds GetCompositeAABB(this GameObject self, bool countZeroSizeBounds = false)
		{
			Bounds composite = new Bounds(self.transform.position, Vector3.zero);

			Collider collider = self.GetComponent<Collider>();

			if (collider != null)
				composite = collider.bounds;

			foreach (Transform childT in self.transform)
			{
				Bounds childAABB = childT.gameObject.GetCompositeAABB(countZeroSizeBounds);

				if ((!countZeroSizeBounds) && childAABB.size == Vector3.zero)
					continue;

				if ((!countZeroSizeBounds) && composite.size == Vector3.zero)
				{
					composite = childAABB;
					continue;
				}

				Vector3 compositeMin = Vector3.Min(composite.min, childAABB.min);
				Vector3 compositeMax = Vector3.Max(composite.max, childAABB.max);

				composite = BoundsFromMinAndMax(compositeMin, compositeMax);
			}

			return composite;
		}

		#endregion

		#region GetCompositeCenterOfMass

		public static Vector3 GetCompositeCenterOfMass(this ArticulationBody self)
		{
			return self.gameObject.GetCompositeCenterOfMass(out float _);
		}

		private static Vector3 GetCompositeCenterOfMass(this GameObject self, out float mass)
		{
			Vector3 centerOfMass = self.transform.position;
			mass = 0;
			if (self.TryGetComponent<ArticulationBody>(out ArticulationBody articulationBody))
			{
				mass = articulationBody.mass;
				centerOfMass = articulationBody.worldCenterOfMass;
			}

			foreach (Transform childT in self.transform)
			{
				Vector3 childCenterOfMass = childT.gameObject.GetCompositeCenterOfMass(out float childMass);

				mass += childMass;

				float childWeight = childMass / mass;

				centerOfMass = (childWeight) * childCenterOfMass + (1 - childWeight) * centerOfMass;
			}

			return centerOfMass;
		}

		#endregion

		#region GetCompositeMass

		public static float GetCompositeMass(this ArticulationBody self)
		{
			return self.gameObject.GetCompositeMass();
		}

		private static float GetCompositeMass(this GameObject self)
		{
			float mass = 0;
			if (self.TryGetComponent<ArticulationBody>(out ArticulationBody articulationBody))
				mass = articulationBody.mass;

			foreach (Transform childT in self.transform)
			{
				mass += childT.gameObject.GetCompositeMass();
			}

			return mass;
		}

		#endregion

		#region GetSceneRoots
		/// <summary>
		/// Returns a list of the root gameobjects in the active scene.
		/// </summary>
		/// <returns>A list of the root gameobjects in the active scene.</returns>
		public static GameObject[] GetActiveSceneRoots()
		{
			return SceneManager.GetActiveScene().GetRootGameObjects();
		}

		#endregion GetSceneRoots
	}
}
