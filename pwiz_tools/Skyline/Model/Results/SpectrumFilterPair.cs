/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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

using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;

namespace pwiz.Skyline.Model.Results
{
    public sealed class SpectrumFilterPair : IComparable<SpectrumFilterPair>
    {
        private static readonly SpectrumProductFilter[] EMPTY_FILTERS = new SpectrumProductFilter[0];
        private bool _hasMinTime;
        private bool _hasMaxTime;
        private double _minTime;
        private double _maxTime;
        public SpectrumFilterPair(PrecursorTextId precursorTextId, Color peptideColor, int id, double? minTime, double? maxTime,
            bool highAccQ1, bool highAccQ3)
        {
            Id = id;
            ChromatogramGroupId = precursorTextId.ChromatogramGroupId;
            PeptideColor = peptideColor;
            Q1 = precursorTextId.PrecursorMz;
            Extractor = precursorTextId.Extractor;

            if (minTime.HasValue)
            {
                _hasMinTime = true;
                _minTime = minTime.Value;
            }

            if (maxTime.HasValue)
            {
                _hasMaxTime = true;
                _maxTime = maxTime.Value;
            }

            OptStep = precursorTextId.OptStep;
            CollisionEnergy = precursorTextId.CollisionEnergy;
            IonMobilityInfo = precursorTextId.IonMobility;
            MinIonMobilityValue = IonMobilityInfo.IsEmpty ? null : IonMobilityInfo.IonMobility.Mobility - (IonMobilityInfo.IonMobilityExtractionWindowWidth??0)/2;
            MaxIonMobilityValue = IonMobilityInfo.IsEmpty ? null : MinIonMobilityValue + (IonMobilityInfo.IonMobilityExtractionWindowWidth ?? 0);
            HighAccQ1 = highAccQ1;
            HighAccQ3 = highAccQ3;

            Ms1ProductFilters = SimProductFilters = Ms2ProductFilters = EMPTY_FILTERS;

            if (Q1 == 0)
            {
                Ms1ProductFilters = new[] {new SpectrumProductFilter(SignedMz.ZERO, 0, 0)};
                SimProductFilters = Ms1ProductFilters;  // We want TIC and BPC for all scans, even if they have narrow machine settings and look like SIM
            }
        }

        public int Id { get; private set; }
        public ChromExtractor Extractor { get; private set; }
        public bool HighAccQ1 { get; private set; }
        public bool HighAccQ3 { get; private set; }
        public ChromatogramGroupId ChromatogramGroupId { get; }
        public SpectrumClassFilter SpectrumClassFilter => ChromatogramGroupId?.SpectrumClassFilter ?? default;
        public Color PeptideColor { get; private set; }
        public SignedMz Q1 { get; private set; }
        public double? MinTime
        {
            get { return _hasMinTime ? _minTime : (double?) null; }
        }
        public double? MaxTime
        {
            get { return _hasMaxTime ? _maxTime : (double?) null; }
        }
        public double? MinIonMobilityValue { get; set; }
        public double? MaxIonMobilityValue { get; set; }
        public int BestWindowGroup { get; private set; }  // For DIA PASEF
        public bool HasBestWindowGroup => BestWindowGroup > 0; // For DIA PASEF
        public float BestWindowGroupMatchArea { get; private set; }  // For DIA PASEF
        public float BestWindowGroupMatchDistance { get; private set; }  // For DIA PASEF
        private int _knownWindowGroupsBitField; // For DIA PASEF
        public int? OptStep { get; }
        private double? CollisionEnergy { get; }
        private IonMobilityFilter IonMobilityInfo { get; set; }
        private bool HasCombinedIonMobility { get; set; } // When true, data was read in 3-array format, which affects spectrum ID format
        internal SpectrumProductFilter[] Ms1ProductFilters { get; set; }
        private SpectrumProductFilter[] SimProductFilters { get; set; }
        public SpectrumProductFilter[] Ms2ProductFilters { get; set; }
        private IIonMobilityFunctionsProvider IonMobilityFunctionsProvider { get; set; }

        public string ScanDescriptionFilter { get; set; }

