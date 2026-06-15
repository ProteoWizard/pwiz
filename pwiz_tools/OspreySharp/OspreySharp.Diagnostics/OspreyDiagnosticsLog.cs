/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Stateless cross-impl bisection diagnostics helpers, shared by the
    /// pipeline task layer and the file-backed sink. Kept separate from the
    /// exe-only <c>OspreyDiagnostics</c> bootstrap (which constructs the sink) so
    /// the task layer can call these without referencing the top-level exe
    /// project: the logging hook, the round-half-to-even f64 formatter, and the
    /// "*_ONLY" abort-after-dump exit used by bisection runs.
    /// </summary>
    public static class OspreyDiagnosticsLog
    {
        /// <summary>
        /// Delegate for logging. The pipeline hooks this to its LogInfo so dump
        /// messages flow through the standard logging channel.
        /// </summary>
        public static Action<string> LogAction { get; set; } = Console.WriteLine;

        /// <summary>
        /// Format a double with 10 decimal places using round-half-to-even
        /// (banker's) to match Rust's {:.10} formatter. .NET Framework's F10
        /// default is round-half-away-from-zero, which flips the last digit on
        /// exact .5 values. Pure helper, available regardless of whether
        /// diagnostics are enabled.
        /// </summary>
        public static string F10(double v)
        {
            return Math.Round(v, 10, MidpointRounding.ToEven)
                .ToString(@"F10", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Log the "aborting after dump" message for the given env var and call
        /// Environment.Exit(0). Used after *_ONLY dumps where the bisection diff
        /// is the only output we care about.
        /// </summary>
        public static void ExitAfterDump(string varName)
        {
            LogAction(string.Format(@"[BISECT] {0} set - aborting after dump", varName));
            Environment.Exit(0);
        }
    }
}
