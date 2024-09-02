/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.Communication
{
    public interface ICommunicator
    {
		/// <summary>
		/// Returns whether the communicator has a line to read.
		/// If there is a line, it is sent as an out parameter.
		/// </summary>
		/// <param name="line">The line to be read.</param>
		/// <returns>Whether there was a line to be read.</returns>
		bool Next(out string line);

		/// <summary>
		/// Writes a line and sends it to the other end to read.
		/// </summary>
		/// <param name="line">The line to write without newlines.</param>
		void Write(string line);

		/// <summary>
		/// Closes the communicator (i.e. calling pipe.Close()).
		/// </summary>
		void Close();
    }

	public static class CommunicatorExtentions
	{
		public static void WriteWarning(this ICommunicator communicator, string warningLine)
		{
			communicator.Write($"Warning: {warningLine}");
		}

		public static void WriteError(this ICommunicator communicator, string errorLine)
		{
			communicator.Write($"Error: {errorLine}");
		}

	}
}
