/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// The ordered set of OspreySharp scoring feature calculators, mirroring
    /// Skyline's <c>PeakFeatureCalculator.CALCULATORS</c>. The array index IS the
    /// parity-critical PIN feature index -- the position written into the
    /// <c>double[21]</c> feature vector and the column order in the
    /// <c>.cs_features.tsv</c> dump. This is the single place that order lives as
    /// data.
    ///
    /// While the inline <c>ScoreCandidate</c> feature block is being decomposed one
    /// family at a time, the slots whose feature is still computed inline are null;
    /// <see cref="Get"/> returns null for those and the harness keeps filling them
    /// inline. When every feature has been extracted the array is null-free and is
    /// the canonical ordered calculator set.
    /// </summary>
    public static class OspreyFeatureCalculators
    {
        /// <summary>Number of PIN features (the feature-vector width).</summary>
        public const int FeatureCount = 21;

        private static readonly IOspreyFeatureCalculator[] _calculators = CreateCalculators();

        private static IOspreyFeatureCalculator[] CreateCalculators()
        {
            var calculators = new IOspreyFeatureCalculator[FeatureCount];

            // Coelution family: pairwise fragment-correlation sum / max / count.
            calculators[0] = new FragmentCoelutionSumCalc();
            calculators[1] = new FragmentCoelutionMaxCalc();
            calculators[2] = new NCoelutingFragmentsCalc();

            // Peak-shape family: reference-XIC apex / area / sharpness.
            calculators[3] = new PeakApexCalc();
            calculators[4] = new PeakAreaCalc();
            calculators[5] = new PeakSharpnessCalc();

            return calculators;
        }

        /// <summary>
        /// The calculator for the given PIN feature index, or null when that
        /// feature has not yet been extracted (still computed inline in
        /// <c>ScoreCandidate</c>).
        /// </summary>
        public static IOspreyFeatureCalculator Get(int featureIndex)
        {
            return _calculators[featureIndex];
        }
    }
}
