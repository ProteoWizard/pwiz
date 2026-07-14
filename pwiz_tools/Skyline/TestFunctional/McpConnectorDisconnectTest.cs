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

using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that a client which gives up on a long call and disconnects frees the pipe server, so the NEXT call
    /// gets through. The call here is a menu item that starts a long operation and shows a LongWaitDlg for it: the
    /// click blocks on the UI thread until the work is done, so the verb that posted it parks in the DialogWatcher
    /// wait for the whole operation. The JSON pipe server is single-instance and serves one request at a time, so
    /// that one parked verb would otherwise lock out every later request -- and the caller may well want those:
    /// Skyline is working and making progress, and the model may not want to wait it out, or may want to look at
    /// Skyline while it runs. The disconnect is the cancel signal: Skyline peeks the pipe, sees the client is gone,
    /// and abandons the waiting call. The operation keeps right on going; only the wait is abandoned.
    /// </summary>
    [TestClass]
    public class McpConnectorDisconnectTest : McpConnectorTest
    {
        private const string MENU_ITEM_TEXT = "Long Operation Test";

        // Set to let the long operation finish. Nothing else ends it, so every exit path must set it.
        private readonly ManualResetEventSlim _releaseWork = new ManualResetEventSlim(false);

        [TestMethod]
        public void TestMcpConnectorDisconnect()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();
            string pipeName = Program.MainJsonToolServer.PipeName;
            RunUI(AddLongOperationMenuItem);
            try
            {
                var stopwatch = Stopwatch.StartNew();

                // A client starts the long operation, watches Skyline work on it, then gives up and disconnects.
                // The request is written raw and the response never read, so closing the pipe really does close it:
                // a client blocked in a synchronous read cannot be disconnected by disposing the stream under it
                // (Windows holds the handle open until the pending ReadFile returns), which is why the MCP client
                // reads asynchronously and cancels the read before dropping the connection.
                using (var pipeA = new NamedPipeClientStream(@".", pipeName, PipeDirection.InOut))
                {
                    pipeA.Connect(5000);
                    pipeA.ReadMode = PipeTransmissionMode.Message;
                    var request = Encoding.UTF8.GetBytes(
                        @"{""jsonrpc"":""2.0"",""method"":""ClickMainMenuItem"",""params"":[""" + MENU_ITEM_TEXT +
                        @"""],""id"":1}");
                    pipeA.Write(request, 0, request.Length);
                    pipeA.Flush();
                    // The dialog is up: the click is now blocked on the UI thread inside the operation, and the
                    // server thread that posted it is parked in the wait for it.
                    WaitForOpenForm<LongWaitDlg>();
                }   // dispose == the client giving up, which is the cancel signal

                // The server must now be free, WHILE the operation it abandoned is still running. GetVersion needs no
                // UI thread, so it can be served even though the UI thread is still inside the operation -- proving
                // the server thread is no longer parked in the abandoned call. Without disconnect-cancellation this
                // connect-and-call would not be served until the operation finished (single instance, and the one
                // server thread still stuck inside the abandoned verb).
                using (var clientB = ConnectClient(pipeName))
                {
                    string version = clientB.GetVersion();
                    Assert.IsFalse(string.IsNullOrEmpty(version),
                        @"The pipe server did not serve a new client after the previous one disconnected.");
                }
                Assert.IsNotNull(FindOpenForm<LongWaitDlg>(),
                    @"The abandoned operation should have kept running -- only the wait for it is abandoned.");

                stopwatch.Stop();
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 30 * 1000,
                    string.Format(@"The server took {0} ms to recover from the client disconnect.",
                        stopwatch.ElapsedMilliseconds));
            }
            finally
            {
                _releaseWork.Set();
            }
            WaitForClosedForm<LongWaitDlg>();
        }

        /// <summary>
        /// Adds a main-menu item whose click runs a long operation behind a LongWaitDlg -- the way any long Skyline
        /// operation runs, with the UI thread inside the dialog's modal message loop (still pumping) until the work
        /// on its own thread is done. The work reports progress, so it reads as advancing rather than hung.
        /// </summary>
        private void AddLongOperationMenuItem()
        {
            var menuItem = new ToolStripMenuItem(MENU_ITEM_TEXT);
            menuItem.Click += (sender, args) =>
            {
                using var longWaitDlg = new LongWaitDlg();
                longWaitDlg.Message = MENU_ITEM_TEXT;
                longWaitDlg.PerformWork(SkylineWindow, 0, broker =>
                {
                    IProgressStatus status = new ProgressStatus(MENU_ITEM_TEXT);
                    for (int percent = 0; !_releaseWork.Wait(100) && !broker.IsCanceled; percent++)
                    {
                        broker.UpdateProgress(status = status.ChangePercentComplete(percent % 100));
                    }
                });
            };
            SkylineWindow.MainMenuStrip.Items.Add(menuItem);
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
