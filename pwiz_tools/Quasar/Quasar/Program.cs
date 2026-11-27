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

namespace pwiz.Quasar
{
    /// <summary>
    /// Command-line entry point for Quasar. Only --help and --version exist so far; the
    /// pipeline stages are added as they are built (see docs/00-spec.md).
    /// </summary>
    public static class Program
    {
        // Exit codes, as Skyline's and Osprey's command lines define them.
        internal const int EXIT_CODE_SUCCESS = 0;
        internal const int EXIT_CODE_FAILURE_TO_START = 1;

        internal const string ARG_HELP = @"--help";
        internal const string ARG_HELP_SHORT = @"-h";
        internal const string ARG_VERSION = @"--version";

        public static int Main(string[] args)
        {
            return Run(args, Console.Out, Console.Error);
        }

        /// <summary>Runs the command line with its output sent to the given writers.</summary>
        internal static int Run(string[] args, TextWriter output, TextWriter error)
        {
            if (args.Length == 0 || args[0] == ARG_HELP || args[0] == ARG_HELP_SHORT)
            {
                output.WriteLine(QuasarResources.Program_Usage);
                return EXIT_CODE_SUCCESS;
            }

            if (args[0] == ARG_VERSION)
            {
                output.WriteLine(QuasarResources.Program_Version__0_, Version);
                return EXIT_CODE_SUCCESS;
            }

            error.WriteLine(QuasarResources.Program_Unknown_argument__0_, args[0]);
            return EXIT_CODE_FAILURE_TO_START;
        }

        /// <summary>The build version, YEAR.ORDINAL.BRANCH.DOY.</summary>
        internal static string Version
        {
            get
            {
                var version = typeof(Program).Assembly.GetName().Version;
                return version == null ? string.Empty : version.ToString(4);
            }
        }
    }
}
