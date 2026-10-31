/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.ComponentModel;
using System.IO;
using AutoQC;
using AutoQC.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SharedBatchTest;

namespace AutoQCTest
{
    [TestClass]
    public class ProcessRunnerTest : AbstractUnitTest
    {
        private const int ERROR_FILE_NOT_FOUND = 2;
        // The exit code of a .NET launcher whose runtime files are missing
        private const int DOTNET_LIBRARY_MISSING = unchecked((int)0x80008083);

        /// <summary>
        /// Checks that <see cref="ConfigRunner.SkylineNotFound"/> is set only when the runner reports that the ClickOnce
        /// Skyline is not installed, the SkylineCmd.exe the configuration runs does not exist, or it exits with the
        /// code of a .NET launcher whose runtime files are missing. <see cref="ConfigRunner"/> then stops the
        /// configuration with an error, and a message that depends on the configuration's Skyline option.
        /// </summary>
        [TestMethod]
        public void TestSkylineNotFound()
        {
            var testDir = TestContext.GetTestPath(@"TestProcessRunner");
            var logger = new Logger(Path.Combine(testDir, "AutoQC.log"), "TestProcessRunner", true);
            try
            {
                ValidateRunnerOutput(logger);
                ValidateConfigRunner(logger, testDir);
                ValidateMessages(logger, testDir);
            }
            finally
            {
                logger.Close();
            }
        }

        private static void ValidateRunnerOutput(Logger logger)
        {
            // Output of SkylineRunner.exe and SkylineDailyRunner.exe when the ClickOnce Skyline is not installed
            AssertRun(logger, ProcStatus.Error, true, 1,
                "Error: Unable to find Skyline program at any of the following locations:",
                @"C:\Users\user\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\MacCoss Lab, UW\Skyline-daily.appref-ms");
            // SkylineCmd.exe left over from a Skyline uninstalled while it was running. The uninstaller removed the
            // .NET runtime files next to it.
            AssertRun(logger, ProcStatus.Error, true, DOTNET_LIBRARY_MISSING);

            // Skyline ran, or the process failed for a different reason. SkylineRunner prints "Could not connect to Skyline"
            // when Skyline is installed but does not start in time. ProcessRunner does not check for a missing
            // SkylineCmd.exe. ConfigRunner does, below.
            AssertRun(logger, ProcStatus.Success, false, 0, "File imported.");
            AssertRun(logger, ProcStatus.Error, false, 1, "Error: Could not connect to Skyline.");
            AssertRun(logger, ProcStatus.Error, false, 2, "Error: Failed importing the results file QC_1.raw.");
            AssertRun(logger, ProcStatus.Error, false, 1, "Error: The Skyline file C:\\QC\\QC.sky does not exist.");
            AssertRunThrows(logger, new Win32Exception(ERROR_FILE_NOT_FOUND));
            AssertRunThrows(logger, new InvalidOperationException());
        }

        private static void ValidateConfigRunner(Logger logger, string testDir)
        {
            // A configuration whose SkylineCmd.exe was uninstalled
            var missingConfig = MakeConfig(new SkylineSettings(SkylineType.Custom, null, Path.Combine(testDir, @"UninstalledSkyline")));
            var missingCmdPath = missingConfig.SkylineSettings.CmdPath;
            AssertEx.IsFalse(File.Exists(missingCmdPath), "Expected no SkylineCmd.exe at " + missingCmdPath);
            // cmd.exe stands in for the ClickOnce runner, which always exists next to AutoQC.
            var cmdExe = Path.Combine(Environment.SystemDirectory, @"cmd.exe");
            const string runnerNotFound = @"/c echo Error: Unable to find Skyline program at any of the following locations:& exit 1";

            using (var runner = new ConfigRunner(missingConfig, logger))
            {
                AssertConfigRun(runner, missingCmdPath, @"--in=QC.sky", true);
                // A later run that finds Skyline clears it
                AssertConfigRun(runner, cmdExe, @"/c exit 0", false);
                AssertConfigRun(runner, cmdExe, runnerNotFound, true);
                AssertConfigRun(runner, cmdExe, @"/c exit " + DOTNET_LIBRARY_MISSING, true);
                // An executable that exists and fails for a different reason
                AssertConfigRun(runner, cmdExe, @"/c exit 1", false);
            }
        }

