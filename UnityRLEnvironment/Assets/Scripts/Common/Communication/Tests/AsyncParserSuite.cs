/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Threading.Tasks;
using System.Threading;
using werignac.Communication;
using werignac.Communication.Dispatch;
using werignac.Communication.Dispatch.Commands;

namespace werignac.Communication.Tests
{
    public class AsyncParserSuite
    {
		private static void SetSyncContext()
		{
			SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
		}

		private static async Task Timeout(Task toWait, int timeout)
		{
			Task finishFirst = await Task.WhenAny(toWait, Task.Delay(timeout));

			if (finishFirst != toWait)
				throw new System.Exception($"Timeout on task.");
		}

		private void SendLine(string toSend, IParserStackEntry parser)
		{
			bool result = parser.TryParse(toSend, out string errorMessage);
			if (! result)
			{
				throw new System.Exception($"Failed to parse line \"{toSend}\" for parser {parser}.");
			}
		}

        [Test]
        public async Task AsyncSingleParser()
        {
			SetSyncContext();
			
			ErrorWarningParser parser = new ErrorWarningParser();

			Task<ParsedErrorWarning> commandResult = parser.GetCommandAsync();

			// Send message.
			await Timeout(Task.Run(()=> { SendLine("Error: some error.", parser); }), 1000);

			// Get result.
			await Timeout(commandResult, 1000);

			Assert.IsTrue(commandResult.Result.IsError);
			Assert.AreEqual("Error from extern:  some error.", commandResult.Result.ToString());
        }

		[Test]
		public async Task AsyncSingleParserWithSync()
		{
			SetSyncContext();
			ErrorWarningParser parser = new ErrorWarningParser();

			await Timeout(Task.Run(() => { SendLine("Error: 1", parser);  }), 1000);

			parser.Next(out ParsedErrorWarning command);
			Assert.IsTrue(command.IsError);
			Assert.AreEqual("Error from extern:  1", command.ToString());

			Task<ParsedErrorWarning> asyncCommandResult = parser.GetCommandAsync();

			await Timeout(Task.Run(() => { SendLine("Error: 2", parser);  } ), 1000);

			await Timeout(asyncCommandResult, 1000);

			Assert.IsTrue(asyncCommandResult.Result.IsError);
			Assert.AreEqual("Error from extern:  2", asyncCommandResult.Result.ToString());
		}

		[Test]
		public async Task AsyncTwoParsers()
		{
			SetSyncContext();

			ErrorWarningParser parser1 = new ErrorWarningParser();
			ErrorWarningParser parser2 = new ErrorWarningParser();

			Task<ParsedErrorWarning> commandResult1 = parser1.GetCommandAsync();
			Task<ParsedErrorWarning> commandResult2 = parser2.GetCommandAsync();

			await Timeout(Task.Run(() => { SendLine("Error: 2.", parser2); } ), 1000);
			await Timeout(commandResult2, 1000);

			Assert.IsFalse(commandResult1.IsCompleted);
			Assert.IsTrue(commandResult2.Result.IsError);
			Assert.AreEqual("Error from extern:  2.", commandResult2.Result.ToString());

			await Timeout(Task.Run(() => { SendLine("Warning: 1.", parser1); } ), 1000);
			await Timeout(commandResult1, 1000);

			Assert.IsFalse(commandResult1.Result.IsError);
			Assert.AreEqual("Warning from extern:  1.", commandResult1.Result.ToString());
		}

		[Test]
		public async Task AsyncTenCommands()
		{
			SetSyncContext();

			ErrorWarningParser parser = new ErrorWarningParser();

			for (int i = 0; i < 10; i++)
			{
				string line = $"Error: {i}";

				Task<ParsedErrorWarning> commandResult = parser.GetCommandAsync();

				await Timeout(Task.Run(() => { SendLine(line, parser); }), 1000);
				await Timeout(commandResult, 1000);

				Assert.IsTrue(commandResult.Result.IsError);
				Assert.AreEqual($"Error from extern:  {i}", commandResult.Result.ToString());
			}
		}
	}
}
