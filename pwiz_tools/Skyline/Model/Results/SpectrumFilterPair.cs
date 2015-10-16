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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public sealed class SpectrumFilterPair : IComparable<SpectrumFilterPair>
    {
        private static readonly SpectrumProductFilter[] EMPTY_FILTERS = new SpectrumProductFilter[0];

        public SpectrumFilterPair(PrecursorTextId precursorTextId, Color peptideColor, int id, double? minTime, double? maxTime,
            double? minDriftTimeMsec, double? maxDriftTimeMsec, double highEnergyDriftTimeOffsetMsec, bool highAccQ1, bool highAccQ3)
        {
            Id = id;
            ModifiedSequence = precursorTextId.TextId;
            PeptideColor = peptideColor;
            Q1 = precursorTextId.PrecursorMz;
            Extractor = precursorTextId.Extractor;
            MinTime = minTime;
            MaxTime = maxTime;
            MinDriftTimeMsec = minDriftTimeMsec;
            MaxDriftTimeMsec = maxDriftTimeMsec;
            HighEnergyDriftTimeOffsetMsec = highEnergyDriftTimeOffsetMsec;
            HighAccQ1 = highAccQ1;
            HighAccQ3 = highAccQ3;

            Ms1ProductFilters = SimProductFilters = Ms2ProductFilters = EMPTY_FILTERS;

            if (Q1 == 0)
            {
                Ms1ProductFilters = new[] {new SpectrumProductFilter(0, 0)};
            }
        }

        public SpectrumFilterPair(ChromatogramRequestDocumentChromatogramGroup requestGroup)
        {
            Q1 = requestGroup.PrecursorMz;
            ModifiedSequence = requestGroup.ModifiedSequence;
            switch (requestGroup.Extractor)
            {
                case RemoteApi.GeneratedCode.ChromExtractor.BasePeak:
                    Extractor = ChromExtractor.base_peak;
                    break;
                case RemoteApi.GeneratedCode.ChromExtractor.Summed:
                    Extractor = ChromExtractor.summed;
                    break;
            }
            if (requestGroup.MinTimeSpecified)
            {
                MinTime = requestGroup.MinTime;
            }
            if (requestGroup.MaxTimeSpecified)
            {
                MaxTime = requestGroup.MaxTime;
            }
            switch (requestGroup.Source)
            {
                case RemoteApi.GeneratedCode.ChromSource.Ms1:
                    Ms1ProductFilters = requestGroup.Chromatogram.Select(
                        product => new SpectrumProductFilter(product.ProductMz, product.MzWindow)).ToArray();
                    HighAccQ1 = requestGroup.MassErrors;
                    break;
                case RemoteApi.GeneratedCode.ChromSource.Ms2:
                    Ms2ProductFilters = requestGroup.Chromatogram.Select(
                        product => new SpectrumProductFilter(product.ProductMz, product.MzWindow)).ToArray();
                    HighAccQ3 = requestGroup.MassErrors;
                    break;
            }
        }

        public int Id { get; private set; }
        public ChromExtractor Extractor { get; private set; }
        public bool HighAccQ1 { get; private set; }
        public bool HighAccQ3 { get; private set; }
        public string ModifiedSequence { get; private set; }
        public Color PeptideColor { get; private set; }
        public double Q1 { get; private set; }
        public double? MinTime { get; private set; }
        public double? MaxTime { get; private set; }
        private double? MinDriftTimeMsec { get; set; }
        private double? MaxDriftTimeMsec { get; set; }
        private double? HighEnergyDriftTimeOffsetMsec { get; set; }
        private SpectrumProductFilter[] Ms1ProductFilters { get; set; }
        private SpectrumProductFilter[] SimProductFilters { get; set; }
        public SpectrumProductFilter[] Ms2ProductFilters { get; set; }

        public void AddQ1FilterValues(IEnumerable<double> filterValues, Func<double, double> getFilterWindow)
        {
            AddFilterValues(MergeFilters(Ms1ProductFilters, filterValues).Distinct(), getFilterWindow,
                filters => Ms1ProductFilters = filters);
            // Make complete copies for SIM scans. Some day these may be different.
            SimProductFilters = Ms1ProductFilters.Select(f => new SpectrumProductFilter(f.TargetMz, f.FilterWidth)).ToArray();
        }

        public void AddQ3FilterValues(IEnumerable<double> filterValues, Func<double, double> getFilterWindow)
        {
            AddFilterValues(MergeFilters(Ms2ProductFilters, filterValues).Distinct(), getFilterWindow,
                filters => Ms2ProductFilters = filters);
        }

        private static IEnumerable<double> MergeFilters(IEnumerable<SpectrumProductFilter> existing, IEnumerable<double> added)
        {
            if (existing == null)
                return added;
            return existing.Select(f => f.TargetMz).Union(added);
        }

        private static void AddFilterValues(IEnumerable<double> filterValues,
                                            Func<double, double> getFilterWindow,
                                            Action<SpectrumProductFilter[]> setProductFilters)
        {
            var listQ3 = filterValues.ToList();

            listQ3.Sort();

            setProductFilters(listQ3.Select(mz => new SpectrumProductFilter(mz, getFilterWindow(mz))).ToArray());
        }

        public ExtractedSpectrum FilterQ1SpectrumList(MsDataSpectrum[] spectra, bool isSimSpectra = false)
        {
            var filters = isSimSpectra ? SimProductFilters : Ms1ProductFilters;
            return FilterSpectrumList(spectra, filters, HighAccQ1, false);
        }

        public ExtractedSpectrum FilterQ3SpectrumList(MsDataSpectrum[] spectra, bool useDriftTimeHighEnergyOffset)
        {
            // All-ions extraction for MS1 scans only
            if (Q1 == 0)
                return null;

            return FilterSpectrumList(spectra, Ms2ProductFilters, HighAccQ3, useDriftTimeHighEnergyOffset);
        }

        /// <summary>
        /// Apply the filter to a list of spectra.  In "normal" operation
        /// this list has a length of one. For ion mobility data it
        /// may be a list of spectra with the same retention time but
        /// different drift times. For Agilent Mse data it may be
        /// a list of MS2 spectra that need averaging (or even a list
        /// of MS2 spectra with mixed retention and drift times).  Averaging
        /// is done by unique retention time count, rather than by spectrum
        /// count, so that ion mobility data ion counts are additive (we're
        /// trying to measure ions per injection, basically).
        /// </summary>
        private ExtractedSpectrum FilterSpectrumList(IEnumerable<MsDataSpectrum> spectra,
            SpectrumProductFilter[] productFilters, bool highAcc, bool useDriftTimeHighEnergyOffset)
        {
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

            int rtCount = 0;
            double lastRT = 0;
            foreach (var spectrum in spectra)
            {
                // If these are spectra from distinct retention times, average them.
                // Note that for drift time data we will see fewer retention time changes 
                // than the total spectra count - ascending DT within each RT.  Within a
                // single retention time the ions are additive.
                var rt = spectrum.RetentionTime ?? 0;
                if (lastRT != rt)
                {
                    rtCount++;
                    lastRT = rt;
                }

                // Filter on drift time, if any
                Assume.IsTrue(!useDriftTimeHighEnergyOffset || spectrum.Id.StartsWith("2")); // Not L10N
                if (!ContainsDriftTime(spectrum.DriftTimeMsec, useDriftTimeHighEnergyOffset))
                    continue;

                var mzArray = spectrum.Mzs;
                if ((mzArray == null) || (mzArray.Length==0))
                    continue;

                // It's not unusual for mzarray and centerArray to have no overlap, esp. with drift time data
                if (Q1 != 0)
                {
                    var lastProductFilter = productFilters[targetCount - 1];
                    if ((lastProductFilter.TargetMz + lastProductFilter.FilterWidth/2) < mzArray[0])
                        continue;
                }

                var intensityArray = spectrum.Intensities;

                // Search for matching peaks for each Q3 filter
                // Use binary search to get to the first m/z value to be considered more quickly
                // This should help MS1 where isotope distributions will be very close in m/z
                // It should also help MS/MS when more selective, larger fragment ions are used,
                // since then a lot of less selective, smaller peaks must be skipped
                int iPeak = 0;
                for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
                {
                    // Look for the first peak that is greater than the start of the filter
                    double targetMz = 0, endFilter = double.MaxValue;
                    if (Q1 != 0)
                    {
                        var productFilter = productFilters[targetIndex];
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

                    // Add the intensity values of all peaks that pass the filter
                    double totalIntensity = extractedIntensities[targetIndex]; // Start with the value from the previous spectrum, if any
                    double meanError =  highAcc ? meanErrors[targetIndex] : 0;
                    for (int iNext = iPeak; iNext < mzArray.Length && mzArray[iNext] < endFilter; iNext++)
                    {
                        double mz = mzArray[iNext];
                        double intensity = intensityArray[iNext];
                    
                        if (Extractor == ChromExtractor.summed)
                            totalIntensity += intensity;
                        else if (intensity > totalIntensity)
                        {
                            totalIntensity = intensity;
                            meanError = 0;
                        }

                        // Accumulate weighted mean mass error for summed, or take a single
                        // mass error of the most intense peak for base peak.
                        if (highAcc && (Extractor == ChromExtractor.summed || meanError == 0))
                        {
                            if (totalIntensity > 0.0)
                            {
                                double deltaPeak = mz - targetMz;
                                meanError += (deltaPeak - meanError) * intensity / totalIntensity;
                            }
                        }
                    }
                    extractedIntensities[targetIndex] = (float) totalIntensity;
                    if (meanErrors != null)
                        meanErrors[targetIndex] = meanError;
                }
                
            }
            if (meanErrors != null)
            {
                for (int i = 0; i < targetCount; i++)
                    massErrors[i] = (float)SequenceMassCalc.GetPpm(productFilters[i].TargetMz, meanErrors[i]);
            }

            // If we summed across spectra of different retention times, scale per
            // unique retention time (but not per drift time)
            if ((Extractor == ChromExtractor.summed) && (rtCount > 1))
            {
                float scale = (float)(1.0 / rtCount);
                for (int i = 0; i < targetCount; i++)
                    extractedIntensities[i] *= scale;
            }
            double dtCenter, dtWidth;
            GetDriftTimeWindow(out dtCenter, out dtWidth, useDriftTimeHighEnergyOffset);
            return new ExtractedSpectrum(ModifiedSequence,
                PeptideColor,
                Q1,
                dtCenter,
                dtWidth,
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

        public bool ContainsRetentionTime(double retentionTime)
        {
            return (!MinTime.HasValue || MinTime.Value <= retentionTime) &&
                (!MaxTime.HasValue || MaxTime.Value >= retentionTime);
        }

        public IEnumerable<ChromatogramRequestDocumentChromatogramGroup> ToChromatogramRequestDocumentChromatogramGroups()
        {
            if (null != Ms1ProductFilters)
            {
                var chromatograms = new List<ChromatogramRequestDocumentChromatogramGroupChromatogram>();
                foreach (var spectrumProductFilter in Ms1ProductFilters)
                {
                    var product = new ChromatogramRequestDocumentChromatogramGroupChromatogram
                    {
                        ProductMz = spectrumProductFilter.TargetMz,
                        MzWindow = spectrumProductFilter.FilterWidth,
                    };
                    chromatograms.Add(product);
                }
                if (chromatograms.Count > 0)
                {
                    yield return MakeChromatogramRequestDocumentChromatogramGroup(ChromSource.ms1, HighAccQ1 && 0 != Q1, chromatograms);
                }
            }
            if (null != Ms2ProductFilters)
            {
                var chromatograms = new List<ChromatogramRequestDocumentChromatogramGroupChromatogram>();
                foreach (var spectrumProductFilter in Ms2ProductFilters)
                {
                    var product = new ChromatogramRequestDocumentChromatogramGroupChromatogram
                    {
                        ProductMz = spectrumProductFilter.TargetMz,
                        MzWindow = spectrumProductFilter.FilterWidth,
                    };
                    chromatograms.Add(product);
                }
                if (chromatograms.Count > 0)
                {
                    yield return
                        MakeChromatogramRequestDocumentChromatogramGroup(ChromSource.fragment, HighAccQ3, chromatograms);
                }
            }
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
                double? imCenterOpt = null;
                double imCenter, imWidth;
                if (GetDriftTimeWindow(out imCenter, out imWidth, highEnergy))
                    imCenterOpt = imCenter;
                foreach (var spectrumProductFilter in productFilters)
                {
                    spectrumProductFilter.FilterId = listChromKeys.Count;
                    var key = new ChromKey(ModifiedSequence,
                        Q1,
                        imCenterOpt,
                        imWidth,
                        spectrumProductFilter.TargetMz,
                        0,  // CE value (Shimadzu SRM only)
                        spectrumProductFilter.FilterWidth,
                        source,
                        Extractor,
                        true,
                        true,
                        MinTime,
                        MaxTime);
                    listChromKeys.Add(key);
                }
            }
        }

        private ChromatogramRequestDocumentChromatogramGroup MakeChromatogramRequestDocumentChromatogramGroup(
            ChromSource chromSource, bool calculateMassErrors, IEnumerable<ChromatogramRequestDocumentChromatogramGroupChromatogram> chromatograms)
        {
            ChromatogramRequestDocumentChromatogramGroup docFilterPair = new ChromatogramRequestDocumentChromatogramGroup
            {
                ModifiedSequence = ModifiedSequence,
                PrecursorMz = Q1,
                MassErrors = calculateMassErrors,
            };
            switch (Extractor)
            {
                case ChromExtractor.base_peak:
                    docFilterPair.Extractor = RemoteApi.GeneratedCode.ChromExtractor.BasePeak;
                    break;
                case ChromExtractor.summed:
                    docFilterPair.Extractor = RemoteApi.GeneratedCode.ChromExtractor.Summed;
                    break;
            }
            if (MinTime.HasValue)
            {
                docFilterPair.MinTime = MinTime.Value;
                docFilterPair.MinTimeSpecified = true;
            }
            if (MaxTime.HasValue)
            {
                docFilterPair.MaxTime = MaxTime.Value;
                docFilterPair.MaxTimeSpecified = true;
            }
            if (MinDriftTimeMsec.HasValue && MaxDriftTimeMsec.HasValue)
            {
                docFilterPair.DriftTime = (MinDriftTimeMsec.Value + MaxDriftTimeMsec.Value)/2;
                if (ChromSource.fragment == chromSource && HighEnergyDriftTimeOffsetMsec.HasValue)
                {
                    docFilterPair.DriftTime += HighEnergyDriftTimeOffsetMsec.Value;
                }
                docFilterPair.DriftTimeSpecified = true;
                docFilterPair.DriftTimeWindow = MaxDriftTimeMsec.Value - MinDriftTimeMsec.Value;
                docFilterPair.DriftTimeWindowSpecified = true;
            }
            switch (chromSource)
            {
                case ChromSource.ms1:
                    docFilterPair.Source = RemoteApi.GeneratedCode.ChromSource.Ms1;
                    break;
                case ChromSource.fragment:
                    docFilterPair.Source = RemoteApi.GeneratedCode.ChromSource.Ms2;
                    break;
                case ChromSource.sim:
                    docFilterPair.Source = RemoteApi.GeneratedCode.ChromSource.Sim;
                    break;
            }
            docFilterPair.Chromatogram = chromatograms.ToArray();
            return docFilterPair;
        }

        public bool ContainsDriftTime(double? driftTimeMsec, bool highEnergy)
        {
            if (!driftTimeMsec.HasValue)
                return true; // It doesn't NOT have the drift time, since there isn't one
            double offset = (highEnergy ? HighEnergyDriftTimeOffsetMsec??0 : 0);
            return (!MinDriftTimeMsec.HasValue || MinDriftTimeMsec.Value+offset <= driftTimeMsec) &&
                (!MaxDriftTimeMsec.HasValue || MaxDriftTimeMsec.Value+offset >= driftTimeMsec);
        }

        public bool GetDriftTimeWindow(out double center, out double width, bool highEnergy)
        {
            if (MinDriftTimeMsec.HasValue && MaxDriftTimeMsec.HasValue)
            {
                // High energy (product ion) scans may have a faster drift time, as in Waters MsE
                double offset = (highEnergy ? HighEnergyDriftTimeOffsetMsec ?? 0 : 0);
                width = MaxDriftTimeMsec.Value - MinDriftTimeMsec.Value;
                center = offset + MinDriftTimeMsec.Value + 0.5*width;
                return true;
            }
            else
            {
                width = 0;
                center = 0;
                return false;
            }
        }
    }

    public class SpectrumProductFilter
    {
        public SpectrumProductFilter(double targetMz, double filterWidth)
        {
            TargetMz = targetMz;
            FilterWidth = filterWidth;
        }

        public double TargetMz { get; private set; }
        public double FilterWidth { get; private set; }
        public int FilterId { get; set; }

        #region object overrides

        protected bool Equals(SpectrumProductFilter other)
        {
            return TargetMz.Equals(other.TargetMz) && FilterWidth.Equals(other.FilterWidth) && FilterId == other.FilterId;
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
                return hashCode;
            }
        }

        #endregion
    }
}