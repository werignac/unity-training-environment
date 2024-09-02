/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.RLEnvironment;

namespace werignac.Crawling
{
	public class CrawlingBodyPartComponent : MonoBehaviour
	{
		// TODO: Create this SimulationFrame in a function and read it to the individual pipe.
		public struct DeserializedSimulationFrame
		{
			public DeserializedVector Velocity { get; set; }
			public DeserializedVector AngularVelocity { get; set; }
			public DeserializedVector WorldPosition { get; set; }
			// Note: not all 3 dimensions may be used.
			public DeserializedVector ArticulationPosition { get; set; }
			public List<DeserializedSimulationFrame> Children { get; set; }

			public override string ToString()
			{
				string message = "";

				message += $"Velocity: {Velocity.ToVector()}";
				message += $"\nAngular Velocity: {AngularVelocity.ToVector()}";
				message += $"\nWorld Postion: {WorldPosition.ToVector()}";
				message += $"\nArticulation Position: {ArticulationPosition.ToVector()}";
				return message;
			}
		}

		public struct DeserializedCreatureStructure
		{
			public DeserializedVector WorldPosition { get; set; }
			public DeserializedVector LocalPosition { get; set; }
			public DeserializedVector ArticulationPosition { get; set; }
			public DeserializedVector WorldRotation { get; set; }
			public DeserializedVector LocalRotation { get; set; }
			public DeserializedVector Size { get; set; }

			public int DegreesOfFreedom { get; set; }

			public List<DeserializedCreatureStructure> Children { get; set; }
		}

		[SerializeField]
		private float density = 1;

		public BoxCollider Box { get; private set; }
		public ArticulationBody ArticulationBody { get; private set; }

		private List<CrawlingBodyPartComponent> ChildrenBodyParts = new List<CrawlingBodyPartComponent>();

		[SerializeField]
		public CrawlerInitializationData.BodyPart InitData { get; set; }

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

		public DeserializedSimulationFrame GetDeserializedSimulationFrame(
#if UNITY_EDITOR
		bool e_Children = true
#endif
		)
		{
			DeserializedSimulationFrame frame = new DeserializedSimulationFrame();
			frame.Velocity = new DeserializedVector(ArticulationBody.velocity);
			frame.AngularVelocity = new DeserializedVector(ArticulationBody.angularVelocity);
			Vector3 articulationPos = new Vector3();
			for (int i = 0; i < ArticulationBody.dofCount; i++)
				articulationPos[i] = ArticulationBody.jointPosition[i];
			frame.ArticulationPosition = new DeserializedVector(articulationPos);
			frame.WorldPosition = new DeserializedVector(transform.position);

#if UNITY_EDITOR
			if (!e_Children)
				return frame;
#endif

			frame.Children = new List<DeserializedSimulationFrame>();

			foreach (CrawlingBodyPartComponent child in ChildrenBodyParts)
			{
				frame.Children.Add(child.GetDeserializedSimulationFrame());
			}

			return frame;
		}

		public DeserializedCreatureStructure GetDeserializedCreatureStructure()
		{
			DeserializedCreatureStructure structure = new DeserializedCreatureStructure();

			structure.WorldPosition = new DeserializedVector(transform.position);
			structure.LocalPosition = new DeserializedVector();
			structure.WorldRotation = new DeserializedVector(transform.rotation.eulerAngles);
			structure.LocalRotation = new DeserializedVector(transform.localRotation.eulerAngles);
			structure.Size = new DeserializedVector(Box.size);

			structure.DegreesOfFreedom = ArticulationBody.dofCount;

			structure.Children = new List<DeserializedCreatureStructure>();
			foreach (CrawlingBodyPartComponent child in ChildrenBodyParts)
			{
				structure.Children.Add(child.GetDeserializedCreatureStructure());
			}

			return structure;
		}
	}
}
