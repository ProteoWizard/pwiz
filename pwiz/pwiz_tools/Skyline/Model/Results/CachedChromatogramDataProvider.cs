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
using System.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results
{
    internal class CachedChromatogramDataProvider : ChromDataProvider
    {
        private ChromatogramCache _cache;
        private ChromKeyIndices[] _chromKeyIndices;

        private ChromKeyIndices _lastIndices;
        private ChromatogramGroupInfo _lastChromGroupInfo;

        private readonly bool _singleMatchMz;

        public CachedChromatogramDataProvider(ChromatogramCache cache,
                                              SrmDocument document,
                                              string dataFilePath,
                                              bool? singleMatchMz,
                                              ProgressStatus status,
                                              int startPercent,
                                              int endPercent,
                                              ILoadMonitor loader)
            : base(status, startPercent, endPercent, loader)
        {
            _cache = cache;
            _chromKeyIndices = cache.GetChromKeys(dataFilePath).OrderBy(v => v.LocationPoints).ToArray();
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

        public override void GetChromatogram(int id, out float[] times, out float[] intensities)
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

            SetPercentComplete(100 * id / _chromKeyIndices.Length);
        }

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