        public int AddQ1FilterValues(IEnumerable<SignedMz> filterValues, Func<double, double> getFilterWindow)
        {

            int filterCount = AddFilterValues(MergeFilters(Ms1ProductFilters, filterValues.Select(mz => new SpectrumFilterValues(mz, 0))).Distinct(),
                getFilterWindow, filters => Ms1ProductFilters = filters);
            // Make complete copies for SIM scans. Some day these may be different.
            SimProductFilters = Ms1ProductFilters.Select(f => new SpectrumProductFilter(f.TargetMz, f.FilterWidth, 0)).ToArray();
            return filterCount * 2;
        }

        public int AddQ3FilterValues(IEnumerable<SpectrumFilterValues> filterValues, Func<double, double> getFilterWindow)
        {
            return AddFilterValues(MergeFilters(Ms2ProductFilters, filterValues).Distinct(),
                getFilterWindow, filters => Ms2ProductFilters = filters);
        }

        private static IEnumerable<SpectrumFilterValues> MergeFilters(IEnumerable<SpectrumProductFilter> existing, IEnumerable<SpectrumFilterValues> added)
        {
            if (existing == null)
                return added;
            return existing.Select(f => new SpectrumFilterValues(f.TargetMz, f.HighEnergyIonMobilityValueOffsetLow, f.HighEnergyIonMobilityValueOffsetHigh)).Union(added);
        }

        private int AddFilterValues(IEnumerable<SpectrumFilterValues> filterValues,
                                            Func<double, double> getFilterWindow,
                                            Action<SpectrumProductFilter[]> setFilters)
        {
            var arrayFilters = filterValues.OrderBy(mz => mz)
                .Select(mz => new SpectrumProductFilter(mz.mz, getFilterWindow(mz.mz), mz.ionMobilityHighEnergyOffsetLow, mz.ionMobilityHighEnergyOffsetHigh))
                .ToArray();
            setFilters(arrayFilters);
            return arrayFilters.Length;
        }

        public ExtractedSpectrum FilterQ1SpectrumList(MsDataSpectrum[] spectra, bool isSimSpectra = false)
        {
            var filters = isSimSpectra ? SimProductFilters : Ms1ProductFilters;
            return FilterSpectrumList(spectra, filters, HighAccQ1, false);
        }

        public ExtractedSpectrum FilterQ3SpectrumList(MsDataSpectrum[] spectra, bool useIonMobilityHighEnergyOffset)
        {
            // All-ions extraction for MS1 scans only
            if (Q1 == 0)
                return null;
            if (CollisionEnergy.HasValue)
            {
                foreach (var spectrum in spectra)
                {
                    foreach (var precursor in spectrum.Precursors)
                    {
                        if (!precursor.PrecursorCollisionEnergy.HasValue)
                        {
                            return null;
                        }

                        if (Math.Abs(precursor.PrecursorCollisionEnergy.Value - CollisionEnergy.Value) > 0.05)
                        {
                            return null;
                        }
                    }
                }
            }

            // Filter out spectra with no overlap of any target with the spectrum's scan window
            var filteredSpectra = spectra.Where(spectrum => spectrum.Metadata == null ||
                                                            !spectrum.Metadata.ScanWindowLowerLimit.HasValue ||
                                                            !spectrum.Metadata.ScanWindowUpperLimit.HasValue ||
                                                            (Ms2ProductFilters.Any() &&
                                                             spectrum.Metadata.ScanWindowLowerLimit < Ms2ProductFilters.Last().TargetMz &&
                                                             spectrum.Metadata.ScanWindowUpperLimit > Ms2ProductFilters.First().TargetMz));
            return FilterSpectrumList(filteredSpectra.ToArray(), Ms2ProductFilters, HighAccQ3, useIonMobilityHighEnergyOffset);
        }

