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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Osprey's part of the <c>--parallel-files</c> progress story. The aggregator itself
    /// (<see cref="MultiProgressReporter"/>) and its seams on <see cref="CommandOutput"/> and
    /// <see cref="ProgressReporter"/> are CommonUtil's, tested in <c>CommonTest</c>; what is
    /// Osprey's is the gate on its tagged machine lines, which must hold inside a file's
    /// buffered block exactly as it holds in the unbuffered log.
    /// </summary>
    [TestClass]
    public class MultiProgressReporterTest
    {
        // The tags --perf-stats gates.
        private static readonly LogTag[] GATED_TAGS =
        {
            LogTag.COUNT, LogTag.BENCH, LogTag.TIMING, LogTag.STAGE_WALL, LogTag.PATH, LogTag.TRAIN
        };

        /// <summary>
        /// Gated machine tags must stay OUT of a file's buffered block (default PerfStats
        /// false), exactly as they stay out of the unbuffered log - otherwise a buffered run
        /// leaks [COUNT]/[BENCH]/etc. and looks like perf mode. An ungated tag and a plain
        /// narrative line survive, and reach the real writer when the file's block flushes.
        /// </summary>
        [TestMethod]
        public void TestMultiProgressReporterGatedTags()
        {
            var savedOut = CommandOutput.Out;
            bool savedPerfStats = OspreyOutput.PerfStats;
            try
            {
                var capture = new StringWriter();
                CommandOutput.Out = capture;
                OspreyOutput.PerfStats = false;
                var multi = new MultiProgressReporter(0.0);
                using (multi.BeginFile(0, 1))
                {
                    foreach (var tag in GATED_TAGS)
                        OspreyLog.Out.LogInfo(tag, @"suppressed");
                    OspreyLog.Out.LogInfo(LogTag.TASK, @"Demo:starting");
                    CommandOutput.Out.WriteLine(@"plain narrative survives");
                }
                string output = capture.ToString();
                StringAssert.Contains(output, @"plain narrative survives",
                    @"a non-stat narrative line must remain in the buffered block");
                StringAssert.Contains(output, LogTag.TASK.Format(@"Demo:starting"),
                    @"an ungated tag must be written whatever PerfStats says");
                foreach (var tag in GATED_TAGS)
                {
                    Assert.IsFalse(output.Contains(tag.ToString()),
                        tag + @" lines must not be written into the buffered block (default PerfStats)");
                }
            }
            finally
            {
                CommandOutput.Out = savedOut;
                OspreyOutput.PerfStats = savedPerfStats;
            }
        }
    }
}
