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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results
{
    internal abstract class ChromDataProvider : IDisposable
    {
        private readonly int _startPercent;
        private readonly int _endPercent;
        protected readonly IProgressMonitor _loader;

        protected ChromDataProvider(ChromFileInfo fileInfo,
                                    ProgressStatus status,
                                    int startPercent,
                                    int endPercent,
                                    IProgressMonitor loader)
        {
            FileInfo = fileInfo;
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

        public ChromFileInfo FileInfo { get; private set; }

        public ProgressStatus Status { get; private set; }

        public ChromatogramLoadingStatus LoadingStatus { get { return (ChromatogramLoadingStatus) Status; } }

        public abstract IEnumerable<KeyValuePair<ChromKey, int>> ChromIds { get; }

        public abstract void GetChromatogram(int id, out ChromExtra extra,
            out float[] times, out float[] intensities, out float[] massErrors);

        public abstract float? MaxRetentionTime { get; }

        public abstract float? MaxIntensity { get; }

        public abstract bool IsProcessedScans { get; }

        public abstract bool IsSingleMzMatch { get; }

        public abstract void ReleaseMemory();

        public abstract void Dispose();
    }

    internal sealed class ChromatogramDataProvider : ChromDataProvider
    {
        private readonly IList<KeyValuePair<ChromKey, int>> _chromIds =
            new List<KeyValuePair<ChromKey, int>>();
        private readonly int[] _chromIndices;

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
                                        ChromFileInfo fileInfo,
                                        bool throwIfSlow,
                                        ProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(fileInfo, status, startPercent, endPercent, loader)
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
            _chromIndices = new int[len];

            int indexPrecursor = -1;
            double lastPrecursor = 0;
            for (int i = 0; i < len; i++)
            {
                int index;
                string id = dataFile.GetChromatogramId(i, out index);

                if (!ChromKey.IsKeyId(id))
                    continue;

                var chromKey = ChromKey.FromId(id);
                if (chromKey.Precursor != lastPrecursor)
                {
                    lastPrecursor = chromKey.Precursor;
                    indexPrecursor++;
                }
                var ki = new KeyValuePair<ChromKey, int>(chromKey, index);
                _chromIndices[index] = indexPrecursor;
                _chromIds.Add(ki);
            }

            if (_chromIds.Count == 0)
                throw new NoSrmDataException(dataFile.FilePath);

            SetPercentComplete(50);
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get { return _chromIds; }
        }

        public override void GetChromatogram(int id, out ChromExtra extra, out float[] times, out float[] intensities, out float[] massErrors)
        {
            // No mass errors in SRM
            massErrors = null;
            if (_readChromatograms == 0)
            {
                _readStartTime = DateTime.Now;
            }

            string chromId;
            _dataFile.GetChromatogram(id, out chromId, out times, out intensities);

            // Assume that each chromatogram will be read once, though this may
            // not always be completely true.
            _readChromatograms++;

            if (!System.Diagnostics.Debugger.IsAttached)
            {
                double predictedMinutes = ExpectedReadDurationMinutes;
                if (_readMaxMinutes > 0 && predictedMinutes > _readMaxMinutes)
                {
                    throw new LoadingTooSlowlyException(_slowLoadWorkAround, Status, predictedMinutes, _readMaxMinutes);
                }
            }

            if (_readChromatograms < _chromIds.Count)
                SetPercentComplete(50 + _readChromatograms * 50 / _chromIds.Count);

            int index = _chromIndices[id];
            extra = new ChromExtra(index, -1);  // TODO: is zero the right value?

            // Display in AllChromatogramsGraph
            LoadingStatus.Transitions.AddTransition(
                index, -1,
                times,
                intensities);
        }

        private double ExpectedReadDurationMinutes
        {
            get { return DateTime.Now.Subtract(_readStartTime).TotalMinutes * _chromIds.Count / _readChromatograms; }
        }

        public override float? MaxIntensity
        {
            get { return null; }
        }

        public override float? MaxRetentionTime
        {
            get { return null; }
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
        private struct ChromKeyAndCollector
        {
            public ChromKey Key;
            public int StatusId;
            public ChromCollector Collector;
        }
        private List<ChromKeyAndCollector> _chromatograms =
            new List<ChromKeyAndCollector>();

        private readonly bool _isProcessedScans;
        private readonly bool _isSingleMzMatch;
        private readonly ChromCollector.Allocator _allocator;

        /// <summary>
        /// The number of chromatograms read so far.
        /// </summary>
        private int _readChromatograms;

        private const int LOAD_PERCENT = 10;
        private const int BUILD_PERCENT = 60;
        private const int READ_PERCENT = 96 - LOAD_PERCENT - BUILD_PERCENT; // Leave 4% empty until the very end

        public SpectraChromDataProvider(MsDataFileImpl dataFile,
                                        ChromFileInfo fileInfo,
                                        SrmDocument document,
                                        ProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(fileInfo, status, startPercent, endPercent, loader)
        {
            // Create allocator used by all ChromCollectors to store transition times and intensities.
            _allocator = new ChromCollector.Allocator(dataFile.FilePath);

            using (dataFile)
            {
                SetPercentComplete(LOAD_PERCENT);

                // If no SRM spectra, then full-scan filtering must be enabled
                bool isSrm = dataFile.HasSrmSpectra;
                if (!isSrm && !document.Settings.TransitionSettings.FullScan.IsEnabled)
                    throw new NoFullScanFilteringException(dataFile.FilePath);

                // Only mzXML from mzWiff requires the introduction of zero values
                // during interpolation.
                _isProcessedScans = dataFile.IsMzWiffXml;

                // Create a spectrum filter data structure, in case it is needed
                // This could be done lazily, but does not seem worth it, given the file reading
                var filter = new SpectrumFilter(document, dataFile);

                // Get data object used to graph all of the chromatograms.
                ChromatogramLoadingStatus.TransitionData allChromData = null;
                if (loader.HasUI)
                {
                    allChromData = LoadingStatus.Transitions;
                    allChromData.FilterCount = filter.FilterPairs != null ? filter.FilterPairs.Count() : 0;
                }

                // First read all of the spectra, building chromatogram time, intensity lists
                var chromMap = new ChromDataCollectorSet(ChromSource.fragment, isSrm ? TimeSharing.single : TimeSharing.grouped, allChromData);
                var chromMapMs1 = new ChromDataCollectorSet(ChromSource.ms1, filter.IsSharedTime ? TimeSharing.shared : TimeSharing.grouped, allChromData);
                var chromMapSim = new ChromDataCollectorSet(ChromSource.sim, TimeSharing.grouped, allChromData);
                int lenSpectra = dataFile.SpectrumCount;
                int statusPercent = 0;

                var dictPrecursorMzToIndex = new Dictionary<double, int>(); // For SRM processing

                var demultiplexer = dataFile.IsMsx ? new MsxDemultiplexer(dataFile, filter) : null;

                // If possible, find the maximum retention time in order to scale the chromatogram graph.
                LoadingStatus.Transitions.MaxRetentionTimeKnown = false;
                if (filter.EnabledMsMs || filter.EnabledMs)
                {
                    var dataSpectrum = dataFile.GetSpectrum(lenSpectra - 1);
                    if (dataSpectrum.RetentionTime.HasValue && allChromData != null)
                    {
                        allChromData.MaxRetentionTime = (float) dataSpectrum.RetentionTime.Value;
                        allChromData.Progressive = true;
                        allChromData.MaxRetentionTimeKnown = true;
                    }
                }

                for (int i = 0; i < lenSpectra; i++)
                {
                    // Update progress indicator
                    int currentPercent = i*BUILD_PERCENT/lenSpectra;
                    if (currentPercent > statusPercent)
                    {
                        statusPercent = currentPercent;
                        SetPercentComplete(LOAD_PERCENT + statusPercent);
                    }

                    if (chromMap.IsSingleTime)
                    {
                        var dataSpectrum = dataFile.GetSrmSpectrum(i);
                        if (dataSpectrum.Level != 2)
                            continue;

                        if (!dataSpectrum.RetentionTime.HasValue)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SpectraChromDataProvider_SpectraChromDataProvider_Scan__0__found_without_scan_time,
                                              dataFile.GetSpectrumId(i)));
                        }
                        if (dataSpectrum.Precursors.Length < 1 || !dataSpectrum.Precursors[0].PrecursorMz.HasValue)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SpectraChromDataProvider_SpectraChromDataProvider_Scan__0__found_without_precursor_mz,
                                              dataFile.GetSpectrumId(i)));
                        }

                        double precursorMz = dataSpectrum.Precursors[0].PrecursorMz.Value;
                        int filterIndex;
                        if (!dictPrecursorMzToIndex.TryGetValue(precursorMz, out filterIndex))
                        {
                            filterIndex = dictPrecursorMzToIndex.Count;
                            dictPrecursorMzToIndex.Add(precursorMz, filterIndex);
                        }

                        // Process the one SRM spectrum
                        ProcessSrmSpectrum(
                            dataSpectrum.RetentionTime.Value,
                            null,  // Peptide unknown
                            precursorMz,
                            filterIndex,
                            dataSpectrum.Mzs,
                            dataSpectrum.Intensities,
                            chromMap);
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
                        if (allChromData != null)
                            allChromData.CurrentTime = (float)rt.Value;

                        if (filter.IsMsSpectrum(dataSpectrum))
                        {
                            var chromMapMs = filter.IsSimSpectrum(dataSpectrum) ? chromMapSim : chromMapMs1;

                            // Process all SRM spectra that can be generated by filtering this full-scan MS1
                            if (chromMapMs.IsSharedTime)
                            {
                                if (!filter.ContainsTime(rt.Value))
                                    continue;
                                chromMapMs.AddSharedTime((float)rt.Value);
                            }
                            foreach (var spectrum in filter.SrmSpectraFromMs1Scan(rt, dataSpectrum.Precursors,
                                dataSpectrum.Mzs, dataSpectrum.Intensities))
                            {
                                chromMapMs.ProcessExtractedSpectrum(rt.Value, spectrum);
                            }
                        }
                        if (filter.IsMsMsSpectrum(dataSpectrum))
                        {
                            // Process all SRM spectra that can be generated by filtering this full-scan MS/MS
                            if (demultiplexer == null)
                            {
                                ProcessSpectrum(dataSpectrum, chromMap, rt.Value, filter);
                            }
                            else
                            {
                                foreach (var deconvSpectrum in demultiplexer.GetDeconvolvedSpectra(i,dataSpectrum))
                                {
                                    ProcessSpectrum(deconvSpectrum, chromMap, rt.Value, filter);
                                }
                            }
                        }
                    }
                }

                if (chromMap.Count == 0 && chromMapMs1.Count == 0 && chromMapSim.Count == 0)
                    throw new NoFullScanDataException(dataFile.FilePath);

                AddChromatograms(chromMap, ChromSource.fragment);
                AddChromatograms(chromMapSim, ChromSource.sim);
                AddChromatograms(chromMapMs1, ChromSource.ms1);
            }
        }

        private void AddChromatograms(ChromDataCollectorSet chromMap, ChromSource source)
        {
            var timesCollector = chromMap.SharedTimesCollector;
            foreach (var pairPrecursor in chromMap.PrecursorCollectorMap)
            {
                if (pairPrecursor == null)
                    continue;
                var modSeq = pairPrecursor.Item1;
                var collector = pairPrecursor.Item2;
                if (chromMap.IsGroupedTime)
                    timesCollector = collector.GroupedTimesCollector;

                foreach (var pairProduct in collector.ProductIntensityMap)
                {
                    var chromCollector = pairProduct.Value;
                    if (timesCollector != null)
                        chromCollector.TimesCollector = timesCollector;
                    var key = new ChromKey(collector.ModifiedSequence,
                                           collector.PrecursorMz,
                                           pairProduct.Key.ProductMz,
                                           pairProduct.Key.ExtractionWidth,
                                           source,
                                           modSeq.Extractor,
                                           true);
                    _chromatograms.Add(new ChromKeyAndCollector
                        {
                            Key = key,
                            StatusId = collector.StatusId,
                            Collector = chromCollector
                        });
                }
            }
        }

        private void ProcessSpectrum(MsDataSpectrum dataSpectrum,
                                            ChromDataCollectorSet chromMap,
                                            double rt,
                                            SpectrumFilter filter)
        {
            foreach (var spectrum in filter.Extract(rt, dataSpectrum))
            {
                if (_loader.IsCanceled)
                    throw new LoadCanceledException(Status);

                chromMap.ProcessExtractedSpectrum(rt, spectrum);
            }
        }

        private static void ProcessSrmSpectrum(double time,
            string modifiedSequence,
            double precursorMz,
            int filterIndex,
            double[] mzs,
            double[] intensities,
            ChromDataCollectorSet chromMap)
        {
            float[] intensityFloats = new float[intensities.Length];
            for (int i = 0; i < intensities.Length; i++)
                intensityFloats[i] = (float) intensities[i];
            var spectrum = new ExtractedSpectrum(modifiedSequence, precursorMz, ChromExtractor.summed, filterIndex,
                                                 mzs, null, intensityFloats, null);
            chromMap.ProcessExtractedSpectrum(time, spectrum);
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get
            {
                for (int i = 0; i < _chromatograms.Count; i++)
                    yield return new KeyValuePair<ChromKey, int>(_chromatograms[i].Key, i);
            }
        }

        public override void GetChromatogram(int id, out ChromExtra extra, out float[] times, out float[] intensities, out float[] massErrors)
        {
            var keyAndCollector = _chromatograms[id];
            keyAndCollector.Collector.ReleaseChromatogram(out times, out intensities, out massErrors);

            extra = new ChromExtra(keyAndCollector.StatusId, LoadingStatus.Transitions.GetRank(id));  // TODO: Get rank

            // Assume that each chromatogram will be read once, though this may
            // not always be completely true.
            _readChromatograms++;

            if (_readChromatograms < _chromatograms.Count)
                SetPercentComplete(LOAD_PERCENT + BUILD_PERCENT + _readChromatograms * READ_PERCENT / _chromatograms.Count);
        }

        public override float? MaxRetentionTime
        {
            get { return LoadingStatus.Transitions.MaxRetentionTime; }
        }

        public override float? MaxIntensity
        {
            get { return LoadingStatus.Transitions.MaxIntensity; }
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
            _allocator.Dispose();
        }

        public static bool HasSpectrumData(MsDataFileImpl dataFile)
        {
            return dataFile.SpectrumCount > 0;
        }
   }

    internal enum TimeSharing { single, shared, grouped }

    internal sealed class ChromDataCollectorSet
    {
        public ChromDataCollectorSet(ChromSource chromSource, TimeSharing timeSharing,
                                     ChromatogramLoadingStatus.TransitionData allChromData)
        {
            ChromSource = chromSource;
            TypeOfScans = timeSharing;
            PrecursorCollectorMap = new List<Tuple<PrecursorModSeq, ChromDataCollector>>();
            if (timeSharing == TimeSharing.shared)
                SharedTimesCollector = new ChromCollector();
            _allChromData = allChromData;
        }

        private ChromSource ChromSource { get; set; }

        private TimeSharing TypeOfScans { get; set; }
        private readonly ChromatogramLoadingStatus.TransitionData _allChromData;

        public bool IsSingleTime { get { return TypeOfScans == TimeSharing.single; } }
        public bool IsGroupedTime { get { return TypeOfScans == TimeSharing.grouped; } }
        public bool IsSharedTime { get { return TypeOfScans == TimeSharing.shared; } }

        public ChromCollector SharedTimesCollector { get; private set; }
        
        public void AddSharedTime(float time)
        {
            SharedTimesCollector.Add(time);
        }

        public IList<Tuple<PrecursorModSeq, ChromDataCollector>> PrecursorCollectorMap { get; private set; }

        public int Count { get { return PrecursorCollectorMap.Count; } }

        public void ProcessExtractedSpectrum(double time, ExtractedSpectrum spectrum)
        {
            double precursorMz = spectrum.PrecursorMz;
            string modifiedSequence = spectrum.ModifiedSequence;
            ChromExtractor extractor = spectrum.Extractor;
            int ionScanCount = spectrum.Mzs.Length;
            ChromDataCollector collector;
            var key = new PrecursorModSeq(precursorMz, modifiedSequence, extractor);
            int index = spectrum.FilterIndex;
            while (PrecursorCollectorMap.Count <= index)
                PrecursorCollectorMap.Add(null);
            if (PrecursorCollectorMap[index] != null)
                collector = PrecursorCollectorMap[index].Item2;
            else
            {
                collector = new ChromDataCollector(modifiedSequence, precursorMz, index, IsGroupedTime);
                PrecursorCollectorMap[index] = new Tuple<PrecursorModSeq, ChromDataCollector>(key, collector);
            }

            int ionCount = collector.ProductIntensityMap.Count;
            if (ionCount == 0)
                ionCount = ionScanCount;

            // Add new time to the shared time list if not SRM, which doesn't share times, or
            // the times are shared with the entire set, as in MS1
            int lenTimes = collector.TimeCount;
            if (IsGroupedTime)
                lenTimes = collector.AddGroupedTime((float)time);

            // Add intensity values to ion scans

            for (int j = 0; j < ionScanCount; j++)
            {
                double productMz = spectrum.Mzs[j];
                double extractionWidth = spectrum.ExtractionWidths != null
                                             ? spectrum.ExtractionWidths[j]
                                             : 0;
                var productKey = new ProductExtractionWidth(productMz, extractionWidth);

                ChromCollector tis;
                if (!collector.ProductIntensityMap.TryGetValue(productKey, out tis))
                {
                    tis = new ChromCollector();
                    if (IsSingleTime)
                        tis.TimesCollector = new ChromCollector();
                    if (spectrum.MassErrors != null)
                        tis.MassErrorCollector = new ChromCollector();
                    // If more than a single ion scan, add any zeros necessary
                    // to make this new chromatogram have an entry for each time.
                    if (ionScanCount > 1)
                    {
                        for (int k = 0; k < lenTimes - 1; k++)
                            tis.Add(0);
                    }
                    collector.ProductIntensityMap.Add(productKey, tis);
                }
                if (IsSingleTime)
                    tis.AddTime((float)time);
                if (spectrum.MassErrors != null)
                    tis.AddMassError(spectrum.MassErrors[j]);
                tis.Add(spectrum.Intensities[j]);
            }

            // Add data for chromatogram graph.
            if (_allChromData != null && spectrum.PrecursorMz != 0) // Exclude TIC and BPC
                _allChromData.Add(spectrum.FilterIndex, ChromSource, (float) time, spectrum.Intensities);

            // If this was a multiple ion scan and not all ions had measurements,
            // make sure missing ions have zero intensities in the chromatogram.
            if (ionScanCount > 1 &&
                (ionCount != ionScanCount || ionCount != collector.ProductIntensityMap.Count))
            {
                // Times should have gotten one longer
                foreach (var tis in collector.ProductIntensityMap.Values)
                {
                    if (tis.Length < lenTimes)
                    {
                        tis.Add(0);
                    }
                }
            }
        }
    }

    internal sealed class ChromDataCollector
    {
        public ChromDataCollector(string modifiedSequence, double precursorMz, int statusId, bool isGroupedTime)
        {
            ModifiedSequence = modifiedSequence;
            PrecursorMz = precursorMz;
            StatusId = statusId;
            ProductIntensityMap = new Dictionary<ProductExtractionWidth, ChromCollector>(); 
            if (isGroupedTime)
                GroupedTimesCollector = new ChromCollector();
        }

        public string ModifiedSequence { get; private set; }
        public double PrecursorMz { get; private set; }
        public int StatusId { get; private set; }
        public Dictionary<ProductExtractionWidth, ChromCollector> ProductIntensityMap { get; private set; }
        public readonly ChromCollector GroupedTimesCollector;
        
        public int AddGroupedTime(float time)
        {
            return GroupedTimesCollector.Add(time);
        }

        public int TimeCount
        {
            get
            {
                // Return the length of any existing time list (in case there are no shared times)
                foreach (var tis in ProductIntensityMap.Values)
                    return tis.Length;
                return 0;
            }
        }
    }

    public sealed class SpectrumFilter
    {
        private readonly TransitionFullScan _fullScan;
        private readonly TransitionInstrument _instrument;
        private readonly FullScanAcquisitionMethod _acquisitionMethod;
        private readonly bool _isHighAccMsFilter;
        private readonly bool _isHighAccProductFilter;
        private readonly bool _isSharedTime;
        private readonly double? _minTime;
        private readonly double? _maxTime;
        private readonly SpectrumFilterPair[] _filterMzValues;
        private readonly bool _isWatersMse;
        private int _mseLevel;
        private MsDataSpectrum _mseLastSpectrum;

        public IEnumerable<SpectrumFilterPair> FilterPairs { get { return _filterMzValues; } }

        public SpectrumFilter(SrmDocument document, MsDataFileImpl dataFile)
        {
            _fullScan = document.Settings.TransitionSettings.FullScan;
            _instrument = document.Settings.TransitionSettings.Instrument;
            _acquisitionMethod = _fullScan.AcquisitionMethod;

            var comparer = PrecursorModSeq.PrecursorModSeqComparerInstance;
            var dictPrecursorMzToFilter = new SortedDictionary<PrecursorModSeq, SpectrumFilterPair>(comparer);

            if (EnabledMs || EnabledMsMs)
            {
                if (EnabledMs)
                {
                    _isHighAccMsFilter = !Equals(_fullScan.PrecursorMassAnalyzer,
                                                 FullScanMassAnalyzerType.qit);

                    var key = new PrecursorModSeq(0, null, ChromExtractor.summed);  // TIC
                    dictPrecursorMzToFilter.Add(key, new SpectrumFilterPair(key, dictPrecursorMzToFilter.Count,
                        _instrument.MinTime, _instrument.MaxTime, _isHighAccMsFilter, _isHighAccProductFilter));
                    key = new PrecursorModSeq(0, null, ChromExtractor.base_peak);   // BPC
                    dictPrecursorMzToFilter.Add(key, new SpectrumFilterPair(key, dictPrecursorMzToFilter.Count,
                        _instrument.MinTime, _instrument.MaxTime, _isHighAccMsFilter, _isHighAccProductFilter));
                }
                if (EnabledMsMs)
                {
                    _isHighAccProductFilter = !Equals(_fullScan.ProductMassAnalyzer,
                                                      FullScanMassAnalyzerType.qit);

                    if (_fullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA &&
                            _fullScan.IsolationScheme.IsAllIons)
                    {
                        _isWatersMse = dataFile.IsWatersFile;
                        _mseLevel = 1;
                    }
                }

                Func<double, double> calcWindowsQ1 = _fullScan.GetPrecursorFilterWindow;
                Func<double, double> calcWindowsQ3 = _fullScan.GetProductFilterWindow;
                _minTime = _instrument.MinTime;
                _maxTime = _instrument.MaxTime;
                bool canSchedule;
                if (RetentionTimeFilterType.scheduling_windows == _fullScan.RetentionTimeFilterType)
                {
                    canSchedule = document.Settings.PeptideSettings.Prediction.CanSchedule(document, PeptidePrediction.SchedulingStrategy.any);
                }
                else if (RetentionTimeFilterType.ms2_ids == _fullScan.RetentionTimeFilterType)
                {
                    canSchedule = true;
                }
                else
                {
                    canSchedule = false;
                }
                // TODO: Figure out a way to turn off time sharing on first SIM scan so that
                //       times can be shared for MS1 without SIM scans
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
                            if (RetentionTimeFilterType.scheduling_windows == _fullScan.RetentionTimeFilterType)
                            {
                                double windowRT;
                                double? centerTime = document.Settings.PeptideSettings.Prediction.PredictRetentionTime(
                                    document, nodePep, nodeGroup, replicateNum, schedulingAlgorithm, false, out windowRT);
                                if (centerTime != null)
                                {
                                    double startTime = centerTime.Value - windowRT / 2;
                                    double endTime = startTime + windowRT;
                                    minTime = Math.Max(minTime ?? 0, startTime);
                                    maxTime = Math.Min(maxTime ?? double.MaxValue, endTime);
                                }
                            }
                            else if (RetentionTimeFilterType.ms2_ids == _fullScan.RetentionTimeFilterType)
                            {
                                var times = document.Settings.GetRetentionTimes(dataFile.FilePath,
                                                                                nodePep.Peptide.Sequence,
                                                                                nodePep.ExplicitMods);
                                if (times.Length == 0)
                                {
                                    times = document.Settings.GetAllRetentionTimes(dataFile.FilePath,
                                                                                   nodePep.Peptide.Sequence,
                                                                                   nodePep.ExplicitMods);
                                }
                                if (times.Length > 0)
                                {
                                    minTime = Math.Max(minTime ?? 0, times.Min() - _fullScan.RetentionTimeFilterLength);
                                    maxTime = Math.Min(maxTime ?? double.MaxValue, times.Max() + _fullScan.RetentionTimeFilterLength);
                                }
                            }
                        }

                        SpectrumFilterPair filter;
                        string seq = nodePep.ModifiedSequence;
                        double mz = nodeGroup.PrecursorMz;
                        var key = new PrecursorModSeq(mz, seq, ChromExtractor.summed);
                        if (!dictPrecursorMzToFilter.TryGetValue(key, out filter))
                        {
                            filter = new SpectrumFilterPair(key, dictPrecursorMzToFilter.Count, minTime, maxTime,
                                _isHighAccMsFilter, _isHighAccProductFilter);
                            dictPrecursorMzToFilter.Add(key, filter);
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

        /*
        public int Count
        {
            get
            {
                return _filterMzValues != null
                           ? _filterMzValues.SelectMany(pair => pair.ArrayQ3 ?? pair.ArrayQ1).Count()
                           : 0;
            }
        }
        */

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

        private bool IsMS1Precursor(TransitionDocNode nodeTran)
        {
            return Transition.IsPrecursor(nodeTran.Transition.IonType) && !nodeTran.HasLoss;
        }

        public bool EnabledMs { get { return _fullScan.PrecursorIsotopes != FullScanPrecursorIsotopes.None; } }
        public bool IsHighAccMsFilter { get { return _isHighAccMsFilter; } }
        public bool EnabledMsMs { get { return _acquisitionMethod != FullScanAcquisitionMethod.None; } }
        public bool IsHighAccProductFilter { get { return _isHighAccProductFilter; } }
        public bool IsSharedTime { get { return _isSharedTime; } }

        public bool ContainsTime(double time)
        {
            return (!_minTime.HasValue || _minTime.Value <= time) &&
                   (!_maxTime.HasValue || _maxTime.Value >= time);
        }

        public double? MaxTime { get { return _maxTime; } }

        public bool IsMsSpectrum(MsDataSpectrum dataSpectrum)
        {
            if (!EnabledMs)
                return false;
            if (_mseLevel > 0)
                return UpdateMseLevel(dataSpectrum) == 1;
            return dataSpectrum.Level == 1;
        }

        public bool IsSimSpectrum(MsDataSpectrum dataSpectrum)
        {
            if (!EnabledMs || _mseLevel > 0)
                return false;
            return dataSpectrum.Level == 1 &&
                   IsSimIsolation(GetIsolationWindows(dataSpectrum.Precursors).FirstOrDefault());
        }

        private static bool IsSimIsolation(IsolationWindowFilter isoWin)
        {
            return isoWin.IsolationMz.HasValue && isoWin.IsolationWidth.HasValue &&
                // TODO: Introduce a variable cut-off in the document settings
                isoWin.IsolationWidth.Value < 200;
        }

        public bool IsMsMsSpectrum(MsDataSpectrum dataSpectrum)
        {
            if (!EnabledMsMs)
                return false;
            if (_mseLevel > 0)
                return UpdateMseLevel(dataSpectrum) == 2;
            return dataSpectrum.Level == 2;
        }

        private int UpdateMseLevel(MsDataSpectrum dataSpectrum)
        {
            if (_mseLastSpectrum != null && !ReferenceEquals(dataSpectrum, _mseLastSpectrum))
            {
                // Waters MSe is enumerated in two separate runs, first MS1 and then MS/MS
                // Bruker MSe is enumerated in interleaved MS1 and MS/MS scans
                if (!_isWatersMse)
                {
                    // Alternate between 1 and 2
                    _mseLevel = (_mseLevel % 2) + 1;
                }
                else if ((dataSpectrum.RetentionTime ?? 0) < (_mseLastSpectrum.RetentionTime ?? 0))
                {
                    // level 1 followed by level 2, followed by data that should be ignored
                    _mseLevel++;
                }
            }
            _mseLastSpectrum = dataSpectrum;
            return _mseLevel;
        }

        public IEnumerable<ExtractedSpectrum> SrmSpectraFromMs1Scan(double? time,
            IList<MsPrecursor> precursors, double[] mzArray, double[] intensityArray)
        {
            if (!EnabledMs || !time.HasValue || mzArray == null || intensityArray == null)
                yield break;

            // All filter pairs have a shot at filtering the MS1 scans
            foreach (var filterPair in FindMs1FilterPairs(precursors))
            {
                if (!filterPair.ContainsTime(time.Value))
                    continue;
                var filteredSrmSpectrum = filterPair.FilterQ1Spectrum(mzArray, intensityArray);
                if (filteredSrmSpectrum != null)
                    yield return filteredSrmSpectrum;
            }
        }

        public IEnumerable<ExtractedSpectrum> Extract(double? time, MsDataSpectrum dataSpectrum)
        {
            double[] mzArray = dataSpectrum.Mzs;
            double[] intensityArray = dataSpectrum.Intensities;
            if (!EnabledMsMs || !time.HasValue || mzArray == null || intensityArray == null)
                yield break;

            foreach (var isoWin in GetIsolationWindows(dataSpectrum.Precursors))
            {
                foreach (var filterPair in FindFilterPairs(isoWin, _acquisitionMethod))
                {
                    if (!filterPair.ContainsTime(time.Value))
                        continue;
                    var filteredSrmSpectrum = filterPair.FilterQ3Spectrum(mzArray, intensityArray);
                    if (filteredSrmSpectrum != null)
                        yield return filteredSrmSpectrum;
                }
            }
        }

        private IEnumerable<IsolationWindowFilter> GetIsolationWindows(IList<MsPrecursor> precursors)
        {
            // Waters MSe high-energy scans actually appear to be MS1 scans without
            // any isolation m/z.  So, use the instrument range.
            if (_mseLevel > 0)
            {
                double isolationWidth = _instrument.MaxMz - _instrument.MinMz;
                double isolationMz = _instrument.MinMz + isolationWidth / 2;
                yield return new IsolationWindowFilter(isolationMz, isolationWidth);
            }
            else if (precursors.Count > 0)
            {
                foreach (var precursor in precursors)
                    yield return new IsolationWindowFilter(precursor.IsolationMz, precursor.IsolationWidth);
            }
            else
            {
                yield return default(IsolationWindowFilter);
            }
        }

        private struct IsolationWindowFilter
        {
            public IsolationWindowFilter(double? isolationMz, double? isolationWidth) : this()
            {
                IsolationMz = isolationMz;
                IsolationWidth = isolationWidth;
            }

            public double? IsolationMz { get; private set; }
            public double? IsolationWidth { get; private set; }

            #region object overrides

            private bool Equals(IsolationWindowFilter other)
            {
                return other.IsolationMz.Equals(IsolationMz) &&
                    other.IsolationWidth.Equals(IsolationWidth);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (obj.GetType() != typeof(IsolationWindowFilter)) return false;
                return Equals((IsolationWindowFilter)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((IsolationMz.HasValue ? IsolationMz.Value.GetHashCode() : 0) * 397) ^
                        (IsolationWidth.HasValue ? IsolationWidth.Value.GetHashCode() : 0);
                }
            }

            #endregion
        }

        private readonly Dictionary<IsolationWindowFilter, IList<SpectrumFilterPair>> _filterPairDictionary =
            new Dictionary<IsolationWindowFilter, IList<SpectrumFilterPair>>();

        private IEnumerable<SpectrumFilterPair> FindFilterPairs(IsolationWindowFilter isoWin,
            FullScanAcquisitionMethod acquisitionMethod, bool ignoreIsolationScheme = false)
        {
            List<SpectrumFilterPair> filterPairs = new List<SpectrumFilterPair>();
            
            if (!isoWin.IsolationMz.HasValue)
                return filterPairs; // empty

            // Return cached value from dictionary if we've seen this target previously.
            IList<SpectrumFilterPair> filterPairsCached;
            if (_filterPairDictionary.TryGetValue(isoWin, out filterPairsCached))
            {
                return filterPairsCached;
            }

            if (acquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                double isoTargMz = isoWin.IsolationMz.Value;
                double? isoTargWidth = isoWin.IsolationWidth;
                if (!ignoreIsolationScheme)
                {
                    CalcDiaIsolationValues(ref isoTargMz, ref isoTargWidth);
                    isoWin = new IsolationWindowFilter(isoTargMz, isoTargWidth);
                }
                if (!isoTargWidth.HasValue)
                {
                    return filterPairs; // empty
                }

                // For multiple case, find the first possible value, and iterate until
                // no longer matching or the end of the array is encountered
                int iFilter = IndexOfFilter(isoTargMz, isoTargWidth.Value);
                if (iFilter != -1)
                {
                    while (iFilter < _filterMzValues.Length && CompareMz(isoTargMz,
                            _filterMzValues[iFilter].Q1, isoTargWidth.Value) == 0)
                        filterPairs.Add(_filterMzValues[iFilter++]);
                }
            }
            else
            {
                // For single case, review all possible matches for the one closest to the
                // desired precursor m/z value.
                SpectrumFilterPair filterPairBest = null;
                double minMzDelta = double.MaxValue;

                // Isolation width for single is based on the instrument m/z match tolerance
                double isoTargMz = isoWin.IsolationMz.Value;
                isoWin = new IsolationWindowFilter(isoTargMz, _instrument.MzMatchTolerance*2);

                foreach (var filterPair in FindFilterPairs(isoWin, FullScanAcquisitionMethod.DIA, true))
                {
                    double mzDelta = Math.Abs(isoTargMz - filterPair.Q1);
                    if (mzDelta < minMzDelta)
                    {
                        minMzDelta = mzDelta;
                        filterPairBest = filterPair;
                    }
                }

                if (filterPairBest != null)
                    filterPairs.Add(filterPairBest);
            }

            _filterPairDictionary[isoWin] = filterPairs;
            return filterPairs;
        }

        private void CalcDiaIsolationValues(ref double isolationTargetMz,
                                            ref double? isolationWidth)
        {
            double isolationWidthValue;
            var isolationScheme = _fullScan.IsolationScheme;
            if (isolationScheme == null)
            {                
                throw new InvalidOperationException("Unexpected attempt to calculate DIA isolation window without an isolation scheme");
            }

            // Calculate window for a simple isolation scheme.
            else if (isolationScheme.PrecursorFilter.HasValue)
            {
                // Use the user specified isolation width, unless it is larger than
                // the acquisition isolation width.  In this case the chromatograms
                // may be very confusing (spikey), because of incorrectly included
                // data points.
                isolationWidthValue = isolationScheme.PrecursorFilter.Value +
                                      (isolationScheme.PrecursorRightFilter ?? 0);
                if (isolationWidth.HasValue && isolationWidth.Value < isolationWidthValue)
                    isolationWidthValue = isolationWidth.Value;

                // Make sure the isolation target is centered in the desired window, even
                // if the window was specified as being asymetric
                if (isolationScheme.PrecursorRightFilter.HasValue)
                    isolationTargetMz += isolationScheme.PrecursorRightFilter.Value - isolationWidthValue/2;
            }

            // Find isolation window.
            else if (isolationScheme.PrespecifiedIsolationWindows.Count > 0)
            {
                IsolationWindow isolationWindow = null;

                // Match pre-specified targets.
                if (isolationScheme.PrespecifiedIsolationWindows[0].Target.HasValue)
                {
                    foreach (var window in isolationScheme.PrespecifiedIsolationWindows)
                    {
                        if (!window.TargetMatches(isolationTargetMz, _instrument.MzMatchTolerance)) continue;
                        if (isolationWindow != null)
                            {
                                throw new InvalidDataException(
                                    string.Format(Resources.SpectrumFilter_FindFilterPairs_Two_isolation_windows_contain_targets_which_match_the_isolation_target__0__,
                                                  isolationTargetMz));
                            }
                        isolationWindow = window;
                    }
                }

                // Find containing window.
                else
                {
                    foreach (var window in isolationScheme.PrespecifiedIsolationWindows)
                    {
                        if (!window.Contains(isolationTargetMz)) continue;
                        if (isolationWindow != null)
                            {
                                throw new InvalidDataException(
                                    string.Format(Resources.SpectrumFilter_FindFilterPairs_Two_isolation_windows_contain_the_isolation_target__0__,
                                                  isolationTargetMz));
                            }
                        isolationWindow = window;
                    }
                }

                if (isolationWindow == null)
                {
                    _filterPairDictionary[new IsolationWindowFilter(isolationTargetMz, isolationWidth)] = new List<SpectrumFilterPair>();
                    isolationWidth = null;
                    return;
                }

                isolationWidthValue = isolationWindow.End - isolationWindow.Start;
                isolationTargetMz = isolationWindow.Start + isolationWidthValue/2;
            }

            // MSe just uses the instrument isolation window
            else if (isolationWidth.HasValue && isolationScheme.IsAllIons)
            {
                isolationWidthValue = isolationWidth.Value;
            }

            // No defined isolation scheme?
            else
            {
                    throw new InvalidDataException(Resources.SpectrumFilter_FindFilterPairs_Isolation_scheme_does_not_contain_any_isolation_windows);
            }
            isolationWidth = isolationWidthValue;
        }

        private IEnumerable<SpectrumFilterPair> FindMs1FilterPairs(IList<MsPrecursor> precursors)
        {
            if (precursors.Count > 1)
                return FindSimFilterPairs(precursors);  // SIM scans
            var isoWin = GetIsolationWindows(precursors).FirstOrDefault();
            if (!IsSimIsolation(isoWin))
                return _filterMzValues; // survey scan
            return FindFilterPairs(isoWin, FullScanAcquisitionMethod.DIA, true);  // SIM scan
        }

        private IEnumerable<SpectrumFilterPair> FindSimFilterPairs(IList<MsPrecursor> precursors)
        {
            return GetIsolationWindows(precursors).SelectMany(isoWin =>
                FindFilterPairs(isoWin, FullScanAcquisitionMethod.DIA, true));  // SIM scan
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

    public struct PrecursorModSeq
    {
        public PrecursorModSeq(double precursorMz, string modifiedSequence, ChromExtractor extractor) : this()
        {
            PrecursorMz = precursorMz;
            ModifiedSequence = modifiedSequence;
            Extractor = extractor;
        }

        public double PrecursorMz { get; private set; }
        public string ModifiedSequence { get; private set; }
        public ChromExtractor Extractor { get; private set; }

        #region object overrides

        public bool Equals(PrecursorModSeq other)
        {
            return PrecursorMz.Equals(other.PrecursorMz) &&
                string.Equals(ModifiedSequence, other.ModifiedSequence) &&
                Extractor == other.Extractor;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is PrecursorModSeq && Equals((PrecursorModSeq) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PrecursorMz.GetHashCode();
                hashCode = (hashCode*397) ^ (ModifiedSequence != null ? ModifiedSequence.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (int) Extractor;
                return hashCode;
            }
        }

        private sealed class PrecursorMzModifiedSequenceComparer : IComparer<PrecursorModSeq>
        {
            public int Compare(PrecursorModSeq x, PrecursorModSeq y)
            {
                int c = Comparer.Default.Compare(x.PrecursorMz, y.PrecursorMz);
                if (c != 0)
                    return c;
                c = string.CompareOrdinal(x.ModifiedSequence, y.ModifiedSequence);
                if (c != 0)
                    return c;
                return x.Extractor - y.Extractor;
            }
        }

        private static readonly IComparer<PrecursorModSeq> PRECURSOR_MOD_SEQ_COMPARER_INSTANCE = new PrecursorMzModifiedSequenceComparer();

        public static IComparer<PrecursorModSeq> PrecursorModSeqComparerInstance
        {
            get { return PRECURSOR_MOD_SEQ_COMPARER_INSTANCE; }
        }

        #endregion
    }

    internal struct ProductExtractionWidth
    {
        public ProductExtractionWidth(double productMz, double extractionWidth) : this()
        {
            ProductMz = productMz;
            ExtractionWidth = extractionWidth;
        }

        public double ProductMz { get; private set; }
        public double ExtractionWidth { get; private set; }

        #region object overrides

        public bool Equals(ProductExtractionWidth other)
        {
            return ProductMz.Equals(other.ProductMz) && ExtractionWidth.Equals(other.ExtractionWidth);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ProductExtractionWidth && Equals((ProductExtractionWidth) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ProductMz.GetHashCode()*397) ^ ExtractionWidth.GetHashCode();
            }
        }

        #endregion
    }

    public sealed class SpectrumFilterPair : IComparable<SpectrumFilterPair>
    {
        public SpectrumFilterPair(PrecursorModSeq precursorModSeq, int id, double? minTime, double? maxTime, bool highAccQ1, bool highAccQ3)
        {
            Id = id;
            ModifiedSequence = precursorModSeq.ModifiedSequence;
            Q1 = precursorModSeq.PrecursorMz;
            Extractor = precursorModSeq.Extractor;
            MinTime = minTime;
            MaxTime = maxTime;
            HighAccQ1 = highAccQ1;
            HighAccQ3 = highAccQ3;

            if (Q1 == 0)
            {
                ArrayQ1 = ArrayQ1Window = new[] {0.0};
            }
        }

        public int Id { get; private set; }
        public ChromExtractor Extractor { get; private set; }
        public bool HighAccQ1 { get; private set; }
        public bool HighAccQ3 { get; private set; }
        public string ModifiedSequence { get; private set; }
        public double Q1 { get; private set; }
        private double? MinTime { get; set; }
        private double? MaxTime { get; set; }
        // Q1 values for when precursor ions are filtered from MS1
        private double[] ArrayQ1 { get; set; }
        private double[] ArrayQ1Window { get; set; }
        // Q3 values for product ions filtered in MS/MS
        public double[] ArrayQ3 { get; private set; }
        public double[] ArrayQ3Window { get; private set; }

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

        public ExtractedSpectrum FilterQ1Spectrum(double[] mzArray, double[] intensityArray)
        {
            return FilterSpectrum(mzArray, intensityArray, ArrayQ1, ArrayQ1Window, HighAccQ1);
        }

        public ExtractedSpectrum FilterQ3Spectrum(double[] mzArray, double[] intensityArray)
        {
            // All-ions extraction for MS1 scans only
            if (Q1 == 0)
                return null;

            return FilterSpectrum(mzArray, intensityArray, ArrayQ3, ArrayQ3Window, HighAccQ3);
        }

        private ExtractedSpectrum FilterSpectrum(double[] mzArray, double[] intensityArray,
            double[] centerArray, double[] windowArray, bool highAcc)
        {
            int len = 1;
            if (Q1 == 0)
                highAcc = false;    // No mass error for all-ions extraction
            else
            {
                if (centerArray.Length == 0)
                    return null;
                len = centerArray.Length;
            }

            float[] extractedIntensities = new float[len];
            float[] massErrors = highAcc ? new float[len] : null;

            // Search for matching peaks for each Q3 filter
            // Use binary search to get to the first m/z value to be considered more quickly
            // This should help MS1 where isotope distributions will be very close in m/z
            // It should also help MS/MS when more selective, larger fragment ions are used,
            // since then a lot of less selective, smaller peaks must be skipped
            int iPeak = Q1 != 0
                ? Array.BinarySearch(mzArray, centerArray[0] - windowArray[0]/2)
                : 0;

            if (iPeak < 0)
                iPeak = ~iPeak;

            for (int i = 0; i < len; i++)
            {
                // Look for the first peak that is greater than the start of the filter
                double target = 0, endFilter = double.MaxValue;
                if (Q1 != 0)
                {
                    target = centerArray[i];
                    double filterWindow = windowArray[i];
                    double startFilter = target - filterWindow / 2;
                    endFilter = startFilter + filterWindow;

                    while (iPeak < mzArray.Length && mzArray[iPeak] < startFilter)
                        iPeak++;
                }

                // Add the intensity values of all peaks less than the end of the filter
                double totalIntensity = 0;
                double meanError = 0;
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
                        double deltaPeak = mz - target;
                        meanError += (deltaPeak - meanError)*intensity/totalIntensity;
                    }
                }
                extractedIntensities[i] = (float) totalIntensity;
                if (massErrors != null)
                    massErrors[i] = (float) SequenceMassCalc.GetPpm(target, meanError);
            }

            return new ExtractedSpectrum(ModifiedSequence,
                                         Q1,
                                         Extractor,
                                         Id,
                                         centerArray,
                                         windowArray,
                                         extractedIntensities,
                                         massErrors);
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

    public sealed class ExtractedSpectrum
    {
        public ExtractedSpectrum(string modifiedSequence,
                                 double precursorMz,
                                 ChromExtractor chromExtractor,
                                 int filterIndex,
                                 double[] mzs,
                                 double[] extractionWidths,
                                 float[] intensities,
                                 float[] massErrors)
        {
            ModifiedSequence = modifiedSequence;
            PrecursorMz = precursorMz;
            Extractor = chromExtractor;
            FilterIndex = filterIndex;
            Mzs = mzs;
            ExtractionWidths = extractionWidths;
            Intensities = intensities;
            MassErrors = massErrors;
        }

        public string ModifiedSequence { get; private set; }
        public double PrecursorMz { get; private set; }
        public int FilterIndex { get; private set; }
        public double[] Mzs { get; private set; }
        public double[] ExtractionWidths { get; private set; }
        public float[] Intensities { get; private set; }
        public float[] MassErrors { get; private set; }
        public ChromExtractor Extractor { get; private set; }
    }
}
