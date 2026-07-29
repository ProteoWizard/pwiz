/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Combines a precursor's per-transition observed ion mobilities into the single per-ion
    /// observed IM/CCS (and the error target). Lives in the model, not the databinding layer,
    /// so any consumer (reports, the Full Scan graph, export) can compute the same value.
    /// </summary>
    public static class ObservedIonMobilityCalculator
    {
        // One observed-IM contribution: an MS1 isotope channel (weighted by predicted
        // abundance) or an MS2 fragment channel (weighted by area, carrying the fragment's
        // high-energy IM offset from the precursor).
        public readonly struct Channel
        {
            public Channel(bool isMs1, double? observedIonMobility, double? observedCcs, double weight, double highEnergyOffset)
            {
                IsMs1 = isMs1;
                ObservedIonMobility = observedIonMobility;
                ObservedCcs = observedCcs;
                Weight = weight;
                HighEnergyOffset = highEnergyOffset;
            }
            public bool IsMs1 { get; }
            public double? ObservedIonMobility { get; }
            public double? ObservedCcs { get; }
            public double Weight { get; }
            public double HighEnergyOffset { get; }
        }

        // The per-ion observed IM, observed CCS, and the target IM filter for the error %.
        public sealed class Result
        {
            public Result(double? ionMobility, double? ccs, IonMobilityFilter target)
            {
                IonMobility = ionMobility;
                Ccs = ccs;
                Target = target;
            }
            public double? IonMobility { get; }
            public double? Ccs { get; }
            public IonMobilityFilter Target { get; }
        }

        /// <summary>
        /// Builds the per-transition channels from (transition, chrom info) pairs for a single
        /// replicate/file and reduces them to the per-ion observed IM, observed CCS, and target.
        /// Pairs whose chrom info is null or empty are ignored.
        /// </summary>
        public static Result Calculate(IEnumerable<(TransitionDocNode Transition, TransitionChromInfo ChromInfo)> transitions)
        {
            var channels = new List<Channel>();
            IonMobilityFilter target = null;
            foreach (var (nodeTran, chromInfo) in transitions)
            {
                if (chromInfo == null || chromInfo.IsEmpty)
                    continue;
                // Any transition carries the precursor's IM filter (its base IM/CCS, offset
                // aside, is the same for all), so the first one is the error target.
                if (target == null && chromInfo.IonMobility != null && !IonMobilityFilter.IsNullOrEmpty(chromInfo.IonMobility))
                    target = chromInfo.IonMobility;
                if (nodeTran.IsMs1)
                {
                    // MS1 isotope channels weight by predicted abundance; an MS1 precursor with
                    // no isotope distribution (m/z-only small molecule, low-res precursor
                    // filtering, or a neutral-loss transition) weights by observed area instead
                    // so its observed IM/CCS is still surfaced.
                    double weight = nodeTran.HasDistInfo ? nodeTran.IsotopeDistInfo.Proportion : chromInfo.Area;
                    channels.Add(new Channel(true, chromInfo.ObservedIonMobility, chromInfo.ObservedCcs, weight, 0));
                }
                else
                {
                    double offset = chromInfo.IonMobility?.HighEnergyIonMobilityOffset ?? 0;
                    channels.Add(new Channel(false, chromInfo.ObservedIonMobility, chromInfo.ObservedCcs, chromInfo.Area, offset));
                }
            }
            return new Result(
                Aggregate(channels),
                WeightedMean(channels.Where(c => c.IsMs1).Select(c => (c.ObservedCcs, c.Weight))),
                target);
        }

        /// <summary>
        /// The single per-ion observed IM: predicted-abundance-weighted mean over the MS1
        /// channels. Falls back to the intensity-weighted mean of the offset-corrected fragment
        /// channels only for genuinely MS2-only precursors (no MS1 channels at all) - not merely
        /// when the MS1 channels carry no observed IM - so the observed IM and the MS1-only
        /// observed CCS stay consistent instead of describing different acquisition levels.
        /// </summary>
        public static double? Aggregate(IEnumerable<Channel> channels)
        {
            var list = channels as IList<Channel> ?? channels.ToList();
            if (list.Any(c => c.IsMs1))
                return WeightedMean(list.Where(c => c.IsMs1).Select(c => (c.ObservedIonMobility, c.Weight)));
            return WeightedMean(list.Where(c => !c.IsMs1)
                .Select(c => (c.ObservedIonMobility.HasValue ? c.ObservedIonMobility - c.HighEnergyOffset : (double?) null, c.Weight)));
        }

        private static double? WeightedMean(IEnumerable<(double? value, double weight)> items)
        {
            double weightedSum = 0, totalWeight = 0;
            foreach (var (value, weight) in items)
            {
                if (!value.HasValue || weight <= 0)
                    continue;
                weightedSum += value.Value * weight;
                totalWeight += weight;
            }
            return totalWeight > 0 ? weightedSum / totalWeight : (double?) null;
        }
    }
}
