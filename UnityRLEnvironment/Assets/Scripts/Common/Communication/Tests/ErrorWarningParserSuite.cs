/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace werignac.Communication.Tests
{
    public class ErrorWarningParserSuite
    {
        [Test]
        public void WarningParserValidInput()
        {
			ErrorWarningParser parser = new ErrorWarningParser();

			string warningPrefix = "Warning:";
			string warningMessage = " Example Warning";

			bool result = parser.TryParse(warningPrefix + warningMessage, out string errorMessage);
			Assert.IsTrue(result, "Expected parser to parse valid warning message.");

			bool hasNext = parser.Next(out ParsedErrorWarning command);
			Assert.IsTrue(hasNext, "Expected parser to return true whilst it has a command queued.");
			Assert.IsFalse(command.IsError, "Expected warning to create a warning command instead of an error command.");
			Assert.AreEqual("Warning from extern: " + warningMessage, command.ToString());

			hasNext = parser.Next(out command);
			Assert.IsFalse(hasNext);
        }

		[Test]
		public void ErrorParserValidInput()
		{
			ErrorWarningParser parser = new ErrorWarningParser();

			string errorPrefix = "Error:";
			string errorMessage = " Example Error";

			bool result = parser.TryParse(errorPrefix + errorMessage, out string parsingErrorMessage);
			Assert.IsTrue(result, "Expected parser to parse valid error message.");

			bool hasNext = parser.Next(out ParsedErrorWarning command);
			Assert.IsTrue(hasNext, "Expected parser to return true whilst it has a command queued.");
			Assert.IsTrue(command.IsError, "Expected error to create a error command instead of a warning command.");
			Assert.AreEqual("Error from extern: " + errorMessage, command.ToString());

			hasNext = parser.Next(out command);
			Assert.IsFalse(hasNext);
		}

		[Test]
		public void InvalidErrorWarningParserInput()
		{
			ErrorWarningParser parser = new ErrorWarningParser();

			string invalidMessage = "This is not valid.";

			bool result = parser.TryParse(invalidMessage, out string parsingErrorMessage);
			Assert.IsFalse(result, "Expected parser to not parse invalid message.");

			bool hasNext = parser.Next(out ParsedErrorWarning command);
			Assert.IsFalse(hasNext);
		}
    }
}
