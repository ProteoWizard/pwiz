/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Times one real <c>.scores.parquet</c> read at one degree of read parallelism after
    /// another, and answers the question the whole pipelined-read design rests on: is the
    /// read DISK-bound or DECODE-bound?
    ///
    /// <para>The 82-file SEA-AD baseline read 131.6 GB of scores parquet during a 1,037 s
    /// phase, which is 127 MB/s - within noise of D:'s measured 122 MB/s sustained
    /// sequential rate. Two readings fit that number and they predict opposite outcomes:</para>
    ///
    /// <list type="bullet">
    /// <item>the phase was DISK-bound, in which case parallel decode buys nothing beyond the
    /// overlap; or</item>
    /// <item>the files were largely served from the OS page cache (the box has 511 GB of RAM
    /// and they had been written minutes earlier), in which case 127 MB/s is a CPU DECODE
    /// ceiling and parallel decode wins big.</item>
    /// </list>
    ///
    /// <para>One measurement separates them: read the same file TWICE back to back at degree
    /// 1. If the second pass is much faster, the first was disk-bound and the second is the
    /// decode ceiling. If the two are close, decode dominates at both passes. The degree
    /// sweep that follows then says how much of that ceiling parallel decode recovers.</para>
    ///
    /// <para><b>The first pass is only COLD if the file is not already in the OS page cache.</b>
    /// On a box that just wrote or read these files it will be warm and the ratio will read
    /// ~1.0 for the wrong reason. Either pick a file the box has not touched since boot, or
    /// drop the standby list first (RAMMap's "Empty Standby List", or any tool that flushes
    /// the file cache). The run prints both passes either way, so a ratio near 1.0 has to be
    /// interpreted against whether the cache was actually cold.</para>
    ///
    /// <para>Opt-in, because it needs a real multi-GB parquet and a QUIET box - a concurrent
    /// run makes every number here meaningless. Set <c>OSPREY_BENCH_SCORES</c> to one
    /// <c>.scores.parquet</c>, or <c>OSPREY_BENCH_RUNDIR</c> to a completed run directory
    /// (the largest scores parquet in it is used). <c>OSPREY_BENCH_READ_DEGREES</c> overrides
    /// the swept degrees (default <c>1,2,4,8,16</c>). Without either path variable this is
    /// inconclusive and says so rather than passing quietly.</para>
    ///
    /// <para>Invoke it with:</para>
    /// <code>
    /// $env:OSPREY_BENCH_SCORES = "D:\...\somefile.scores.parquet"
    /// pwsh -File ./ai/scripts/Osprey/Build-Osprey.ps1 -Configuration Release `
    ///     -SourceRoot &lt;worktree&gt; -RunTests -TestName ParquetRead_ColdWarmAndDegrees
    /// </code>
    ///
    /// <para>Release, not Debug - a Debug build's decode numbers are not the product's. The
    /// per-pass lines are written to the test's standard output.</para>
    /// </summary>
    [TestClass]
    public class ParquetReadPipelineBenchTest
    {
        private const string SCORES_VAR = @"OSPREY_BENCH_SCORES";
        private const string RUNDIR_VAR = @"OSPREY_BENCH_RUNDIR";
        private const string DEGREES_VAR = @"OSPREY_BENCH_READ_DEGREES";

        [TestMethod]
        public void ParquetRead_ColdWarmAndDegrees()
        {
            string path = ResolveScoresPath();
            if (path == null)
            {
                Assert.Inconclusive(
                    @"Set {0} to one .scores.parquet, or {1} to a completed run directory, " +
                    @"to run this bench - and run it on an otherwise QUIET box.",
                    SCORES_VAR, RUNDIR_VAR);
                return;
            }

            var degrees = ResolveDegrees();
            long bytes = new FileInfo(path).Length;
            Console.WriteLine(@"file    : {0}", path);
            Console.WriteLine(@"size    : {0:N2} GB", bytes / (1024.0 * 1024.0 * 1024.0));
            Console.WriteLine(@"degrees : {0}", string.Join(@",", degrees));
            Console.WriteLine(@"cores   : {0}", Environment.ProcessorCount);
            Console.WriteLine();

            // The critical pair. Both at degree 1, back to back, nothing else in between.
            var cold = TimeScalarRead(path, 1);
            var warm = TimeScalarRead(path, 1);
            Report(@"scalars  degree  1  (pass 1)", cold, bytes);
            Report(@"scalars  degree  1  (pass 2)", warm, bytes);
            double ratio = warm.Seconds > 0 ? cold.Seconds / warm.Seconds : 0;
            Console.WriteLine(@"pass1/pass2     : {0:N2}   {1}", ratio, ratio >= 1.5
                ? @"-> pass 1 was DISK-bound; pass 2 is the decode ceiling"
                : @"-> DECODE-bound at both passes (or pass 1 was already cached)");
            Console.WriteLine();

            // Decode-only sweep: ReadFdrStubScalars materializes nothing, so this is as close
            // to "decompress + decode five column chunks" as the public surface gets.
            foreach (int degree in degrees)
            {
                var result = TimeScalarRead(path, degree);
                Report(string.Format(CultureInfo.InvariantCulture, @"scalars  degree {0,2}", degree),
                    result, bytes);
                Assert.AreEqual(warm.Rows, result.Rows,
                    @"row count must not depend on the read degree");
            }
            Console.WriteLine();

            // The realistic first-pass shape: eleven columns and one FdrEntry per row, so the
            // serial materialization the pipeline deliberately did NOT parallelize is in the
            // measurement. The gap between the two sweeps is what that serial tail costs.
            foreach (int degree in degrees)
            {
                var result = TimeStubRead(path, degree);
                Report(string.Format(CultureInfo.InvariantCulture, @"stubs    degree {0,2}", degree),
                    result, bytes);
                Assert.AreEqual(warm.Rows, result.Rows,
                    @"stub count must not depend on the read degree");
            }
        }

        /// <summary>
        /// Decode-only pass: five column chunks per row group, no <c>FdrEntry</c> allocated,
        /// so the elapsed time is decompress + decode plus whatever the disk contributes.
        /// </summary>
        private static BenchResult TimeScalarRead(string path, int degree)
        {
            ParquetScoreCache.ReadThreadsForTest = degree;
            try
            {
                Settle();
                long rows = 0;
                var timer = Stopwatch.StartNew();
                ParquetScoreCache.ReadFdrStubScalars(path,
                    (entryId, charge, isDecoy, coelutionSum, modifiedSequence) => rows++);
                timer.Stop();
                return new BenchResult
                {
                    Seconds = timer.Elapsed.TotalSeconds,
                    Rows = rows,
                    WorkingSetGb = Environment.WorkingSet / (1024.0 * 1024.0 * 1024.0),
                };
            }
            finally
            {
                ParquetScoreCache.ReadThreadsForTest = null;
            }
        }

        /// <summary>
        /// The whole first-pass stub load: eleven column chunks decoded in the pipeline, one
        /// <see cref="pwiz.Osprey.Core.FdrEntry"/> per row materialized on the consuming thread.
        /// </summary>
        private static BenchResult TimeStubRead(string path, int degree)
        {
            ParquetScoreCache.ReadThreadsForTest = degree;
            try
            {
                Settle();
                var timer = Stopwatch.StartNew();
                var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(path);
                timer.Stop();
                var result = new BenchResult
                {
                    Seconds = timer.Elapsed.TotalSeconds,
                    Rows = stubs.Count,
                    WorkingSetGb = Environment.WorkingSet / (1024.0 * 1024.0 * 1024.0),
                };
                // Held until after the working set is sampled, then dropped so the next pass
                // does not start against this pass's garbage.
                stubs.Clear();
                return result;
            }
            finally
            {
                ParquetScoreCache.ReadThreadsForTest = null;
            }
        }

        private static void Report(string label, BenchResult result, long bytes)
        {
            double mbPerSecond = result.Seconds > 0
                ? bytes / (1024.0 * 1024.0) / result.Seconds
                : 0;
            Console.WriteLine(@"{0} : {1,8:N2} s   {2,8:N1} MB/s   {3,13:N0} rows   WS {4:N2} GB",
                label, result.Seconds, mbPerSecond, result.Rows, result.WorkingSetGb);
        }

        // Collect before every timed pass so one pass's garbage is not the next pass's GC
        // pause. Not a substitute for a quiet box; nothing here can compensate for one.
        private static void Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static string ResolveScoresPath()
        {
            string scores = Environment.GetEnvironmentVariable(SCORES_VAR);
            if (!string.IsNullOrEmpty(scores) && File.Exists(scores))
                return scores;

            string runDir = Environment.GetEnvironmentVariable(RUNDIR_VAR);
            if (string.IsNullOrEmpty(runDir) || !Directory.Exists(runDir))
                return null;
            // The largest file in the directory: the biggest one has the most row groups and
            // the least per-file fixed cost in the number.
            return Directory.GetFiles(runDir, @"*.scores.parquet")
                .OrderByDescending(p => new FileInfo(p).Length)
                .FirstOrDefault();
        }

        private static List<int> ResolveDegrees()
        {
            var degrees = new List<int>();
            string raw = Environment.GetEnvironmentVariable(DEGREES_VAR);
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string part in raw.Split(','))
                {
                    if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out int degree) && degree > 0)
                    {
                        degrees.Add(degree);
                    }
                }
            }
            if (degrees.Count == 0)
                degrees.AddRange(new[] { 1, 2, 4, 8, 16 });
            return degrees;
        }

        /// <summary>One timed pass over the file.</summary>
        private sealed class BenchResult
        {
            public double Seconds;
            public long Rows;
            public double WorkingSetGb;
        }
    }
}
