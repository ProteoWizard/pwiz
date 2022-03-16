/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Linq;
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class ChromCacheJoiner : ChromCacheWriter
    {
        private int _currentPartIndex = -1;
        private int _scoreTypesCount = -1;
        private readonly bool _assumeNegativeChargeInPreV11Caches;

        private readonly byte[] _buffer = new byte[0x40000];  // 256K
        private readonly Dictionary<Target, int> _dictTextIdToByteIndex = new Dictionary<Target, int>();

        public ChromCacheJoiner(string cachePath, IPooledStream streamDest,
                                IList<string> cacheFilePaths, ILoadMonitor loader, ProgressStatus status,
                                Action<ChromatogramCache, IProgressStatus> completed,
                                bool assumeNegativeChargeInPreV11Caches)
            : base(cachePath, loader, status, completed)
        {
            _destinationStream = streamDest;

            CacheFilePaths = cacheFilePaths;
            _assumeNegativeChargeInPreV11Caches = assumeNegativeChargeInPreV11Caches; // Deal with older cache formats where we did not record polarity
        }

        private IList<string> CacheFilePaths { get; set; }

        public void JoinParts()
        {
            lock (this)
            {
                if (_currentPartIndex != -1)
                    return;
                _currentPartIndex = 0;
                while (JoinNextPart())
                {
                }
            }
        }

        private bool JoinNextPart()
        {
            // Check for cancellation on every part.
            if (_loader.IsCanceled)
            {
                _loader.UpdateProgress(_status = _status.Cancel());
                Complete(null);
                return false;
            }

            if (_currentPartIndex >= CacheFilePaths.Count)
            {
                Complete(null);
                return false;
            }

            // If not cancelled, update progress.
            string cacheFilePath = CacheFilePaths[_currentPartIndex];
            string message = string.Format(Resources.ChromCacheJoiner_JoinNextPart_Joining_file__0__, cacheFilePath);
            int percent = _currentPartIndex * 100 / CacheFilePaths.Count;
            _status = _status.ChangeMessage(message).ChangePercentComplete(percent);
            _loader.UpdateProgress(_status);

            try
            {
                using (var inStream = _loader.StreamManager.CreateStream(cacheFilePath, FileMode.Open, false))
                {

                    if (_fs.Stream == null)
                        _fs.Stream = _loader.StreamManager.CreateStream(_fs.SafeName, FileMode.Create, true);

                    ChromatogramCache.RawData rawData;
                    long bytesData = ChromatogramCache.LoadStructs(inStream, out rawData, _assumeNegativeChargeInPreV11Caches);

                    // If joining, then format version should have already been checked.
                    Assume.IsTrue(ChromatogramCache.IsVersionCurrent(rawData.FormatVersion) ||
                        // WatersCacheTest uses older format partial caches
                        rawData.FormatVersion == ChromatogramCache.FORMAT_VERSION_CACHE_2);

                    int offsetFiles = _listCachedFiles.Count;
                    int offsetTransitions = _listTransitions.Count;
                    int offsetPeaks = _peakCount;
                    int offsetScores = _scoreCount;
                    long offsetPoints = _fs.Stream.Position;

                    // Scan ids
                    long offsetScanIds = _fsScans.Stream.Position;
                    _listCachedFiles.AddRange(rawData.ChromCacheFiles.Select(f => f.RelocateScanIds(f.LocationScanIds + offsetScanIds)));
                    if (rawData.CountBytesScanIds > 0)
                    {
                        inStream.Seek(rawData.LocationScanIds, SeekOrigin.Begin);
                        inStream.TransferBytes(_fsScans.Stream, rawData.CountBytesScanIds);
                    }

                    _peakCount += rawData.NumPeaks;
                    rawData.TransferPeaks(inStream, CacheFormat, 0, rawData.NumPeaks, _fsPeaks.FileStream);
                    _listTransitions.AddRange(rawData.ChromTransitions);
                    // Initialize the score types the first time through
                    if (_scoreTypesCount == -1)
                    {
                        _listScoreTypes = rawData.ScoreTypes;
                        _scoreTypesCount = _listScoreTypes.Count;
                    }
                    else if (!ArrayUtil.EqualsDeep(_listScoreTypes, rawData.ScoreTypes))
                    {
                        // If the existing score types in the caches are not the same, the caches cannot be joined
                        if (_listScoreTypes.Intersect(rawData.ScoreTypes).Count() != _listScoreTypes.Count)
                            throw new InvalidDataException(@"Data cache files with different score types cannot be joined.");
                    }
                    _scoreCount += rawData.NumScores;
                    inStream.Seek(rawData.LocationScoreValues, SeekOrigin.Begin);
                    inStream.TransferBytes(_fsScores.FileStream, rawData.NumScores * ChromatogramCache.SCORE_VALUE_SIZE);
                    for (int i = 0; i < rawData.ChromatogramEntries.Length; i++)
                    {
                        AddChromGroup(new ChromGroupHeaderEntry(i, rawData.RecalcEntry(i,
                                            offsetFiles,
                                            offsetTransitions,
                                            offsetPeaks,
                                            offsetScores,
                                            offsetPoints,
                                            _dictTextIdToByteIndex,
                                            _listTextIdBytes)));
                    }

                    inStream.Seek(0, SeekOrigin.Begin);

                    long copyBytes = bytesData;
                    while (copyBytes > 0)
                    {
                        int read = inStream.Read(_buffer, 0, (int)Math.Min(_buffer.Length, copyBytes));
                        _fs.Stream.Write(_buffer, 0, read);
                        copyBytes -= read;
                    }

                    _currentPartIndex++;
                    return true;
                }
            }
            catch (InvalidDataException x)
            {
                Complete(x);
            }
            catch (IOException x)
            {
                Complete(x);
            }
            catch (Exception x)
            {
                Complete(new Exception(String.Format(Resources.ChromCacheJoiner_JoinNextPart_Failed_to_create_cache__0__, CachePath), x));
            }
            return false;
        }
    }
}
