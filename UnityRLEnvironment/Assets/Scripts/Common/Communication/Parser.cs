/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using System;

namespace werignac.Communication
{
    public abstract class Parser<CommandType> : IParser<CommandType>
    {
		/// <summary>
		/// A queue of commands that have been parsed and need to be processed.
		/// </summary>
		private volatile Queue<CommandType> _parsedCommands = new Queue<CommandType>();

		/// <summary>
		/// Add System event that is fired when a new command is added to the parsedCommands queue.
		/// Starts unfired.
		/// TODO: Cleanup parsedCommandEvent.
		/// </summary>
		private ManualResetEvent parsedCommandEvent = new ManualResetEvent(false);

		/// <summary>
		/// Callback for when a command is parsed.
		/// 
		/// Cannot invoke Unity functions.
		/// </summary>
		private Action<CommandType> onParsedCallback = null;

		/// <summary>
		/// Function implemented by specific types of parsers. Takes a string
		/// and parses the line, outputs a command, and returns true. If parsing fails, returns false
		/// and provides and error message.
		/// 
		/// If parsing succeeds, but there's no new command that needs to be parsed, the function will
		/// return true but then send null as a command.
		/// </summary>
		/// <param name="lineToParse">The line to parse.</param>
		/// <param name="command">The command outputted by parsing.</param>
		/// <param name="errorMessage">An error message if parsing fails.</param>
		/// <returns>Whether parsing succeeded.</returns>
		protected abstract bool ParseLine(string lineToParse, out CommandType command, out string errorMessage);

		/// <summary>
		/// Checks if there is currently a command to be processed and returns it.
		/// Thread-safe. Does not block.
		/// </summary>
		/// <param name="command">The command that needs to be processed.</param>
		/// <returns>Whether there was a command that needed to be processed.</returns>
		public bool Next(out CommandType command)
		{
			return TryDequeueWithEvent(out command);
		}

		/// <summary>
		/// Waits for a new command to come in with async tasks.
		/// </summary>
		/// <returns>The command that came in.</returns>
		public async Task<CommandType> GetCommandAsync()
		{
			CommandType command;

			// If there is already a parsed command waiting to be processed,
			// return it.
			if (TryDequeueWithEvent(out command))
				return command;

			// Wait for signal that a command has been parsed.
			await Task.Run(()=> { parsedCommandEvent.WaitOne(); });

			// Return the new parsed command that just came in.
			TryDequeueWithEvent(out command);
			if (command == null)
				throw new System.Exception($"ParsedCommandEvent set when queue was empty on parser {this}.");
			return command;
		}

		private bool TryDequeueWithEvent(out CommandType command)
		{
			command = default(CommandType);

			lock (_parsedCommands)
			{
				if (_parsedCommands.Count == 0)
					return false;

				command = _parsedCommands.Dequeue();

				if (_parsedCommands.Count == 0)
					parsedCommandEvent.Reset();
			}

			return true;
		}

		/// <summary>
		/// Tries to parse the provided line. If it could be parsed, returns true and adds
		/// the parsed command to the _parsedCommands queue. Also sends a signal for async commands.
		/// Otherwise, returns false and outputs an error message.
		/// returns
		/// </summary>
		/// <param name="lineToParse">The line to parse.</param>
		/// <param name="errorMessage">The error message on failure.</param>
		/// <returns>Whether parsing succeeded.</returns>
		public bool TryParse(string lineToParse, out string errorMessage)
		{
			bool wasParsed = ParseLine(lineToParse, out CommandType command, out errorMessage);

			if (wasParsed && command != null)
			{
				lock(_parsedCommands)
				{
					_parsedCommands.Enqueue(command);
					//Signal that a command has been parsed.
					parsedCommandEvent.Set();
				}

				if (onParsedCallback != null)
					onParsedCallback(command);
			}

			return wasParsed;
		}

		public void SetOnParsedCallback(Action<CommandType> callback)
		{
			onParsedCallback = callback;
		}
	}
}
