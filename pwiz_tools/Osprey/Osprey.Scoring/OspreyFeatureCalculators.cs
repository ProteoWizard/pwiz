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

using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// The ordered set of Osprey scoring feature calculators, mirroring
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
        /// <summary>
        /// Number of PIN features -- the parity-locked feature-vector width. Indices
        /// 0..20 ARE the Rust PIN order, the <c>.scores.parquet</c> column order, and the
        /// regression golden's column order: never renumber them. Extra (non-PIN) scores
        /// are APPENDED at 21+ (<see cref="ExtraFeatureCount"/>), which is what keeps
        /// <c>--extra-features</c> byte-neutral when it is off.
        /// </summary>
        public const int FeatureCount = 21;

        /// <summary>
        /// Number of EXTRA (non-PIN) scores appended after the 21 when
        /// <c>--extra-features</c> is on -- scores the Rust engine computes but the PIN
        /// excludes because they misbehave with a linear model. See
        /// <see cref="OspreyConfig.ExtraFeatures"/> for why they are tree-only.
        /// </summary>
        public const int ExtraFeatureCount = 4;

        /// <summary>The feature-vector width for this run: the 21 PIN features, plus the
        /// extras when <paramref name="extraFeatures"/> is on.</summary>
        public static int Count(bool extraFeatures)
        {
            return extraFeatures ? FeatureCount + ExtraFeatureCount : FeatureCount;
        }

        private static readonly IOspreyFeatureCalculator[] _calculators = CreateCalculators();

        private static IOspreyFeatureCalculator[] CreateCalculators()
        {
            var calculators = new IOspreyFeatureCalculator[FeatureCount + ExtraFeatureCount];

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

            // --- EXTRA (non-PIN) scores, --extra-features only --------------------------
            // Appended at 21+ so the PIN indices above stay frozen. Each is a score the
            // Rust engine computes but the PIN drops because a LINEAR model cannot use it
            // (non-monotone or collinear); trees split on them freely. Only reached when
            // OspreyConfig.ExtraFeatures is set.
            calculators[21] = new CoelutionMinCalc();
            calculators[22] = new PeakSymmetryCalc();
            calculators[23] = new MassAccuracyStdCalc();
            calculators[24] = new HyperscoreCalc();

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
        /// Projects the 21 calculators into a single <see cref="OspreyFeatureInfo"/>
        /// vector in parity-critical PIN-index order, pairing each supplied machine
        /// <paramref name="featureNames"/> entry with that calculator's
        /// <see cref="IOspreyFeatureCalculator.DisplayName"/> label and
        /// <see cref="IOspreyFeatureCalculator.IsReversedScore"/> direction. The FDR
        /// layer (which does not reference this assembly) takes this single vector by
        /// value -- replacing the former parallel name / label / reversed arrays --
        /// to label and direction-flag the rows of the feature-contribution report.
        /// The machine name stays the caller's parity-critical PIN column name;
        /// label + direction are owned by each calculator (the single source of
        /// truth, in lockstep with the index order).
        /// </summary>
        /// <param name="featureNames">
        /// The parity-critical PIN feature names, one per feature in index order
        /// (typically <c>ParquetScoreCache.PIN_FEATURE_NAMES</c>). Must have
        /// <see cref="FeatureCount"/> entries.
        /// </param>
        public static OspreyFeatureInfo[] BuildFeatureInfos(string[] featureNames)
        {
            // Accepts either width: the 21 PIN features, or 21 + the extras when
            // --extra-features widened the vector. The caller's array length selects,
            // so the two stay in lockstep with the parquet schema it was built from.
            if (featureNames == null ||
                (featureNames.Length != FeatureCount &&
                 featureNames.Length != FeatureCount + ExtraFeatureCount))
            {
                throw new System.ArgumentException(
                    string.Format(@"BuildFeatureInfos expects {0} or {1} feature names",
                        FeatureCount, FeatureCount + ExtraFeatureCount),
                    nameof(featureNames));
            }
            var infos = new OspreyFeatureInfo[featureNames.Length];
            for (int i = 0; i < featureNames.Length; i++)
                infos[i] = new OspreyFeatureInfo(
                    featureNames[i], _calculators[i].DisplayName, _calculators[i].IsReversedScore);
            return infos;
        }
    }
}
