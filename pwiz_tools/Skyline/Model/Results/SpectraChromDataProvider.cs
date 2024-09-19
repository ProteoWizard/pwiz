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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class SpectraChromDataProvider : ChromDataProvider
    {
        private readonly string _cachePath;
        private Collectors _collectors;
        private Spectra _spectra;
        private GlobalChromatogramExtractor _globalChromatogramExtractor;
        private IDemultiplexer _demultiplexer;
        private readonly IRetentionTimePredictor _retentionTimePredictor;
        private List<SpectrumMetadata> _spectrumMetadatas = new List<SpectrumMetadata>();
        private Dictionary<string, int> _scanIdDictionary = new Dictionary<string, int>();
        private readonly bool _isProcessedScans;
        private double? _maxIonMobilityValue;
        private bool _isSingleMzMatch;
        private bool _sourceHasPositivePolarityData;
        private bool _sourceHasNegativePolarityData;
        private Predicate<SpectrumMetadata> _globalSpectrumClassFilter;
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

        private readonly OptimizableRegression _optimization;

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
            _globalChromatogramExtractor = new GlobalChromatogramExtractor(dataFile);
            if (_document.Settings.TransitionSettings.FullScan.IsEnabledMs 
                && !_globalChromatogramExtractor.IsTicChromatogramUsable())
            {
                _globalChromatogramExtractor.TicChromatogramIndex = null;
            }

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

            _optimization = _document.Settings.MeasuredResults.Chromatograms
                .FirstOrDefault(chromSet => chromSet.ContainsFile(fileInfo.FilePath))?.OptimizationFunction;

            UpdatePercentComplete();

            if (NeedMaxIonMobilityValue(dataFile))
                _maxIonMobilityValue = dataFile.GetMaxIonMobility();
            _globalSpectrumClassFilter = _document.Settings.TransitionSettings.FullScan.SpectrumClassFilter.MakePredicate();

            // Create the filter responsible for chromatogram extraction
            bool firstPass = (_retentionTimePredictor != null);
            _filter = new SpectrumFilter(_document, FileInfo.FilePath, new DataFileInstrumentInfo(dataFile),
                _optimization, _maxIonMobilityValue, _retentionTimePredictor, firstPass, _globalChromatogramExtractor);

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
                // If exception thrown before construction is complete then Dispose will not be called.
                if (_spectra == null)
                    dataFile.Dispose();
                else
                    _spectra.Dispose();

                throw;
            }
        }

        private bool NeedMaxIonMobilityValue(MsDataFileImpl dataFile)
        {
            // Only need to find the IM range if filter window width calculation mode is linear
            var settings = _document.Settings.TransitionSettings.IonMobilityFiltering;

            if (settings == null || settings.IsEmpty)
                return false;

            if (dataFile.IsWatersSonarData())
                return false;

            if (settings.FilterWindowWidthCalculator?.WindowWidthMode != IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.linear_range)
                return false; // Only the linear_range option needs to discover the IM range

            // This is the expensive part - check if there are any ion mobilities in the libraries that will need windows
            // TODO (bspratt): Use a quicker check for any ion mobility for a file - this especially slow with big DDA libraries used in DIA where the library may be composed of 40 files none of them this one
            // Though this is rarely used - the linear width option is really only used in Waters SONAR data
            return _document.Settings.GetIonMobilities(_document.MoleculeLibKeys.ToArray(), new MsDataFilePath(dataFile.FilePath)) != null;
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
            _filter = new SpectrumFilter(_document, FileInfo.FilePath, _filter, _optimization, _maxIonMobilityValue,
                _retentionTimePredictor, false, _globalChromatogramExtractor);
            _spectra = null;
            _isSrm = false;

            InitSpectrumReader(dataFile);
            InitChromatogramExtraction();
            return true;
        }

        public override eIonMobilityUnits IonMobilityUnits { get { return _filter.IonMobilityUnits; } }

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
            var chromMapMs1Pos = new ChromDataCollectorSet(ChromSource.ms1, ms1TimeSharing, _allChromData, _blockWriter);
            var chromMapMs1Neg = new ChromDataCollectorSet(ChromSource.ms1, ms1TimeSharing, _allChromData, _blockWriter);
            var chromMapSim = new ChromDataCollectorSet(ChromSource.sim, TimeSharing.grouped, _allChromData, _blockWriter);
            var chromMapGlobal = new ChromDataCollectorSet(ChromSource.unknown, TimeSharing.single, _allChromData, _blockWriter);
            var chromMaps = new[] {chromMap, chromMapSim, chromMapMs1Pos, chromMapMs1Neg, chromMapGlobal};

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
                var dataSpectrum = _spectra.CurrentSpectrum;
                if (!_globalSpectrumClassFilter(dataSpectrum.Metadata))
                {
                    continue;
                }
                if (_spectra.HasSrmSpectra)
                {

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
                        ChromatogramGroupId.ForPeptide(peptideNode, null),
                        peptideNode != null ? peptideNode.Color : PeptideDocNode.UNKNOWN_COLOR,
                        precursorMz,
                        filterIndex,
                        dataSpectrum.Mzs,
                        dataSpectrum.Intensities,
                        chromMap);
                }
                else if (_filter.EnabledMsMs || _filter.EnabledMs)
                {
                    var spectra = _spectra.CurrentSpectra;

                    // FAIMS chromatogram extraction is a special case for non-contiguous scans
                    // Ignore this spectrum if FAIMS CV is not interesting
                    if (!_filter.PassesFilterFAIMS(dataSpectrum))
                    {
                        continue;
                    }

                    float rt = _spectra.CurrentTime;
                    if (_allChromData != null)
                        _allChromData.CurrentTime = rt;

                    if (_filter.IsMsSpectrum(dataSpectrum))
                    {
                        ChromDataCollectorSet chromMapMs;
                        if (_filter.IsSimSpectrum(dataSpectrum, spectra))
                        {
                            chromMapMs = chromMapSim;
                        }
                        else
                        {
                            chromMapMs = dataSpectrum.NegativeCharge ? chromMapMs1Neg : chromMapMs1Pos;
                        }
                        string scanId = dataSpectrum.Id;

                        // Process all SRM spectra that can be generated by filtering this full-scan MS1
                        if (chromMapMs.IsSharedTime)
                        {
                            chromMapMs.AddSharedTime(rt, GetScanIdIndex(dataSpectrum.Metadata));
                        }
                        lock (_blockWriter)
                        {
                            foreach (var spectrum in _filter.SrmSpectraFromMs1Scan(rt, dataSpectrum.Precursors, spectra))
                            {
                                chromMapMs.ProcessExtractedSpectrum(rt, _collectors, GetScanIdIndex(dataSpectrum.Metadata), spectrum, AddChromCollector);
                            }
                        }
                    }
                    else if (_filter.IsMsMsSpectrum(dataSpectrum))
                    {
                        // Process all SRM spectra that can be generated by filtering this full-scan MS/MS
                        if (_demultiplexer == null)
                        {
                            ProcessSpectrumList(spectra, chromMap, rt, _filter, dataSpectrum.Metadata);
                        }
                        else
                        {
                            int i = _spectra.CurrentIndex;
                            foreach (var deconvSpectrum in _demultiplexer.GetDeconvolvedSpectra(i, dataSpectrum))
                            {
                                ProcessSpectrumList(new[] {deconvSpectrum}, chromMap, rt, _filter, dataSpectrum.Metadata);
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

        private int GetScanIdIndex(SpectrumMetadata spectrumMetadata)
        {
            if (_scanIdDictionary.TryGetValue(spectrumMetadata.Id, out int scanIndex))
            {
                return scanIndex;
            }
            scanIndex = _spectrumMetadatas.Count;
            _spectrumMetadatas.Add(spectrumMetadata);
            _scanIdDictionary.Add(spectrumMetadata.Id, scanIndex);
            return scanIndex;
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
                        collector.ChromatogramGroupId,
                        collector.PrecursorMz,
                        collector.IonMobility,
                        pairProduct.Key.TargetMz,
                        0,
                        0,
                        pairProduct.Key.FilterWidth,
                        chromMap.ChromSource,
                        modSeq.Extractor);

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
                                     SpectrumMetadata spectrumMetadata)
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

                    chromMap.ProcessExtractedSpectrum(rt, _collectors, GetScanIdIndex(spectrumMetadata), spectrum, AddChromCollector);
                }
            }
        }

        private void ProcessSrmSpectrum(float time,
                                               ChromatogramGroupId chromatogramGroupId,
                                               Color peptideColor,
                                               SignedMz precursorMz,
                                               int filterIndex,
                                               double[] mzs,
                                               double[] intensities,
                                               ChromDataCollectorSet chromMap)
        {
            float[] intensityFloats = new float[intensities.Length];
            for (int i = 0; i < intensities.Length; i++)
                intensityFloats[i] = (float) intensities[i];
            var productFilters = mzs.Select(mz => new SpectrumProductFilter(new SignedMz(mz, precursorMz.IsNegative), 0, 0)).ToArray();
            var spectrum = new ExtractedSpectrum(chromatogramGroupId, peptideColor, precursorMz,
            IonMobilityFilter.EMPTY, // ion mobility unknown
                ChromExtractor.summed, filterIndex, productFilters, intensityFloats, null);
            chromMap.ProcessExtractedSpectrum(time, _collectors, -1, spectrum, null);
        }

        public override IEnumerable<ChromKeyProviderIdPair> ChromIds
        {
            get
            {
                var chromIds = new List<ChromKeyProviderIdPair>(_collectors.ChromKeys.Count);
                for (int i = 0; i < _collectors.ChromKeys.Count; i++)
                    chromIds.Add(new ChromKeyProviderIdPair(_collectors.ChromKeys[i], i));
                VerifyGlobalChromatograms(chromIds);
                return chromIds;
            }
        }

        private void VerifyGlobalChromatograms(IList<ChromKeyProviderIdPair> chromIds)
        {
            var globalChromKeys = _globalChromatogramExtractor.ListChromKeys();
            int indexFirstGlobalChromatogram = chromIds.Count - globalChromKeys.Count;
            for (int relativeIndex = 0; relativeIndex < globalChromKeys.Count; relativeIndex++)
            {
                int absoluteIndex = indexFirstGlobalChromatogram + relativeIndex;
                var expectedChromKey = globalChromKeys[relativeIndex];
                var actualChromKey = chromIds[absoluteIndex].Key;
                if (!Equals(expectedChromKey, actualChromKey))
                {
                    var message = string.Format(@"ChromKey mismatch at position {0}. Expected: {1} Actual: {2}",
                        absoluteIndex, expectedChromKey, actualChromKey);
                    Assume.Fail(message);
                }
            }
        }

        public override IResultFileMetadata ResultFileData
        {
            get { return new ResultFileMetaData(_spectrumMetadatas); }
        }

        public override void SetRequestOrder(IList<IList<int>> chromatogramRequestOrder)
        {
            if (_isSrm)
                return;

            if (_chromGroups != null)
                _chromGroups.Dispose();

            _chromGroups = new ChromGroups(chromatogramRequestOrder, _collectors.ChromKeys, (float) MaxRetentionTime.GetValueOrDefault(), _spectra.CycleCount, _cachePath);
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
                }, @"Chromatogram extractor");
            }
        }

        public override bool GetChromatogram(int id, ChromatogramGroupId chromatogramGroupId, Color peptideColor, out ChromExtra extra, out TimeIntensities timeIntensities)
        {
            var chromKey = _collectors.ChromKeys.Count > id ? _collectors.ChromKeys[id] : null;
            timeIntensities = null;
            extra = null;
            if (SignedMz.ZERO.Equals(chromKey?.Precursor ?? SignedMz.ZERO))
            {
                int indexFirstGlobalChromatogram =
                    _collectors.ChromKeys.Count - _globalChromatogramExtractor.ChromatogramCount;
                int indexInGlobalChromatogramExtractor = id - indexFirstGlobalChromatogram;
                if (_globalChromatogramExtractor.GetChromatogramAt(indexInGlobalChromatogramExtractor, out float[] times, out float[] intensities))
                {
                    timeIntensities = new TimeIntensities(times, intensities, null, null);
                    extra = new ChromExtra(0, 0);
                }
            }
            if (null == timeIntensities)
            {
                var statusId = _collectors.ReleaseChromatogram(id, _chromGroups, out timeIntensities);
                extra = new ChromExtra(statusId, 0);
                // Each chromatogram will be read only once!
                _readChromatograms++;
            }

            if (null != chromKey && SignedMz.ZERO.Equals(chromKey.Precursor) &&
                ChromExtractor.summed == chromKey.Extractor && timeIntensities.NumPoints > 0)
            {
                _ticArea = timeIntensities.Integral(0, timeIntensities.NumPoints - 1);
            }

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
                _spectrumMetadatas = null;
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
            private readonly int _countCycles;
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
            private const int MAX_QUEUE_MEMORY = 200 * 1024 * 1024; // 200 MB
            private readonly MemoryBlockingCollection<SpectrumInfo> _pendingInfoList =
                new MemoryBlockingCollection<SpectrumInfo>(MAX_QUEUE_MEMORY, READ_BUFFER_SIZE);

            private const int SORT_THREAD_COUNT = 4;
            private QueueWorker<SpectrumInfo> _unprocessedInfoList;
            private SpectrumInfo _currentInfo;

            public Spectra(SrmDocument document, SpectrumFilter filter, ChromatogramLoadingStatus.TransitionData allChromData, MsDataFileImpl dataFile)
            {
                _document = document;
                _filter = filter;
                _dataFile = dataFile;

                _allChromData = allChromData;
                
                _lookaheadContext = new LookaheadContext(_filter, _dataFile);
                _countSpectra = dataFile.SpectrumCount;

                // Initially use the number of spectra as the estimate of the number of points that chromatograms will have.
                _countCycles = _countSpectra;
                try
                {
                    double[] tic = dataFile.GetTotalIonCurrent();
                    if (tic != null && tic.Length > 0)
                    {
                        // The TIC's length is equal to the number of MS1 spectra in the data file.
                        // It is a better estimate of extracted chromatogram length
                        // because spectrum count can be massive for data files with IMS
                        _countCycles = tic.Length;
                    }
                }
                catch (Exception)
                {
                    // Ignore and use _countSpectra
                }

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
                }, @"Spectrum reader");
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
                    // Just in case the Read thread is waiting to add a spectrum to a full pending list
                    _pendingInfoList.TryTake(out _);
                }
                return dataFile;
            }

            /// <summary>
            /// Detaches and disposes the data file associated with this instance
            /// </summary>
            public void Dispose()
            {
                var dataFile = Detach();
                dataFile?.Dispose();
                _unprocessedInfoList?.Dispose();
                _pendingInfoList?.Dispose();
                _currentInfo?.Dispose();
            }

            public int PercentComplete
            {
                get
                {
                    // If the data file has been disposed, then count this as 100% complete
                    if (_currentInfo == null || _currentInfo.IsLast)
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
                if (_unprocessedInfoList != null)
                    _unprocessedInfoList.DoneAdding();
                _pendingInfoList.Add(SpectrumInfo.LAST);
            }

            public bool NextSpectrum()
            {
                var lastInfo = _currentInfo;
                try
                {
                    if (_runningAsync)
                    {
                        _currentInfo = _pendingInfoList.Take();
                        _currentInfo.SortEvent?.WaitOne();   // Until sorted
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
                }
                finally
                {
                    if (!ReferenceEquals(_currentInfo, lastInfo))
                        lastInfo?.Dispose();
                }
                return !_currentInfo.IsLast;
            }

            public int SpectrumCount { get { return _countSpectra; } }
            public int CycleCount { get { return _countCycles; } }

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

                    EnsureSortedMzs(nextInfo);

                    _pendingInfoList.Add(nextInfo);
                }
                while (!nextInfo.IsLast);

                _unprocessedInfoList?.DoneAdding(true);
            }

            private void EnsureSortedMzs(SpectrumInfo nextInfo)
            {
                // If the spectrum contains an IMS array, then it needs to be ordered
                // Once the sorter thread is created, all spectra must go through it
                if (nextInfo.DataSpectrum?.IonMobilities != null)
                {
                    // If not running async just sort on the current thread
                    if (!_runningAsync)
                        SortSpectrum(nextInfo, 0);
                    else
                    {
                        // Defer starting the extra thread until the first case is seen
                        if (_unprocessedInfoList == null)
                        {
                            // Don't let unprocessed spectra get too far ahead, because sorting will
                            // take a relatively consistent amount of time. So, if it takes longer than
                            // retrieval, then this queue will just get backed up. Using more than a 
                            // single thread helps to keep this O(n*log(n)) processing from becoming a
                            // bottleneck
                            _unprocessedInfoList = new QueueWorker<SpectrumInfo>(null, SortSpectrum);
                            _unprocessedInfoList.RunAsync(SORT_THREAD_COUNT, @"Spectrum sorter");
                        }

                        _unprocessedInfoList.Add(nextInfo);
                    }
                }
            }

            private void SortSpectrum(SpectrumInfo spectrumInfo, int i)
            {
                var spectrum = spectrumInfo.DataSpectrum;
                ArrayUtil.Sort(spectrum.Mzs, spectrum.Intensities, spectrum.IonMobilities);
                spectrumInfo.SortEvent.Set();
            }

            private SpectrumInfo ReadSpectrum(ref int i)
            {
                while ((i = _lookaheadContext.NextIndex(i)) < _countSpectra)
                {
                    try
                    {
                        if (HasSrmSpectra)
                        {
                            var nextSpectrum = _dataFile.GetSrmSpectrum(i);
                            if (nextSpectrum.Level != 2)
                                continue;

                            if (!nextSpectrum.RetentionTime.HasValue)
                            {
                                throw new InvalidDataException(
                                string.Format(ResultsResources.SpectraChromDataProvider_SpectraChromDataProvider_Scan__0__found_without_scan_time,
                                    _dataFile.GetSpectrumId(i)));
                            }
                            var precursors = nextSpectrum.Precursors;
                            if (precursors.Count < 1 || !precursors[0].PrecursorMz.HasValue)
                            {
                            throw new InvalidDataException(
                                string.Format(ResultsResources.SpectraChromDataProvider_SpectraChromDataProvider_Scan__0__found_without_precursor_mz,
                                    _dataFile.GetSpectrumId(i)));
                            }
                            return new SpectrumInfo(i, new[] {nextSpectrum}, (float) nextSpectrum.RetentionTime.Value);
                        }
                        else
                        {
                            // CONSIDER: This showed up as 10% in diaPASEF profiling because it requires FullMetaData
                            // It no longer provides any benefit in that case, because of the use of combined 3D spectra
                            // Before reinstating this filter, we need a way of deciding whether it will be of any use
                            // by querying the MsDataFile, did it succeed in producing combined spectra. Otherwise,
                            // this is a costly operation with little benefit.
                            //                        var ionMobility = _filter.HasIonMobilityFilters ? _lookaheadContext.GetIonMobility(i) : null ; // Read this first to take advantage of cache behavior

                            // If MS/MS filtering is not enabled, skip anything that is not a MS1 scan
                            var msLevel = _lookaheadContext.GetMsLevel(i);
                            if (!_filter.EnabledMsMs && msLevel != 1)
                                continue;
                            // And if full gradient MS1 is not required and MS1 filtering is not enabled, skip MS1 spectra (unless this is GC-EI, where everything is fragmented)
                            if (!_filter.EnabledMs && msLevel == 1 && !_filter.IsFilteringFullGradientMs1 && !_filter.IsElectronIonizationMse)
                                continue;

                            // Skip quickly through the chromatographic lead-in and tail when possible 
                            if (msLevel > 1 || (_filter.HasRangeRT && !_filter.IsFilteringFullGradientMs1)) // We need all MS1 for TIC and BPC
                            {
                                // Only do these checks if we can get the information instantly. Otherwise,
                                // this will slow down processing in more complex cases.
                                double? rtCheck = _lookaheadContext.GetRetentionTime(i);
                                if (_filter.IsOutsideRetentionTimeRange(rtCheck))
                                {
                                    // Leave an update cue for the chromatogram painter then move on
                                    if (_allChromData != null)
                                        _allChromData.CurrentTime = (float) rtCheck.Value;
                                    continue;
                                }

                                if (msLevel > 1)
                                {
                                    var precursors = _lookaheadContext.GetPrecursors(i, 1);
                                    if (precursors.Any() && !_filter.HasProductFilterPairs(rtCheck, precursors))
                                    {
                                        continue;
                                    }
                                }
                            }

                            // Ignore uninteresting ion mobility ranges
                            //                        if (ionMobility != null && ionMobility.HasValue && _filter.IsOutsideIonMobilityRange(ionMobility))
                            //                        {
                            //                            continue;
                            //                        }

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
                                // looking for the 3 in 3.0.1 (or the 10 in 10.0.1) or the 2 in 1.2.3 if it's combined ion mobility'
                                if (MsDataSpectrum.WatersFunctionNumberFromId(nextSpectrum.Id, _dataFile.HasCombinedIonMobilitySpectra && nextSpectrum.IonMobilities != null) > 2)
                                    continue;
                            }
                            else if (_filter.IsWatersFile)
                            {
                                // looking for the 3 in id string 3.0.1 (or the 10 in 10.0.1)
                                if (_dataFile.IsWatersLockmassSpectrum(nextSpectrum))
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
                            if (!rt.HasValue)
                                continue; // Spectrum didn't match filter, probably due to being outside IM range

                            if (!_filter.ContainsTime(rt.Value))
                            {
                                if (_allChromData != null)
                                    _allChromData.CurrentTime = (float) rt.Value;
                                continue;
                            }

                            return new SpectrumInfo(i, nextSpectra, rt.Value);
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.Message.Contains(@"NoVendorPeakPickingException"))
                            throw;
                        throw new Exception($@"error reading spectrum {_dataFile.GetSpectrumId(i)}", e);
                    }
                }
                return SpectrumInfo.LAST;
            }

            private class SpectrumInfo : IDisposable, IMemSized
            {
                public static readonly SpectrumInfo LAST = new SpectrumInfo(-1, null, 0);

                public SpectrumInfo(int index, MsDataSpectrum[] allSpectra, double retentionTime)
                {
                    Index = index;
                    AllSpectra = allSpectra;
                    RetentionTime = retentionTime;
                    // Size should be dominated by the array lengths
                    int arrayLen = 0, arrayCount = 0;
                    if (allSpectra != null)
                    {
                        DataSpectrum = allSpectra[0];
                        arrayLen = allSpectra.Sum(s => s.Intensities.Length);
                        arrayCount = 2;
                    }

                    if (DataSpectrum != null && DataSpectrum.IonMobilities != null)
                    {
                        SortEvent = new ManualResetEvent(false);
                        arrayCount++;
                    }

                    Size = arrayLen * arrayCount * sizeof(double);
                }

                public ManualResetEvent SortEvent { get; private set; }

                public int Index { get; private set; }
                public MsDataSpectrum DataSpectrum { get; private set; }
                public MsDataSpectrum[] AllSpectra { get; private set; }
                public double RetentionTime { get; private set; }
                public int Size { get; private set; }

                public bool IsLast { get { return DataSpectrum == null; } }

                public void Dispose()
                {
                    SortEvent?.Dispose();
                }
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

                var chromKeyIndexes = chromKeys.Select((chromKey, index) => Tuple.Create(chromKey, index));
                var groupedByEndTime = chromKeyIndexes.GroupBy(tuple => tuple.Item1.OptionalMaxTime ?? float.MaxValue)
                    .OrderBy(group => group.Key).ToList();
                ChromKeys = ImmutableList.ValueOf(groupedByEndTime.SelectMany(group => group.Select(tuple => tuple.Item1)));
                if (groupedByEndTime.Count > 1)
                {
                    var sortIndexes = groupedByEndTime.SelectMany(group => group.Select(tuple => tuple.Item2)).ToArray();
                    // The sort indexes tell us where the keys used to live. For lookup, we need
                    // to go the other way. Chromatograms will come in indexed by where they used to
                    // be, and we need to put them into the _chromList array in the new location of
                    // the ChromKey.
                    _chromKeyLookup = new int[sortIndexes.Length];
                    for (int j = 0; j < sortIndexes.Length; j++)
                        _chromKeyLookup[sortIndexes[j]] = j;
                }
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

            public IList<MsPrecursor> GetPrecursors(int index, int level)
            {
                if (index == _lookAheadIndex && _lookAheadDataSpectrum != null)
                    return _lookAheadDataSpectrum.GetPrecursorsByMsLevel(level);
                else
                    return _dataFile.GetPrecursors(index, level);
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
                double? rtReported = null;
                double rtTotal = 0;

                if (dataSpectrum.IonMobility.HasValue) // Old style per-scan ion mobility
                {
                    // IM data - gather spectra at this RT ignoring any with uninteresting IM values
                    _previousIonMobilityValue = IonMobilityValue.EMPTY;
                    _lookAheadDataSpectrum = null;
                    while (_lookAheadIndex++ < _lenSpectra)
                    {
                        _rt = dataSpectrum.RetentionTime;
                        if (_rt.HasValue && dataSpectrum.Mzs.Length != 0)
                        {
                            spectrumList.Add(dataSpectrum);
                            if (!rtReported.HasValue)
                                rtReported = dataSpectrum.RetentionTime;
                        }
                        if (!dataSpectrum.IonMobility.HasValue)
                            break;

                        // Advance to next spectrum with correct RT and in-range IM
                        var foundUsefulSpectrum = false;
                        while (_lookAheadIndex < _lenSpectra)
                        {
                            var nextIM = _dataFile.IonMobilityUnits == eIonMobilityUnits.none ? 
                                IonMobilityValue.EMPTY : 
                                _dataFile.GetIonMobility(_lookAheadIndex); // If we need this, get it now as it tends to sweep up the RT value as well
                            var nextRT = _dataFile.GetStartTime(_lookAheadIndex);
                            if ((_rt ?? 0) != (nextRT ?? -1))
                                break; // We've left the RT range, done here
                            if (!_filter.IsAllIons)
                            {
                                // Unless doing All-Ions pay attention to changes in precursor isolation
                                // Neither do we ever expect to see a transition in MS1 without an RT change
                                // So, ignore the case when nextPrecursors are empty
                                var nextPrecursors = _dataFile.GetPrecursors(_lookAheadIndex, 1);
                                if (nextPrecursors.Count > 0 && !ArrayUtil.EqualsDeep(nextPrecursors, dataSpectrum.Precursors))
                                    break; // Different isolation
                            }
                            if (IsNextSpectrumIonMobilityForCurrentRT(nextIM))
                            {
                                foundUsefulSpectrum = true;
                                break; // This spectrum has interesting RT and IM, go add to list
                            }
                            _lookAheadIndex++; // Keep looking for useful IM ranges within this RT
                        }

                        if (!foundUsefulSpectrum)
                        {
                            _lookAheadDataSpectrum = null; // Ran off end of current RT
                            break;
                        }

                        if (_lookAheadIndex < _lenSpectra)
                        {
                            dataSpectrum = _lookAheadDataSpectrum = _dataFile.GetSpectrum(_lookAheadIndex); // Add this to the list
                        }
                    }
                }
                else if (_filter.IsAgilentMse)
                {
                    // Agilent ramped-CE data - MS2 scans get averaged
                    var startCE = GetPrecursorCollisionEnergy(dataSpectrum);
                    var listLevel = dataSpectrum.Level;
                    while (_lookAheadIndex++ < _lenSpectra)
                    {
                        _rt = dataSpectrum.RetentionTime;
                        if (_rt.HasValue && dataSpectrum.Mzs.Length != 0)
                        {
                            spectrumList.Add(dataSpectrum);
                            rtTotal += dataSpectrum.RetentionTime.Value;
                        }
                        if (_lookAheadIndex < _lenSpectra)
                        {
                            dataSpectrum = _lookAheadDataSpectrum = _dataFile.GetSpectrum(_lookAheadIndex);
                            if (!NextSpectrumIsAgilentMse(dataSpectrum, listLevel, startCE))
                                break;
                        }
                    }
                    if (spectrumList.Count > 0)
                        rtReported = rtTotal / spectrumList.Count;
                }
                else
                {
                    // No need to search forward, this isn't IMS or Agilent ramped-CE data
                    rtReported = dataSpectrum.RetentionTime;
                    if (rtReported.HasValue && dataSpectrum.Mzs.Length != 0)
                    {
                        spectrumList.Add(dataSpectrum);
                    }
                }

                if (spectrumList.Any()) // Should have at least one non-empty scan at this ion mobility
                    _rt = rtReported;
                else
                    _rt = null;
                rt = _rt; // Set return value
                return spectrumList.ToArray();
            }

            private bool IsNextSpectrumIonMobilityForCurrentRT(IonMobilityValue nextIM)
            {
                var isUsefulNextSpectrum = IonMobilityValue.IsExpectedValueOrdering(_previousIonMobilityValue, nextIM) && 
                                           !_filter.IsOutsideIonMobilityRange(nextIM);
                _previousIonMobilityValue = nextIM;
                return isUsefulNextSpectrum;
            }

            private static double GetPrecursorCollisionEnergy(MsDataSpectrum dataSpectrum)
            {
                return dataSpectrum.Precursors.Count > 0
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

        public bool HasDeclaredMSnSpectra { get { return _dataFile.HasDeclaredMSnSpectra; } }

        public IEnumerable<MsInstrumentConfigInfo> ConfigInfoList
        {
            get { return _dataFile.GetInstrumentConfigInfoList(); }
        }

        public bool ProvidesCollisionalCrossSectionConverter { get { return _dataFile.ProvidesCollisionalCrossSectionConverter; } }
        public eIonMobilityUnits IonMobilityUnits { get { return _dataFile.IonMobilityUnits; } }
        public bool HasCombinedIonMobility { get { return _dataFile.HasCombinedIonMobilitySpectra; } } // When true, data source provides IMS data in 3-array format, which affects spectrum ID format

        public IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge, object obj)
        {
            var im = _dataFile.IonMobilityFromCCS(ccs, mz, charge);
            if (!im.HasValue)
            {
                Trace.TraceWarning(ResultsResources.DataFileInstrumentInfo_IonMobilityFromCCS_no_conversion, obj, ccs, mz, charge);
            }
            return im;
        }
        public double CCSFromIonMobility(IonMobilityValue im, double mz, int charge, object obj)
        {
            var ccs = _dataFile.CCSFromIonMobilityValue(im, mz, charge);
            if (double.IsNaN(ccs))
            {
                Trace.TraceWarning(ResultsResources.DataFileInstrumentInfo_CCSFromIonMobility_no_conversion, obj, im, mz, charge);
            }
            return ccs;
        }

        public bool IsWatersSonarData { get { return _dataFile.IsWatersSonarData(); } }
        public Tuple<int, int> SonarMzToBinRange(double mz, double tolerance)
        {
            return _dataFile.SonarMzToBinRange(mz, tolerance);
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
            var chromatogramGroupId = spectrum.ChromatogramGroupId;
            ChromExtractor extractor = spectrum.Extractor;
            int ionScanCount = spectrum.ProductFilters.Length;
            ChromDataCollector collector;
            var key = new PrecursorTextId(precursorMz, null, null, ionMobility, chromatogramGroupId, extractor);
            int index = spectrum.FilterIndex;
            while (PrecursorCollectorMap.Count <= index)
                PrecursorCollectorMap.Add(null);
            if (PrecursorCollectorMap[index] != null)
                collector = PrecursorCollectorMap[index].Item2;
            else
            {
                collector = new ChromDataCollector(chromatogramGroupId, precursorMz, ionMobility, index, IsGroupedTime);
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
                // The chromatogram index is used to determine which spillfile to use.
                // All chromatograms in this group use the same spillfile, so any chromatogram index will work
                int firstChromatogramIndex = chromatograms.ProductFilterIdToId(spectrum.ProductFilters.First().FilterId);

                collector.ScansCollector.Add(firstChromatogramIndex, scanId, _blockWriter);
                collector.GroupedTimesCollector.Add(firstChromatogramIndex, time, _blockWriter);
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
                _allChromData.Add(spectrum.ChromatogramGroupId, spectrum.PeptideColor, spectrum.FilterIndex, time, spectrum.Intensities);

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
        public ChromDataCollector(ChromatogramGroupId chromatogramGroupId, SignedMz precursorMz, IonMobilityFilter ionMobility, int statusId, bool isGroupedTime)
        {
            ChromatogramGroupId = chromatogramGroupId;
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

        public ChromatogramGroupId ChromatogramGroupId { get; private set; }
        public SignedMz PrecursorMz { get; private set; }
        public IonMobilityFilter IonMobility { get; private set; }
        public int StatusId { get; private set; }
        public Dictionary<SpectrumProductFilter, ChromCollector> ProductIntensityMap { get; private set; }
        public readonly SortedBlockedList<float> GroupedTimesCollector;
        public readonly BlockedList<int> ScansCollector;

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
