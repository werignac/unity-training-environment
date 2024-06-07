using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using UnityEngine;
using werignac.Utils;
using System.Text.Json;
using System.Threading.Tasks;
using werignac.GeneticAlgorithm;


namespace werignac.Crawling
{
	/// <summary>
	/// Component for a single crawling creature with two parts.
	/// 
	/// Generates parts, initializes a pipe for commands, and communicates with brain until death.
	/// </summary>
	public class CrawlerComponent : MonoBehaviour, IAsyncSimulateStep
	{
		[SerializeField]
		private GameObject bodyPartPrefab;

		private CrawlingBodyPartComponent firstCrawler;
		private CrawlingBodyPartComponent secondCrawler;
		private ArticulationBody joint;

		[SerializeField]
		private CrawlerInitializationData initData;

		#region Pipe
		private NamedPipeClientStream pipe = null;
		private StreamWriter sw = null;
		private StreamReader sr = null;

		/// <summary>
		/// Used to tell when the connection task for the pipe has completed.
		/// </summary>
		private Task connectTask = null;
		/// <summary>
		/// Lines that are queued for writing before pipe connects.
		/// </summary>
		private Queue<string> LineQueue = new Queue<string>();
		#endregion

		#region Between-Call Data
		/// <summary>
		/// Instruction sent through pipe of how to move the segments of this creature.
		/// </summary>
		private string moveInstruction;

		/// <summary>
		/// deserialized simulation frame to write in incoming async process.
		/// </summary>
		private CrawlingBodyPartComponent.DeserializedSimulationFrame deserializedSimulationFrame;
		#endregion

		public void Initialize(CrawlerInitializationData initData)
		{
			// Initialize the body parts of this crawler.
			InitializeBodies(initData);

			// Connect to a pipe or set pipe to null.
			InitializePipe(initData);

			// Notify the client about the creature's structure.
			WriteLine(JsonSerializer.Serialize(firstCrawler.GetDeserializedCreatureStructure())).Wait();

			// Set initData references for editor visuals.
			this.initData = initData;
			firstCrawler.InitData = initData.First;
			secondCrawler.InitData = initData.Second;
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

		public void InitializePipe(CrawlerInitializationData initData)
		{
			if (initData.PipeName == null || (initData.PipeName.Length == 0))
				return;

			pipe = new NamedPipeClientStream(".", initData.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

			// TODO: Always use timeout
#if UNITY_EDITOR
			connectTask = pipe.ConnectAsync(10 * 1000);
#else
			connectTask = pipe.ConnectAsync(10 * 1000);
#endif
			sw = new StreamWriter(pipe);
			sr = new StreamReader(pipe);
		}

		/// <summary>
		/// Flushes the queue for lines that need to be written prior to connection.
		/// Should only be called once after connect.
		/// </summary>
		private async Task FlushLineQueue()
		{
			// TODO: Remove
#if UNITY_EDITOR
			Debug.LogFormat("Pipe connection for creature {0} complete", initData.Index);
#endif
			// Can only be set after connection.
			while (LineQueue.TryDequeue(out string line))
			{
				await AwaitTimeout(sw?.WriteLineAsync(line), 2000, "write from the line queue");
			}
			await AwaitTimeout(sw?.FlushAsync(), 2000, "flushing from the line queue");

			connectTask = null;
			LineQueue.Clear();
		}

		public async Task WriteLine(string line)
		{
			if (pipe == null)
				return;

			// If we have not connected, add to the queue.
			if (connectTask != null && !connectTask.IsCompleted)
			{
				LineQueue.Enqueue(line);
			}
			else
			{
				// If this is the first write post-connecting, flush any queued lines pre-connecting.
				if (connectTask != null)
					await FlushLineQueue();

				await AwaitTimeout(sw?.WriteLineAsync(line), 2000, "write a line");
				// Always flush as we need an immediate response from the brain.
				await AwaitTimeout(sw?.FlushAsync(), 3000, "flush line");
			}
		}

		public void OnSimulateStep(float deltaTime)
		{
			deserializedSimulationFrame = firstCrawler.GetDeserializedSimulationFrame();
		}

		// TODO: Make async.
		public async Task OnSimulateStepAsync(float deltaTime)
		{
			// Report the current velocities and positions. Report Score?
			await WriteLine(JsonSerializer.Serialize(deserializedSimulationFrame));

			// Read and execute input.
			// TODO: Consider running brain asynchronously.
			if (sr != null && pipe != null)
			{
				// Should only be called at most once.
				if (connectTask != null && !connectTask.IsCompleted)
				{
					await connectTask;
					await FlushLineQueue();
				}
				moveInstruction = await AwaitTimeout(sr.ReadLineAsync(), 3000, "read instruction");

				//Debug.Log($"Line \"{moveInstruction}\" on creature {initData.Index}");
			}
		}

		public void OnPostSimulateStepAsync(float deltaTime)
		{
			// TODO: Do something with moveInstruction
			moveInstruction = null;
		}

		private void OnDestroy()
		{
			if (connectTask != null && !connectTask.IsCompleted)
				connectTask.Wait();

			sw?.WriteLine("END");
			sw?.Flush();


			if (sr != null)
			{
				string line = sr.ReadLine();
				while (line != "QUIT")
				{
					Debug.LogFormat("Unexpected Quitting Line {0}", line);
					line = sr.ReadLine();
				}
			}

			sw?.Close();
			sr?.Close();
			pipe?.Close();
		}

//#if UNITY_EDITOR
		private async Task AwaitTimeout(Task task, int timeout, string context)
		{
			// TODO: Use timeouts when a certain flag is present in runtime settings.
			Task returnedTask = await Task.WhenAny(task, Task.Delay(timeout));

			if (returnedTask != task)
				throw new System.Exception($"Timeout in CralwerComponent after {timeout / 1000} seconds on creature {initData.Index} when trying to {context}.");
		}

		private async Task<T> AwaitTimeout<T>(Task<T> task, int timeout, string context)
		{
			// TODO: Use timeouts when a certain flag is present in runtime settings.
			Task returnedTask = await Task.WhenAny(task, Task.Delay(timeout));

			if (returnedTask != task)
				throw new System.Exception($"Timeout in CrawlerComponent after {timeout / 1000} seconds on creature {initData.Index} when trying to {context}.");

			return task.Result;
		}
//#endif
	}
}
