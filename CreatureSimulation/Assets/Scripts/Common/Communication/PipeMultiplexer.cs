using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using System.Text.Json;
using System.Threading.Tasks;

namespace werignac.Communication
{
	public class PipeMultiplexer : MonoBehaviour
	{
		#region Pipe
		private NamedPipeClientStream pipe = null;
		private StreamWriter sw = null;
		private StreamReader sr = null;

		private Task readLoopTask;
		#endregion

		#region Multiplexing

		private class IDBuffer
		{
			private Queue<string> buffer = new Queue<string>();

			
			public string ReadLine()
			{
				return buffer.Dequeue();
			}

			public void WriteLine(string toWrite)
			{
				buffer.Enqueue(toWrite);
			}

			public void Close()
			{
			}
		}

		/// <summary>
		/// Stores the incoming lines from the pipe into this buffer.
		/// An id is used to distinguish which lines are for which listeners.
		/// </summary>
		private Dictionary<int, IDBuffer> LineBuffer = new Dictionary<int, IDBuffer>();
		#endregion

		#region Events
		public UnityEvent<int> onLineIn = new UnityEvent<int>();
		public UnityEvent onClose = new UnityEvent();
		#endregion

		[Header("Debugging")]
		[SerializeField, Tooltip("How long in seconds before attempting to connect the pipe causes a timeout.")]
		private float pipeTimeout = 10;
#if UNITY_EDITOR
		[SerializeField, Tooltip("The name of the pipe to use in debugging.")]
		private string testPipeName = "Pipe0";
		[SerializeField, Tooltip("Whether to use the Test Pipe Name (in debugging) or get the passed pipe.")]
		private bool useTestPipeName = true;
#endif

		public void Initialize()
		{
			InitializePipe();

			readLoopTask = ReadLoop();
		}

		public void InitializePipe()
		{
			string pipeName = "";

#if UNITY_EDITOR
			if (useTestPipeName)
				pipeName = testPipeName;
#endif


			if (pipeName == null || (pipeName.Length == 0))
				return;

			pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
			// TODO: Always use timeout
#if UNITY_EDITOR
			pipe.ConnectAsync();
#else
			pipe.ConnectAsync((int) (pipeTimeout * 1000));
#endif
			sw = new StreamWriter(pipe);
			sr = new StreamReader(pipe);
		}

		/// <summary>
		/// Writes a line to the pipe from a id. Used for sending updates about a particular
		/// creature's state.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="toWrite"></param>
		/// <param name="flush"></param>
		public void WriteLine(int id, string toWrite, bool flush = true)
		{
			WriteLine($"{id} {toWrite}", flush);
		}

		/// <summary>
		/// Writes a line to the pipe.
		/// </summary>
		/// <param name="toWrite"></param>
		/// <param name="flush"></param>
		public void WriteLine(string toWrite, bool flush = true)
		{
			sw?.WriteLine(toWrite);
			if (flush)
			{
				sw?.Flush();
				pipe?.Flush();
			}
		}

		public async Task<string> ReadLineAsync(int id)
		{
			return await Task.Run(() => ReadLine(id));
		}

		public string ReadLine(int id)
		{
			IDBuffer buff;
			lock (LineBuffer)
			{
				buff = GetBufferForID(id);
			}

			return buff.ReadLine();
		}

		private IDBuffer GetBufferForID(int id)
		{
			if (!LineBuffer.ContainsKey(id))
			{
				LineBuffer.Add(id, new IDBuffer());
			}
			return LineBuffer[id];
		}

		private async Task ReadLoop()
		{
			// TODO: Read messages until there are none left.
			string line = "";
			while (true)
			{
				line = await sr.ReadLineAsync();

				// If told to quit, stop running the loop.
				if (line == "QUIT" || line == null)
				{
					sw?.Close();
					sr?.Close();
					pipe?.Close();
					onClose.Invoke();
					break;
				}

				// Otherwise, multiplex the line.
				int indexOfSpace = line.IndexOf(' ');
				int id = int.Parse(line.Substring(0, indexOfSpace));
				string subline = line.Substring(indexOfSpace + 1);
				MultiplexLine(id, subline);
			}
		}

		private void MultiplexLine(int id, string line)
		{
			lock (LineBuffer)
			{
				IDBuffer buff = GetBufferForID(id);
				buff.WriteLine(line);
			}
			onLineIn.Invoke(id);
		}

		public void CloseID(int id)
		{
			lock (LineBuffer)
			{
				if (LineBuffer.ContainsKey(id))
				{
					LineBuffer[id].Close();
					LineBuffer.Remove(id);
				}
			}
		}

		public void CloseAllIDs()
		{
			lock (LineBuffer)
			{
				foreach (var pair in LineBuffer)
				{
					LineBuffer[pair.Key].Close();
				}
				LineBuffer.Clear();
			}
		}

		/// <summary>
		/// Closes the pipe by sending a end message, and then waiting for a QUIT response
		/// in the ReadLoop.
		/// </summary>
		/// <returns></returns>
		public async Task ClosePipeAsync()
		{
			await Task.Run(ClosePipe);
		}

		public void ClosePipe()
		{
			if (sw != null)
			{
				sw.WriteLine("END");
				sw.Flush();
			}

			// Wait until the read loop has closed.
			readLoopTask.Wait();
		}
	}
}
