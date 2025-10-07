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

// TODO: periodically check whether drives associated with monitored directory paths are available. This helps detect and recover from
//       errors (ex: a network drive becomes unavailable).
// TODO: add a background task that refreshes the state of all items in the cache periodically. For example, when Skyline gains focus
//       after being minimized or after the user switches from a different application back to Skyline. 
namespace pwiz.Skyline.Controls.FilesTree
{
    public enum FileSystemType { in_memory, local_file_system, none }

    public interface IFileSystemService
    {
        FileSystemType FileSystemType { get; }

        IList<string> MonitoredDirectories();
        bool IsMonitoringDirectory(string fullPath);
        bool IsFileAvailable(string fullPath);

        void StartWatching(string directoryPath, CancellationToken cancellationToken);
        void LoadFile(string localFilePath, string filePath, string fileName, string documentPath, Action<string, CancellationToken> callback);
        void StopWatching();
        void Dispose();
    }

    public class FileSystemService : IFileSystemService
    {
        internal static FileSystemService Create(Control synchronizingObject,
            BackgroundActionService backgroundActionService,
            Action<string, CancellationToken> fileDeletedAction,
            Action<string, CancellationToken> fileCreatedAction,
            Action<string, string, CancellationToken> fileRenamedAction)
        {
            return new FileSystemService(synchronizingObject, backgroundActionService, fileDeletedAction, fileCreatedAction, fileRenamedAction);
        }

        private static readonly IFileSystemService NO_DOCUMENT = new DefaultService();

        private readonly Action<string, CancellationToken> _fileDeletedAction;
        private readonly Action<string, CancellationToken> _fileCreatedAction;
        private readonly Action<string, string, CancellationToken> _fileRenamedAction;
        private readonly Control _synchronizingObject;
        private readonly BackgroundActionService _backgroundActionService;

        private IFileSystemService _delegate;

        private FileSystemService(Control synchronizingObject,
            BackgroundActionService backgroundActionService,
            Action<string, CancellationToken> fileDeletedAction,
            Action<string, CancellationToken> fileCreatedAction,
            Action<string, string, CancellationToken> fileRenamedAction)
        {
            _synchronizingObject = synchronizingObject;
            _backgroundActionService = backgroundActionService;
            _fileDeletedAction = fileDeletedAction;
            _fileCreatedAction = fileCreatedAction;
            _fileRenamedAction = fileRenamedAction;

            // Start with the default implementation (NO_DOCUMENT) until Skyline has a document to monitor.
            _delegate = NO_DOCUMENT;
        }

        public FileSystemType FileSystemType => _delegate.FileSystemType;
        public bool IsMonitoringDirectory(string fullPath) => _delegate.IsMonitoringDirectory(fullPath);
        public bool IsFileAvailable(string fullPath) => _delegate.IsFileAvailable(fullPath);

        public IList<string> MonitoredDirectories()
        {
            Assume.IsNotNull(_delegate.MonitoredDirectories());
            return _delegate.MonitoredDirectories();
        }

        // CONSIDER: does this need to lock while updating _delegate?
        public void StartWatching(string directoryPath, CancellationToken cancellationToken)
        {
            // Console.WriteLine($@"===== StartWatching {directoryPath ?? @"in-memory"} {cancellationToken.GetHashCode()}");

            if (directoryPath == null)
                _delegate = new MemoryOnlyService(_backgroundActionService);
            else
                _delegate = new LocalStorageService(_synchronizingObject, _backgroundActionService, _fileDeletedAction, _fileCreatedAction, _fileRenamedAction);

            _delegate.StartWatching(directoryPath, cancellationToken);
        }

        public void LoadFile(string localFilePath, string filePath, string fileName, string documentPath, Action<string, CancellationToken> callback)
        {
            _delegate.LoadFile(localFilePath, filePath, fileName, documentPath, callback);
        }

        // CONSIDER: does this need to lock while updating _delegate?
        public void StopWatching()
        {
            // Console.WriteLine($@"===== StopWatching {MonitoredDirectories ?? @"in-memory"}");

            _delegate.StopWatching();

            // Revert to the default NO_DOCUMENT implementation until a document is loaded
            _delegate = NO_DOCUMENT;
        }

        public void Dispose()
        {
            _delegate.Dispose();
        }
    }

