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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;

namespace AutoQC
{
    public class AutoQCBackgroundWorker
    {
        private BackgroundWorker _worker;

        private int _totalImportCount;

        private readonly AutoQCFileSystemWatcher _fileWatcher;

        private readonly IAppControl _appControl;
        private readonly IProcessControl _processControl;
        private readonly IAutoQCLogger _logger;

        private DateTime _lastAcquiredFileDate;

        public const int WAIT_FOR_NEW_FILE = 5000;
        private const string CANCELLED = "Cancelled";
        //private const string ERROR = "Error";
        private const string COMPLETED = "Completed";
       
        public AutoQCBackgroundWorker(IAppControl appControl, IProcessControl processControl, IAutoQCLogger logger)
        {
            _appControl = appControl;
            _processControl = processControl;
            _logger = logger;

            _fileWatcher = new AutoQCFileSystemWatcher(logger);
        }

        public void Start(MainSettings mainSettings)
        {
            _lastAcquiredFileDate = mainSettings.LastAcquiredFileDate;
            _fileWatcher.Init(mainSettings);
            ProcessFiles();
        }

        private void ProcessFiles()
        {
            RunBackgroundWorker(ProcessFiles, ProcessFilesCompleted);
        }

        private void RunBackgroundWorker(DoWorkEventHandler doWork, RunWorkerCompletedEventHandler doOnComplete)
        {
            if (_worker != null)
            {
                if (_worker.CancellationPending)
                {
                    return;
                }
                if (_worker.IsBusy)
                {
                    LogError("Background worker is running!!!");
                    Stop();
                    return;
                }
            }

            _worker = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
            _worker.DoWork += doWork;
            _worker.RunWorkerCompleted += doOnComplete;
            _worker.RunWorkerAsync();
        }

        private void ProcessNewFiles(DoWorkEventArgs e)
        {
            LogWithSpace("Importing new files...");
            var inWait = false;
            while (true)
            {
                if (_worker.CancellationPending)
                {
                    e.Result = CANCELLED;
                    break;
                }

                var filePath = _fileWatcher.GetFile();
               
                if (filePath != null)
                {
                    var importContext = new ImportContext(filePath) { TotalImportCount = _totalImportCount };
                    ImportFile(e, importContext);

                    
                    if (!_fileWatcher.IsFolderAvailable())
                    {
                        // We may have lost connection to a mapped network drive.
                        continue;
                    }

                    // Make one last attempt to import any old files.
                    TryReimportOldFiles(e, true);

                    inWait = false;
                }
                else
                {
                    // Try to import any older files that resulted in an import error the first time.
                    TryReimportOldFiles(e, false);

                    if (!inWait)
                    {
                        LogWithSpace("Waiting for files...");
                    }

                    inWait = true;
                    Thread.Sleep(WAIT_FOR_NEW_FILE);
                }
            }
        }

        private void TryReimportOldFiles(DoWorkEventArgs e, bool forceImport)
        {
            var reimportQueue = _fileWatcher.GetFilesToReimport();
            var failed = new List<RawFile>();

            while (reimportQueue.Count > 0)
            {
                var file = reimportQueue.Dequeue();
                if (forceImport || file.TryReimport())
                {
                    var importContext = new ImportContext(file.FilePath) { TotalImportCount = _totalImportCount };
                    _logger.Log("Attempting to re-import file {0}.", file.FilePath);
                    if (!ImportFile(e, importContext, false))
                    {
                        _logger.Log("Adding file to re-import queue: {0}", file.FilePath);
                        file.LastImportTime = DateTime.Now;
                        failed.Add(file);
                    }
                }
                else
                {
                    failed.Add(file); // We are going to try to re-import later
                }
            }

            foreach (var file in failed)
            {
                _fileWatcher.AddToReimportQueue(file);
            }
        }

