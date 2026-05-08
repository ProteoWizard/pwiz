/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class IntensityAccumulator
    {
        // Idotp threshold for keeping an IM bin in the cross-isotope COG reduction.
        // Below this, the bin is treated as interferent-dominated and discarded.
        // Mirrors the 0.7 cutoff that's commonly accepted as "good fit" in Skyline UI.
        public const double IDOTP_THRESHOLD_FOR_ION_MOBILITY_GUARD = 0.7;

        // Reject an IM bin when at least this fraction of the expected isotope envelope
        // has zero observed signal - mirrors IonMobilityFinder.EvaluateBestIonMobilityValue.
        public const double EXPECTED_PROPORTION_FOR_MISSING_GUARD = 0.10;

        public double TotalIntensity { get; set; }
        public double MeanMassError { get; set; }

        // Summed extractor with IM tracking: histogram of (IM -> summed intensity)
        // for the triplets that fell inside the m/z and IM extraction windows.
        // Exposed publicly so the caller (SpectrumFilterPair) can carry it across
        // multiple spectra at the same retention time, and so a future post-step
        // can use the histogram for interference checks (see CogIonMobility TODO).
        public Dictionary<double, double> IonMobilityIntensityBins { get; }

        // Base-peak extractor with IM tracking: IM at the single highest-intensity
        // peak ever seen. Carried across spectra by the caller via this property.
        public double BasePeakIonMobility { get; set; }

        bool _highAcc;
        bool _trackIonMobility;
        ChromExtractor _extractor;
        private double _targetMz;

        public IntensityAccumulator(bool highAcc, ChromExtractor extractor, double targetMz,
            bool trackIonMobility = false, Dictionary<double, double> ionMobilityIntensityBins = null)
        {
            _highAcc = highAcc;
            _extractor = extractor;
            _targetMz = targetMz;
            _trackIonMobility = trackIonMobility;
            if (trackIonMobility && extractor == ChromExtractor.summed)
            {
                IonMobilityIntensityBins = ionMobilityIntensityBins ?? new Dictionary<double, double>();
            }
        }

        public void AddPoint(double mz, double intensity, double? ionMobility = null)
        {
            bool basePeakReset = false;
            if (_extractor == ChromExtractor.summed)
                TotalIntensity += intensity;
            else if (intensity > TotalIntensity)
            {
                TotalIntensity = intensity;
                MeanMassError = 0;
                BasePeakIonMobility = 0;
                basePeakReset = true;
            }

            if (TotalIntensity <= 0.0)
                return;

            // Accumulate weighted mean mass error for summed, or take a single
            // mass error of the most intense peak for base peak.
            if (_highAcc && (_extractor == ChromExtractor.summed || MeanMassError == 0))
            {
                double deltaPeakMz = mz - _targetMz;
                MeanMassError += (deltaPeakMz - MeanMassError) * intensity / TotalIntensity;
            }

            // Track IM as a histogram over IM bins (summed extractor) or as the IM
            // at the single strongest peak (base_peak). Crucially, neither path
            // averages IM *values*: the summed histogram is later collapsed via
            // CogIonMobility, which averages bin *positions*. This avoids
            // assuming IM is a linearly averageable coordinate (it isn't for 1/K0
            // and other non-linear instrument calibrations).
            if (_trackIonMobility && ionMobility.HasValue)
            {
                if (_extractor == ChromExtractor.summed)
                {
                    if (IonMobilityIntensityBins.TryGetValue(ionMobility.Value, out var sum))
                        IonMobilityIntensityBins[ionMobility.Value] = sum + intensity;
                    else
                        IonMobilityIntensityBins[ionMobility.Value] = intensity;
                }
                else if (basePeakReset)
                {
                    BasePeakIonMobility = ionMobility.Value;
                }
            }
        }

        /// <summary>
        /// Final observed IM for this accumulator, applying the appropriate
        /// reduction for the extractor: COG bin-index of the IM histogram for
        /// summed extraction; IM at the strongest peak for base_peak.
        /// </summary>
        public double ObservedIonMobility
        {
            get
            {
                if (!_trackIonMobility)
                    return 0;
                if (_extractor != ChromExtractor.summed)
                    return BasePeakIonMobility;
                return CogIonMobility(IonMobilityIntensityBins);
            }
        }

        /// <summary>
        /// Collapse an (IM -> summed intensity) histogram to a single representative
        /// observed IM, by computing the intensity-weighted center of gravity of the
        /// bin *positions* (after sorting bins by IM) and returning the IM at that
        /// COG bin. Averaging bin positions rather than IM values keeps this correct
        /// for non-linear IM coordinates such as 1/K0.
        ///
        /// For interference rejection, see ResolveObservedIonMobilityWithIdotpGuard,
        /// which combines per-isotope histograms before COG.
        /// </summary>
        public static double CogIonMobility(Dictionary<double, double> ionMobilityIntensityBins)
        {
            if (ionMobilityIntensityBins == null || ionMobilityIntensityBins.Count == 0)
                return 0;
            var sorted = new KeyValuePair<double, double>[ionMobilityIntensityBins.Count];
            int idx = 0;
            foreach (var kvp in ionMobilityIntensityBins)
                sorted[idx++] = kvp;
            Array.Sort(sorted, (a, b) => a.Key.CompareTo(b.Key));
            double weightedIndexSum = 0;
            double total = 0;
            for (int i = 0; i < sorted.Length; i++)
            {
                weightedIndexSum += i * sorted[i].Value;
                total += sorted[i].Value;
            }
            if (total == 0)
                return 0;
            int cog = (int)Math.Round(weightedIndexSum / total);
            cog = Math.Max(0, Math.Min(sorted.Length - 1, cog));
            return sorted[cog].Key;
        }

        public override string ToString() // Debug convenience, not user facing
        {
            return $@"mz:{_targetMz} i:{TotalIntensity} mzErr:{MeanMassError:F6} im:{ObservedIonMobility:F6}";
        }

        /// <summary>
        /// Cross-isotope idotp guard for the observed-IM reduction. Combines the
        /// per-channel (IM -> summed intensity) histograms of an MS1 isotope group
        /// into a single Dict[IM, double[channels]] (taking the union of IM keys,
        /// zero-filling channels that didn't observe a triplet at a given IM),
        /// then drops IM bins whose isotope intensity vector doesn't match the
        /// expected envelope (low idotp, or missing signal where >=10% expected).
        /// The surviving bins are reduced via COG-bin-index, weighted by total
        /// intensity across isotopes at each bin.
        ///
        /// Returns null when no bin survives the guard (e.g., all bins are
        /// interferent-dominated, or input is empty); the caller may fall back to
        /// per-channel COG in that case.
        /// </summary>
        public static double? ResolveObservedIonMobilityWithIdotpGuard(
            Dictionary<double, double>[] perChannelBins,
            IList<float> expectedProportions)
        {
            if (perChannelBins == null || perChannelBins.Length == 0 || expectedProportions == null)
                return null;
            int channelCount = Math.Min(perChannelBins.Length, expectedProportions.Count);
            if (channelCount == 0)
                return null;

            var unionIms = new SortedSet<double>();
            for (int c = 0; c < channelCount; c++)
            {
                if (perChannelBins[c] == null)
                    continue;
                foreach (var im in perChannelBins[c].Keys)
                    unionIms.Add(im);
            }
            if (unionIms.Count == 0)
                return null;

            var expectedVector = new double[channelCount];
            for (int c = 0; c < channelCount; c++)
                expectedVector[c] = expectedProportions[c];
            var statExpected = new Statistics(expectedVector);

            var survivingBins = new Dictionary<double, double>();
            var observed = new double[channelCount];
            foreach (var im in unionIms)
            {
                Array.Clear(observed, 0, observed.Length);
                double total = 0;
                for (int c = 0; c < channelCount; c++)
                {
                    if (perChannelBins[c] != null &&
                        perChannelBins[c].TryGetValue(im, out var intensity))
                    {
                        observed[c] = intensity;
                        total += intensity;
                    }
                }
                if (total <= 0)
                    continue;

                if (HasMissingExpectedSignal(observed, expectedVector))
                    continue;

                var statObserved = new Statistics(observed);
                var idotp = statExpected.Angle(statObserved);
                if (double.IsNaN(idotp) || idotp < IDOTP_THRESHOLD_FOR_ION_MOBILITY_GUARD)
                    continue;

                survivingBins[im] = total;
            }
            if (survivingBins.Count == 0)
                return null;
            return CogIonMobility(survivingBins);
        }

        private static bool HasMissingExpectedSignal(double[] observed, double[] expected)
        {
            for (int c = 0; c < observed.Length; c++)
            {
                if (expected[c] >= EXPECTED_PROPORTION_FOR_MISSING_GUARD && observed[c] == 0)
                    return true;
            }
            return false;
        }
    }
}
