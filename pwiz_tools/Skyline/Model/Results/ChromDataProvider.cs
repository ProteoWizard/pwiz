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
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    internal abstract class ChromDataProvider : IDisposable
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

        public abstract bool IsSingleMzMatch { get; }

        public abstract void ReleaseMemory();

        public abstract void Dispose();
    }

    internal sealed class ChromatogramDataProvider : ChromDataProvider
    {
        private readonly IList<KeyValuePair<ChromKey, int>> _chromIds =
            new List<KeyValuePair<ChromKey, int>>();

        private MsDataFileImpl _dataFile;

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
                if (_dataFile.IsThermoFile)
                {
                    _readMaxMinutes = 4;
                    _slowLoadWorkAround = LoadingTooSlowlyException.Solution.local_file;
                }
                // WIFF file issues have been fixed with new WIFF reader library, and mzWiff.exe
                // has been removed from the installation.
//                else if (_dataFile.IsABFile)
//                {
//                    _readMaxMinutes = 4;
//                    _slowLoadWorkAround = LoadingTooSlowlyException.Solution.mzwiff_conversion;
//                }
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

        public override bool IsSingleMzMatch
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

        public override void ReleaseMemory()
        {
            Dispose();
        }

        public override void Dispose()
        {
            if (_dataFile != null)
                _dataFile.Dispose();
            _dataFile = null;
        }
    }

    internal sealed class SpectraChromDataProvider : ChromDataProvider
    {
        private List<KeyValuePair<ChromKey, ChromCollected>> _chromatograms =
            new List<KeyValuePair<ChromKey, ChromCollected>>();

        private readonly bool _isProcessedScans;
        private readonly bool _isSingleMzMatch;

        public SpectraChromDataProvider(MsDataFileImpl dataFile,
                                        SrmDocument document,
                                        ProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(status, startPercent, endPercent, loader)
        {
            using (dataFile)
            {
                // 10% done with this file
                const int loadPercent = 10;
                SetPercentComplete(loadPercent);

                // Only mzXML from mzWiff requires the introduction of zero values
                // during interpolation.
                _isProcessedScans = dataFile.IsMzWiffXml;

                // Create a spectrum filter data structure, in case it is needed
                // This could be done lazily, but does not seem worth it, given the file reading
                var filter = new SpectrumFilter(document);

                // First read all of the spectra, building chromatogram time, intensity lists
                bool isSrm = dataFile.HasSrmSpectra;
                var chromMap = new ChromDataCollectorSet(isSrm ? TimeSharing.single : TimeSharing.grouped);
                var chromMapMs1 = new ChromDataCollectorSet(filter.IsSharedTime ? TimeSharing.shared : TimeSharing.grouped);
                int lenSpectra = dataFile.SpectrumCount;
                int statusPercent = 0;
                for (int i = 0; i < lenSpectra; i++)
                {
                    // Update progress indicator
                    int currentPercent = i*80/lenSpectra;
                    if (currentPercent > statusPercent)
                    {
                        statusPercent = currentPercent;
                        SetPercentComplete(statusPercent + loadPercent);
                    }

                    if (chromMap.IsSingleTime)
                    {
                        var dataSpectrum = dataFile.GetSrmSpectrum(i);
                        if (dataSpectrum.Level != 2)
                            continue;

                        if (!dataSpectrum.RetentionTime.HasValue)
                            throw new InvalidDataException(String.Format("Scan {0} found without scan time.", dataFile.GetSpectrumId(i)));
                        if (dataSpectrum.Precursors.Length < 1 || !dataSpectrum.Precursors[0].PrecursorMz.HasValue)
                            throw new InvalidDataException(String.Format("Scan {0} found without precursor m/z.", dataFile.GetSpectrumId(i)));

                        // Process the one SRM spectrum
                        ProcessSrmSpectrum(dataSpectrum.RetentionTime.Value,
                                           dataSpectrum.Precursors[0].PrecursorMz.Value,
                                           dataSpectrum.Mzs,
                                           dataSpectrum.Intensities,
                                           chromMap,
                                           null);
                    }
                    else if (filter.EnabledMsMs || filter.EnabledMs)
                    {
                        // Full-scan filtering should always match a single precursor
                        // m/z value to a single precursor node in the document tree,
                        // because that is the way the filters are constructed in the
                        // first place.
                        _isSingleMzMatch = true;

                        // If MS/MS filtering is not enabled, skip anything that is not a MS1 scan
                        if (!filter.EnabledMsMs && dataFile.GetMsLevel(i) != 1)
                            continue;

                        var dataSpectrum = dataFile.GetSpectrum(i);
                        if (dataSpectrum.Mzs.Length == 0)
                            continue;

                        double? rt = dataSpectrum.RetentionTime;
                        if (!rt.HasValue)
                            continue;

                        if (dataSpectrum.Level == 1 && filter.EnabledMs)
                        {
                            // Process all SRM spectra that can be generated by filtering this full-scan MS1
                            int? insertIndex = null;
                            if (filter.IsSharedTime)
                            {
                                if (!filter.ContainsTime(rt.Value))
                                    continue;
                                insertIndex = AddTime((float)rt.Value, chromMapMs1.Times);
                            }
                            foreach (var spectrum in filter.SrmSpectraFromMs1Scan(rt,
                                dataSpectrum.Mzs, dataSpectrum.Intensities))
                            {
                                ProcessSrmSpectrum(rt.Value, spectrum.PrecursorMz,
                                    spectrum.Mzs, spectrum.Intensities, chromMapMs1, insertIndex);
                            }
                        }
                        else if (dataSpectrum.Level == 2 && filter.EnabledMsMs)
                        {
                            // Process all SRM spectra that can be generated by filtering this full-scan MS/MS
                            foreach (var spectrum in filter.SrmSpectraFromFullScan(rt,
                                    dataSpectrum.Precursors,
                                    dataSpectrum.Mzs,
                                    dataSpectrum.Intensities))
                            {
                                ProcessSrmSpectrum(rt.Value, spectrum.PrecursorMz,
                                    spectrum.Mzs, spectrum.Intensities, chromMap, null);
                            }
                        }
                    }
                }

                if (chromMap.Count == 0 && chromMapMs1.Count == 0)
                    throw new NoSrmDataException();

                AddChromatograms(chromMap);
                AddChromatograms(chromMapMs1);
            }
        }

        private void AddChromatograms(ChromDataCollectorSet chromMap)
        {
            float[] times = chromMap.Times != null ? chromMap.Times.ToArray() : null;
            foreach (var collector in chromMap.PrecursorCollectorMap.Values)
            {
                var collectorTimes = times;
                if (collectorTimes == null && collector.Times != null)
                    collectorTimes = collector.Times.ToArray();

                foreach (var pair in collector.ProductIntensityMap)
                {
                    var key = new ChromKey(collector.PrecursorMz, pair.Key);
                    var collected = pair.Value.ReleaseChromatogram(collectorTimes);
                    _chromatograms.Add(new KeyValuePair<ChromKey, ChromCollected>(key, collected));
                }
            }
        }

        private static void ProcessSrmSpectrum(double time,
            double precursorMz,
            double[] mzArray,
            double[] intensityArray,
            ChromDataCollectorSet chromMap,
            int? insertIndex)
        {
            ChromDataCollector collector;
            if (!chromMap.PrecursorCollectorMap.TryGetValue(precursorMz, out collector))
            {
                collector = new ChromDataCollector(precursorMz, chromMap);
                chromMap.PrecursorCollectorMap.Add(precursorMz, collector);
            }

            int ionCount = collector.ProductIntensityMap.Count;
            int ionScanCount = mzArray.Length;
            if (ionCount == 0)
                ionCount = ionScanCount;

            // Add new time to the shared time list if not SRM, which doesn't share times, or
            // the times are shared with the entire set, as in MS1
            if (chromMap.IsGroupedTime)
                insertIndex = AddTime((float) time, collector.Times);

            // Add intenisity values to ion scans
            int lenTimes = collector.TimeCount;
            for (int j = 0; j < ionScanCount; j++)
            {
                double productMz = mzArray[j];
                double intensity = intensityArray[j];

                ChromCollector tis;
                if (!collector.ProductIntensityMap.TryGetValue(productMz, out tis))
                {
                    tis = new ChromCollector(collector.Times);
                    // If more than a single ion scan, add any zeros necessary
                    // to make this new chromatogram have an entry for each time.
                    if (ionScanCount > 1)
                    {
                        for (int k = 0; k < lenTimes - 1; k++)
                            tis.Intensities.Add(0);
                    }
                    collector.ProductIntensityMap.Add(productMz, tis);
                }
                if (chromMap.IsSingleTime)
                    insertIndex = AddTime((float) time, tis.Times);
                if (insertIndex.HasValue)
                    tis.Intensities.Insert(insertIndex.Value, (float)intensity);
                else
                    tis.Intensities.Add((float)intensity);
            }

            // If this was a multiple ion scan and not all ions had measurements,
            // make sure missing ions have zero intensities in the chromatogram.
            if (ionScanCount > 1 &&
                (ionCount != ionScanCount || ionCount != collector.ProductIntensityMap.Count))
            {
                // Times should have gotten one longer
                foreach (var tis in collector.ProductIntensityMap.Values)
                {
                    if (tis.Intensities.Count < lenTimes)
                    {
                        if (insertIndex.HasValue)
                            tis.Intensities.Insert(insertIndex.Value, 0);
                        else
                            tis.Intensities.Add(0);                        
                    }
                }
            }
        }

        private static int? AddTime(float time, List<float> times)
        {
            int lenTimes = times.Count;
            int? insertIndex = null;
            if (lenTimes == 0 || time >= times[lenTimes - 1])
            {
                times.Add(time);
            }
            else
            {
                // Insert out of order time in the correct location
                insertIndex = times.BinarySearch(time);
                if (insertIndex < 0)
                    insertIndex = ~insertIndex;
                times.Insert(insertIndex.Value, time);
            }
            return insertIndex;
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
            times = tis.Times;
            intensities = tis.Intensities;
        }

        public override bool IsProcessedScans
        {
            get { return _isProcessedScans; }
        }

        public override bool IsSingleMzMatch
        {
            get { return _isSingleMzMatch; }
        }

        public override void ReleaseMemory()
        {
            Dispose();
        }

        public override void Dispose()
        {
            _chromatograms = null;
        }

        public static bool HasSpectrumData(MsDataFileImpl dataFile)
        {
            return dataFile.SpectrumCount > 0;
        }
   }

    internal enum TimeSharing { single, shared, grouped }

    internal sealed class ChromDataCollectorSet
    {
        public ChromDataCollectorSet(TimeSharing timeSharing)
        {
            TypeOfScans = timeSharing;
            PrecursorCollectorMap = new Dictionary<double, ChromDataCollector>();
            if (timeSharing == TimeSharing.shared)
                Times = new List<float>();
        }

        public TimeSharing TypeOfScans { get; private set; }

        public bool IsSingleTime { get { return TypeOfScans == TimeSharing.single; } }
        public bool IsGroupedTime { get { return TypeOfScans == TimeSharing.grouped; } }

        public List<float> Times { get; private set; }
        
        public Dictionary<double, ChromDataCollector> PrecursorCollectorMap { get; private set; }

        public int Count { get { return PrecursorCollectorMap.Count; } }

        public float[] ReleaseTimes()
        {
            var times = Times;
            Times = null;
            return times != null ? times.ToArray() : null;
        }
    }

    internal sealed class ChromDataCollector
    {
        public ChromDataCollector(double precursorMz, ChromDataCollectorSet chromMap)
        {
            PrecursorMz = precursorMz;
            ProductIntensityMap = new Dictionary<double, ChromCollector>();
            if (!chromMap.IsSingleTime)
                Times = chromMap.Times ?? new List<float>();
        }

        public double PrecursorMz { get; private set; }
        public List<float> Times { get; private set; }
        public Dictionary<double, ChromCollector> ProductIntensityMap { get; private set; }

        public int TimeCount
        {
            get
            {
                // Return the length of any existing time list (in case there are no shared times)
                foreach (var tis in ProductIntensityMap.Values)
                    return tis.Times.Count;
                return 0;
            }
        }

        public float[] ReleaseTimes()
        {
            var times = Times;
            Times = null;
            return times != null ? times.ToArray() : null;
        }
    }

    internal sealed class ChromCollector
    {
        public ChromCollector(List<float> times)
        {
            Times = times ?? new List<float>();
            Intensities = new List<float>();
        }

        public List<float> Times { get; private set; }
        public List<float> Intensities { get; private set; }

        public ChromCollected ReleaseChromatogram(float[] times)
        {
            var result = new ChromCollected(times ?? Times.ToArray(), Intensities.ToArray());

            // Release the memory for the times and intensities
            Times = null;
            Intensities = null;

            return result;
        }
    }

    internal sealed class ChromCollected
    {
        public ChromCollected(float[] times, float[] intensities)
        {
            if (times.Length != intensities.Length)
            {
                throw new InvalidDataException(string.Format("Times ({0}) and intensities ({1}) disagree in point count.",
                    times.Length, intensities.Length));
            }
            Times = times;
            Intensities = intensities;
        }

        public float[] Times { get; private set; }
        public float[] Intensities { get; private set; }
    }

    internal sealed class SpectrumFilter
    {
//        private const double MILLION = 1000000;

        private readonly TransitionFullScan _fullScan;
        private readonly TransitionInstrument _instrument;
        private readonly FullScanPrecursorFilterType _precursorFilterType;
        private readonly double _precursorFilterWindow;
        private readonly double? _precursorRightFilterWindow;
        private readonly bool _isHighAccMsFilter;
        private readonly bool _isHighAccProductFilter;
        private readonly bool _isSharedTime;
        private readonly double? _minTime;
        private readonly double? _maxTime;
        private readonly SpectrumFilterPair[] _filterMzValues;

        public SpectrumFilter(SrmDocument document)
        {
            _fullScan = document.Settings.TransitionSettings.FullScan;
            _instrument = document.Settings.TransitionSettings.Instrument;
            _precursorFilterType = _fullScan.PrecursorFilterType;
            if (EnabledMs || EnabledMsMs)
            {
                if (EnabledMs)
                {
                    _isHighAccMsFilter = !Equals(_fullScan.PrecursorMassAnalyzer,
                                                 FullScanMassAnalyzerType.qit);
                }
                if (EnabledMsMs)
                {
                    _precursorFilterWindow = _fullScan.PrecursorFilter ?? _instrument.MzMatchTolerance*2;
                    _precursorRightFilterWindow = _fullScan.PrecursorRightFilter;
                    _isHighAccProductFilter = !Equals(_fullScan.ProductMassAnalyzer,
                                                      FullScanMassAnalyzerType.qit);
                }

                var dictPrecursorMzToFilter = new SortedDictionary<double, SpectrumFilterPair>();

                Func<double, double> calcWindowsQ1 = _fullScan.GetPrecursorFilterWindow;
                Func<double, double> calcWindowsQ3 = _fullScan.GetProductFilterWindow;
                _minTime = _instrument.MinTime;
                _maxTime = _instrument.MaxTime;
                bool canSchedule = _fullScan.IsScheduledFilter &&
                    document.Settings.PeptideSettings.Prediction.CanSchedule(document, false);
                _isSharedTime = !canSchedule;
                int? replicateNum = null;
                if (document.Settings.HasResults)
                    replicateNum = document.Settings.MeasuredResults.Chromatograms.Count - 1;
                var schedulingAlgorithm = replicateNum.HasValue
                                              ? ExportSchedulingAlgorithm.Single
                                              : ExportSchedulingAlgorithm.Average;
                foreach (var nodePep in document.Peptides)
                {
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        if (nodeGroup.Children.Count == 0)
                            continue;

                        double? minTime = _minTime, maxTime = _maxTime;
                        if (canSchedule)
                        {
                            double windowRT;
                            double? centerTime = document.Settings.PeptideSettings.Prediction.PredictRetentionTime(
                                document, nodePep, nodeGroup, replicateNum, schedulingAlgorithm, false, out windowRT);
                            if (centerTime != null)
                            {
                                double startTime = centerTime.Value - windowRT/2;
                                double endTime = startTime + windowRT;
                                minTime = Math.Max(minTime ?? 0, startTime);
                                maxTime = Math.Min(maxTime ?? double.MaxValue, endTime);
                            }
                        }

                        SpectrumFilterPair filter;
                        if (!dictPrecursorMzToFilter.TryGetValue(nodeGroup.PrecursorMz, out filter))
                        {
                            filter = new SpectrumFilterPair(nodeGroup.PrecursorMz, minTime, maxTime);
                            dictPrecursorMzToFilter.Add(nodeGroup.PrecursorMz, filter);
                        }

                        if (!EnabledMs)
                        {
                            filter.AddQ3FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                     select nodeTran.Mz, calcWindowsQ3);
                        }
                        else if (!EnabledMsMs)
                        {
                            filter.AddQ1FilterValues(GetMS1MzValues(nodeGroup), calcWindowsQ1);
                        }
                        else
                        {
                            filter.AddQ1FilterValues(GetMS1MzValues(nodeGroup), calcWindowsQ1);
                            filter.AddQ3FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                     where !IsMS1Precursor(nodeTran)
                                                     select nodeTran.Mz, calcWindowsQ3);
                        }
                    }
                }
                _filterMzValues = dictPrecursorMzToFilter.Values.ToArray();
            }
        }

        public int Count
        {
            get
            {
                return _filterMzValues != null
                           ? _filterMzValues.SelectMany(pair => pair.ArrayQ3 ?? pair.ArrayQ1).Count()
                           : 0;
            }
        }

        private IEnumerable<double> GetMS1MzValues(TransitionGroupDocNode nodeGroup)
        {
            var isotopePeaks = nodeGroup.IsotopeDist;
            if (isotopePeaks == null)
            {
                // Return the MS1 transition m/z values, if the precursor has no isotope peaks
                foreach (var nodeTran in nodeGroup.Children.Cast<TransitionDocNode>().Where(IsMS1Precursor))
                    yield return nodeTran.Mz;
            }
            else
            {
                // Otherwise, return all possible isotope peaks
                for (int i = 0; i < isotopePeaks.CountPeaks; i++)
                    yield return isotopePeaks.GetMZI(isotopePeaks.PeakIndexToMassIndex(i));
            }
        }

        public bool IsMS1Precursor(TransitionDocNode nodeTran)
        {
            return Transition.IsPrecursor(nodeTran.Transition.IonType) && !nodeTran.HasLoss;
        }

        public bool EnabledMs { get { return _fullScan.PrecursorIsotopes != FullScanPrecursorIsotopes.None; } }
        public bool IsHighAccMsFilter { get { return _isHighAccMsFilter; } }
        public bool EnabledMsMs { get { return _precursorFilterType != FullScanPrecursorFilterType.None; } }
        public bool IsHighAccProductFilter { get { return _isHighAccProductFilter; } }
        public bool IsSharedTime { get { return _isSharedTime; } }

        public bool ContainsTime(double time)
        {
            return (!_minTime.HasValue || _minTime.Value <= time) &&
                   (!_maxTime.HasValue || _maxTime.Value >= time);
        }

        public IEnumerable<FilteredSrmSpectrum> SrmSpectraFromFullScan(double? time,
            IEnumerable<MsPrecursor> precursors, double[] mzArray, double[] intensityArray)
        {
            if (!EnabledMsMs || !time.HasValue || mzArray == null || intensityArray == null)
                yield break;

            foreach (var precursor in precursors)
            {
                double? isolationMz = precursor.IsolationMz;
                if (!isolationMz.HasValue)
                    continue;

                foreach (var filterPair in FindFilterPairs(isolationMz.Value, precursor.IsolationWidth, _precursorFilterType))
                {
                    if (!filterPair.ContainsTime(time.Value))
                        continue;
                    var filteredSrmSpectrum = filterPair.FilterQ3Spectrum(mzArray, intensityArray);
                    if (filteredSrmSpectrum != null)
                        yield return filteredSrmSpectrum;
                }
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
                if (!filterPair.ContainsTime(time.Value))
                    continue;
                var filteredSrmSpectrum = filterPair.FilterQ1Spectrum(mzArray, intensityArray);
                if (filteredSrmSpectrum != null)
                    yield return filteredSrmSpectrum;
            }
        }

        private IEnumerable<SpectrumFilterPair> FindFilterPairs(double isolationTargetMz, double? isolationWidth,
            FullScanPrecursorFilterType precursorFilterType)
        {
            if (precursorFilterType == FullScanPrecursorFilterType.Multiple)
            {
                // Use the user specified isolation width, unless it is larger than
                // the acquisition isolation width.  In this case the chromatograms
                // may be very confusing (spikey), because of incorrectly included
                // data points.
                double isolationWidthValue = _precursorFilterWindow + (_precursorRightFilterWindow ?? 0);
                if (isolationWidth.HasValue && isolationWidth.Value < _precursorFilterWindow)
                    isolationWidthValue = isolationWidth.Value;

                // Make sure the isolation target is centered in the desired window, even
                // if the window was specified as being asymetric
                if (_precursorRightFilterWindow.HasValue)
                    isolationTargetMz += _precursorRightFilterWindow.Value - isolationWidthValue / 2;

                // For multiple case, find the first possible value, and iterate until
                // no longer matching or the end of the array is encountered
                int iFilter = IndexOfFilter(isolationTargetMz, isolationWidthValue);
                if (iFilter == -1)
                    yield break;

                while (iFilter < _filterMzValues.Length && CompareMz(isolationTargetMz,
                        _filterMzValues[iFilter].Q1, isolationWidthValue) == 0)
                    yield return _filterMzValues[iFilter++];
            }
            else
            {
                // For single case, review all possible matches for the one closest to the
                // desired precursor m/z value.
                SpectrumFilterPair filterPairBest = null;
                double minMzDelta = double.MaxValue;

                foreach (var filterPair in FindFilterPairs(isolationTargetMz, isolationWidth,
                                                           FullScanPrecursorFilterType.Multiple))
                {
                    double mzDelta = Math.Abs(isolationTargetMz - filterPair.Q1);
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

        private int IndexOfFilter(double precursorMz, double window)
        {
            return IndexOfFilter(precursorMz, window, 0, _filterMzValues.Length - 1);
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
            if (compare > 0)
                return IndexOfFilter(precursorMz, window, mid + 1, right);
            
            // Scan backward until the first matching element is found.
            while (mid > 0 && CompareMz(precursorMz, _filterMzValues[mid - 1].Q1, window) == 0)
                mid--;

            return mid;
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
        public SpectrumFilterPair(double q1, double? minTime, double? maxTime)
        {
            Q1 = q1;
            MinTime = minTime;
            MaxTime = maxTime;
        }

        public double Q1 { get; private set; }
        public double? MinTime { get; private set; }
        public double? MaxTime { get; private set; }
        // Q1 values for when precursor ions are filtered from MS1
        public double[] ArrayQ1 { get; set; }
        public double[] ArrayQ1Window { get; set; }
        // Q3 values for product ions filtered in MS/MS
        public double[] ArrayQ3 { get; set; }
        public double[] ArrayQ3Window { get; set; }

        public void AddQ1FilterValues(IEnumerable<double> filterValues, Func<double, double> getFilterWindow)
        {
            AddFilterValues(MergeFilters(ArrayQ1, filterValues).Distinct(), getFilterWindow,
                centers => ArrayQ1 = centers, windows => ArrayQ1Window = windows);
        }

        public void AddQ3FilterValues(IEnumerable<double> filterValues, Func<double, double> getFilterWindow)
        {
            AddFilterValues(MergeFilters(ArrayQ3, filterValues).Distinct(), getFilterWindow,
                centers => ArrayQ3 = centers, windows => ArrayQ3Window = windows);
        }

        private static IEnumerable<double> MergeFilters(IEnumerable<double> existing, IEnumerable<double> added)
        {
            if (existing == null)
                return added;
            return existing.Union(added);
        }

        private static void AddFilterValues(IEnumerable<double> filterValues,
            Func<double, double> getFilterWindow,
            Action<double[]> setCenters, Action<double[]> setWindows)
        {
            var listQ3 = filterValues.ToList();

            listQ3.Sort();

            setCenters(listQ3.ToArray());
            setWindows(listQ3.ConvertAll(mz => getFilterWindow(mz)).ToArray());
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

        public bool ContainsTime(double time)
        {
            return (!MinTime.HasValue || MinTime.Value <= time) &&
                   (!MaxTime.HasValue || MaxTime.Value >= time);
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
