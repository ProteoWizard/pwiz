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
using System.IO;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
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
        private List<string> _scanIdList =
            new List<string>();

        private readonly bool _isProcessedScans;
        private readonly bool _isSingleMzMatch;
        private readonly ChromCollector.Allocator _allocator;

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
                _previousDriftTime = 0;
                _lenSpectra = dataFile.SpectrumCount;
            }

            private int _lookAheadIndex;
            private double? _rt;
            private readonly SpectrumFilter _filter;
            private readonly MsDataFileImpl _dataFile;
            private MsDataSpectrum _lookAheadDataSpectrum; // Result of _datafile.GetSpectrum(_lookaheadIndex), or null
            private readonly int _lenSpectra;
            private double _previousDriftTime;

            public int GetMsLevel(int index)
            {
                if (index == _lookAheadIndex && _lookAheadDataSpectrum != null)
                    return _lookAheadDataSpectrum.Level;
                else
                    return _dataFile.GetMsLevel(index);
            }

            public double? GetRetentionTime(int index)
            {
                if (index == _lookAheadIndex && _lookAheadDataSpectrum != null)
                    return _lookAheadDataSpectrum.RetentionTime;
                else
                    return _dataFile.GetStartTime(index);  // Returns 0 if retrieval is too expensive
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

            private bool NextSpectrumIsDriftScanForCurrentRetentionTime(MsDataSpectrum nextSpectrum)
            {
                bool result = ((_rt ?? 0) == (nextSpectrum.RetentionTime ?? -1)) &&
                              ((nextSpectrum.DriftTimeMsec ?? 0) > _previousDriftTime);
                _previousDriftTime = nextSpectrum.DriftTimeMsec ?? 0;
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
            // a single "drift bin" with no drift time.
            //
            // Also for Agilent ramped-CE msE, gather MS2 scans together
            // so they get averaged.
            public MsDataSpectrum[] Lookahead(MsDataSpectrum dataSpectrum, out double? rt)
            {
                var spectrumList = new List<MsDataSpectrum>();
                int listLevel = dataSpectrum.Level;
                double startCE = GetPrecursorCollisionEnergy(dataSpectrum);
                _previousDriftTime = -1;
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
                    if (!_filter.IsAgilentMse && !dataSpectrum.DriftTimeMsec.HasValue)
                        break;

                    if (_lookAheadIndex < _lenSpectra)
                    {
                        dataSpectrum = _lookAheadDataSpectrum = _dataFile.GetSpectrum(_lookAheadIndex);
                        // Reasons to keep adding to the list:
                        //   Retention time hasn't changed but drift time has increased, or
                        //   Agilent ramped-CE data - MS2 scans get averaged
                        if (!(NextSpectrumIsDriftScanForCurrentRetentionTime(dataSpectrum) ||
                              NextSpectrumIsAgilentMse(dataSpectrum, listLevel, startCE)))
                            break;
                    }
                }
                if (spectrumList.Any()) // Should have at least one non-empty scan at this drift time
                    _rt = _filter.IsAgilentMse ? (rtTotal/spectrumList.Count()) : rtFirst;
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
                                        string cachePath, // We'll write tempfiles in this directory
                                        ProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(fileInfo, status, startPercent, endPercent, loader)
        {
            // Create allocator used by all ChromCollectors to store transition times and intensities.
            _allocator = new ChromCollector.Allocator(cachePath);

            using (dataFile)
            {
                SetPercentComplete(LOAD_PERCENT);

                // If no SRM spectra, then full-scan filtering must be enabled
                bool isSrm = dataFile.HasSrmSpectra;
                if (!isSrm && !document.Settings.TransitionSettings.FullScan.IsEnabled)
                    throw new NoFullScanFilteringException(dataFile.FilePath);

                PeptideFinder peptideFinder = isSrm ? new PeptideFinder(document) : null;
                    
                // Only mzXML from mzWiff requires the introduction of zero values
                // during interpolation.
                _isProcessedScans = dataFile.IsMzWiffXml;

                // Create a spectrum filter data structure, in case it is needed
                // This could be done lazily, but does not seem worth it, given the file reading
                var filter = new SpectrumFilter(document, fileInfo.FilePath, dataFile);

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

                // If possible, find the maximum retention time in order to scale the chromatogram graph.
                if (allChromData != null && (filter.EnabledMsMs || filter.EnabledMs))
                {
                    var retentionTime = dataFile.GetStartTime(lenSpectra - 1);
                    if (retentionTime.HasValue)
                    {
                        allChromData.MaxRetentionTime = (float) retentionTime.Value;
                        allChromData.MaxRetentionTimeKnown = true;
                        allChromData.Progressive = true;
                    }
                }

                // Determine what type of demultiplexer, if any, to use based on settings in the
                // IsolationScheme menu
                IsolationScheme isoScheme = document.Settings.TransitionSettings.FullScan.IsolationScheme;
                var handlingType = isoScheme == null
                    ? IsolationScheme.SpecialHandlingType.NONE
                    : isoScheme.SpecialHandling;
                IDemultiplexer demultiplexer = null;
                switch (handlingType)
                {
                    case IsolationScheme.SpecialHandlingType.OVERLAP:
                        demultiplexer = new OverlapDemultiplexer(dataFile, filter);
                        break;
                    case IsolationScheme.SpecialHandlingType.MULTIPLEXED:
                        demultiplexer = new MsxDemultiplexer(dataFile, filter);
                        break;
                    case IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED:
                        demultiplexer = new MsxOverlapDemultiplexer(dataFile, filter);
                        break;
                }

                var lookaheadContext = new LookaheadContext(filter, dataFile);

                for (int i = 0; i < lenSpectra; i = lookaheadContext.NextIndex(i))
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
                        var colorSeed = peptideFinder.GetColorSeed(precursorMz);
                        ProcessSrmSpectrum(
                            dataSpectrum.RetentionTime.Value,
                            colorSeed,
                            precursorMz,
                            filterIndex,
                            dataSpectrum.Mzs,
                            dataSpectrum.Intensities,
                            0, 0, // ion mobility unknown
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
                        var msLevel = lookaheadContext.GetMsLevel(i);
                        if (!filter.EnabledMsMs && msLevel != 1)
                            continue;

                        // Skip quickly through the chromatographic lead-in and tail when possible 
                        if (msLevel > 1) // We need all MS1 for TIC and BPC
                        {
                            double? rtCheck = lookaheadContext.GetRetentionTime(i);
                            if (filter.IsOutsideRetentionTimeRange(rtCheck))
                            {
                                // Leave an update cue for the chromatogram painter then move on
                                if (allChromData != null)
                                    allChromData.CurrentTime = (float)rtCheck.Value;
                                continue;
                            }
                        }
                        // Inexpensive checks are complete, now actually get the spectrum data
                        var dataSpectrum = lookaheadContext.GetSpectrum(i);
                        // Assertion for testing ID to spectrum index support
//                        int iFromId = dataFile.GetScanIndex(dataSpectrum.Id);
//                        Assume.IsTrue(i == iFromId);
                        if (dataSpectrum.Mzs.Length == 0)
                            continue;

                        double? rt = dataSpectrum.RetentionTime;
                        if (!rt.HasValue)
                            continue;
                        if (allChromData != null)
                            allChromData.CurrentTime = (float)rt.Value;

                        // Deal with ion mobility data - look ahead for a run of scans all 
                        // with the same retention time.  For non-IMS data we'll just get
                        // a single "drift bin" with no drift time.
                        //
                        // Also for Agilent ramped-CE msE, gather MS2 scans together
                        // so they get averaged.
                        //
                        var spectra = lookaheadContext.Lookahead(dataSpectrum, out rt);
                        if (!filter.ContainsTime(rt.Value))
                            continue;

                        if (filter.IsMsSpectrum(dataSpectrum))
                        {
                            var chromMapMs = filter.IsSimSpectrum(dataSpectrum) ? chromMapSim : chromMapMs1;
                            string scanId = dataSpectrum.Id;

                            // Process all SRM spectra that can be generated by filtering this full-scan MS1
                            if (chromMapMs.IsSharedTime)
                            {
                                chromMapMs.AddSharedTime((float)rt.Value, GetScanIdIndex(scanId));
                            }
                            foreach (var spectrum in filter.SrmSpectraFromMs1Scan(rt, dataSpectrum.Precursors,
                                spectra))
                            {
                                chromMapMs.ProcessExtractedSpectrum(rt.Value, GetScanIdIndex(scanId), spectrum);
                            }
                        }
                        if (filter.IsMsMsSpectrum(dataSpectrum))
                        {
                            // Process all SRM spectra that can be generated by filtering this full-scan MS/MS
                            if (demultiplexer == null)
                            {
                                ProcessSpectrumList(spectra, chromMap, rt.Value, filter, dataSpectrum.Id);
                            }
                            else
                            {
                                foreach (var deconvSpectrum in demultiplexer.GetDeconvolvedSpectra(i, dataSpectrum))
                                {
                                    ProcessSpectrumList(new [] { deconvSpectrum }, chromMap, rt.Value, filter, dataSpectrum.Id);
                                }
                            }
                        }
                        if (filter.GetMseLevel() > 2)
                        {
                            // We're into the Waters function 3 in Mse data - leave this loop
                            lookaheadContext.NextIndex(lenSpectra);
                        }        
                    }
                }

                if (chromMap.Count == 0 && chromMapMs1.Count == 0 && chromMapSim.Count == 0)
                    throw new NoFullScanDataException(dataFile.FilePath);

                AddChromatograms(chromMap, ChromSource.fragment);
                AddChromatograms(chromMapSim, ChromSource.sim);
                AddChromatograms(chromMapMs1, ChromSource.ms1);

                if (dataFile.GetLog() != null) // in case perf logging is enabled
                    DebugLog.Info(dataFile.GetLog());
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

        private void AddChromatograms(ChromDataCollectorSet chromMap, ChromSource source)
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
                    scanIdCollector = collector.GroupedTimesCollector.ScanIdCollector;
                    timesCollector = collector.GroupedTimesCollector;
                }

                foreach (var pairProduct in collector.ProductIntensityMap)
                {
                    var chromCollector = pairProduct.Value;
                    if (timesCollector != null)
                    {
                        chromCollector.ScanIdCollector = scanIdCollector;
                        chromCollector.TimesCollector = timesCollector;
                    }
                    var key = new ChromKey(collector.ModifiedSequence,
                        collector.PrecursorMz,
                        collector.IonMobilityValue,
                        collector.IonMobilityExtractionWidth,
                        pairProduct.Key.ProductMz,
                        0,
                        pairProduct.Key.ExtractionWidth,
                        source,
                        modSeq.Extractor,
                        true,
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

        /// <summary>
        /// Process a list of spectra - typically of length one,
        /// but possibly a set of drift bins all with same retention time,
        /// or a set of Agilent ramped-CE Mse scans to be averaged
        /// </summary>
        private void ProcessSpectrumList(MsDataSpectrum[] spectra,
                                     ChromDataCollectorSet chromMap,
                                     double rt,
                                     SpectrumFilter filter,
                                     string scanId)
        {
            foreach (var spectrum in filter.Extract(rt, spectra))
            {
                if (_loader.IsCanceled)
                    throw new LoadCanceledException(Status);

                chromMap.ProcessExtractedSpectrum(rt, GetScanIdIndex(scanId), spectrum);
            }
        }

        private static void ProcessSrmSpectrum(double time,
                                               string modifiedSequence,
                                               double precursorMz,
                                               int filterIndex,
                                               double[] mzs,
                                               double[] intensities,
                                               double ionMobilityValue,
                                               double ionMobilityExtractionWidth,
                                               ChromDataCollectorSet chromMap)
        {
            float[] intensityFloats = new float[intensities.Length];
            for (int i = 0; i < intensities.Length; i++)
                intensityFloats[i] = (float) intensities[i];
            var spectrum = new ExtractedSpectrum(modifiedSequence, precursorMz, ionMobilityValue, ionMobilityExtractionWidth,
                ChromExtractor.summed, filterIndex, mzs, null, intensityFloats, null);
            chromMap.ProcessExtractedSpectrum(time, -1, spectrum);
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get
            {
                for (int i = 0; i < _chromatograms.Count; i++)
                    yield return new KeyValuePair<ChromKey, int>(_chromatograms[i].Key, i);
            }
        }

        public override byte[] ScanIdBytes
        {
            get { return DataFileScanIds.ToBytes(_scanIdList); }
        }

        public override bool GetChromatogram(
            int id, string modifiedSequence,
            out ChromExtra extra, out float[] times, out int[] scanIds, out float[] intensities, out float[] massErrors)
        {
            var keyAndCollector = _chromatograms[id];
            keyAndCollector.Collector.ReleaseChromatogram(out times, out scanIds, out intensities, out massErrors);

            extra = new ChromExtra(keyAndCollector.StatusId, LoadingStatus.Transitions.GetRank(id));  // TODO: Get rank

            // Assume that each chromatogram will be read once, though this may
            // not always be completely true.
            _readChromatograms++;

            if (_readChromatograms < _chromatograms.Count)
                SetPercentComplete(LOAD_PERCENT + BUILD_PERCENT + _readChromatograms * READ_PERCENT / _chromatograms.Count);
            return true;
        }

        public override double? MaxRetentionTime
        {
            get { return LoadingStatus.Transitions.MaxRetentionTime; }
        }

        public override double? MaxIntensity
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
            ChromCollector.ReleaseStatics();
            _scanIdList = null;
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
            PrecursorCollectorMap = new List<Tuple<PrecursorTextId, ChromDataCollector>>();
            if (timeSharing == TimeSharing.shared)
            {
                SharedTimesCollector = new ChromCollector();
                ScanIdsCollector = new ChromCollector();
            }
            _allChromData = allChromData;
        }

        private ChromSource ChromSource { get; set; }

        private TimeSharing TypeOfScans { get; set; }
        private readonly ChromatogramLoadingStatus.TransitionData _allChromData;

        public bool IsSingleTime { get { return TypeOfScans == TimeSharing.single; } }
        public bool IsGroupedTime { get { return TypeOfScans == TimeSharing.grouped; } }
        public bool IsSharedTime { get { return TypeOfScans == TimeSharing.shared; } }

        public ChromCollector SharedTimesCollector { get; private set; }
        public ChromCollector ScanIdsCollector { get; private set; }

        public void AddSharedTime(float time, int scanId)
        {
            SharedTimesCollector.Add(time);
            ScanIdsCollector.Add(scanId);
        }

        public IList<Tuple<PrecursorTextId, ChromDataCollector>> PrecursorCollectorMap { get; private set; }

        public int Count { get { return PrecursorCollectorMap.Count; } }

        public void ProcessExtractedSpectrum(double time, int scanId, ExtractedSpectrum spectrum)
        {
            double precursorMz = spectrum.PrecursorMz;
            double? ionMobilityValue = spectrum.IonMobilityValue;
            double ionMobilityExtractionWidth = spectrum.IonMobilityExtractionWidth;
            string textId = spectrum.TextId;
            ChromExtractor extractor = spectrum.Extractor;
            int ionScanCount = spectrum.Mzs.Length;
            ChromDataCollector collector;
            var key = new PrecursorTextId(precursorMz, textId, extractor);
            int index = spectrum.FilterIndex;
            while (PrecursorCollectorMap.Count <= index)
                PrecursorCollectorMap.Add(null);
            if (PrecursorCollectorMap[index] != null)
                collector = PrecursorCollectorMap[index].Item2;
            else
            {
                collector = new ChromDataCollector(textId, precursorMz, ionMobilityValue, ionMobilityExtractionWidth, index, IsGroupedTime);
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
                collector.AddScanId(scanId);
                lenTimes = collector.AddGroupedTime((float)time);
            }

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
                _allChromData.Add(spectrum.TextId, spectrum.FilterIndex, ChromSource, (float)time, spectrum.Intensities);

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
        public ChromDataCollector(string modifiedSequence, double precursorMz, double ? ionMobilityValue, double ionMobilityExtractionWidth, int statusId, bool isGroupedTime)
        {
            ModifiedSequence = modifiedSequence;
            PrecursorMz = precursorMz;
            IonMobilityValue = ionMobilityValue;
            IonMobilityExtractionWidth = ionMobilityExtractionWidth;
            StatusId = statusId;
            ProductIntensityMap = new Dictionary<ProductExtractionWidth, ChromCollector>();
            if (isGroupedTime)
                GroupedTimesCollector = new ChromCollector();
        }

        public string ModifiedSequence { get; private set; }
        public double PrecursorMz { get; private set; }
        public double? IonMobilityValue { get; private set; }
        public double IonMobilityExtractionWidth { get; private set; }
        public int StatusId { get; private set; }
        public Dictionary<ProductExtractionWidth, ChromCollector> ProductIntensityMap { get; private set; }
        public readonly ChromCollector GroupedTimesCollector;

        public int AddGroupedTime(float time)
        {
            return GroupedTimesCollector.Add(time);
        }

        public void AddScanId(int scanId)
        {
            GroupedTimesCollector.AddScanId(scanId);
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

    internal struct ProductExtractionWidth
    {
        public ProductExtractionWidth(double productMz, double extractionWidth)
            : this()
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
            return obj is ProductExtractionWidth && Equals((ProductExtractionWidth)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ProductMz.GetHashCode() * 397) ^ ExtractionWidth.GetHashCode();
            }
        }

        #endregion
    }
}