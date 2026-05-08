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

namespace pwiz.Skyline.Model.Results
{
    public class IntensityAccumulator
    {
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
        /// TODO (bspratt): add an interference guard. The histogram already gives us
        /// the per-target intensity-vs-IM picture; if SpectrumFilter retained these
        /// per-target histograms across all isotope targets of a precursor, a post-
        /// extraction step could discount IM bins whose isotope distribution
        /// disagrees with the expected envelope (idotp guard, the way
        /// IonMobilityFinder.EvaluateBestIonMobilityValue does on unfiltered data).
        /// Asymmetry summary stats over the same histogram are also worth keeping
        /// (cf. the TODO in IonMobilityFinder).
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
    }
}
