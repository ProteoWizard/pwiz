/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class MultiFileLoader
    {
        private readonly QueueWorker<LoadInfo> _worker;
        private readonly Dictionary<string, int> _loadingPaths;
        private int _fileCount;

        public MultiFileLoader()
        {
            _worker = new QueueWorker<LoadInfo>(null, LoadFile);
            _loadingPaths = new Dictionary<string, int>();
        }

        public int ThreadCount { get; set; }
 
        /// <summary>
        /// Add the given file to the queue of files to load.
        /// </summary>
        public void Load(
            IList<KeyValuePair<string, string>> pathPairs,
            SrmDocument document, 
            ChromatogramCache cacheRecalc, 
            MultiFileLoadMonitor loader,
            Action<ChromatogramCache, IProgressStatus> complete)
        {
            // There must be at least 1 background thread. Otherwise, all work happens synchronously,
            // and it is not possible to stop processing a set of files once the lock below is entered.
            Assume.IsTrue(ThreadCount > 0);

            // This may be called on multiple background loader threads simultaneously, but QueueWorker can handle it.
            _worker.RunAsync(ThreadCount, "Load file"); // Not L10N
            
            lock (this)
            {
                // Find non-duplicate paths to load.
                var newPairs = new List<KeyValuePair<string, string>>();
                foreach (var pair in pathPairs)
                {
                    var path = pair.Key;

                    // Ignore a file that is already being loaded (or is queued for loading).
                    if (_loadingPaths.ContainsKey(path))
                        continue;
                    int idIndex = document.Id.GlobalIndex;
                    _loadingPaths.Add(path, idIndex);
                    newPairs.Add(pair);
                }

                // Add new paths to queue.
                foreach (var pair in newPairs)
                {
                    var path = pair.Key;
                    var partPath = pair.Value;
                    var status = new ChromatogramLoadingStatus(path, ++_fileCount);
                    loader.Add(status);

                    // Queue work item to load the file.
                    _worker.Add(new LoadInfo
                    {
                        Path = path,
                        PartPath = partPath,
                        Document = document,
                        CacheRecalc = cacheRecalc,
                        Status = status,
                        Loader = loader,
                        Complete = complete
                    });
                }
            }
        }

        public void Reset()
        {
            _fileCount = 0;
        }

        public void DoneAddingFiles()
        {
            if (_worker != null)
                _worker.DoneAdding(true);
        }

        public void Cancel()
        {
            lock (this)
            {
                _worker.Clear();
                _loadingPaths.Clear();
                _fileCount = 0;
            }
        }

        public void ClearFile(string filePath)
        {
            lock (this)
            {
                _loadingPaths.Remove(filePath);
            }
        }

        public bool AllowJoin(SrmDocument document)
        {
            lock (this)
            {
                return !_loadingPaths.ContainsValue(document.Id.GlobalIndex);
            }
        }

        private class LoadInfo
        {
            public string Path;
            public string PartPath;
            public SrmDocument Document;
            public ChromatogramCache CacheRecalc;
            public IProgressStatus Status;
            public ILoadMonitor Loader;
            public Action<ChromatogramCache, IProgressStatus> Complete;
        }

        private void LoadFile(LoadInfo loadInfo, int threadIndex)
        {
            ChromatogramCache.Build(
                loadInfo.Document,
                loadInfo.CacheRecalc,
                loadInfo.PartPath,
                MsDataFileUri.Parse(loadInfo.Path),
                loadInfo.Status,
                loadInfo.Loader,
                loadInfo.Complete);

            lock (this)
            {
                // Remove from list of loading files.
                _loadingPaths.Remove(loadInfo.Path);
            }
        }
    }

    public class MultiFileLoadMonitor : BackgroundLoader.LoadMonitor
    {
        private readonly ChromatogramManager _chromatogramManager;
        public MultiFileLoadMonitor(ChromatogramManager manager, IDocumentContainer container, object tag)
            : base(manager, container, tag)
        {
            _chromatogramManager = manager;
        }

        public void Add(ChromatogramLoadingStatus status)
        {
            _chromatogramManager.Status = _chromatogramManager.Status.Add(status);
        }

        public override UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            var loadingStatus = status as ChromatogramLoadingStatus;
            UpdateProgressResponse progressResult;
            if (loadingStatus != null)
            {
                _chromatogramManager.Status = _chromatogramManager.Status.ChangeStatus(loadingStatus);
                progressResult = _chromatogramManager.UpdateProgress(_chromatogramManager.Status);
            }
            else
            {
                progressResult = _chromatogramManager.UpdateProgress(status);
            }
            switch (progressResult)
            {
                case UpdateProgressResponse.cancel:
                    _chromatogramManager.Cancel();
                    break;
                case UpdateProgressResponse.option1:
                    // Retry.
                    _chromatogramManager.RemoveFile(status);
                    break;
                case UpdateProgressResponse.option2:
                    // Skip.
                    _chromatogramManager.RemoveFile(status);
                    break;
            }
            return progressResult;
        }
    }
}
