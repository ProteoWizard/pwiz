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
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal class ChromCacheWriter : IDisposable
    {
        private readonly Action<ChromatogramCache, Exception> _completed;

        protected readonly List<ChromCachedFile> _listCachedFiles = new List<ChromCachedFile>();
        protected readonly List<ChromPeak> _listPeaks = new List<ChromPeak>();
        protected readonly List<ChromTransition> _listTransitions = new List<ChromTransition>();
        protected readonly List<ChromGroupHeaderInfo> _listGroups = new List<ChromGroupHeaderInfo>();
        protected readonly FileSaver _fs;
        protected readonly ILoadMonitor _loader;
        protected ProgressStatus _status;
        protected Stream _outStream;
        protected IPooledStream _destinationStream;

        protected ChromCacheWriter(string cachePath, ILoadMonitor loader, ProgressStatus status,
                                   Action<ChromatogramCache, Exception> completed)
        {
            CachePath = cachePath;
            _fs = new FileSaver(CachePath);
            _loader = loader;
            _status = status;
            _completed = completed;
        }

        protected string CachePath { get; private set; }

        protected void Complete(Exception x)
        {
            lock (this)
            {
                ChromatogramCache result = null;
                try
                {
                    if (x == null && !_status.IsFinal)
                    {
                        if (_outStream != null)
                        {
                            ChromatogramCache.WriteStructs(_outStream, _listCachedFiles, _listGroups, _listTransitions, _listPeaks);

                            _loader.StreamManager.Finish(_outStream);
                            _outStream = null;
                            _fs.Commit(_destinationStream);
                        }

                        // Create stream identifier, but do not open.  The stream will be opened
                        // the first time the document uses it.
                        var readStream = _loader.StreamManager.CreatePooledStream(CachePath, false);

                        result = new ChromatogramCache(CachePath,
                                                       ChromatogramCache.FORMAT_VERSION_CACHE,
                                                       _listCachedFiles.ToArray(),
                                                       _listGroups.ToArray(),
                                                       _listTransitions.ToArray(),
                                                       _listPeaks.ToArray(),
                                                       readStream);
                        _loader.UpdateProgress(_status.Complete());
                    }
                }
                catch (Exception x2)
                {
                    x = x2;
                }
                finally
                {
                    Dispose();
                }

                _completed(result, x);
            }
        }

        public virtual void Dispose()
        {
            if (_outStream != null)
            {
                try { _loader.StreamManager.Finish(_outStream); }
                catch (IOException) { }
            }
            _fs.Dispose();
        }
    }
}