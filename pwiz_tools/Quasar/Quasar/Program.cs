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
using System.Globalization;
using System.IO;

namespace pwiz.Quasar
{
    /// <summary>
    /// Command-line entry point for Quasar: Stages 1 to 4 of the spec, from a raw file or mzML
    /// to a pseudo-spectrum mzML.
    /// </summary>
    public static class Program
    {
        // Exit codes, as Skyline's and Osprey's command lines define them.
        internal const int EXIT_CODE_SUCCESS = 0;
        internal const int EXIT_CODE_FAILURE_TO_START = 1;
        internal const int EXIT_CODE_RAN_WITH_ERRORS = 2;

        internal const string ARG_HELP = @"--help";
        internal const string ARG_HELP_SHORT = @"-h";
        internal const string ARG_VERSION = @"--version";
        internal const string ARG_INPUT = @"--input";
        internal const string ARG_INPUT_SHORT = @"-i";
        internal const string ARG_OUTPUT = @"--output";
        internal const string ARG_OUTPUT_SHORT = @"-o";
        internal const string ARG_WINDOWS = @"--windows";
        internal const string ARG_THREADS = @"--threads";

        /// <summary>The output extension when no output is named.</summary>
        internal const string EXT_OUTPUT = @".quasar.mzML";

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

            var settings = new PipelineSettings { CommandLine = string.Join(@" ", args) };
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string value = i + 1 < args.Length ? args[i + 1] : null;
                if (arg == ARG_INPUT || arg == ARG_INPUT_SHORT)
                    settings.InputPath = value;
                else if (arg == ARG_OUTPUT || arg == ARG_OUTPUT_SHORT)
                    settings.OutputPath = value;
                else if (arg == ARG_WINDOWS && TryParseRange(value, out int first, out int last))
                {
                    settings.FirstWindow = first;
                    settings.LastWindow = last;
                }
                else if (arg == ARG_THREADS && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int threads))
                    settings.Threads = threads;
                else
                {
                    error.WriteLine(QuasarResources.Program_Unknown_argument__0_, arg);
                    return EXIT_CODE_FAILURE_TO_START;
                }

                i++;
            }

            if (string.IsNullOrEmpty(settings.InputPath))
            {
                error.WriteLine(QuasarResources.Program_Missing_argument__0_, ARG_INPUT);
                return EXIT_CODE_FAILURE_TO_START;
            }

            settings.OutputPath ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settings.InputPath)) ?? string.Empty,
                Path.GetFileNameWithoutExtension(settings.InputPath) + EXT_OUTPUT);
            try
            {
                new QuasarPipeline(settings, error).Run();
                return EXIT_CODE_SUCCESS;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is NotSupportedException ||
                                       ex is UnauthorizedAccessException)
            {
                error.WriteLine(QuasarResources.Program_Error__0_, ex.Message);
                return EXIT_CODE_RAN_WITH_ERRORS;
            }
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

        /// <summary>Parses "12" or "12-20" as an inclusive window index range.</summary>
        private static bool TryParseRange(string text, out int first, out int last)
        {
            first = last = 0;
            if (string.IsNullOrEmpty(text))
                return false;
            var parts = text.Split('-');
            if (parts.Length > 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out first))
                return false;
            last = first;
            return parts.Length == 1 || int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out last);
        }
    }
}
