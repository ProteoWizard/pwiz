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

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// One <c>.fdr_experiment.bin</c> record's payload: the EXPERIMENT-scope statistics for one
    /// DISTINCT entry_id - the two experiment q-values, the experiment protein q-value, and the
    /// per-entry score the experiment competitions ranked on. The counterpart to
    /// <see cref="FdrScoreRecord"/>, which carries the RUN-scope statistics of one observation.
    ///
    /// <para>These four columns are a property of the PRECURSOR across the whole analysis, not
    /// of any one run: <c>entry_id</c> is a base precursor id with the decoy bit in its high
    /// bit (<c>PercolatorEntry.BASE_ID_MASK</c>), so the same precursor carries the same
    /// entry_id in every file, and one experiment competition assigns it one value. Persisting
    /// them beside the run-scope columns therefore wrote the same number once per run the
    /// precursor appeared in - which is what made the 1st-pass sidecars 52.3 GB at 257 files
    /// against 0.44 GB for one experiment-wide file (issue #4486).</para>
    /// </summary>
    public readonly struct FdrExperimentRecord
    {
        public readonly uint EntryId;
        public readonly double ExperimentPrecursorQvalue;
        public readonly double ExperimentPeptideQvalue;

        /// <summary>
        /// Picked-protein q-value for this entry's peptide. Assigned by modified sequence over
        /// every entry whether or not it passed anything (<c>ProteinFdr.PropagateProteinQvalues</c>),
        /// so it is experiment-scope for the same reason the q-values beside it are: one
        /// parsimony + picked-protein FDR runs over the pooled detected set and one value per
        /// peptide reaches every entry in every file. Which PASS a value came from is recorded
        /// by the file it is written to, not by a second field.
        /// </summary>
        public readonly double ExperimentProteinQvalue;

        /// <summary>
        /// The per-entry score the EXPERIMENT-scope competitions ranked this entry on
        /// (issue #4522). <see cref="FdrScoreRecord.Score"/> is the per-ROW SVM discriminant,
        /// which is the quantity the RUN-scope q-values compete on; the experiment scope instead
        /// competes on a per-entry roll-up across runs, and without it persisted no consumer can
        /// re-gate at experiment scope - it has to rebuild the roll-up itself and branch on
        /// <c>OSPREY_EXPERIMENT_AGG</c>, which silently gives the wrong answer on exactly the
        /// arms where the aggregation is under study.
        ///
        /// <para>Under the default aggregation this is the max over the entry's rows across all
        /// runs; under mean-best-N it is <c>TargetDecoyCompetition.ComputeBaseIdMeanBestN</c>'s
        /// value for the entry. Every row of an entry carried the same value in both modes even
        /// when it was stored per row, which is what makes one record per entry_id lossless.
        /// See <c>PercolatorQValues.ComputeExperimentAggregateScoreMap</c>.</para>
        ///
        /// <para>Not a general q-to-score inverse: the best-of-runs clamp
        /// (<c>ClampExperimentQToBestRunFlat</c>, issue #4390) floors an experiment q up to a RUN
        /// q, so after clamping the experiment q is not a monotone function of this score. This
        /// field is the score its competition ranked on, which is what a score-space acceptance
        /// boundary needs; it is not a way to turn any q threshold back into a score
        /// threshold.</para>
        /// </summary>
        public readonly double ExperimentAggregateScore;

        /// <summary>
        /// Posterior error probability for this entry's identification: 
        /// <c>PepEstimator.PosteriorError</c> of the score its base_id's experiment-wide
        /// target/decoy competition won on. Real on the WINNING entry_id of each base_id and 1.0
        /// on the losing label, which lost the competition and so has no posterior error of its
        /// own.
        ///
        /// <para>Experiment-scope for exactly the reason the three q-values above are: one
        /// competition over the whole analysis assigns the precursor one value, and it is the
        /// same in every run the precursor appears in. The WINNING RUN is where the maximum
        /// happened to occur, not part of the value - which is why no file is recorded here.
        /// </para>
        ///
        /// <para>It used to live on the per-observation <see cref="FdrScoreRecord"/> instead,
        /// real on the winning run's row and 1.0 on every other observation of the same
        /// precursor. That 1.0 was not a probability but a sentinel meaning "not the row the
        /// estimate was computed on" - one fact materialized across ~933K slots on a 3-file run.
        /// Worse, it was unknowable until the whole experiment had been folded, so the 2nd pass
        /// re-opened and rewrote every per-run sidecar afterwards; that rewrite is what broke
        /// those files' immutability and forced the experiment-wide stage to hold write access to
        /// output it does not own (issue #4486).</para>
        /// </summary>
        public readonly double Pep;

        public FdrExperimentRecord(
            uint entryId,
            double experimentPrecursorQvalue, double experimentPeptideQvalue,
            double experimentProteinQvalue, double experimentAggregateScore,
            double pep = 1.0)
        {
            EntryId = entryId;
            ExperimentPrecursorQvalue = experimentPrecursorQvalue;
            ExperimentPeptideQvalue = experimentPeptideQvalue;
            ExperimentProteinQvalue = experimentProteinQvalue;
            ExperimentAggregateScore = experimentAggregateScore;
            Pep = pep;
        }
    }
}
