/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// The command line's help, version and unknown-argument paths, with their exit codes.
    /// </summary>
    [TestClass]
    public class ProgramTest
    {
        [TestMethod]
        public void TestCommandLine()
        {
            // No arguments and --help both print the usage and succeed.
            foreach (var args in new[] { Array.Empty<string>(), new[] { Program.ARG_HELP }, new[] { Program.ARG_HELP_SHORT } })
            {
                var (exitCode, output, error) = Run(args);
                Assert.AreEqual(Program.EXIT_CODE_SUCCESS, exitCode);
                Assert.AreEqual(QuasarResources.Program_Usage + Environment.NewLine, output);
                Assert.AreEqual(string.Empty, error);
            }

            // --version prints the four-part build version.
            var version = Run(new[] { Program.ARG_VERSION });
            Assert.AreEqual(Program.EXIT_CODE_SUCCESS, version.ExitCode);
            Assert.AreEqual(4, Program.Version.Split('.').Length);
            Assert.AreEqual(string.Format(QuasarResources.Program_Version__0_, Program.Version) + Environment.NewLine,
                version.Output);

            // An unknown argument is reported on the error writer and fails to start.
            const string unknown = @"--not-an-argument";
            var bad = Run(new[] { unknown });
            Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, bad.ExitCode);
            Assert.AreEqual(string.Empty, bad.Output);
            Assert.AreEqual(string.Format(QuasarResources.Program_Unknown_argument__0_, unknown) + Environment.NewLine,
                bad.Error);
        }

        private static (int ExitCode, string Output, string Error) Run(string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            int exitCode = Program.Run(args, output, error);
            return (exitCode, output.ToString(), error.ToString());
        }
    }
}
