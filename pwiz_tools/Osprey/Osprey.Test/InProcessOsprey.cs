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

using System.IO;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Runs an Osprey command line in the test process through <see cref="Program.RunCommand"/>,
    /// the way Osprey.exe runs it, with the process-wide state a run sets restored afterward so
    /// one command line cannot leak its writer or directories into the next.
    /// </summary>
    public static class InProcessOsprey
    {
        /// <summary>
        /// Run <paramref name="args"/>, writing to <paramref name="writer"/>, and return the exit code.
        /// </summary>
        public static int Run(string[] args, CommandStatusWriter writer)
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

        /// <summary>
        /// Run <paramref name="args"/> and return the exit code, with everything the run wrote in
        /// <paramref name="output"/>.
        /// </summary>
        public static int Run(string[] args, out string output)
        {
            var buffer = new StringBuilder();
            int exitCode;
            using (var writer = new CommandStatusWriter(new StringWriter(buffer)))
            {
                exitCode = Run(args, writer);
            }
            output = buffer.ToString();
            return exitCode;
        }
    }
}
