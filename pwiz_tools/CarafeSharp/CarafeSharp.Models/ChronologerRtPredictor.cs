/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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

using System.Collections.Generic;
using System.Linq;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Chronologer for every peptide form it can encode, and an AlphaPeptDeep model for the rest (a
    /// modification Chronologer has no token for, a length outside 6 to 50). The fallback's predictions are
    /// carried onto Chronologer's hydrophobic-index scale through iRT: both models' iRT kit fits give the
    /// line from one scale to the other. Owns both models.
    /// </summary>
    public sealed class ChronologerRtPredictor : IRtPredictor
    {
        private readonly ChronologerModel _chronologer;
        private readonly RtModel _fallback;
        private readonly (double Slope, double Intercept) _irt;
        private readonly (double Slope, double Intercept) _fallbackToHi;

        public ChronologerRtPredictor(ChronologerModel chronologer, RtModel fallback)
        {
            _chronologer = chronologer;
            _fallback = fallback;
            _irt = chronologer.FitIrtCalibration();
            var fallbackIrt = fallback.FitIrtCalibration();
            // iRT = a * fallback + b and iRT = c * hi + d, so hi = (a / c) * fallback + (b - d) / c.
            _fallbackToHi = (fallbackIrt.Slope / _irt.Slope, (fallbackIrt.Intercept - _irt.Intercept) / _irt.Slope);
        }

        /// <summary>True for a fine-tuned Chronologer, which predicts the training run's normalized RT.</summary>
        public bool PredictsNormalizedRt
        {
            get { return _chronologer.PredictsNormalizedRt; }
        }

        /// <summary>Peptide forms predicted so far by the fallback model.</summary>
        public int FallbackCount { get; private set; }

        public double[] Predict(IReadOnlyList<PeptideForm> peptides)
        {
            double[] predicted = _chronologer.Predict(peptides);
            int[] rejected = Enumerable.Range(0, predicted.Length).Where(i => double.IsNaN(predicted[i])).ToArray();
            if (rejected.Length > 0)
            {
                double[] fallback = _fallback.Predict(rejected.Select(i => peptides[i]).ToArray());
                for (int k = 0; k < rejected.Length; k++)
                    predicted[rejected[k]] = _fallbackToHi.Slope * fallback[k] + _fallbackToHi.Intercept;
                FallbackCount += rejected.Length;
            }
            return predicted;
        }

        public (double Slope, double Intercept) FitIrtCalibration()
        {
            return _irt;
        }

        public void Dispose()
        {
            _chronologer.Dispose();
            _fallback.Dispose();
        }
    }
}
