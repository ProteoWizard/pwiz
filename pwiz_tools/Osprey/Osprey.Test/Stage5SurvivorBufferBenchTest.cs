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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Reproduces the Stage 5 survivor-buffer memory peak against a COMPLETED run directory,
    /// without re-running the first pass.
    ///
    /// <para>The peak that killed a 446-file CHS join is not the 1.34 B streamed entries - those
    /// hold 5-21 GB - it is <c>FirstPassFdrTask.ReloadFirstPassSurvivors</c> collecting every
    /// file's survivors into one all-files buffer: 289 M entries at ~274 B each, ~100 GB.
    /// <c>OSPREY_STAGE6_STREAM_SURVIVORS</c> already releases that buffer after Stage 5 planning
    /// (issue #4526), so Stage 6 no longer HOLDS it - but Stage 5 still BUILDS it.</para>
    ///
    /// <para>Reproducing that through the pipeline costs the 3h45m first pass. It does not have
    /// to: every input the reload consumes is already on disk in any completed run directory -
    /// the Stage 4 parquets, the <c>.1st-pass.fdr_scores.bin</c> sidecars, and the passing
    /// base_id set, which <c>FirstPassFdrTask</c> writes into every <c>.reconciliation.json</c>
    /// as <c>first_pass_base_ids</c> (format v3). So this drives the loader directly and measures both shapes:</para>
    ///
    /// <list type="bullet">
    /// <item>COLLECT - what Stage 5 does today: every file's survivors into one list.</item>
    /// <item>STREAM - what the fix does: one file resident at a time, consumed and dropped.</item>
    /// </list>
    ///
    /// <para>Opt-in, because it needs a real multi-GB run directory. Point
    /// <c>OSPREY_BENCH_RUNDIR</c> at one (a CHS plate directory is ~86 files / ~15 GB of buffer,
    /// enough to show the shape in minutes; the 446-file directory shows the real peak).
    /// Without it the test is inconclusive and says so rather than passing quietly.</para>
    ///
    /// <para>CAVEAT on absolute numbers: the loader shares modified-sequence strings through a
    /// library-seeded pool, worth ~72 B of a survivor's 274 B. Seeding it here would mean loading
    /// the 12 GB library (~9.5 min). With <c>OSPREY_BENCH_LIBRARY</c> unset this runs on an empty
    /// frozen pool, so BOTH shapes are inflated by roughly that much and the A/B ratio stays
    /// meaningful while the absolutes are an upper bound.</para>
    /// </summary>
    [TestClass]
    public class Stage5SurvivorBufferBenchTest
    {
        private const string RUNDIR_VAR = @"OSPREY_BENCH_RUNDIR";

        [TestMethod]
        public void Stage5SurvivorBuffer_CollectVsStream()
        {
            string runDir = Environment.GetEnvironmentVariable(RUNDIR_VAR);
            if (string.IsNullOrEmpty(runDir) || !Directory.Exists(runDir))
            {
                Assert.Inconclusive(
                    @"Set {0} to a completed Osprey run directory (one holding *.scores.parquet, " +
                    @"*.1st-pass.fdr_scores.bin and *.reconciliation.json) to run this bench.",
                    RUNDIR_VAR);
                return;
            }

            var parquets = Directory.GetFiles(runDir, @"*.scores.parquet")
                .Where(p => !p.EndsWith(@".osprey.task", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
            Assert.AreNotEqual(0, parquets.Count, @"No .scores.parquet in " + runDir);

            var perFileParquetPaths = new Dictionary<string, string>();
            foreach (string p in parquets)
            {
                string stem = Path.GetFileName(p);
                stem = stem.Substring(0, stem.Length - @".scores.parquet".Length);
                perFileParquetPaths[stem] = p;
            }

            HashSet<uint> baseIds = ReadGlobalBaseIds(runDir);
            Assert.IsNotNull(baseIds,
                @"No .reconciliation.json carrying first_pass_base_ids in " + runDir +
                @" - that file is where FirstPassFdrTask persists the passing base_id set, and " +
                @"without it the survivor selection cannot be reproduced.");

            Console.WriteLine(@"run dir : {0}", runDir);
            Console.WriteLine(@"files   : {0}", perFileParquetPaths.Count);
            Console.WriteLine(@"base ids: {0:N0}", baseIds.Count);

            // A real config, not null: ResolveSidecarBasePath dereferences InputFiles to find
            // each file's .1st-pass.fdr_scores.bin. InputFiles carries the synthetic .raw paths
            // the CHS run itself used (the sources are long gone; only the stems matter here),
            // and OutputBlib names the experiment sidecar the loader looks for.
            var config = new OspreyConfig
            {
                InputFiles = perFileParquetPaths.Values
                    .Select(p => p.Substring(0, p.Length - @".scores.parquet".Length) + @".raw")
                    .ToList(),
                OutputBlib = Path.Combine(runDir, @"out.blib"),
            };

            var pool = BuildPool();

            // STREAM first, so the COLLECT run cannot benefit from a warmed file cache the
            // stream run paid for - measuring them the other way round would flatter the fix.
            var stream = Measure(@"STREAM ", perFileParquetPaths, baseIds, pool, config, collect: false);
            var collect = Measure(@"COLLECT", perFileParquetPaths, baseIds, pool, config, collect: true);

            Console.WriteLine();
            Console.WriteLine(@"survivors        : {0:N0}", collect.Survivors);
            Console.WriteLine(@"COLLECT peak     : {0:N2} GB  ({1:N0} s)", collect.PeakGb, collect.Seconds);
            Console.WriteLine(@"STREAM  peak     : {0:N2} GB  ({1:N0} s)", stream.PeakGb, stream.Seconds);
            Console.WriteLine(@"bytes per entry  : {0:N0} (collect)",
                collect.Survivors == 0 ? 0 : collect.PeakBytes / collect.Survivors);
            Console.WriteLine(@"reduction        : {0:N1}x",
                stream.PeakGb <= 0 ? 0 : collect.PeakGb / stream.PeakGb);

            // Written beside the data as well as to the console: a passing test's stdout is
            // swallowed by the summary runner, and these numbers ARE the deliverable.
            string report = Path.Combine(runDir, @"stage5-buffer-bench.txt");
            File.WriteAllLines(report, new[]
            {
                @"Stage 5 survivor-buffer bench",
                string.Format(@"run dir          : {0}", runDir),
                string.Format(@"files            : {0}", perFileParquetPaths.Count),
                string.Format(@"passing base ids : {0:N0}", baseIds.Count),
                string.Format(@"survivors        : {0:N0}", collect.Survivors),
                string.Format(@"COLLECT peak     : {0:N2} GB ({1:N0} s)", collect.PeakGb, collect.Seconds),
                string.Format(@"STREAM  peak     : {0:N2} GB ({1:N0} s)", stream.PeakGb, stream.Seconds),
                string.Format(@"bytes per entry  : {0:N0} (collect)",
                    collect.Survivors == 0 ? 0 : collect.PeakBytes / collect.Survivors),
                string.Format(@"reduction        : {0:N1}x",
                    stream.PeakGb <= 0 ? 0 : collect.PeakGb / stream.PeakGb),
                @"NOTE: sequence pool empty unless OSPREY_BENCH_LIBRARY is set, so absolute",
                @"peaks are an upper bound; the COLLECT/STREAM ratio is unaffected.",
            });
            Console.WriteLine(@"report           : {0}", report);

            Assert.AreEqual(collect.Survivors, stream.Survivors,
                @"The two shapes must select the SAME survivors - a memory win from dropping rows " +
                @"is not a win.");
            Assert.IsTrue(stream.PeakGb < collect.PeakGb,
                string.Format(@"Streaming should peak below collecting; got stream={0:N2} GB " +
                              @"collect={1:N2} GB", stream.PeakGb, collect.PeakGb));
        }

        private sealed class Result
        {
            internal long Survivors;
            internal long PeakBytes;
            internal double PeakGb => PeakBytes / (double)(1024 * 1024 * 1024);
            internal double Seconds;
        }

        /// <summary>
        /// Drive the loader over every file. <paramref name="collect"/> true keeps each file's
        /// list (what Stage 5 does today); false drops it after counting (what the fix does).
        /// Peak is the managed heap sampled after each file, which is what the survivor buffer
        /// actually occupies - working set would fold in the GC's committed-but-free pages and
        /// read high for both shapes.
        /// </summary>
        private static Result Measure(string label,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            HashSet<uint> baseIds, LibraryStringInterner pool, OspreyConfig config, bool collect)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var loader = new FirstPassSurvivorLoader(perFileParquetPaths, config, baseIds, pool);
            var held = collect ? new List<KeyValuePair<string, List<FdrEntry>>>() : null;
            var result = new Result();
            long baseline = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();

            int n = 0;
            foreach (var kvp in perFileParquetPaths)
            {
                var stubs = loader.Load(kvp.Key, out string error);
                Assert.IsNotNull(stubs, error);
                result.Survivors += stubs.Count;
                if (collect)
                    held.Add(new KeyValuePair<string, List<FdrEntry>>(kvp.Key, stubs));
                long now = GC.GetTotalMemory(false) - baseline;
                if (now > result.PeakBytes)
                    result.PeakBytes = now;
                if (++n % 10 == 0)
                    Console.WriteLine(@"  {0} {1,4}/{2}  heap {3:N2} GB", label, n,
                        perFileParquetPaths.Count, now / (double)(1024 * 1024 * 1024));
            }
            sw.Stop();
            result.Seconds = sw.Elapsed.TotalSeconds;
            GC.KeepAlive(held);
            return result;
        }

        /// <summary>
        /// The passing base_id set, read from any one <c>.reconciliation.json</c>. It is written
        /// per file but is the same GLOBAL set in each, so the first readable one serves.
        /// </summary>
        private static HashSet<uint> ReadGlobalBaseIds(string runDir)
        {
            foreach (string path in Directory.GetFiles(runDir, @"*.reconciliation.json")
                         .Where(p => !p.EndsWith(@".osprey.task", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var file = ReconciliationFile.Load(path);
                var ids = file?.FirstPassBaseIds;
                if (ids != null && ids.Length > 0)
                    return new HashSet<uint>(ids);
            }
            return null;
        }

        private static LibraryStringInterner BuildPool()
        {
            string lib = Environment.GetEnvironmentVariable(@"OSPREY_BENCH_LIBRARY");
            var pool = new LibraryStringInterner();
            if (string.IsNullOrEmpty(lib))
            {
                Console.WriteLine(
                    @"pool    : EMPTY (set OSPREY_BENCH_LIBRARY to seed it). Absolute peaks are " +
                    @"an upper bound; the COLLECT/STREAM ratio is unaffected.");
            }
            pool.Freeze();
            return pool;
        }
    }
}
