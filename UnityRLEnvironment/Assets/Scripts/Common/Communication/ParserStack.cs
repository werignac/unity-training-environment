/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace werignac.Communication
{
	/// <summary>
	/// An object that when passed a line tries parsing it via a stack of parsers.
	/// Stops parsing at the first sucessful parser per line.
	/// 
	/// Thread-safe.
	/// </summary>
    public class ParserStack : IParserStackEntry
    {
		// TODO: Handle Overflow. Write Error and Warning Parsers. Connect to Communicator in Dispatcher.

		/// <summary>
		/// List of parsers in the ParserStack.
		/// When a line is sent to the communicator, the parser stack extracts the line
		/// and tries to have each parser parse it starting with the first parser.
		/// Once it reaches a parser that successfully parses the line, it stops iterating
		/// through the list.
		/// </summary>
		private List<IParserStackEntry> _parsers = new List<IParserStackEntry>();

		/// <summary>
		/// Adds the passed parser to the end of the parsers stack.
		/// </summary>
		/// <param name="toAdd"></param>
		public void AddParser(IParserStackEntry toAdd)
		{
			lock (_parsers)
			{
				_parsers.Add(toAdd);
			}
		}

		/// <summary>
		/// Adds a new parser of the provided type to the end of the parsers stack.
		/// </summary>
		/// <typeparam name="ParserType">The type of parser to add to the end of the parsers stack.</typeparam>
		public ParserType AddParser<ParserType>() where ParserType : IParserStackEntry, new()
		{
			ParserType newParser = new ParserType();
			lock (_parsers)
			{
				_parsers.Add(newParser);
			}
			return newParser;
		}


		/// <summary>
		/// Pops the passed parser from the top of the parser stack.
		/// Throws an error if the parser to pop does not match the parser at the top
		/// of the stack.
		/// </summary>
		/// <param name="toPop">The parser to pop.</param>
		public void PopParser(IParserStackEntry toPop)
		{
			lock (_parsers)
			{
				int lastIndex = _parsers.Count - 1;
				IParserStackEntry lastParser = _parsers[lastIndex];

				if (lastParser != toPop)
					throw new System.Exception($"Tried to pop parser from parser stack, but did not match.\nParser at end of stack: {lastParser}\nPassed parser: {toPop}");

				_parsers.RemoveAt(lastIndex);
			}
		}

		/// <summary>
		/// Pops the parser from the top of the parser stack if it is
		/// of the passed parser type. Throws an error if the parser at the top of the stack
		/// is not of the supplied type.
		/// </summary>
		/// <typeparam name="ParserType">Parser type to pop.</typeparam>
		/// <returns>The popped parser.</returns>
		public ParserType PopParser<ParserType>() where ParserType : IParserStackEntry
		{
			lock (_parsers)
			{
				int lastIndex = _parsers.Count - 1;
				IParserStackEntry lastParser = _parsers[lastIndex];

				if (!(lastParser is ParserType))
					throw new System.Exception($"Tried to pop parser from parser stack, but type did not match.\nParser at end of stack: {lastParser}\nPassed parser type: {typeof(ParserType)}");

				_parsers.RemoveAt(lastIndex);
				return (ParserType)lastParser;
			}
		}

		/// <summary>
		/// Tries to parse a line via the parsers in the parser stack.
		/// </summary>
		/// <param name="lineToParse">The line to parse</param>
		/// <param name="cumulativeErrorMessage">The error message describing how all the parsers failed on failure.</param>
		/// <returns>Whether parsing succeeded for a parser.</returns>
		public bool TryParse(string lineToParse, out string cumulativeErrorMessage)
		{
			cumulativeErrorMessage = "";
			int parserCount = 0;
			bool wasParsed = false;
			lock (_parsers)
			{
				parserCount = _parsers.Count;

				foreach (IParserStackEntry parser in _parsers)
				{
					if (parser.TryParse(lineToParse, out string errorMessage))
					{
						wasParsed = true;
						Debug.Log($"Parsed Line: {lineToParse} on parser {parser}.");
						break;
					}
					else
					{
						cumulativeErrorMessage += errorMessage + '\n';
					}
				}
			}

			if (! wasParsed)
			{
				cumulativeErrorMessage = $"Could not be parsed by any of the {parserCount} parsers.\nError Messages:\n{cumulativeErrorMessage}";
			}

			return wasParsed;
		}
	}
}
