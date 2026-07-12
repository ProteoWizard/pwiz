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
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that a client which gives up on a long call and disconnects frees the pipe server, so the NEXT call
    /// gets through. This matters because the JSON pipe server is single-instance and serves one request at a time:
    /// a verb parked waiting on a long Skyline operation (a big document load riding its LongWaitDlg) would otherwise
    /// pin the server thread and lock out every later request -- including the very request that would cancel the
    /// dialog. The disconnect is the cancel signal: Skyline peeks the pipe, sees the client is gone, and abandons the
    /// waiting call. Whatever Skyline was doing keeps right on going; only the wait is abandoned.
    /// </summary>
    [TestClass]
    public class ConnectorDisconnectTest : McpTutorialTest
    {
        [TestMethod]
        public void TestConnectorDisconnect()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();
            string pipeName = Program.MainJsonToolServer.PipeName;
            // Resolve the form to read BEFORE blocking the UI thread (this read needs it too).
            string mainFormId = GetConnectorForm<SkylineWindow>();

            // Block the UI thread, the way a long document load does while its LongWaitDlg works: any connector verb
            // posted behind it now parks in the DialogWatcher wait loop and cannot finish.
            var releaseUi = new ManualResetEventSlim(false);
            SkylineWindow.BeginInvoke((Action) (() => releaseUi.Wait(TimeSpan.FromMinutes(2))));
            try
            {
                var stopwatch = Stopwatch.StartNew();

                // A client asks for something that cannot finish, waits a moment, then gives up and disconnects.
                // The request is written raw and the response never read, so closing the pipe really does close it:
                // a client blocked in a synchronous read cannot be disconnected by disposing the stream under it
                // (Windows holds the handle open until the pending ReadFile returns), which is why the MCP client
                // reads asynchronously and cancels the read before dropping the connection.
                using (var pipeA = new NamedPipeClientStream(@".", pipeName, PipeDirection.InOut))
                {
                    pipeA.Connect(5000);
                    pipeA.ReadMode = PipeTransmissionMode.Message;
                    var request = Encoding.UTF8.GetBytes(
                        @"{""jsonrpc"":""2.0"",""method"":""GetControls"",""params"":[""" + mainFormId + @"""],""id"":1}");
                    pipeA.Write(request, 0, request.Length);
                    pipeA.Flush();
                    Thread.Sleep(1000); // let the server thread get into the wait
                }   // dispose == the client giving up, which is the cancel signal

                // The server must now be free. GetVersion needs no UI thread, so it can be served even though the UI
                // thread is STILL blocked -- proving the server thread is no longer parked in the abandoned call.
                // Without disconnect-cancellation this connect-and-call would never be served (single instance, and
                // the one server thread still stuck inside the abandoned verb).
                using (var clientB = ConnectClient(pipeName))
                {
                    string version = clientB.GetVersion();
                    Assert.IsFalse(string.IsNullOrEmpty(version),
                        @"The pipe server did not serve a new client after the previous one disconnected.");
                }

                stopwatch.Stop();
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 30 * 1000,
                    string.Format(@"The server took {0} ms to recover from the client disconnect.",
                        stopwatch.ElapsedMilliseconds));
            }
            finally
            {
                releaseUi.Set();
            }
        }

        private static SkylineJsonToolClient ConnectClient(string pipeName)
        {
            var pipe = new NamedPipeClientStream(@".", pipeName, PipeDirection.InOut);
            pipe.Connect(5000);
            pipe.ReadMode = PipeTransmissionMode.Message;
            return new SkylineJsonToolClient(pipe);
        }
    }
}
