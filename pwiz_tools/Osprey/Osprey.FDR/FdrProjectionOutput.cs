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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// The five per-row Percolator q-value outputs the score pass computes, in the
    /// exact field order the SVM write-back produces them (issue #4355 step (b),
    /// FdrProjection struct-shrink S0). These are the outputs the lean
    /// <see cref="FdrProjection"/> no longer stores: the score pass hands each row's
    /// values to a per-pass <see cref="IFdrOutputSink"/> instead of overlaying them
    /// onto the struct. <c>ExperimentProteinQvalue</c> is NOT here -- it is produced by
    /// first-pass protein FDR AFTER the score pass, not by the score pass.
    /// </summary>
    public readonly struct FdrQValues
    {
        public readonly double RunPrecursorQvalue;
        public readonly double RunPeptideQvalue;
        public readonly double ExperimentPrecursorQvalue;
        public readonly double ExperimentPeptideQvalue;
        public readonly double Pep;

        public FdrQValues(
            double runPrecursorQvalue, double runPeptideQvalue,
            double experimentPrecursorQvalue, double experimentPeptideQvalue, double pep)
        {
            RunPrecursorQvalue = runPrecursorQvalue;
            RunPeptideQvalue = runPeptideQvalue;
            ExperimentPrecursorQvalue = experimentPrecursorQvalue;
            ExperimentPeptideQvalue = experimentPeptideQvalue;
            Pep = pep;
        }

        /// <summary>
        /// Effective run-level q-value for the FDR control level, matching
        /// <see cref="FdrRowExtensions.EffectiveRunQvalue{T}"/> (and the retired
        /// <c>FdrProjection.EffectiveRunQvalue</c>) exactly.
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
        /// Effective experiment-level q-value for the FDR control level, matching
        /// <see cref="FdrRowExtensions.EffectiveExperimentQvalue{T}"/> exactly. Used by the
        /// streaming <c>--model-diagnostics</c> accumulator's cross-run experiment
        /// gate, where the entry is off the struct and only these q-values are live.
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

    /// <summary>
    /// Per-pass output sink for the projection score pass (issue #4355 step (b),
    /// FdrProjection struct-shrink S0 / increment C1). The lean
    /// <see cref="FdrProjection"/> no longer carries the six q-value outputs; the
    /// streaming write-back path
    /// (<c>PercolatorScorer.ScoreProjectionAndComputeFdrInPlace</c>) instead calls
    /// <see cref="Accept"/> per row, in the existing nested (file, row) order, handing
    /// each row's freshly computed <see cref="FdrQValues"/> + <see cref="FdrProjection.Score"/>
    /// to a caller-supplied sink. Both passes stream every row straight to the per-file
    /// <c>.fdr_scores.bin</c> sidecar (never stored -> 32 B resident): the 2nd pass writes
    /// the final record, the 1st pass writes a phase-1 record whose <c>experiment_protein_qvalue</c>
    /// first-pass protein FDR patches from disk afterward (issue #4355 struct-shrink S2; S1
    /// still kept a 16 B/row 1st-pass {RunPeptideQ, RunProteinQ} array, since removed). The
    /// sink also OWNS the tail <c>[COUNT]</c> tally (<see cref="Finish"/>),
    /// because it is the only place the q-values are live on BOTH passes once they are
    /// off the struct.
    /// </summary>
    public interface IFdrOutputSink
    {
        /// <summary>
        /// Accept one scored row. <paramref name="fileIdx"/> / <paramref name="rowIdx"/>
        /// locate the row within its per-file list; <paramref name="entryId"/> /
        /// <paramref name="isDecoy"/> / <paramref name="charge"/> / <paramref name="peptide"/>
        /// identify it; <paramref name="score"/> +
        /// <paramref name="experimentAggregateScore"/> + <paramref name="q"/> are the freshly
        /// computed outputs. Called once per row, in nested (file, row) order == the flat
        /// score-pass index order. <paramref name="charge"/> / <paramref name="peptide"/> are
        /// passed in (not read off a resident row) so the sink's [COUNT] tally + streaming
        /// --model-diagnostics accumulator work whether the caller holds a resident projection
        /// (2nd pass) or streams the rows straight from parquet with no resident buffer at all
        /// (1st-pass streaming, issue #4355 struct-shrink S3 Stage B).
        ///
        /// <para><paramref name="experimentAggregateScore"/> is the per-entry score the
        /// experiment-scope competitions ranked this row's entry on (sidecar v4, issue #4522)
        /// - constant across every row of an entry, and equal to <paramref name="score"/> only
        /// on a single-file run whose precursors carry one row each. It rides the sink rather
        /// than <see cref="FdrQValues"/> because it is a score, not a q-value, and rather than
        /// <see cref="FdrProjection"/> because that struct is deliberately lean (issue #4355
        /// S0/S1) and guarded against regrowth.</para>
        /// </summary>
        void Accept(int fileIdx, int rowIdx, uint entryId, bool isDecoy,
            byte charge, string peptide, double score, double experimentAggregateScore,
            in FdrQValues q);

        /// <summary>
        /// Finalize the pass: emit the tail <c>[COUNT]</c> lines (per-file pass counts,
        /// total, unique precursors) and flush any deferred per-file output. Called once
        /// at the end of the score pass, replacing the former inline tail block.
        /// </summary>
        void Finish(Action<string> logInfo);
    }

    /// <summary>
    /// Hand one file's COMPLETE run-scope first-pass output to the caller, at the moment
    /// pass 1 finishes that file. The four values are exactly what the per-file
    /// <c>.1st-pass.fdr_scores.bin</c> stores, and pass 1 computes all four - the score from
    /// the averaged fold model, the two run q-values from
    /// <see cref="PercolatorQValues.ComputePerFileRunQvalues"/> over this file's own rows.
    ///
    /// <para><b>Why the write moved here.</b> The sidecar used to be assembled by the output
    /// sink during pass 2, one whole phase after the values existed. On a 446-file cohort pass
    /// 1 is 55 minutes and pass 2 another 82, and nothing durable was written until the second
    /// of those - so a run interrupted anywhere in the first 137 minutes lost all of it,
    /// including files that had been completely and correctly scored. Writing each file as pass
    /// 1 completes it puts the exposure at ONE in-flight file, and that does not grow with
    /// cohort size.</para>
    ///
    /// <para>Pass 2 is not thereby redundant: its unique product is the experiment-scope
    /// values, which cannot exist before the barrier that turns pass 1's accumulated
    /// competition into the experiment maps. What pass 2 stops doing is reloading features and
    /// recomputing a score it can read back from the file pass 1 just wrote.</para>
    ///
    /// <para>Arrays are the pass's own scratch and are REUSED after this returns, so an
    /// implementation must consume them synchronously rather than retaining them.
    /// <paramref name="fileIndex"/> is the file's position in the score pass's file order,
    /// which is the projection's file order.</para>
    /// </summary>
    public delegate void FileRunScopeSink(string fileName, int fileIndex, int rowCount,
        uint[] entryIds, double[] scores, double[] runPrecursorQvalues, double[] runPeptideQvalues);
}
