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
using System.IO;
using System.Linq;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal abstract class ChromDataProvider
    {
        private readonly int _startPercent;
        private readonly int _endPercent;
        private readonly IProgressMonitor _loader;

        protected ChromDataProvider(ProgressStatus status, int startPercent, int endPercent, IProgressMonitor loader)
        {
            Status = status;

            _startPercent = startPercent;
            _endPercent = endPercent;
            _loader = loader;
        }

        protected void SetPercentComplete(int percent)
        {
            if (_loader.IsCanceled)
            {
                _loader.UpdateProgress(Status = Status.Cancel());
                throw new LoadCanceledException(Status);
            }

            percent = Math.Min(_endPercent, (_endPercent - _startPercent) * percent / 100 + _startPercent);
            if (Status.IsPercentComplete(percent))
                return;

            _loader.UpdateProgress(Status = Status.ChangePercentComplete(percent));
        }

        public ProgressStatus Status { get; private set; }

        public abstract IEnumerable<KeyValuePair<ChromKey, int>> ChromIds { get; }

        public abstract void GetChromatogram(int id, out float[] times, out float[] intensities);

        public abstract bool IsProcessedScans { get; }
    }

    internal sealed class ChromatogramDataProvider : ChromDataProvider
    {
        private readonly IList<KeyValuePair<ChromKey, int>> _chromIds =
            new List<KeyValuePair<ChromKey, int>>();

        private readonly MsDataFileImpl _dataFile;

        /// <summary>
        /// The number of chromatograms read so far.
        /// </summary>
        private int _readChromatograms;

        /// <summary>
        /// Records the time at which chromatogram loading began to allow prediction
        /// of how long the file load will take.
        /// </summary>
        private DateTime _readStartTime;

        /// <summary>
        /// If the predicted time to load this file ever exceeds this threshold,
        /// a <see cref="LoadingTooSlowlyException"/> is thrown.
        /// </summary>
        private readonly double _readMaxMinutes;

        /// <summary>
        /// Possible work-around for a <see cref="LoadingTooSlowlyException"/>
        /// </summary>
        private readonly LoadingTooSlowlyException.Solution _slowLoadWorkAround;

        public ChromatogramDataProvider(MsDataFileImpl dataFile,
                                        bool throwIfSlow,
                                        ProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(status, startPercent, endPercent, loader)
        {
            _dataFile = dataFile;

            if (throwIfSlow)
            {
                // Both WIFF files and Thermo Raw files have potential performance bugs
                // that have work-arounds.  WIFF files tend to take longer to load early
                // chromatograms, while Thermo files remain fairly consistent.
                if (_dataFile.IsABFile)
                {
                    _readMaxMinutes = 4;
                    _slowLoadWorkAround = LoadingTooSlowlyException.Solution.mzwiff_conversion;
                }
                else if (_dataFile.IsThermoFile)
                {
                    _readMaxMinutes = 4;
                    _slowLoadWorkAround = LoadingTooSlowlyException.Solution.local_file;
                }
            }

            int len = dataFile.ChromatogramCount;

            for (int i = 0; i < len; i++)
            {
                int index;
                string id = dataFile.GetChromatogramId(i, out index);

                if (!ChromKey.IsKeyId(id))
                    continue;

                var ki = new KeyValuePair<ChromKey, int>(ChromKey.FromId(id), index);
                _chromIds.Add(ki);
            }

            if (_chromIds.Count == 0)
                throw new NoSrmDataException();

            SetPercentComplete(50);
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get { return _chromIds; }
        }

        public override void GetChromatogram(int id, out float[] times, out float[] intensities)
        {
            if (_readChromatograms == 0)
            {
                _readStartTime = DateTime.Now;
            }

            string chromId;
            _dataFile.GetChromatogram(id, out chromId, out times, out intensities);

            // Assume that each chromatogram will be read once, though this may
            // not always be completely true.
            _readChromatograms++;

            double predictedMinutes = ExpectedReadDurationMinutes;
            if (_readMaxMinutes > 0 && predictedMinutes > _readMaxMinutes)
            {
                throw new LoadingTooSlowlyException(_slowLoadWorkAround, Status, predictedMinutes, _readMaxMinutes);
            }

            if (_readChromatograms < _chromIds.Count)
                SetPercentComplete(50 + _readChromatograms * 50 / _chromIds.Count);
        }

        private double ExpectedReadDurationMinutes
        {
            get { return DateTime.Now.Subtract(_readStartTime).TotalMinutes * _chromIds.Count / _readChromatograms; }
        }

        public override bool IsProcessedScans
        {
            get { return false; }
        }

        public static bool HasChromatogramData(MsDataFileImpl dataFile)
        {
            int len = dataFile.ChromatogramCount;

            // Many files have just one TIC chromatogram
            if (len < 2)
                return false;

            for (int i = 0; i < len; i++)
            {
                int index;
                string id = dataFile.GetChromatogramId(i, out index);

                if (ChromKey.IsKeyId(id))
                    return true;
            }
            return false;
        }
    }

    internal sealed class SpectraChromDataProvider : ChromDataProvider
    {
        private readonly List<KeyValuePair<ChromKey, ChromCollector>> _chromatograms =
            new List<KeyValuePair<ChromKey, ChromCollector>>();

        private readonly bool _isProcessedScans;

        public SpectraChromDataProvider(MsDataFileImpl dataFile,
                                        SrmDocument document,
                                        ProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(status, startPercent, endPercent, loader)
        {
            // 10% done with this file
            SetPercentComplete(10);

            // Create a spectrum filter data structure, in case it is needed
            // This could be done lazily, but does not seem worth it, given the file reading
            var filter = new SpectrumFilter(document);

            // First read all of the spectra, building chromatogram time, intensity lists
            var chromMap = new Dictionary<double, ChromDataCollector>();
            var chromMapMs1 = new Dictionary<double, ChromDataCollector>();
            int lenSpectra = dataFile.SpectrumCount;
            bool isSrm = dataFile.HasSrmSpectra;
            // Thermo high accuracy data currently returns bogus precursor m/z values
            // for high accuracy targeted MS/MS
            bool isIsolationPrimary = dataFile.IsThermoFile && filter.IsHighAccProductFilter;
            int eighth = 0;
            for (int i = 0; i < lenSpectra; i++)
            {
                // Update progress indicator
                if (i * 8 / lenSpectra > eighth)
                {
                    eighth++;
                    SetPercentComplete((eighth + 1) * 10);
                }

                if (isSrm)
                {
                    var dataSpectrum = dataFile.GetSrmSpectrum(i);
                    if (dataSpectrum.Level != 2)
                        continue;

                    if (!dataSpectrum.RetentionTime.HasValue)
                        throw new InvalidDataException(String.Format("Scan {0} found without scan time.", dataFile.GetSpectrumId(i)));
                    if (!dataSpectrum.PrecursorMz.HasValue)
                        throw new InvalidDataException(String.Format("Scan {0} found without precursor m/z.", dataFile.GetSpectrumId(i)));

                    // Process the one SRM spectrum
                    ProcessSrmSpectrum(dataSpectrum.RetentionTime.Value,
                                       dataSpectrum.PrecursorMz.Value,
                                       dataSpectrum.Mzs,
                                       dataSpectrum.Intensities,
                                       chromMap);
                }
                else if (filter.EnabledMsMs || filter.EnabledMs)
                {
                    // If MS/MS filtering is not enabled, skip anything that is not a MS1 scan
                    if (!filter.EnabledMsMs && dataFile.GetMsLevel(i) != 1)
                        continue;

                    var dataSpectrum = dataFile.GetSpectrum(i);
                    
                    double? precursorMz = (isIsolationPrimary ? dataSpectrum.IsolationWindow : null);
                    if (!precursorMz.HasValue)
                        precursorMz = dataSpectrum.PrecursorMz;
                    double? rt = dataSpectrum.RetentionTime;

                    if (!rt.HasValue)
                        continue;

                    if (dataSpectrum.Level == 1 && filter.EnabledMs)
                    {
                        // Process all SRM spectra that can be generated by filtering this full-scan MS1
                        foreach (var spectrum in filter.SrmSpectraFromMs1Scan(rt,
                            dataSpectrum.Mzs, dataSpectrum.Intensities))
                        {
                            ProcessSrmSpectrum(rt.Value,
                                spectrum.PrecursorMz, spectrum.Mzs, spectrum.Intensities, chromMapMs1);
                        }
                    }
                    else if (dataSpectrum.Level == 2 && filter.EnabledMsMs)
                    {
                        // Process all SRM spectra that can be generated by filtering this full-scan MS/MS
                        foreach (var spectrum in filter.SrmSpectraFromFullScan(rt,
                            precursorMz, dataSpectrum.Mzs, dataSpectrum.Intensities))
                        {
                            ProcessSrmSpectrum(rt.Value,
                                spectrum.PrecursorMz, spectrum.Mzs, spectrum.Intensities, chromMap);
                        }
                    }
                }
            }

            if (chromMap.Count == 0 && chromMapMs1.Count == 0)
                throw new NoSrmDataException();

            AddChromatograms(chromMap);
            AddChromatograms(chromMapMs1);

            // Only mzXML from mzWiff requires the introduction of zero values
            // during interpolation.
            _isProcessedScans = dataFile.IsMzWiffXml;
        }

        private void AddChromatograms(Dictionary<double, ChromDataCollector> chromMap)
        {
            foreach (var collector in chromMap.Values)
            {
                foreach (var pair in collector.ProductIntensityMap)
                {
                    var key = new ChromKey(collector.PrecursorMz, pair.Key);
                    _chromatograms.Add(new KeyValuePair<ChromKey, ChromCollector>(key, pair.Value));
                }
            }
        }

        private static void ProcessSrmSpectrum(double time, double precursorMz, double[] mzArray, double[] intensityArray,
            Dictionary<double, ChromDataCollector> chromMap)
        {
            ChromDataCollector collector;
            if (!chromMap.TryGetValue(precursorMz, out collector))
            {
                collector = new ChromDataCollector(precursorMz);
                chromMap.Add(precursorMz, collector);
            }

            int ionCount = collector.ProductIntensityMap.Count;
            int ionScanCount = mzArray.Length;
            if (ionCount == 0)
                ionCount = ionScanCount;

            int lenTimesCurrent = collector.TimeCount;
            for (int j = 0; j < ionScanCount; j++)
            {
                double productMz = mzArray[j];
                double intensity = intensityArray[j];

                ChromCollector tis;
                if (!collector.ProductIntensityMap.TryGetValue(productMz, out tis))
                {
                    tis = new ChromCollector();
                    // If more than a single ion scan, add any zeros necessary
                    // to make this new chromatogram have an entry for each time.
                    if (ionScanCount > 1)
                    {
                        for (int k = 0; k < lenTimesCurrent; k++)
                            tis.Intensities.Add(0);
                    }
                    collector.ProductIntensityMap.Add(productMz, tis);
                }
                int lenTimes = tis.Times.Count;
                if (lenTimes == 0 || time >= tis.Times[lenTimes - 1])
                {
                    tis.Times.Add((float)time);
                    tis.Intensities.Add((float)intensity);
                }
                else
                {
                    // Insert out of order time in the correct location
                    int iGreater = tis.Times.BinarySearch((float)time);
                    if (iGreater < 0)
                        iGreater = ~iGreater;
                    tis.Times.Insert(iGreater, (float)time);
                    tis.Intensities.Insert(iGreater, (float)intensity);
                }
            }

            // If this was a multiple ion scan and not all ions had measurements,
            // make sure missing ions have zero intensities in the chromatogram.
            if (ionScanCount > 1 &&
                (ionCount != ionScanCount || ionCount != collector.ProductIntensityMap.Count))
            {
                // Times should have gotten one longer
                lenTimesCurrent++;
                foreach (var tis in collector.ProductIntensityMap.Values)
                {
                    if (tis.Intensities.Count < lenTimesCurrent)
                    {
                        tis.Intensities.Add(0);
                        tis.Times.Add((float)time);
                    }
                }
            }
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get
            {
                for (int i = 0; i < _chromatograms.Count; i++)
                    yield return new KeyValuePair<ChromKey, int>(_chromatograms[i].Key, i);
            }
        }

        public override void GetChromatogram(int id, out float[] times, out float[] intensities)
        {
            var tis = _chromatograms[id].Value;
            times = tis.Times.ToArray();
            intensities = tis.Intensities.ToArray();
        }

        public override bool IsProcessedScans
        {
            get { return _isProcessedScans; }
        }

        public static bool HasSpectrumData(MsDataFileImpl dataFile)
        {
            return dataFile.SpectrumCount > 0;
        }
    }

    internal sealed class ChromDataCollector
    {
        public ChromDataCollector(double precursorMz)
        {
            PrecursorMz = precursorMz;
            ProductIntensityMap = new Dictionary<double, ChromCollector>();
        }

        public double PrecursorMz { get; private set; }
        public Dictionary<double, ChromCollector> ProductIntensityMap { get; private set; }

        public int TimeCount
        {
            get
            {
                // Return the length of any existing time list
                foreach (var tis in ProductIntensityMap.Values)
                    return tis.Times.Count;
                return 0;
            }
        }
    }

    internal sealed class ChromCollector
    {
        public ChromCollector()
        {
            Times = new List<float>();
            Intensities = new List<float>();
        }

        public List<float> Times { get; private set; }
        public List<float> Intensities { get; private set; }
    }

    internal sealed class SpectrumFilter
    {
//        private const double MILLION = 1000000;

        private readonly TransitionFullScan _fullScan;
        private readonly FullScanPrecursorFilterType _precursorFilterType;
        private readonly double _precursorFilterWindow;
        private readonly bool _isHighAccMsFilter;
        private readonly bool _isHighAccProductFilter;
        private readonly List<SpectrumFilterPair> _filterMzValues;

        public SpectrumFilter(SrmDocument document)
        {
            _fullScan = document.Settings.TransitionSettings.FullScan;
            _precursorFilterType = _fullScan.PrecursorFilterType;
            if (EnabledMs || EnabledMsMs)
            {
                if (EnabledMs)
                {
                    _isHighAccMsFilter = !Equals(_fullScan.PrecursorMassAnalyzer,
                                                 FullScanMassAnalyzerType.qit);
                }
                if (EnabledMsMs && _fullScan.PrecursorFilter.HasValue)
                {
                    _precursorFilterWindow = _fullScan.PrecursorFilter.Value;
                    _isHighAccProductFilter = !Equals(_fullScan.ProductMassAnalyzer,
                                                      FullScanMassAnalyzerType.qit);
                }

                _filterMzValues = new List<SpectrumFilterPair>();
                Func<double, double> calcWindowsQ1 = _fullScan.GetPrecursorFilterWindow;
                Func<double, double> calcWindowsQ3 = _fullScan.GetProductFilterWindow;
                foreach (var nodeGroup in document.TransitionGroups)
                {
                    if (nodeGroup.Children.Count == 0)
                        continue;

                    var filter = new SpectrumFilterPair(nodeGroup.PrecursorMz);
                    if (!EnabledMs)
                    {
                        filter.AddQ3FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                 select nodeTran.Mz, calcWindowsQ3);
                    }
                    else if (!EnabledMsMs)
                    {
                        filter.AddQ1FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                 where nodeTran.Mz == filter.Q1
                                                 select nodeTran.Mz, calcWindowsQ1);
                    }
                    else
                    {
                        filter.AddQ1FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                 where nodeTran.Mz == filter.Q1
                                                 select nodeTran.Mz, calcWindowsQ1);
                        filter.AddQ3FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                 where nodeTran.Mz != filter.Q1
                                                 select nodeTran.Mz, calcWindowsQ3);
                    }

                    _filterMzValues.Add(filter);
                }
                _filterMzValues.Sort();
            }
        }

        public bool EnabledMs { get { return _fullScan.PrecursorMassAnalyzer != FullScanMassAnalyzerType.none; } }
        public bool IsHighAccMsFilter { get { return _isHighAccMsFilter; } }
        public bool EnabledMsMs { get { return _precursorFilterType != FullScanPrecursorFilterType.None; } }
        public bool IsHighAccProductFilter { get { return _isHighAccProductFilter; } }

        public IEnumerable<FilteredSrmSpectrum> SrmSpectraFromFullScan(double? time, double? precursorMz,
            double[] mzArray, double[] intensityArray)
        {
            if (!EnabledMsMs || !time.HasValue || !precursorMz.HasValue || mzArray == null || intensityArray == null)
                yield break;

            foreach (var filterPair in FindFilterPairs(precursorMz.Value, _precursorFilterType))
            {
                var filteredSrmSpectrum = filterPair.FilterQ3Spectrum(mzArray, intensityArray);
                if (filteredSrmSpectrum != null)
                    yield return filteredSrmSpectrum;
            }
        }

        public IEnumerable<FilteredSrmSpectrum> SrmSpectraFromMs1Scan(double? time,
            double[] mzArray, double[] intensityArray)
        {
            if (!EnabledMs || !time.HasValue || mzArray == null || intensityArray == null)
                yield break;

            // All filter pairs have a shot at filtering the MS1 scans
            foreach (var filterPair in _filterMzValues)
            {
                var filteredSrmSpectrum = filterPair.FilterQ1Spectrum(mzArray, intensityArray);
                if (filteredSrmSpectrum != null)
                    yield return filteredSrmSpectrum;
            }
        }

        private IEnumerable<SpectrumFilterPair> FindFilterPairs(double precursorMz,
            FullScanPrecursorFilterType precursorFilterType)
        {
            if (precursorFilterType == FullScanPrecursorFilterType.Multiple)
            {
                // For multiple case, find the first possible value, and iterate until
                // no longer matching or the end of the array is encountered
                int iFilter = IndexOfFilter(precursorMz);
                if (iFilter == -1)
                    yield break;

                while (iFilter < _filterMzValues.Count &&
                        CompareMz(precursorMz, _filterMzValues[iFilter].Q1, _precursorFilterWindow) == 0)
                    yield return _filterMzValues[iFilter++];
            }
            else
            {
                // For single case, review all possible matches for the one closest to the
                // desired precursor m/z value.
                SpectrumFilterPair filterPairBest = null;
                double minMzDelta = double.MaxValue;

                foreach (var filterPair in FindFilterPairs(precursorMz, FullScanPrecursorFilterType.Multiple))
                {
                    double mzDelta = Math.Abs(precursorMz - filterPair.Q1);
                    if (mzDelta < minMzDelta)
                    {
                        minMzDelta = mzDelta;
                        filterPairBest = filterPair;
                    }
                }

                if (filterPairBest != null)
                    yield return filterPairBest;
            }
        }

        private int IndexOfFilter(double precursorMz)
        {
            return IndexOfFilter(precursorMz, _precursorFilterWindow, 0, _filterMzValues.Count - 1);
        }

        private int IndexOfFilter(double precursorMz, double window, int left, int right)
        {
            // Binary search for the right precursorMz
            if (left > right)
                return -1;
            int mid = (left + right) / 2;
            int compare = CompareMz(precursorMz, _filterMzValues[mid].Q1, window);
            if (compare < 0)
                return IndexOfFilter(precursorMz, window, left, mid - 1);
            else if (compare > 0)
                return IndexOfFilter(precursorMz, window, mid + 1, right);
            else
            {
                // Scan backward until the first matching element is found.
                while (mid > 0 && CompareMz(precursorMz, _filterMzValues[mid - 1].Q1, window) == 0)
                    mid--;

                return mid;
            }
        }

        private static int CompareMz(double mz1, double mz2, double window)
        {
            double startMz = mz1 - window/2;
            if (startMz < mz2 && mz2 < startMz + window)
                return 0;
            return (mz1 > mz2 ? 1 : -1);
        }
    }

    internal sealed class SpectrumFilterPair : IComparable<SpectrumFilterPair>
    {
        public SpectrumFilterPair(double q1)
        {
            Q1 = q1;
        }

        public double Q1 { get; private set; }
        // Q1 values for when precursor ions are filtered from MS1
        public double[] ArrayQ1 { get; set; }
        public double[] ArrayQ1Window { get; set; }
        // Q3 values for product ions filtered in MS/MS
        public double[] ArrayQ3 { get; set; }
        public double[] ArrayQ3Window { get; set; }

        public void AddQ1FilterValues(IEnumerable<double> filterValues, Func<double, double> getFilterWindow)
        {
            double[] centerArray, windowArray;
            AddFilterValues(filterValues, getFilterWindow, out centerArray, out windowArray);
            ArrayQ1 = centerArray;
            ArrayQ1Window = windowArray;
        }

        public void AddQ3FilterValues(IEnumerable<double> filterValues, Func<double, double> getFilterWindow)
        {
            double[] centerArray, windowArray;
            AddFilterValues(filterValues, getFilterWindow, out centerArray, out windowArray);
            ArrayQ3 = centerArray;
            ArrayQ3Window = windowArray;
        }

        private static void AddFilterValues(IEnumerable<double> filterValues, Func<double, double> getFilterWindow,
            out double[] centerArray, out double[] windowArray)
        {
            var listQ3 = filterValues.ToList();
            listQ3.Sort();

            centerArray = listQ3.ToArray();
            windowArray = listQ3.ConvertAll(mz => getFilterWindow(mz)).ToArray();
        }

        public FilteredSrmSpectrum FilterQ1Spectrum(double[] mzArray, double[] intensityArray)
        {
            return FilterSpectrum(mzArray, intensityArray, ArrayQ1, ArrayQ1Window);
        }

        public FilteredSrmSpectrum FilterQ3Spectrum(double[] mzArray, double[] intensityArray)
        {
            return FilterSpectrum(mzArray, intensityArray, ArrayQ3, ArrayQ3Window);
        }

        private FilteredSrmSpectrum FilterSpectrum(double[] mzArray, double[] intensityArray,
            double[] centerArray, double[] windowArray)
        {
            if (centerArray.Length == 0)
                return null;

            double[] intensityArrayNew = new double[centerArray.Length];

            // Search for matching peaks for each Q3 filter
            int iPeak = 0;
            for (int i = 0; i < centerArray.Length; i++)
            {
                // Look for the first peak that is greater than the start of the filter
                double filterWindow = windowArray[i];
                double startFilter = centerArray[i] - filterWindow / 2;
                while (iPeak < mzArray.Length && mzArray[iPeak] < startFilter)
                    iPeak++;

                // Add the intensity values of all peaks less than the end of the filter
                int iNext = iPeak;
                double endFilter = startFilter + filterWindow;
                while (iNext < mzArray.Length && mzArray[iNext] < endFilter)
                    intensityArrayNew[i] += intensityArray[iNext++];
            }

            return new FilteredSrmSpectrum(Q1, centerArray, intensityArrayNew);            
        }

        public int CompareTo(SpectrumFilterPair other)
        {
            return Comparer.Default.Compare(Q1, other.Q1);
        }
    }

    internal sealed class FilteredSrmSpectrum
    {
        public FilteredSrmSpectrum(double precursorMz, double[] mzs, double[] intensities)
        {
            PrecursorMz = precursorMz;
            Mzs = mzs;
            Intensities = intensities;
        }

        public double PrecursorMz { get; private set; }
        public double[] Mzs { get; private set; }
        public double[] Intensities { get; private set; }
    }
}