        private void ProcessFilesCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                LogError("An exception occurred while importing the file.");
                _logger.LogException(e.Error);  
            }
            else if (e.Result == null)
            {
                Log("Error importing file.");
            }
            else if (CANCELLED.Equals(e.Result))
            {
                Log("Cancelled importing files.");
            }
            else
            {
                LogWithSpace("Finished importing files.");
            }
            Stop();
        }

        private void ProcessFiles(object sender, DoWorkEventArgs e)
        {
            if(ProcessExistingFiles(e))
            {
                ProcessNewFiles(e);  
            }
            e.Result = COMPLETED;
        }

        private bool ProcessExistingFiles(DoWorkEventArgs e)
        {
            // Queue up any existing data files in the folder
            _logger.Log("Importing existing files...", 1, 0);
            var files = _fileWatcher.GetExistingFiles();

            // Enable notifications on new files that get added to the folder.
            _fileWatcher.StartWatching();

            if (files.Count == 0)
            {
                Log("No existing files found.");
                return true;
            }
            
            Log("Existing files found: {0}", files.Count);

            var importContext = new ImportContext(files) {TotalImportCount = _totalImportCount};
            while (importContext.GetNextFile() != null)
            {
                if (_worker.CancellationPending)
                {
                    e.Result = CANCELLED;
                    return false;
                }

                var filePath = importContext.GetCurrentFile();
                if (!(new FileInfo(filePath).Exists))
                {
                    // User may have deleted this file.
                    Log("File {0} no longer exists. Skipping...", filePath);
                    continue;
                }

                var fileLastWriteTime = File.GetLastWriteTime(filePath);
                if (fileLastWriteTime.CompareTo(_lastAcquiredFileDate.AddSeconds(1)) < 0)
                {
                    Log(
                        "File {0} was acquired ({1}) before the acquisition date ({2}) on the last imported file in the Skyline document. Skipping...",
                        Path.GetFileName(filePath),
                        fileLastWriteTime,
                        _lastAcquiredFileDate);
                    continue;
                }

                ImportFile(e, importContext);
            }

            LogWithSpace("Finished importing existing files...");
            return true;
        }

        private bool ImportFile(DoWorkEventArgs e, ImportContext importContext, bool addToReimportQueueOnFailure = true)
        {
            var filePath = importContext.GetCurrentFile();
            try
            {
                _fileWatcher.WaitForFileReady(filePath);
            }
            catch (FileStatusException fse)
            {
                if (!FileStatusException.DOES_NOT_EXIST.Equals(fse.Message))
                {
                    _logger.LogError("File does not exist: {0}.", filePath);
                }
                else
                {
                    _logger.LogException(fse);
                }
                // Put the file in the re-import queue
                if (addToReimportQueueOnFailure)
                {
                    _logger.Log("Adding file to re-import queue: {0}", filePath);
                    _fileWatcher.AddToReimportQueue(filePath);
                }
                return false;
            }

            if (_worker.CancellationPending)
            {
                e.Result = CANCELLED;
                return false;
            }

            if (!ProcessOneFile(importContext))
            {
                if (addToReimportQueueOnFailure)
                {
                    _logger.Log("Adding file to re-import queue: {0}", filePath);
                    _fileWatcher.AddToReimportQueue(filePath);
                }
                return false;
            }

            return true;
        }

        private bool ProcessOneFile(ImportContext importContext)
        {
            var processInfos = _processControl.GetProcessInfos(importContext);
            if (processInfos.Any(procInfo => !_processControl.RunProcess(procInfo)))
            {
                return false;
            }
            _totalImportCount++;
            return true;
        }

        public void Stop()
        {
            _fileWatcher.Stop();
            _totalImportCount = 0;

            if (_worker != null && _worker.IsBusy)
            {
                CancelAsync();
                _appControl.SetWaiting();
            }
            else
            {
                _appControl.SetStopped();
            }
        }

        private void LogWithSpace(string message, params Object[] args)
        {
            _logger.Log(message, 1, 1, args);
        }

        private void Log(string message, params Object[] args)
        {
            _logger.Log(message, args);    
        }

        private void LogError(string message, params Object[] args)
        {
            _logger.LogError(message, args);
        }

        private void CancelAsync()
        {
            _worker.CancelAsync();
            _processControl.StopProcess();
        }

        public bool IsRunning()
        {
            return _worker.IsBusy;
        }
    }
}
