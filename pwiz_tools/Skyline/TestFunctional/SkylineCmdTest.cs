/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test the SkylineCmd.exe command-line executable by running it in a separate process.
    /// No idea why this was originally written as a functional test, but I changed it to a unit test
    /// when I fixed its handle leaking problem using ShellExecute = false. -brendan
    /// </summary>
    [TestClass]
    public class SkylineCmdTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSkylineCmd()
        {
            TestFilesZip = @"TestFunctional/SkylineCmdTest.zip";
            TestFilesDir = new TestFilesDir(TestContext, TestFilesZip);

            // failure to start
            {
                var process = Process.Start(GetProcessStartInfo("--invalidargument"));
                WaitForExit(process, Program.EXIT_CODE_FAILURE_TO_START);
            }
            // ran with errors
            {
                var process = Process.Start(GetProcessStartInfo("\"--in=" + TestFilesDir.GetTestPath("invalidpath.sky") + "\""));
                WaitForExit(process, Program.EXIT_CODE_RAN_WITH_ERRORS);
            }
            // success
            {
                var process = Process.Start(GetProcessStartInfo("\"--in=" + TestFilesDir.GetTestPath("SkylineCmdTest.sky") + "\""));
                WaitForExit(process, Program.EXIT_CODE_SUCCESS);
            }
        }

        private const int EXIT_WAIT_TIME = 20 * 1000;   // 20 seconds

        private static void WaitForExit(Process process, int exitCode)
        {
            Assert.IsNotNull(process);
            Assert.IsTrue(process.WaitForExit(EXIT_WAIT_TIME), string.Format("SkylineCmd not exited in {0} seconds", EXIT_WAIT_TIME/1000));
            Assert.AreEqual(exitCode, process.ExitCode);
        }

        private ProcessStartInfo GetProcessStartInfo(string arguments)
        {
            return new ProcessStartInfo(FindSkylineCmdExe())
            {
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            };
        }

        private static string FindSkylineCmdExe()
        {
            string assemblyLocationDirectoryName = Path.GetDirectoryName(typeof(SkylineCmdTest).Assembly.Location);
            Assert.IsNotNull(assemblyLocationDirectoryName);
            string path = Path.Combine(assemblyLocationDirectoryName, "SkylineCmd.exe");
            Assert.IsTrue(File.Exists(path), path + " not found");
            return path;
        }
    }
}
