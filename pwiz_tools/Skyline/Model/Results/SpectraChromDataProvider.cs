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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class SpectraChromDataProvider : ChromDataProvider
    {
        private readonly string _cachePath;
        private Collectors _collectors;
        private Spectra _spectra;
        private IDemultiplexer _demultiplexer;
        private readonly IRetentionTimePredictor _retentionTimePredictor;
        private List<string> _scanIdList = new List<string>();
        private readonly bool _isProcessedScans;
        private double? _maxIonMobilityValue;
        private bool _isSingleMzMatch;
        private bool _sourceHasPositivePolarityData;
        private bool _sourceHasNegativePolarityData;
        private double? _ticArea;

        private readonly ChromatogramLoadingStatus.TransitionData _allChromData;

        /// <summary>
        /// The number of chromatograms read so far.
        /// </summary>
        private int _readChromatograms;

        private readonly SrmDocument _document;
        private SpectrumFilter _filter;
        private ChromGroups _chromGroups;
        private BlockWriter _blockWriter;
        private bool _isSrm;
        private readonly object _disposeLock = new object();
        private bool _isDisposing;

        private const int LOAD_PERCENT = 10;
        private const int BUILD_PERCENT = 70 - LOAD_PERCENT;
        private const int READ_PERCENT = 96 - LOAD_PERCENT - BUILD_PERCENT; // Leave 4% empty until the very end

        private const int MAX_FULL_GRADIENT_TRANSITIONS = 20*1000;

        public SpectraChromDataProvider(MsDataFileImpl dataFile,
            ChromFileInfo fileInfo,
            SrmDocument document,
            IRetentionTimePredictor retentionTimePredictor,
            string cachePath, // We'll write tempfiles in this directory
            IProgressStatus status,
            int startPercent,
            int endPercent,
            IProgressMonitor loader)
            : base(fileInfo, status, startPercent, endPercent, loader)
        {
            _document = document;
            _cachePath = cachePath;

            // If no SRM spectra, then full-scan filtering must be enabled
            _isSrm = dataFile.HasSrmSpectra;
            if (!_isSrm)
            {
                if (!document.Settings.TransitionSettings.FullScan.IsEnabled)
                    throw new NoFullScanFilteringException(FileInfo.FilePath);

                // Only use the retention time predictor on non-SRM data, and only when
                // there are enough transitions to cause performance issues with extracting
                // full-gradient in a single pass, and then trimming.
                if (_document.Settings.TransitionSettings.FullScan.RetentionTimeFilterType == RetentionTimeFilterType.scheduling_windows &&
                    _document.MoleculeTransitionCount > MAX_FULL_GRADIENT_TRANSITIONS)
                {
                    _retentionTimePredictor = retentionTimePredictor;
                }
            }

            // Only mzXML from mzWiff requires the introduction of zero values
            // during interpolation.
            _isProcessedScans = dataFile.IsMzWiffXml;

            UpdatePercentComplete();
            _maxIonMobilityValue = dataFile.GetMaxIonMobility(); // Needed for linear range ion mobility window width calculations

            // Create the filter responsible for chromatogram extraction
            bool firstPass = (_retentionTimePredictor != null);
            _filter = new SpectrumFilter(_document, FileInfo.FilePath, new DataFileInstrumentInfo(dataFile),
                _maxIonMobilityValue,
                _retentionTimePredictor, firstPass);

            if (!_isSrm && (_filter.EnabledMs || _filter.EnabledMsMs))
            {
                // Full-scan filtering should always match a single precursor
                // m/z value to a single precursor node in the document tree,
                // because that is the way the filters are constructed in the
                // first place.
                _isSingleMzMatch = true;
            }

            // Get data object used to graph all of the chromatograms.
            if (_loader.HasUI && Status is ChromatogramLoadingStatus)
                _allChromData = ((ChromatogramLoadingStatus) Status).Transitions;

            try
            {
                InitSpectrumReader(dataFile);
                InitChromatogramExtraction();
            }
            catch(Exception)
            {
                // If exception thrown before construction is complete than Dispose will not be called.
                if (_spectra == null)
                    dataFile.Dispose();
                else
                    _spectra.Dispose();

                throw;
            }
        }


        private void InitSpectrumReader(MsDataFileImpl dataFile)
        {
            // Create the spectra object responsible for delivering spectra for extraction
            _spectra = new Spectra(_document, _filter, _allChromData, dataFile);

            // Determine what type of demultiplexer, if any, to use based on settings in the
            // IsolationScheme menu
            _demultiplexer = _spectra.CreateDemultiplexer();

            if (_demultiplexer == null)
            {
                _spectra.RunAsync();
            }
        }

        private void InitChromatogramExtraction()
        {
            // Load SRM chromatograms synchronously.
            if (_isSrm)
            {
                _collectors = new Collectors();
                ExtractChromatograms();
            }
            // Load non-SRM chromatograms asynchronously.
            else
            {
                var keys = _filter.ProductChromKeys;
                bool runAsync = _retentionTimePredictor != null || keys.Any(k => k.OptionalMaxTime.HasValue);
                _collectors = new Collectors(_filter.ProductChromKeys, runAsync);
            }
        }

        public override bool CompleteFirstPass()
        {
            // Ignore this notification, if there is no retention time predictor
            if (_retentionTimePredictor == null)
                return false;

            ExtractionComplete();
            var dataFile = _spectra.Detach();

            // Start the second pass
            _filter = new SpectrumFilter(_document, FileInfo.FilePath, _filter, _maxIonMobilityValue, _retentionTimePredictor);
            _spectra = null;
            _isSrm = false;

            InitSpectrumReader(dataFile);
            InitChromatogramExtraction();
            return true;
        }

        public override MsDataFileImpl.eIonMobilityUnits IonMobilityUnits { get { return _filter.IonMobilityUnits; } }

        private void ExtractionComplete()
        {
            if (_collectors != null)
                _collectors.ExtractionComplete();
        }

        /// <summary>
        /// Process spectra, gathering chromatogram information, synchronously for
        /// SRM spectra, or on an async thread for other cases.
        /// </summary>
        private void ExtractChromatograms()
        {
            lock (_disposeLock)
            {
                ExtractChromatogramsLocked();
            }
        }

        private void ExtractChromatogramsLocked()
        {
            // First read all of the spectra, building chromatogram time, intensity lists
            var fragmentTimeSharing = _spectra.HasSrmSpectra ? TimeSharing.single : TimeSharing.grouped;
            var chromMap = new ChromDataCollectorSet(ChromSource.fragment, fragmentTimeSharing, _allChromData, _blockWriter);
            var ms1TimeSharing = _filter.IsSharedTime ? TimeSharing.shared : TimeSharing.grouped;
            var chromMapMs1 = new ChromDataCollectorSet(ChromSource.ms1, ms1TimeSharing, _allChromData, _blockWriter);
            var chromMapSim = new ChromDataCollectorSet(ChromSource.sim, TimeSharing.grouped, _allChromData, _blockWriter);
            var chromMaps = new[] {chromMap, chromMapSim, chromMapMs1};

            var dictPrecursorMzToIndex = new Dictionary<SignedMz, int>(); // For SRM processing

            var peptideFinder = _spectra.HasSrmSpectra ? new PeptideFinder(_document) : null;

            while (_spectra.NextSpectrum())
            {
                if (_isDisposing)
                {
                    CompleteChromatograms(chromMaps);
                    return;
                }

                var isNegative = _spectra.CurrentSpectrumIsNegative;
                if (isNegative.HasValue)
                {
                    if (isNegative.Value)
                    {            
                        _sourceHasNegativePolarityData = true;
                    }
                    else
                    {
                        _sourceHasPositivePolarityData = true;
                    }
                }

                UpdatePercentComplete();

                if (_spectra.HasSrmSpectra)
                {
                    var dataSpectrum = _spectra.CurrentSpectrum;

                    var precursorMz = dataSpectrum.Precursors[0].PrecursorMz ?? SignedMz.ZERO;
                    int filterIndex;
                    if (!dictPrecursorMzToIndex.TryGetValue(precursorMz, out filterIndex))
                    {
                        filterIndex = dictPrecursorMzToIndex.Count;
                        dictPrecursorMzToIndex.Add(precursorMz, filterIndex);
                    }

                    // Process the one SRM spectrum
                    var peptideNode = peptideFinder != null ? peptideFinder.FindPeptide(precursorMz) : null;
                    ProcessSrmSpectrum(
                        (float) dataSpectrum.RetentionTime.Value,
                        peptideNode != null ? peptideNode.ModifiedTarget : null,
                        peptideNode != null ? peptideNode.Color : PeptideDocNode.UNKNOWN_COLOR,
                        precursorMz,
                        filterIndex,
                        dataSpectrum.Mzs,
                        dataSpectrum.Intensities,
                        IonMobilityFilter.EMPTY, // ion mobility unknown
                        chromMap);
                }
                else if (_filter.EnabledMsMs || _filter.EnabledMs)
                {
                    var dataSpectrum = _spectra.CurrentSpectrum;
                    var spectra = _spectra.CurrentSpectra;

                    float rt = _spectra.CurrentTime;
                    if (_allChromData != null)
                        _allChromData.CurrentTime = rt;

                    if (_filter.IsMsSpectrum(dataSpectrum))
                    {
                        var chromMapMs = _filter.IsSimSpectrum(dataSpectrum, spectra) ? chromMapSim : chromMapMs1;
                        string scanId = dataSpectrum.Id;

                        // Process all SRM spectra that can be generated by filtering this full-scan MS1
                        if (chromMapMs.IsSharedTime)
                        {
                            chromMapMs.AddSharedTime(rt, GetScanIdIndex(scanId));
                        }
                        lock (_blockWriter)
                        {
                            foreach (var spectrum in _filter.SrmSpectraFromMs1Scan(rt, dataSpectrum.Precursors, spectra))
                            {
                                chromMapMs.ProcessExtractedSpectrum(rt, _collectors, GetScanIdIndex(scanId), spectrum, AddChromCollector);
                            }
                        }
                    }
                    if (_filter.IsMsMsSpectrum(dataSpectrum))
                    {
                        // Process all SRM spectra that can be generated by filtering this full-scan MS/MS
                        if (_demultiplexer == null)
                        {
                            ProcessSpectrumList(spectra, chromMap, rt, _filter, dataSpectrum.Id);
                        }
                        else
                        {
                            int i = _spectra.CurrentIndex;
                            foreach (var deconvSpectrum in _demultiplexer.GetDeconvolvedSpectra(i, dataSpectrum))
                            {
                                ProcessSpectrumList(new[] {deconvSpectrum}, chromMap, rt, _filter, dataSpectrum.Id);
                            }
                        }
                    }

                    // Complete any chromatograms with filter pairs with a maximum time earlier
                    // than the current retention time.
                    CompleteChromatograms(chromMaps, rt);
                }
            }
            
            string log = _spectra.GetLog();
            if (log != null) // in case perf logging is enabled
                DebugLog.Info(log);

            if (_spectra.HasSrmSpectra)
            {
                foreach (var map in chromMaps)
                    AddChromatograms(map);
            }
            else
            {
                CompleteChromatograms(chromMaps);
            }

            if (chromMap.Count == 0 && chromMapMs1.Count == 0 && chromMapSim.Count == 0)
                throw new NoFullScanDataException(FileInfo.FilePath);
        }

        private void AddChromCollector(int productFilterId, ChromCollector collector)
        {
            _collectors.AddCollector(productFilterId, collector);
        }

        private void CompleteChromatograms(ChromDataCollectorSet[] chromMaps, float retentionTime = -1)
        {
            var finishedFilterPairs = _filter.RemoveFinishedFilterPairs(retentionTime);
            foreach (var filterPair in finishedFilterPairs)
                AddChromatogramsForFilterPair(chromMaps, filterPair);

            // Update time for which chromatograms are available.
            var collectors = _collectors;
            if (collectors != null)
                collectors.AddComplete(retentionTime >= 0 ? retentionTime : float.MaxValue);
        }

        private void AddChromatogramsForFilterPair(ChromDataCollectorSet[] chromMaps, SpectrumFilterPair filterPair)
        {
            // Fill the chromatograms that were actually extracted
            foreach (var chromMap in chromMaps)
            {
                if (filterPair.Id >= chromMap.PrecursorCollectorMap.Count ||
                    chromMap.PrecursorCollectorMap[filterPair.Id] == null)
                    continue;

                var pairPrecursor = chromMap.PrecursorCollectorMap[filterPair.Id];
                chromMap.PrecursorCollectorMap[filterPair.Id] = null;
                var collector = pairPrecursor.Item2;
                var scanIdCollector = chromMap.ScanIdsCollector;
                var timesCollector = chromMap.SharedTimesCollector;
                if (chromMap.IsGroupedTime)
                {
                    scanIdCollector = collector.ScansCollector;
                    timesCollector = collector.GroupedTimesCollector;
                }
                    
                foreach (var pairProduct in collector.ProductIntensityMap)
                {
                    var chromCollector = pairProduct.Value;
                    if (timesCollector != null)
                    {
                        chromCollector.SetScans(scanIdCollector);
                        chromCollector.SetTimes(timesCollector);
                    }

                    // Otherwise NRE will occur later
                    Assume.IsTrue(chromCollector.IsSetTimes);

                    _collectors.AddCollector(pairProduct.Key.FilterId, chromCollector);
                }
            }
        }

        private int GetScanIdIndex(string id)
        {
            if (_scanIdList.Count > 0)
            {
                if (id == _scanIdList[_scanIdList.Count - 1])
                {
                    return _scanIdList.Count - 1;
                }
            }
            int nextIndex = _scanIdList.Count;
            _scanIdList.Add(id);
            return nextIndex;
        }

        private void AddChromatograms(ChromDataCollectorSet chromMap)
        {
            var scanIdCollector = chromMap.ScanIdsCollector;
            var timesCollector = chromMap.SharedTimesCollector;
            foreach (var pairPrecursor in chromMap.PrecursorCollectorMap)
            {
                if (pairPrecursor == null)
                    continue;
                var modSeq = pairPrecursor.Item1;
                var collector = pairPrecursor.Item2;
                if (chromMap.IsGroupedTime)
                {
                    scanIdCollector = collector.ScansCollector;
                    timesCollector = collector.GroupedTimesCollector;
                }

                foreach (var pairProduct in collector.ProductIntensityMap)
                {
                    var chromCollector = pairProduct.Value;
                    if (timesCollector != null)
                    {
                        chromCollector.SetScans(scanIdCollector);
                        chromCollector.SetTimes(timesCollector);
                    }
                    var key = new ChromKey(
                        collector.ModifiedSequence,
                        collector.PrecursorMz,
                        collector.IonMobility,
                        pairProduct.Key.TargetMz,
                        0,
                        pairProduct.Key.FilterWidth,
                        chromMap.ChromSource,
                        modSeq.Extractor,
                        true,
                        true,
                        null,
                        null);

                    _collectors.AddSrmCollector(key, chromCollector);
                }
            }
        }

        /// <summary>
        /// Process a list of spectra - typically of length one,
        /// but possibly a set of drift bins all with same retention time,
        /// or a set of Agilent ramped-CE Mse scans to be averaged
        /// </summary>
        private void ProcessSpectrumList(MsDataSpectrum[] spectra,
                                     ChromDataCollectorSet chromMap,
                                     float rt,
                                     SpectrumFilter filter,
                                     string scanId)
        {
            lock (_blockWriter)
            {
                foreach (var spectrum in filter.Extract(rt, spectra))
                {
                    if (_loader.IsCanceled)
                    {
                        _loader.UpdateProgress(Status = Status.Cancel());
                        throw new LoadCanceledException(Status);
                    }

                    chromMap.ProcessExtractedSpectrum(rt, _collectors, GetScanIdIndex(scanId), spectrum, AddChromCollector);
                }
            }
        }

        private void ProcessSrmSpectrum(float time,
                                               Target modifiedSequence,
                                               Color peptideColor,
                                               SignedMz precursorMz,
                                               int filterIndex,
                                               double[] mzs,
                                               double[] intensities,
                                               IonMobilityFilter ionMobility,
                                               ChromDataCollectorSet chromMap)
        {
            float[] intensityFloats = new float[intensities.Length];
            for (int i = 0; i < intensities.Length; i++)
                intensityFloats[i] = (float) intensities[i];
            var productFilters = mzs.Select(mz => new SpectrumProductFilter(new SignedMz(mz, precursorMz.IsNegative), 0)).ToArray();
            var spectrum = new ExtractedSpectrum(modifiedSequence, peptideColor, precursorMz, ionMobility,
                ChromExtractor.summed, filterIndex, productFilters, intensityFloats, null);
            chromMap.ProcessExtractedSpectrum(time, _collectors, -1, spectrum, null);
        }

        public override IEnumerable<ChromKeyProviderIdPair> ChromIds
        {
            get
            {
                var chromIds = new ChromKeyProviderIdPair[_collectors.ChromKeys.Count];
                for (int i = 0; i < chromIds.Length; i++)
                    chromIds[i] = new ChromKeyProviderIdPair(_collectors.ChromKeys[i], i); 
                return chromIds;
            }
        }

        public override byte[] MSDataFileScanIdBytes
        {
            get { return MsDataFileScanIds.ToBytes(_scanIdList); }
        }

        public override void SetRequestOrder(IList<IList<int>> chromatogramRequestOrder)
        {
            if (_isSrm)
                return;

            if (_chromGroups != null)
                _chromGroups.Dispose();

            _chromGroups = new ChromGroups(chromatogramRequestOrder, _collectors.ChromKeys, (float) (MaxRetentionTime ?? 30), _cachePath);
            _blockWriter = new BlockWriter(_chromGroups);

            if (!_collectors.IsRunningAsync)
                ExtractChromatograms();
            else
            {
                ActionUtil.RunAsync(() =>
                {
                    try
                    {
                        ExtractChromatograms();
                    }
                    catch (Exception ex)
                    {
                        if (_collectors == null)
                            throw;

                        _collectors.SetException(ex);
                    }
                }, "Chromatogram extractor"); // Not L10N
            }
        }

        public override bool GetChromatogram(int id, Target modifiedSequence, Color peptideColor, out ChromExtra extra, out TimeIntensities timeIntensities)
        {
            var statusId = _collectors.ReleaseChromatogram(id, _chromGroups,
                out timeIntensities);
            if (timeIntensities.NumPoints > 0)
            {
                var chromKey = _collectors.ChromKeys[id];
                if (SignedMz.ZERO.Equals(chromKey.Precursor) && SignedMz.ZERO.Equals(chromKey.Product) &&
                    ChromExtractor.summed == chromKey.Extractor)
                {
                    _ticArea = timeIntensities.Integral(0, timeIntensities.NumPoints - 1);
                }
            }
            extra = new ChromExtra(statusId, 0);

            // Each chromatogram will be read only once!
            _readChromatograms++;

            UpdatePercentComplete();
            return timeIntensities.NumPoints > 0;
        }

        private void UpdatePercentComplete()
        {
            int basePercent = LOAD_PERCENT;
            if (_retentionTimePredictor != null && _filter != null && !_filter.IsFirstPass)
                basePercent += BUILD_PERCENT / 2;

            int percentComplete = 0;
            if (_spectra != null)
                percentComplete = _spectra.PercentComplete * BUILD_PERCENT / 100;
            if (_retentionTimePredictor != null)
                percentComplete /= 2;

            int chromPercent = 0;
            // For two-pass extraction, just ignore the chromatograms for the first pass, as they are only
            // a very small fraction of the total number.
            if (_retentionTimePredictor == null || (_filter != null && !_filter.IsFirstPass))
            {
                if (_collectors != null && _collectors.Count > 0)
                    chromPercent = Math.Min(_readChromatograms, _collectors.Count) * READ_PERCENT / _collectors.Count;
            }

            SetPercentComplete(basePercent + percentComplete + chromPercent);
        }

        public override double? MaxRetentionTime
        {
            get { return Status is ChromatogramLoadingStatus ? ((ChromatogramLoadingStatus)Status).Transitions.MaxRetentionTime : 0; }
        }

        public override double? MaxIntensity
        {
            get { return Status is ChromatogramLoadingStatus ? ((ChromatogramLoadingStatus)Status).Transitions.MaxIntensity : 0; }
        }

        public override double? TicArea
        {
            get { return _ticArea; } 
        }

        public override bool IsProcessedScans
        {
            get { return _isProcessedScans; }
        }

        public override bool IsSingleMzMatch
        {
            get { return _isSingleMzMatch; }
        }

        public override bool SourceHasPositivePolarityData
        {
            get { return _sourceHasPositivePolarityData; }
        }

        public override bool SourceHasNegativePolarityData
        {
            get { return _sourceHasNegativePolarityData; }
        }

        public override void ReleaseMemory()
        {
        }

        public override void Dispose()
        {
            _isDisposing = true;
            lock (_disposeLock)
            {
                _spectra.Dispose();
                _scanIdList = null;
                if (_chromGroups != null)
                {
                    _chromGroups.Dispose();
                    _chromGroups = null;
                }
            }
            _collectors = null;
        }

        public static bool HasSpectrumData(MsDataFileImpl dataFile)
        {
            return dataFile.SpectrumCount > 0;
        }

        private class Spectra : IDisposable
        {
            private bool _runningAsync;
            private readonly SrmDocument _document;
            private readonly SpectrumFilter _filter;
            private readonly object _dataFileLock = new object();
            private MsDataFileImpl _dataFile;
            private LookaheadContext _lookaheadContext;
            private readonly int _countSpectra;
            private readonly ChromatogramLoadingStatus.TransitionData _allChromData;
            private Exception _exception;

            /// <summary>
            /// The number of chromatograms the reader thread is allowed to read ahead. This number is important.
            /// If it is too small, then the spectrum reader will end up waiting for spectra to be processed in
            /// cases where a lot of precursors need to be extracted. Though, overall, spectrum reading is the
            /// slowest part of the pipeline, and processing will eventually catch up when scans are seen that
            /// require less extraction. It should also stay small enough to ensure scans in memory are never
            /// an important memory burden.
            /// </summary>
            private const int READ_BUFFER_SIZE = 100;
            private readonly BlockingCollection<SpectrumInfo> _pendingInfoList =
                new BlockingCollection<SpectrumInfo>(READ_BUFFER_SIZE);
            private SpectrumInfo _currentInfo;

            public Spectra(SrmDocument document, SpectrumFilter filter, ChromatogramLoadingStatus.TransitionData allChromData, MsDataFileImpl dataFile)
            {
                _document = document;
                _filter = filter;
                _dataFile = dataFile;

                _allChromData = allChromData;
                
                _lookaheadContext = new LookaheadContext(_filter, _dataFile);
                _countSpectra = dataFile.SpectrumCount;

                HasSrmSpectra = dataFile.HasSrmSpectra;
                
                // If possible, find the maximum retention time in order to scale the chromatogram graph.
                if (_allChromData != null && (_filter.EnabledMsMs || _filter.EnabledMs))
                {
                    var retentionTime = _dataFile.GetStartTime(_countSpectra - 1);
                    if (retentionTime.HasValue)
                    {
                        _allChromData.MaxRetentionTime = (float)retentionTime.Value;
                        _allChromData.MaxRetentionTimeKnown = true;
                        _allChromData.Progressive = true;
                    }
                }
            }

            public bool HasSrmSpectra { get; private set; }

            public bool IsRunningAsync { get {  return _runningAsync; } }

            public int CurrentIndex { get { return _currentInfo != null ? _currentInfo.Index : -1; } }

            public MsDataSpectrum CurrentSpectrum
            {
                get { return _currentInfo != null ? _currentInfo.DataSpectrum : null; }
            }

            public bool? CurrentSpectrumIsNegative
            {
                get { return _currentInfo != null ? _currentInfo.DataSpectrum.NegativeCharge : (bool?)null; }
            }

            public MsDataSpectrum[] CurrentSpectra
            {
                get { return _currentInfo != null ? _currentInfo.AllSpectra : null; }                
            }

            public float CurrentTime
            {
                get { return (float)(_currentInfo != null ? _currentInfo.RetentionTime : 0); }
            }

            public void RunAsync()
            {
                _runningAsync = true;

                ActionUtil.RunAsync(() =>
                {
                    try
                    {
                        Read();
                    }
                    catch (Exception ex)
                    {
                        SetException(ex);
                    }
                }, "Spectrum reader"); // Not L10N
            }

            /// <summary>
            /// Releases and returns the data file associated with this instance, ending any asynchronous processing
            /// </summary>
            public MsDataFileImpl Detach()
            {
                MsDataFileImpl dataFile;

                lock (_dataFileLock)
                {
                    dataFile = _dataFile;
                    _dataFile = null;
                }

                if (_runningAsync)
                {
                    // Just in case the Read thread is waiting to add a spectrum to a full pening list
                    SpectrumInfo info;
                    _pendingInfoList.TryTake(out info);
                }
                return dataFile;
            }

            /// <summary>
            /// Detaches and disposes the data file associated with this instance
            /// </summary>
            public void Dispose()
            {
                var dataFile = Detach();
                if (dataFile != null)
                    dataFile.Dispose();
            }

            public int PercentComplete
            {
                get
                {
                    // If the data file has been disposed, then count this as 100% complete
                    if (_currentInfo.IsLast)
                        return 100;
                    return Math.Max(0, CurrentIndex)*100/_countSpectra;
                }
            }

            public IDemultiplexer CreateDemultiplexer()
            {
                switch (HandlingType)
                {
                    case IsolationScheme.SpecialHandlingType.OVERLAP:
                        return new OverlapDemultiplexer(_dataFile, _filter);
                    case IsolationScheme.SpecialHandlingType.MULTIPLEXED:
                        return new MsxDemultiplexer(_dataFile, _filter);
                    case IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED:
                        return new MsxOverlapDemultiplexer(_dataFile, _filter);
                    case IsolationScheme.SpecialHandlingType.FAST_OVERLAP:
                        return new FastOverlapDemultiplexer(_dataFile);
                    default:
                        return null;
                }
            }

            private string HandlingType
            {
                get
                {
                    var isoScheme = _document.Settings.TransitionSettings.FullScan.IsolationScheme;
                    return isoScheme != null ? isoScheme.SpecialHandling : IsolationScheme.SpecialHandlingType.NONE;
                }
            }

            public string GetLog()
            {
                lock (_dataFileLock)
                {
                    return _dataFile.GetLog();
                }
            }

            private void SetException(Exception exception)
            {
                _exception = exception;
                _pendingInfoList.Add(SpectrumInfo.LAST);
            }

            public bool NextSpectrum()
            {
                if (_runningAsync)
                {
                    _currentInfo = _pendingInfoList.Take();
                    if (_exception != null)
                        Helpers.WrapAndThrowException(_exception);
                }
                else
                {
                    lock (_dataFileLock)
                    {
                        int i = _currentInfo != null ? _currentInfo.Index : -1;
                        _currentInfo = ReadSpectrum(ref i);
                    }
                }
                return !_currentInfo.IsLast;
            }

            /// <summary>
            /// Method for asynchronous reading of spectra
            /// </summary>
            private void Read()
            {
                int i = -1; // First call to ReadSpectrum will advance this to zero
                SpectrumInfo nextInfo;
                do
                {
                    lock (_dataFileLock)
                    {
                        // Check to see if disposed by another thread
                        if (_dataFile == null)
                            return;

                        nextInfo = ReadSpectrum(ref i);
                    }
                    _pendingInfoList.Add(nextInfo);
                }
                while (!nextInfo.IsLast);
            }

            private SpectrumInfo ReadSpectrum(ref int i)
            {
                while ((i = _lookaheadContext.NextIndex(i)) < _countSpectra)
                {

                    if (HasSrmSpectra)
                    {
                        var nextSpectrum = _dataFile.GetSrmSpectrum(i);
                        if (nextSpectrum.Level != 2)
                            continue;

                        if (!nextSpectrum.RetentionTime.HasValue)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SpectraChromDataProvider_SpectraChromDataProvider_Scan__0__found_without_scan_time,
                                    _dataFile.GetSpectrumId(i)));
                        }
                        var precursors = nextSpectrum.Precursors;
                        if (precursors.Length < 1 || !precursors[0].PrecursorMz.HasValue)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SpectraChromDataProvider_SpectraChromDataProvider_Scan__0__found_without_precursor_mz,
                                    _dataFile.GetSpectrumId(i)));
                        }
                        return new SpectrumInfo(i, nextSpectrum, new []{nextSpectrum},
                            (float) nextSpectrum.RetentionTime.Value);
                    }
                    else
                    {
                        // If MS/MS filtering is not enabled, skip anything that is not a MS1 scan
                        var msLevel = _lookaheadContext.GetMsLevel(i);
                        if (!_filter.EnabledMsMs && msLevel != 1)
                            continue;

                        // Skip quickly through the chromatographic lead-in and tail when possible 
                        if (msLevel > 1) // We need all MS1 for TIC and BPC
                        {
                            // Only do these checks if we can get the information instantly. Otherwise,
                            // this will slow down processing in more complex cases.
                            var timeAndPrecursors = _lookaheadContext.GetInstantTimeAndPrecursors(i);
                            double? rtCheck = timeAndPrecursors.RetentionTime;
                            if (_filter.IsOutsideRetentionTimeRange(rtCheck))
                            {
                                // Leave an update cue for the chromatogram painter then move on
                                if (_allChromData != null)
                                    _allChromData.CurrentTime = (float)rtCheck.Value;
                                continue;
                            }

                            var precursors = timeAndPrecursors.Precursors;
                            if (precursors.Any() && !_filter.HasProductFilterPairs(rtCheck, precursors))
                            {
                                continue;
                            }
                        }

                        // Inexpensive checks are complete, now actually get the spectrum data
                        var nextSpectrum = _lookaheadContext.GetSpectrum(i);
                        // Assertion for testing ID to spectrum index support
                        //                        int iFromId = dataFile.GetSpectrumIndex(dataSpectrum.Id);
                        //                        Assume.IsTrue(i == iFromId);
                        if (nextSpectrum.Mzs.Length == 0)
                            continue;

                        double? rt = nextSpectrum.RetentionTime;
                        if (!rt.HasValue)
                            continue;

                        // For Waters msE skip any lockspray data
                        if (_filter.IsWatersMse)
                        {
                            // looking for the 3 in 3.0.1 (or the 10 in 10.0.1)
                            if (nextSpectrum.WatersFunctionNumber > 2)
                                continue;
                        }
                        else if (_filter.IsWatersFile)
                        {
                            // looking for the 3 in id string 3.0.1 (or the 10 in 10.0.1)
                            if ( _dataFile.IsWatersLockmassSpectrum(nextSpectrum))
                                continue;
                        }

                        // Deal with ion mobility data - look ahead for a run of scans all 
                        // with the same retention time.  For non-IMS data we'll just get
                        // a single "ion mobility bin" with no ion mobility value.
                        //
                        // Also for Agilent ramped-CE msE, gather MS2 scans together
                        // so they get averaged.
                        //

                        var nextSpectra = _lookaheadContext.Lookahead(nextSpectrum, out rt);
                        if (!_filter.ContainsTime(rt.Value))
                        {
                            if (_allChromData != null)
                                _allChromData.CurrentTime = (float)rt.Value;
                            continue;
                        }

                        return new SpectrumInfo(i, nextSpectrum, nextSpectra, rt.Value);
                    }
                }
                return SpectrumInfo.LAST;
            }

            private class SpectrumInfo
            {
                public static readonly SpectrumInfo LAST = new SpectrumInfo(-1, null, null, 0);

                public SpectrumInfo(int index, MsDataSpectrum dataSpectrum, MsDataSpectrum[] allSpectra, double retentionTime)
                {
                    Index = index;
                    DataSpectrum = dataSpectrum;
                    AllSpectra = allSpectra;
                    RetentionTime = retentionTime;
                }

                public int Index { get; private set; }
                public MsDataSpectrum DataSpectrum { get; private set; }
                public MsDataSpectrum[] AllSpectra { get; private set; }
                public double RetentionTime { get; private set; }

                public bool IsLast { get { return DataSpectrum == null; } }
            }
        }

        /// <summary>
        /// Manage collectors for chromatogram storage and retrieval, possibly on different threads.
        /// </summary>
        public class Collectors
        {
            private readonly IList<ChromCollector> _collectors;
            private readonly int[] _chromKeyLookup;
            private float _retentionTime;
            private Exception _exception;

            public Collectors()
            {
                ChromKeys = new List<ChromKey>();
                _collectors = new List<ChromCollector>();
            }

            public Collectors(ICollection<ChromKey> chromKeys, bool runningAsync)
            {
                IsRunningAsync = runningAsync;

                // Sort ChromKeys in order of max retention time, and note the sort order.
                var chromKeyArray = chromKeys.ToArray();
                if (chromKeyArray.Length > 1)
                {
                    var lastMaxTime = chromKeyArray[0].OptionalMaxTime ?? float.MaxValue;
                    for (int i = 1; i < chromKeyArray.Length; i++)
                    {
                        var maxTime = chromKeyArray[i].OptionalMaxTime ?? float.MaxValue;
                        if (maxTime < lastMaxTime)
                        {
                            int[] sortIndexes;
                            ArrayUtil.Sort(chromKeyArray, out sortIndexes);
                            // The sort indexes tell us where the keys used to live. For lookup, we need
                            // to go the other way. Chromatograms will come in indexed by where they used to
                            // be, and we need to put them into the _chromList array in the new location of
                            // the ChromKey.
                            _chromKeyLookup = new int[sortIndexes.Length];
                            for (int j = 0; j < sortIndexes.Length; j++)
                                _chromKeyLookup[sortIndexes[j]] = j;
                            break;
                        }
                        lastMaxTime = maxTime;
                    }
                }
                ChromKeys = chromKeyArray;

                // Create empty chromatograms for each ChromKey.
                _collectors = new ChromCollector[chromKeys.Count];
            }

            public bool IsRunningAsync { get; private set; }

            public IList<ChromKey> ChromKeys { get; private set; }

            public int Count { get { return ChromKeys.Count; } }

            /// <summary>
            /// Add key and collector for an SRM chromatogram.
            /// </summary>
            public void AddSrmCollector(ChromKey chromKey, ChromCollector collector)
            {
                // Not allowed to use this method in the async case
                Assume.IsFalse(IsRunningAsync);

                ChromKeys.Add(chromKey);
                // ReSharper disable once InconsistentlySynchronizedField
                _collectors.Add(collector);
            }

            /// <summary>
            /// Add a collector for a non-SRM chromatogram.
            /// </summary>
            public void AddCollector(int productFilterId, ChromCollector collector)
            {
                lock (this)
                {
                    int index = ProductFilterIdToId(productFilterId);
                    _collectors[index] = collector;
                }
            }

            public int ProductFilterIdToId(int productFilterId)
            {
                return _chromKeyLookup == null ? productFilterId : _chromKeyLookup[productFilterId];
            }

            public void ExtractionComplete()
            {
                lock (this)
                {
                    _retentionTime = float.MaxValue;
                    Monitor.PulseAll(this);
                }
            }

            public void AddComplete(float retentionTime)
            {
                lock (this)
                {
                    _retentionTime = retentionTime;
                    Monitor.PulseAll(this);
                }
            }

            /// <summary>
            /// Called from the reader thread to get a chromatogram. May have to wait until the chromatogram
            /// extraction thread has completed the requested chromatogram.
            /// </summary>
            public int ReleaseChromatogram(
                int chromatogramIndex,
                ChromGroups chromGroups,
                out TimeIntensities timeIntensities)
            {
                lock (this)
                {
                    while (_exception == null)
                    {
                        // Copy chromatogram data to output arrays and release memory.
                        var collector = _collectors[chromatogramIndex];
                        int status;
                        if (chromGroups != null)
                        {
                            status = chromGroups.ReleaseChromatogram(chromatogramIndex, _retentionTime, collector,
                                out timeIntensities);
                        }
                        else
                        {
                            collector.ReleaseChromatogram(null, out timeIntensities);
                            status = collector.StatusId;
                        }
                        if (status >= 0)
                        {
                            _collectors[chromatogramIndex] = null;
                            return status;
                        }
                        Monitor.Wait(this);
                    }
                }

                // Propagate exception from provider thread.
                Helpers.WrapAndThrowException(_exception);
                throw _exception;   // Unreachable code, but keeps compiler happy
            }

            public void SetException(Exception exception)
            {
                if (!IsRunningAsync)
                    throw exception;

                _exception = exception;
                ExtractionComplete();
            }
        }

        /// <summary>
        /// Helper class for lookahead necessary for ion mobility and 
        /// Agilent Mse data
        /// </summary>
        private struct LookaheadContext
        {
            public LookaheadContext(SpectrumFilter filter, MsDataFileImpl dataFile)
            {
                _lookAheadIndex = 0;
                _lookAheadDataSpectrum = null;
                _filter = filter;
                _dataFile = dataFile;
                _rt = null;
                _previousIonMobilityValue = IonMobilityValue.EMPTY;
                _lenSpectra = dataFile.SpectrumCount;
            }

            private int _lookAheadIndex;
            private double? _rt;
            private readonly SpectrumFilter _filter;
            private readonly MsDataFileImpl _dataFile;
            private MsDataSpectrum _lookAheadDataSpectrum; // Result of _datafile.GetSpectrum(_lookaheadIndex), or null
            private readonly int _lenSpectra;
            private IonMobilityValue _previousIonMobilityValue;

            public int GetMsLevel(int index)
            {
                if (index == _lookAheadIndex && _lookAheadDataSpectrum != null)
                    return _lookAheadDataSpectrum.Level;
                else
                    return _dataFile.GetMsLevel(index);
            }

            public IonMobilityValue GetIonMobility(int index)
            {
                if (index == _lookAheadIndex && _lookAheadDataSpectrum != null)
                    return _lookAheadDataSpectrum.IonMobility;
                else
                    return _dataFile.GetIonMobility(index);
            }

            public double? GetRetentionTime(int index)
            {
                if (index == _lookAheadIndex && _lookAheadDataSpectrum != null)
                    return _lookAheadDataSpectrum.RetentionTime;
                else
                    return _dataFile.GetStartTime(index);  // Returns 0 if retrieval is too expensive
            }

            public MsTimeAndPrecursors GetInstantTimeAndPrecursors(int index)
            {
                if (index == _lookAheadIndex && _lookAheadDataSpectrum != null)
                    return new MsTimeAndPrecursors
                    {
                        Precursors = _lookAheadDataSpectrum.Precursors,
                        RetentionTime = _lookAheadDataSpectrum.RetentionTime
                    };
                else
                    return _dataFile.GetInstantTimeAndPrecursors(index);
            }

            public MsDataSpectrum GetSpectrum(int index)
            {
                if (index == _lookAheadIndex)
                {
                    return _lookAheadDataSpectrum ?? (_lookAheadDataSpectrum = _dataFile.GetSpectrum(index));
                }
                else
                {
                    return _dataFile.GetSpectrum(index);
                }
            }

            public int NextIndex(int proposed)
            {
                if (_lookAheadIndex <= proposed)
                {
                    _lookAheadIndex = proposed + 1;
                    _lookAheadDataSpectrum = null;
                }
                return _lookAheadIndex;
            }

            private bool NextSpectrumIsIonMobilityScanForCurrentRetentionTime(MsDataSpectrum nextSpectrum)
            {
                bool result = ((_rt ?? 0) == (nextSpectrum.RetentionTime ?? -1)) &&
                              IonMobilityValue.IsExpectedValueOrdering(_previousIonMobilityValue, nextSpectrum.IonMobility);
                _previousIonMobilityValue = nextSpectrum.IonMobility;
                return result;
            }

            private bool NextSpectrumIsAgilentMse(MsDataSpectrum nextSpectrum, int listLevel, double startCE)
            {
                // Average runs of MS/MS scans until the start CE is seen again
                return (_filter.IsAgilentMse &&
                    listLevel == 2 &&
                    nextSpectrum.Level == 2 &&
                    startCE != GetPrecursorCollisionEnergy(nextSpectrum));
            }

            // Deal with ion mobility data - look ahead for a run of scans all
            // with the same retention time.  For non-IMS data we'll just get
            // a single "ion mobility bin" with no ion mobility value.
            //
            // Also for Agilent ramped-CE msE, gather MS2 scans together
            // so they get averaged.
            public MsDataSpectrum[] Lookahead(MsDataSpectrum dataSpectrum, out double? rt)
            {
                var spectrumList = new List<MsDataSpectrum>();
                int listLevel = dataSpectrum.Level;
                double startCE = GetPrecursorCollisionEnergy(dataSpectrum);
                _previousIonMobilityValue = IonMobilityValue.EMPTY;
                double rtTotal = 0;
                double? rtFirst = null;
                _lookAheadDataSpectrum = null;
                while (_lookAheadIndex++ < _lenSpectra)
                {
                    _rt = dataSpectrum.RetentionTime;
                    if (_rt.HasValue && dataSpectrum.Mzs.Length != 0)
                    {
                        spectrumList.Add(dataSpectrum);
                        rtTotal += dataSpectrum.RetentionTime.Value;
                        if (!rtFirst.HasValue)
                            rtFirst = dataSpectrum.RetentionTime;
                    }
                    if (!_filter.IsAgilentMse && !dataSpectrum.IonMobility.HasValue)
                        break;

                    if (_lookAheadIndex < _lenSpectra)
                    {
                        dataSpectrum = _lookAheadDataSpectrum = _dataFile.GetSpectrum(_lookAheadIndex);
                        // Reasons to keep adding to the list:
                        //   Retention time hasn't changed but ion mobility has changed, or
                        //   Agilent ramped-CE data - MS2 scans get averaged
                        if (!(NextSpectrumIsIonMobilityScanForCurrentRetentionTime(dataSpectrum) ||
                              NextSpectrumIsAgilentMse(dataSpectrum, listLevel, startCE)))
                            break;
                    }
                }
                if (spectrumList.Any()) // Should have at least one non-empty scan at this ion mobility
                    _rt = _filter.IsAgilentMse ? (rtTotal / spectrumList.Count()) : rtFirst;
                else
                    _rt = null;
                rt = _rt;
                return spectrumList.ToArray();
            }

            private static double GetPrecursorCollisionEnergy(MsDataSpectrum dataSpectrum)
            {
                return dataSpectrum.Precursors.Length > 0
                    ? dataSpectrum.Precursors[0].PrecursorCollisionEnergy ?? 0
                    : 0;
            }
        }
    }

    public class DataFileInstrumentInfo : IFilterInstrumentInfo
    {
        private readonly MsDataFileImpl _dataFile;

        public DataFileInstrumentInfo(MsDataFileImpl dataFile)
        {
            _dataFile = dataFile;
        }

        public bool IsWatersFile { get { return _dataFile.IsWatersFile; } }

        public bool IsAgilentFile { get { return _dataFile.IsAgilentFile; } }

        public bool ProvidesCollisionalCrossSectionConverter { get { return _dataFile.ProvidesCollisionalCrossSectionConverter; } }
        public MsDataFileImpl.eIonMobilityUnits IonMobilityUnits { get { return _dataFile.IonMobilityUnits; } }

        public IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge)
        {
            return _dataFile.IonMobilityFromCCS(ccs, mz, charge);
        }
        public double CCSFromIonMobility(IonMobilityValue im, double mz, int charge)
        {
            return _dataFile.CCSFromIonMobilityValue(im, mz, charge);
        }
    }
    internal enum TimeSharing { single, shared, grouped }

    internal sealed class ChromDataCollectorSet
    {
        public ChromDataCollectorSet(ChromSource chromSource, TimeSharing timeSharing,
                                     ChromatogramLoadingStatus.TransitionData allChromData, BlockWriter blockWriter)
        {
            ChromSource = chromSource;
            TypeOfScans = timeSharing;
            PrecursorCollectorMap = new List<Tuple<PrecursorTextId, ChromDataCollector>>();
            if (timeSharing == TimeSharing.shared)
            {
                SharedTimesCollector = new SortedBlockedList<float>();
                ScanIdsCollector = new BlockedList<int>();
            }
            _allChromData = allChromData;
            _blockWriter = blockWriter;
        }

        public ChromSource ChromSource { get; private set; }

        private TimeSharing TypeOfScans { get; set; }
        private readonly ChromatogramLoadingStatus.TransitionData _allChromData;
        private readonly BlockWriter _blockWriter;

        public bool IsSingleTime { get { return TypeOfScans == TimeSharing.single; } }
        public bool IsGroupedTime { get { return TypeOfScans == TimeSharing.grouped; } }
        public bool IsSharedTime { get { return TypeOfScans == TimeSharing.shared; } }

        public SortedBlockedList<float> SharedTimesCollector { get; private set; }
        public BlockedList<int> ScanIdsCollector { get; private set; }

        public void AddSharedTime(float time, int scanId)
        {
            lock (_blockWriter)
            {
                SharedTimesCollector.AddShared(time);
                ScanIdsCollector.AddShared(scanId);
            }
        }

        public IList<Tuple<PrecursorTextId, ChromDataCollector>> PrecursorCollectorMap { get; private set; }

        public int Count { get { return PrecursorCollectorMap.Count; } }

        public void ProcessExtractedSpectrum(float time, SpectraChromDataProvider.Collectors chromatograms, int scanId, ExtractedSpectrum spectrum, Action<int, ChromCollector> addCollector)
        {
            var precursorMz = spectrum.PrecursorMz;
            var ionMobility = spectrum.IonMobility;
            var target = spectrum.Target;
            ChromExtractor extractor = spectrum.Extractor;
            int ionScanCount = spectrum.ProductFilters.Length;
            ChromDataCollector collector;
            var key = new PrecursorTextId(precursorMz, target, extractor);
            int index = spectrum.FilterIndex;
            while (PrecursorCollectorMap.Count <= index)
                PrecursorCollectorMap.Add(null);
            if (PrecursorCollectorMap[index] != null)
                collector = PrecursorCollectorMap[index].Item2;
            else
            {
                collector = new ChromDataCollector(target, precursorMz, ionMobility, index, IsGroupedTime);
                PrecursorCollectorMap[index] = new Tuple<PrecursorTextId, ChromDataCollector>(key, collector);
            }

            int ionCount = collector.ProductIntensityMap.Count;
            if (ionCount == 0)
                ionCount = ionScanCount;

            // Add new time to the shared time list if not SRM, which doesn't share times, or
            // the times are shared with the entire set, as in MS1
            int lenTimes = collector.TimeCount;
            if (IsGroupedTime)
            {
                // Shared scan ids and times do not belong to a group.
                collector.AddScanId(scanId);
                collector.AddGroupedTime(time);
                lenTimes = collector.GroupedTimesCollector.Count;
            }

            // Add intensity values to ion scans

            for (int j = 0; j < ionScanCount; j++)
            {
                var productFilter = spectrum.ProductFilters[j];
                var chromIndex = chromatograms.ProductFilterIdToId(productFilter.FilterId);

                ChromCollector chromCollector;
                if (!collector.ProductIntensityMap.TryGetValue(productFilter, out chromCollector))
                {
                    chromCollector = new ChromCollector(chromIndex, IsSingleTime, spectrum.MassErrors != null);
                    // If more than a single ion scan, add any zeros necessary
                    // to make this new chromatogram have an entry for each time.
                    if (ionScanCount > 1 && lenTimes > 1)
                    {
                        chromCollector.FillZeroes(chromIndex, lenTimes - 1, _blockWriter);
                    }
                    collector.ProductIntensityMap.Add(productFilter, chromCollector);

                    if (addCollector != null)
                        addCollector(productFilter.FilterId, chromCollector);
                }
                if (IsSingleTime)
                    chromCollector.AddTime(chromIndex, time, _blockWriter);
                chromCollector.AddPoint(chromIndex, 
                    spectrum.Intensities[j],
                    spectrum.MassErrors != null ? spectrum.MassErrors[j] : (float?)null, 
                    _blockWriter);
            }

            // Add data for chromatogram graph.
            if (_allChromData != null && spectrum.PrecursorMz != 0) // Exclude TIC and BPC
                _allChromData.Add(spectrum.Target, spectrum.PeptideColor, spectrum.FilterIndex, time, spectrum.Intensities);

            // If this was a multiple ion scan and not all ions had measurements,
            // make sure missing ions have zero intensities in the chromatogram.
            if (ionScanCount > 1 &&
                (ionCount != ionScanCount || ionCount != collector.ProductIntensityMap.Count))
            {
                // Times should have gotten one longer
                foreach (var item in collector.ProductIntensityMap)
                {
                    var productFilter = item.Key;
                    var chromCollector = item.Value;
                    var chromIndex = chromatograms.ProductFilterIdToId(productFilter.FilterId);
                    if (chromCollector.Count < lenTimes)
                    {
                        chromCollector.AddPoint(chromIndex, 0, 0, _blockWriter);
                    }
                }
            }
        }
    }

    internal sealed class ChromDataCollector
    {
        public ChromDataCollector(Target modifiedSequence, SignedMz precursorMz, IonMobilityFilter ionMobility, int statusId, bool isGroupedTime)
        {
            ModifiedSequence = modifiedSequence;
            PrecursorMz = precursorMz;
            IonMobility = ionMobility;
            StatusId = statusId;
            ProductIntensityMap = new Dictionary<SpectrumProductFilter, ChromCollector>();
            if (isGroupedTime)
            {
                GroupedTimesCollector = new SortedBlockedList<float>();
                ScansCollector = new BlockedList<int>();
            }
        }

        public Target ModifiedSequence { get; private set; }
        public SignedMz PrecursorMz { get; private set; }
        public IonMobilityFilter IonMobility { get; private set; }
        public int StatusId { get; private set; }
        public Dictionary<SpectrumProductFilter, ChromCollector> ProductIntensityMap { get; private set; }
        public readonly SortedBlockedList<float> GroupedTimesCollector;
        public readonly BlockedList<int> ScansCollector;

        public void AddGroupedTime(float time)
        {
            GroupedTimesCollector.AddShared(time);
        }

        public void AddScanId(int scanId)
        {
            ScansCollector.AddShared(scanId);
        }

        public int TimeCount
        {
            get
            {
                // Return the length of any existing time list (in case there are no shared times)
                foreach (var tis in ProductIntensityMap.Values)
                    return tis.Count;
                return 0;
            }
        }
    }
}