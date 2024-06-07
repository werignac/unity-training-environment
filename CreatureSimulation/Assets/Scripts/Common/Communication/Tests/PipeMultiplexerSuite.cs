using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Reflection;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;

namespace werignac.Communication.Tests
{
    public class PipeMultiplexerSuite
    {
		private readonly BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private readonly string TEST_PIPE_NAME = "TestPipe";

		private PipeMultiplexer CreateMultiplexer(string pipeName)
		{
			GameObject MultiplexerObject = new GameObject();
			MultiplexerObject.AddComponent<PipeMultiplexer>();

			PipeMultiplexer multiplexer = MonoBehaviour.Instantiate(MultiplexerObject).GetComponent<PipeMultiplexer>();
			FieldInfo pipeNameField = multiplexer.GetType().GetField("testPipeName", FieldFlags);
			FieldInfo usePipeNameField = multiplexer.GetType().GetField("useTestPipeName", FieldFlags);

			pipeNameField.SetValue(multiplexer, pipeName);
			usePipeNameField.SetValue(multiplexer, true);

			multiplexer.Initialize();
			return multiplexer;
		}

		private void CreateMultiplexerAndPipe(string pipeName, out PipeMultiplexer multiplexer, out NamedPipeServerStream pipe, out StreamWriter sw, out StreamReader sr)
		{
			pipe = new NamedPipeServerStream(pipeName,
				PipeDirection.InOut,
				NamedPipeServerStream.MaxAllowedServerInstances,
				PipeTransmissionMode.Message,
				PipeOptions.Asynchronous);
			sw = new StreamWriter(pipe);
			sr = new StreamReader(pipe);
			multiplexer = CreateMultiplexer(pipeName);
			pipe.WaitForConnection();
		}

		private void CleanupMultiplexerAndPipe(PipeMultiplexer multiplexer, NamedPipeServerStream pipe, StreamWriter sw, StreamReader sr)
		{
			multiplexer.CloseAllIDs();
			Task closeTask = multiplexer.ClosePipeAsync();

			Assert.AreEqual("END", sr.ReadLine());
			sw.WriteLine("QUIT");

			// Wait one second for close before timeout.
			closeTask.Wait(1000);

			sw.Close();
			sr.Close();
			pipe.Close();

			Object.Destroy(multiplexer.gameObject);
		}

        [Test]
        public void PipeMultiplexerExistsAndCleanup()
        {
			CreateMultiplexerAndPipe(TEST_PIPE_NAME, out PipeMultiplexer multiplexer,
				out NamedPipeServerStream pipe,
				out StreamWriter sw,
				out StreamReader sr);
			
			// Check multiplexer exists.
			Assert.IsNotNull(multiplexer);
			Assert.IsTrue(multiplexer.didAwake);
			Assert.IsFalse(multiplexer.didStart);

			// Check multiplexer has the expected values.
			FieldInfo pipeNameField = multiplexer.GetType().GetField("testPipeName", FieldFlags);
			FieldInfo usePipeNameField = multiplexer.GetType().GetField("useTestPipeName", FieldFlags);

			Assert.AreEqual(TEST_PIPE_NAME, pipeNameField.GetValue(multiplexer));
			Assert.IsTrue((bool) usePipeNameField.GetValue(multiplexer));

			// Cleanup
			CleanupMultiplexerAndPipe(multiplexer, pipe, sw, sr);
        }

        [Test]
        public void PipeMultiplexerMessages_IDPostConnect()
        {
			CreateMultiplexerAndPipe(TEST_PIPE_NAME, out PipeMultiplexer multiplexer,
				out NamedPipeServerStream pipe,
				out StreamWriter sw,
				out StreamReader sr);

			Task write = Task.Run(() =>
			   {
				   sw.WriteLine("1 Hello");
				   sw.WriteLine("1 World");
				   sw.Flush();
			   });

			Task read = Task.Run(async () =>
			{
				await Task.Delay(1000);

				Assert.AreEqual("Hello", multiplexer.ReadLine(1));
				Assert.AreEqual("World", multiplexer.ReadLine(1));
			});

			Task.WaitAll(write, read);

			CleanupMultiplexerAndPipe(multiplexer, pipe, sw, sr);
		}
    }
}
