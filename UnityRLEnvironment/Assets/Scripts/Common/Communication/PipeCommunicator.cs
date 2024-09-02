/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace werignac.Communication
{
	/// <summary>
	/// A class that communicates with an external process using named pipes.
	/// TODO: Get rid of Next() and line queue in ICommunicator. Defer to a buffer
	/// that can be told asynchronously when to send the next line to a parser.
	/// </summary>
	public class PipeCommunicator : UnityEngine.Object, ICommunicator
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
		/// The action to call when a line is successfully read.
		/// </summary>
		private Action<string> _onReadLine;

		/// <summary>
		/// Thread that waits for the signal to stop reading.
		/// Cleans up the endReadThreadSignal afterwards.
		/// </summary>
		private Task endReadWaitThread;
		
		/// <summary>
		/// A signal, that when set to true, ends the read loop.
		/// TODO: Cleanup
		/// </summary>
		private ManualResetEventSlim endReadThreadSignal = new ManualResetEventSlim(false);

		/// <summary>
		/// Opens the pipe and streams and starts the read thread.
		/// </summary>
		public PipeCommunicator(string pipeName, float pipeTimeout, Action<string> onReadLine)
		{
			_onReadLine = onReadLine;

			pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
			pipe.Connect((int)(pipeTimeout * 1000));

			sw = new StreamWriter(pipe);
			sr = new StreamReader(pipe);

			endReadWaitThread = Task.Run(() => { WaitForEndSignal(); });
			readThread = ReadThread();
		}

		/// <summary>
		/// A method that just waits for the signal to stop reading and then
		/// disposes the signal. Runs on a different thread to detect when
		/// we should stop reading from the pipe.
		/// </summary>
		private void WaitForEndSignal()
		{
			endReadThreadSignal.Wait();
			endReadThreadSignal.Dispose();
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
				// Wait for line async or cancellation, whichever comes first.
				Task<string> readLineTask = sr.ReadLineAsync();
				Task awaitedEvent = await Task.WhenAny(endReadWaitThread, readLineTask);

				// If we received the end signal, stop reading.
				if (awaitedEvent == endReadWaitThread)
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
						Debug.Log($"Read Line from Pipe: \"{line}\".");
					}

					_onReadLine(line);
				}
			}
		}

		/// <summary>
		/// Close the streams and pipe and stop execution of the readthread.
		/// </summary>
		public void Close()
		{
			// Signal for the readthread to close using proper signaling.
			endReadThreadSignal.Set();

			// Wait for read thread to finish.
			if (!Task.WaitAll(new Task[] { endReadWaitThread, readThread }, 5000))
				Debug.LogWarning("Failed to close PipeCommunicator.");

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
		}
	}
}
