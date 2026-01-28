/*
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

// TODO: refresh all items in the cache periodically and when Skyline regains focus. Also consider restarting the FileSystemWatchers.
namespace pwiz.Skyline.Controls.FilesTree
{
    public enum FileSystemType { local_file_system, none }
    public enum PathAvailability { available, unavailable, unknown }

    public interface IFileSystemService
    {
        FileSystemType FileSystemType { get; }

        IList<string> MonitoredDirectories();
        bool IsMonitoringDirectory(string fullPath);
        bool IsFileAvailable(string fullPath);
        bool IsFileSystemWatchingComplete();
        bool IsWatching();
        void StartWatching(CancellationToken cancellationToken);
        void LoadFile(string localFilePath, string filePath, string fileName, string documentPath, Action<string, CancellationToken> callback);
        void StopWatching();
        void Dispose();
    }

    public class FileSystemService : IFileSystemService
    {
        private static readonly IFileSystemService NO_DOCUMENT = new NoOpService();

        private readonly Control _synchronizingObject;
        private readonly BackgroundActionService _backgroundActionService;

        internal event Action<string, CancellationToken> FileDeletedAction;
        internal event Action<string, CancellationToken> FileCreatedAction;
        internal event Action<string, string, CancellationToken> FileRenamedAction;

        internal FileSystemService(Control synchronizingObject, BackgroundActionService backgroundActionService)
        {
            _synchronizingObject = synchronizingObject;
            _backgroundActionService = backgroundActionService;

            // Use the no-op implementation until a document loads.
            Delegate = NO_DOCUMENT;
        }

        public FileSystemType FileSystemType => Delegate.FileSystemType;
        public IFileSystemService Delegate { get; private set; }

        public bool IsMonitoringDirectory(string fullPath) => Delegate.IsMonitoringDirectory(fullPath);
        public bool IsFileAvailable(string fullPath) => Delegate.IsFileAvailable(fullPath);
        public bool IsFileSystemWatchingComplete() => Delegate.IsFileSystemWatchingComplete();
        public bool IsWatching() => Delegate.IsWatching();

        public IList<string> MonitoredDirectories()
        {
            Assume.IsNotNull(Delegate.MonitoredDirectories());
            return Delegate.MonitoredDirectories();
        }

        /// <summary>
        /// Start the file system monitor. 
        /// </summary>
        /// <param name="cancellationToken"></param>
        // CONSIDER: does this need to lock while updating Delegate?
        public void StartWatching(CancellationToken cancellationToken)
        {
            // Dispose old delegate if switching from a previous LocalFileSystemService
            var oldDelegate = Delegate;

            var localStorageService = new LocalFileSystemService(_synchronizingObject, _backgroundActionService);
            localStorageService.FileDeletedAction += FileDeletedAction;
            localStorageService.FileCreatedAction += FileCreatedAction;
            localStorageService.FileRenamedAction += FileRenamedAction;
            Delegate = localStorageService;

            // Dispose old delegate AFTER switching to new one to prevent race conditions
            oldDelegate?.Dispose();

            Delegate.StartWatching(cancellationToken);
        }

        public void LoadFile(string localFilePath, string filePath, string fileName, string documentPath, Action<string, CancellationToken> callback)
        {
            Delegate.LoadFile(localFilePath, filePath, fileName, documentPath, callback);
        }

        /// <summary>
        /// Stop monitoring the file system and switch back to the no-op implementation until another Skyline document loads.
        /// </summary>
        // CONSIDER: does this need to lock while updating Delegate?
        public void StopWatching()
        {
            var oldDelegate = Delegate;
            Delegate = NO_DOCUMENT;
            oldDelegate.Dispose();  // Dispose AFTER switching to NO_DOCUMENT to prevent race conditions
        }

        public void Dispose()
        {
            Delegate.Dispose();
        }
    }

    /// <summary>
    /// No-op implementation of the file system monitor used when no Skyline document is loaded - e.g. when looking at Skyline's start page.
    /// </summary>
    internal class NoOpService : IFileSystemService
    {
        public FileSystemType FileSystemType => FileSystemType.none;
        public IList<string> MonitoredDirectories() => ImmutableList.Empty<string>();
        public bool IsMonitoringDirectory(string fullPath) => false;
        public bool IsFileAvailable(string fullPath) => false;
        public bool IsFileSystemWatchingComplete() => true; // NoOpService never watches anything
        public bool IsWatching() => false; // NoOpService never watches anything
        public void StartWatching(CancellationToken cancellationToken) { }
        public void LoadFile(string localFilePath, string filePath, string fileName, string documentPath, Action<string, CancellationToken> callback) { }
        public void StopWatching() { }
        public void Dispose() { }
    }

    /// <summary>
    /// Implementation of the file system monitor used when a Skyline document is loaded. Usually, the .sky document is
    /// saved to disk, though File => New creates a Skyline document that exists only in-memory.
    /// </summary>
    public class LocalFileSystemService : IFileSystemService
    {
        private static readonly HashSet<string> FILE_EXTENSION_IGNORE_LIST = new HashSet<string> { @".tmp", @".bak" };

        private readonly object _fswLock = new object();
        private bool _isStopped;

        public LocalFileSystemService(Control synchronizingObject, BackgroundActionService backgroundActionService)
        {
            SynchronizingObject = synchronizingObject;
            BackgroundActionService = backgroundActionService;

            Cache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            FileSystemWatchers = new ConcurrentDictionary<string, ManagedFileSystemWatcher>();

            // Use a different timespan when running tests. 
            // CONSIDER: should tests configure this directly?
            var timeSpan = Program.FunctionalTest ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(3);
            HealthMonitor = new FileSystemHealthMonitor(timeSpan);
            HealthMonitor.PathAvailabilityChanged += OnPathAvailabilityChanged;
        }

        internal event Action<string, CancellationToken> FileDeletedAction;
        internal event Action<string, CancellationToken> FileCreatedAction;
        internal event Action<string, string, CancellationToken> FileRenamedAction;

        private Control SynchronizingObject { get; }
        private BackgroundActionService BackgroundActionService { get; }
        private ConcurrentDictionary<string, bool> Cache { get; }
        private CancellationToken CancellationToken { get; set; }
        private ConcurrentDictionary<string, ManagedFileSystemWatcher> FileSystemWatchers { get; }
        private FileSystemHealthMonitor HealthMonitor { get; set; }

        public FileSystemType FileSystemType => FileSystemType.local_file_system;

        /// <summary>
        /// Get the directories currently monitored for changes. Paths are fully qualified. The returned list is immutable.
        /// </summary>
        /// <returns>List of paths. If no paths monitored, list will be non-null and empty.</returns>
        public IList<string> MonitoredDirectories()
        {
            var list = new List<string>();
            lock (_fswLock)
            {
                list.AddRange(FileSystemWatchers.Select(item => item.Value.Path));
            }

            return ImmutableList.ValueOfOrEmpty(list);
        }

        public bool IsMonitoringDirectory(string fullPath)
        {
            if(fullPath == null) 
                return false;

            // If fullPath is a file, get its directory
            if (File.Exists(fullPath))
            {
                fullPath = FileSystemUtil.GetDirectoryOrRoot(fullPath);
            }

            if (fullPath == null)
                return false;

            return FileSystemWatchers.ContainsKey(fullPath);
        }

        public bool IsFileAvailable(string fullPath)
        {
            fullPath = FileSystemUtil.Normalize(fullPath);

            return Cache.TryGetValue(fullPath, out var isAvailable) && isAvailable;
        }

        public void TriggerAvailabilityMonitor()
        {
            HealthMonitor.Trigger();
        }

        public void StartWatching(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            HealthMonitor.Start();
            _isStopped = false;
        }

        public bool IsWatching()
        {
            return !_isStopped;
        }

        /// <summary>
        /// Attempt to locate the specified file on the local file system. If the file is found, two things happen:
        ///
        ///     (1) an entry is stored in a cache so the file does not need to be located again, saving unnecessary file system access.
        ///     (2) the callback is invoked on the UI thread, passing the path to the local file, a flag indicating whether the file is
        ///         available, and the cancellation token.
        ///
        /// If no local file is found, nothing is stored in the cache so the file will be searched for again the next time a caller
        /// tries to locate the file.
        ///
        /// To locate a local file, this searches common ways Skyline finds local files. These searches are not exhaustive and may not find all the files
        /// Skyline can find. Expect to expand on this method as edge cases are found.
        /// </summary>
        /// <param name="localFilePath">Where the caller thinks the local file is located.</param>
        /// <param name="filePath">The path to the local file. N.B. this path usually comes from SrmDocument and may not specify a path that exists locally.</param>
        /// <param name="fileName">The name of the file to find.</param>
        /// <param name="documentPath">A local path to the current .sky file.</param>
        /// <param name="callback">Callback to invoke if the file is found in the cache or if the given <see cref="filePath"/> and <see cref="fileName"/> can be mapped to a local file.</param>
        public void LoadFile(string localFilePath, string filePath, string fileName, string documentPath, Action<string, CancellationToken> callback)
        {
            var cancellationToken = CancellationToken;

            // See if the file is already loaded into the cache - if so, queue work to invoke the callback with the cached file state.
            if (localFilePath != null && Cache.TryGetValue(localFilePath, out _))
            {
                Assume.IsFalse(SynchronizingObject.InvokeRequired);

                BackgroundActionService.RunUI(() =>
                {
                    callback(localFilePath, cancellationToken);
                });
            }
            // Not in the cache, so queue a new background task to:
            //
            //  (1) determine the path to the local file
            //  (2) update the UI with the results of locating the local file
            else
            {
                BackgroundActionService.AddTask(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    localFilePath = LocateFile(filePath, fileName, documentPath);

                    // Check whether this file's location is monitored for changes. If not, start a new FileSystemWatcher for that location
                    if (localFilePath != null)
                    {
                        localFilePath = FileSystemUtil.Normalize(localFilePath);

                        // Use GetDirectoryOrRoot to handle cases where localFilePath is in a root directory - ex: C:\sample-file-123.raw
                        var directoryPath = FileSystemUtil.GetDirectoryOrRoot(localFilePath);
                        WatchDirectory(directoryPath);

                        Cache[localFilePath] = true;
                    }
                    // Add files that could not be found to the cache (marked as missing) even though they couldn't be found. This records
                    // our potential interest in these paths since we cannot tell right now whether a network drive is down, the file will
                    // be restored later, and so on.
                    else if(filePath != null)
                    {
                        var fullFilePath = FileSystemUtil.Normalize(filePath);

                        Cache[fullFilePath] = false;
                    }
                    // Otherwise, filePath is null, which typically indicates a file that has never been saved. So, do not 
                    // put it in the cache (because it has no file path) but do queue a task to update the UI.

                    BackgroundActionService.RunUI(() =>
                    {
                        callback(localFilePath, cancellationToken);
                    });
                });
            }
        }

        /// <summary>
        /// Handle events raised by the <see cref="FileSystemHealthMonitor"/>. Triggered when the availability of a
        /// directory monitored by FileSystemWatcher becomes available or unavailable.
        ///
        /// If a directory becomes unavailable, the FileSystemWatcher monitoring the directory will be paused until the
        /// directory becomes available again when the FSW will be restarted.
        /// </summary>
        /// <param name="fullPath">The affected path.</param>
        /// <param name="availability">Whether the path is available.</param>
        private void OnPathAvailabilityChanged(string fullPath, PathAvailability availability)
        {
            BackgroundActionService.AddTask(() =>
            {
                // Directory became unavailable so pause the associated FileSystemWatcher and update the tree
                if (availability == PathAvailability.unavailable)
                {
                    if (FileSystemWatchers.TryGetValue(fullPath, out var managedFsw))
                    {
                        managedFsw.Pause();

                        HandleDirectoryUnavailable(fullPath);
                    }
                }
                // Directory became available so restart the associated FileSystemWatcher and update the tree
                else if (availability == PathAvailability.available)
                {
                    if (FileSystemWatchers.TryGetValue(fullPath, out var managedFsw))
                    {
                        if (managedFsw.IsPaused)
                        {
                            managedFsw.Resume();

                            HandleDirectoryAvailable(fullPath);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Safely invoke a FileSystemWatcher event handler. Guards against race conditions where
        /// FSW events arrive after StopWatching() has started cleanup. FSW callbacks run on I/O
        /// completion port threads, so unhandled exceptions would terminate the process.
        /// See GitHub issue #3845.
        /// </summary>
        private void SafeInvokeFswHandler(Action action)
        {
            if (_isStopped)
                return;

            try
            {
                action();
            }
            catch (ObjectDisposedException)
            {
                // Expected during cleanup race conditions when _isStopped check passes but
                // StopWatching() disposes resources before the action completes. Silently ignore.
            }
            catch (Exception ex)
            {
                // Report programming defects (NRE, IndexOutOfRange, etc.) so tests fail on bugs.
                // Silently ignore expected FSW exceptions (IOException from network disconnects, etc.).
                // Files view is auxiliary UI; its background processing should never interrupt user workflow.
                if (ExceptionUtil.IsProgrammingDefect(ex))
                    Program.ReportException(ex);
            }
        }

        private void FileSystemWatcher_OnError(object sender, ErrorEventArgs e)
        {
            SafeInvokeFswHandler(() =>
            {
                var fsw = sender as FileSystemWatcher;
                if (fsw == null)
                    return;
                HandleFswError(fsw.Path, e.GetException());
            });
        }

        private void HandleFswError(string directoryPath, Exception exception)
        {
            // Likely means the directory is unavailable so find all affected files, mark their cache entries as unavailable, and update the tree
            if (exception is IOException)
            {
                HandleDirectoryUnavailable(directoryPath);
            }
        }

        public void StopWatching()
        {
            // It is a coding error if the caller does not cancel in-flight work before calling StopWatching
            Assume.IsTrue(CancellationToken.IsCancellationRequested);

            // Set stopped flag first to signal FSW event handlers to exit early.
            // This prevents race conditions where events arrive while cleanup is in progress.
            _isStopped = true;

            lock (_fswLock)
            {
                foreach (var kvPair in FileSystemWatchers)
                {
                    var watcher = kvPair.Value;

                    watcher.Stop();
                    watcher.Renamed -= FileSystemWatcher_OnRenamed;
                    watcher.Deleted -= FileSystemWatcher_OnDeleted;
                    watcher.Created -= FileSystemWatcher_OnCreated;
                    watcher.Error -= FileSystemWatcher_OnError;
                    watcher.Dispose();
                }

                FileSystemWatchers?.Clear();
            }

            HealthMonitor.Stop();
            HealthMonitor.Dispose();

            Cache?.Clear();
        }

        /// <summary>
        /// Returns true if file system watching is completely shut down:
        /// - All FileSystemWatchers are disposed
        /// - HealthMonitor thread is stopped
        /// - BackgroundActionService tasks are complete
        /// </summary>
        public bool IsFileSystemWatchingComplete()
        {
            if (!_isStopped)
                return false;

            lock (_fswLock)
            {
                if (FileSystemWatchers.Count > 0)
                    return false;
            }

            if (HealthMonitor != null && !HealthMonitor.IsStopped())
                return false;

            return BackgroundActionService.IsComplete;
        }

        public void Dispose()
        {
            StopWatching();

            HealthMonitor?.Dispose();
            HealthMonitor = null;

            Cache?.Clear();
        }

        /// <summary>
        /// Start a <see cref="ManagedFileSystemWatcher"/> for <see cref="directoryPath"/> and tracks the new watcher in <see cref="FileSystemWatchers"/>.
        /// Does not start a new <see cref="ManagedFileSystemWatcher"/> if one is already running for the directory.
        ///
        /// Callers who want to monitor a directory containing a file should use <see cref="FileSystemUtil.GetDirectoryOrRoot"/> to get the directory
        /// containing the file.
        /// </summary>
        /// <param name="directoryPath">The directory to watch.</param>
        private void WatchDirectory(string directoryPath)
        {
            if (directoryPath == null)
                return;

            // Lock around the entire check-and-add operation to prevent race condition
            // where two threads both pass IsMonitoringDirectory check before either adds to dictionary
            lock (_fswLock)
            {
                if (IsMonitoringDirectory(directoryPath))
                    return;

                var managedFsw = new ManagedFileSystemWatcher(directoryPath);

                managedFsw.Renamed += FileSystemWatcher_OnRenamed;
                managedFsw.Deleted += FileSystemWatcher_OnDeleted;
                managedFsw.Created += FileSystemWatcher_OnCreated;
                managedFsw.Error += FileSystemWatcher_OnError;

                managedFsw.Start();

                FileSystemWatchers[directoryPath] = managedFsw;
            }

            HealthMonitor.AddPath(directoryPath);
        }

        private void HandleDirectoryUnavailable(string directoryPath) 
        {
            var cancellationToken = CancellationToken;

            foreach (var cacheKey in Cache.Keys)
            {
                if (FileSystemUtil.IsFileInDirectory(directoryPath, cacheKey) && !File.Exists(cacheKey))
                {
                    Cache[cacheKey] = false;
                }
            }

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested && FileDeletedAction != null)
                {
                    FileDeletedAction(directoryPath, cancellationToken);
                }
            });
        }

        private void HandleDirectoryAvailable(string directoryPath)
        {
            var cancellationToken = CancellationToken;

            foreach (var cacheKey in Cache.Keys)
            {
                if (FileSystemUtil.IsFileInDirectory(directoryPath, cacheKey) && File.Exists(cacheKey))
                {
                    Cache[cacheKey] = true;
                }
            }

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested && FileCreatedAction != null)
                {
                    FileCreatedAction(directoryPath, cancellationToken);
                }
            });
        }

        private void FileSystemWatcher_OnDeleted(object sender, FileSystemEventArgs e)
        {
            SafeInvokeFswHandler(() => HandleFileDeleted(e.FullPath));
        }

        private void HandleFileDeleted(string fullPath)
        {
            var cancellationToken = CancellationToken;

            if (ShouldIgnoreFile(fullPath) || cancellationToken.IsCancellationRequested)
                return;

            // Cannot check whether the changed path was a directory (since it was deleted) so
            // start by looking for FullPath in the cache. If not found, check whether FullPath
            // may have contained items in the cache.
            if (Cache.ContainsKey(fullPath))
            {
                Cache[fullPath] = false;
            }
            else
            {
                foreach (var cacheKey in Cache.Keys)
                {
                    if (FileSystemUtil.IsFileInDirectory(fullPath, cacheKey) && !File.Exists(cacheKey))
                    {
                        Cache[cacheKey] = false;
                    }
                }
            }

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested && FileDeletedAction != null)
                {
                    FileDeletedAction(fullPath, cancellationToken);
                }
            });
        }

        private void FileSystemWatcher_OnCreated(object sender, FileSystemEventArgs e)
        {
            SafeInvokeFswHandler(() => HandleFileCreated(e.FullPath));
        }

        private void HandleFileCreated(string fullPath)
        {
            var cancellationToken = CancellationToken;

            if (ShouldIgnoreFile(fullPath) || cancellationToken.IsCancellationRequested)
                return;

            var isDirectory = Directory.Exists(fullPath);

            if (isDirectory)
            {
                foreach (var cacheKey in Cache.Keys)
                {
                    if (FileSystemUtil.IsFileInDirectory(fullPath, cacheKey) && File.Exists(cacheKey))
                    {
                        Cache[cacheKey] = true;
                    }
                }
            }
            else
            {
                Cache[fullPath] = true;
            }

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested && FileCreatedAction != null)
                {
                    FileCreatedAction(fullPath, cancellationToken);
                }
            });
        }

        private void FileSystemWatcher_OnRenamed(object sender, RenamedEventArgs e)
        {
            SafeInvokeFswHandler(() => HandleFileRenamed(e.OldFullPath, e.FullPath));
        }

        private void HandleFileRenamed(string oldFullPath, string fullPath)
        {
            var cancellationToken = CancellationToken;

            // Ignore file names with .bak / .tmp extensions as they are temporary files created during the save process.
            //
            // N.B. it's important to only ignore rename events where the new file name's extension is on the ignore list.
            // Otherwise, files that actually exist will be marked as missing in Files Tree without a way to force those
            // nodes to reset their FileState from disk leading to much confusion.
            if (ShouldIgnoreFile(fullPath) || cancellationToken.IsCancellationRequested)
                return;

            var isDirectory = Directory.Exists(fullPath);

            /*
            c:\tmp\foo1.raw
            c:\tmp\foo2.raw

            rename c:\tmp to c:\tmp2
                old: c:\tmp
                new: c:\tmp2

            find all files with old name, mark as missing
            find all files with new name, mark as available
             */
            if (isDirectory)
            {
                // files in the directory's old name no longer exist at the expected path
                Cache.Keys.Where(item => FileSystemUtil.IsFileInDirectory(oldFullPath, item)).ForEach(item => Cache[item] = false);

                // files in the directory's new name _might_ exist but could have already been marked missing so
                // do a real check whether the file exists
                foreach (var cacheKey in Cache.Keys)
                {
                    if (FileSystemUtil.IsFileInDirectory(fullPath, cacheKey) && File.Exists(cacheKey))
                    {
                        Cache[cacheKey] = true;
                    }
                }
            }
            else
            {
                if (Cache.ContainsKey(oldFullPath))
                {
                    Cache[oldFullPath] = false;
                }
                else if (Cache.ContainsKey(fullPath))
                {
                    Cache[fullPath] = true;
                }
            }

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested && FileRenamedAction != null)
                {
                    FileRenamedAction(oldFullPath, fullPath, cancellationToken);
                }
            });
        }

        /// Find Skyline files on the local file system.
        ///
        /// SkylineFiles uses this approach to locate file paths found in SrmSettings. It starts with
        /// the given path but those paths may be set on others machines. If not available locally, use
        /// <see cref="PathEx.FindExistingRelativeFile"/> to search for the file locally.
        ///
        // Looks for a file on the file system. Can make one or more calls to File.Exists or Directory.Exists while 
        // checking places Skyline might have stored the file. Should be called from a worker thread to avoid blocking the UI.
        // TODO: what other ways does Skyline use to find files of various types? For example, Chromatogram.GetExistingDataFilePath or other possible locations for spectral libraries
        internal string LocateFile(string filePath, string fileName, string documentPath)
        {
            // Given a file path c:\tmp\foo.txt and document path c:\abc\def\ghi\doc.sky
            //
            // (1) Does a given file exist at its fully qualified path?
            //      If yes, localPath = filePath
            //
            // (2) Look elsewhere starting with the documentFilePath
            //      (2.1) Does foo.text exist c:\abc\def\ghi
            //          If yes, localPath = c:\abc\def\ghi\foo.txt
            //      (2.2) Does foo.txt exist in c:\abc\def
            //          If yes, localPath = c:\abc\def\foo.txt
            //      (2.3) Does foo.txt exist in c:\abc
            //          If yes, localPath = c:\abc\foo.txt
            //
            // (3) Otherwise, foo.txt does not exist

            string path;

            // ReSharper disable once ConvertIfStatementToReturnStatement
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (File.Exists(filePath))
            {
                path = filePath;
            }
            else
            {
                path = PathEx.FindExistingRelativeFile(documentPath, fileName);
            }

            return path;
        }

        // FileSystemWatcher events may reference files we should ignore. For example, .tmp or .bak files
        // created when saving a Skyline document or view file. So, check paths against an ignore list.
        public static bool ShouldIgnoreFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return true;

            var extension = Path.GetExtension(filePath);

            return FILE_EXTENSION_IGNORE_LIST.Contains(extension);
        }
    }
}