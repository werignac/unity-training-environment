using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using werignac.Communication.Dispatch;
using werignac.Communication.Dispatch.Commands;

namespace werignac.Communication.Tests
{
    public class DispatchParserSuite
    {
        [Test]
        public void DispatchParserInvalidInput()
        {
			DispatchParser parser = new DispatchParser();

			string invalidMessage = "This is not a valid message.";

			bool result = parser.TryParse(invalidMessage, out string parserErrorMessage);
			Assert.IsFalse(result);

			bool hasNext = parser.Next(out DispatchCommand command);
			Assert.IsFalse(hasNext);
        }

		[Test]
		public void DispatchParserValidRunInput()
		{
			DispatchParser parser = new DispatchParser();

			string runMessage = "run experiment";

			bool result = parser.TryParse(runMessage, out string parserErrorMessage);
			Assert.IsTrue(result);

			bool hasNext = parser.Next(out DispatchCommand command);
			Assert.IsTrue(hasNext);
			Assert.IsTrue(command is DispatchRunCommand);
			Assert.AreEqual((command as DispatchRunCommand).ExperimentToRun, "experiment");

			hasNext = parser.Next(out command);
			Assert.IsFalse(hasNext);
		}

		[Test]
		public void DispatchParserNoArgsRunInput()
		{
			DispatchParser parser = new DispatchParser();

			string runMessage = "run";

			bool result = parser.TryParse(runMessage, out string parserErrorMessage);
			Assert.IsFalse(result);
			Assert.AreEqual(parserErrorMessage, "Run command requires an experiment name.");

			bool hasNext = parser.Next(out DispatchCommand command);
			Assert.IsFalse(hasNext);
		}

		[Test]
		public void DispatchParserQuitInput()
		{
			DispatchParser parser = new DispatchParser();
			foreach (string quitMessage in new string[] { "quit", "quit ", "quit val", "quit val val"})
			{
				bool result = parser.TryParse(quitMessage, out string parserErrorMessage);
				Assert.IsTrue(result);

				bool hasNext = parser.Next(out DispatchCommand command);
				Assert.IsTrue(hasNext);
				Assert.IsTrue(command is DispatchQuitCommand);

				hasNext = parser.Next(out command);
				Assert.IsFalse(hasNext);
			}
		}

		[Test]
		public void DispatchParserValidSetInput()
		{
			DispatchParser parser = new DispatchParser();

			string setMessage = "set variable value";

			bool result = parser.TryParse(setMessage, out string parserErrorMessage);
			Assert.IsTrue(result);

			bool hasNext = parser.Next(out DispatchCommand command);
			Assert.IsTrue(hasNext);
			Assert.IsTrue(command is DispatchSetCommand);
			Assert.AreEqual("variable", (command as DispatchSetCommand).SettingName);
			Assert.AreEqual("value", (command as DispatchSetCommand).SettingValue);

			hasNext = parser.Next(out command);
			Assert.IsFalse(hasNext);
		}

		// TODO: Test # of args for set.
    }
}
