/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

using pwiz.Common.SystemUtil;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Osprey's switches over the shared output seam. The seam itself - the process-wide
    /// writer every layer below the exe writes to, with the per-file scoped redirect the
    /// <see cref="MultiProgressReporter"/> uses - is <see cref="CommandOutput"/> in CommonUtil;
    /// <c>Program</c> points <see cref="CommandOutput.Out"/> at its single
    /// <see cref="CommandStatusWriter"/>. What stays here is what only Osprey's command line
    /// sets: the gates on its tagged machine lines and its verbose detail.
    /// </summary>
    public static class OspreyOutput
    {
        /// <summary>
        /// When false (default), the gated machine tags (<see cref="LogTag.COUNT"/>,
        /// <see cref="LogTag.TIMING"/>, <see cref="LogTag.BENCH"/>, <see cref="LogTag.STAGE_WALL"/>,
        /// <see cref="LogTag.PATH"/>, <see cref="LogTag.TRAIN"/>) are not written, so the human log
        /// stays clean. The --perf-stats flag sets this true so the perf tools and the regression
        /// gate get the tagged lines.
        /// </summary>
        public static bool PerfStats { get; set; }

        /// <summary>
        /// When true (--verbose), implementer-grade detail that the default log hides is
        /// emitted (e.g. per-fold Percolator iterations). Use <see cref="WriteVerbose"/> to
        /// gate such lines.
        /// </summary>
        public static bool Verbose { get; set; }

        /// <summary>
        /// Write a line to <see cref="CommandOutput.Out"/> only when <see cref="Verbose"/> is
        /// set. The inherited WriteLine(format, args) overload routes through the overridden
        /// WriteLine(string), so the stamps still apply.
        /// </summary>
        public static void WriteVerbose(string format, params object[] args)
        {
            if (Verbose)
                CommandOutput.Out.WriteLine(format, args);
        }
    }
}
