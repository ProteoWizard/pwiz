/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Thin (32-byte) projection of an <see cref="FdrEntry"/> carrying only the
    /// scalar slice the FDR peak's Percolator SVM competition reads: identity + the
    /// drive fields + the single <see cref="Score"/> the SVM writes (issue #4355 step
    /// (b), FdrProjection struct-shrink S0 / increment C1). The six per-row q-value
    /// OUTPUTS the score pass produces no longer live on the struct: the write-back
    /// hands each row's values to a per-pass <see cref="IFdrOutputSink"/> as an
    /// <see cref="FdrQValues"/> value that both passes stream straight to the per-file
    /// <c>.fdr_scores.bin</c> sidecar -- never resident (issue #4355 struct-shrink S2;
    /// S1 kept a 16 B/row 1st-pass {RunPeptideQ, RunProteinQ} array, which first-pass
    /// protein FDR + compaction now re-read off the sidecar instead). Dropping the
    /// q-value outputs takes the resident peak buffer from 80 B to this 32 B on both
    /// passes. Replaces the full <see cref="FdrEntry"/> stub buffer that was
    /// held resident across first-pass Percolator + protein FDR + the 1st-pass sidecar
    /// write + compaction; full <see cref="FdrEntry"/> survivors are reloaded from
    /// parquet + the sidecar after compaction (see <c>FirstJoinTask</c>). Held in a
    /// <c>List</c> so the backing store is a contiguous struct array (no per-row object
    /// header, no per-row <c>ModifiedSequence</c> string -- the string is interned once
    /// into the <see cref="FdrProjectionSet.PeptideById"/> table and referenced here by
    /// <see cref="PeptideId"/>).
    ///
    /// Field-lifecycle rationale (design §1/§2a): every RT/bounds/heavy field is
    /// untouched until AFTER compaction and is reload-obtained for the (small)
    /// survivor set, so the peak buffer needs only this scalar projection.
    /// <see cref="CoelutionSum"/> is an f64 because best-per-precursor ranks
    /// training candidates on it BEFORE any Score exists (risk #2); it must not be
    /// narrowed. A <c>readonly struct</c>: <see cref="Score"/> is populated by
    /// whole-element replacement (<see cref="WithScore"/>) on the backing array, never
    /// in-place mutation.
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

        // --- Drive value + SVM output (16 bytes: 2 x f64) ---

        /// <summary>Coelution sum (parquet <c>fragment_coelution_sum</c> = Features[0]); f64.</summary>
        public readonly double CoelutionSum;

        /// <summary>SVM discriminant score (Percolator output).</summary>
        public readonly double Score;

        /// <summary>
        /// Lean constructor (issue #4355 struct-shrink S0): identity/drive +
        /// <see cref="CoelutionSum"/> + <see cref="Score"/>. The q-value outputs are
        /// no longer stored on the struct -- they flow through an
        /// <see cref="IFdrOutputSink"/> during the score pass (see the type remarks).
        /// A freshly-loaded (not-yet-scored) row carries the <see cref="FdrEntry"/>
        /// default <c>Score</c> of 0.
        /// </summary>
        public FdrProjection(
            uint entryId, uint parquetIndex, int peptideId, ushort fileIdx,
            byte charge, bool isDecoy, double coelutionSum, double score)
        {
            EntryId = entryId;
            ParquetIndex = parquetIndex;
            PeptideId = peptideId;
            FileIdx = fileIdx;
            Charge = charge;
            IsDecoy = isDecoy;
            CoelutionSum = coelutionSum;
            Score = score;
        }

        /// <summary>
        /// Return a copy with <see cref="Score"/> replaced (the SVM discriminant the
        /// write-back assigns). Whole-element replacement on the readonly struct's
        /// backing array, the same discipline the retired six-arg
        /// <c>WithPercolatorResults</c> used -- the q-value outputs it also overlaid
        /// now go to the <see cref="IFdrOutputSink"/> instead.
        /// </summary>
        public FdrProjection WithScore(double score)
        {
            return new FdrProjection(
                EntryId, ParquetIndex, PeptideId, FileIdx, Charge, IsDecoy, CoelutionSum, score);
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

        /// <summary>
        /// Per-file row counts when this set is COUNTS-ONLY (issue #4355 struct-shrink S3,
        /// Stage B): the 1st-pass streaming score path holds no resident <see cref="FdrProjection"/>
        /// rows at all -- it streams identity + features straight from parquet -- so the set
        /// carries only the ordered file names + each file's row count. <c>null</c> on the
        /// normal / 2nd-pass resident set, where the count is <c>PerFile[f].Value.Count</c>.
        /// </summary>
        private readonly int[] _leanRowCounts;

        public FdrProjectionSet(
            List<KeyValuePair<string, List<FdrProjection>>> perFile, string[] peptideById)
            : this(perFile, peptideById, null)
        {
        }

        private FdrProjectionSet(
            List<KeyValuePair<string, List<FdrProjection>>> perFile, string[] peptideById,
            int[] leanRowCounts)
        {
            PerFile = perFile;
            PeptideById = peptideById;
            _leanRowCounts = leanRowCounts;
        }

        /// <summary>
        /// A COUNTS-ONLY set: the ordered file names + each file's parquet row count, with NO
        /// resident <see cref="FdrProjection"/> rows and an empty <see cref="PeptideById"/>
        /// (issue #4355 struct-shrink S3, Stage B). Used by the 1st-pass streaming score path,
        /// which streams every row (identity + features) straight from parquet, so the O(rows)
        /// projection buffer is never allocated. Downstream first-pass protein FDR / compaction /
        /// survivor reload read only the file names (Stage A already moved them off the rows), and
        /// the score-pass sink reads per-file counts via <see cref="RowCount"/>.
        /// </summary>
        public static FdrProjectionSet CountsOnly(IReadOnlyList<string> fileNames, IReadOnlyList<int> rowCounts)
        {
            if (fileNames == null) throw new ArgumentNullException(nameof(fileNames));
            if (rowCounts == null) throw new ArgumentNullException(nameof(rowCounts));
            if (fileNames.Count != rowCounts.Count)
                throw new ArgumentException(@"fileNames and rowCounts must be the same length");
            var perFile = new List<KeyValuePair<string, List<FdrProjection>>>(fileNames.Count);
            var counts = new int[fileNames.Count];
            for (int f = 0; f < fileNames.Count; f++)
            {
                perFile.Add(new KeyValuePair<string, List<FdrProjection>>(
                    fileNames[f], new List<FdrProjection>()));
                counts[f] = rowCounts[f];
            }
            return new FdrProjectionSet(perFile, Array.Empty<string>(), counts);
        }

        /// <summary>
        /// Row count for a file: the resident row-list count on a normal / 2nd-pass set, or the
        /// stored per-file count on a <see cref="CountsOnly"/> set (whose row lists are empty).
        /// The score-pass sink keys its per-file sidecar flush on this, so both shapes work.
        /// </summary>
        public int RowCount(int fileIdx)
        {
            return _leanRowCounts != null ? _leanRowCounts[fileIdx] : PerFile[fileIdx].Value.Count;
        }

        /// <summary>Total projection rows across all files.</summary>
        public int TotalRows
        {
            get
            {
                int n = 0;
                if (_leanRowCounts != null)
                {
                    foreach (int c in _leanRowCounts)
                        n += c;
                    return n;
                }
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
        /// The lean struct carries <see cref="FdrEntry.Score"/> through (0 on the
        /// straight-through path; overwritten by the SVM write-back's
        /// <see cref="FdrProjection.WithScore"/>); the q-value OUTPUTS are no longer
        /// copied onto the struct (issue #4355 struct-shrink S0) -- they flow through
        /// the per-pass <see cref="IFdrOutputSink"/> instead.
        ///
        /// <paramref name="parquetRowResolver"/> selects how each row's
        /// <see cref="FdrProjection.ParquetIndex"/> is resolved -- the ONLY axis on
        /// which the 1st and 2nd pass differ (issue #4374):
        /// <list type="bullet">
        /// <item><c>null</c> (1st pass): <c>ParquetIndex = entry.ParquetIndex</c>, the
        /// stub's own position in its ORIGINAL <c>.scores.parquet</c> -- unchanged.</item>
        /// <item>non-<c>null</c> (2nd pass): the resolver maps each file name to that
        /// file's RECONCILED-parquet <c>(entry_id, charge, scan) -&gt; row</c> table
        /// (built by <c>Pass2FdrSidecar.BuildReconciledIdentityToRow</c>), and
        /// <c>ParquetIndex</c> is set to <c>row</c> for the entry's identity, or
        /// <see cref="uint.MaxValue"/> when the identity is absent (-&gt; basic-feature
        /// fallback in the streaming score pass). Baking the reconciled row makes the
        /// streamed feature lookup byte-identical to the resident identity binding AND
        /// keeps the scan-omitted projection sort valid, because the reconciled parquet
        /// is written <c>(entry_id, charge, scan)</c>-sorted so the row is
        /// scan-monotonic within a <c>(entry_id, charge)</c> group.</item>
        /// </list>
        ///
        /// <paramref name="releaseStubs"/> (1st pass only): when <c>true</c>, each
        /// file's <see cref="FdrEntry"/> stub list is <c>Clear()/TrimExcess()</c>ed the
        /// moment its projection rows are built, so the full projection never coexists
        /// with the full stub buffer -- this removes the "projection built" memory spike
        /// (issue #4355 step b (iv-b)). The global distinct-peptide id assignment runs
        /// BEFORE any release, so the ordinal-id invariant is unaffected. The 2nd pass
        /// leaves it <c>false</c>: its survivor buffer must stay resident for Stage 7/8.
        /// </summary>
        public static FdrProjectionSet BuildFromEntries(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            Func<string, IReadOnlyDictionary<(uint, byte, uint), uint>> parquetRowResolver = null,
            bool releaseStubs = false)
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
            distinct.Sort(StringComparer.Ordinal); // Array.Sort OK: distinct is de-duplicated (grow-only via idByPeptide), no two strings are equal, so the Ordinal comparer never ties.
            var peptideById = distinct.ToArray();
            for (int id = 0; id < peptideById.Length; id++)
                idByPeptide[peptideById[id]] = id;

            var perFile = new List<KeyValuePair<string, List<FdrProjection>>>(perFileEntries.Count);
            ushort fileIdx = 0;
            foreach (var kvp in perFileEntries)
            {
                // 2nd pass (resolver supplied): resolve this file's
                // (entry_id, charge, scan) -> reconciled-parquet row map ONCE, one
                // file at a time so no more than one file's map is resident. 1st pass
                // (resolver null): the map stays null and the stub's own ParquetIndex
                // (its original-parquet position) is carried through unchanged.
                IReadOnlyDictionary<(uint, byte, uint), uint> rowByIdentity =
                    parquetRowResolver?.Invoke(kvp.Key);

                var rows = new List<FdrProjection>(kvp.Value.Count);
                foreach (var e in kvp.Value)
                {
                    int peptideId = idByPeptide[e.ModifiedSequence ?? string.Empty];
                    uint parquetIndex;
                    if (rowByIdentity == null)
                        parquetIndex = e.ParquetIndex;
                    else if (!rowByIdentity.TryGetValue(
                                 (e.EntryId, e.Charge, e.ScanNumber), out parquetIndex))
                        parquetIndex = uint.MaxValue;
                    rows.Add(new FdrProjection(
                        e.EntryId, parquetIndex, peptideId, fileIdx, e.Charge, e.IsDecoy,
                        e.CoelutionSum, e.Score));
                }
                perFile.Add(new KeyValuePair<string, List<FdrProjection>>(kvp.Key, rows));
                fileIdx++;

                // (iv-b) 1st-pass incremental stub release: drop this file's FdrEntry
                // stubs (and their per-row modseq strings) the instant its projection
                // rows exist, so the resident set is projection[0..f] + FdrEntry[f..N]
                // and never the full projection AND full stub buffer at once. Peak is
                // maximal at the first file (all stubs, ~no projection) and shrinks
                // thereafter, so the "projection built" spike disappears. The distinct
                // peptide ids were assigned globally above, so this does not touch them.
                if (releaseStubs)
                {
                    kvp.Value.Clear();
                    kvp.Value.TrimExcess();
                }
            }

            return new FdrProjectionSet(perFile, peptideById);
        }

        /// <summary>
        /// Streaming builder that produces a byte-identical <see cref="FdrProjectionSet"/>
        /// without ever materializing the fat <see cref="FdrEntry"/> stub buffer. The
        /// 191M-row stub rematerialize (PerFileScoringTask) cost ~53 GB on an 82-file
        /// Astral run purely to be converted here into 32 B rows; feeding parquet scalars
        /// straight in drops that to ~6 GB.
        ///
        /// Parity invariant: <see cref="BuildFromEntries"/> assigns
        /// <see cref="FdrProjection.PeptideId"/> as the Ordinal rank of the modified
        /// sequence among the distinct set across ALL files. A streaming pass cannot know
        /// that rank up front, so ids are assigned in INSERTION order while rows arrive and
        /// remapped to the ordinal rank in <see cref="Build"/>. Row order, file order and
        /// <see cref="FdrProjection.ParquetIndex"/> are unchanged, so the result is
        /// element-for-element identical to <see cref="BuildFromEntries"/>.
        ///
        /// NOT thread-safe, by design: <see cref="FdrProjection.FileIdx"/> and the
        /// per-file <see cref="FdrProjection.ParquetIndex"/> running counts are only
        /// meaningful if files are added in a deterministic sequence. Callers must drive
        /// BeginFile/AddRow/EndFile from a single thread (PerFileScoringTask does, after
        /// its per-file scoring fan-out has already joined).
        /// </summary>
        public sealed class Builder
        {
            private readonly Dictionary<string, int> _insertionIdByPeptide =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly List<string> _distinctByInsertion = new List<string>();
            private readonly List<KeyValuePair<string, List<FdrProjection>>> _perFile =
                new List<KeyValuePair<string, List<FdrProjection>>>();
            private List<FdrProjection> _rows;
            private ushort _fileIdx;

            /// <summary>
            /// Open a file's row list. Files must be added in the same order the join
            /// expects (the scoring order), which is what fixes <see cref="FdrProjection.FileIdx"/>.
            /// </summary>
            public void BeginFile(string fileName, int capacityHint = 0)
            {
                _rows = capacityHint > 0
                    ? new List<FdrProjection>(capacityHint)
                    : new List<FdrProjection>();
                _perFile.Add(new KeyValuePair<string, List<FdrProjection>>(fileName, _rows));
            }

            /// <summary>
            /// Append one parquet row of the open file. <see cref="FdrProjection.ParquetIndex"/>
            /// is the running per-file row ordinal, matching
            /// <c>LoadFdrStubsFromParquet</c>'s <c>ParquetIndex = stubs.Count</c>.
            /// <see cref="FdrProjection.Score"/> stays 0 -- the FDR pass writes it back.
            /// </summary>
            public void AddRow(uint entryId, byte charge, bool isDecoy, double coelutionSum,
                string modifiedSequence)
            {
                string modseq = modifiedSequence ?? string.Empty;
                if (!_insertionIdByPeptide.TryGetValue(modseq, out int insertionId))
                {
                    insertionId = _distinctByInsertion.Count;
                    _insertionIdByPeptide[modseq] = insertionId;
                    _distinctByInsertion.Add(modseq);
                }
                _rows.Add(new FdrProjection(
                    entryId, (uint)_rows.Count, insertionId, _fileIdx, charge, isDecoy,
                    coelutionSum, 0.0));
            }

            /// <summary>Close the open file and advance <see cref="FdrProjection.FileIdx"/>.</summary>
            public void EndFile()
            {
                _rows = null;
                _fileIdx++;
            }

            /// <summary>
            /// Sort the distinct peptides Ordinal, remap every row's insertion-order
            /// <see cref="FdrProjection.PeptideId"/> to its ordinal rank, and return the set.
            /// The remap table is one int per distinct peptide (~9 MB at 2.3M peptides).
            /// </summary>
            public FdrProjectionSet Build()
            {
                var peptideById = _distinctByInsertion.ToArray();
                Array.Sort(peptideById, StringComparer.Ordinal); // Array.Sort OK: _distinctByInsertion is de-duplicated (grow-only via _insertionIdByPeptide), no two strings are equal, so the Ordinal comparer never ties -- the same argument BuildFromEntries makes for its List.Sort.

                var ordinalIdByPeptide =
                    new Dictionary<string, int>(peptideById.Length, StringComparer.Ordinal);
                for (int id = 0; id < peptideById.Length; id++)
                    ordinalIdByPeptide[peptideById[id]] = id;

                var remap = new int[_distinctByInsertion.Count];
                for (int insertionId = 0; insertionId < remap.Length; insertionId++)
                    remap[insertionId] = ordinalIdByPeptide[_distinctByInsertion[insertionId]];

                foreach (var kvp in _perFile)
                {
                    var rows = kvp.Value;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        rows[i] = new FdrProjection(
                            r.EntryId, r.ParquetIndex, remap[r.PeptideId], r.FileIdx,
                            r.Charge, r.IsDecoy, r.CoelutionSum, r.Score);
                    }
                }

                return new FdrProjectionSet(_perFile, peptideById);
            }
        }
    }
}
