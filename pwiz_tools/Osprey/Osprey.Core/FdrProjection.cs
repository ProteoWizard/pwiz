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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Thin (80-byte) projection of an <see cref="FdrEntry"/> carrying only the
    /// scalar slice the first-pass FDR peak touches: identity + the drive fields
    /// the Percolator SVM competition reads, plus the seven FDR outputs it writes
    /// (issue #4355 step (b) increment ii, Option A). Replaces the full
    /// <see cref="FdrEntry"/> stub buffer that was held resident across first-pass
    /// Percolator + protein FDR + the 1st-pass sidecar write + compaction; full
    /// <see cref="FdrEntry"/> survivors are reloaded from parquet + the sidecar
    /// after compaction (see <c>FirstJoinTask</c>). Held in a <c>List</c> so the
    /// backing store is a contiguous struct array (no per-row object header, no
    /// per-row <c>ModifiedSequence</c> string -- the string is interned once into
    /// the <see cref="FdrProjectionSet.PeptideById"/> table and referenced here by
    /// <see cref="PeptideId"/>).
    ///
    /// Field-lifecycle rationale (design §1/§2a): every RT/bounds/heavy field is
    /// untouched until AFTER compaction and is reload-obtained for the (small)
    /// survivor set, so the peak buffer needs only this scalar projection.
    /// <see cref="CoelutionSum"/> is an f64 because best-per-precursor ranks
    /// training candidates on it BEFORE any Score exists (risk #2); it must not be
    /// narrowed. A <c>readonly struct</c>: the FDR outputs are populated by
    /// whole-element replacement (<see cref="WithPercolatorResults"/> /
    /// <see cref="WithRunProteinQvalue"/>) on the backing array, never in-place
    /// mutation.
    /// </summary>
    public readonly struct FdrProjection
    {
        // --- Identity / drive fields (16 bytes, 8-aligned) ---

        /// <summary>Entry id (base_id with the decoy bit in 0x80000000).</summary>
        public readonly uint EntryId;

        /// <summary>
        /// Row index in the source file's <c>.scores.parquet</c>. Drives the
        /// per-file feature reload (<c>ResolveFeatureRow</c>) and the SVM sort
        /// tiebreak; kept resident rather than derived so the projection can be
        /// sorted without disturbing the row-to-parquet correspondence.
        /// </summary>
        public readonly uint ParquetIndex;

        /// <summary>
        /// Interned modified-sequence id: index into
        /// <see cref="FdrProjectionSet.PeptideById"/>. Ids are assigned in
        /// <see cref="StringComparison.Ordinal"/> order of the distinct modified
        /// sequences (risk #1), so grouping/sorting by <see cref="PeptideId"/>
        /// reproduces the ordinal string order the training subsample depends on.
        /// </summary>
        public readonly int PeptideId;

        /// <summary>
        /// Index of the source file within the per-file grouping. The resident
        /// buffer keeps the per-file grouping (the file name is the group key), so
        /// this is retained per the projection design for O(1) file identity
        /// without the string and for a future flat (un-grouped) layout.
        /// </summary>
        public readonly ushort FileIdx;

        /// <summary>Precursor charge.</summary>
        public readonly byte Charge;

        /// <summary>Target/decoy flag.</summary>
        public readonly bool IsDecoy;

        // --- Drive value + FDR outputs (64 bytes: 8 x f64) ---

        /// <summary>Coelution sum (parquet <c>fragment_coelution_sum</c> = Features[0]); f64.</summary>
        public readonly double CoelutionSum;

        /// <summary>SVM discriminant score (Percolator output).</summary>
        public readonly double Score;

        public readonly double RunPrecursorQvalue;
        public readonly double RunPeptideQvalue;
        public readonly double RunProteinQvalue;
        public readonly double ExperimentPrecursorQvalue;
        public readonly double ExperimentPeptideQvalue;
        public readonly double Pep;

        /// <summary>
        /// Full-field constructor. Mirrors the <see cref="FdrEntry"/> default of
        /// 1.0 for every q-value / PEP when the caller passes those defaults for a
        /// freshly-loaded (not-yet-scored) row.
        /// </summary>
        public FdrProjection(
            uint entryId, uint parquetIndex, int peptideId, ushort fileIdx,
            byte charge, bool isDecoy, double coelutionSum, double score,
            double runPrecursorQvalue, double runPeptideQvalue, double runProteinQvalue,
            double experimentPrecursorQvalue, double experimentPeptideQvalue, double pep)
        {
            EntryId = entryId;
            ParquetIndex = parquetIndex;
            PeptideId = peptideId;
            FileIdx = fileIdx;
            Charge = charge;
            IsDecoy = isDecoy;
            CoelutionSum = coelutionSum;
            Score = score;
            RunPrecursorQvalue = runPrecursorQvalue;
            RunPeptideQvalue = runPeptideQvalue;
            RunProteinQvalue = runProteinQvalue;
            ExperimentPrecursorQvalue = experimentPrecursorQvalue;
            ExperimentPeptideQvalue = experimentPeptideQvalue;
            Pep = pep;
        }

        /// <summary>
        /// Return a copy with the six Percolator SVM outputs overlaid (Score + the
        /// four run/experiment precursor+peptide q-values + PEP), leaving
        /// <see cref="RunProteinQvalue"/> for the later first-pass protein FDR.
        /// Mirrors the fields <c>PercolatorEngine.ApplyPercolatorResults</c> writes
        /// onto an <see cref="FdrEntry"/>.
        /// </summary>
        public FdrProjection WithPercolatorResults(
            double score, double runPrecursorQvalue, double runPeptideQvalue,
            double experimentPrecursorQvalue, double experimentPeptideQvalue, double pep)
        {
            return new FdrProjection(
                EntryId, ParquetIndex, PeptideId, FileIdx, Charge, IsDecoy, CoelutionSum,
                score, runPrecursorQvalue, runPeptideQvalue, RunProteinQvalue,
                experimentPrecursorQvalue, experimentPeptideQvalue, pep);
        }

        /// <summary>Return a copy with <see cref="RunProteinQvalue"/> set (first-pass protein FDR output).</summary>
        public FdrProjection WithRunProteinQvalue(double runProteinQvalue)
        {
            return new FdrProjection(
                EntryId, ParquetIndex, PeptideId, FileIdx, Charge, IsDecoy, CoelutionSum,
                Score, RunPrecursorQvalue, RunPeptideQvalue, runProteinQvalue,
                ExperimentPrecursorQvalue, ExperimentPeptideQvalue, Pep);
        }

        /// <summary>
        /// Effective run-level q-value for the FDR control level, matching
        /// <see cref="FdrEntry.EffectiveRunQvalue"/> exactly.
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
    }

    /// <summary>
    /// The first-pass FDR peak buffer: per-file <see cref="FdrProjection"/> lists
    /// (mirroring the <c>List&lt;KeyValuePair&lt;string, List&lt;FdrEntry&gt;&gt;&gt;</c>
    /// hand-off shape so file order / nested iteration order are preserved for the
    /// index-zip write-back and the per-file feature stream) plus the interned
    /// <see cref="PeptideById"/> modified-sequence table shared by every row.
    /// Built once from the cold hand-off buffer at the join boundary
    /// (issue #4355 step (b) increment ii).
    /// </summary>
    public sealed class FdrProjectionSet
    {
        /// <summary>Per-file projection rows, in the same file/entry order as the source buffer.</summary>
        public List<KeyValuePair<string, List<FdrProjection>>> PerFile { get; }

        /// <summary>
        /// Distinct modified sequences in <see cref="StringComparison.Ordinal"/>
        /// order; <c>PeptideById[id]</c> is the string interned by
        /// <see cref="FdrProjection.PeptideId"/> == <c>id</c>. Retained (not
        /// discarded) because first-pass protein FDR joins peptides to proteins
        /// through the library on this string, and the SVM materializes
        /// <c>PercolatorEntry.Peptide</c> from it.
        /// </summary>
        public string[] PeptideById { get; }

        public FdrProjectionSet(
            List<KeyValuePair<string, List<FdrProjection>>> perFile, string[] peptideById)
        {
            PerFile = perFile;
            PeptideById = peptideById;
        }

        /// <summary>Total projection rows across all files.</summary>
        public int TotalRows
        {
            get
            {
                int n = 0;
                foreach (var kvp in PerFile)
                    n += kvp.Value.Count;
                return n;
            }
        }

        /// <summary>
        /// Build a projection set from the cold per-file <see cref="FdrEntry"/>
        /// hand-off buffer. Assigns <see cref="FdrProjection.PeptideId"/> in
        /// <see cref="StringComparison.Ordinal"/> order of the distinct modified
        /// sequences (0..M-1) -- the single highest byte-identity risk (#1): the
        /// training-subset selection sorts peptide group keys Ordinal, so an id
        /// ordering that reproduces the ordinal string ordering keeps the subsample
        /// (hence the trained SVM, hence every downstream q-value) byte-identical.
        /// The <see cref="FdrEntry"/> FDR-output fields are copied through so a
        /// round-trip of an already-scored buffer preserves them; on the
        /// straight-through path they are the not-yet-scored defaults (Score 0,
        /// q-values / PEP 1.0) that first-pass Percolator then fills.
        /// </summary>
        public static FdrProjectionSet BuildFromEntries(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            if (perFileEntries == null) throw new ArgumentNullException(nameof(perFileEntries));

            // Distinct modified sequences -> ordinal-sorted id. Collect the
            // distinct set first (grow-only), then sort Ordinal and assign
            // 0..M-1 so id order == ordinal string order (risk #1). Only the M
            // distinct strings are held, never a per-row copy.
            var idByPeptide = new Dictionary<string, int>(StringComparer.Ordinal);
            var distinct = new List<string>();
            foreach (var kvp in perFileEntries)
            {
                foreach (var e in kvp.Value)
                {
                    string modseq = e.ModifiedSequence ?? string.Empty;
                    if (!idByPeptide.ContainsKey(modseq))
                    {
                        idByPeptide[modseq] = 0; // placeholder; real id assigned after the ordinal sort
                        distinct.Add(modseq);
                    }
                }
            }
            distinct.Sort(StringComparer.Ordinal);
            var peptideById = distinct.ToArray();
            for (int id = 0; id < peptideById.Length; id++)
                idByPeptide[peptideById[id]] = id;

            var perFile = new List<KeyValuePair<string, List<FdrProjection>>>(perFileEntries.Count);
            ushort fileIdx = 0;
            foreach (var kvp in perFileEntries)
            {
                var rows = new List<FdrProjection>(kvp.Value.Count);
                foreach (var e in kvp.Value)
                {
                    int peptideId = idByPeptide[e.ModifiedSequence ?? string.Empty];
                    rows.Add(new FdrProjection(
                        e.EntryId, e.ParquetIndex, peptideId, fileIdx, e.Charge, e.IsDecoy,
                        e.CoelutionSum, e.Score, e.RunPrecursorQvalue, e.RunPeptideQvalue,
                        e.RunProteinQvalue, e.ExperimentPrecursorQvalue,
                        e.ExperimentPeptideQvalue, e.Pep));
                }
                perFile.Add(new KeyValuePair<string, List<FdrProjection>>(kvp.Key, rows));
                fileIdx++;
            }

            return new FdrProjectionSet(perFile, peptideById);
        }
    }
}
