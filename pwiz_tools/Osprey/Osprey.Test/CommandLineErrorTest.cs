/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The command-line errors a user can reach without data, run through
    /// <see cref="Program.RunCommand"/> exactly as Osprey.exe runs them, in the manner of
    /// Skyline's CommandLineTest. Each case asserts what the user sees: one "Error:" line, a
    /// failure exit code that agrees with it, the flag, value or path the user typed, and no
    /// exception type or stack trace in place of a message.
    ///
    /// <para>Assertions use only text that is not translated - flag names, paths, the values
    /// typed - so they hold once the messages move to resources.</para>
    /// </summary>
    [TestClass]
    public class CommandLineErrorTest
    {
        // The startup-checked variables the cases below set.
        private static readonly string[] STARTUP_VARIABLES =
        {
            @"OSPREY_PASS2_QVALUE", @"OSPREY_STAGE7_STREAM", @"OSPREY_ALLOW_UNFIXED_RESIDENT"
        };

        private string _testDir;

        [TestInitialize]
        public void CreateTestDir()
        {
            _testDir = Path.Combine(Path.GetTempPath(), @"osprey-cli-" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void DeleteTestDir()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        [TestMethod]
        public void TestCommandLineErrors()
        {
            // No arguments: the usage, then the error.
            string output = RunCommandAndValidateError();
            StringAssert.Contains(output, OspreyCommandArgs.ARG_INPUT.ArgumentText);

            // --task with no name, and with a name that is not a task: both list every task.
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_TASK.ArgumentText);
            AssertNamesEveryTask(output);
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_TASK.ArgumentText, @"Bogus");
            StringAssert.Contains(output, @"Bogus");
            AssertNamesEveryTask(output);

            // Parse errors: the flag or value the user typed, not the exception that carried it.
            output = RunCommandAndValidateError(@"--bogus-flag");
            StringAssert.Contains(output, @"--bogus-flag");
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_THREADS.ArgumentText, @"bad");
            StringAssert.Contains(output, OspreyCommandArgs.ARG_THREADS.ArgumentText);
            string missingList = TestPath(@"missing-list.txt");
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_INPUT_LIST.ArgumentText, missingList);
            StringAssert.Contains(output, missingList);

            // A task missing an argument it requires names the task and the argument.
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_TASK.ArgumentText, PerFileScoringTask.TASK_NAME,
                OspreyCommandArgs.ARG_INPUT.ArgumentText, TestPath(@"a.mzML"));
            StringAssert.Contains(output, PerFileScoringTask.TASK_NAME);
            StringAssert.Contains(output, OspreyCommandArgs.ARG_LIBRARY.ArgumentText);

            // Files that do not exist: each error names the path.
            string library = CreateEmptyFile(@"library.blib");
            string missingInput = TestPath(@"missing.mzML");
            output = RunCommandAndValidateError(InputLibraryOutput(missingInput, library));
            StringAssert.Contains(output, missingInput);

            string input = CreateEmptyFile(@"run1.mzML");
            string missingLibrary = TestPath(@"missing.blib");
            output = RunCommandAndValidateError(InputLibraryOutput(input, missingLibrary));
            StringAssert.Contains(output, missingLibrary);

            string unwritableLog = Path.Combine(TestPath(@"no-such-dir"), @"run.log");
            output = RunCommandAndValidateError(InputLibraryOutput(input, library)
                .Concat(new[] { OspreyCommandArgs.ARG_LOG_FILE.ArgumentText, unwritableLog }).ToArray());
            StringAssert.Contains(output, unwritableLog);

            // --task ModelDiagnostics before any analysis exists: an error, not an empty report.
            var modelDiagnostics = InputLibraryOutput(input, library)
                .Concat(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, ModelDiagnosticsTask.TASK_NAME }).ToArray();
            output = RunCommandAndValidateError(modelDiagnostics);
            StringAssert.Contains(output, ModelDiagnosticsTask.TASK_NAME);

            // Environment variables checked at startup: the variable and the value set.
            output = RunCommandAndValidateError(Variable(@"OSPREY_PASS2_QVALUE", @"bogus"),
                InputLibraryOutput(input, library));
            StringAssert.Contains(output, @"OSPREY_PASS2_QVALUE");
            StringAssert.Contains(output, @"bogus");
            output = RunCommandAndValidateError(Variable(@"OSPREY_STAGE7_STREAM", @"1"),
                InputLibraryOutput(input, library));
            StringAssert.Contains(output, @"OSPREY_STAGE7_STREAM");
            // A retired allowance token is a warning, not an error: the run goes on, here to
            // the ModelDiagnostics error above, which stays the only error line.
            output = RunCommandAndValidateError(Variable(@"OSPREY_ALLOW_UNFIXED_RESIDENT", @"hpc-merge"),
                modelDiagnostics);
            StringAssert.Contains(output, @"hpc-merge");
            StringAssert.Contains(output, ModelDiagnosticsTask.TASK_NAME);
        }

        /// <summary>
        /// Runs a command line expected to fail before any work starts, and returns its output
        /// after checking what every such failure must share, as Skyline's
        /// <c>AbstractUnitTestEx.RunCommand</c> does for exit status.
        /// </summary>
        private static string RunCommandAndValidateError(params string[] args)
        {
            return RunCommandAndValidateError(new Dictionary<string, string>(), args);
        }

        /// <summary>
        /// The same, with the named environment variables overridden for this command line.
        /// </summary>
        private static string RunCommandAndValidateError(IReadOnlyDictionary<string, string> variables,
            params string[] args)
        {
            var buffer = new StringBuilder();
            var writer = new CommandStatusWriter(new StringWriter(buffer));
            int exitCode;
            // Every variable this test sets is blanked in the other cases, so a value exported in
            // the developer's shell cannot turn one case's expected error into another's.
            var isolated = STARTUP_VARIABLES.ToDictionary(name => name, name => string.Empty);
            foreach (var pair in variables)
                isolated[pair.Key] = pair.Value;
            using (OspreyEnvironment.OverrideVariables(isolated))
            {
                exitCode = RunCommandInProcess(args, writer);
            }
            string output = buffer.ToString();
            string message = string.Format(@"Command line: {0}{1}Output:{1}{2}",
                string.Join(@" ", args), Environment.NewLine, output);

            Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, exitCode, message);
            Assert.IsTrue(writer.IsErrorReported, message);
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            Assert.AreEqual(1, lines.Count(CommandStatusWriter.IsErrorLine),
                @"exactly one error line. " + message);
            // A usage error is a message, never an exception type and stack.
            Assert.IsFalse(output.Contains(typeof(Exception).Namespace + @"."), message);
            return output;
        }

        /// <summary>
        /// <see cref="Program.RunCommand"/> with the process-wide state it sets restored after,
        /// so one command line cannot leak its writer or directories into the next test.
        /// </summary>
        private static int RunCommandInProcess(string[] args, CommandStatusWriter writer)
        {
            var savedOut = OspreyOutput.Out;
            bool savedPerfStats = OspreyOutput.PerfStats;
            bool savedVerbose = OspreyOutput.Verbose;
            var savedDiagnosticsLog = OspreyDiagnosticsLog.Log;
            string savedOutputDir = ArtifactPaths.OutputDir;
            string savedCacheDir = ArtifactPaths.CacheDir;
            try
            {
                return Program.RunCommand(args, writer);
            }
            finally
            {
                OspreyOutput.Out = savedOut;
                OspreyOutput.PerfStats = savedPerfStats;
                OspreyOutput.Verbose = savedVerbose;
                OspreyDiagnosticsLog.Log = savedDiagnosticsLog;
                ArtifactPaths.OutputDir = savedOutputDir;
                ArtifactPaths.CacheDir = savedCacheDir;
            }
        }

        private static IReadOnlyDictionary<string, string> Variable(string name, string value)
        {
            return new Dictionary<string, string> { { name, value } };
        }

        private static void AssertNamesEveryTask(string output)
        {
            foreach (var taskName in OspreyCommandArgs.ARG_TASK.Values)
                StringAssert.Contains(output, taskName);
        }

        private string[] InputLibraryOutput(string input, string library)
        {
            return new[]
            {
                OspreyCommandArgs.ARG_INPUT.ArgumentText, input,
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, library,
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, TestPath(@"out.blib")
            };
        }

        private string TestPath(string fileName)
        {
            return Path.Combine(_testDir, fileName);
        }

        private string CreateEmptyFile(string fileName)
        {
            string path = TestPath(fileName);
            File.WriteAllText(path, string.Empty);
            return path;
        }
    }
}
