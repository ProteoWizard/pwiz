/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// One per-file <c>.fdr_scores.bin</c> record's payload: the RUN-scope statistics for one
    /// OBSERVATION - entry_id + SVM score + the two run q-values + the detection apex RT.
    /// Decoupled from any resident buffer (issue #4355 struct-shrink S0), because the lean
    /// <c>FdrProjection</c> does not carry the q-value outputs, so the sidecar writers assemble
    /// records
    /// of this shape and hand them to
    /// <see cref="FdrScoresSidecar.Write(string, System.Collections.Generic.IReadOnlyList{FdrScoreRecord}, FdrScoresSidecar.Pass)"/>.
    /// The 36-byte layout stays single-sourced through <c>FdrScoresSidecar.WriteRecord</c>.
    ///
    /// <para>The four EXPERIMENT-scope columns this struct used to carry -
    /// <c>experiment_precursor_qvalue</c>, <c>experiment_peptide_qvalue</c>,
    /// <c>experiment_protein_qvalue</c> and <c>experiment_aggregate_score</c> - moved to
    /// <see cref="FdrExperimentRecord"/> at format v5 (issue #4486). They are one value per
    /// DISTINCT entry_id for the whole analysis, so persisting them per observation wrote the
    /// same number once per run the precursor appears in: 52.3 GB of 1st-pass sidecars at 257
    /// files against 0.44 GB for the experiment-wide file that replaces the duplication. The
    /// split is by SCOPE, and it is what makes a per-file sidecar immutable - the experiment
    /// columns were the only ones that could not be known when the record was written, and
    /// they are the reason the file used to be rewritten twice after it was created.</para>
    ///
    /// <para>Splitting the struct rather than only the byte layout is deliberate: a reader that
    /// still wants an experiment column now fails to COMPILE. A byte-offset reader left behind
    /// by a layout-only change compiles, runs, and silently decodes whatever field now occupies
    /// the offset it remembers.</para>
    /// </summary>
    public readonly struct FdrScoreRecord
    {
        public readonly uint EntryId;
        public readonly double Score;
        public readonly double RunPrecursorQvalue;
        public readonly double RunPeptideQvalue;

        /// <summary>
        /// This observation's detection apex retention time, in minutes - the same
        /// <c>apex_rt</c> the file's <c>.scores.parquet</c> row carries (format v7, issue #4522).
        ///
        /// <para>It is here because it is a RUN-scope per-observation fact, which is what this
        /// record is. The one consumer that wanted it had to go and get it from the parquet
        /// instead: the model-diagnostics peak co-assignment panel read a whole <c>apex_rt</c>
        /// column per file and joined it to this sidecar POSITIONALLY, asserting the alignment
        /// on entry_id because nothing in either format recorded that contract. That join
        /// allocated 29 MB per file against 4 MB for everything else the panel did, and the
        /// 446-run cohort took private bytes from 10 to 26 GB across it. Carrying the column
        /// costs 8 bytes per record of sequential IO - a 29% larger sidecar - and removes a
        /// large-object column read, an inferred join and the assertion that policed it.</para>
        /// </summary>
        public readonly double ApexRt;

        // NO Pep COLUMN, on either pass (issue #4486). PEP is one value per base_id -
        // PepEstimator.PosteriorError over the single winning observation - and storing it here
        // meant writing that one fact into every observation of the precursor, real on the winner
        // and 1.0 everywhere else. The 1.0 was never a posterior error probability; it was a
        // sentinel meaning "not the row the estimate was computed on", i.e. a materialized
        // left-outer-join. It also could not be known until the whole experiment had been folded,
        // so the 2nd pass re-opened and rewrote every per-run sidecar afterwards, which is what
        // broke these files' immutability and forced the experiment-wide stage to hold write
        // access to output it does not own.
        //
        // The winner fact now lives once on FdrExperimentRecord (Pep + PepWinnerFileIndex) and
        // consumers that want the per-observation view join to it at read time via
        // FdrExperimentRecord.PepForFile. Both passes store it the same way, which they did not
        // before: pass 1 wrote a final value once, pass 2 wrote a placeholder and patched it.

        public FdrScoreRecord(
            uint entryId, double score,
            double runPrecursorQvalue, double runPeptideQvalue, double apexRt)
        {
            EntryId = entryId;
            Score = score;
            RunPrecursorQvalue = runPrecursorQvalue;
            RunPeptideQvalue = runPeptideQvalue;
            ApexRt = apexRt;
        }
    }
}
