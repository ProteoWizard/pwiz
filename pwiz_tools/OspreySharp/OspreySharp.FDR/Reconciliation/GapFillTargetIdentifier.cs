/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.FDR.Reconciliation
{
    /// <summary>
    /// Identifies precursors that passed FDR in some sibling replicate but
    /// were missing from a given file, so the Stage 6 worker can score them
    /// at the consensus RT and contribute per-file boundaries to the blib.
    ///
    /// Ports <c>identify_gap_fill_targets</c> from
    /// <c>osprey/crates/osprey/src/reconciliation.rs</c>.
    /// </summary>
    public static class GapFillTargetIdentifier
    {
        /// <summary>
        /// For each input file, return the list of <see cref="GapFillTarget"/>
        /// records to write into <c>reconciliation.json</c>'s
        /// <c>gap_fill_targets</c> array. The result is sorted by
        /// <c>target_entry_id</c> so the JSON output matches Rust byte for
        /// byte. Files with no gap-fill candidates are omitted.
        /// </summary>
        /// <param name="consensus">Per-peptide consensus RTs from
        /// <see cref="ConsensusRts"/>.</param>
        /// <param name="perFileEntries">Post-compaction FDR stubs per file.</param>
        /// <param name="perFileRefinedCal">Per-file refined RT calibrations
        /// (LOESS refit on consensus peptides). Preferred over original.</param>
        /// <param name="perFileOriginalCal">Per-file first-pass RT calibrations.
        /// Used as a fallback when no refined calibration is available.</param>
        /// <param name="experimentFdr">FDR threshold; precursors are eligible
        /// for gap-fill when any of their four q-values is &lt;= this.</param>
        /// <param name="libLookup"><c>(modified_sequence, charge) →
        /// (target_entry_id, decoy_entry_id)</c> from the library. Decoy IDs
        /// follow the <c>target_id | 0x80000000</c> convention.</param>
        /// <param name="libPrecursorMz"><c>target_entry_id → precursor m/z</c>;
        /// only consulted when an isolation-window filter is supplied.</param>
        /// <param name="perFileIsolationMz">Optional per-file
        /// <c>(lo, hi)</c> isolation window intervals. When supplied,
        /// candidates whose library precursor m/z falls outside every window
        /// are filtered out (essential for GPF datasets with disjoint m/z
        /// ranges). May be <c>null</c> to disable the filter; an empty list
        /// for a file similarly disables the filter for that file.</param>
        public static IReadOnlyDictionary<string, IReadOnlyList<GapFillTarget>> Identify(
            IReadOnlyList<PeptideConsensusRT> consensus,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, RTCalibration> perFileRefinedCal,
            IReadOnlyDictionary<string, RTCalibration> perFileOriginalCal,
            double experimentFdr,
            IReadOnlyDictionary<(string ModifiedSequence, byte Charge),
                (uint TargetEntryId, uint DecoyEntryId)> libLookup,
            IReadOnlyDictionary<uint, double> libPrecursorMz,
            IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz)
        {
            if (consensus == null)
                throw new ArgumentNullException(nameof(consensus));
            if (perFileEntries == null)
                throw new ArgumentNullException(nameof(perFileEntries));
            if (perFileRefinedCal == null)
                throw new ArgumentNullException(nameof(perFileRefinedCal));
            if (perFileOriginalCal == null)
                throw new ArgumentNullException(nameof(perFileOriginalCal));
            if (libLookup == null)
                throw new ArgumentNullException(nameof(libLookup));
            if (libPrecursorMz == null)
                throw new ArgumentNullException(nameof(libPrecursorMz));

            // 1. Build passing precursors. Targets only — paired decoys ride
            //    along via the same library lookup. Match the Rust rationale
            //    at reconciliation.rs:870-891: a precursor is eligible if ANY
            //    of the four q-values clears the threshold so a peptide that
            //    passes peptide-level FDR (even with middling precursor q)
            //    still gets its missing charge states gap-filled.
            var passingPrecursors = new HashSet<(string ModifiedSequence, byte Charge)>();
            foreach (var fileKvp in perFileEntries)
            {
                foreach (var entry in fileKvp.Value)
                {
                    if (entry.IsDecoy)
                        continue;
                    double bestQ = Math.Min(
                        Math.Min(entry.RunPrecursorQvalue, entry.RunPeptideQvalue),
                        Math.Min(entry.ExperimentPrecursorQvalue, entry.ExperimentPeptideQvalue));
                    if (bestQ <= experimentFdr)
                        passingPrecursors.Add((entry.ModifiedSequence, entry.Charge));
                }
            }

            var result = new Dictionary<string, IReadOnlyList<GapFillTarget>>();
            if (passingPrecursors.Count == 0)
                return result;

            // 2. Consensus lookup by (modified_sequence, is_decoy=false).
            var consensusMap = new Dictionary<string, PeptideConsensusRT>();
            foreach (var c in consensus)
            {
                if (c.IsDecoy)
                    continue;
                consensusMap[c.ModifiedSequence] = c;
            }

            // 3. Per-file: find missing precursors, optionally filter by
            //    isolation-window m/z, look up consensus RT, and emit
            //    GapFillTarget records.
            foreach (var fileKvp in perFileEntries)
            {
                string fileName = fileKvp.Key;
                var entries = fileKvp.Value;

                RTCalibration cal = null;
                if (!perFileRefinedCal.TryGetValue(fileName, out cal))
                    perFileOriginalCal.TryGetValue(fileName, out cal);
                if (cal == null)
                    continue;

                IReadOnlyList<(double Lo, double Hi)> isoWindows = null;
                if (perFileIsolationMz != null)
                    perFileIsolationMz.TryGetValue(fileName, out isoWindows);

                var present = new HashSet<(string ModifiedSequence, byte Charge)>();
                foreach (var e in entries)
                {
                    if (e.IsDecoy)
                        continue;
                    present.Add((e.ModifiedSequence, e.Charge));
                }

                var targets = new List<GapFillTarget>();
                foreach (var key in passingPrecursors)
                {
                    if (present.Contains(key))
                        continue;

                    if (!libLookup.TryGetValue(key, out var ids))
                        continue;

                    // m/z range filter: skip precursors whose m/z is not
                    // covered by any isolation window in this file. Strict
                    // upper bound (precursor_mz < hi) matches Rust at
                    // reconciliation.rs:954-956.
                    if (isoWindows != null && isoWindows.Count > 0)
                    {
                        if (!libPrecursorMz.TryGetValue(ids.TargetEntryId, out double precursorMz))
                            continue;
                        bool inRange = false;
                        for (int i = 0; i < isoWindows.Count; i++)
                        {
                            var w = isoWindows[i];
                            if (precursorMz >= w.Lo && precursorMz < w.Hi)
                            {
                                inRange = true;
                                break;
                            }
                        }
                        if (!inRange)
                            continue;
                    }

                    if (!consensusMap.TryGetValue(key.ModifiedSequence, out var consensusEntry))
                        continue;

                    double expectedRt = cal.Predict(consensusEntry.ConsensusLibraryRt);
                    double halfWidth = consensusEntry.MedianPeakWidth / 2.0;

                    targets.Add(new GapFillTarget
                    {
                        Charge = key.Charge,
                        DecoyEntryId = ids.DecoyEntryId,
                        ExpectedRt = expectedRt,
                        HalfWidth = halfWidth,
                        ModifiedSequence = key.ModifiedSequence,
                        TargetEntryId = ids.TargetEntryId,
                    });
                }

                if (targets.Count == 0)
                    continue;

                // Sort by target_entry_id for deterministic output (matches
                // Rust serializer in reconciliation_io.rs line 167).
                targets.Sort((a, b) => a.TargetEntryId.CompareTo(b.TargetEntryId));
                result[fileName] = targets;
            }

            return result;
        }
    }
}
