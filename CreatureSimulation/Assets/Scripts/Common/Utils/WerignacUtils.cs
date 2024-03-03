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
		public static bool TryGetComponentInAll<T>(out T outComponent)
		{
			outComponent = default(T);
			foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
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

			while(toVisit.Count > 0)
			{
				GameObject visit = toVisit.Dequeue();
				onVisit(visit);

				for(int i = 0; i < visit.transform.childCount; i++)
				{
					toVisit.Enqueue(visit.transform.GetChild(i).gameObject);
				}
			}
		}

		#endregion
	}
}