    internal class DefaultService : IFileSystemService
    {
        public FileSystemType FileSystemType => FileSystemType.none;
        public IList<string> MonitoredDirectories() => ImmutableList.Empty<string>();
        public bool IsMonitoringDirectory(string fullPath) => false;
        public bool IsFileAvailable(string fullPath) => false;
        public void StartWatching(string directoryPath, CancellationToken cancellationToken) { }
        public void LoadFile(string localFilePath, string filePath, string fileName, string documentPath, Action<string, CancellationToken> callback) { }
        public void StopWatching() { }
        public void Dispose() { }
    }

    internal class MemoryOnlyService : IFileSystemService
    {
        public MemoryOnlyService(BackgroundActionService backgroundActionService)
        {
            BackgroundActionService = backgroundActionService;
        }

        private BackgroundActionService BackgroundActionService { get; }
        private CancellationToken CancellationToken { get; set; }

        public FileSystemType FileSystemType => FileSystemType.in_memory;
        public IList<string> MonitoredDirectories() => ImmutableList.Empty<string>();
        public bool IsMonitoringDirectory(string fullPath) => fullPath == null;
        public bool IsFileAvailable(string fullPath) => true;

        public void StartWatching(string directoryPath, CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        public void LoadFile(string localFilePath, string filePath, string fileName, string documentPath, Action<string, CancellationToken> callback)
        {
            var cancellationToken = CancellationToken;
            BackgroundActionService.RunUI(() =>
            {
                callback(localFilePath, cancellationToken);
            });
        }

        public void StopWatching() { }
        public void Dispose() { }
    }

    public class LocalStorageService : IFileSystemService
    {
        private static readonly IList<string> FILE_EXTENSION_IGNORE_LIST = new List<string> { @".tmp", @".bak" };

        private readonly object _fswLock = new object();

        internal LocalStorageService(Control synchronizingObject,
            BackgroundActionService backgroundActionService,
            Action<string, CancellationToken> fileDeletedAction,
            Action<string, CancellationToken> fileCreatedAction,
            Action<string, string, CancellationToken> fileRenamedAction)
        {
            SynchronizingObject = synchronizingObject;
            BackgroundActionService = backgroundActionService;
            FileDeletedAction = fileDeletedAction;
            FileCreatedAction = fileCreatedAction;
            FileRenamedAction = fileRenamedAction;

            Cache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            FileSystemWatchers = new List<FileSystemWatcher>(1);
        }

        public FileSystemType FileSystemType => FileSystemType.local_file_system;

        /// <summary>
        /// Get the directories currently monitored for changes. Paths are fully qualified. The returned list is immutable.
        /// </summary>
        /// <returns>List of paths. If no paths monitored, list will be empty and non-null.</returns>
        public IList<string> MonitoredDirectories()
        {
            var list = new List<string>();
            lock (_fswLock)
            {
                list.AddRange(FileSystemWatchers.Select(watcher => Path.GetFullPath(watcher.Path)));
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
                fullPath = Path.GetDirectoryName(fullPath);
            }

            if (fullPath == null)
                return false;

            var canonicalFullPath = Path.GetFullPath(fullPath);

            var isMonitored = FileSystemWatchers.Any(watcher => IsInOrSubdirectoryOf(watcher.Path, canonicalFullPath));

            return isMonitored;
        }

        public bool IsFileAvailable(string fullPath)
        {
            fullPath = Path.GetFullPath(fullPath);

            return Cache.TryGetValue(fullPath, out var isAvailable) && isAvailable;
        }

        private Control SynchronizingObject { get; }
        private BackgroundActionService BackgroundActionService { get; }
        private ConcurrentDictionary<string, bool> Cache { get; }
        private CancellationToken CancellationToken { get; set; }
        private Action<string, CancellationToken> FileDeletedAction { get; }
        private Action<string, CancellationToken> FileCreatedAction { get; }
        private Action<string, string, CancellationToken> FileRenamedAction { get; }
        private IList<FileSystemWatcher> FileSystemWatchers { get; }

