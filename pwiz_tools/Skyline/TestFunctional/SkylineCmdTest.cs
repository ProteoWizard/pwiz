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
    [TestClass]
    public class SkylineCmdTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSkylineCmd()
        {
            TestFilesZip = @"TestFunctional/SkylineCmdTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // failure to start
            {
                var process = Process.Start(GetProcessStartInfo("--invalidargument"));
                Assert.IsNotNull(process);
                Assert.IsTrue(process.WaitForExit(10000));
                Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, process.ExitCode);
            }
            // ran with errors
            {
                var process = Process.Start(GetProcessStartInfo("\"--in=" + TestFilesDir.GetTestPath("invalidpath.sky") + "\""));
                Assert.IsNotNull(process);
                Assert.IsTrue(process.WaitForExit(10000));
                Assert.AreEqual(Program.EXIT_CODE_RAN_WITH_ERRORS, process.ExitCode);
            }
            // success
            {
                var process = Process.Start(GetProcessStartInfo("\"--in=" + TestFilesDir.GetTestPath("SkylineCmdTest.sky") + "\""));
                Assert.IsNotNull(process);
                Assert.IsTrue(process.WaitForExit(10000));
                Assert.AreEqual(Program.EXIT_CODE_SUCCESS, process.ExitCode);
            }
        }

        private ProcessStartInfo GetProcessStartInfo(string arguments)
        {
            var processStartInfo = new ProcessStartInfo(FindSkylineCmdExe(), arguments);
            if (Program.SkylineOffscreen)
            {
                processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            return processStartInfo;
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
