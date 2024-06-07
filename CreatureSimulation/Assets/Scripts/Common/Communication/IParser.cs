using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace werignac.Communication
{
	public interface IParserQueueEntry
	{
		/// <summary>
		/// Tries to parse the provided line. If successfully parsed,
		/// the line is added to the Parser's queue, events are triggered,
		/// and the function returns true. Otherwise, false is returned.
		/// </summary>
		/// <param name="toParse">The line to parse.</param>
		/// <returns>Whether the line was parsed successfully.</returns>
		bool TryParse(string lineToParse, out string errorMessage);
	}

    public interface IParser<CommandType> : IParserQueueEntry
    {
		/// <summary>
		/// Gets the next command to process from a communicator.
		/// </summary>
		/// <param name="command">The command to process.</param>
		/// <returns>Whether there was a command to process.</returns>
		bool Next(out CommandType command);

		/// <summary>
		/// Async function that finishes once a new command has come in.
		/// </summary>
		Task<CommandType> GetCommandAsync();
    }
}