        public void StartWatching(string directoryPath, CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;

            lock (_fswLock)
            {
                var fileSystemWatcher = CreateWatcherForDirectory(directoryPath);
                FileSystemWatchers.Add(fileSystemWatcher);
            }
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
            // Not in the cache, so (1) determine the path to the local file and (2) if found, queue work to invoke the callback with info about the local file path.
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
                        localFilePath = Path.GetFullPath(localFilePath);

                        lock (_fswLock)
                        {
                            var directoryPath = Path.GetDirectoryName(localFilePath);
                            var isMonitored = IsMonitoringDirectory(directoryPath);
                            if (!isMonitored) 
                            {
                                var fileSystemWatcher = CreateWatcherForDirectory(directoryPath);
                                FileSystemWatchers.Add(fileSystemWatcher);
                            }

                            // Check whether directoryPath's parent directory is monitored. If not, start another FileSystemWatcher
                            // to see if directoryPath is renamed
                            directoryPath = Path.GetDirectoryName(directoryPath);
                            isMonitored = IsMonitoringDirectory(directoryPath);
                            if (!isMonitored)
                            {
                                var fileSystemWatcher = CreateWatcherForDirectory(directoryPath);
                                FileSystemWatchers.Add(fileSystemWatcher);
                            }
                        }

                        Cache[localFilePath] = true;
                    }
                    // Add files that could not be found to the cache (marked as missing) even though they couldn't be found. This records
                    // our potential interest in these paths since we cannot tell right now whether a network drive is down, the file will
                    // be restored later, and so on.
                    else
                    {
                        var fullFilePath = Path.GetFullPath(filePath);

                        Cache[fullFilePath] = false;
                    }

