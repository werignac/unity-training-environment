/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using werignac.Communication;
using werignac.Communication.Dispatch;
using werignac.Communication.Dispatch.Commands;

namespace werignac.Communication.Tests
{
    public class ParserStackSuite
    {
        [Test]
        public void ParserStackOneParserParsesCommand()
        {
			ParserStack stack = new ParserStack();
			ErrorWarningParser ewParser1 = stack.AddParser<ErrorWarningParser>();
			ErrorWarningParser ewParser2 = stack.AddParser<ErrorWarningParser>();

			bool result = stack.TryParse("Error: some error.", out string cumulativeErrorMessage);
			Assert.IsTrue(result);

			bool hasNext = ewParser1.Next(out ParsedErrorWarning command);
			Assert.IsTrue(hasNext);
			Assert.IsTrue(command.IsError);
			hasNext = ewParser1.Next(out command);
			Assert.IsFalse(hasNext);

			hasNext = ewParser2.Next(out command);
			Assert.IsFalse(hasNext);
        }

		[Test]
		public void ParserStackCommandFallthrough()
		{
			ParserStack stack = new ParserStack();
			ErrorWarningParser ewParser = stack.AddParser<ErrorWarningParser>();
			DispatchParser dispatchParser = stack.AddParser<DispatchParser>();

			bool result = stack.TryParse("run experiment", out string cumulativeErrorMessage);
			Assert.IsTrue(result);

			bool hasNext = ewParser.Next(out ParsedErrorWarning errorWarning);
			Assert.IsFalse(hasNext);

			hasNext = dispatchParser.Next(out DispatchCommand dispatchCommand);
			Assert.IsTrue(hasNext);
			Assert.IsTrue(dispatchCommand is DispatchRunCommand);
			Assert.AreEqual("experiment", (dispatchCommand as DispatchRunCommand).ExperimentToRun);
			hasNext = dispatchParser.Next(out dispatchCommand);
			Assert.IsFalse(hasNext);
		}

		[Test]
		public void ParserStackInvalidCommand()
		{
			ParserStack stack = new ParserStack();

			string invalidCommand = "Invalid command.";

			bool result = stack.TryParse(invalidCommand, out string cumulativeErrorMessage);
			Assert.IsFalse(result);

			ErrorWarningParser ewParser = stack.AddParser<ErrorWarningParser>();
			DispatchParser dispatchParser = stack.AddParser<DispatchParser>();

			result = stack.TryParse(invalidCommand, out cumulativeErrorMessage);
			Assert.IsFalse(result);

			bool hasNext = ewParser.Next(out ParsedErrorWarning errorWarning);
			Assert.IsFalse(hasNext);

			hasNext = dispatchParser.Next(out DispatchCommand command);
			Assert.IsFalse(hasNext);
		}
    }
}
