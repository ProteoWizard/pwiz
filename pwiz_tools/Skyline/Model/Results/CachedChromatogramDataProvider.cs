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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal class CachedChromatogramDataProvider : ChromDataProvider
    {
        private ChromatogramCache _cache;
        private int _fileIndex;
        private ChromKeyIndices[] _chromKeyIndices;

        private ChromKeyIndices _lastIndices;
        private ChromatogramGroupInfo _lastChromGroupInfo;

        private readonly bool _singleMatchMz;
        
        private readonly float? _maxRetentionTime;
        private readonly float? _maxIntensity;

        public CachedChromatogramDataProvider(ChromatogramCache cache,
                                              SrmDocument document,
                                              MsDataFileUri dataFilePath,
                                              ChromFileInfo fileInfo,
                                              bool? singleMatchMz,
                                              ProgressStatus status,
                                              int startPercent,
                                              int endPercent,
                                              ILoadMonitor loader)
            : base(fileInfo, status, startPercent, endPercent, loader)
        {
            _cache = cache;
            _fileIndex = cache.CachedFiles.IndexOf(f => Equals(f.FilePath, dataFilePath));
            _chromKeyIndices = cache.GetChromKeys(dataFilePath).OrderBy(v => v.LocationPoints).ToArray();
            _cache.GetStatusDimensions(dataFilePath, out _maxRetentionTime, out _maxIntensity);
            _singleMatchMz = singleMatchMz.HasValue
                                 ? singleMatchMz.Value
                                 // Unfortunately, before the single matching status was
                                 // written into the cache file, we can only guess about its
                                 // status based on the overall document settings
                                 : document.Settings.TransitionSettings.FullScan.IsEnabled;
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get { return _chromKeyIndices.Select((v, i) => new KeyValuePair<ChromKey, int>(v.Key, i)); }
        }

        public override bool GetChromatogram(
            int id, string modifiedSequence, Color peptideColor,
            out ChromExtra extra, out float[] times, out int[] scanIndexes, out float[] intensities, out float[] massErrors)
        {
            var chromKeyIndices = _chromKeyIndices[id];
            if (_lastChromGroupInfo == null || _lastIndices.GroupIndex != chromKeyIndices.GroupIndex)
            {
                _lastChromGroupInfo = _cache.LoadChromatogramInfo(chromKeyIndices.GroupIndex);
                _lastChromGroupInfo.ReadChromatogram(_cache);
            }
            _lastIndices = chromKeyIndices;
            var tranInfo = _lastChromGroupInfo.GetTransitionInfo(chromKeyIndices.TranIndex);
            times = tranInfo.Times;
            intensities = tranInfo.Intensities;
            massErrors = null;
            if (tranInfo.MassError10Xs != null)
                massErrors = tranInfo.MassError10Xs.Select(m => m/10.0f).ToArray();
            scanIndexes = null;
            if (tranInfo.ScanIndexes != null)
                scanIndexes = tranInfo.ScanIndexes[(int) chromKeyIndices.Key.Source];

            SetPercentComplete(100 * id / _chromKeyIndices.Length);

            extra = new ChromExtra(chromKeyIndices.StatusId, chromKeyIndices.StatusRank);

            // Display in AllChromatogramsGraph
            if (chromKeyIndices.Key.Precursor != 0)
            {
                LoadingStatus.Transitions.AddTransition(
                    modifiedSequence,
                    peptideColor,
                    chromKeyIndices.StatusId,
                    chromKeyIndices.StatusRank,
                    times,
                    intensities);
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