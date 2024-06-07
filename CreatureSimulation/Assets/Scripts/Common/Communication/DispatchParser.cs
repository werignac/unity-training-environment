using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Communication.Dispatch.Commands;

namespace werignac.Communication.Dispatch
{
	/// <summary>
	/// TODO: Inherit from Parser instead of IParser.
	/// </summary>
	public class DispatchParser : Parser<DispatchCommand>
	{
		private ICommunicator _communicator;

		public DispatchParser(ICommunicator communicator)
		{
			_communicator = communicator;
		}

		public bool Next(out DispatchCommand command)
		{
			command = null;

			// Keep going through _communicator.Next until we run
			// out of lines, or a valid command is found.
			while (command == null && _communicator.Next(out string line))
			{
				// Try to parse the line. If ParseLine is true, command will be set to
				// a non-null value.
				if (! ParseLine(line, out command, out string errorMessage))
				{
					// If ParseLine is false, there may be an error message to send back.
					if (errorMessage != null && errorMessage.Length > 0)
						_communicator.WriteError(errorMessage);
				}
			}

			return command != null;
		}

		protected override bool ParseLine(string toParse, out DispatchCommand command, out string errorMessage)
		{
			command = null;
			errorMessage = null;

			string[] words = toParse.Split(" ");

			if (words.Length == 0)
			{
				// Nothing to parse if the length of toParse is zero w/o whitespace.
				return false;
			}

			switch (words[0])
			{
				// run <experiment_name> - Open a scene with a maching name from the settings map of experiments.
				case "run":
					if (words.Length == 1)
					{
						errorMessage = $"Run command requires an experiment name.";
						return false;
					}

					string experimentName = words[1];

					command = new DispatchRunCommand(experimentName);
					return true;

				// set <setting_name> <value> - sets a value for the dispatcher
				case "set":
					command = new DispatchSetCommand(words[1], words[2]);
					return true;

				// quit - closes the application.
				case "quit":
					command = new DispatchQuitCommand();
					return true;

				default:
					errorMessage = $"Could not recognize command \"{toParse}\".";
					return false;
			}
		}
	}
}
