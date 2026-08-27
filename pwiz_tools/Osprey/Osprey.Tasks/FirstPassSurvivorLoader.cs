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
    /// ordinal, which the filtering below does not renumber) and the result is sorted
    /// by the canonical (EntryId, Charge, ScanNumber, ParquetIndex) key. Callers must
    /// not re-order it.</para>
    ///
    /// <para>Survivors are selected DURING the parquet read rather than after it, so the
    /// rows that do not survive are never built (issue #4486). That inverts the order
    /// this class used to run in - it overlaid the sidecar onto the FULL stub set first,
    /// which is the superset
    /// <see cref="FdrScoresSidecar.TryRead(string, IList{FdrEntry}, FdrScoresSidecar.Pass)"/> requires -
    /// so the overlay is now told which absences were deliberate. Each survivor ends up
    /// with the same values either way, because records are matched by entry_id rather
    /// than by position.</para>
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
            return Load(fileName, null, out error);
        }

        /// <summary>
        /// Load one file's survivors from <paramref name="parquetPathOverride"/> instead of
        /// its Stage 4 <c>.scores.parquet</c>.
        ///
        /// <para>The override is that file's <c>.scores-reconciled.parquet</c>, and only when
        /// the caller has judged it CURRENT for this run - existence is not enough, because a
        /// parquet left by a run with different reconciliation parameters would inject another
        /// arm's boundaries (see <c>RescoredPoolPlan.ReconciledPaths</c>). Reading it makes
        /// the Stage 4 parquet unnecessary: this branch now holds the survivor subset with
        /// Stage 6's re-scored boundaries already applied and the gap-fill rows already
        /// merged, which is precisely what the second read used to put back (#4486).</para>
        ///
        /// <para>The sidecar overlay is unchanged and is resolved from the ORIGINAL scores
        /// path, not from this one - the 1st-pass sidecar is a Stage 5 artifact and has no
        /// reconciled sibling. Gap-fill rows simply find no record in it and keep the
        /// score-reset defaults a cold run gives them, which is the direction
        /// <c>TryRead</c> tolerates; the predicate covers the other direction, a record whose
        /// row was compacted away.</para>
        /// </summary>
        internal List<FdrEntry> Load(string fileName, string parquetPathOverride, out string error)
        {
            error = null;
            if (_perFileParquetPaths == null ||
                !_perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
            {
                error = string.Format(
                    @"First-pass survivor load: no scores parquet path for {0}", fileName);
                return null;
            }
            if (parquetPathOverride != null)
            {
                // Refused, not fallen back on. A subsetted reconciled parquet with no
                // score_index column records nowhere which .scores.parquet row each row came
                // from, so reading it by position maps every row to the wrong Stage 4
                // features - and produces a well-formed answer while doing it.
                if (ParquetScoreCache.IsSubsetWithoutScoreIndex(parquetPathOverride))
                {
                    error = string.Format(
                        @"First-pass survivor load: {0} holds the survivor subset but carries no " +
                        @"score_index column, so its rows cannot be matched back to " +
                        @".scores.parquet. Re-run --task CompactPerFileRescoring over this " +
                        @"directory to rewrite them with it.", parquetPathOverride);
                    return null;
                }
                parquetPath = parquetPathOverride;
            }

            // Applied AS THE PARQUET IS READ, not after. This kept ~533 K of ~2.99 M stubs
            // per file at 257 CHS files, so filtering afterwards built 5.6x what survived
            // it - the whole reason Stage 7's front end allocates before it compacts
            // (issue #4486). Hoisted out of the two calls below so both use one delegate.
            Func<uint, bool> isSurvivor = entryId =>
                _firstPassBaseIds.Contains(entryId & ScoringTaskShared.BASE_ID_MASK);

            List<FdrEntry> stubs;
            try
            {
                stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath, isSurvivor);
            }
            catch (Exception ex)
            {
                error = string.Format(
                    @"First-pass survivor load: failed to load stubs from {0}: {1}",
                    parquetPath, ex.Message);
                return null;
            }

            // Overlay the 1st-pass sidecar. That sidecar covers the whole stub set, so with
            // the survivors already selected above it carries millions of records with no
            // entry to land on; the predicate tells the reader which absences it asked for,
            // leaving any OTHER missing entry_id the corruption it has always been. Records
            // are matched by entry_id rather than position, so overlaying the filtered list
            // gives each survivor the same values overlaying the full one did.
            string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(
                fileName, _perFileParquetPaths, _config);
            string pass1Path = FdrScoresSidecar.Pass1Path(sidecarBase);
            if (!FdrScoresSidecar.TryRead(pass1Path, stubs, FdrScoresSidecar.Pass.FirstPass,
                    entryId => !isSurvivor(entryId)))
            {
                error = string.Format(
                    @"First-pass survivor load: failed to overlay .1st-pass.fdr_scores.bin for {0} " +
                    @"(expected at {1})", fileName, pass1Path);
                return null;
            }

            stubs.TrimExcess();
            stubs.Sort(FdrEntry.CANONICAL_ORDER); // Array.Sort OK: CANONICAL_ORDER's terminal key ParquetIndex is unique per reloaded stub, so the comparison never ties
            return stubs;
        }
    }
}
