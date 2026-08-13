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

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// One <c>.fdr_scores.bin</c> record's payload (entry_id + SVM score + 4 q-values +
    /// PEP + run_protein_qvalue + experiment_aggregate_score), decoupled from any resident
    /// buffer (issue #4355 struct-shrink S0). Since the lean <c>FdrProjection</c> no longer
    /// carries the q-value outputs, the projection sidecar writers assemble records of this
    /// shape -- the 1st pass from the lean row's <c>EntryId</c>/<c>Score</c> plus the parallel
    /// outputs array, the 2nd pass from the streamed q-values plus the survivor's
    /// <c>RunProteinQvalue</c> lookup -- and hand them to
    /// <see cref="FdrScoresSidecar.Write(string, System.Collections.Generic.IReadOnlyList{FdrScoreRecord}, FdrScoresSidecar.Pass)"/>.
    /// The 68-byte byte layout stays single-sourced through
    /// <c>FdrScoresSidecar.WriteRecord</c>.
    /// </summary>
    public readonly struct FdrScoreRecord
    {
        public readonly uint EntryId;
        public readonly double Score;
        public readonly double RunPrecursorQvalue;
        public readonly double RunPeptideQvalue;
        public readonly double ExperimentPrecursorQvalue;
        public readonly double ExperimentPeptideQvalue;
        public readonly double Pep;
        public readonly double RunProteinQvalue;

        /// <summary>
        /// The per-entry score the EXPERIMENT-scope competitions ranked this entry on, beside
        /// the experiment q-values it produced (v4). <see cref="Score"/> is the per-ROW SVM
        /// discriminant, which is the quantity the RUN-scope q-values compete on; the
        /// experiment scope instead competes on a per-entry roll-up across runs, and without
        /// it persisted no consumer can re-gate at experiment scope -- it has to rebuild the
        /// roll-up itself and branch on <c>OSPREY_EXPERIMENT_AGG</c>, which silently gives the
        /// wrong answer on exactly the arms where the aggregation is under study.
        ///
        /// <para>Under the default aggregation this is the max over the entry's rows across
        /// all runs; under mean-best-N it is
        /// <c>TargetDecoyCompetition.ComputeBaseIdMeanBestN</c>'s value for the entry. Every
        /// row of an entry carries the same value in both modes, so a consumer reads it and
        /// compares -- no reduction, no branch. See
        /// <c>PercolatorQValues.ComputeExperimentAggregateScoreMap</c>.</para>
        ///
        /// <para>Not a general q-to-score inverse: the best-of-runs clamp
        /// (<c>ClampExperimentQToBestRunFlat</c>, issue #4390) floors an experiment q up to a
        /// RUN q, so after clamping the experiment q is not a monotone function of this score.
        /// This field is the score its competition ranked on, which is what a score-space
        /// acceptance boundary needs; it is not a way to turn any q threshold back into a
        /// score threshold.</para>
        /// </summary>
        public readonly double ExperimentAggregateScore;

        public FdrScoreRecord(
            uint entryId, double score,
            double runPrecursorQvalue, double runPeptideQvalue,
            double experimentPrecursorQvalue, double experimentPeptideQvalue,
            double pep, double runProteinQvalue, double experimentAggregateScore)
        {
            EntryId = entryId;
            Score = score;
            RunPrecursorQvalue = runPrecursorQvalue;
            RunPeptideQvalue = runPeptideQvalue;
            ExperimentPrecursorQvalue = experimentPrecursorQvalue;
            ExperimentPeptideQvalue = experimentPeptideQvalue;
            Pep = pep;
            RunProteinQvalue = runProteinQvalue;
            ExperimentAggregateScore = experimentAggregateScore;
        }
    }
}
