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
using System.Diagnostics;
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class ChromCacheJoiner : ChromCacheWriter
    {
        private int _currentPartIndex = -1;
        private long _copyBytes;
        private Stream _inStream;
        private readonly byte[] _buffer = new byte[0x40000];  // 256K

        public ChromCacheJoiner(string cachePath, IPooledStream streamDest,
                                IList<string> cacheFilePaths, ILoadMonitor loader, ProgressStatus status,
                                Action<ChromatogramCache, Exception> completed)
            : base(cachePath, loader, status, completed)
        {
            _destinationStream = streamDest;

            CacheFilePaths = cacheFilePaths;
        }

        private IList<string> CacheFilePaths { get; set; }

        public void JoinParts()
        {
            lock (this)
            {
                if (_currentPartIndex != -1)
                    return;
                _currentPartIndex = 0;
                JoinNextPart();
            }
        }

        private void JoinNextPart()
        {
            lock (this)
            {
                if (_currentPartIndex >= CacheFilePaths.Count)
                {
                    Complete(null);
                    return;
                }

                // Check for cancellation on every part.
                if (_loader.IsCanceled)
                {
                    _loader.UpdateProgress(_status = _status.Cancel());
                    Complete(null);
                    return;
                }

                // If not cancelled, update progress.
                string cacheFilePath = CacheFilePaths[_currentPartIndex];
                string message = String.Format("Joining file {0}", cacheFilePath);
                int percent = _currentPartIndex * 100 / CacheFilePaths.Count;
                _status = _status.ChangeMessage(message).ChangePercentComplete(percent);
                _loader.UpdateProgress(_status);

                try
                {
                    _inStream = _loader.StreamManager.CreateStream(cacheFilePath, FileMode.Open, false);

                    if (_outStream == null)
                        _outStream = _loader.StreamManager.CreateStream(_fs.SafeName, FileMode.Create, true);

                    int formatVersion;
                    ChromCachedFile[] chromCacheFiles;
                    ChromGroupHeaderInfo[] chromatogramEntries;
                    ChromTransition[] chromTransitions;
                    ChromPeak[] chromatogramPeaks;

                    long bytesData = ChromatogramCache.LoadStructs(_inStream,
                                                                   out formatVersion,
                                                                   out chromCacheFiles,
                                                                   out chromatogramEntries,
                                                                   out chromTransitions,
                                                                   out chromatogramPeaks);

                    // If joining, then format version should have already been checked.
                    Debug.Assert(ChromatogramCache.IsVersionCurrent(formatVersion) ||
                        // WatersCacheTest uses older format partial caches
                        formatVersion == ChromatogramCache.FORMAT_VERSION_CACHE_2);

                    int offsetFiles = _listCachedFiles.Count;
                    int offsetTransitions = _listTransitions.Count;
                    int offsetPeaks = _listPeaks.Count;
                    long offsetPoints = _outStream.Position;

                    _listCachedFiles.AddRange(chromCacheFiles);
                    _listPeaks.AddRange(chromatogramPeaks);
                    _listTransitions.AddRange(chromTransitions);
                    for (int i = 0; i < chromatogramEntries.Length; i++)
                        chromatogramEntries[i].Offset(offsetFiles, offsetTransitions, offsetPeaks, offsetPoints);
                    _listGroups.AddRange(chromatogramEntries);

                    _copyBytes = bytesData;
                    _inStream.Seek(0, SeekOrigin.Begin);

                    CopyInToOut();
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
                    Complete(new Exception(String.Format("Failed to create cache '{0}'.", CachePath), x));
                }
            }
        }

        private void CopyInToOut()
        {
            if (_copyBytes > 0)
            {
                _inStream.BeginRead(_buffer, 0, (int)Math.Min(_buffer.Length, _copyBytes),
                                    FinishRead, null);
            }
            else
            {
                try { _inStream.Close(); }
                catch (IOException) { }
                _inStream = null;

                _currentPartIndex++;
                JoinNextPart();
            }
        }

        private void FinishRead(IAsyncResult ar)
        {
            try
            {
                int read = _inStream.EndRead(ar);
                if (read == 0)
                    throw new IOException(String.Format("Unexpected end of file in {0}.", CacheFilePaths[_currentPartIndex]));
                _copyBytes -= read;
                _outStream.BeginWrite(_buffer, 0, read, FinishWrite, null);
            }
            catch (Exception x)
            {
                Complete(x);
            }
        }

        private void FinishWrite(IAsyncResult ar)
        {
            try
            {
                _outStream.EndWrite(ar);
                CopyInToOut();
            }
            catch (Exception x)
            {
                Complete(x);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_inStream != null)
            {
                try { _inStream.Close(); }
                catch (IOException) { }
            }
        }
    }
}