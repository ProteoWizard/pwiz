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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The command-line errors a user can reach without data, run through
    /// <see cref="Program.RunCommand"/> exactly as Osprey.exe runs them, in the manner of
    /// Skyline's CommandLineTest. Each case asserts what the user sees: one "Error:" line, a
    /// failure exit code that agrees with it, and the exact message, formatted from the same
    /// resource the code uses, with the flag, value or path the user typed - never an exception
    /// type or stack trace in place of a message.
    ///
    /// <para>Messages Osprey.Tasks owns and the developer-only OSPREY_* variable messages are
    /// asserted by the flag, value or variable they must name.</para>
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
            AssertErrorMessage(output, OspreyResources.Program_Run_No_arguments_were_given__see_the_usage_above_);

            // --task with no name, and with a name that is not a task: both list every task.
            string taskList = string.Join(@", ", OspreyCommandArgs.ARG_TASK.Values);
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_TASK.ArgumentText);
            AssertErrorMessage(output, string.Format(OspreyResources.Program_Run__0__requires_a_task_name___1___,
                OspreyCommandArgs.ARG_TASK.ArgumentText, taskList));
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_TASK.ArgumentText, @"Bogus");
            AssertErrorMessage(output, string.Format(OspreyResources.Program_ResolveTask__0___unknown_task___1____Valid_tasks___2__,
                OspreyCommandArgs.ARG_TASK.ArgumentText, @"Bogus", taskList));

            // Parse errors: the flag or value the user typed, not the exception that carried it.
            output = RunCommandAndValidateError(@"--bogus-flag");
            AssertErrorMessage(output, string.Format(
                OspreyResources.OspreyCommandArgs_TokenizeAndDispatch_Unknown_argument___0___Run_with__1__to_see_valid_options_,
                @"--bogus-flag", OspreyCommandArgs.ARG_HELP.ArgumentText));
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_THREADS.ArgumentText, @"bad");
            AssertErrorMessage(output, string.Format(OspreyResources.OspreyArgUsageProvider_ValueInvalidMessage_Invalid_value___0___for__1__,
                @"bad", OspreyCommandArgs.ARG_THREADS.ArgumentText));
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_OUTPUT.ArgumentText);
            AssertErrorMessage(output, string.Format(OspreyResources.OspreyArgUsageProvider_ValueMissingMessage__0__requires_a_value_,
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText));
            string missingList = TestPath(@"missing-list.txt");
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_INPUT_LIST.ArgumentText, missingList);
            AssertErrorMessage(output, string.Format(OspreyResources.OspreyCommandArgs_ReadInputList__0__file_not_found___1_,
                OspreyCommandArgs.ARG_INPUT_LIST.ArgumentText, missingList));

            // A task missing an argument it requires names the task and the argument. The
            // message is Osprey.Tasks' own (ValidateSelection).
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_TASK.ArgumentText, PerFileScoringTask.TASK_NAME,
                OspreyCommandArgs.ARG_INPUT.ArgumentText, TestPath(@"a.mzML"));
            StringAssert.Contains(output, PerFileScoringTask.TASK_NAME);
            StringAssert.Contains(output, OspreyCommandArgs.ARG_LIBRARY.ArgumentText);

            // Files that do not exist: each error names the path.
            string library = CreateEmptyFile(@"library.blib");
            string missingInput = TestPath(@"missing.mzML");
            output = RunCommandAndValidateError(InputLibraryOutput(missingInput, library));
            AssertErrorMessage(output, string.Format(
                OspreyResources.Program_Run_Input_file_not_found__and_no_spectra_cache_or_intermediate_file_exists_to_stand_in_for_it_,
                missingInput));

            string input = CreateEmptyFile(@"run1.mzML");
            string missingLibrary = TestPath(@"missing.blib");
            output = RunCommandAndValidateError(InputLibraryOutput(input, missingLibrary));
            AssertErrorMessage(output, string.Format(OspreyResources.Program_Run_Library_file_not_found___0_, missingLibrary));

            string unwritableLog = Path.Combine(TestPath(@"no-such-dir"), @"run.log");
            output = RunCommandAndValidateError(InputLibraryOutput(input, library)
                .Concat(new[] { OspreyCommandArgs.ARG_LOG_FILE.ArgumentText, unwritableLog }).ToArray());
            AssertErrorMessage(output, string.Format(OspreyResources.Program_Run_Failed_to_open_log_file__0____1_,
                unwritableLog, GetOpenErrorMessage(unwritableLog)));

            // --task ModelDiagnostics before any analysis exists: an error, not an empty report.
            var modelDiagnostics = InputLibraryOutput(input, library)
                .Concat(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, ModelDiagnosticsTask.TASK_NAME }).ToArray();
            string noFirstPass = string.Format(
                OspreyResources.Program_RunModelDiagnosticsTask__0___there_is_no_completed_first_pass_to_describe__no_first_pass_intermediate_file_for_,
                Program.ModelDiagnosticsTaskText, FirstPassFdrTask.TASK_NAME);
            output = RunCommandAndValidateError(modelDiagnostics);
            AssertErrorMessage(output, noFirstPass);

            // Environment variables checked at startup: the variable and the value set. These
            // messages are for a developer who set the variable, so they stay English.
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
            AssertErrorMessage(output, noFirstPass);

            // --culture: a name .NET does not know stops the run with .NET's own message (which
            // spans lines), and a command line run under another culture puts the caller's
            // culture back.
            const string badCulture = @"not a culture!";
            string cultureError = Assert.ThrowsException<CultureNotFoundException>(
                () => CultureInfo.GetCultureInfo(badCulture)).Message;
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_INTERNAL_CULTURE.ArgumentText, badCulture);
            StringAssert.Contains(output, Program.ErrorPrefix + @" " + cultureError);

            var callerCulture = CultureInfo.CurrentCulture;
            var callerUiCulture = CultureInfo.CurrentUICulture;
            string otherCulture = callerCulture.TwoLetterISOLanguageName == @"ja" ? @"fr-FR" : @"ja-JP";
            output = RunCommandAndValidateError(OspreyCommandArgs.ARG_INTERNAL_CULTURE.ArgumentText, otherCulture);
            Assert.AreSame(callerCulture, CultureInfo.CurrentCulture);
            Assert.AreSame(callerUiCulture, CultureInfo.CurrentUICulture);
            // The run wrote in the other culture, so its expected text is formatted there too.
            using (new CultureScope(CultureInfo.GetCultureInfo(otherCulture)))
            {
                AssertErrorMessage(output, string.Format(OspreyResources.Program_ValidateArgs_No_input_files_specified__Use__0_,
                    Program.USAGE_INPUT));
            }
            // --culture is applied to formatting and resource lookup for the whole run (the scope
            // RunCommand holds), and not only parsed.
            using (Program.CreateCultureScope(new[] { OspreyCommandArgs.ARG_INTERNAL_CULTURE.ArgumentText, otherCulture },
                       out string scopeError))
            {
                Assert.IsNull(scopeError);
                Assert.AreEqual(otherCulture, CultureInfo.CurrentCulture.Name);
                Assert.AreEqual(otherCulture, CultureInfo.CurrentUICulture.Name);
                Assert.AreEqual(otherCulture, CultureInfo.DefaultThreadCurrentCulture?.Name);
            }
            Assert.AreSame(callerCulture, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// The one error line in <paramref name="output"/> is exactly <paramref name="expected"/>
        /// after the error prefix, both in the current culture.
        /// </summary>
        private static void AssertErrorMessage(string output, string expected)
        {
            string errorLine = output.ReadLines().Single(CommandStatusWriter.IsErrorLine);
            Assert.AreEqual(Program.ErrorPrefix + @" " + expected, errorLine);
        }

        /// <summary>
        /// The message the runtime gives for opening <paramref name="path"/> to write, which the
        /// log-file error passes through.
        /// </summary>
        private static string GetOpenErrorMessage(string path)
        {
            return Assert.ThrowsException<DirectoryNotFoundException>(() => new StreamWriter(path).Dispose()).Message;
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
                exitCode = InProcessOsprey.Run(args, writer);
            }
            string output = buffer.ToString();
            string message = string.Format(@"Command line: {0}{1}Output:{1}{2}",
                string.Join(@" ", args), Environment.NewLine, output);

            Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, exitCode, message);
            Assert.IsTrue(writer.IsErrorReported, message);
            var lines = output.ReadLines();
            Assert.AreEqual(1, lines.Count(CommandStatusWriter.IsErrorLine),
                @"exactly one error line. " + message);
            // A usage error is a message, never an exception type and stack.
            Assert.IsFalse(output.Contains(typeof(Exception).Namespace + @"."), message);
            return output;
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
