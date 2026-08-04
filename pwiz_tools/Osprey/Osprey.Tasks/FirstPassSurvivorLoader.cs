/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Loads ONE file's post-compaction first-pass survivors from that file's
    /// original <c>.scores.parquet</c> plus its finalized
    /// <c>.1st-pass.fdr_scores.bin</c> sidecar, filtered to a passing base_id set.
    ///
    /// <para>Both artifacts are on disk for every file by the time Stage 5 compacts,
    /// so a survivor list can be rebuilt at any later point without holding it. That
    /// is the whole point of this type: the all-files survivor buffer is the O(files)
    /// resident structure that costs 28 GB at 163 files (issue #4526), and every
    /// consumer of it reads one file at a time. Extracting the per-file load gives
    /// those consumers a source they can call per file instead of indexing into a
    /// buffer somebody else had to materialize.</para>
    ///
    /// <para>The load is byte-order-identical to the legacy in-place compaction:
    /// <see cref="FdrEntry.ParquetIndex"/> comes from the ORIGINAL parquet (row
    /// ordinal), the sidecar is overlaid onto the FULL stub set before filtering
    /// (the superset contract <see cref="FdrScoresSidecar.TryRead"/> requires), and
    /// the result is sorted by the canonical (EntryId, Charge, ScanNumber,
    /// ParquetIndex) key. Callers must not re-order it.</para>
    /// </summary>
    internal sealed class FirstPassSurvivorLoader
    {
        private readonly IReadOnlyDictionary<string, string> _perFileParquetPaths;
        private readonly OspreyConfig _config;
        private readonly HashSet<uint> _firstPassBaseIds;

        /// <param name="perFileParquetPaths">file name -> its original
        /// <c>.scores.parquet</c>, the same map Stage 5 resolves sidecar paths from.</param>
        /// <param name="config">Run config, for the sidecar base-path resolution.</param>
        /// <param name="firstPassBaseIds">The passing base_id set the compaction gate
        /// produced. Small (446 K uints at 163 files) and shared across every file, so
        /// holding it costs nothing next to the survivor lists it selects.</param>
        internal FirstPassSurvivorLoader(
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            HashSet<uint> firstPassBaseIds)
        {
            _perFileParquetPaths = perFileParquetPaths;
            _config = config;
            _firstPassBaseIds = firstPassBaseIds;
        }

        /// <summary>
        /// Load one file's survivors. Returns null with <paramref name="error"/> set
        /// on any missing path or failed sidecar overlay; those are genuine faults
        /// rather than absences, because Stage 5 just wrote both artifacts. The
        /// caller owns logging and the exit code, so this type stays free of
        /// <see cref="PipelineContext"/>.
        /// </summary>
        internal List<FdrEntry> Load(string fileName, out string error)
        {
            error = null;
            if (_perFileParquetPaths == null ||
                !_perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
            {
                error = string.Format(
                    @"First-pass survivor load: no scores parquet path for {0}", fileName);
                return null;
            }

            List<FdrEntry> stubs;
            try
            {
                stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath);
            }
            catch (Exception ex)
            {
                error = string.Format(
                    @"First-pass survivor load: failed to load stubs from {0}: {1}",
                    parquetPath, ex.Message);
                return null;
            }

            // Overlay the 1st-pass sidecar onto the FULL stub set (superset contract)
            // BEFORE filtering to survivors.
            string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(
                fileName, _perFileParquetPaths, _config);
            string pass1Path = FdrScoresSidecar.Pass1Path(sidecarBase);
            if (!FdrScoresSidecar.TryRead(pass1Path, stubs, FdrScoresSidecar.Pass.FirstPass))
            {
                error = string.Format(
                    @"First-pass survivor load: failed to overlay .1st-pass.fdr_scores.bin for {0} " +
                    @"(expected at {1})", fileName, pass1Path);
                return null;
            }

            stubs.RemoveAll(e => !_firstPassBaseIds.Contains(e.EntryId & ScoringTaskShared.BASE_ID_MASK));
            stubs.TrimExcess();
            stubs.Sort(FdrEntry.CANONICAL_ORDER); // Array.Sort OK: CANONICAL_ORDER's terminal key ParquetIndex is unique per reloaded stub, so the comparison never ties
            return stubs;
        }
    }
}
