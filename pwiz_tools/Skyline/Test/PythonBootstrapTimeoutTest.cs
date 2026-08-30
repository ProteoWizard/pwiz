/*
 * Author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// The Python bootstrap steps reach the network, and a stalled package index leaves them
    /// waiting on a process that never exits. Both halves of the cap have to hold for that wait
    /// to end: the timeout has to fire, and it has to reach the caller. Swallowing it is not a
    /// smaller bug than not having it - the caller sees a step that neither finished nor failed
    /// and simply waits again, which is indistinguishable from no cap at all.
    /// </summary>
    [TestClass]
    public class PythonBootstrapTimeoutTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestPythonBootstrapTimeout()
        {
            var previousRunner = PythonInstaller.TestPipeSkylineProcessRunner;
            try
            {
                var runner = new HangingProcessRunner();
                PythonInstaller.TestPipeSkylineProcessRunner = runner;
                var timeout = TimeSpan.FromMilliseconds(500);

                // The cap fires rather than waiting on the process forever.
                var stopwatch = Stopwatch.StartNew();
                AssertEx.ThrowsException<PythonBootstrapTimeoutException>(() =>
                    PythonInstaller.RunProcessOrThrow(runner, @"cmd", @"cmd", false, true,
                        CancellationToken.None, timeout));
                stopwatch.Stop();
                AssertEx.IsTrue(runner.WasCancelled, @"the runner was never asked to stop");
                AssertEx.IsLessThan(stopwatch.Elapsed, TimeSpan.FromSeconds(30));

                // And it survives PipInstall, which swallows ordinary install failures. Before the
                // timeout was given its own type this was caught there and discarded, so a capped
                // pip install still ran to the caller's own limit.
                runner.Reset();
                var package = new PythonPackage { Name = PythonInstaller.VIRTUALENV, Version = null };
                var installer = new PythonInstaller(new[] { package }, TextWriter.Null, @"testenv");
                AssertEx.ThrowsException<PythonBootstrapTimeoutException>(() =>
                    installer.PipInstall(@"python.exe", new[] { package }, null, timeout));
            }
            finally
            {
                PythonInstaller.TestPipeSkylineProcessRunner = previousRunner;
            }
        }

        /// <summary>
        /// Stands in for a process that has stopped making progress: it returns only when the
        /// token is cancelled, the way the real runner behaves once it kills a hung child.
        /// </summary>
        private sealed class HangingProcessRunner : ISkylineProcessRunnerWrapper
        {
            public bool WasCancelled { get; private set; }

            public void Reset()
            {
                WasCancelled = false;
            }

            public int RunProcess(string arguments, bool runAsAdministrator, TextWriter writer,
                bool createNoWindow = false, CancellationToken cancellationToken = default)
            {
                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                WasCancelled = cancellationToken.IsCancellationRequested;
                return WasCancelled ? 1 : 0;
            }
        }
    }
}