        /// <summary>
        /// Apply the filter to a list of spectra.  In "normal" operation
        /// this list has a length of one. For ion mobility data it
        /// may be a list of spectra with the same retention time but
        /// different ion mobility values. For Agilent Mse data it may be
        /// a list of MS2 spectra that need averaging (or even a list
        /// of MS2 spectra with mixed retention and ion mobility values).  Averaging
        /// is done by unique retention time count, rather than by spectrum
        /// count, so that ion mobility data ion counts are additive (we're
        /// trying to measure ions per injection, basically).
        /// </summary>
        private ExtractedSpectrum FilterSpectrumList(MsDataSpectrum[] spectra,
            SpectrumProductFilter[] productFilters, bool highAcc, bool useIonMobilityHighEnergyOffset)
        {

            if (HasIonMobilityFAIMS() && spectra.All(s => !Equals(IonMobilityInfo.IonMobility, s.IonMobility)))
            {
                return null; // No compensation voltage match
            }

            int targetCount = 1;
            if (Q1 == 0)
                highAcc = false;    // No mass error for all-ions extraction
            else
            {
                if (productFilters.Length == 0)
                    return null;
                targetCount = productFilters.Length;
            }

            float[] extractedIntensities = new float[targetCount];
            float[] massErrors = highAcc ? new float[targetCount] : null;
            double[] meanErrors = highAcc ? new double[targetCount] : null;

            int spectrumCount = 0;
            int rtCount = 0;
            double lastRT = 0;

            var imRangeHelper = new IonMobilityRangeHelper(spectra, useIonMobilityHighEnergyOffset ? productFilters : null,
                MinIonMobilityValue, MaxIonMobilityValue);
            if (imRangeHelper.IndexFirst >= spectra.Length)
            {
                // No ion mobility match - record a zero intensity unless IM value is outside the
                // machine's measured range, or if this is a polarity mismatch
                if (!IsOutsideSpectraRangeIM(spectra, MinIonMobilityValue, MaxIonMobilityValue) && 
                    spectra.Any(s => Equals(s.NegativeCharge, Q1.IsNegative)))
                {
                    spectrumCount++; // Our flag to process this as zero rather than null
                }
            } 

            for (int specIndex = imRangeHelper.IndexFirst; specIndex < spectra.Length; specIndex++)
            {
                var spectrum = spectra[specIndex];

                if (imRangeHelper.IsBeyondRange(spectrum))
                    break;

                // If these are spectra from distinct retention times, average them.
                // Note that for ion mobility data we will see fewer retention time changes 
                // than the total spectra count - ascending DT (or descending 1/K0) within each RT.  Within a
                // single retention time the ions are additive.
                var rt = spectrum.RetentionTime ?? 0;
                if (lastRT != rt)
                {
                    rtCount++;
                    lastRT = rt;
                }

                // Filter on scan polarity
                if (Q1.IsNegative != spectrum.NegativeCharge)
                    continue;

                spectrumCount++;

                var mzArray = spectrum.Mzs;
                if (mzArray == null || mzArray.Length == 0)
                    continue;

                // It's not unusual for mzarray and centerArray to have no overlap, esp. with ion mobility data
                if (Q1 != 0)
                {
                    var lastProductFilter = productFilters[targetCount - 1];
                    if (lastProductFilter.TargetMz.Value + lastProductFilter.FilterWidth/2 < mzArray[0])
                        continue;
                }

                var intensityArray = spectrum.Intensities;
                var imsArray = spectrum.IonMobilities;

                // Search for matching peaks for each Q3 filter
                // N.B. this code searches both MS1 data and MS2, for MS1 Q1 is nominal precursor mz and "Q3" is isotope mz, so it's really Q1
                // Use binary search to get to the first m/z value to be considered more quickly
                // This should help MS1 where isotope distributions will be very close in m/z
                // It should also help MS/MS when more selective, larger fragment ions are used,
                // since then a lot of less selective, smaller peaks must be skipped
                int iPeak = 0;
                for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
                {
                    var productFilter = productFilters[targetIndex];
                    // In this context HighEnergyIonMobilityValue* may represent the effect of higher CE,
                    // or it may reflect the use of the information found in DIA PASEF window group tables
                    // Ensure uncombined IM spectra are within range
                    if (spectrum.IonMobilities == null &&
                        !ContainsIonMobilityValue(spectrum.IonMobility, 
                            useIonMobilityHighEnergyOffset ? productFilter.HighEnergyIonMobilityValueOffsetLow : 0,
                            useIonMobilityHighEnergyOffset ? productFilter.HighEnergyIonMobilityValueOffsetHigh : 0))
                    {
                        continue;
                    }

                    // Look for the first peak that is greater than the start of the filter
                    double targetMz = 0, endFilter = double.MaxValue;
                    if (Q1 != 0)
                    {
                        targetMz = productFilter.TargetMz;
                        double filterWindow = productFilter.FilterWidth;
                        double startFilter = targetMz - filterWindow / 2;
                        endFilter = startFilter + filterWindow;

                        if (iPeak < mzArray.Length)
                        {
                            iPeak = Array.BinarySearch(mzArray, iPeak, mzArray.Length - iPeak, startFilter);
                            if (iPeak < 0)
                                iPeak = ~iPeak;
                        }
                        if (iPeak >= mzArray.Length)
                            break; // No further overlap
                    }
                    
                    // TODO:(bspratt) for full frame diaPASEF MS2, try not sorting - make IM the initial binary search range (and deal with mz that rolls over)

                    // Add the intensity values of all peaks that pass the filter
                    var accumulator = new IntensityAccumulator(highAcc, Extractor, targetMz)
                    {
                        TotalIntensity = extractedIntensities[targetIndex], // Start with the value from the previous spectrum, if any
                        MeanMassError = highAcc ? meanErrors[targetIndex] : 0
                    };

                    var isMs2DiaPasefWithoutPrecursorIM = spectrum.WindowGroup > 0 && !MinIonMobilityValue.HasValue;
                    for (int iNext = iPeak; iNext < mzArray.Length && mzArray[iNext] < endFilter; iNext++)
                    {
                        // Avoid adding points that are not within the allowed ion mobility range
                        // In the case of full-frame MS2 DiaPASEF this also enforces mz range
                        var im = imsArray == null ? spectrum.IonMobility?.Mobility : imsArray[iNext];
                        if (im != null)
                        {
                            if (isMs2DiaPasefWithoutPrecursorIM)
                            {
                                // MS2 DiaPASEF without precursor filter - we use the frame information to make IM a cue for isolation range
                                if (productFilter.HighEnergyIonMobilityValueOffsetLow > im.Value ||
                                    productFilter.HighEnergyIonMobilityValueOffsetHigh < im.Value)
                                {
                                    continue;
                                }
                            }
                            else if (!ContainsIonMobilityValue(im.Value,
                                useIonMobilityHighEnergyOffset ? productFilter.HighEnergyIonMobilityValueOffsetLow : 0,
                                useIonMobilityHighEnergyOffset ? productFilter.HighEnergyIonMobilityValueOffsetHigh : 0))
                            {
                                continue;
                            }
                        }
                        accumulator.AddPoint(mzArray[iNext], intensityArray[iNext]);
                    }
                    extractedIntensities[targetIndex] = (float) accumulator.TotalIntensity;
                    if (meanErrors != null)
                        meanErrors[targetIndex] = accumulator.MeanMassError;
                }
                
            }
            if (spectrumCount == 0)
            {
                return null;
            }
            if (meanErrors != null)
            {
                for (int i = 0; i < targetCount; i++)
                    massErrors[i] = (float)SequenceMassCalc.GetPpm(productFilters[i].TargetMz, meanErrors[i]);
            }

            // If we summed across spectra of different retention times, scale per
            // unique retention time (but not per ion mobility value)
            if (Extractor == ChromExtractor.summed && rtCount > 1)
            {
                float scale = (float)(1.0 / rtCount);
                for (int i = 0; i < targetCount; i++)
                    extractedIntensities[i] *= scale;
            }
            var dtFilter = GetIonMobilityWindow();
            return new ExtractedSpectrum(ChromatogramGroupId,
                PeptideColor,
                Q1,
                dtFilter, 
                Extractor,
                Id,
                productFilters,
                extractedIntensities,
                massErrors);
        }

