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
using System.ComponentModel;
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

        public const int WAIT_5SEC = 5000;
        private const string CANCELLED = "Cancelled";
        private const string ERROR = "Error";
        private const string COMPLETED = "COMPLETED";
       
        public AutoQCBackgroundWorker(IAppControl appControl, IProcessControl processControl, IAutoQCLogger logger)
        {
            _appControl = appControl;
            _processControl = processControl;
            _logger = logger;

            _fileWatcher = new AutoQCFileSystemWatcher(logger);
        }

        public void Start(MainSettings mainSettings)
        {
            if (mainSettings.ImportExistingFiles)
            {
                LogWithSpace("Processing existing files...");
                ProcessExistingFiles();
            }
            else
            {
                LogWithSpace("Processing new files...");
                ProcessNewFiles();
            }

            _fileWatcher.Start(mainSettings);
        }

        private void ProcessExistingFiles()
        {
            RunBackgroundWorker(ProcessExistingFiles, ProcessExistingFilesCompleted);
        }

        private void ProcessNewFiles()
        {
            RunBackgroundWorker(ProcessNewFiles, ProcessNewFilesCompleted);
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

        private void ProcessNewFiles(object sender, DoWorkEventArgs e)
        {
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
                    _fileWatcher.WaitForFileReady(filePath);
                    var importContext = new ImportContext(filePath) {TotalImportCount = _totalImportCount};
                    if (!ProcessOneFile(importContext))
                    {
                        e.Result = ERROR;
                        break;
                    }
                    inWait = false;
                }
                else
                {
                    if (!inWait)
                    {
                        LogWithSpace("Waiting for files...");
                    }

                    inWait = true;
                    Thread.Sleep(WAIT_5SEC);
                }
            }
        }

        private void ProcessNewFilesCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (CANCELLED.Equals(e.Result))
            {
                Log("Cancelled processing files.");
            }
            Stop();
        }

        private void ProcessExistingFilesCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result == null || ERROR.Equals(e.Result))
            {
                Log("Error processing existing files.");
                Stop();
            }
            else if (CANCELLED.Equals(e.Result))
            {
                Log("Cancelled processing existing files.");
                Stop();
            }
            else
            {
                LogWithSpace("Finished processing existing files.");
                ProcessNewFiles(); // Start processing new files.
            }
        }

        private void ProcessExistingFiles(object sender, DoWorkEventArgs e)
        {
            // Queue up any existing data files in the folder
            var files = _fileWatcher.GetAllFiles();

            Log(string.Format("Found {0} files.", files.Count));

            var importContext = new ImportContext(files) {TotalImportCount = _totalImportCount};
            while (importContext.GetNextFile() != null)
            {
                _fileWatcher.WaitForFileReady(importContext.GetCurrentFile()); // TODO: Do we need to wait?
                if (!ProcessOneFile(importContext))
                {
                    e.Result = ERROR;
                    return;
                }

                if (!_worker.CancellationPending) continue;

                e.Result = CANCELLED;
                return;
            }
            e.Result = COMPLETED;
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

            if (_worker.IsBusy)
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
        }

        public bool IsRunning()
        {
            return _worker.IsBusy;
        }
    }
}
