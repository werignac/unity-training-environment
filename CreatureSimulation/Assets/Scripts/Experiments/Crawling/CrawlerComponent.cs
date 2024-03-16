using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Utils;

namespace werignac.Crawling
{
	public class CrawlerComponent : MonoBehaviour
	{
		[SerializeField]
		private GameObject bodyPartPrefab;

		private GameObject first;
		private GameObject second;
		private ArticulationBody joint;

#if UNITY_EDITOR
		[SerializeField]
		private CrawlerInitializationData initData;
#endif

		public void Initialize(CrawlerInitializationData initData)
		{
			// Create first box.
			Vector3 firstWorldScale = initData.First.Size;
			Quaternion firstWorldRot = Quaternion.Euler(initData.First.Rotation);
			first = Instantiate(bodyPartPrefab, Vector3.zero, firstWorldRot, transform);
			var firstCrawler = first.GetComponent<CrawlingBodyPartComponent>();
			firstCrawler.Initialize(firstWorldScale);

			joint = first.GetComponent<ArticulationBody>();

			// Create second so that the articulation body lines up.
			Vector3 secondWorldScale = initData.Second.Size;
			Quaternion secondWorldRot = Quaternion.Euler(initData.Second.Rotation);
			second = Instantiate(bodyPartPrefab, Vector3.zero, secondWorldRot, transform);
			var secondCrawler = second.GetComponent<CrawlingBodyPartComponent>();
			secondCrawler.Initialize(secondWorldScale);

			Vector3 secondDisplacement = firstCrawler.GetRelativePointInWorld(initData.First.ConnectionPoint) - secondCrawler.GetRelativePointInWorld(initData.Second.ConnectionPoint);
			second.transform.Translate(secondDisplacement, Space.World);
			firstCrawler.SetChildJoint(secondCrawler, initData.Second.ConnectionPoint);

			// Update transforms to perform next calculations
			Physics.SyncTransforms();

			// TODO: Normalize scale?

			// Set on ground and center.
			Bounds creatureBounds = first.GetCompositeAABB();
			Vector3 centerOfMass = firstCrawler.ArticulationBody.GetCompositeCenterOfMass();
			Vector3 translation = new Vector3(-centerOfMass.x, -creatureBounds.min.y, -centerOfMass.z);
			first.transform.Translate(translation, Space.World);
			
			first.transform.SetParent(transform, true);

			// Activate Articulation Bodies after all transformations are set.
			firstCrawler.ActivateArticulationBodies();

			// TODO: Connect to a pipe.

#if UNITY_EDITOR
			this.initData = initData;
			firstCrawler.InitData = initData.First;
			secondCrawler.InitData = initData.Second;
#endif
		}

		public void OnSimulateStep(float deltaTime)
		{
			// TODO: Report the current velocities and positions and execute input.
		}
	}
}
