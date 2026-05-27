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
using System.IO;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// In-memory state needed to drive a per-file Stage 6 rescore from
    /// the Stage 5 → Stage 6 boundary files on disk. Mirrors
    /// <c>RescoreInputs</c> in <c>osprey/crates/osprey/src/rescore.rs</c>.
    /// The same shape the in-process pipeline holds at the boundary, so
    /// the rescore engine can be written once and used from both the
    /// in-process path and the worker path.
    /// </summary>
    public class RescoreInputs
    {
        /// <summary>
        /// Per-file <see cref="FdrEntry"/> stubs from
        /// <c>&lt;stem&gt;.scores.parquet</c>, with SVM scores + 4 q-values
        /// + PEP + <c>RunProteinQvalue</c> overlaid from the
        /// <c>&lt;stem&gt;.1st-pass.fdr_scores.bin</c> sidecar. File order
        /// matches the order of <c>parquetPaths</c> passed to
        /// <see cref="RescoreHydration.HydrateForRescore"/>.
        /// </summary>
        public List<KeyValuePair<string, List<FdrEntry>>> PerFileEntries { get; set; }

        /// <summary>
        /// Reconciliation actions keyed by <c>(file_name, vec_idx)</c>.
        /// Built from the homogeneous <c>use_cwt_peak_actions</c> +
        /// <c>forced_integration_actions</c> arrays in
        /// <c>reconciliation.json</c> by joining each action's
        /// <c>entry_id</c> against the loaded stub list. Keep actions
        /// are implicitly absent (the planner never persists them).
        /// </summary>
        public Dictionary<(string FileName, int Index), ReconcileAction> ReconciliationActions { get; set; }

        /// <summary>
        /// Refined per-file RT calibrations reconstructed from
        /// <c>reconciliation.json</c>'s <c>refined_rt_calibration</c>
        /// field via <see cref="RTCalibration.FromModelParams"/>. Files
        /// whose envelope had a null calibration (e.g., refined fit
        /// failed during Stage 5) are absent from the dictionary.
        /// </summary>
        public Dictionary<string, RTCalibration> RefinedCalibrations { get; set; }

        /// <summary>
        /// Per-file gap-fill targets parsed from
        /// <c>reconciliation.json</c>'s <c>gap_fill_targets</c> array.
        /// </summary>
        public Dictionary<string, List<GapFillTarget>> PerFileGapFill { get; set; }

        /// <summary>
        /// Per-file multi-charge consensus rescore targets. Populated
        /// only by callers that compute it post-compaction (consensus
        /// is meaningful only against the surviving entry set). Left
        /// null when the bundle is built ahead of compaction; the
        /// downstream task computes it on demand in that case.
        /// </summary>
        public Dictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> PerFileConsensusTargets { get; set; }

        /// <summary>
        /// Full set of file stems participating in the planner's join, as
        /// read from <c>reconciliation.json</c>'s <c>file_stems</c> field
        /// (v2+). Per-file Stage 6 rescore workers carry this through so
        /// they can compute the join-wide reconciliation parameter hash —
        /// the worker's <c>OspreyConfig.InputFiles</c> only has its single
        /// parquet, but the hash that the downstream
        /// <c>--join-at-pass=2</c> merge node validates is computed over
        /// all files. Empty list when reading a v1 envelope (the worker
        /// falls back to its <c>InputFiles</c> stems in that case).
        /// </summary>
        public List<string> JoinFileStems { get; set; }

        /// <summary>Total non-Keep reconciliation actions across all files.</summary>
        public int TotalActions => ReconciliationActions.Count;

        /// <summary>Total stubs across all files.</summary>
        public int TotalStubs
        {
            get
            {
                int n = 0;
                foreach (var kv in PerFileEntries)
                    n += kv.Value.Count;
                return n;
            }
        }

        /// <summary>Total gap-fill targets across all files.</summary>
        public int TotalGapFillTargets
        {
            get
            {
                int n = 0;
                foreach (var kv in PerFileGapFill)
                    n += kv.Value.Count;
                return n;
            }
        }
    }

    /// <summary>
    /// Hydrate the Stage 5 → Stage 6 boundary file pair into the
    /// in-memory state needed to drive a per-file rescore. Mirrors
    /// <c>hydrate_for_rescore</c> in
    /// <c>osprey/crates/osprey/src/rescore.rs</c>.
    /// </summary>
    public static class RescoreHydration
    {
        /// <summary>
        /// Read each <c>&lt;stem&gt;.scores.parquet</c> in
        /// <paramref name="parquetPaths"/>, overlay the matching
        /// <c>&lt;stem&gt;.1st-pass.fdr_scores.bin</c> sidecar (v3 format,
        /// pass = FirstPass), and parse the matching
        /// <c>&lt;stem&gt;.reconciliation.json</c> envelope into the
        /// per-file action map + refined calibration + gap-fill list.
        ///
        /// File names are extracted from the parquet stem with the
        /// <c>.scores</c> suffix stripped, mirroring Rust's
        /// <c>synthetic_input_from_parquet</c>. The output preserves the
        /// input ordering.
        ///
        /// Throws <see cref="InvalidDataException"/> on any per-file boundary
        /// file that is missing, unreadable, or fails its format-version /
        /// count checks. Does not silently fall back to partial state — a
        /// Stage 6 worker that proceeded with one file's planner output
        /// missing would scramble gap-fill results across files.
        /// </summary>
        public static RescoreInputs HydrateForRescore(IList<string> parquetPaths)
        {
            if (parquetPaths == null) throw new ArgumentNullException(nameof(parquetPaths));
            if (parquetPaths.Count == 0)
                throw new InvalidDataException("HydrateForRescore: parquetPaths is empty");

            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>(parquetPaths.Count);
            foreach (var parquetPath in parquetPaths)
            {
                string syntheticInput = SyntheticInputFromParquet(parquetPath);
                string fileName = Path.GetFileNameWithoutExtension(syntheticInput);
                if (string.IsNullOrEmpty(fileName))
                {
                    throw new InvalidDataException(string.Format(
                        "HydrateForRescore: could not derive file_name from parquet path {0}",
                        parquetPath));
                }

                // Stubs from parquet (entry_id, charge, modseq, RTs,
                // parquet_index assigned by LoadFdrStubsFromParquet).
                List<FdrEntry> stubs;
                try
                {
                    stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException(string.Format(
                        "HydrateForRescore: failed to load stubs from {0}: {1}",
                        parquetPath, ex.Message), ex);
                }
                perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
            }

            return HydrateReconciliationOverlay(perFileEntries, parquetPaths);
        }

        /// <summary>
        /// Overlay the per-file 1st-pass FDR sidecars and parse the per-file
        /// <c>reconciliation.json</c> envelopes onto an already-loaded
        /// <paramref name="perFileEntries"/> list. The per-file element at
        /// index <c>i</c> in <paramref name="perFileEntries"/> must
        /// correspond to <paramref name="parquetPaths"/>[<c>i</c>] — the
        /// fileName key is rederived from the parquet path for the sidecar
        /// path computation.
        ///
        /// Used by both the worker-mode <see cref="HydrateForRescore"/>
        /// wrapper (which loads stubs first) and the in-pipeline joinOnly
        /// dispatch (which already has stubs loaded with PIN features +
        /// calibration siblings, and just needs the rescore overlay added
        /// to share state with FirstJoin / PerFileRescore).
        ///
        /// The returned <see cref="RescoreInputs"/> references the SAME
        /// <see cref="FdrEntry"/> list objects passed in
        /// <paramref name="perFileEntries"/>; this method mutates those
        /// lists' element fields via the FDR sidecar overlay.
        /// <see cref="RescoreInputs.PerFileConsensusTargets"/> is left
        /// null; callers that need it compute it post-compaction.
        /// </summary>
        public static RescoreInputs HydrateReconciliationOverlay(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IList<string> parquetPaths)
        {
            if (perFileEntries == null) throw new ArgumentNullException(nameof(perFileEntries));
            if (parquetPaths == null) throw new ArgumentNullException(nameof(parquetPaths));
            if (perFileEntries.Count != parquetPaths.Count)
            {
                throw new InvalidDataException(string.Format(
                    "HydrateReconciliationOverlay: perFileEntries.Count ({0}) != parquetPaths.Count ({1})",
                    perFileEntries.Count, parquetPaths.Count));
            }

            var refinedCalibrations = new Dictionary<string, RTCalibration>();
            var perFileGapFill = new Dictionary<string, List<GapFillTarget>>();
            var reconciliationActions = new Dictionary<(string, int), ReconcileAction>();
            // Captured from the first envelope's file_stems field (v2+);
            // every subsequent envelope must carry the same set so the
            // worker's join-wide reconciliation hash matches the planner's.
            // Mirrors the consistency check in Rust hydrate_for_rescore.
            List<string> joinFileStems = null;

            for (int i = 0; i < perFileEntries.Count; i++)
            {
                string parquetPath = parquetPaths[i];
                string syntheticInput = SyntheticInputFromParquet(parquetPath);
                string fileName = perFileEntries[i].Key;
                var stubs = perFileEntries[i].Value;

                // Overlay SVM scores + 4 q-values + PEP + RunProteinQvalue
                // from .1st-pass.fdr_scores.bin v3. expected_pass = FirstPass:
                // the planner's actions were computed against first-pass FDR,
                // and the worker compaction predicate uses first-pass q-values.
                string sidecarPath = FdrScoresSidecar.Pass1Path(syntheticInput);
                if (!FdrScoresSidecar.TryRead(sidecarPath, stubs, FdrScoresSidecar.Pass.FirstPass))
                {
                    throw new InvalidDataException(string.Format(
                        "HydrateReconciliationOverlay: failed to overlay .1st-pass.fdr_scores.bin for {0} " +
                        "(expected at {1})", fileName, sidecarPath));
                }

                // Parse reconciliation.json.
                string reconPath = ReconciliationFile.PathForInput(syntheticInput);
                ReconciliationFile envelope;
                try
                {
                    envelope = ReconciliationFile.Load(reconPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException(string.Format(
                        "HydrateReconciliationOverlay: failed to read {0}: {1}",
                        reconPath, ex.Message), ex);
                }

                // Capture / validate file_stems across all envelopes.
                // ReconciliationFile.Load already rejects any envelope whose
                // format_version != CurrentFormatVersion (currently 2), so by
                // the time we reach here `envelope.FileStems` must be the
                // planner's full join file set -- a non-empty list, identical
                // across every envelope produced by a single planner step.
                // Any disagreement (including unexpected empty stems) means
                // the on-disk envelopes were produced by different planner
                // steps and indicates a corrupted hand-off. Mirrors the
                // consistency check in Rust's hydrate_for_rescore.
                var envelopeStems = NormalizeStems(envelope.FileStems);
                if (joinFileStems == null)
                {
                    joinFileStems = envelopeStems;
                }
                else if (!StemsEqual(joinFileStems, envelopeStems))
                {
                    throw new InvalidDataException(string.Format(
                        "HydrateReconciliationOverlay: reconciliation.json {0} carries a " +
                        "different file_stems set than its siblings (planner inconsistency). " +
                        "Expected: [{1}]; got: [{2}]",
                        reconPath,
                        string.Join(", ", joinFileStems),
                        string.Join(", ", envelopeStems)));
                }

                // Build entry_id -> vec_idx map from the loaded stubs so the
                // planner's entry_id-keyed actions can be rehomed onto
                // (file_name, vec_idx) keys the rescore engine consumes.
                var idToIdx = new Dictionary<uint, int>(stubs.Count);
                for (int idx = 0; idx < stubs.Count; idx++)
                    idToIdx[stubs[idx].EntryId] = idx;

                if (envelope.UseCwtPeakActions != null)
                {
                    foreach (var entry in envelope.UseCwtPeakActions)
                    {
                        if (!idToIdx.TryGetValue(entry.EntryId, out int vecIdx))
                        {
                            throw new InvalidDataException(string.Format(
                                "HydrateReconciliationOverlay: use_cwt_peak entry_id {0} in {1} not found " +
                                "in stubs (parquet drift?)", entry.EntryId, reconPath));
                        }
                        reconciliationActions[(fileName, vecIdx)] = new ReconcileAction.UseCwtPeak(
                            (int)entry.CandidateIdx, entry.StartRt, entry.ApexRt, entry.EndRt);
                    }
                }

                if (envelope.ForcedIntegrationActions != null)
                {
                    foreach (var entry in envelope.ForcedIntegrationActions)
                    {
                        if (!idToIdx.TryGetValue(entry.EntryId, out int vecIdx))
                        {
                            throw new InvalidDataException(string.Format(
                                "HydrateReconciliationOverlay: forced_integration entry_id {0} in {1} not " +
                                "found in stubs (parquet drift?)", entry.EntryId, reconPath));
                        }
                        reconciliationActions[(fileName, vecIdx)] = new ReconcileAction.ForcedIntegration(
                            entry.ExpectedRt, entry.HalfWidth);
                    }
                }

                if (envelope.RefinedRtCalibration != null)
                {
                    var cal = RTCalibration.FromModelParams(
                        envelope.RefinedRtCalibration.LibraryRts,
                        envelope.RefinedRtCalibration.FittedRts,
                        envelope.RefinedRtCalibration.AbsResiduals,
                        envelope.RefinedRtCalibration.ResidualSd);
                    refinedCalibrations[fileName] = cal;
                }

                if (envelope.GapFillTargets != null && envelope.GapFillTargets.Count > 0)
                {
                    var gapFill = new List<GapFillTarget>(envelope.GapFillTargets.Count);
                    foreach (var g in envelope.GapFillTargets)
                    {
                        gapFill.Add(new GapFillTarget
                        {
                            TargetEntryId = g.TargetEntryId,
                            DecoyEntryId = g.DecoyEntryId,
                            ExpectedRt = g.ExpectedRt,
                            HalfWidth = g.HalfWidth,
                            ModifiedSequence = g.ModifiedSequence,
                            Charge = g.Charge,
                        });
                    }
                    perFileGapFill[fileName] = gapFill;
                }
            }

            return new RescoreInputs
            {
                PerFileEntries = perFileEntries,
                ReconciliationActions = reconciliationActions,
                RefinedCalibrations = refinedCalibrations,
                PerFileGapFill = perFileGapFill,
                PerFileConsensusTargets = null,
                JoinFileStems = joinFileStems ?? new List<string>(),
            };
        }

        /// <summary>
        /// Sort + dedup a list of file stems (Ordinal). Returns a new list;
        /// the input is not mutated. Empty / null input becomes an empty
        /// list. Used to canonicalize the <c>file_stems</c> field from
        /// each <c>reconciliation.json</c> envelope before consistency
        /// checks across siblings.
        /// </summary>
        private static List<string> NormalizeStems(IList<string> stems)
        {
            if (stems == null || stems.Count == 0)
                return new List<string>();
            var result = new List<string>(stems.Count);
            foreach (var s in stems)
            {
                if (!string.IsNullOrEmpty(s))
                    result.Add(s);
            }
            result.Sort(StringComparer.Ordinal);
            for (int i = result.Count - 1; i > 0; i--)
            {
                if (string.Equals(result[i], result[i - 1], StringComparison.Ordinal))
                    result.RemoveAt(i);
            }
            return result;
        }

        /// <summary>
        /// Ordinal element-wise equality on two pre-normalized stem lists.
        /// </summary>
        private static bool StemsEqual(IList<string> a, IList<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Inverse of <c>scores_path_for_input</c>: given
        /// <c>/data/sample1.scores.parquet</c>, produce a synthetic input
        /// path <c>/data/sample1.mzML</c> whose stem matches what the
        /// worker used. This lets the worker reuse the existing
        /// path-derivation helpers (FDR sidecars, calibration JSON,
        /// reconciliation JSON) without duplicating them. The synthetic
        /// path is never opened — only its components are inspected.
        /// Mirrors Rust's <c>synthetic_input_from_parquet</c>.
        /// </summary>
        public static string SyntheticInputFromParquet(string parquetPath)
        {
            if (parquetPath == null) throw new ArgumentNullException(nameof(parquetPath));
            // GetFileNameWithoutExtension returns "" not null for valid paths
            // and throws on invalid input, so the result is never null here.
            string stem = Path.GetFileNameWithoutExtension(parquetPath);
            // Strip a trailing ".scores" if present.
            const string ScoresSuffix = ".scores";
            if (stem.EndsWith(ScoresSuffix, StringComparison.Ordinal))
                stem = stem.Substring(0, stem.Length - ScoresSuffix.Length);
            string parent = Path.GetDirectoryName(parquetPath);
            string filename = stem + ".mzML";
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
        }
    }
}
