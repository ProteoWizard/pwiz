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

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Unit tests for the <c>--parallel-files</c> progress aggregator
    /// (<see cref="MultiProgressReporter"/>) and its seams on
    /// <see cref="OspreyOutput"/> + <see cref="ProgressReporter"/>:
    /// the throttled <c>[i] p%</c> aggregate line, the equal-weight segment
    /// model that composes one file's percent, per-file narrative buffering
    /// flushed contiguously on completion, and the inert-off-the-parallel-path
    /// behavior (a ProgressReporter with no active scope still prints inline).
    ///
    /// Drives the API synchronously through the (internals-visible) handle
    /// surface rather than a real <c>Parallel.For</c>, so the assertions are
    /// deterministic; the cross-thread ExecutionContext flow is exercised by the
    /// end-to-end Stellar/Astral runs. Interval 0 disables the render throttle so
    /// every advance is observable.
    /// </summary>
    [TestClass]
    public class MultiProgressReporterTest
    {
        [TestMethod]
        public void TestMultiProgressReporter()
        {
            var savedOut = OspreyOutput.Out;
            try
            {
                ValidateSegmentCompositeAndRouting();
                ValidateAggregateLineAndBuffering();
                ValidateInertWhenNoScope();
            }
            finally
            {
                OspreyOutput.Out = savedOut;
            }
        }

        // One file, four equal-weight segments: each segment's 0..100 maps into its
        // 25% slice, and a ProgressReporter constructed inside the scope routes its
        // percent to the active segment (no inline "<pct>%" line) while its heading
        // still buffers into the file's block.
        private static void ValidateSegmentCompositeAndRouting()
        {
            var capture = new StringWriter();
            OspreyOutput.Out = capture;
            OspreyOutput.PerfStats = false;   // default: machine stat lines suppressed

            var multi = new MultiProgressReporter(0.0);
            var percents = new List<int>();
            string fileBlock;
            using (var file = multi.BeginFile(0, @"fileA", 4))
            {
                // Segment 1/4 (read): a reporter inside the scope feeds the slice.
                file.BeginSegment();
                using (var reader = new ProgressReporter(@"Reading fileA", 100))
                {
                    reader.Report(50);                 // (0*100 + 50) / 4 = 12
                    percents.Add(file.Slot.Percent);
                }                                       // Dispose banks segment at 100 -> 25
                percents.Add(file.Slot.Percent);

                // Segment 2/4 (calibrate): no reporter -- BeginSegment alone carries
                // the file to the slice boundary (50%) when segment 3 opens.
                file.BeginSegment();
                file.BeginSegment();                    // open segment 3 -> floor at 50
                percents.Add(file.Slot.Percent);
                file.CurrentSegmentSink.Report(100);    // (2*100 + 100) / 4 = 75
                percents.Add(file.Slot.Percent);

                // Machine stat lines must be filtered OUT of the buffered block (default
                // PerfStats=false) exactly as the unbuffered Out filters them -- otherwise a
                // buffered run leaks [COUNT]/[BENCH]/etc. and looks like perf mode. A plain
                // narrative line survives.
                OspreyOutput.Out.WriteLine(@"[COUNT] suppressed count line");
                OspreyOutput.Out.WriteLine(@"[BENCH] suppressed bench line");
                OspreyOutput.Out.WriteLine(@"[TIMING] suppressed timing line");
                OspreyOutput.Out.WriteLine(@"plain narrative survives");

                // Read the file's OWN buffer (not the full capture, which also holds
                // the aggregate "[i] p%" lines) to check what the narrative block got.
                fileBlock = file.BufferContents;
            }

            Assert.AreEqual(12, percents[0], @"segment 1 at 50% should compose to 12%");
            Assert.AreEqual(25, percents[1], @"segment 1 disposed should bank the slice at 25%");
            Assert.AreEqual(50, percents[2], @"two segments behind should floor the file at 50%");
            Assert.AreEqual(75, percents[3], @"segment 3 at 100% should compose to 75%");

            StringAssert.Contains(fileBlock, @"Reading fileA...",
                @"the reporter heading must buffer into the file block");
            Assert.IsFalse(fileBlock.Contains(@"%"),
                @"inside a scope the reporter must route its percent, not print a '%' line into the block");
            StringAssert.Contains(fileBlock, @"plain narrative survives",
                @"a non-stat narrative line must remain in the buffered block");
            Assert.IsFalse(fileBlock.Contains(@"[COUNT]"),
                @"[COUNT] stat lines must be filtered out of the buffered block (default PerfStats)");
            Assert.IsFalse(fileBlock.Contains(@"[BENCH]"),
                @"[BENCH] stat lines must be filtered out of the buffered block (default PerfStats)");
            Assert.IsFalse(fileBlock.Contains(@"[TIMING]"),
                @"[TIMING] stat lines must be filtered out of the buffered block (default PerfStats)");
        }

        // Two concurrent files: the aggregate line shows both active slots, each
        // file's narrative is buffered and flushed as one contiguous block on
        // completion (LIFO here to respect the synthetic single-thread scope stack),
        // and a completed file drops off the line.
        private static void ValidateAggregateLineAndBuffering()
        {
            var capture = new StringWriter();
            OspreyOutput.Out = capture;

            var multi = new MultiProgressReporter(0.0);
            var fileA = multi.BeginFile(0, @"fileA", 4);
            OspreyOutput.Out.WriteLine(@"AAA narrative line");   // -> fileA buffer
            var fileB = multi.BeginFile(1, @"fileB", 4);
            OspreyOutput.Out.WriteLine(@"BBB narrative line");   // -> fileB buffer

            fileA.BeginSegment();
            fileA.CurrentSegmentSink.Report(40);                 // fileA -> 10%
            fileB.BeginSegment();
            fileB.CurrentSegmentSink.Report(80);                 // fileB -> 20%

            fileB.Dispose();                                     // flush B, drop slot 2
            fileA.Dispose();                                     // flush A, drop slot 1

            string output = capture.ToString();
            StringAssert.Contains(output, @"[1] 10%  [2] 20%",
                @"the aggregate line must show both active files with 0-based index shown as [i+1]");

            // Each file's narrative must appear as one contiguous block (its single
            // line here), not interleaved into the other file's block.
            StringAssert.Contains(output, @"AAA narrative line");
            StringAssert.Contains(output, @"BBB narrative line");
            // fileB completed first (LIFO), so its block flushes before fileA's.
            Assert.IsTrue(
                output.IndexOf(@"BBB narrative line", StringComparison.Ordinal)
                    < output.IndexOf(@"AAA narrative line", StringComparison.Ordinal),
                @"the first-completed file's block should flush first");
        }

        // Off the parallel path (no per-file scope), a ProgressReporter prints its
        // percent inline exactly as before -- the seam is inert.
        private static void ValidateInertWhenNoScope()
        {
            var capture = new StringWriter();
            OspreyOutput.Out = capture;

            Assert.IsNull(MultiProgressReporter.Current,
                @"no per-file scope should be active outside BeginFile");
            using (var progress = new ProgressReporter(@"Standalone", 100, string.Empty, 0.0))
                progress.Report(50);

            string output = capture.ToString();
            StringAssert.Contains(output, @"Standalone...");
            StringAssert.Contains(output, @"50%");
        }
    }
}
