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

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// What one run's own first-pass model (<see cref="PerFileFirstPassModel"/>) leaves for its
    /// second pass: the final round's model, the calibration map its scores went through, and
    /// the run's protein-best column with its floor. The second pass scores a moved row with
    /// <see cref="Model"/>, appending the row's protein-best value (the floor for an entry the
    /// run had no row for), and puts the score through <see cref="Calibrate"/>, so the row lands
    /// on the scale of the run's first-pass scores.
    /// </summary>
    public sealed class RunFirstPassModel
    {
        /// <param name="model">The final round's model: fold weights or trees, and the standardizer.</param>
        /// <param name="calibrationThreshold">The raw score the calibration maps to 0.</param>
        /// <param name="calibrationDenominator">The raw score distance the calibration maps to 1.</param>
        /// <param name="proteinBestColumn">The run's protein-best value per row, in row order, or
        /// null when the model was trained without the column.</param>
        /// <param name="proteinBestFloor">The value of an entry the run had no row for.</param>
        public RunFirstPassModel(PercolatorResults model, double calibrationThreshold, double calibrationDenominator,
            double[] proteinBestColumn, double proteinBestFloor)
        {
            if (!(calibrationDenominator > 0))
                throw new ArgumentOutOfRangeException(nameof(calibrationDenominator), @"The calibration denominator must be positive.");
            Model = model;
            CalibrationThreshold = calibrationThreshold;
            CalibrationDenominator = calibrationDenominator;
            ProteinBestColumn = proteinBestColumn;
            ProteinBestFloor = proteinBestFloor;
        }

        /// <summary>The final round's model.</summary>
        public PercolatorResults Model { get; }

        /// <summary>The raw score the calibration maps to 0: the lowest target passing the run's
        /// precursor q, or the run's best decoy when no target passes; far above every raw score
        /// when the run was excluded from the experiment competition.</summary>
        public double CalibrationThreshold { get; }

        /// <summary>The threshold less the median best decoy per entry; the run's score range
        /// when the run was excluded.</summary>
        public double CalibrationDenominator { get; }

        /// <summary>The run's protein-best value per row, in row order, or null.</summary>
        public double[] ProteinBestColumn { get; }

        /// <summary>The value of an entry the run had no first-pass row for.</summary>
        public double ProteinBestFloor { get; }

        /// <summary>Whether the model was trained with the protein-best column.</summary>
        public bool ScoresProteinBest => ProteinBestFeature.IsAppendedTo(Model?.FeatureNames);

        /// <summary>The calibrated score of a raw model score, the map the run's first-pass scores went through.</summary>
        public double Calibrate(double rawScore)
        {
            return (rawScore - CalibrationThreshold) / CalibrationDenominator;
        }
    }
}
