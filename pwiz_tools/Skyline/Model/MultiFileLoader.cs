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
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class MultiFileLoader
    {
        private const int MAX_PARALLEL_LOAD_FILES = 8;

        private readonly QueueWorker<LoadInfo> _worker;
        private readonly Dictionary<MsDataFileUri, int> _loadingPaths;
        private readonly bool _synchronousMode;
        private int _threadCount;

        private readonly object _statusLock;
        private MultiProgressStatus _status;

        public MultiFileLoader(bool synchronousMode)
        {
            _worker = new QueueWorker<LoadInfo>(null, LoadFile);
            _loadingPaths = new Dictionary<MsDataFileUri, int>();
            _synchronousMode = synchronousMode;
            _threadCount = 1;
            _statusLock = new object();
            ResetStatus();
        }

        public MultiProgressStatus Status { get { return _status; } }

        public MultiProgressStatus ChangeStatus(ChromatogramLoadingStatus loadingStatus)
        {
            return ChangeStatus(s => s.ChangeStatus(loadingStatus));
        }

        public MultiProgressStatus CompleteStatus()
        {
            return ChangeStatus(s => (MultiProgressStatus) s.Complete());
        }

        public MultiProgressStatus CancelStatus()
        {
            return ChangeStatus(s => (MultiProgressStatus)s.Cancel());
        }

        public void ResetStatus()
        {
            ChangeStatus(s => new MultiProgressStatus(_synchronousMode));
        }

        private MultiProgressStatus ChangeStatus(Func<MultiProgressStatus, MultiProgressStatus> change)
        {
            lock (_statusLock)
            {
                return _status = change(_status);
            }
        }

        // User is presented these options for file load parallelism
        public enum ImportResultsSimultaneousFileOptions
        {
            one_at_a_time, several, many
        }

        public void InitializeThreadCount(int? loadingThreads)
        {
            if (ParallelEx.SINGLE_THREADED)
            {
                loadingThreads = 1; // Makes debugging easier.
            }

            if (loadingThreads.HasValue)
                _threadCount = loadingThreads.Value;
            else
            {
                switch ((ImportResultsSimultaneousFileOptions)Settings.Default.ImportResultsSimultaneousFiles)
                {
                    case ImportResultsSimultaneousFileOptions.one_at_a_time:
                        _threadCount = 1;
                        break;

                    case ImportResultsSimultaneousFileOptions.several: // Several is 1/4 logical processors (i.e. 2 for an i7)
                        _threadCount = Math.Max(2, Environment.ProcessorCount / 4); // Min of 2, because you really expect more than 1
                        break;

                    case ImportResultsSimultaneousFileOptions.many: // Many is 1/2 logical processors (i.e. 4 for an i7)
                        _threadCount = Math.Max(2, Environment.ProcessorCount / 2); // Min of 2, because you really expect more than 1
                        break;
                }
                _threadCount = Math.Min(MAX_PARALLEL_LOAD_FILES, _threadCount);
            }
        }

        /// <summary>
        /// Add the given file to the queue of files to load.
        /// </summary>
        public void Load(
            IList<DataFileReplicates> loadList,
            SrmDocument document,
            string documentFilePath,
            ChromatogramCache cacheRecalc,
            MultiFileLoadMonitor loadMonitor,
            Action<ChromatogramCache, IProgressStatus> complete)
        {
            // This may be called on multiple background loader threads simultaneously, but QueueWorker can handle it.
            _worker.RunAsync(_threadCount, "Load file"); // Not L10N

            lock (this)
            {
                // Find non-duplicate paths to load.
                var uniqueLoadList = new List<DataFileReplicates>();
                foreach (var loadItem in loadList)
                {
                    // Ignore a file that is already being loaded (or is queued for loading).
                    if (_loadingPaths.ContainsKey(loadItem.DataFile))
                        continue;
                    int idIndex = document.Id.GlobalIndex;
                    _loadingPaths.Add(loadItem.DataFile, idIndex);
                    uniqueLoadList.Add(loadItem);
                }

                if (uniqueLoadList.Count == 0)
                    return;

                // Add new paths to queue.
                foreach (var loadItem in uniqueLoadList)
                {
                    var loadingStatus = new ChromatogramLoadingStatus(loadItem.DataFile, loadItem.ReplicateList);

                    ChangeStatus(s => s.Add(loadingStatus));

                    // Queue work item to load the file.
                    _worker.Add(new LoadInfo
                    {
                        Path = loadItem.DataFile,
                        PartPath = loadItem.PartPath,
                        Document = document,
                        DocumentFilePath = documentFilePath,
                        CacheRecalc = cacheRecalc,
                        Status = loadingStatus,
                        LoadMonitor = new SingleFileLoadMonitor(loadMonitor, loadItem.DataFile),
                        Complete = complete
                    });
                }
            }
        }

        public void DoneAddingFiles()
        {
            _worker.DoneAdding(true);
        }

        public void ClearFile(MsDataFileUri filePath)
        {
            lock (this)
            {
                _loadingPaths.Remove(filePath);
                if (_loadingPaths.Count == 0)
                    ResetStatus();
            }
        }

        public bool CompleteDocument(SrmDocument document, ILoadMonitor loadMonitor)
        {
            // Refuse completion, if anything is still not final
            var status = Status;
            var notFinal = status.ProgressList.Where(s => !s.IsFinal).ToArray();
            if (notFinal.Any())
            {
                // Write output in attempt to associate new hangs in nightly tests with the return false below
//                Console.WriteLine(TextUtil.LineSeparate("*** Attempt to complete document with non-final status ***",   // Not L10N
//                    TextUtil.LineSeparate(notFinal.Select(s => string.Format("{0} {1}% - {2}", s.State, s.PercentComplete, s.FilePath))))); // Not L10N
                return false;
            }

            if (status.ProgressList.Any())
                loadMonitor.UpdateProgress(CompleteStatus());
            ClearDocument(document);
            return true;
        }

        public void ClearDocument(SrmDocument document)
        {
            lock (this)
            {
                foreach (var filePath in _loadingPaths.Where(p => p.Value == document.Id.GlobalIndex).Select(p => p.Key).ToArray())
                {
                    _loadingPaths.Remove(filePath);
                }
                if (_loadingPaths.Count == 0)
                    ResetStatus();
            }
        }

        public bool AnyLoading()
        {
            lock (this)
            {
                return _loadingPaths.Any();
            }
        }

        public bool IsLoading(SrmDocument document)
        {
            lock (this)
            {
                return _loadingPaths.ContainsValue(document.Id.GlobalIndex);
            }
        }

        private class LoadInfo
        {
            public MsDataFileUri Path;
            public string PartPath;
            public SrmDocument Document;
            public string DocumentFilePath;
            public ChromatogramCache CacheRecalc;
            public IProgressStatus Status;
            public ILoadMonitor LoadMonitor;
            public Action<ChromatogramCache, IProgressStatus> Complete;
        }

        private void LoadFile(LoadInfo loadInfo, int threadIndex)
        {
            ChromatogramCache.Build(
                loadInfo.Document,
                loadInfo.DocumentFilePath,
                loadInfo.CacheRecalc,
                loadInfo.PartPath,
                loadInfo.Path,
                loadInfo.Status,
                loadInfo.LoadMonitor,
                loadInfo.Complete);

            var loadingStatus = loadInfo.Status as ChromatogramLoadingStatus;
            if (loadingStatus != null)
                loadingStatus.Transitions.Flush();
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

        public override UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            var loadingStatus = status as ChromatogramLoadingStatus;
            if (loadingStatus != null)
            {
                // If the ChromatogramManager has alread had its status reset, avoid calling
                // UpdateProgress with the empty status
                var multiStatus = _chromatogramManager.ChangeStatus(loadingStatus);
                if (multiStatus.IsEmpty)
                    return UpdateProgressResponse.normal;
                status = multiStatus;
            }
            var progressResult = _chromatogramManager.UpdateProgress(status);
            return progressResult;
        }

        public bool IsCanceledFile(MsDataFileUri filePath)
        {
            return IsCanceledItem(filePath);
        }
    }

    public class SingleFileLoadMonitor : BackgroundLoader.LoadMonitor
    {
        private readonly MultiFileLoadMonitor _loadMonitor;
        private readonly MsDataFileUri _dataFile;
        private DateTime _lastCancelCheck;
        private bool _isCanceled;

        public SingleFileLoadMonitor(MultiFileLoadMonitor loadMonitor, MsDataFileUri dataFile)
        {
            _loadMonitor = loadMonitor;
            _dataFile = dataFile;
            _lastCancelCheck = DateTime.UtcNow; // Said to be 117x faster than Now and this is for a delta
            HasUI = loadMonitor.HasUI;
        }

        public override IStreamManager StreamManager
        {
            get { return _loadMonitor.StreamManager; }
        }

        public override bool IsCanceled
        {
            get
            {
                // Once set always true
                if (_isCanceled)
                    return true;
                // Checking for whether a file is canceled showed up in the profiler for
                // large DIA runs with lots of chromatograms being extracted. So, here
                // we prevent this check from happening more than once every 10 ms
                // which is actually long enough to get this under 1% of really huge
                // extractions.
                var currentTime = DateTime.UtcNow;
                if ((currentTime - _lastCancelCheck).TotalMilliseconds < 10)
                    return false;
                _lastCancelCheck = currentTime;
                return _isCanceled = _loadMonitor.IsCanceledFile(_dataFile);
            }
        }

        public override UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            return _loadMonitor.UpdateProgress(status);
        }
    }
}