                    BackgroundActionService.RunUI(() =>
                    {
                        callback(localFilePath, cancellationToken);
                    });
                });
            }
        }

        public void StopWatching()
        {
            // It is a coding error if the caller does not cancel in-flight work before calling StopWatching
            Assume.IsTrue(CancellationToken.IsCancellationRequested);

            lock (_fswLock)
            {
                foreach (var watcher in FileSystemWatchers)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Renamed -= FileSystemWatcher_OnRenamed;
                    watcher.Deleted -= FileSystemWatcher_OnDeleted;
                    watcher.Created -= FileSystemWatcher_OnCreated;

                    watcher.Dispose();
                }

                FileSystemWatchers?.Clear();
            }

            Cache?.Clear();
        }

        public void Dispose()
        {
            if (FileSystemWatchers != null)
            {
                StopWatching();
            }

            Cache?.Clear();
        }

        private FileSystemWatcher CreateWatcherForDirectory(string directoryPath)
        {
            var fileSystemWatcher = new FileSystemWatcher();
            fileSystemWatcher.Path = directoryPath;
            fileSystemWatcher.SynchronizingObject = SynchronizingObject;
            fileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;

            // FileSystemService does not recursively monitor subdirectories. Instead, FileSystemWatcher instances are
            // started for each directory containing files Skyline wants to monitor. This approach is easier to 
            // reason about and avoids the downsides of inadvertently monitoring a large tree of files - for example,
            // if a replicate sample file happens to be imported from C:\ or from the root of a slow network drive.
            fileSystemWatcher.IncludeSubdirectories = false;

            fileSystemWatcher.Renamed += FileSystemWatcher_OnRenamed;
            fileSystemWatcher.Deleted += FileSystemWatcher_OnDeleted;
            fileSystemWatcher.Created += FileSystemWatcher_OnCreated;
            fileSystemWatcher.EnableRaisingEvents = true;

            return fileSystemWatcher;
        }

        private void FileSystemWatcher_OnDeleted(object sender, FileSystemEventArgs e)
        {
            var cancellationToken = CancellationToken;

            if (IgnoreFileName(e.FullPath) || cancellationToken.IsCancellationRequested)
                return;

            // Cannot check whether the changed path was a directory (since it was deleted) so 
            // start by looking for FullPath in the cache. If not found, check whether FullPath
            // may have contained items in the cache.
            if (Cache.ContainsKey(e.FullPath))
            {
                Cache[e.FullPath] = false;
            }
            else
            {
                foreach (var cacheKey in Cache.Keys)
                {
                    if (IsFileInDirectory(e.FullPath, cacheKey) && !File.Exists(cacheKey))
                    {
                        Cache[cacheKey] = false;
                    }
                }
            }

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested && FileDeletedAction != null)
                {
                    FileDeletedAction(e.FullPath, cancellationToken);
                }
            });
        }

        private void FileSystemWatcher_OnCreated(object sender, FileSystemEventArgs e)
        {
            var cancellationToken = CancellationToken;

            if (IgnoreFileName(e.FullPath) || cancellationToken.IsCancellationRequested)
                return;

            var isDirectory = Directory.Exists(e.FullPath);

            if (isDirectory)
            {
                foreach (var cacheKey in Cache.Keys)
                {
                    if (IsFileInDirectory(e.FullPath, cacheKey) && File.Exists(cacheKey))
                    {
                        Cache[cacheKey] = true;
                    }
                }
            }
            else
            {
                Cache[e.FullPath] = true;
            }

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested && FileCreatedAction != null)
                {
                    FileCreatedAction(e.FullPath, cancellationToken);
                }
            });
        }

        private void FileSystemWatcher_OnRenamed(object sender, RenamedEventArgs e)
        {
            var cancellationToken = CancellationToken;

            // Ignore file names with .bak / .tmp extensions as they are temporary files created during the save process.
            //
            // N.B. it's important to only ignore rename events where the new file name's extension is on the ignore list.
            // Otherwise, files that actually exist will be marked as missing in Files Tree without a way to force those
            // nodes to reset their FileState from disk leading to much confusion.
            if (IgnoreFileName(e.FullPath) || cancellationToken.IsCancellationRequested)
                return;

            var isDirectory = Directory.Exists(e.FullPath);

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
                Cache.Keys.Where(item => IsFileInDirectory(e.OldFullPath, item)).ForEach(item => Cache[item] = false);

                // files in the directory's new name _might_ exist but could have already been marked missing so 
                // do a real check whether the file exists
                foreach (var cacheKey in Cache.Keys)
                {
                    if (IsFileInDirectory(e.FullPath, cacheKey) && File.Exists(cacheKey))
                    {
                        Cache[cacheKey] = true;
                    }
                }
            }
            else
            {
                if (Cache.ContainsKey(e.OldFullPath))
                {
                    Cache[e.OldFullPath] = false;
                }
                else if (Cache.ContainsKey(e.FullPath))
                {
                    Cache[e.FullPath] = true;
                }
            }

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested && FileRenamedAction != null)
                {
                    FileRenamedAction(e.OldFullPath, e.FullPath, cancellationToken);
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
        internal static string LocateFile(string filePath, string fileName, string documentPath)
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
        public static bool IgnoreFileName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return true;

            var extension = Path.GetExtension(filePath);

            return FILE_EXTENSION_IGNORE_LIST.Contains(extension);
        }

        /// <summary>
        /// Check whether the <see cref="filePath"/> is contained in the <see cref="directoryPath"/>. The directory
        /// could contain the child file directly or in a nested subdirectory.
        /// </summary>
        /// <param name="directoryPath">Path to a directory</param>
        /// <param name="filePath">Path to a directory or a file</param>
        /// <returns>true if child contained in parent. False otherwise.</returns>
        /// CONSIDER: how should this handle a filePath that's too long and throws PathTooLongException?
        public static bool IsFileInDirectory(string directoryPath, string filePath)
        {
            var fullParentPath = Path.GetFullPath(directoryPath);
            
            if(!fullParentPath.EndsWith(Path.DirectorySeparatorChar.ToString()) && 
               !fullParentPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                fullParentPath += Path.DirectorySeparatorChar;
            }

            // Perform a case-insensitive comparison. Returns true if filePath is somewhere below directoryPath.
            return filePath.StartsWith(fullParentPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseDirectory"></param>
        /// <param name="potentialSubdirectory"></param>
        /// <returns></returns>
        /// CONSIDER: how should this handle a filePath that's too long and throws PathTooLongException?
        public static bool IsInOrSubdirectoryOf(string baseDirectory, string potentialSubdirectory)
        {
            // Normalize paths to ensure consistent comparison (e.g., handle relative paths, different separators)
            var normalizedPotentialSubdirectory = Path.GetFullPath(potentialSubdirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedBaseDirectory = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Add a directory separator to the base directory for accurate 'Contains' check
            // This prevents false positives where a directory name is a prefix of another (e.g., C:\Dir and C:\Directory)
            normalizedBaseDirectory += Path.DirectorySeparatorChar;

            // Perform a case-insensitive comparison. Returns true if potentialSubdirectory is somewhere below baseDirectory.
            return PathEquals(normalizedPotentialSubdirectory, normalizedBaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Compare two directory paths, remembering to ignore capitalization.
        /// </summary>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        /// <returns>true if the paths are the same; false otherwise</returns>
        public static bool PathEquals(string path1, string path2)
        {
            return string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);
        }
    }
}