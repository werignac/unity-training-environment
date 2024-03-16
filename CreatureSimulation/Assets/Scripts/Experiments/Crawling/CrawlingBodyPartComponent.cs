using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.Crawling
{
	public class CrawlingBodyPartComponent : MonoBehaviour
	{
		[SerializeField]
		private float density = 1;

		public BoxCollider Box { get; private set; }
		public ArticulationBody ArticulationBody { get; private set; }

		private List<CrawlingBodyPartComponent> ChildrenBodyParts = new List<CrawlingBodyPartComponent>();

#if UNITY_EDITOR
		[SerializeField]
		public CrawlerInitializationData.BodyPart InitData { get; set; }
#endif

		/// <summary>
		/// Call before setting the parent.
		/// </summary>
		public void Initialize(Vector3 scale)
		{
			Box = GetComponent<BoxCollider>();
			Box.size = scale;
			Transform GFX = transform.GetChild(0);
			GFX.localScale = scale;

			// Set the mass in accordance to density.
			ArticulationBody = GetComponent<ArticulationBody>();
			ArticulationBody.mass = scale.x * scale.y * scale.z * density;
		}

		public Vector3 GetRelativePointInWorld(Vector3 relativePoint)
		{
			return transform.TransformPoint(GetRelativePointInLocal(relativePoint));
		}

		public Vector3 GetRelativePointInLocal(Vector3 relativePoint)
		{
			Vector3 diffFromCenter = (Vector3.one * 0.5f) - relativePoint;
			Vector3 localPoint = Box.center - new Vector3(
				Box.size.x * diffFromCenter.x,
				Box.size.y * diffFromCenter.y,
				Box.size.z * diffFromCenter.z);

			return localPoint;
		}

		public void SetChildJoint(CrawlingBodyPartComponent other, Vector3 otherPoint)
		{
			other.transform.SetParent(this.transform);
			other.ArticulationBody.jointType = ArticulationJointType.SphericalJoint;
			other.ArticulationBody.anchorPosition = other.GetRelativePointInLocal(otherPoint);

			ChildrenBodyParts.Add(other);
		}

		public void ActivateArticulationBodies()
		{
			ArticulationBody.enabled = true;

			foreach (var childBodyPart in ChildrenBodyParts)
			{
				childBodyPart.ActivateArticulationBodies();
			}
		}
	}
}
