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
    /// All 21 PIN features are now extracted, so every slot is populated and
    /// <see cref="Get"/> never returns null; this array is the canonical ordered
    /// calculator set. (Scores the Rust engine computes but excludes from the 21 PIN
    /// features are intentionally NOT here -- see the non-PIN-scores backlog.)
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

            // Xcorr + Savitzky-Golay family: apex xcorr and SG-weighted xcorr /
            // cosine over the apex +/- 2 scans.
            calculators[6] = new XcorrCalc();
            calculators[17] = new SgXcorrCalc();
            calculators[18] = new SgCosineCalc();

            // Apex-match family: longest consecutive b/y run, explained intensity,
            // and signed / absolute mean fragment mass error at the apex spectrum.
            calculators[7] = new ConsecutiveIonsCalc();
            calculators[8] = new ExplainedIntensityCalc();
            calculators[9] = new MassAccuracyMeanCalc();
            calculators[10] = new AbsMassAccuracyMeanCalc();

            // RT-deviation family: apex RT minus expected RT, and its absolute value.
            calculators[11] = new RtDeviationCalc();
            calculators[12] = new AbsRtDeviationCalc();

            // MS1 family (HRAM only): precursor coelution + isotope-envelope cosine.
            calculators[13] = new Ms1PrecursorCoelutionCalc();
            calculators[14] = new Ms1IsotopeCosineCalc();

            // Median-polish family: cosine / residual-ratio / min-fragment-R^2 /
            // residual-correlation of the Tukey median-polish fit.
            calculators[15] = new MedianPolishCosineCalc();
            calculators[16] = new MedianPolishResidualRatioCalc();
            calculators[19] = new MedianPolishMinFragmentR2Calc();
            calculators[20] = new MedianPolishResidualCorrelationCalc();

            return calculators;
        }

        /// <summary>
        /// The calculator for the given PIN feature index (0..20). All 21 are
        /// populated, so this never returns null.
        /// </summary>
        public static IOspreyFeatureCalculator Get(int featureIndex)
        {
            return _calculators[featureIndex];
        }

        /// <summary>
        /// The <see cref="IOspreyFeatureCalculator.IsReversedScore"/> flag for each
        /// of the 21 PIN features, in parity-critical index order. The FDR layer
        /// (which does not reference this assembly) takes this vector by value to
        /// flag unexpected-direction coefficients in the feature-contribution table.
        /// </summary>
        public static bool[] GetReversedScoreFlags()
        {
            var flags = new bool[FeatureCount];
            for (int i = 0; i < FeatureCount; i++)
                flags[i] = _calculators[i].IsReversedScore;
            return flags;
        }

        /// <summary>
        /// The human-friendly (Skyline-style)
        /// <see cref="IOspreyFeatureCalculator.DisplayName"/> for each of the 21 PIN
        /// features, in parity-critical index order. The FDR layer (which does not
        /// reference this assembly) takes this vector by value to label the rows of
        /// the post-training feature-contribution report. Display text only -- the
        /// parity-gated columns continue to use the machine
        /// <see cref="IOspreyFeatureCalculator.Name"/>.
        /// </summary>
        public static string[] GetFeatureLabels()
        {
            var labels = new string[FeatureCount];
            for (int i = 0; i < FeatureCount; i++)
                labels[i] = _calculators[i].DisplayName;
            return labels;
        }
    }
}
