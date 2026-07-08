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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The five per-row Percolator q-value outputs the score pass computes, in the
    /// exact field order the SVM write-back produces them (issue #4355 step (b),
    /// FdrProjection struct-shrink S0). These are the outputs the lean
    /// <see cref="FdrProjection"/> no longer stores: the score pass hands each row's
    /// values to a per-pass <see cref="IFdrOutputSink"/> instead of overlaying them
    /// onto the struct. <c>RunProteinQvalue</c> is NOT here -- it is produced by
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
        /// <see cref="FdrEntry.EffectiveRunQvalue"/> (and the retired
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
    }

    /// <summary>
    /// Per-pass output sink for the projection score pass (issue #4355 step (b),
    /// FdrProjection struct-shrink S0 / increment C1). The lean
    /// <see cref="FdrProjection"/> no longer carries the six q-value outputs; the
    /// two write-back paths (streaming
    /// <c>PercolatorFdr.ScoreProjectionAndComputeFdrInPlace</c> and direct
    /// <c>PercolatorEngine.ApplyPercolatorResultsToProjection</c>) instead call
    /// <see cref="Accept"/> per row, in the existing nested (file, row) order, handing
    /// each row's freshly computed <see cref="FdrQValues"/> + <see cref="FdrProjection.Score"/>
    /// to a caller-supplied sink. The 2nd pass streams them straight to the
    /// <c>.2nd-pass.fdr_scores.bin</c> sidecar (never stored -> 32 B resident); the 1st
    /// pass keeps only {RunPeptideQ, RunProteinQ} in a parallel
    /// <see cref="FdrProjectionOutputs"/> array (48 B resident) and streams the other
    /// four q-values to a phase-1 partial sidecar (issue #4355 struct-shrink S1). The
    /// sink also OWNS the tail <c>[COUNT]</c> tally (<see cref="Finish"/>),
    /// because it is the only place the q-values are live on BOTH passes once they are
    /// off the struct.
    /// </summary>
    public interface IFdrOutputSink
    {
        /// <summary>
        /// Accept one scored projection row. <paramref name="fileIdx"/> /
        /// <paramref name="rowIdx"/> locate the lean struct row within its per-file
        /// list; <paramref name="entryId"/> / <paramref name="isDecoy"/> identify it;
        /// <paramref name="score"/> + <paramref name="q"/> are the freshly computed
        /// outputs. Called once per row, in nested (file, row) order == the flat
        /// score-pass index order.
        /// </summary>
        void Accept(int fileIdx, int rowIdx, uint entryId, bool isDecoy,
            double score, in FdrQValues q);

        /// <summary>
        /// Finalize the pass: emit the tail <c>[COUNT]</c> lines (per-file pass counts,
        /// total, unique precursors) and flush any deferred per-file output. Called once
        /// at the end of the score pass, replacing the former inline tail block.
        /// </summary>
        void Finish(Action<string> logInfo);
    }

    /// <summary>
    /// The 1st-pass parallel RESIDENT q-value array (issue #4355 step (b), FdrProjection
    /// struct-shrink S1): holds ONLY the two q-values first-pass protein FDR and
    /// compaction still need resident across ALL rows -- <c>RunPeptideQvalue</c> and
    /// <c>RunProteinQvalue</c> (a 2 x f64 = 16 B/row struct array, taking the 1st-pass
    /// resident projection from 80 B to 48 B). Indexed by (fileIdx, rowIdx) against
    /// <see cref="FdrProjectionSet.PerFile"/>. The OTHER FOUR q-values the score pass
    /// produces (<c>RunPrecursorQvalue</c>, <c>ExperimentPrecursorQvalue</c>,
    /// <c>ExperimentPeptideQvalue</c>, <c>Pep</c>) are NO LONGER resident: the 1st-pass
    /// storing sink streams them straight to the phase-1 partial
    /// <c>.1st-pass.fdr_scores.bin</c> during the score pass and never keeps them (S0
    /// kept all six here; S1 drops four to disk). <c>RunPeptideQvalue</c> is stored during
    /// the score pass via <see cref="SetRunPeptideQvalue"/>; <c>RunProteinQvalue</c> is
    /// filled by first-pass protein FDR via <see cref="SetRunProteinQvalue"/> (until then
    /// it holds the 1.0 default) and is applied to the sidecar by the phase-2
    /// <c>[52..60]</c> patch. Kept as a jagged struct array (not a <c>List</c>) so
    /// <c>RunProteinQvalue</c> can be written in place.
    /// </summary>
    public sealed class FdrProjectionOutputs
    {
        // [fileIdx][rowIdx]; a mutable 2-field struct so RunProteinQvalue can be set in
        // place after the score pass, matching the FdrEntry oracle's post-score
        // protein-FDR write onto the resident stub.
        private readonly QValues[][] _rows;

        public FdrProjectionOutputs(FdrProjectionSet projections)
        {
            if (projections == null) throw new ArgumentNullException(nameof(projections));
            var perFile = projections.PerFile;
            _rows = new QValues[perFile.Count][];
            for (int f = 0; f < perFile.Count; f++)
                _rows[f] = new QValues[perFile[f].Value.Count];
        }

        /// <summary>
        /// Store a row's run peptide q-value (the protein-FDR detected gate, the
        /// best-peptide reduction, and compaction all need it resident across the whole
        /// pass) and reset its <c>RunProteinQvalue</c> to the 1.0 default (mirrors the
        /// fresh <see cref="FdrEntry"/>'s pre-protein-FDR value); first-pass protein FDR
        /// overwrites it via <see cref="SetRunProteinQvalue"/>. The other four q-values
        /// are streamed to the phase-1 sidecar by the sink, not stored here.
        /// </summary>
        public void SetRunPeptideQvalue(int fileIdx, int rowIdx, double runPeptideQvalue)
        {
            var file = _rows[fileIdx];
            file[rowIdx].RunPeptideQvalue = runPeptideQvalue;
            file[rowIdx].RunProteinQvalue = 1.0;
        }

        /// <summary>Run peptide q-value (protein-FDR detected gate + compaction + best-peptide reduction).</summary>
        public double RunPeptideQvalue(int fileIdx, int rowIdx) => _rows[fileIdx][rowIdx].RunPeptideQvalue;

        /// <summary>Run protein q-value (written by first-pass protein FDR; read by compaction + the phase-2 sidecar patch).</summary>
        public double RunProteinQvalue(int fileIdx, int rowIdx) => _rows[fileIdx][rowIdx].RunProteinQvalue;

        /// <summary>Set the first-pass protein-FDR run protein q-value for a row.</summary>
        public void SetRunProteinQvalue(int fileIdx, int rowIdx, double runProteinQvalue) =>
            _rows[fileIdx][rowIdx].RunProteinQvalue = runProteinQvalue;

        /// <summary>
        /// The two per-row q-values the 1st pass keeps resident (issue #4355 struct-shrink
        /// S1). A mutable struct so <c>RunProteinQvalue</c> can be filled in place after
        /// protein FDR without re-allocating.
        /// </summary>
        public struct QValues
        {
            public double RunPeptideQvalue;
            public double RunProteinQvalue;
        }
    }
}
