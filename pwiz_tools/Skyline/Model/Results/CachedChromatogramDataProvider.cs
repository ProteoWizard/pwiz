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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
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
                                              ILoadMonitor loader)
            : base(fileInfo, status, startPercent, endPercent, loader)
        {
            // Deal with older cache formats where we did not record chromatogram polarity
            var assumeNegativeChargesInPreV11Caches = document.MoleculeTransitionGroups.All(p => p.PrecursorMz.IsNegative);

            // Need a newly loaded copy to allow for concurrent loading for multiple cached files
            _cache = ChromatogramCache.Load(cache.CachePath, new ProgressStatus(), loader, assumeNegativeChargesInPreV11Caches);

            _fileIndex = cache.CachedFiles.IndexOf(f => Equals(f.FilePath, dataFilePath));
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

        public override eIonMobilityUnits IonMobilityUnits { get { return _cache != null ? _cache.CachedFiles[_fileIndex].IonMobilityUnits : eIonMobilityUnits.none; } }

        public override bool GetChromatogram(int id, Target modifiedSequence, Color peptideColor, out ChromExtra extra, out TimeIntensities timeIntensities)
        {
            var chromKeyIndices = _chromKeyIndices[id];
            if (_lastChromGroupInfo == null || _lastIndices.GroupIndex != chromKeyIndices.GroupIndex)
            {
                _lastChromGroupInfo = _cache.LoadChromatogramInfo(chromKeyIndices.GroupIndex);
                _lastChromGroupInfo.ReadChromatogram(_cache);
            }
            _lastIndices = chromKeyIndices;
            var tranInfo = _lastChromGroupInfo.GetTransitionInfo(chromKeyIndices.TranIndex, TransformChrom.raw);
            timeIntensities = tranInfo.TimeIntensities;

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
                    modifiedSequence,
                    peptideColor,
                    chromKeyIndices.StatusId,
                    chromKeyIndices.StatusRank,
                    timeIntensities.Times,
                    timeIntensities.Intensities);
            }
            return true;
        }

        public override byte[] MSDataFileScanIdBytes
        {
            get { return _cache.LoadMSDataFileScanIdBytes(_fileIndex); }
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
            Dispose();
        }

        public override void Dispose()
        {
            if (_cache != null)
                _cache.ReadStream.CloseStream();
            _cache = null;
            _chromKeyIndices = null;
            _lastChromGroupInfo = null;
        }
    }
}