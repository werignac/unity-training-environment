using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.Creatures
{
	[RequireComponent(typeof(ArticulationBody))]
	public class ArticulationInput : MonoBehaviour
	{
		private ArticulationBody articulationBody;

		private List<SignalSender> jointAngles = new List<SignalSender>();
		private List<SignalSender> jointAngularVelocities = new List<SignalSender>();
		private List<SignalSender> jointLinearVelocities = new List<SignalSender>();
		//		private SignalSender[] jointVelocities;

		private DebugNeuron debug;

		class AngleObserver : SignalSender
		{
			private ArticulationBody articulationBody;
			private int xyz; // 0, 1, or 2
			private List<Tuple<SignalReceiver<float>, int>> outConnections = new List<Tuple<SignalReceiver<float>, int>>();

			public AngleObserver(ArticulationBody _articulationBody, int _xyz)
			{
				articulationBody = _articulationBody;
				xyz = _xyz;
			}

			public void AddOutConnections(params Tuple<SignalReceiver<float>, int>[] connections)
			{
				outConnections.AddRange(connections);
			}

			public void Evaluate()
			{
				Vector3 euler = articulationBody.transform.localEulerAngles;
				float angle = euler[xyz] * Mathf.Deg2Rad;

				foreach(Tuple<SignalReceiver<float>, int> connection in outConnections)
				{
					connection.Item1.GetSignal().SetInput(angle, connection.Item2);
				}
			}

			public void SetOutConnection(SignalReceiver<float> _toSet, int index)
			{
				outConnections.Add(new Tuple<SignalReceiver<float>, int>(_toSet, index));
			}
		}

		class AngularVelocityObserver : SignalSender
		{
			private ArticulationBody articulationBody;
			private int xyz; // 0, 1, or 2
			private Dictionary<SignalReceiver<float>, int> outConnections = new Dictionary<SignalReceiver<float>, int>();

			public AngularVelocityObserver(ArticulationBody _articulationBody, int _xyz)
			{
				articulationBody = _articulationBody;
				xyz = _xyz;
			}

			public void AddOutConnections(params Tuple<SignalReceiver<float>, int>[] connections)
			{
				foreach (var connection in connections)
					outConnections.Add(connection.Item1, connection.Item2);
			}

			public void Evaluate()
			{
				Vector3 euler = articulationBody.transform.InverseTransformDirection(articulationBody.angularVelocity);
				float angle = euler[xyz] * Mathf.Deg2Rad;

				foreach (var connection in outConnections)
				{
					connection.Key.GetSignal().SetInput(angle, connection.Value);
				}
			}

			public void SetOutConnection(SignalReceiver<float> _toSet, int index)
			{
				outConnections.Add(_toSet, index);
			}
		}

		class LinearVelocityObserver : SignalSender
		{
			private ArticulationBody articulationBody;
			private int xyz; // 0, 1, or 2
			private Dictionary<SignalReceiver<float>, int> outConnections = new Dictionary<SignalReceiver<float>, int>();

			public LinearVelocityObserver(ArticulationBody _articulationBody, int _xyz)
			{
				articulationBody = _articulationBody;
				xyz = _xyz;
			}

			public void AddOutConnections(params Tuple<SignalReceiver<float>, int>[] connections)
			{
				foreach (var connection in connections)
					outConnections.Add(connection.Item1, connection.Item2);
			}

			public void Evaluate()
			{
				Vector3 euler = articulationBody.transform.InverseTransformDirection(articulationBody.velocity);
				float angle = euler[xyz] * Mathf.Deg2Rad;

				foreach (var connection in outConnections)
				{
					connection.Key.GetSignal().SetInput(angle, connection.Value);
				}
			}

			public void SetOutConnection(SignalReceiver<float> _toSet, int index)
			{
				outConnections.Add(_toSet, index);
			}
		}

		// Start is called before the first frame update
		void Start()
		{
			articulationBody = GetComponent<ArticulationBody>();

			string format = $"Joint {name} Angles: ( ";

			if (articulationBody.twistLock != ArticulationDofLock.LockedMotion)
			{
				format += "{" + jointAngles.Count.ToString() + "}, ";
				jointAngles.Add(new AngleObserver(articulationBody, 0));
			}
			else
			{
				format += "0, ";
			}

			jointAngularVelocities.Add(new AngularVelocityObserver(articulationBody, 0));
			jointLinearVelocities.Add(new LinearVelocityObserver(articulationBody, 0));

			if (articulationBody.swingYLock != ArticulationDofLock.LockedMotion)
			{
				format += "{" + jointAngles.Count.ToString() + "}, ";
				jointAngles.Add(new AngleObserver(articulationBody, 1));
			}
			else
			{
				format += "0, ";
			}

			jointAngularVelocities.Add(new AngularVelocityObserver(articulationBody, 1));
			jointLinearVelocities.Add(new LinearVelocityObserver(articulationBody, 1));

			if (articulationBody.swingZLock != ArticulationDofLock.LockedMotion)
			{
				format += "{" + jointAngles.Count.ToString() + "} )";
				jointAngles.Add(new AngleObserver(articulationBody, 2));
			}
			else
			{
				format += "0 )";
			}

			jointAngularVelocities.Add(new AngularVelocityObserver(articulationBody, 2));
			jointLinearVelocities.Add(new LinearVelocityObserver(articulationBody, 2));

			format += $"\nAngular Velocity: ({"{" + jointAngles.Count + "}"}, {"{" + (jointAngles.Count + 1) + "}"}, {"{" + (jointAngles.Count + 2) + "}"})";
			format += $"\nLinear Velocity: ({"{" + (jointAngles.Count + 3) + "}"}, {"{" + (jointAngles.Count + 4) + "}"}, {"{" + (jointAngles.Count + 5) + "}"})";


			debug = new DebugNeuron(format, jointAngles.Count + jointAngularVelocities.Count + jointLinearVelocities.Count);

			for (int i = 0; i < jointAngles.Count; i++)
				jointAngles[i].SetOutConnection(debug, i);

			for (int i = 0; i < jointAngularVelocities.Count; i++)
				jointAngularVelocities[i].SetOutConnection(debug, i + jointAngles.Count);

			for (int i = 0; i < jointLinearVelocities.Count; i++)
				jointLinearVelocities[i].SetOutConnection(debug, i + jointAngles.Count + jointAngularVelocities.Count);
		}

		private void Update()
		{
			// TODO: Remove and put in creature
			debug.Progress();

			foreach(SignalSender jointAngle in jointAngles)
				jointAngle.Evaluate();

			foreach (SignalSender jointAngularVelocity in jointAngularVelocities)
				jointAngularVelocity.Evaluate();

			foreach (SignalSender jointLinearVelocity in jointLinearVelocities)
				jointLinearVelocity.Evaluate();

			debug.Evaluate();
		}
	}
}
