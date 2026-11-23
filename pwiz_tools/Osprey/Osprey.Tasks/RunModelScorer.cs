/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// One run's own frozen first-pass model, as its second pass applies it to the rows
    /// reconciliation moved: the model from the run's
    /// <c>&lt;stem&gt;.1st-pass.model.json</c>, the protein-best column from its stub scores
    /// parquet, and the calibration map, so a moved row's score lands on the scale of the run's
    /// first-pass scores. The protein-best value of a row is its entry's first-pass value - what
    /// its siblings scored, which a move of its own peak does not change - and the run's floor for
    /// an entry the run had no first-pass row for, exactly as the analysis-wide model's second
    /// pass appends the column.
    ///
    /// <para>Thread-safe: <see cref="FrozenModelScorer"/> keeps one buffer per thread, and the
    /// rest is read-only.</para>
    /// </summary>
    internal sealed class RunModelScorer
    {
        /// <summary>
        /// The run's model, read from <paramref name="modelPath"/>, with its protein-best column
        /// read from <paramref name="scoresPath"/>, the run's stub scores parquet. Throws
        /// <see cref="InvalidDataException"/> naming the file when either cannot be read: a run
        /// whose moved rows cannot be scored has no second pass.
        /// </summary>
        public static RunModelScorer Load(string modelPath, string scoresPath, PinFeatureSelection features)
        {
            var runModel = FirstPassModelIO.LoadRunModel(modelPath, features);
            var scorer = runModel != null ? FrozenModelScorer.TryCreate(runModel.Model) : null;
            if (scorer == null)
                throw new InvalidDataException(MissingMessage(modelPath));
            ProteinBestRunValues proteinBest = null;
            if (scorer.ScoresProteinBest)
            {
                var entryIds = new List<uint>();
                var values = new List<double>();
                if (!ParquetScoreCache.ReadProteinBestRows(scoresPath, (entryId, value) =>
                    {
                        entryIds.Add(entryId);
                        values.Add(value);
                    }))
                {
                    throw new InvalidDataException(MissingMessage(scoresPath));
                }
                proteinBest = ProteinBestRunValues.FromRows(entryIds, values, runModel.ProteinBestFloor);
            }
            return new RunModelScorer(runModel, scorer, proteinBest, features.FdrFeatureIndices);
        }

        private readonly RunFirstPassModel _runModel;
        private readonly FrozenModelScorer _scorer;
        private readonly ProteinBestRunValues _proteinBest;
        private readonly int[] _fdrIndices;

        private RunModelScorer(RunFirstPassModel runModel, FrozenModelScorer scorer, ProteinBestRunValues proteinBest,
            int[] fdrIndices)
        {
            _runModel = runModel;
            _scorer = scorer;
            _proteinBest = proteinBest;
            _fdrIndices = fdrIndices;
            if (scorer.InputFeatures != fdrIndices.Length)
            {
                // Exception text for internal diagnostics, not displayed to user
                throw new InvalidOperationException(string.Format(
                    @"The run's first-pass model scores {0} features, and this search has {1}.",
                    scorer.InputFeatures, fdrIndices.Length));
            }
        }

        /// <summary>
        /// The run's protein-best value of each entry_id, for its reconciled stub's column, or null
        /// when the model has no protein-best column.
        /// </summary>
        public Func<uint, double> ProteinBestOf => _proteinBest != null ? _proteinBest.ValueFor : null;

        /// <summary>
        /// Score every row of <paramref name="entries"/> the rescore re-scored - each one holding a
        /// full feature vector in memory, a moved, gap-filled or CWT row - with the run's frozen
        /// model, calibrated, into its <see cref="FdrEntry.Score"/>. A row with no features was not
        /// re-scored and keeps its first-pass score from the stub. Returns the rows scored.
        /// </summary>
        public int ScoreRescoredRows(List<FdrEntry> entries)
        {
            int scored = 0;
            var fdrFeatures = new double[_fdrIndices.Length];
            foreach (var entry in entries)
            {
                if (entry.Features == null || entry.Features.Length != ParquetScoreCache.NUM_PIN_FEATURES)
                    continue;
                for (int i = 0; i < _fdrIndices.Length; i++)
                {
                    double v = entry.Features[_fdrIndices[i]];
                    fdrFeatures[i] = double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v;
                }
                entry.Score = _runModel.Calibrate(_scorer.Score(fdrFeatures, entry.EntryId, _proteinBest));
                scored++;
            }
            return scored;
        }

        internal static string MissingMessage(string path)
        {
            return string.Format(
                OspreyTasksResources.RunModelScorer_Load_The_run_s_first_pass_model_could_not_be_read_from__0___Run__1__again_,
                path, OspreyArgNames.TaskText(PerFileScoringTask.TASK_NAME));
        }
    }
}
