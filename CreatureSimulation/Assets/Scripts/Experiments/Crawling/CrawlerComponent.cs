using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using UnityEngine;
using werignac.Utils;
using System.Text.Json;
using System.Threading.Tasks;
using werignac.GeneticAlgorithm;
using werignac.Communication;
using werignac.Subsystem;
using werignac.GeneticAlgorithm.Dispatch;

namespace werignac.Crawling
{
	public class CrawlerMoveInstruction { }

	/// <summary>
	/// Component for a single crawling creature with two parts.
	/// 
	/// Generates parts, initializes a pipe for commands, and communicates with brain until death.
	/// </summary>
	public class CrawlerComponent : MonoBehaviour, IAsyncSimulateStep
	{
		#region Communication
		private Dispatcher dispatcher;
		private CrawlingExperiment experiment;
		JsonParser<CrawlerMoveInstruction> jsonParser;
		#endregion Communication

		[SerializeField]
		private GameObject bodyPartPrefab;

		private CrawlingBodyPartComponent firstCrawler;
		private CrawlingBodyPartComponent secondCrawler;
		private ArticulationBody joint;

		[SerializeField]
		private CrawlerInitializationData initData;

		#region Between-Call Data
		/// <summary>
		/// Instruction sent through pipe of how to move the segments of this creature.
		/// </summary>
		private CrawlerMoveInstruction moveInstruction;

		/// <summary>
		/// deserialized simulation frame to write in incoming async process.
		/// </summary>
		private CrawlingBodyPartComponent.DeserializedSimulationFrame deserializedSimulationFrame;
		#endregion

		public void Initialize(CrawlerInitializationData initData)
		{
			dispatcher = SubsystemManagerComponent.Get().GetSubsystem<Dispatcher>();
			// TODO: Be passed communication objects instead of having to fetch them.
			//  would help with build integration.
			if (!WerignacUtils.TryGetComponentInActiveScene(out experiment))
				throw new System.Exception("Could not find experiment for crawler component");
			jsonParser = experiment.Multiplexer.GetParserFromIndex(initData.Index);

			// Initialize the body parts of this crawler.
			InitializeBodies(initData);

			// Set initData references for editor visuals.
			this.initData = initData;
			firstCrawler.InitData = initData.First;
			secondCrawler.InitData = initData.Second;

			// Notify the client about the creature's structure.
			WriteLine(JsonSerializer.Serialize(firstCrawler.GetDeserializedCreatureStructure()));
		}

		public void InitializeBodies(CrawlerInitializationData initData)
		{
			// Create first box.
			Vector3 firstWorldScale = initData.First.Size;
			Quaternion firstWorldRot = Quaternion.Euler(initData.First.Rotation);
			GameObject first = Instantiate(bodyPartPrefab, Vector3.zero, firstWorldRot, transform);
			firstCrawler = first.GetComponent<CrawlingBodyPartComponent>();
			firstCrawler.Initialize(firstWorldScale);

			joint = first.GetComponent<ArticulationBody>();

			// Create second so that the articulation body lines up.
			Vector3 secondWorldScale = initData.Second.Size;
			Quaternion secondWorldRot = Quaternion.Euler(initData.Second.Rotation);
			GameObject second = Instantiate(bodyPartPrefab, Vector3.zero, secondWorldRot, transform);
			secondCrawler = second.GetComponent<CrawlingBodyPartComponent>();
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
		}

		public void WriteLine(string line)
		{
			string line_with_multiplex_prefix = $"{initData.Index} {line}";
			dispatcher?.Communicator?.Write(line_with_multiplex_prefix);
		}

		public void OnSimulateStep(float deltaTime)
		{
			deserializedSimulationFrame = firstCrawler.GetDeserializedSimulationFrame();
		}

		public async Task OnSimulateStepAsync(float deltaTime)
		{
			// Report the current velocities and positions. Report Score?
			WriteLine(JsonSerializer.Serialize(deserializedSimulationFrame));

			if (dispatcher != null)
			{
				JsonCommand<CrawlerMoveInstruction> command = await WerignacUtils.AwaitTimeout(jsonParser.GetCommandAsync(), 1000, $"wait for command in creature {initData.Index}.");
				dispatcher.CommunicatorBuffer.AcceptNext();
				// Get the last move instruction and save it for PostSimulateStepAsync.
				foreach (var _moveInstruction in command.DeserializedObjects)
				{
					moveInstruction = _moveInstruction;
				}
			}
		}

		public void OnPostSimulateStepAsync(float deltaTime)
		{
			// moveInstruction can be null on timeout or if we're not connected to Python.
			if (moveInstruction == null)
				return;

			// TODO: Do something with moveInstruction
			moveInstruction = null;
		}

		private void OnDestroy()
		{
			experiment.Multiplexer.RemoveParser(initData.Index);
		}
	}
}
