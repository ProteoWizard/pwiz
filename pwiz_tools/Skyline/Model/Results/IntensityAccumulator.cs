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
        // Idotp threshold for keeping an IM bin in the cross-isotope reduction.
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
        /// Cross-isotope idotp guard for the observed-IM reduction.
        ///
        /// Tier 1 - exact-bin guard: each candidate IM reads each channel's exact
        /// bin. Precise on precursors with clean, well-sampled isotope envelopes.
        ///
        /// Tier 2 - windowed-guard fallback: when the exact-bin guard rejects every
        /// bin, retry with each channel summed over the monoisotope channel's own
        /// IM peak width (see DeriveHalfWindow). That bridges the detection-limit
        /// gap - the strong M0 channel spans many IM bins while weak M+1/M+2 only
        /// clear detection near the apex, so on the exact-bin path the shoulder
        /// bins zero-fill and get wrongly dropped by missing-where-expected.
        ///
        /// Both tiers reduce via CogOverSurvivors. Returns null when no bin
        /// survives either tier; the caller may fall back to per-channel COG.
        /// </summary>
        public static double? ResolveObservedIonMobilityWithIdotpGuard(
            Dictionary<double, double>[] perChannelBins,
            IList<float> expectedProportions)
        {
            // Tier 1: exact-bin guard.
            var exactSurvivors = CollectSurvivingBins(perChannelBins, expectedProportions, 0);
            if ((exactSurvivors?.Count ?? 0) > 0)
                return CogOverSurvivors(exactSurvivors);

            // Tier 2: windowed-guard fallback, summing each channel over the M0
            // channel's own IM peak width to bridge the detection-limit gap.
            double halfWindow = DeriveHalfWindow(perChannelBins);
            if (halfWindow > 0)
            {
                var windowedSurvivors = CollectSurvivingBins(perChannelBins, expectedProportions, halfWindow);
                if ((windowedSurvivors?.Count ?? 0) > 0)
                    return CogOverSurvivors(windowedSurvivors);
            }

            // Neither tier produced a survivor; caller falls back to per-channel COG.
            return null;
        }

        // Per-bin record produced by the guard's collect-survivors pass and
        // consumed by CogOverSurvivors.
        private struct SurvivingBin
        {
            public double Im;
            public double Total;
        }

        // Builds the union-of-IM-keys cross-channel histogram, computes idotp per
        // bin, and applies the threshold + missing-where-expected guard. The
        // returned list is the input that CogOverSurvivors consumes.
        //
        // When halfWindow > 0, each channel's value at a candidate IM is summed
        // over [center - halfWindow, center + halfWindow] instead of read from
        // the exact bin. That bridges the discretization offset between isotope
        // channels - M0, M+1, M+2 are extracted at slightly different m/z and so
        // land in slightly different discrete IM bins, which on the exact-bin
        // path produces spurious zero-fills that the missing-where-expected
        // guard then rejects.
        private static List<SurvivingBin> CollectSurvivingBins(
            Dictionary<double, double>[] perChannelBins,
            IList<float> expectedProportions,
            double halfWindow)
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

            var survivors = new List<SurvivingBin>();
            var observed = new double[channelCount];
            if (halfWindow > 0)
            {
                // Windowed aggregation: pre-sort each channel's (im, intensity) pairs.
                // Candidates (unionIms) and each channel are both iterated in ascending IM
                // order, so the window sum is maintained with a forward-only two-pointer sweep
                // - O(channelEntries) per channel total, rather than a binary search per
                // (candidate, channel) cell. The sweep state persists across candidates, so it
                // lives outside the candidate loop but entirely within this branch.
                var sortedChannels = new KeyValuePair<double, double>[channelCount][];
                for (int c = 0; c < channelCount; c++)
                {
                    if (perChannelBins[c] == null)
                    {
                        sortedChannels[c] = Array.Empty<KeyValuePair<double, double>>();
                        continue;
                    }
                    var pairs = new List<KeyValuePair<double, double>>(perChannelBins[c]);
                    pairs.Sort((a, b) => a.Key.CompareTo(b.Key));
                    sortedChannels[c] = pairs.ToArray();
                }
                var loIdx = new int[channelCount];
                var hiIdx = new int[channelCount];
                var winSum = new double[channelCount];
                foreach (var im in unionIms)
                {
                    Array.Clear(observed, 0, observed.Length);
                    double total = 0;
                    for (int c = 0; c < channelCount; c++)
                    {
                        var arr = sortedChannels[c];
                        double hi = im + halfWindow;
                        while (hiIdx[c] < arr.Length && arr[hiIdx[c]].Key <= hi)
                        {
                            winSum[c] += arr[hiIdx[c]].Value;
                            hiIdx[c]++;
                        }
                        double lo = im - halfWindow;
                        while (loIdx[c] < hiIdx[c] && arr[loIdx[c]].Key < lo)
                        {
                            winSum[c] -= arr[loIdx[c]].Value;
                            loIdx[c]++;
                        }
                        observed[c] = winSum[c];
                        total += winSum[c];
                    }
                    AddSurvivingBinIfPasses(survivors, observed, expectedVector, statExpected, im, total);
                }
            }
            else
            {
                // Exact-bin aggregation: read each channel's intensity from the exact IM bin.
                foreach (var im in unionIms)
                {
                    Array.Clear(observed, 0, observed.Length);
                    double total = 0;
                    for (int c = 0; c < channelCount; c++)
                    {
                        double v = perChannelBins[c] != null && perChannelBins[c].TryGetValue(im, out var intensity)
                            ? intensity
                            : 0;
                        observed[c] = v;
                        total += v;
                    }
                    AddSurvivingBinIfPasses(survivors, observed, expectedVector, statExpected, im, total);
                }
            }
            return survivors;
        }

        // Evaluates one candidate IM bin's summed-channel vector against the expected isotope
        // proportions (missing-where-expected check, then idotp guard) and adds it to survivors
        // if it passes. Shared by the windowed and exact-bin aggregation paths above.
        private static void AddSurvivingBinIfPasses(List<SurvivingBin> survivors, double[] observed,
            double[] expectedVector, Statistics statExpected, double im, double total)
        {
            if (total <= 0)
                return;
            if (HasMissingExpectedSignal(observed, expectedVector))
                return;
            var idotp = statExpected.Angle(new Statistics(observed));
            if (double.IsNaN(idotp) || idotp < IDOTP_THRESHOLD_FOR_ION_MOBILITY_GUARD)
                return;
            survivors.Add(new SurvivingBin { Im = im, Total = total });
        }

        // Half-window for the windowed guard variant, derived from the
        // monoisotope (M0) channel's own observed IM peak width: half the IM
        // interval that contains the central 80% of M0's intensity. M0 is the
        // strong, reliable channel, so its spread directly measures the natural
        // IM peak width for this precursor on this instrument - independent of
        // whether a user IM filter is set. The weaker isotope channels borrow
        // that scale when their signal is summed, which lets a shoulder bin
        // still see the weak channels' near-apex signal instead of a spurious
        // zero. Returns 0 when M0 has too little signal to estimate a width.
        private static double DeriveHalfWindow(Dictionary<double, double>[] perChannelBins)
        {
            if (perChannelBins == null || perChannelBins.Length == 0 || perChannelBins[0] == null)
                return 0;
            var m0 = perChannelBins[0];
            if (m0.Count < 3)
                return 0;
            var sorted = new KeyValuePair<double, double>[m0.Count];
            int idx = 0;
            foreach (var kvp in m0)
                sorted[idx++] = kvp;
            Array.Sort(sorted, (a, b) => a.Key.CompareTo(b.Key));
            double total = 0;
            foreach (var kvp in sorted)
                total += kvp.Value;
            if (total <= 0)
                return 0;
            // IM at the 10th and 90th intensity percentiles - the central 80% of M0.
            double tail = total * 0.1;
            double acc = 0;
            double lowIm = sorted[0].Key;
            for (int i = 0; i < sorted.Length; i++)
            {
                acc += sorted[i].Value;
                if (acc >= tail)
                {
                    lowIm = sorted[i].Key;
                    break;
                }
            }
            acc = 0;
            double highIm = sorted[sorted.Length - 1].Key;
            for (int i = sorted.Length - 1; i >= 0; i--)
            {
                acc += sorted[i].Value;
                if (acc >= tail)
                {
                    highIm = sorted[i].Key;
                    break;
                }
            }
            double width = highIm - lowIm;
            return width > 0 ? width / 2.0 : 0;
        }

        // Intensity-weighted COG of bin positions across surviving bins.
        // Reuses the same averaging idea as CogIonMobility, so non-linear IM
        // coordinates (1/K0) stay correct.
        private static double? CogOverSurvivors(List<SurvivingBin> survivors)
        {
            survivors.Sort((a, b) => a.Im.CompareTo(b.Im));
            double weightedIndexSum = 0;
            double total = 0;
            for (int i = 0; i < survivors.Count; i++)
            {
                weightedIndexSum += i * survivors[i].Total;
                total += survivors[i].Total;
            }
            if (total == 0)
                return null;
            int cog = (int)Math.Round(weightedIndexSum / total);
            cog = Math.Max(0, Math.Min(survivors.Count - 1, cog));
            return survivors[cog].Im;
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
