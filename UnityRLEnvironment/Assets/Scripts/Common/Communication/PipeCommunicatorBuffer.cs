/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System;

namespace werignac.Communication
{
	/// <summary>
	/// A class that serves as an asynchronous buffer for the PipeCommunicator.
	/// 
	/// The idea is that this buffer can receive lines read asynchronously and wait to parse
	/// them before the last one has been parsed.
	/// 
	/// Should be constructed before PipeCommunicator, and OnReadLine should be passed
	/// to PipeCommunicator.
	///
	/// </summary>
    public class PipeCommunicatorBuffer
    {
		private ConcurrentQueue<string> buffer = new ConcurrentQueue<string>();

		/// <summary>
		/// The loop of emptrying the buffer one item at a time.
		/// </summary>
		private Task bufferLoopThread;

		/// <summary>
		/// Action to call when a line is removed from the buffer.
		/// </summary>
		private Action<string> _onBufferOut;

		/// <summary>
		/// An event that signals when it is ok to take a line out of the buffer.
		/// </summary>
		private AutoResetEvent passNextEvent = new AutoResetEvent(true);

		/// <summary>
		/// An event that signals when the buffer has an item in it.
		/// </summary>
		private ManualResetEventSlim bufferHasItemEvent = new ManualResetEventSlim(false);

		/// <summary>
		/// Thread that waits for the closeEvent to be set. Used for Task.WhenAll compatiblility
		/// without creating multiple unmanaged tasks for waiting for the close signal.
		/// </summary>
		private Task closeWaitThread;

		/// <summary>
		/// An event that signals when this object should close.
		/// </summary>
		private ManualResetEventSlim closeEvent = new ManualResetEventSlim(false);

		public PipeCommunicatorBuffer(Action<string> onBufferOut)
		{
			_onBufferOut = onBufferOut;

			closeWaitThread = Task.Run(() =>
			{
				closeEvent.Wait();
			});

			bufferLoopThread = BufferLoop();
		}

		/// <summary>
		/// Function that should be passed to PipeCommunicator's constructor as the
		/// Action argument. Adds a line to this buffer when called.
		/// </summary>
		/// <param name="line"></param>
		public void OnReadLine(string line)
		{
			lock (buffer)
			{
				buffer.Enqueue(line);
				bufferHasItemEvent.Set();
			}
		}

		/// <summary>
		/// A loop that empties the buffer. Called internally.
		/// </summary>
		/// <returns></returns>
		private async Task BufferLoop()
		{
			while(true)
			{
				// Wait for a new line to arrive or for a singal to come in to close the buffer.
				Task awaitedTask = await Task.WhenAny(Task.Run(() => { bufferHasItemEvent.Wait(); }), closeWaitThread);

				// If we were sent a signal to close this object, stop the buffer loop thread.
				if (awaitedTask == closeWaitThread)
				{
					Cleanup();
					return;
				}

				// Otherwise, read from the buffer.
				bool canRead;
				string toProcess;
				lock(buffer)
				{
					canRead = buffer.TryDequeue(out toProcess);
					if (!canRead)
						bufferHasItemEvent.Reset();
				}
				while (canRead)
				{
					// Wait for permission to send the next line or for a signal to close the buffer.
					awaitedTask = await Task.WhenAny(Task.Run(() => { passNextEvent.WaitOne(); }), closeWaitThread);

					// If we were sent a signal to close this object, stop the buffer loop thread.
					if (awaitedTask == closeWaitThread)
					{
						Cleanup();
						return;
					}

					// Otherwise, send the string read from the buffer.
					_onBufferOut(toProcess);

					// Check that we can still read off lines. If not,
					// stop the loop.
					lock (buffer)
					{
						canRead = buffer.TryDequeue(out toProcess);
						if (!canRead) // If we've exited the loop, the buffer has no more items to process.
							bufferHasItemEvent.Reset(); // Reset the even to wait until the buffer gets a new item.
					}
				}
			}
		}

		/// <summary>
		/// Signals that the buffer can free the next item.
		/// Should be called externally after a line is processed.
		/// </summary>
		public void AcceptNext()
		{
			passNextEvent.Set();
		}

		/// <summary>
		/// Cleans up all lingering threads and event objects.
		/// Should be called externally.
		/// </summary>
		public void Close()
		{
			closeEvent.Set();
			if (!Task.WaitAll(new Task[] { closeWaitThread, bufferLoopThread }, 5000))
				Debug.LogWarning("Failed to close communication buffer.");
		}

		/// <summary>
		/// Called internally. Disposes all events.
		/// </summary>
		private void Cleanup()
		{
			bufferHasItemEvent.Dispose();
			passNextEvent.Dispose();
			closeEvent.Dispose();
		}
    }
}
