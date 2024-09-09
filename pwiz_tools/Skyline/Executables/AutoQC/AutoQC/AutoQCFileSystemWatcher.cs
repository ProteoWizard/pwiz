/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{

    public class AutoQCFileSystemWatcher
    {
        private readonly Logger _logger;
        private readonly ConfigRunner _configRunner;

        private IResultFileStatus _fileStatusChecker;

        // Collection of new mass spec files to be processed.
        private ConcurrentQueue<string> _dataFiles;
        // Collection of mass spec files that resulted in an error while importing
        private Queue<RawFile> _retryFiles;
        private HashSet<string> _retryFilePaths;

        private FileSystemWatcher _fileWatcher;
        private bool _dataInDirectories;

        private bool _cancelled;
        private ErrorEventArgs _fileWatcherError;
        private DriveInfo _driveInfo;
        private DateTime _lastFileEvent;

        private int _acquisitionTimeSetting;
        private FileFilter _fileFilter;

        private string _configName;

        private const int WAIT_60SEC = 60000;

        private const string THERMO_EXT = ".raw";
        private const string SCIEX_EXT = ".wiff";
        private const string SCIEX_WIFF2_EXT = ".wiff2";
        private const string WATERS_EXT = ".raw";
        private const string AGILENT_EXT = ".d";
        private const string BRUKER_EXT = ".D";
        private const string SHIAMDZU_EXT = ".lcd";

        private bool _includeSubfolders;
        private string _instrument;

        public AutoQCFileSystemWatcher(Logger logger, ConfigRunner configRunner)
        {
            _fileWatcher = InitFileSystemWatcher();

            _logger = logger;
            _configRunner = configRunner;
        }

        private FileSystemWatcher InitFileSystemWatcher()
        {
            var fileWatcher = new FileSystemWatcher();
            fileWatcher.Created += (s, e) => FileAdded(e);
            fileWatcher.Renamed += (s, e) => FileRenamed(e);
            fileWatcher.Error += (s, e) => OnFileWatcherError(e);
            return fileWatcher;
        }

        public void Init(AutoQcConfig config)
        {
            _configName = config.Name;

            var mainSettings = config.MainSettings;
            _dataFiles = new ConcurrentQueue<string>();
            _retryFiles = new Queue<RawFile>();
            _retryFilePaths = new HashSet<string>();

            _fileStatusChecker = GetFileStatusChecker(mainSettings);

            _fileWatcher.EnableRaisingEvents = false;

            if (mainSettings.IncludeSubfolders)
            {
                _fileWatcher.IncludeSubdirectories = true;
                _includeSubfolders = true;
            }

            _fileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime;

            _instrument = mainSettings.InstrumentType;
            _dataInDirectories = IsDataInDirectories(_instrument);

            _fileWatcher.Filter = GetFileFilter(mainSettings.InstrumentType);

            _fileWatcher.Path = mainSettings.FolderToWatch;

            _fileFilter = mainSettings.QcFileFilter;

            _acquisitionTimeSetting = mainSettings.AcquisitionTime;

            _driveInfo = new DriveInfo(_fileWatcher.Path);
        }

        public static bool IsDataInDirectories(string instrument)
        {
            return (instrument.Equals(MainSettings.WATERS) // Waters: .raw directory
                    || instrument.Equals(MainSettings.AGILENT) // Agilent: .d directory
                    || instrument.Equals(MainSettings.BRUKER)); // Bruker: .D directory
        }

        public void StartWatching()
        {
            _lastFileEvent = DateTime.Now; // In case of an error, if we have to restart FileSystemWatcher, we will look for files added after this time

            _cancelled = false;
            // Begin watching.
            _fileWatcher.EnableRaisingEvents = true;
        }

        private static IResultFileStatus GetFileStatusChecker(MainSettings mainSettings)
        {
            return new AcquisitionTimeFileStatus(mainSettings.AcquisitionTime);
        }

        private static string GetFileFilter(string instrument)
        {
            return "*" + GetDataFileExt(instrument);
        }

        public static string GetDataFileExt(string instrument)
        {
            switch (instrument)
            {
                case MainSettings.THERMO:
                    return THERMO_EXT;
                case MainSettings.SCIEX:
                    return SCIEX_EXT;
                case MainSettings.SCIEX_WIFF2:
                    return SCIEX_WIFF2_EXT;
                case MainSettings.WATERS:
                    return WATERS_EXT; // Waters: .raw directory
                case MainSettings.AGILENT:
                    return AGILENT_EXT; // Agilent: .d directory
                case MainSettings.BRUKER:
                    return BRUKER_EXT; // Bruker: .D directory
                case MainSettings.SHIMADZU:
                    return SHIAMDZU_EXT;
                default:
                    return ".*";
            }
        }

        private void Restart()
        {
            if (_cancelled)
            {
                _logger.Log(Resources.AutoQCFileSystemWatcher_Restart_FileSystemWatcher_canceled_);
                return;
            }

            _logger.Log(Resources.AutoQCFileSystemWatcher_Restart_Re_initializing_FileSystemWatcher___);

            var filter = _fileWatcher.Filter;
            var path = _fileWatcher.Path;
            var notifyFilter = _fileWatcher.NotifyFilter;

            _fileWatcher.Dispose();
            _fileWatcher = null;
            _fileWatcher = InitFileSystemWatcher();
            _fileWatcher.Filter = filter;
            _fileWatcher.Path = path;
            _fileWatcher.NotifyFilter = notifyFilter;
            // _fileWatcher.InternalBufferSize = 64 * 1024;
            _fileWatcher.IncludeSubdirectories = _includeSubfolders;

            _logger.Log(string.Format(Resources.AutoQCFileSystemWatcher_Restart_Getting_raw_data_added_since__0__, _lastFileEvent));

            var files = GetExistingFiles();

            _fileWatcherError = null;

            var lastTime = _lastFileEvent;
            _lastFileEvent = DateTime.Now;

            _fileWatcher.EnableRaisingEvents = true;

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);

                if (lastTime >= fileInfo.CreationTime)
                {
                    continue;
                }

                _logger.Log(string.Format(Resources.AutoQCFileSystemWatcher_Restart_Adding__0__, fileInfo.Name));
                _dataFiles.Enqueue(file);
            }
            _logger.Log("Done");
        }

        public void Stop()
        {
            _cancelled = true;
            _fileWatcher.EnableRaisingEvents = false;
        }

        private void FileRenamed(FileSystemEventArgs e)
        {
            var name = Path.GetFileName(e.FullPath.TrimEnd(Path.DirectorySeparatorChar));
            if (name.ToLower(CultureInfo.InvariantCulture).
                EndsWith(GetDataFileExt(_instrument).ToLower(CultureInfo.InvariantCulture)))
            {
                FileAdded(e);
            }
        }

        private void FileAdded(FileSystemEventArgs e)
        {
            _lastFileEvent = DateTime.Now;

            var path = e.FullPath;

            if (!PassesFileFilter(path))
            {
                return;
            }

            if ((_dataInDirectories && Directory.Exists(path))
                || (!_dataInDirectories && File.Exists(path)))
            {
                _logger.Log(string.Format(Resources.AutoQCFileSystemWatcher_FileAdded__0__added_to_directory_, e.Name));
                _dataFiles.Enqueue(e.FullPath);   
            }
        }

        private void OnFileWatcherError(ErrorEventArgs e)
        {
            var folder = _fileWatcher != null ? _fileWatcher.Path : "UNKNOWN";
            _logger.LogError(string.Format(Resources.AutoQCFileSystemWatcher_OnFileWatcherError_There_was_an_error_watching_the_folder__0__, folder), e.GetException().ToString());
            _fileWatcherError = e;
        }

        public bool RawDataExists(string filePath)
        {
            return _dataInDirectories ? Directory.Exists(filePath) : File.Exists(filePath);
        }

        public void WaitForFileReady(string filePath)
        {
            _cancelled = false;

            var counter = 0;
            while (true)
            {
                CheckDrive();

                if (!RawDataExists(filePath))
                {
                    throw new FileStatusException(string.Format("{0} {1}", filePath, FileStatusException.DOES_NOT_EXIST));
                }

                var fileStatus= _fileStatusChecker.CheckStatus(filePath);
                if (fileStatus.Equals(FileStatus.Ready))
                {
                    break;
                }

                if (fileStatus.Equals(FileStatus.ExceedMaximumAcquiTime))
                {
                    throw new FileStatusException("Data acquisition has exceeded the expected acquisition time." +
                                        "The instrument probably encountered an error.");
                }

                if (counter % 10 == 0)
                {
                    _logger.Log(string.Format(Resources.AutoQCFileSystemWatcher_WaitForFileReady__0__is_being_acquired__Waiting___, Path.GetFileName(filePath)));
                }
                counter++;
                // Wait for 60 seconds.
                Thread.Sleep(WAIT_60SEC);
                if (_cancelled)
                {
                    _logger.Log(Resources.AutoQCFileSystemWatcher_Restart_FileSystemWatcher_canceled_);
                    return;
                }
            }
            _logger.Log(string.Format(Resources.AutoQCFileSystemWatcher_WaitForFileReady__0__is_ready, Path.GetFileName(filePath)));
        }

        public string GetFile()
        {
            CheckDrive();

            if (_dataFiles.IsEmpty)
            {
                return null;
            }
                
            string filePath;
            _dataFiles.TryDequeue(out filePath);
            return filePath;
        }
        
        public void CheckDrive()
        {  
            if (!_driveInfo.IsMappedNetworkDrive())
            {
                if (!_driveInfo.IsUncPath())
                {
                    if (!Directory.Exists(_fileWatcher.Path))
                    {
                        throw new FileWatcherException(string.Format("Folder does not exist {0}. Configuration \"{1}\"", _fileWatcher.Path, _configName));
                    }
                    if (_fileWatcherError != null)
                    {
                        throw new FileWatcherException(
                            string.Format("There was an error watching the folder {0}. Configuration \"{1}\"",
                                _fileWatcher.Path, _configName),
                            _fileWatcherError.GetException());
                    }
                    return;
                }
                
                // We are watching a path on a network share.  Wait to reconect for up to an hour in case of network interruptions.
                WaitForNetworkPath(DateTime.Now);
                
                return;
            }
            
            // If we are watching a mapped network drive, make sure that we can still connect to it.
            // If we lose connection to a mapped network drive, FileSystemWatcher does not fire any new events
            // even after the connection is re-established.
            try
            {
                TryConnect(DateTime.Now);
            }
            catch (FileWatcherException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new FileWatcherException(string.Format("Error connecting to network drive. Message was: {0}.", e.Message), e);  
            }

            if (_fileWatcherError != null)
            {
                // Error may have been caused by the network drive getting disconnected.  If we are here
                // it means another config watching a folder on the same mapped drive re-mapped the drive.
                if (Directory.Exists(_fileWatcher.Path))
                {
                    ProgramLog.Info(string.Format(Resources.AutoQCFileSystemWatcher_CheckDrive_Restarting_file_watcher_for_configuration___0___, _configName));
                    RestartFileWatcher();
                }
                else
                {
                    // Some other error occurred.  Stop the configuration with an error message
                    throw new FileWatcherException(string.Format("File watcher for configuration \"{0}\" threw an error", _configName), _fileWatcherError.GetException());
                }
            }

            if (!Directory.Exists(_driveInfo.Path))
            {
                // The mapped network drive is accessible but the watched folder is no longer available.
                throw new FileWatcherException(string.Format("Folder does not exist {0}. Configuration \"{1}\"", _fileWatcher.Path, _configName));
            }
        }

        private void TryConnect(DateTime startTime)
        {
            if (startTime.AddHours(1) < DateTime.Now)
            {
                throw new FileWatcherException(string.Format("Unable to connect to network drive for 1 hour. Network Drive: {0}. Configuration \"{1}\".", _driveInfo, _configName));   
            }

            bool reconnected;
            var driveAvailable = NetworkDriveUtil.EnsureDrive(_driveInfo, _logger, out reconnected, _configName);
            if (driveAvailable && _configRunner.IsDisconnected())
            {
                _configRunner.ChangeStatus(RunnerStatus.Running);
            }

            if (!driveAvailable)
            {
                if (_configRunner.IsRunning())
                {
                    _configRunner.ChangeStatus(RunnerStatus.Disconnected);
                }
                // keep trying every 1 minute for a hour or until the drive is available again.  
                Thread.Sleep(TimeSpan.FromMinutes(1));

                TryConnect(startTime);
            }

            if (reconnected)
            {
                ProgramLog.Info(string.Format(Resources.AutoQCFileSystemWatcher_TryConnect_Re_connected_drive__0__for_configuration___1_____Restarting_file_watcher_, _driveInfo,
                    _configName));
                RestartFileWatcher();
            }
        }

        private void WaitForNetworkPath(DateTime startTime)
        {
            var available = Directory.Exists(_driveInfo.Path);
            if (available)
            {
                if (_fileWatcherError != null)
                {
                    // There may have been a network interruption before call to CheckDrive()
                    RestartFileWatcher();
                }
                return;
            }

            if (_fileWatcherError == null)
            {
                // Path is unavailable.  Disable raising events
                _fileWatcher.EnableRaisingEvents = false;
            }
            _configRunner.ChangeStatus(RunnerStatus.Disconnected);

            while (true)
            {
                _logger.LogError(string.Format(Resources.AutoQCFileSystemWatcher_WaitForNetworkPath_Watched_folder_is_not_available___0___Waiting___, _driveInfo.Path));

                // keep trying every 1 minute for a hour or until the network share is available again. 
                Thread.Sleep(TimeSpan.FromMinutes(1));

                available = Directory.Exists(_driveInfo.Path);

                if (!available)
                {
                    if (startTime.AddHours(1) < DateTime.Now)
                    {
                        throw new FileWatcherException(string.Format("Watched folder has been unavailable for > 1 hour for configuration \"{1}\". {0}", _driveInfo, _configName));
                    }
                }
                else
                {
                    _logger.Log(string.Format(Resources.AutoQCFileSystemWatcher_WaitForNetworkPath_Watched_folder_is_available___0__, _driveInfo.Path));

                    if (_configRunner.IsDisconnected())
                    {
                        _configRunner.ChangeStatus(RunnerStatus.Running);
                    }
                    RestartFileWatcher();
                    break;
                }   
            }
        }

        private void RestartFileWatcher()
        {
            try
            {
                Restart();
            }
            catch (Exception e)
            {
                throw new FileWatcherException(string.Format("Error restarting file watcher for configuration \"{0}\"", _configName), e);
            }   
        }

        public List<string> GetExistingFiles()
        {
            var rawData = _dataInDirectories ? GetDirectories(_fileWatcher.Path, GetDataFileExt(_instrument)) 
                                             : GetFiles(_fileWatcher.Path);
            rawData.RemoveAll(data => !PassesFileFilter(data));
            return rawData;
        }

        private List<string> GetDirectories(string directory, string dataDirExt)
        {
            var dataDirs = new List<string>();
            
            if (_includeSubfolders)
            {
                var subdirs = Directory.GetDirectories(directory);
                foreach (var subdir in subdirs)
                {
                    if (subdir.EndsWith(dataDirExt))
                    {
                        dataDirs.Add(subdir);
                    }
                    else
                    {
                        dataDirs.AddRange(GetDirectories(subdir, dataDirExt));
                    }
                }      
            }
            else
            {
                dataDirs.AddRange(Directory.GetDirectories(directory, _fileWatcher.Filter));  
            }

            return dataDirs;
        }

        private List<string> GetFiles(string directory)
        {
            var files = new List<string>();
            files.AddRange(Directory.GetFiles(directory, _fileWatcher.Filter));

            if (!_includeSubfolders) return files;

            var subdirs = Directory.GetDirectories(directory);
            foreach (var subdir in subdirs)
            {
                files.AddRange(GetFiles(subdir));   
            }
            return files;
        }

        private bool PassesFileFilter(string dataPath)
        {
            if (_fileFilter == null)
            {
                return true;
            }

            return _fileFilter.Matches(dataPath);
        }

        public void AddToReimportQueue(RawFile file)
        {
            if (!_retryFilePaths.Contains(file.FilePath))
            {
                _retryFiles.Enqueue(file);
                _retryFilePaths.Add(file.FilePath);
            }
            else
            {
                _logger.Log(string.Format(Resources.AutoQCFileSystemWatcher_AddToReimportQueue_File_already_exists_in_re_import_queue___0_, file.FilePath));
            }
        }

        public void AddToReimportQueue(string filePath)
        {
            var rawFile = new RawFile(filePath, DateTime.Now, GetReimportDelay());
            AddToReimportQueue(rawFile);
        }

        private long GetReimportDelay()
        {
            return (long) (_acquisitionTimeSetting * 0.1 * 60 * 1000);
        }

        public int GetQueueCount()
        {
            return _dataFiles.Count;
        }

        public int GetReimportQueueCount()
        {
            return _retryFiles.Count;
        }

        public RawFile GetNextFileToReimport()
        {
            if (_retryFiles.Count <= 0) return null;
            var rawFile = _retryFiles.Dequeue();
            _retryFilePaths.Remove(rawFile.FilePath);

            return rawFile;
        }
    }

    public class FileStatusException : Exception
    {
        public const string DOES_NOT_EXIST = "does not exist";

        public FileStatusException(string message) : base(message)
        {
        }

        public FileStatusException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class RawFile
    {
        internal string FilePath { get; private set; }
        internal DateTime LastImportTime { get; set; }
        internal long WaitTime { get; set; } // Wait time in miliseconds

        public RawFile(string filePath, DateTime lastImportTime, long waitTime)
        {
            FilePath = filePath;
            LastImportTime = lastImportTime;
            WaitTime = waitTime;
        }

        public bool TryReimport()
        {
            return LastImportTime.AddMilliseconds(WaitTime) <= DateTime.Now;
        }
    }

    public class FileWatcherException : Exception
    {
        public FileWatcherException(string message) : base(message)
        {
        }

        public FileWatcherException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DriveInfo
    {
        public string Path { get; }
        public string DriveLetter { get; }
        public string NetworkPath { get; }

        public DriveInfo(string path)
        {
            Path = path;
            DriveLetter = NetworkDriveUtil.GetDriveLetter(path);
            if(DriveLetter != null)
            { 
                try
                {
                    NetworkPath = NetworkDriveUtil.GetNetworkPath(DriveLetter);
                }
                catch (FileWatcherException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new FileWatcherException(string.Format("Unable to read network drive properties for {0}", DriveLetter), e);
                }
            }
            else if (path.StartsWith(@"\\"))
            {
                NetworkPath = path;
            }
        }

        // Returns true if this is a mapped network drive: we know the drive letter and the network path
        public bool IsMappedNetworkDrive()
        {
            return DriveLetter != null && NetworkPath != null;
        }

        public bool IsUncPath()
        {
            return DriveLetter == null && NetworkPath != null;
        }

        public override string ToString()
        {
            if (IsMappedNetworkDrive())
            {
                return string.Format("DriveLetter: {0}; Network Path: {1}; Watched path {2}", DriveLetter, NetworkPath, Path);
            }

            return IsUncPath() ? string.Format("Network Path: {0}; Watched path {1}", NetworkPath, Path) 
                : string.Format("DriveLetter: {0}; Watched path {1}", DriveLetter, Path);
        }
    }
}