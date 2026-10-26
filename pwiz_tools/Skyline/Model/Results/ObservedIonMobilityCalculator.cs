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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.CommonMsData;
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
        // Extraction passes the file's maximum IM, which only a linear range window's width uses.
        // The offset lookup discards the width, and the raw file may not be available, so any
        // nonzero value will do.
        private const double ION_MOBILITY_MAX_FOR_UNUSED_WIDTH = 1;

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
        /// <param name="transitions">The precursor's transitions with their chrom infos</param>
        /// <param name="ms1Extracted">True when MS1 full-scan filtering is on. Otherwise precursor
        /// transitions are extracted from MS2 spectra, in the high-energy frame, like fragments.</param>
        /// <param name="getPrecursorHighEnergyOffset">Gets the precursor's high-energy IM offset, used
        /// for transitions without an explicit one. Extraction folds the offset into each fragment's
        /// stored IM filter, so it cannot be recovered from the results. Called at most once, and
        /// only when the high-energy channels contribute to the result, since the lookup can be
        /// costly (see <see cref="GetPrecursorHighEnergyOffset"/>).</param>
        public static Result Calculate(IEnumerable<(TransitionDocNode Transition, TransitionChromInfo ChromInfo)> transitions,
            bool ms1Extracted, Func<double> getPrecursorHighEnergyOffset)
        {
            var channels = new List<Channel>();
            var highEnergyTransitions = new List<(TransitionDocNode Transition, TransitionChromInfo ChromInfo)>();
            IonMobilityFilter target = null;
            foreach (var (nodeTran, chromInfo) in transitions)
            {
                if (chromInfo == null || chromInfo.IsEmpty)
                    continue;
                if (nodeTran.IsMs1 && ms1Extracted)
                {
                    if (target == null && HasFilter(chromInfo))
                        target = chromInfo.IonMobility;
                    // MS1 isotope channels weight by predicted abundance; a precursor with no isotope
                    // distribution (an m/z-only small molecule, or low resolution precursor filtering)
                    // weights by observed area instead, so its observed IM/CCS is still surfaced. The
                    // two never mix within a precursor: every MS1 transition gets its isotope
                    // distribution from the precursor, so either all have one or none do.
                    // A precursor transition with a neutral loss (e.g. [M-H2O]) is not MS1 (see
                    // NeutralFragmentIon.IsMs1). It is extracted from MS2 spectra like a fragment, so it
                    // is a high-energy channel below, used only when the precursor has no MS1 channels.
                    // That keeps it out of the precursor's observed IM when isotopes were measured,
                    // which is right physically too: a neutral loss is a fragment ion with its own
                    // mobility, not a measurement of the precursor ion's.
                    double weight = nodeTran.HasDistInfo ? nodeTran.IsotopeDistInfo.Proportion : chromInfo.Area;
                    channels.Add(new Channel(true, chromInfo.ObservedIonMobility, chromInfo.ObservedCcs, weight, 0));
                }
                else
                {
                    highEnergyTransitions.Add((nodeTran, chromInfo));
                }
            }
            // The high-energy channels matter only when there are no MS1 channels (Aggregate falls
            // back to them, and CCS is MS1-only) or no MS1 channel supplied the target.
            if (highEnergyTransitions.Count > 0 && (channels.Count == 0 || target == null))
            {
                double? precursorHighEnergyOffset = null;
                foreach (var (nodeTran, chromInfo) in highEnergyTransitions)
                {
                    double offset = nodeTran.ExplicitValues.IonMobilityHighEnergyOffset ??
                                    (precursorHighEnergyOffset ??= getPrecursorHighEnergyOffset());
                    if (target == null && HasFilter(chromInfo))
                        target = RemoveHighEnergyOffset(chromInfo.IonMobility, offset);
                    channels.Add(new Channel(false, chromInfo.ObservedIonMobility, chromInfo.ObservedCcs, chromInfo.Area, offset));
                }
            }
            return new Result(
                Aggregate(channels),
                WeightedMean(channels.Where(c => c.IsMs1).Select(c => (c.ObservedCcs, c.Weight))),
                target);
        }

        private static bool HasFilter(TransitionChromInfo chromInfo)
        {
            return chromInfo.IonMobility != null && !IonMobilityFilter.IsNullOrEmpty(chromInfo.IonMobility);
        }

        /// <summary>
        /// The high-energy IM offset chromatogram extraction applied to a precursor's MS2
        /// transitions in the given file, looked up from the same sources extraction used,
        /// including spectral library ion mobilities.
        /// </summary>
        public static double GetPrecursorHighEnergyOffset(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, MsDataFileUri filePath)
        {
            var libraryIonMobilities = settings.TransitionSettings.IonMobilityFiltering.UseSpectralLibraryIonMobilityValues
                ? settings.GetIonMobilities(new[] { nodeGroup.GetLibKey(settings, nodePep) }, filePath)
                : null;
            try
            {
                return settings.GetIonMobilityFilter(nodePep, nodeGroup, null, libraryIonMobilities, null, ION_MOBILITY_MAX_FOR_UNUSED_WIDTH)
                    .HighEnergyIonMobilityOffset ?? 0;
            }
            catch (InvalidDataException)
            {
                // Explicit ion mobility without units, which also prevented IM filtered extraction
                return 0;
            }
        }

        /// <summary>
        /// The precursor's IM filter, recovered from a filter extraction shifted into the
        /// high-energy frame.
        /// </summary>
        private static IonMobilityFilter RemoveHighEnergyOffset(IonMobilityFilter highEnergyFilter, double offset)
        {
            if (offset == 0 || !highEnergyFilter.IonMobility.HasValue)
                return highEnergyFilter;
            return IonMobilityFilter.GetIonMobilityFilter(highEnergyFilter.IonMobility.Mobility.Value - offset,
                highEnergyFilter.IonMobilityUnits, highEnergyFilter.IonMobilityExtractionWindowWidth,
                highEnergyFilter.CollisionalCrossSectionSqA);
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
            // double? - double yields null when the fragment has no observed IM, so the
            // offset-corrected value is null exactly when it should be (WeightedMean skips it).
            return WeightedMean(list.Where(c => !c.IsMs1)
                .Select(c => (c.ObservedIonMobility - c.HighEnergyOffset, c.Weight)));
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
            return totalWeight > 0 ? weightedSum / totalWeight : null;
        }
    }
}
