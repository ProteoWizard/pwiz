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

using System.Globalization;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// The ordered set of Osprey scoring feature calculators, mirroring
    /// Skyline's <c>PeakFeatureCalculator.CALCULATORS</c>. The array index IS the
    /// parity-critical PIN feature index -- the position written into the
    /// <c>double[FeatureCount]</c> feature vector and the feature column order in
    /// <c>.scores.parquet</c>. This is the single place that order lives as
    /// data.
    ///
    /// The first 21 are the Rust PIN features, every slot populated, so <see cref="Get"/>
    /// never returns null. Slots 21-40 are C#-only: the runner-up, context, per-fragment-rank and spectrum-across-peak families,
    /// masked by default (<see cref="DEFAULT_MASKED_FEATURES"/>, OSPREY_FEATURE_MASK) while
    /// they are evaluated. (Scores the Rust engine computes but excludes from its PIN features
    /// are intentionally NOT here - see the non-PIN-scores backlog.)
    /// </summary>
    public static class OspreyFeatureCalculators
    {
        /// <summary>Number of PIN features (the feature-vector width).</summary>
        public const int FeatureCount = 41;

        /// <summary>
        /// The features masked when OSPREY_FEATURE_MASK is unset: the runner-up, context,
        /// per-fragment-rank and spectrum-across-peak families, still under evaluation. A masked feature is written as 0, so the
        /// standardizer gives it scale 1 and value 0 and the classifier cannot use it -
        /// which keeps the default search's results what they were before these features.
        /// </summary>
        public static readonly string[] DEFAULT_MASKED_FEATURES =
        {
            @"peak_coelution_margin", @"peak_rank_margin", @"peak_candidate_count",
            @"context_peptide_length", @"context_precursor_charge", @"context_precursor_mz",
            @"frag_rank1_abs_mass_error", @"frag_rank2_abs_mass_error", @"frag_rank3_abs_mass_error",
            @"frag_rank4_abs_mass_error", @"frag_rank5_abs_mass_error", @"frag_rank6_abs_mass_error",
            @"frag_rank1_intensity_share", @"frag_rank2_intensity_share", @"frag_rank3_intensity_share",
            @"frag_rank4_intensity_share", @"frag_rank5_intensity_share", @"frag_rank6_intensity_share",
            @"peak_spectrum_cosine_mean", @"peak_spectrum_cosine_sd",
        };

        private static readonly IOspreyFeatureCalculator[] _calculators = CreateCalculators();

        private static readonly bool[] _masked = CreateMask(OspreyEnvironment.FeatureMask ?? DEFAULT_MASKED_FEATURES);

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

            // Runner-up family: the chosen peak against the other candidates in its window.
            calculators[21] = new PeakCoelutionMarginCalc();
            calculators[22] = new PeakRankMarginCalc();
            calculators[23] = new PeakCandidateCountCalc();

            // Context family: what is being matched, shared by a target and its decoy.
            calculators[24] = new PeptideLengthCalc();
            calculators[25] = new PrecursorChargeCalc();
            calculators[26] = new PrecursorMzCalc();

            // Per-fragment-rank family: the top library fragments matched at the apex, one feature
            // per rank, so the classifier can weigh each rank separately.
            for (int r = 1; r <= FragmentRankApexMatch.RANK_COUNT; r++)
            {
                calculators[26 + r] = new FragmentRankMassErrorCalc(r);
                calculators[26 + FragmentRankApexMatch.RANK_COUNT + r] = new FragmentRankIntensityShareCalc(r);
            }

            // Spectrum-across-peak family: the library cosine at every scan of the peak.
            calculators[39] = new PeakSpectrumCosineMeanCalc();
            calculators[40] = new PeakSpectrumCosineSdCalc();

            return calculators;
        }

        private static bool[] CreateMask(string[] maskedNames)
        {
            var masked = new bool[FeatureCount];
            foreach (string name in maskedNames)
            {
                int index = System.Array.FindIndex(_calculators, c => c.Name == name);
                if (index < 0)
                {
                    throw new System.InvalidOperationException(
                        string.Format(@"OSPREY_FEATURE_MASK names an unknown feature: {0}", name));
                }
                masked[index] = true;
            }
            return masked;
        }

        /// <summary>
        /// The calculator for the given PIN feature index (0 to FeatureCount - 1). All are
        /// populated, so this never returns null.
        /// </summary>
        public static IOspreyFeatureCalculator Get(int featureIndex)
        {
            return _calculators[featureIndex];
        }

        /// <summary>
        /// True when the feature at <paramref name="featureIndex"/> is masked (written as 0):
        /// named in OSPREY_FEATURE_MASK, or in <see cref="DEFAULT_MASKED_FEATURES"/> when it is unset.
        /// </summary>
        public static bool IsMasked(int featureIndex)
        {
            return _masked[featureIndex];
        }

        /// <summary>
        /// Projects the calculators into a single <see cref="OspreyFeatureInfo"/>
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
            if (featureNames == null || featureNames.Length != FeatureCount)
                throw new System.ArgumentException(
                    string.Format(@"BuildFeatureInfos expects {0} feature names", FeatureCount),
                    nameof(featureNames));
            // The report label is read in the invariant UI culture, switched on this thread only.
            var reportLabels = new string[FeatureCount];
            var uiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                for (int i = 0; i < FeatureCount; i++)
                    reportLabels[i] = _calculators[i].DisplayName;
            }
            finally
            {
                CultureInfo.CurrentUICulture = uiCulture;
            }
            var infos = new OspreyFeatureInfo[FeatureCount];
            for (int i = 0; i < FeatureCount; i++)
            {
                infos[i] = new OspreyFeatureInfo(
                    featureNames[i], _calculators[i].DisplayName, _calculators[i].IsReversedScore, reportLabels[i]);
            }
            return infos;
        }
    }
}
