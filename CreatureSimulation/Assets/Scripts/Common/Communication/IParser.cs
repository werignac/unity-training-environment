using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.Events;
using System;

namespace werignac.Communication
{
	public interface IParserStackEntry
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

    public interface IParser<CommandType> : IParserStackEntry
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

		/// <summary>
		/// Set a callback for after a command is parsed.
		/// Is independent from Next() queue.
		/// </summary>
		/// <param name="callback">Callback. Cannot execute Unity functions.</param>
		void SetOnParsedCallback(Action<CommandType> callback);
    }
}
