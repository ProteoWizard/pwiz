/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal class CachedChromatogramDataProvider : ChromDataProvider
    {
        private ChromatogramCache _cache;
        private readonly int _fileIndex;
        private ChromKeyIndices[] _chromKeyIndices;

        private ChromKeyIndices _lastIndices;
        private ChromatogramGroupInfo _lastChromGroupInfo;

        private readonly bool _singleMatchMz;
        
        private readonly float? _maxRetentionTime;
        private readonly float? _maxIntensity;

        private readonly bool _sourceHasPositivePolarityData;
        private readonly bool _sourceHasNegativePolarityData;
        // Captured up front, since the cache builder asks for it after ReleaseMemory() drops the cache
        private readonly eIonMobilityUnits _ionMobilityUnits;
        // Storage scale of per-time-point observed IM, zero when observed IM is not tracked
        private readonly int _observedIonMobilityScale;
        // The previous peaks' observed IM and CCS, by chromatogram and peak boundaries, for peaks
        // picked again unchanged (see GetPreviousObservedCcs)
        private readonly ConcurrentDictionary<Tuple<int, float, float>, Tuple<float, float>> _previousObservedCcs =
            new ConcurrentDictionary<Tuple<int, float, float>, Tuple<float, float>>();
        // Converts observed IM to CCS for any other peak, with the raw file's vendor calibration
        private readonly RawFileCcsConverter _rawFileCcsConverter;

        /// <summary>
        /// The number of chromatograms read so far.
        /// </summary>
        private int _readChromatograms;

        public CachedChromatogramDataProvider(ChromatogramCache cache,
                                              SrmDocument document,
                                              MsDataFileUri dataFilePath,
                                              ChromFileInfo fileInfo,
                                              bool? singleMatchMz,
                                              IProgressStatus status,
                                              int startPercent,
                                              int endPercent,
                                              ILoadMonitor loader,
                                              Func<MsDataFileImpl> openRawFile)
            : base(fileInfo, status, startPercent, endPercent, loader)
        {
            // Open a new stream for the cache so that we can do concurrent reads
            _cache = cache = cache.ChangeReadStream(loader.StreamManager.CreatePooledStream(cache.CachePath, false));

            _fileIndex = cache.CachedFiles.IndexOf(f => Equals(f.FilePath, dataFilePath));
            _ionMobilityUnits = _fileIndex >= 0 ? cache.CachedFiles[_fileIndex].IonMobilityUnits : eIonMobilityUnits.none;
            _observedIonMobilityScale = RawTimeIntensities.GetObservedIonMobilityScaleOrZero(_ionMobilityUnits);
            if (_observedIonMobilityScale != 0)
                _rawFileCcsConverter = new RawFileCcsConverter(_ionMobilityUnits, openRawFile);
            _chromKeyIndices = cache.GetChromKeys(dataFilePath).OrderBy(v => v.LocationPoints).ToArray();
            foreach (var c in _chromKeyIndices.Where(i => i.Key.Precursor != 0))
            {
                if (c.Key.Precursor.IsNegative)
                {
                    _sourceHasNegativePolarityData = true;
                }
                else
                {
                    _sourceHasPositivePolarityData = true;
                }
            }
            _cache.GetStatusDimensions(dataFilePath, out _maxRetentionTime, out _maxIntensity);
            _singleMatchMz = singleMatchMz.HasValue
                                 ? singleMatchMz.Value
                                 // Unfortunately, before the single matching status was
                                 // written into the cache file, we can only guess about its
                                 // status based on the overall document settings
                                 : document.Settings.TransitionSettings.FullScan.IsEnabled;
        }

        public override IEnumerable<ChromKeyProviderIdPair> ChromIds
        {
            get { return _chromKeyIndices.Select((v, i) => new ChromKeyProviderIdPair(v.Key, i)); }
        }

        public override eIonMobilityUnits IonMobilityUnits { get { return _ionMobilityUnits; } }

        public override IIonMobilityFunctionsProvider IonMobilityFunctionsProvider { get { return _rawFileCcsConverter; } }

        /// <summary>
        /// The observed CCS of the previous peak with the same boundaries in the same chromatogram,
        /// which, picked from the same stored points, has the same apex and so the same observed IM
        /// and CCS. Converting that IM again would need the raw file. The previous peak may have
        /// been picked at import, from unrounded IM, so its IM is compared at storage precision.
        /// </summary>
        public override double? GetPreviousObservedCcs(int providerId, ChromPeak peak)
        {
            if (!peak.ObservedIonMobility.HasValue ||
                !_previousObservedCcs.TryGetValue(Tuple.Create(providerId, peak.StartTime, peak.EndTime), out var previous))
            {
                return null;
            }
            if (Math.Round(previous.Item1 * (double)_observedIonMobilityScale) !=
                Math.Round(peak.ObservedIonMobility.Value * (double)_observedIonMobilityScale))
            {
                return null;
            }
            return previous.Item2;
        }

        public override bool GetChromatogram(int id, ChromatogramGroupId chromatogramGroupId, Color peptideColor, out ChromExtra extra, out TimeIntensities timeIntensities)
        {
            var chromKeyIndices = _chromKeyIndices[id];
            if (_lastChromGroupInfo == null || _lastIndices.GroupIndex != chromKeyIndices.GroupIndex)
            {
                _lastChromGroupInfo = _cache.LoadChromatogramInfo(chromKeyIndices.GroupIndex);
            }
            _lastIndices = chromKeyIndices;
            var tranInfo = _lastChromGroupInfo.GetTransitionInfo(chromKeyIndices.TranIndex, TransformChrom.raw);
            timeIntensities = tranInfo.TimeIntensities;
            // Observed CCS is a precursor property, and only peaks from precursor chromatograms carry it
            if (_observedIonMobilityScale != 0 && chromKeyIndices.Key.Source != ChromSource.fragment)
            {
                foreach (var peak in tranInfo.Peaks)
                {
                    if (peak.ObservedIonMobility.HasValue && peak.ObservedCcs.HasValue)
                    {
                        _previousObservedCcs[Tuple.Create(id, peak.StartTime, peak.EndTime)] =
                            Tuple.Create(peak.ObservedIonMobility.Value, peak.ObservedCcs.Value);
                    }
                }
            }

            // Assume that each chromatogram will be read once, though this may
            // not always be completely true.
            _readChromatograms++;

            // But avoid reaching 100% before reading is actually complete
            SetPercentComplete(Math.Min(99, 100 * _readChromatograms / _chromKeyIndices.Length));

            extra = new ChromExtra(chromKeyIndices.StatusId, chromKeyIndices.StatusRank);

            // Display in AllChromatogramsGraph
            if (chromKeyIndices.Key.Precursor != 0 && Status is ChromatogramLoadingStatus)
            {
                ((ChromatogramLoadingStatus)Status).Transitions.AddTransition(
                    chromatogramGroupId,
                    peptideColor,
                    chromKeyIndices.StatusId,
                    chromKeyIndices.StatusRank,
                    timeIntensities.Times,
                    timeIntensities.Intensities);
            }
            return true;
        }

        public override IResultFileMetadata ResultFileData
        {
            get { return _cache?.GetOrLoadResultFileMetadata(_fileIndex); }
        }

        public override double? MaxRetentionTime { get { return _maxRetentionTime; } }

        public override double? MaxIntensity { get { return _maxIntensity; } }

        public override bool IsProcessedScans
        {
            get { return false; }
        }

        public override bool IsSingleMzMatch
        {
            get { return _singleMatchMz; }
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
            // Peaks are still being scored after reading ends, so the CCS converter stays open
            ReleaseCache();
        }

        public override void Dispose()
        {
            ReleaseCache();
            _previousObservedCcs.Clear();
            _rawFileCcsConverter?.Dispose();
        }

        private void ReleaseCache()
        {
            if (_cache != null)
                _cache.ReadStream.CloseStream();
            _cache = null;
            _chromKeyIndices = null;
            _lastChromGroupInfo = null;
        }
    }

    /// <summary>
    /// Observed IM to CCS conversion for peaks picked again from cached chromatograms (rescore),
    /// where the raw file whose vendor calibration does the conversion is not open. The raw file
    /// is opened on first need; when it cannot be found or opened, peaks get no CCS.
    /// </summary>
    internal sealed class RawFileCcsConverter : IIonMobilityFunctionsProvider, IDisposable
    {
        private readonly Func<MsDataFileImpl> _openRawFile;
        private readonly object _rawFileLock = new object();
        private bool _rawFileOpened;
        private MsDataFileImpl _rawFile;
        private DataFileInstrumentInfo _rawFileConverter;

        public RawFileCcsConverter(eIonMobilityUnits ionMobilityUnits, Func<MsDataFileImpl> openRawFile)
        {
            IonMobilityUnits = ionMobilityUnits;
            _openRawFile = openRawFile;
        }

        public eIonMobilityUnits IonMobilityUnits { get; }

        public bool ProvidesCollisionalCrossSectionConverter => true;

        public double CCSFromIonMobility(IonMobilityValue im, double mz, int charge, object obj)
        {
            if (!im.Mobility.HasValue)
                return double.NaN;
            lock (_rawFileLock)
            {
                var rawFileConverter = GetRawFileConverter();
                if (rawFileConverter == null || !rawFileConverter.ProvidesCollisionalCrossSectionConverter)
                    return double.NaN;
                return rawFileConverter.CCSFromIonMobility(im, mz, charge, obj);
            }
        }

        public IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge, object obj) => IonMobilityValue.EMPTY;
        public bool HasCombinedIonMobility => false;
        public bool IsWatersSonarData => false;
        public Tuple<int, int> SonarMzToBinRange(double mz, double tolerance) => null;
        public bool IsValidDiaPasefPoint(int windowGroup, double im, double isoMzLow, double isoMzHigh) => true;

        public void Dispose()
        {
            lock (_rawFileLock)
            {
                _rawFile?.Dispose();
                _rawFile = null;
                _rawFileConverter = null;
            }
        }

        // Opens the raw file at most once, for its vendor conversion
        private DataFileInstrumentInfo GetRawFileConverter()
        {
            if (!_rawFileOpened)
            {
                _rawFileOpened = true;
                try
                {
                    _rawFile = _openRawFile?.Invoke();
                }
                catch (Exception)
                {
                    // Observed CCS is supplementary, so a raw file that cannot be opened leaves it empty
                    _rawFile = null;
                }
                if (_rawFile != null)
                    _rawFileConverter = new DataFileInstrumentInfo(_rawFile);
            }
            return _rawFileConverter;
        }
    }
}