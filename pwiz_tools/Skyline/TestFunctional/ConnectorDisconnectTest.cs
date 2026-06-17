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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that <see cref="JsonUiService.RunWithDialogWatch{T}"/> abandons a long-running call
    /// and returns when the connected client disconnects, so the single-instance pipe server is not
    /// left blocked on a call no one is waiting for.
    /// </summary>
    [TestClass]
    public class ConnectorDisconnectTest : AbstractFunctionalTest
    {
        private volatile bool _clientConnected;

        [TestMethod]
        public void TestConnectorDisconnect()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            _clientConnected = true;
            JsonUiService.SetClientConnectedCheck(() => _clientConnected);
            try
            {
                // Simulate the client disconnecting shortly after the call starts.
                ActionUtil.RunAsync(() => { Thread.Sleep(500); _clientConnected = false; }, @"disconnect");

                // The work would block for a long time; RunWithDialogWatch must return once the client
                // is gone, not wait for the work to finish.
                var stopwatch = Stopwatch.StartNew();
                JsonUiService.RunWithDialogWatch(() => { Thread.Sleep(30000); return true; });
                stopwatch.Stop();

                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000,
                    string.Format(@"RunWithDialogWatch did not return after the client disconnected ({0} ms).",
                        stopwatch.ElapsedMilliseconds));
            }
            finally
            {
                JsonUiService.SetClientConnectedCheck(null);
            }
        }
    }
}