        public int CompareTo(SpectrumFilterPair other)
        {
            return Comparer.Default.Compare(Q1, other.Q1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsRetentionTime(double retentionTime)
        {
            return (!_hasMinTime || _minTime <= retentionTime) && (!_hasMaxTime || _maxTime >= retentionTime);
        }

        public IEnumerable<int> ProductFilterIds
        {
            get
            {
                foreach (var filters in new[] {Ms1ProductFilters, SimProductFilters, Ms2ProductFilters})
                {
                    foreach (var spectrumProductFilter in filters)
                    {
                        yield return spectrumProductFilter.FilterId;
                    }
                }
            }
        }

        public void AddChromKeys(List<ChromKey> listChromKeys)
        {
            AddChromKeys(ChromSource.ms1, Ms1ProductFilters, false, listChromKeys);
            AddChromKeys(ChromSource.sim, SimProductFilters, false, listChromKeys);
            AddChromKeys(ChromSource.fragment, Ms2ProductFilters, true, listChromKeys);
        }

        private void AddChromKeys(ChromSource source, SpectrumProductFilter[] productFilters, bool highEnergy,
                                  List<ChromKey> listChromKeys)
        {
            if (null != productFilters)
            {
                var ionMobilityFilter = GetIonMobilityWindow();
                foreach (var spectrumProductFilter in productFilters)
                {
                    spectrumProductFilter.FilterId = listChromKeys.Count;
                    var key = new ChromKey(ChromatogramGroupId,
                        Q1,
                        ionMobilityFilter.ApplyOffset(highEnergy ? spectrumProductFilter.HighEnergyIonMobilityValueOffsetLow : 0,
                            highEnergy ? spectrumProductFilter.HighEnergyIonMobilityValueOffsetHigh : 0),
                        spectrumProductFilter.TargetMz,
                        OptStep ?? 0,
                        0,  // CE value (Shimadzu SRM only)
                        spectrumProductFilter.FilterWidth,
                        source,
                        Extractor);
                    if (_hasMinTime && _hasMaxTime)
                    {
                        key = key.ChangeOptionalTimes(_minTime, _maxTime);
                    }
                    listChromKeys.Add(key);
                }
            }
        }

        public bool HasIonMobilityFAIMS()
        {
            return IonMobilityInfo.IonMobility.Units == eIonMobilityUnits.compensation_V &&
                   IonMobilityInfo.HasIonMobilityValue;
        }

        public bool ContainsIonMobilityValue(IonMobilityValue ionMobility, double highEnergyOffsetLow, double highEnergyOffsetHigh)
        {
            return !ionMobility.HasValue || ContainsIonMobilityValue(ionMobility.Mobility.Value, highEnergyOffsetLow, highEnergyOffsetHigh);
        }

        public bool ContainsIonMobilityValue(double ionMobilityValue, double highEnergyOffsetLow, double highEnergyOffsetHigh)
        {
            return (!MinIonMobilityValue.HasValue || MinIonMobilityValue.Value + highEnergyOffsetLow <= ionMobilityValue) &&
                   (!MaxIonMobilityValue.HasValue || MaxIonMobilityValue.Value + highEnergyOffsetHigh >= ionMobilityValue);
        }

        public IonMobilityFilter GetIonMobilityWindow()
        {
            if (MinIonMobilityValue.HasValue && MaxIonMobilityValue.HasValue)
            {
                // High energy (product ion) scans may have a faster ion mobility, as in Waters MsE, that gets applied elsewhere
                var width = MaxIonMobilityValue.Value - MinIonMobilityValue.Value;
                var center = MinIonMobilityValue.Value + 0.5*width;
                return IonMobilityFilter.GetIonMobilityFilter(center, IonMobilityInfo.IonMobility.Units, width, IonMobilityInfo.CollisionalCrossSectionSqA);
            }
            else
            {
                return IonMobilityFilter.EMPTY;
            }
        }

        public bool GetIsKnownWindowGroup(int windowGroup)
        {
            return windowGroup == SpectrumFilter.WINDOWGROUP_NONE || ((_knownWindowGroupsBitField & (1 << (windowGroup-1))) != 0); // WindowGroup is 1-based
        }

        public void SetIsKnownWindowGroup(int windowGroup)
        {
            if (windowGroup == SpectrumFilter.WINDOWGROUP_NONE)
            {
                _knownWindowGroupsBitField = -1; // None of the window groups was a match
            }
            else
            {
                _knownWindowGroupsBitField |= (1 << (windowGroup - 1)); // WindowGroup is 1-based
            }
        }

        public bool IsBestWindowGroup(int windowGroup)
        {
            return windowGroup == BestWindowGroup;
        }

        public bool ProposeBestWindowGroup(int proposedBestWindowGroup, double area, double distance)
        {
            // We look at how much of the target mz x IM search area overlaps that in the window group
            // In a tie, we go for the window that is most centered on the target
            if (BestWindowGroup > 0)
            {
                // We have identified at least one window that hits - update?
                if (area < BestWindowGroupMatchArea)
                {
                    // Proposed window is not better than current winner, but note that we checked this window
                    SetIsKnownWindowGroup(proposedBestWindowGroup);
                    return false;
                }
                if (area >= BestWindowGroupMatchArea || // Better coverage
                    distance < BestWindowGroupMatchDistance) // Same coverage, take the one closest to center
                {
                    BestWindowGroupMatchArea = (float)area;
                    BestWindowGroupMatchDistance = (float)distance;
                    BestWindowGroup = proposedBestWindowGroup;
                    SetIsKnownWindowGroup(proposedBestWindowGroup);
//Console.WriteLine($@"{this.ChromatogramGroupId} wg={BestWindowGroup}");
                    return true;
                }
                // Already have a best window and this is it
                if (BestWindowGroup == proposedBestWindowGroup)
                {
                    return true;
                }
            }
            else if (area <= 0)
            {
                // No hit on this window, nor any other yet - reserve judgment, don't mark as known
                return false;
            }
            BestWindowGroup = proposedBestWindowGroup;
            BestWindowGroupMatchArea = (float)area;
            BestWindowGroupMatchDistance = (float)distance;
            SetIsKnownWindowGroup(BestWindowGroup);
            return true;
        }

        public bool MatchesDdaPrecursor(SignedMz precursorMz)
        {
            return Ms1ProductFilters.Any(filter => 0 == filter.TargetMz.CompareTolerant(precursorMz, filter.FilterWidth));
        }

        public bool IsOutsideSpectraRangeIM(MsDataSpectrum[] spectra, double? minIonMobilityValue, double? maxIonMobilityValue)
        {
            // For distinguishing zero IM values from not-measured IM values
            return minIonMobilityValue.HasValue && maxIonMobilityValue.HasValue &&
                   spectra.All(s =>
                       (s.IonMobilityMeasurementRangeLow.HasValue &&
                        s.IonMobilityMeasurementRangeLow > maxIonMobilityValue) ||
                       (s.IonMobilityMeasurementRangeHigh.HasValue &&
                        s.IonMobilityMeasurementRangeHigh < minIonMobilityValue));
        }

        public bool MatchesSpectrum(SpectrumMetadata spectrumMetadata)
        {
            if (SpectrumClassFilter.IsEmpty)
            {
                return true;
            }

            return SpectrumClassFilter.MakePredicate()(spectrumMetadata);
        }

        public override string ToString() // For debug convenience
        {
            return $@"{Q1} {BestWindowGroup}";
        }
    }

    internal class IonMobilityRangeHelper
    {
        private readonly bool _isDescending;    // i.e. 1/K0
        private double? _endIonMobilityValue;
        private double _endOffset;

        public IonMobilityRangeHelper(MsDataSpectrum[] spectra, SpectrumProductFilter[] productFilters,
            double? minIonMobilityValue, double? maxIonMobilityValue)
        {
            _isDescending = IsDescending(spectra);
            double? startIonMobilityValue = minIonMobilityValue;
            _endIonMobilityValue = maxIonMobilityValue;
            SwapIfDescending(ref startIonMobilityValue, ref _endIonMobilityValue);

            double startOffset = 0;
            if (productFilters != null)
            {
                startOffset = productFilters.Select(f => f.HighEnergyIonMobilityValueOffsetLow).Min();
                _endOffset = productFilters.Select(f => f.HighEnergyIonMobilityValueOffsetHigh).Max();
                SwapIfDescending(ref startOffset, ref _endOffset);
            }

            if (startIonMobilityValue.HasValue)
            {
                double im = startIonMobilityValue.Value;
                if (startOffset != 0)
                    im += startOffset;   // For breakpoint

                if (spectra.Length != 1)
                {
                    IndexFirst = CollectionUtil.BinarySearch(spectra, s => Compare(s, im), true);
                    if (IndexFirst < 0)
                        IndexFirst = ~IndexFirst;
                }
                else
                {
                    var spectrum = spectra[0];
                    if (spectrum.IonMobilities != null)
                    {
                        double startSpectrumIonMobility = spectrum.MinIonMobility.Value;
                        double endSpectrumIonMobility = spectrum.MaxIonMobility.Value;
                        SwapIfDescending(ref startSpectrumIonMobility, ref endSpectrumIonMobility);
                        if (Compare(endSpectrumIonMobility, startIonMobilityValue.Value + startOffset) < 0 ||
                            Compare(startSpectrumIonMobility, _endIonMobilityValue.Value + _endOffset) > 0)
                        {
                            IndexFirst = 1;
                        }
                    }
                }
            }
        }

        public int IndexFirst { get; private set; }

        public int DirectionMultiplier { get { return _isDescending ? -1 : 1; } }

        private bool IsDescending(MsDataSpectrum[] spectra)
        {
            if (spectra.Length > 1)
            {
                var im0 = spectra[0].IonMobility.Mobility;
                var im1 = spectra[1].IonMobility.Mobility;
                if (im0.HasValue && im1.HasValue)
                    return im0 > im1;
            }
            return false;
        }

        public bool IsBeyondRange(MsDataSpectrum spectrum)
        {
            if (!_endIonMobilityValue.HasValue)
                return false;

            double im = _endIonMobilityValue.Value;
            if (_endOffset != 0)
                im += _endOffset;   // For breakpoint

            return Compare(spectrum, im) > 0;
        }

        private void SwapIfDescending<TItem>(ref TItem item1, ref TItem item2)
        {
            if (_isDescending)
                Helpers.Swap(ref item1, ref item2);
        }

        private int Compare(MsDataSpectrum spectrum, double ionMobilityValue)
        {
            var sim = spectrum.IonMobility.Mobility;
            if (!sim.HasValue)
                return 0;

            return Compare(sim.Value, ionMobilityValue);
        }

        private int Compare(double ionMobilityValue1, double ionMobilityValue2)
        {
            return DirectionMultiplier * ionMobilityValue1.CompareTo(ionMobilityValue2);
        }
    }

    public class SpectrumProductFilter
    {
        public SpectrumProductFilter(double targetMz, double filterWidth, double? highEnergyIonMobilityValueOffsetLow = null, double? highEnergyIonMobilityValueOffsetHigh = null) :
            this(new SignedMz(targetMz), filterWidth, highEnergyIonMobilityValueOffsetLow, highEnergyIonMobilityValueOffsetHigh)
        {
        }

        public SpectrumProductFilter(SignedMz targetMz, double filterWidth, double? highEnergyIonMobilityValueOffsetLow = null, double? highEnergyIonMobilityValueOffsetHigh = null)
        {
            TargetMz = targetMz;
            FilterWidth = filterWidth;
            HighEnergyIonMobilityValueOffsetLow = highEnergyIonMobilityValueOffsetLow ?? 0; // Shift lower edge of window
            HighEnergyIonMobilityValueOffsetHigh= highEnergyIonMobilityValueOffsetHigh ?? HighEnergyIonMobilityValueOffsetLow; // Shift upper edge of window
        }

        public SignedMz TargetMz { get; private set; }
        public double FilterWidth { get; private set; }
        public int FilterId { get; set; }
        public double HighEnergyIonMobilityValueOffsetLow { get; set; }
        public double HighEnergyIonMobilityValueOffsetHigh { get; set; }


        #region object overrides

        protected bool Equals(SpectrumProductFilter other)
        {
            return TargetMz.Equals(other.TargetMz) && FilterWidth.Equals(other.FilterWidth) &&
                   FilterId == other.FilterId && 
                   Equals(HighEnergyIonMobilityValueOffsetLow, other.HighEnergyIonMobilityValueOffsetLow) &&
                   Equals(HighEnergyIonMobilityValueOffsetHigh, other.HighEnergyIonMobilityValueOffsetHigh);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SpectrumProductFilter) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = TargetMz.GetHashCode();
                hashCode = (hashCode*397) ^ FilterWidth.GetHashCode();
                hashCode = (hashCode*397) ^ FilterId;
                hashCode = (hashCode * 397) ^ HighEnergyIonMobilityValueOffsetLow.GetHashCode();
                hashCode = (hashCode * 397) ^ HighEnergyIonMobilityValueOffsetHigh.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString() // For debug convenience
        {
            return HighEnergyIonMobilityValueOffsetLow == HighEnergyIonMobilityValueOffsetHigh ? 
                $@"mz={TargetMz} w={FilterWidth} id={FilterId} heo={HighEnergyIonMobilityValueOffsetLow}" : 
                $@"mz={TargetMz} w={FilterWidth} id={FilterId} heo={HighEnergyIonMobilityValueOffsetLow}:{HighEnergyIonMobilityValueOffsetHigh}";
        }

        #endregion
    }

    public class SpectrumFilterValues : IComparable<SpectrumFilterValues>, IComparable
    {
        public SignedMz mz;
        public double ionMobilityHighEnergyOffsetLow; // Allow for asymmetrical ion mobility window adjustments
        public double ionMobilityHighEnergyOffsetHigh;

        public SpectrumFilterValues(SignedMz mz, double ionMobilityHighEnergyOffsetLow, double? ionMobilityHighEnergyOffsetHigh = null)
        {
            this.mz = mz;
            this.ionMobilityHighEnergyOffsetLow = ionMobilityHighEnergyOffsetLow;
            this.ionMobilityHighEnergyOffsetHigh = ionMobilityHighEnergyOffsetHigh ?? ionMobilityHighEnergyOffsetLow;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SpectrumFilterValues);
        }

        public bool Equals(SpectrumFilterValues other)
        {
            return other != null &&
                   mz.Equals(other.mz) &&
                   ionMobilityHighEnergyOffsetLow == other.ionMobilityHighEnergyOffsetLow &&
                   ionMobilityHighEnergyOffsetHigh == other.ionMobilityHighEnergyOffsetHigh;
        }

        public override int GetHashCode()
        {
            var hashCode = 1155459730;
            hashCode = hashCode * -1521134295 + EqualityComparer<SignedMz>.Default.GetHashCode(mz);
            hashCode = hashCode * -1521134295 + ionMobilityHighEnergyOffsetLow.GetHashCode();
            hashCode = hashCode * -1521134295 + ionMobilityHighEnergyOffsetHigh.GetHashCode();
            return hashCode;
        }

        public override string ToString() // For debug convenience, not user facing
        {
            return ionMobilityHighEnergyOffsetLow == ionMobilityHighEnergyOffsetHigh ?
                $@"mz={mz} heo={ionMobilityHighEnergyOffsetLow}" : 
                $@"mz={mz} heo={ionMobilityHighEnergyOffsetLow}:{ionMobilityHighEnergyOffsetHigh}";
        }

        public int CompareTo(SpectrumFilterValues other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var mzComparison = mz.CompareTo(other.mz);
            if (mzComparison != 0) return mzComparison;
            var lowComparison = ionMobilityHighEnergyOffsetLow.CompareTo(other.ionMobilityHighEnergyOffsetLow);
            if (lowComparison != 0) return lowComparison;
            return ionMobilityHighEnergyOffsetHigh.CompareTo(other.ionMobilityHighEnergyOffsetHigh);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            if (!(obj is SpectrumFilterValues)) throw new ArgumentException(@"Object must be of type SpectrumFilterValues");
            return CompareTo((SpectrumFilterValues)obj);
        }
    }
}