        private static void ValidateMessages(Logger logger, string testDir)
        {
            // A restart finds the installed Skyline again for the Skyline and Skyline-daily options.
            var restartMessage = Resources.ConfigRunner_GetSkylineNotFoundMessage_Skyline_could_not_be_found__Stopping_configuration__Restart_AutoQC_;
            AssertMessage(logger, new SkylineSettings(SkylineType.Skyline, null), restartMessage);
            AssertMessage(logger, new SkylineSettings(SkylineType.SkylineDaily, null), restartMessage);

            // A restart builds the same path for a custom folder, so the message names the folder to edit.
            var customDir = Path.Combine(testDir, @"UninstalledSkyline");
            AssertMessage(logger, new SkylineSettings(SkylineType.Custom, null, customDir),
                string.Format(Resources.ConfigRunner_GetSkylineNotFoundMessage_Skyline_could_not_be_found_in__0___Stopping_configuration__Edit_the_configuration_,
                    customDir));
        }

        private static void AssertMessage(Logger logger, SkylineSettings skylineSettings, string expectedMessage)
        {
            using (var runner = new ConfigRunner(MakeConfig(skylineSettings), logger))
            {
                AssertEx.AreEqual(expectedMessage, runner.GetSkylineNotFoundMessage(),
                    "Unexpected message for the Skyline option " + skylineSettings.Type);
            }
        }

        private static void AssertConfigRun(ConfigRunner runner, string exePath, string args, bool expectedSkylineNotFound)
        {
            var status = runner.RunProcess(new ProcessInfo(exePath, args, args));
            AssertEx.AreEqual(expectedSkylineNotFound, runner.SkylineNotFound,
                string.Format("Unexpected SkylineNotFound running {0} {1} with status {2}", exePath, args, status));
        }

        private static AutoQcConfig MakeConfig(SkylineSettings skylineSettings)
        {
            return new AutoQcConfig("TestSkylineNotFound", false, DateTime.MinValue, DateTime.MinValue,
                TestUtils.GetTestMainSettings(), TestUtils.GetNoPublishPanoramaSettings(), skylineSettings);
        }

        private static void AssertRun(Logger logger, ProcStatus expectedStatus, bool expectedSkylineNotFound,
            int exitCode, params string[] output)
        {
            var runner = new TestProcessRunner(logger, exitCode, output, null);
            AssertEx.AreEqual(expectedStatus, runner.RunProcess(new ProcessInfo("SkylineRunner.exe", "--in=QC.sky", "--in=QC.sky")),
                "Unexpected process status for output: " + string.Join(Environment.NewLine, output));
            AssertEx.AreEqual(expectedSkylineNotFound, runner.SkylineNotFound,
                "Unexpected SkylineNotFound for output: " + string.Join(Environment.NewLine, output));
        }

        private static void AssertRunThrows(Logger logger, Exception exception)
        {
            var runner = new TestProcessRunner(logger, 0, new string[0], exception);
            AssertEx.AreEqual(ProcStatus.Error, runner.RunProcess(new ProcessInfo("SkylineRunner.exe", "--in=QC.sky", "--in=QC.sky")),
                "Expected an error status for exception " + exception);
            AssertEx.AreEqual(false, runner.SkylineNotFound,
                "Expected no SkylineNotFound for exception " + exception);
        }

        private class TestProcessRunner : AutoQC.ProcessRunner
        {
            private readonly int _exitCode;
            private readonly string[] _output;
            private readonly Exception _exception;

            public TestProcessRunner(Logger logger, int exitCode, string[] output, Exception exception) : base(logger)
            {
                _exitCode = exitCode;
                _output = output;
                _exception = exception;
            }

            protected override int CreateAndRunProcess()
            {
                if (_exception != null)
                    throw _exception;
                foreach (var line in _output)
                {
                    WriteToLog(line);
                }
                return _exitCode;
            }
        }
    }
}
