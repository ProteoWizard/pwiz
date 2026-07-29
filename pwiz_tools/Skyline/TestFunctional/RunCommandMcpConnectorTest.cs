/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies IJsonToolService.RunCommand runs the command under a LongWaitDlg -- so a long command (here a large
    /// FASTA import) shows progress and can be cancelled -- driven through DialogWatcher.CallFunction, so a client
    /// that gives up and disconnects frees the pipe while the command keeps running under its dialog. Then the model,
    /// reconnected, cancels it through the connector. Also checks the guard that refuses a command while a modal (the
    /// running command's own LongWaitDlg) blocks the main window, keeping commands one-at-a-time.
    /// </summary>
    [TestClass]
    public class RunCommandMcpConnectorTest : McpConnectorTest
    {
        [TestMethod]
        public void TestRunCommandMcpConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();
            string pipeName = Program.MainJsonToolServer.PipeName;

            string fastaPath = Path.Combine(Path.GetTempPath(), @"RunCommandMcpConnectorTest.fasta");
            WriteLargeFasta(fastaPath);
            try
            {
                // A file path travels through JSON-RPC; forward slashes avoid escaping and .NET opens them on Windows.
                string importArg = @"--import-fasta=" + fastaPath.Replace('\\', '/');

                // A client starts the (long) import, watches Skyline work on it behind the LongWaitDlg, then gives up
                // and disconnects. The request is written raw and its response never read, so closing the pipe really
                // closes it (a client blocked in a synchronous read cannot be disconnected by disposing the stream).
                using (var pipeA = new NamedPipeClientStream(@".", pipeName, PipeDirection.InOut))
                {
                    pipeA.Connect(5000);
                    pipeA.ReadMode = PipeTransmissionMode.Message;
                    // RunCommand takes a single string[] argument, hence the doubled brackets in params.
                    var request = Encoding.UTF8.GetBytes(
                        @"{""jsonrpc"":""2.0"",""method"":""RunCommand"",""params"":[[""" + importArg +
                        @"""]],""id"":1}");
                    pipeA.Write(request, 0, request.Length);
                    pipeA.Flush();
                    // RunCommand shows a LongWaitDlg for the import: proof the command reports progress rather than
                    // looking hung. The command runs on the dialog's background thread; the request that posted it is
                    // parked in the DialogWatcher wait.
                    WaitForOpenForm<LongWaitDlg>();
                }   // dispose == the client giving up: this frees the pipe but must NOT cancel the command

                // The server must now be free WHILE the import it abandoned keeps running. GetVersion needs no UI
                // thread, so it is served even though the UI thread is inside the import's modal loop -- proving the
                // pipe was freed. Without disconnect-frees-the-wait this would block until the import finished.
                var stopwatch = Stopwatch.StartNew();
                using (var clientB = SkylineJsonToolClient.Connect(pipeName))
                {
                    string version = clientB.GetVersion();
                    Assert.IsFalse(string.IsNullOrEmpty(version),
                        @"The pipe server did not serve a new client after the previous one disconnected.");
                }
                stopwatch.Stop();
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 30 * 1000,
                    string.Format(@"The server took {0} ms to recover from the client disconnect.",
                        stopwatch.ElapsedMilliseconds));

                string longWaitDlgId = WaitForMcpConnectorForm<LongWaitDlg>();
                Assert.IsNotNull(FindOpenForm<LongWaitDlg>(),
                    @"The abandoned command should have kept running -- only the wait for it is abandoned.");

                // A command is refused while a modal (here the running command's own LongWaitDlg) blocks the main
                // window, keeping commands one-at-a-time.
                AssertCommandRefusedWhileBusy(importArg);

                // The model, reconnected, finds the still-open progress dialog and cancels it through the connector.
                McpConnector.DismissWithCancelButton(longWaitDlgId);
                WaitForClosedForm<LongWaitDlg>();
            }
            finally
            {
                FileEx.SafeDelete(fastaPath, true);
            }
        }

        private void AssertCommandRefusedWhileBusy(string importArg)
        {
            try
            {
                McpConnector.RunCommand(new[] { importArg });
                Assert.Fail(@"RunCommand should be refused while a command is already running.");
            }
            catch (InvalidOperationException)
            {
                // Expected: the running command's LongWaitDlg blocks the main window, so the command is refused.
            }
        }

        private static void WriteLargeFasta(string path)
        {
            // Many short proteins with tryptic (K/R) sites: enough digestion that the import runs for seconds, so the
            // LongWaitDlg stays up long enough to disconnect, reconnect and cancel before it would finish on its own.
            const string residues = @"AAAAAAAAAAKGGGGGGGGGGRSSSSSSSSSSKDDDDDDDDDDR";
            var sb = new StringBuilder();
            for (int i = 0; i < 20000; i++)
            {
                sb.Append(@">PROT").Append(i).Append('\n');
                for (int j = 0; j < 6; j++)
                    sb.Append(residues).Append('\n');
            }
            File.WriteAllText(path, sb.ToString());
        }
    }
}
