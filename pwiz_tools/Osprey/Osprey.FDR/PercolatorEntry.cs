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

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Input entry for Percolator scoring.
    /// </summary>
    public class PercolatorEntry
    {
        /// <summary>
        /// Masks the high bit off an <see cref="EntryId"/> to get its base (precursor)
        /// id: a target and its paired decoy share a base id, which is what
        /// target-decoy competition groups on.
        ///
        /// Lives here rather than on any one consumer because five classes across the
        /// FDR pipeline mask entry ids - competition, sampling, scoring, the streaming
        /// paths and the Stage-5 dumps - and its previous home, the
        /// <c>PercolatorFdr</c> god class, was dissolved by the decomposition in
        /// issue #4468.
        /// </summary>
        internal const uint BASE_ID_MASK = 0x7FFFFFFF;

        /// <summary>Source file name (for per-run FDR).</summary>
        public string FileName { get; set; }

        /// <summary>Modified peptide sequence (for fold grouping and peptide-level FDR).</summary>
        public string Peptide { get; set; }

        /// <summary>Precursor charge state.</summary>
        public byte Charge { get; set; }

        /// <summary>Whether this is a decoy.</summary>
        public bool IsDecoy { get; set; }

        /// <summary>Entry ID for target-decoy pairing (high bit = decoy).</summary>
        public uint EntryId { get; set; }

        /// <summary>
        /// Row index of this observation within its source file's
        /// <c>.scores.parquet</c>. Lets the streaming score pass reload the
        /// 21-feature vector on demand (issue #4355 Phase 4) instead of holding
        /// every entry's <see cref="Features"/> resident for the whole join.
        /// <c>uint.MaxValue</c> marks an appended entry (e.g. Stage 6 gap-fill)
        /// that has no original parquet row.
        /// </summary>
        public uint ParquetIndex { get; set; }

        /// <summary>
        /// The <c>fragment_coelution_sum</c> feature (PIN feature 0), carried as a
        /// resident scalar so best-per-precursor selection can rank observations
        /// without holding the full <see cref="Features"/> vector. Equal to
        /// <c>Features[0]</c> byte-for-byte on the first pass (both come from the
        /// same parquet column / <c>CoelutionScorer</c> assignment).
        /// </summary>
        public double CoelutionSum { get; set; }

        /// <summary>Raw feature values. Null on streaming-path stubs (issue #4355
        /// Phase 4), where the vector is loaded per file from parquet at score
        /// time by <see cref="ParquetIndex"/>.</summary>
        public double[] Features { get; set; }
    }

}
