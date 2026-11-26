/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using Parquet;
using pwiz.Common;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using Pwiz.Util.Misc;

namespace pwiz.Skyline
{
    /// <summary>
    /// The start-up that Skyline and SkylineCmd share, and the command-line run SkylineCmd makes.
    /// </summary>
    public static class CommandLineProgram
    {
        /// <summary>
        /// Runs the commands in <paramref name="args"/>, writing their output to
        /// <paramref name="textWriter"/>, and returns the exit code.
        /// </summary>
        public static int Run(string[] args, TextWriter textWriter)
        {
            // Before anything can load a shell extension that brings its own older msvcp140.
            AppLocalCrt.Pin();
            InitializeProcess();
            if (!IsPatchedParquetNet())
            {
                textWriter.WriteLine(SkylineResources.Program_Main_The_Parquet_dll_at__0__is_not_the_version__1__requires__Reinstall__1__,
                    typeof(ParquetRowGroupWriter).Assembly.Location, ProgramContext.Name);
            }
            using var commandLine = new CommandLine(new CommandStatusWriter(textWriter));
            return commandLine.Run(args, false);
        }

        /// <summary>
        /// Sets up what every run of Skyline needs, with or without its windows.
        /// </summary>
        public static void InitializeProcess()
        {
            if (string.IsNullOrEmpty(Settings.Default.InstallationId)) // Each instance to have GUID
                Settings.Default.InstallationId = Guid.NewGuid().ToString();

            CommonApplicationSettings.ProgramName = ProgramContext.Name;
            CommonApplicationSettings.ProgramNameAndVersion = Install.ProgramNameAndVersion;
            CommonActionUtil.ExceptionReporter = ProgramContext.ReportException;
            SkylineRemoteAccountServices.Initialize();
        }

        /// <summary>
        /// True if the loaded Parquet.dll is pwiz's patched build of Parquet.Net, identified by
        /// a method the NuGet release does not have. Both have the same assembly identity.
        /// </summary>
        public static bool IsPatchedParquetNet()
        {
            return typeof(ParquetRowGroupWriter).GetMethod(nameof(ParquetRowGroupWriter.PrepareColumnAsync)) != null;
        }
    }
}
