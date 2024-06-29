using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using werignac.Communication;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Text;

namespace werignac.Communication.Tests
{
	public class JsonNumber
	{
		public int Number { get; set; }
	}

    public class FullPipelineSuite
    {
		private static void SetSyncContext()
		{
			SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
		}

		private static void CreateCreatureCommunicationPipeline(
			out PipeCommunicator communicator,
			out PipeCommunicatorBuffer buffer,
			out ParserStack stack,
			out MultiplexedParserToSubParsers<JsonParser<JsonNumber>> multiplexParser,
			out NamedPipeServerStream pipe,
			out StreamWriter sw)
		{
			SetSyncContext();

			multiplexParser = new MultiplexedParserToSubParsers<JsonParser<JsonNumber>>();
			stack = new ParserStack();
			stack.AddParser(multiplexParser.MParser);

			FromBufferToStack(out buffer, stack);
			pipe = new NamedPipeServerStream("pipe", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
			Task connectTask = pipe.WaitForConnectionAsync();
			communicator = new PipeCommunicator("pipe", 1000, buffer.OnReadLine);
			connectTask.Wait();
			sw = new StreamWriter(pipe);
		}

		private static void FromBufferToStack(out PipeCommunicatorBuffer buffer, ParserStack stack)
		{
			buffer = new PipeCommunicatorBuffer(
			(string line) =>
			{
				if (!stack.TryParse(line, out string cumulativeErrorMessage))
				{
					Assert.Fail(cumulativeErrorMessage);
				}
			});
		}

		private async Task CountToNumber(JsonParser<JsonNumber> parser, int numberToCountTo, PipeCommunicatorBuffer buffer)
		{
			for (int i = 0; i < numberToCountTo; i++)
			{
				JsonCommand<JsonNumber> numberCommand = await parser.GetCommandAsync();
				foreach (JsonNumber number in numberCommand.DeserializedObjects)
				{
					Assert.AreEqual(i, number.Number);
				}
				buffer.AcceptNext();
			}
		}

        // A Test behaves as an ordinary method
        [Test]
        public async Task FullPipelinePresendAllCommands()
        {
			CreateCreatureCommunicationPipeline(
				out PipeCommunicator communicator,
				out PipeCommunicatorBuffer buffer,
				out ParserStack stack, out MultiplexedParserToSubParsers<JsonParser<JsonNumber>> multiplexParser,
				out NamedPipeServerStream pipe,
				out StreamWriter sw
				);

			int n = 5;
			int m = 100;

			Task[] tasks = new Task[n];

			for (int i = 0; i < n; i++)
			{
				tasks[i] = CountToNumber(multiplexParser.GetParserFromIndex(i), m, buffer);
			}

			StringBuilder stringBuilder = new StringBuilder();

			for (int i = 0; i < m; i++)
			{
				for (int e = 0; e < n; e++)
				{
					stringBuilder.Append(e.ToString() + " { \"Number\": " + i.ToString() + "}\n");
				}
			}

			sw.WriteLine(stringBuilder.ToString());
			sw.Flush();

			Task timeout = Task.Delay(10000);
			Task awaitedTask = await Task.WhenAny(Task.WhenAll(tasks), timeout);

			if (awaitedTask == timeout)
			{
				Assert.Fail("Waiting for async loop timed out.");
			}
        }

		private async Task ExpectNumber(JsonParser<JsonNumber> parser, int numberToExpect, PipeCommunicatorBuffer buffer)
		{
			JsonCommand<JsonNumber> numberCommand = await parser.GetCommandAsync();
			foreach (JsonNumber number in numberCommand.DeserializedObjects)
			{
				Assert.AreEqual(numberToExpect, number.Number);
			}
			buffer.AcceptNext();
		}

		[Test]
		public async Task FullPipelinePresendAllCommands_2()
		{
			CreateCreatureCommunicationPipeline(
				out PipeCommunicator communicator,
				out PipeCommunicatorBuffer buffer,
				out ParserStack stack, out MultiplexedParserToSubParsers<JsonParser<JsonNumber>> multiplexParser,
				out NamedPipeServerStream pipe,
				out StreamWriter sw
				);

			int n = 5;
			int m = 100;

			StringBuilder stringBuilder = new StringBuilder();

			for (int i = 0; i < m; i++)
			{
				for (int e = 0; e < n; e++)
				{
					stringBuilder.Append(e.ToString() + " { \"Number\":" + i.ToString() + "}\n");
				}
			}

			sw.WriteLine(stringBuilder.ToString());
			sw.Flush();

			for (int i = 0; i < m; i++)
			{
				Task[] tasks = new Task[n];

				for (int e = 0; e < n; e++)
				{
					tasks[e] = ExpectNumber(multiplexParser.GetParserFromIndex(e), i, buffer);
				}

				Task timeout = Task.Delay(1000);
				Task awaitedTask = await Task.WhenAny(Task.WhenAll(tasks), timeout);

				if (awaitedTask == timeout)
				{
					Assert.Fail("Waiting for async loop timed out.");
				}
			}
		}

	}
}
