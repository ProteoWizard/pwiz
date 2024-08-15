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
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class MultiFileLoader : IDisposable
    {
        public const int MAX_PARALLEL_LOAD_FILES = 12;
        public const int MAX_PARALLEL_LOAD_FILES_USER_GC = 3; // On some systems we find that parallel performance suffers when not using ServerGC, as during SkylineTester runs

        private readonly QueueWorker<LoadInfo> _worker;
        private readonly Dictionary<MsDataFileUri, int> _loadingPaths;
        private readonly bool _synchronousMode;
        private int? _threadCountPreferred;
        private ImportResultsSimultaneousFileOptions _simultaneousFileOptions;

        private readonly object _statusLock;
        private MultiProgressStatus _status;

        public MultiFileLoader(bool synchronousMode)
        {
            _worker = new QueueWorker<LoadInfo>(null, LoadFile);
            _loadingPaths = new Dictionary<MsDataFileUri, int>();
            _synchronousMode = synchronousMode;
            _statusLock = new object();
            ResetStatus();
        }

        public void Dispose()
        {
            _worker?.Dispose();
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
            lock (this)
            {
                _threadCountPreferred = loadingThreads;
                _simultaneousFileOptions = (ImportResultsSimultaneousFileOptions)Settings.Default.ImportResultsSimultaneousFiles;
            }
        }

        public static int GetOptimalThreadCount(int? loadingThreads, int? fileCount,
            ImportResultsSimultaneousFileOptions simultaneousFileOptions)
        {
            if (ParallelEx.SINGLE_THREADED)
                return 1; // Makes debugging easier.

            return GetOptimalThreadCount(loadingThreads, fileCount, Environment.ProcessorCount,
                simultaneousFileOptions);
        }

        public static int GetMaxLoadThreadCount()
        {
            // On some systems we find that parallel performance suffers when not using ServerGC, as during SkylineTester runs, so we lower the max thread count
            return System.Runtime.GCSettings.IsServerGC ? MAX_PARALLEL_LOAD_FILES : MAX_PARALLEL_LOAD_FILES_USER_GC;
        }

        public static int GetOptimalThreadCount(int? loadingThreads, int? fileCount, int processorCount, ImportResultsSimultaneousFileOptions simultaneousFileOptions)
        {
            if (!loadingThreads.HasValue)
            {
                switch (simultaneousFileOptions)
                {
                    case ImportResultsSimultaneousFileOptions.one_at_a_time:
                        loadingThreads = 1;
                        break;

                    case ImportResultsSimultaneousFileOptions.several: // Several is 1/4 logical processors (i.e. 2 for an i7)
                        loadingThreads = Math.Max(2, processorCount / 4); // Min of 2, because you really expect more than 1
                        break;

                    case ImportResultsSimultaneousFileOptions.many: // Many is 1/2 logical processors (i.e. 4 for an i7)
                        loadingThreads = Math.Max(2, processorCount / 2); // Min of 2, because you really expect more than 1
                        break;
                }

                // On some systems we find that parallel performance suffers when not using ServerGC, as during SkylineTester runs
                var maxLoadThreads = GetMaxLoadThreadCount();
                loadingThreads = Math.Min(maxLoadThreads, loadingThreads.Value);
                loadingThreads = GetBalancedThreadCount(loadingThreads.Value, fileCount);
            }

            return loadingThreads.Value;
        }

        private static int GetBalancedThreadCount(int loadingThreads, int? fileCount)
        {
            // If we know the file count, and the number of threads is greater than 2
            // attempt to optimize load balancing
            if (loadingThreads < 3 || !fileCount.HasValue)
                return loadingThreads;

            int files = fileCount.Value;
            int fullCycles = files / loadingThreads;
            int remainder = files % loadingThreads;
            int totalCycles = fullCycles + (remainder == 0 ? 0 : 1);
            // While there is a remainder (i.e. files do not divide evenly into the available threads)
            // and reducing the thread count by 1 would not increase the total cycle count
            while (remainder > 0)
            {
                int reducedThreads = loadingThreads - 1;
                var revisedFullCycles = files / reducedThreads;
                var revisedRemainder = files % reducedThreads;
                var revisedTotalCycles = revisedFullCycles + (revisedRemainder == 0 ? 0 : 1);
                if (totalCycles != revisedTotalCycles)
                    break;
                loadingThreads = reducedThreads;
                remainder = fileCount.Value % loadingThreads;
            }
            return loadingThreads;
        }

        /// <summary>
        /// Add the given files to the queue of files to load.
        /// </summary>
        public void Load(
            IList<DataFileReplicates> loadList,
            SrmDocument document,
            string documentFilePath,
            ChromatogramCache cacheRecalc,
            MultiFileLoadMonitor loadMonitor,
            Action<IList<FileLoadCompletionAccumulator.Completion>> complete)
        {
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

                int threadCount = GetOptimalThreadCount(_threadCountPreferred, uniqueLoadList.Count, _simultaneousFileOptions);
                _worker.RunAsync(threadCount, @"Load file");

                var accumulator = new FileLoadCompletionAccumulator(complete, threadCount, uniqueLoadList.Count);

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
                        Complete = accumulator.Complete
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
//                Console.WriteLine(TextUtil.LineSeparate(@"*** Attempt to complete document with non-final status ***",
//                    TextUtil.LineSeparate(notFinal.Select(s => string.Format(@"{0} {1}% - {2}", s.State, s.PercentComplete, s.FilePath)))));
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

    public class FileLoadCompletionAccumulator
    {
        public class Completion
        {
            public Completion(ChromatogramCache cache, IProgressStatus status)
            {
                Cache = cache;
                Status = status;
            }

            public ChromatogramCache Cache { get; private set; }
            public IProgressStatus Status { get; private set; }
        }

        private readonly QueueWorker<Completion> _completionWorker;
        private readonly Action<IList<Completion>> _complete;
        private readonly int _threadCount;
        private readonly int _loadingCount;
        private int _completedCount;
        private int _consumedCount;
        private List<Completion> _accumulatedCompletions;

        public FileLoadCompletionAccumulator(Action<IList<Completion>> complete, int threadCount, int loadingCount)
        {
            _complete = complete;
            _threadCount = threadCount;
            _loadingCount = loadingCount;
            // If there are multiple threads or even multiple files make committing
            // completed caches asynchronous to the loading process. With multiple
            // threads this will also commit only every time all threads complete a
            // file.
            if (_threadCount > 1 && _loadingCount > 1)
            {
                _completionWorker = new QueueWorker<Completion>(null, ConsumeCompletion);
                _completionWorker.RunAsync(1, @"Commit loaded files");
                _accumulatedCompletions = new List<Completion>();
            }
        }

        private void ConsumeCompletion(Completion completion, int threadIndex)
        {
            _consumedCount++;
            _accumulatedCompletions.Add(completion);
            if (_consumedCount == _loadingCount || _accumulatedCompletions.Count == _threadCount)
            {
                _complete(_accumulatedCompletions);
                _accumulatedCompletions.Clear();
            }
            // Report errors and cancellations as soon as possible
            else if (!completion.Status.IsComplete)
            {
                _complete(new SingletonList<Completion>(completion));
                // Add an empty completion, which will be ignored during batch
                _accumulatedCompletions[_accumulatedCompletions.Count - 1] = new Completion(null, new ProgressStatus());
            }
        }

        public void Complete(ChromatogramCache cache, IProgressStatus status)
        {
            var completedCount = Interlocked.Increment(ref _completedCount);
            var completion = new Completion(cache, status);
            if (_completionWorker == null)
                _complete(new SingletonList<Completion>(completion));
            else
            {
                _completionWorker.Add(completion);

                if (completedCount == _loadingCount)
                    _completionWorker.DoneAdding();
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
