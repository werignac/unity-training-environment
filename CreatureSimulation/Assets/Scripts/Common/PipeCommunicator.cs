using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace werignac.Communication
{
	/// <summary>
	/// A class that communicates with an external process using named pipes.
	/// </summary>
	public class PipeCommunicator : Object, ICommunicator
	{
		// IPC fields
		private NamedPipeClientStream pipe = null;
		private StreamWriter sw = null;
		private StreamReader sr = null;

		/// <summary>
		/// Queue of lines read from the read thread.
		/// Popped from in Next().
		/// </summary>
		private Queue<string> lineQueue = new Queue<string>();

		/// <summary>
		/// Task of reading lines from the pipe continuously.
		/// </summary>
		private Task readThread;

		/// <summary>
		/// Polling task that ends when an end seignal has been sent.
		/// </summary>
		private Task readEndSignalThread;

		/// <summary>
		/// A signal, that when set to true, ends the read loop.
		/// </summary>
		private bool endReadThreadSignal = false;

		/// <summary>
		/// Opens the pipe and streams and starts the read thread.
		/// </summary>
		public PipeCommunicator(string pipeName, float pipeTimeout)
		{
			pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
			pipe.Connect((int)(pipeTimeout * 1000));

			sw = new StreamWriter(pipe);
			sr = new StreamReader(pipe);

			readEndSignalThread = ReadEndSignalThread();
			readThread = ReadThread();
		}

		/// <summary>
		/// Continuously read lines until a signal is sent to stop
		/// reading.
		/// </summary>
		/// <returns></returns>
		private async Task ReadThread()
		{
			while (true)
			{
				// TODO: Wait for line async or cancellation, whichever comes first.
				Task<string> readLineTask = sr.ReadLineAsync();
				await Task.WhenAny(readLineTask, readEndSignalThread);

				// If we received the end signal, stop reading.
				if (readEndSignalThread.IsCompleted)
				{
					return;
				}

				// If we received a line, add it to the queue.
				if (readLineTask.IsCompleted)
				{
					string line = readLineTask.Result;

					if (line == null)
						continue;

					line.Trim();

					lock (lineQueue)
					{
						lineQueue.Enqueue(line);
					}
				}
			}
		}

		/// <summary>
		/// TODO: Don't use polling for this. Use normal signals.
		/// </summary>
		/// <returns></returns>
		private async Task ReadEndSignalThread()
		{
			while (! endReadThreadSignal)
			{
				await Task.Delay(100);
			}
		}

		/// <summary>
		/// Close the streams and pipe and stop execution of the readthread.
		/// </summary>
		public void Close()
		{
			// TODO: Signal for the readthread to close using proper signaling.
			endReadThreadSignal = true;

			// Wait for read thread to finish.
			Task.WaitAll(readEndSignalThread, readThread);

			try
			{
				// Close all the streams and pipe.
				sw.Close();
				sr.Close();
				pipe.Close();
			} catch (System.Exception e)
			{
				Debug.LogError(e);
			}
		}

		/// <summary>
		/// Check if a line is queued for processing.
		/// </summary>
		/// <param name="line">The line that was dequeued from lineQueue.</param>
		/// <returns>Wether lineQueue was empty.</returns>
		public bool Next(out string line)
		{
			lock(lineQueue)
			{
				line = null;

				if (lineQueue.Count == 0)
					return false;

				line = lineQueue.Dequeue();
				return true;
			}
		}

		/// <summary>
		/// Write a line via the streamwriter and flush.
		/// </summary>
		/// <param name="line">The line to write.</param>
		public void Write(string line)
		{
			sw.WriteLine(line);
			sw.Flush();
			pipe.Flush();
			/*
			sw.WriteLineAsync(line).ContinueWith((_) =>
			{
				sw.FlushAsync().ContinueWith((_) =>
				{
					pipe.FlushAsync();
				});
			});*/
		}
	}
}
