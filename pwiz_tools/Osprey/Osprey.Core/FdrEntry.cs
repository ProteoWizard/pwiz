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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// An entry in the FDR results table with q-values at multiple levels.
    /// Maps to osprey-core/src/types.rs FdrEntry.
    /// </summary>
    public class FdrEntry
    {
        /// <summary>
        /// The canonical survivor order - (EntryId, Charge, ScanNumber, ParquetIndex) - that
        /// the post-Percolator-sort + compaction buffer establishes and every path which
        /// rebuilds that buffer from disk has to reproduce. Held in ONE place because four
        /// separate copies of this comparison had accumulated across the Stage 5 reload, the
        /// Stage 6 rebuild and the resume overlays, and a buffer built by one copy is compared
        /// against a buffer built by another.
        ///
        /// <para>Safe with the unstable <c>Array.Sort</c> that <c>List.Sort</c> uses: the
        /// terminal key ParquetIndex is unique per file, so the comparison never ties and the
        /// result does not depend on the sort's tie handling.</para>
        /// </summary>
        public static readonly Comparison<FdrEntry> CANONICAL_ORDER = (a, b) =>
        {
            int c = a.EntryId.CompareTo(b.EntryId);
            if (c != 0)
                return c;
            c = a.Charge.CompareTo(b.Charge);
            if (c != 0)
                return c;
            c = a.ScanNumber.CompareTo(b.ScanNumber);
            if (c != 0)
                return c;
            return a.ParquetIndex.CompareTo(b.ParquetIndex);
        };

        public uint EntryId { get; set; }
        public uint ParquetIndex { get; set; }
        public bool IsDecoy { get; set; }
        public byte Charge { get; set; }
        public uint ScanNumber { get; set; }
        public double ApexRt { get; set; }
        public double StartRt { get; set; }
        public double EndRt { get; set; }
        public double CoelutionSum { get; set; }
        public double Score { get; set; }
        public double RunPrecursorQvalue { get; set; }
        public double RunPeptideQvalue { get; set; }
        public double RunProteinQvalue { get; set; }
        public double ExperimentPrecursorQvalue { get; set; }
        public double ExperimentPeptideQvalue { get; set; }
        public double ExperimentProteinQvalue { get; set; }
        public double Pep { get; set; }
        public string ModifiedSequence { get; set; }

        /// <summary>
        /// The per-entry score the EXPERIMENT-scope competitions ranked this entry on (sidecar
        /// v4, issue #4522): max over the entry's rows across runs under the default
        /// aggregation, <c>TargetDecoyCompetition.ComputeBaseIdMeanBestN</c>'s value under
        /// mean-best-N. <see cref="Score"/> is the per-ROW discriminant the RUN-scope q-values
        /// compete on; this is its experiment-scope counterpart, persisted beside the
        /// experiment q-values so a consumer can re-gate at that scope without rebuilding the
        /// roll-up and branching on <c>OSPREY_EXPERIMENT_AGG</c>.
        /// </summary>
        public double ExperimentAggregateScore { get; set; }

        /// <summary>
        /// Full PIN feature vector (21 features) computed during coelution scoring.
        /// Used by Percolator FDR. Null if features have not been computed yet
        /// (e.g., for stubs loaded from a Parquet cache without features).
        /// </summary>
        public double[] Features { get; set; }

        /// <summary>
        /// All CWT-detected peak candidates evaluated during coelution scoring,
        /// not just the winning peak. Populated by the scoring path; null on
        /// stubs loaded from Parquet without the cwt_candidates column. Stage 6
        /// reconciliation reads this list to decide whether a per-file peak
        /// boundary should be replaced by an inter-replicate consensus
        /// boundary, mirroring Rust CoelutionScoredEntry::cwt_candidates.
        /// </summary>
        public List<CwtCandidate> CwtCandidates { get; set; }

        /// <summary>
        /// Library fragment m/z list for this entry, mirroring Rust
        /// CoelutionScoredEntry::fragment_mzs. Populated at scoring time
        /// from the candidate's full fragment list (NOT just the top-N
        /// used by XIC extraction). Null on stubs loaded from Parquet
        /// before the column is read back. Carried through to the
        /// reconciled .scores.parquet's `fragment_mzs` blob column for
        /// downstream consumers (Skyline blib export, etc.).
        /// </summary>
        public double[] FragmentMzs { get; set; }

        /// <summary>
        /// Library fragment relative intensities for this entry, mirroring
        /// Rust CoelutionScoredEntry::fragment_intensities. Same length /
        /// ordering as <see cref="FragmentMzs"/>. f32 to match the
        /// underlying LibraryFragment representation.
        /// </summary>
        public float[] FragmentIntensities { get; set; }

        /// <summary>
        /// Reference XIC retention times across the WINNING peak's
        /// boundary (i.e. ref_xic[bestPeak.StartIndex..=bestPeak.EndIndex]).
        /// Mirrors Rust CoelutionScoredEntry::reference_xic's first
        /// component — the per-scan RT axis the apex / peak shape was
        /// computed from.
        /// </summary>
        public double[] ReferenceXicRts { get; set; }

        /// <summary>
        /// Reference XIC intensities across the WINNING peak's boundary.
        /// Mirrors Rust CoelutionScoredEntry::reference_xic's second
        /// component. Same length as <see cref="ReferenceXicRts"/>.
        /// </summary>
        public double[] ReferenceXicIntensities { get; set; }

        /// <summary>
        /// Peak area within the WINNING peak's boundary (trapezoidal
        /// integration on the reference XIC). Distinct from Stage 4's
        /// `peak_area` (the original CWT peak's area) — for Stage 6
        /// override / reconciled entries this reflects the reconciled
        /// boundary, not the original CWT detection.
        /// </summary>
        public double BoundsArea { get; set; }

        /// <summary>
        /// Peak SNR within the WINNING peak's boundary. Distinct from
        /// Stage 4's `signal_to_noise` (the original CWT peak's SNR);
        /// reflects the reconciled boundary on Stage 6 override entries.
        /// </summary>
        public double BoundsSnr { get; set; }

        public FdrEntry()
        {
            RunPrecursorQvalue = 1.0;
            RunPeptideQvalue = 1.0;
            RunProteinQvalue = 1.0;
            ExperimentPrecursorQvalue = 1.0;
            ExperimentPeptideQvalue = 1.0;
            ExperimentProteinQvalue = 1.0;
            Pep = 1.0;
        }

        /// <summary>
        /// Reset the discriminant fields to the Rust <c>to_fdr_entry</c> defaults: Score and
        /// <see cref="ExperimentAggregateScore"/> 0, every q-value and Pep 1.0. This is the
        /// state a Stage 6 rescore target is left in for the 2nd pass to fill, and the state a
        /// fresh gap-fill stub is appended in.
        ///
        /// <para>One method because the same eight assignments had been written out five
        /// times - the rescore overlay, both gap-fill passes, and the rebuild-from-disk - and
        /// the paths are compared against each other for byte identity. A field added to the
        /// set in four places out of five is a divergence nothing would catch.</para>
        /// </summary>
        public void ResetScores()
        {
            Score = 0.0;
            ExperimentAggregateScore = 0.0;
            RunPrecursorQvalue = 1.0;
            RunPeptideQvalue = 1.0;
            RunProteinQvalue = 1.0;
            ExperimentPrecursorQvalue = 1.0;
            ExperimentPeptideQvalue = 1.0;
            ExperimentProteinQvalue = 1.0;
            Pep = 1.0;
        }

        /// <summary>
        /// Returns the effective run-level q-value based on the FDR control level.
        /// </summary>
        public double EffectiveRunQvalue(FdrLevel level)
        {
            switch (level)
            {
                case FdrLevel.Precursor:
                    return RunPrecursorQvalue;
                case FdrLevel.Peptide:
                    return RunPeptideQvalue;
                case FdrLevel.Both:
                    return Math.Max(RunPrecursorQvalue, RunPeptideQvalue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        /// <summary>
        /// Returns the effective experiment-level q-value based on the FDR control level.
        /// </summary>
        public double EffectiveExperimentQvalue(FdrLevel level)
        {
            switch (level)
            {
                case FdrLevel.Precursor:
                    return ExperimentPrecursorQvalue;
                case FdrLevel.Peptide:
                    return ExperimentPeptideQvalue;
                case FdrLevel.Both:
                    return Math.Max(ExperimentPrecursorQvalue, ExperimentPeptideQvalue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }
    }
}
