using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.Json;
using System;

namespace werignac.Communication
{
	/// <summary>
	/// The output of the JsonParser.
	/// </summary>
	/// <typeparam name="SerializableType">The type of objects the parser parses.</typeparam>
	public class JsonCommand<SerializableType>
	{
		/// <summary>
		/// The list of objects parsed by the parser.
		/// </summary>
		public IEnumerable<SerializableType> DeserializedObjects { get; private set; }
		/// <summary>
		/// Whether we've reached the end of the list of JSON objects.
		/// </summary>
		public bool IsEnd { get; private set; }

		public JsonCommand(IEnumerable<SerializableType> deserializedObjects, bool isEnd)
		{
			DeserializedObjects = deserializedObjects;
			IsEnd = isEnd;
		}
	}

	/// <summary>
	/// Parser that deserializes a series of Json objects.
	/// Parses until END is reached.
	/// Note that in a parser stack, this will consume non-parseable lines in the
	/// case of multiple lines that have {} brackets.
	/// 
	/// For example, if the following was not a Json object:
	/// {
	/// Some non-Json contents here.
	/// }
	/// 
	/// The first two lines would appear to be successfully consumed (as they appear to be part 
	/// of a Json object). But the last line would fail, and only the last line would move
	/// to parsers higher on the stack.
	/// </summary>
	/// <typeparam name="SerializableType">The type of object this parser tries to parse.</typeparam>
	public class JsonParser<SerializableType> : Parser<JsonCommand<SerializableType>>
	{
		/// <summary>
		/// The number of open brackets.
		/// </summary>
		private int openBracketCount = 0;
		
		/// <summary>
		/// State of an unfinished Json object string from a previous
		/// ParseLine call.
		/// </summary>
		private string pastJsonString = "";

		/// <summary>
		/// The line to look for when a list of json objects has finished.
		/// </summary>
		private const string JSON_LIST_TERMINATOR = "END";

		/// <summary>
		/// Parses the line as part of a JSON object string.
		/// Can be a line in the middle of a JSON object, or a line with multiple JSON objects.
		/// </summary>
		/// <param name="lineToParse">The line to parse.</param>
		/// <param name="command">The output json objects and whether the list has terminated.</param>
		/// <param name="errorMessage">An error message if parsing fails.</param>
		/// <returns></returns>
		protected override bool ParseLine(string lineToParse, out JsonCommand<SerializableType> command, out string errorMessage)
		{
			// Initializing Outs
			command = null;
			errorMessage = "";

			// If we've reached the end of the list, return so.
			if (lineToParse.Equals(JSON_LIST_TERMINATOR))
			{
				command = new JsonCommand<SerializableType>(new SerializableType[0], true);
				return true;
			}

			// Accumulation of JSON objects parsed in the current (and previous unfinished) lines.
			List<SerializableType> commandList = new List<SerializableType>();

			// Accumulation of a JSON object string.
			string JSONStr = pastJsonString;

			// The index to start looking for {} bracksts (in newline).
			int index = -1;
			int nextAppendStart = 0;

			// Parse the new line, counting brackets. If we detect that we've
			// reached the end of a JSON object, return the JSON object.
			do
			{
				index = lineToParse.IndexOfAny(new char[] { '{', '}' }, index + 1);
				if (index < 0)
					continue;

				char bracket = lineToParse[index];
				if (bracket == '{')
				{
					openBracketCount++;
				}
				else if (bracket == '}')
				{
					openBracketCount--;

					if (openBracketCount < 0)
						throw new Exception($"JSON string started with closed bracket \n{JSONStr}");
					// If bracketCount is zero, we completed a JSON object.
					if (openBracketCount == 0)
					{
						// Add the end of the JSON object.
						JSONStr += lineToParse.Substring(nextAppendStart, index - nextAppendStart + 1);
						JSONStr.Trim();

						bool deserializationSucceeded = false;

						try // Try to parse the JSON object.
						{
							commandList.Add(JsonSerializer.Deserialize<SerializableType>(JSONStr));
							deserializationSucceeded = true;
						} catch (JsonException jsonException)
						{
							errorMessage = $"Could not parse line: {jsonException}.";
						}
						
						// Start looking for the next JSON object.
						// Resets the state of the parser if parsing failed.
						JSONStr = "";
						nextAppendStart = index + 1;
						openBracketCount = 0;
						pastJsonString = "";

						if (!deserializationSucceeded)
							return false;
					}
				}
			} while (index >= 0);

			// Add the remaining contents to the JSONStr. Don't forget to trim.
			JSONStr += lineToParse.Substring(nextAppendStart);
			JSONStr.Trim(); // TODO: Only trim start, not end.
			pastJsonString = JSONStr;

			// If we deserialized some objects, create a command to pass these
			// objects.
			if (commandList.Count > 0)
				command = new JsonCommand<SerializableType>(commandList, false);

			return true;
		}
	}
}
