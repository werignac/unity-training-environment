/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Communication.Dispatch.Commands;

namespace werignac.Communication.Dispatch
{
	/// <summary>
	/// A parser that reads dispatch commands.
	/// Dispatch commands use a similar format to cmd commands:
	/// run <EXPERIMENT_NAME>
	/// set <GLOBAL_VARIABLE_NAME> <GLOBAL_VARIABLE_VALUE>
	/// quit
	/// </summary>
	public class DispatchParser : Parser<DispatchCommand>
	{

		protected override bool ParseLine(string toParse, out DispatchCommand command, out string errorMessage)
		{
			command = null;
			errorMessage = null;

			string[] words = toParse.Split(" ");

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
