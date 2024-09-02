/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

namespace werignac.Communication
{
	/// <summary>
	/// Represents a parsed error from an external communicator.
	/// </summary>
	public class ParsedErrorWarning
	{
		public bool IsError { get; private set; }

		private string _message;

		public ParsedErrorWarning(string message, bool isError)
		{
			_message = message;
			IsError = isError;
		}

		public override string ToString()
		{
			return (IsError ? "Error" : "Warning") + " from extern: " + _message;
		}
	}

	/// <summary>
	/// Parser to detect errors and warnings.
	/// By default, is the first parser in the parser stack of the dispatcher.
	/// </summary>
    public class ErrorWarningParser : Parser<ParsedErrorWarning>
    {
        private static string regexStr = "^(?<is_error>Error|Warning):(?<message>.*)$";

		/// <summary>
		/// Parses an error or warning of the format:
		/// Error:<SOME_ERROR_CONTENT>
		/// Warning:<SOME_WARNING_CONTENT>
		/// </summary>
		/// <param name="lineToParse">The line to parse.</param>
		/// <param name="command">The output parsed error or warning.</param>
		/// <param name="errorMessage">The error message in the event that an error or warning was not parsed.</param>
		/// <returns>Whether parsing was successful.</returns>
		protected override bool ParseLine(string lineToParse, out ParsedErrorWarning command, out string errorMessage)
		{
			command = null;
			errorMessage = "";
			Regex regex = new Regex(regexStr, RegexOptions.IgnoreCase);
			Match match = regex.Match(lineToParse);
			
			if (! match.Success)
			{
				errorMessage = $"Could not parse line {lineToParse} as an error or warning.";
				return false;
			}

			command = new ParsedErrorWarning(match.Groups["message"].Value, match.Groups["is_error"].Value.StartsWith("Error"));
			return true;
		}
	}
}
