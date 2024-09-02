/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

namespace werignac.Communication
{
	public class MultiplexedCommand
	{
		public int Index { get; set; }
		public string Command { get; set; }

		public MultiplexedCommand(int index, string command)
		{
			Index = index;
			Command = command;
		}
	}

	/// <summary>
	/// Parser that parses a line with an index and then a subparser-specific line.
	/// For example:
	/// 1 run <EXPERIMENT_NAME>
	/// </summary>
	public class MultiplexerParser : Parser<MultiplexedCommand>
	{
		private const string regexStr = @"^(?<index>\d+) (?<line>.*)$";

		protected override bool ParseLine(string lineToParse, out MultiplexedCommand command, out string errorMessage)
		{
			command = null;
			errorMessage = "";

			Regex regex = new Regex(regexStr);
			Match match = regex.Match(lineToParse);

			if (!match.Success)
			{
				errorMessage = $"Could not parse line \"{lineToParse}\" as a multiplexed line. Expected line structure {regexStr}.";
				return false;
			}

			command = new MultiplexedCommand(int.Parse(match.Groups["index"].Value), match.Groups["line"].Value);
			return true;
		}
	}

	public class MultiplexedParserToSubParsers<SubParser_Type> where SubParser_Type : IParserStackEntry, new()
	{
		public MultiplexerParser MParser { get; private set; } = new MultiplexerParser();
		private Dictionary<int, SubParser_Type> multiplexingDictionary = new Dictionary<int, SubParser_Type>();
		public MultiplexedParserToSubParsers()
		{
			MParser.SetOnParsedCallback(OnCommandParsed);
		}

		private void OnCommandParsed(MultiplexedCommand command)
		{
			SubParser_Type subparser = GetParserFromIndex(command.Index);

			if (!subparser.TryParse(command.Command, out string errorMessage))
			{
				// TODO: Send a message to the dispatcher.
				Debug.LogError($"Could not parse multiplexed line:\n\t{errorMessage.Replace("\n", "\n\t")}");
			}
		}

		public void RemoveParser(int index)
		{
			multiplexingDictionary.Remove(index);
		}

		/// <summary>
		/// Creates a parser if one does not exist.
		/// Otherwise, gets the parser already mapped to the passed index.
		/// </summary>
		/// <returns></returns>
		public SubParser_Type GetParserFromIndex(int index)
		{
			SubParser_Type subparser;

			lock (multiplexingDictionary)
			{
				if (!multiplexingDictionary.TryGetValue(index, out subparser))
				{
					subparser = new SubParser_Type();
					multiplexingDictionary.Add(index, subparser);
				}
			}

			return subparser;
		}
	}
}
