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
using System.IO;
using System.Threading;

namespace AutoQC
{

    // ReSharper disable once InconsistentNaming
    public class AutoQCFileSystemWatcher
    {
        private readonly IAutoQCLogger _logger;

        private IResultFileStatus _fileStatusChecker;

        // Collection of new mass spec files to be processed.
        private ConcurrentQueue<string> _dataFiles;
        // Collection of mass spec files that resulted in an error while importing
        private Queue<RawFile> _retryFiles; 

        private FileSystemWatcher _fileWatcher;
        private bool _dataInDirectories;
        private NetworkDriveUtil _networkDrive;

        private bool _cancelled;
        private bool _folderAvailable = true;

        private int _acquisitionTimeSetting;

        private const int WAIT_60SEC = 60000;

        // TODO: We need to support other instrument vendors
        private const string THERMO_EXT = ".raw";
        private const string SCIEX_EXT = ".wiff";
        private const string WATERS_EXT = ".raw";
        private const string AGILENT_EXT = ".d";

        public AutoQCFileSystemWatcher(IAutoQCLogger logger)
        {
            _fileWatcher = InitFileSystemWatcher();

            _logger = logger;

            _networkDrive = new NetworkDriveUtil(this, logger);
        }

        private FileSystemWatcher InitFileSystemWatcher()
        {
            var fileWatcher = new FileSystemWatcher();
            fileWatcher.Created += (s, e) => FileAdded(e);
            fileWatcher.Error += (s, e) => OnFileWatcherError(e);
            return fileWatcher;
        }

        public void Init(MainSettings mainSettings)
        {
            _dataFiles = new ConcurrentQueue<string>();
            _retryFiles = new Queue<RawFile>();

            _fileStatusChecker = GetFileStatusChecker(mainSettings);

            _fileWatcher.EnableRaisingEvents = false;

            _fileWatcher.Filter = GetFileFilter(mainSettings.InstrumentType, out _dataInDirectories);

            _fileWatcher.Path = mainSettings.FolderToWatch;

            _acquisitionTimeSetting = mainSettings.AcquisitionTime;
        }

        public void StartWatching()
        {
            _cancelled = false;
            // Begin watching.
            _fileWatcher.EnableRaisingEvents = true;
        }

        private static IResultFileStatus GetFileStatusChecker(MainSettings mainSettings)
        {
            if (mainSettings.InstrumentType.Equals(MainSettings.THERMO))
            {
                return new XRawFileStatus(mainSettings.AcquisitionTime);
            }
            return new AcquisitionTimeFileStatus(mainSettings.AcquisitionTime);
        }

        private static string GetFileFilter(string instrument, out bool dataInDirectories)
        {
            var ext = ".*";
            if (instrument.Equals(MainSettings.THERMO))
            {
                ext = THERMO_EXT;
                dataInDirectories = false;
            }
            else if (instrument.Equals(MainSettings.SCIEX))
            {
                ext = SCIEX_EXT;
                dataInDirectories = false;
            }
            else if (instrument.Equals(MainSettings.WATERS))
            {
                // Waters: .raw directory
                ext = WATERS_EXT;
                dataInDirectories = true;
            }
            else if (instrument.Equals(MainSettings.AGILENT))
            {
                // Agilent: .d directory
                ext = AGILENT_EXT;
                dataInDirectories = true;
            }
            else
            {
                dataInDirectories = false;
            }
            // TODO: We need to support other instrument vendors
            return "*" + ext;
        }

        public void Pause()
        {
            _fileWatcher.EnableRaisingEvents = false;
        }

        public void Restart(DateTime timeDisconnected)
        {
            if (_cancelled)
            {
                _logger.Log("FileSystemWatcher cancelled. ");
                return;
            }

            _logger.Log("Reconnected. Re-initializing FileSystemWatcher...");

            var filter = _fileWatcher.Filter;
            var path = _fileWatcher.Path;

            _fileWatcher.Dispose();
            _fileWatcher = null;
            _fileWatcher = InitFileSystemWatcher();
            _fileWatcher.Filter = filter;
            _fileWatcher.Path = path;

            _logger.Log("Looking for raw data added to directory while the folder was unavailable.");

            var files = GetExistingFiles();

            _folderAvailable = true;

            _fileWatcher.EnableRaisingEvents = true;

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);

                if (fileInfo.CreationTime <= timeDisconnected) continue;

                _logger.Log("Adding {0}.", fileInfo.Name);
                _dataFiles.Enqueue(file);
            }
        }

        public void Stop()
        {
            _cancelled = true;
            _fileWatcher.EnableRaisingEvents = false;
        }

        void FileAdded(FileSystemEventArgs e)
        {
            var path = e.FullPath;
            if ((_dataInDirectories && Directory.Exists(path))
                || (!_dataInDirectories && File.Exists(path)))
            {
                _logger.Log("{0} added to directory.", e.Name);
                _dataFiles.Enqueue(e.FullPath);   
            }
        }

        void OnFileWatcherError(ErrorEventArgs e)
        {
            _logger.LogError("There was an error watching the folder. {0}", e.GetException().Message);
            _folderAvailable = false;
        }

        public bool IsFolderAvailable()
        {
            return _folderAvailable;
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
                if (!RawDataExists(filePath))
                {
                    throw new FileStatusException(string.Format("{0} {1}", filePath, FileStatusException.DOES_NOT_EXIST));
                }

                var fileStatus= _fileStatusChecker.CheckStatus(filePath);
                if (fileStatus.Equals(Status.Ready))
                {
                    break;
                }

                if (fileStatus.Equals(Status.ExceedMaximumAcquiTime))
                {
                    throw new FileStatusException("Data acquistion has exceeded the expected acquistion time." +
                                        "The instument probably encountered an error.");
                }

                if (counter % 10 == 0)
                {
                    _logger.Log("{0} is being acquired. Waiting...", Path.GetFileName(filePath));
                }
                counter++;
                // Wait for 60 seconds.
                Thread.Sleep(WAIT_60SEC);
                if (_cancelled)
                {
                    _logger.Log("FileSystemWatcher cancelled. ");
                    return;
                }
            }
            _logger.Log("{0} is ready", Path.GetFileName(filePath));
        }

        public string GetFile()
        {
            // If we are monitoring a network mapped drive, make sure that we can still connect to it.
            // If we lose connection to a networked drive, FileSystemWatcher does not fire any new events
            // even after the connection is re-established.
            _networkDrive.EnsureDrive(_fileWatcher.Path);
        
            if (_dataFiles.IsEmpty)
            {
                return null;
            }
                
            string filePath;
            _dataFiles.TryDequeue(out filePath);
            return filePath;
        }

        public List<string> GetExistingFiles()
        {
            var rawData = new List<string>();
            rawData.AddRange(_dataInDirectories
                ? Directory.GetDirectories(_fileWatcher.Path, _fileWatcher.Filter)
                : Directory.GetFiles(_fileWatcher.Path, _fileWatcher.Filter));
            return rawData;
        }

        public string GetDirectory()
        {
            return _fileWatcher.Path;
        }

        public void AddToReimportQueue(RawFile file)
        {
            _retryFiles.Enqueue(file);
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

        public Queue<RawFile> GetFilesToReimport()
        {
            return _retryFiles;
        }

        public RawFile GetNextFileToReimport()
        {
            return _retryFiles.Count > 0 ? _retryFiles.Dequeue() : null;
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
}
