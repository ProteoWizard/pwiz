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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace AutoQC
{
    public class ConfigRunner: IProcessControl
    {
        private BackgroundWorker _worker;

        private int _totalImportCount;

        private AutoQCFileSystemWatcher _fileWatcher;

        private readonly IMainUiControl _uiControl;
        private IAutoQcLogger _logger;
        private ProcessRunner _processRunner;

        public AutoQcConfig Config { get; private set; }

        private PanoramaPinger _panoramaPinger;

        
        public const int WAIT_FOR_NEW_FILE = 5000;
        private const string CANCELLED = "Cancelled";
        //private const string ERROR = "Error";
        private const string COMPLETED = "Completed";

        private readonly object _lock = new object();
        private Status _status;

        public enum Status
        {
            Starting,
            Running,
            Stopping,
            Stopped,
            Error
        }

        public ConfigRunner(AutoQcConfig config, IMainUiControl uiControl)
        {
            _status = Status.Stopped;

            Config = config;

            _uiControl = uiControl;

            CreateLogger();
        }

        public Status GetStatus()
        {
            lock (_lock)
            {
                return _status;
            }
        }

        public string GetConfigName()
        {
            return Config.Name;
        }

        public bool IsConfigEnabled()
        {
            return Config.IsEnabled;
        }

        public DateTime GetConfigCreateDate()
        {
            return Config.Created;
        }

        public IAutoQcLogger GetLogger()
        {
            return _logger;
        }

        public string GetLogDirectory()
        {
            return Path.GetDirectoryName(_logger.GetFile());
        }

        public void DisableUiLogging()
        {
            _logger.DisableUiLogging();
        }

        public void EnableUiLogging()
        {
            _logger.LogToUi(_uiControl);   
        }

        public void Start()
        {
            try
            {
                Config.MainSettings.ValidateSettings();
            }
            catch (ArgumentException ex)
            {
                ChangeStatus(Status.Error);
                _logger.LogException(ex);
                throw ex;
            }

            RunBackgroundWorker(RunConfiguration, ProcessFilesCompleted);
        }

        private void ChangeStatus(Status status)
        {
            lock (_lock)
            {
                _status = status;
            }
            _uiControl.ChangeConfigUiStatus(this);
        }

        private void CreateLogger()
        {
            // Initialize logging to log in the folder with the Skyline document.
            var skylineFileDir = Config.MainSettings.SkylineFileDir;
            var logFile = Path.Combine(skylineFileDir, "AutoQC.log");
            _logger = new AutoQcLogger(logFile);   
        }

        private void InitLogger()
        {
            var sb = new StringBuilder("");
            sb.AppendLine("Initializing logging...");
            sb.AppendLine("");
            sb.Append(Config);
            _logger.Log(sb.ToString());
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

        private void RunConfiguration(object sender, DoWorkEventArgs e)
        {
            try
            {
                ChangeStatus(Status.Starting);

                // Thread.Sleep(2000);

                InitLogger();

                _fileWatcher = new AutoQCFileSystemWatcher(_logger);

                // Make sure "Integrate all" is checked in the Skyline settings
                if (!IsIntegrateAllChecked(_logger, Config.MainSettings))
                {
                    ChangeStatus(Status.Error);
                    return;
                }

                // Export a report from the Skyline document to get the most recent acquisition date on the results files
                // imported into the document.
                if (!ReadLastAcquiredFileDate(_logger, this))
                {
                    ChangeStatus(Status.Error);
                    return;
                }

                if (Config.PanoramaSettings.PublishToPanorama)
                {
                    Log("Initializing Panorama pinger...");
                    _panoramaPinger = new PanoramaPinger(Config.PanoramaSettings, _logger);
                    _panoramaPinger.Init();
                }

                _fileWatcher.Init(Config.MainSettings);

                Log("Starting configuration...");
                ChangeStatus(Status.Running);

                if (ProcessExistingFiles(e))
                {
                    ProcessNewFiles(e);
                }
                e.Result = COMPLETED;
            }

            catch (Exception x)
            {
                LogException(x);
                ChangeStatus(Status.Error);
            }
        }

        private void ProcessNewFiles(DoWorkEventArgs e)
        {
            Log("Importing new files...");
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
                    var success = ImportFile(e, importContext);

                    
                    if (!_fileWatcher.IsFolderAvailable())
                    {
                        // We may have lost connection to a mapped network drive.
                        continue;
                    }

                    if (success)
                    {
                        // Make one last attempt to import any old files. 
                        // Any files that still do not import successfully
                        // will be removed from the re-import queue.
                        TryReimportOldFiles(e, true);
                    }

                    inWait = false;
                }
                else
                {
                    // Try to import any older files that resulted in an import error the first time.
                    TryReimportOldFiles(e, false);

                    if (!inWait)
                    {
                        Log("Waiting for files...");
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
                    _logger.Log("Attempting to re-import {0}.", file.FilePath);
                    if (!ImportFile(e, importContext, false)) 
                    {
                        if (forceImport)
                        {
                            // forceImport is true when we attempt to import failed files after successfully importing a newer file.
                            // If the file still fails to import we will not add it back to the re-import queue.
                            _logger.Log("{0} failed to import successfully. Skipping...", file.FilePath);     
                        }
                        else
                        {
                            if (_fileWatcher.RawDataExists(file.FilePath))
                            {
                                _logger.Log("Adding {0} to re-import queue.", file.FilePath);
                                file.LastImportTime = DateTime.Now;
                                failed.Add(file);   
                            }
                            else
                            {
                                _logger.Log("{0} no longer exists. Skipping...", file.FilePath);
                            }
                            
                        }        
                    }
                }
                else
                {
                    failed.Add(file); // We are going to try to re-import later
                }
            }

            foreach (var file in failed)
            {
                if (_fileWatcher.RawDataExists(file.FilePath))
                {
                    _fileWatcher.AddToReimportQueue(file);
                }
            }
        }

        private void ProcessFilesCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                LogError("An exception occurred while importing the file.");
                LogException(e.Error);  
            }
            else if (e.Result == null)
            {
                LogError("Error importing file.");
            }
            else if (CANCELLED.Equals(e.Result))
            {
                Log("Cancelled importing files.");
            }
            else
            {
                Log("Finished importing files.");
            }
            Stop();
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
                if (!_fileWatcher.RawDataExists(filePath))
                {
                    // User may have deleted this file.
                    Log("{0} no longer exists. Skipping...", filePath);
                    continue;
                }

                var lastAcquiredFileDate = Config.MainSettings.LastAcquiredFileDate;
                var fileLastWriteTime = File.GetLastWriteTime(filePath);
                if (fileLastWriteTime.CompareTo(lastAcquiredFileDate.AddSeconds(1)) < 0)
                {
                    Log(
                        "{0} was acquired ({1}) before the acquisition date ({2}) on the last imported file in the Skyline document. Skipping...",
                        Path.GetFileName(filePath),
                        fileLastWriteTime,
                        lastAcquiredFileDate);
                    continue;
                }

                ImportFile(e, importContext);
            }

            Log("Finished importing existing files...");
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
                if (fse.Message.Contains(FileStatusException.DOES_NOT_EXIST))
                {
                    _logger.LogError("{0} does not exist.", filePath);
                }
                else
                {
                    _logger.LogException(fse);
                }
                // Put the file in the re-import queue
                if (addToReimportQueueOnFailure)
                {
                    AddToReimportQueue(filePath);
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
                    AddToReimportQueue(filePath);
                }
                return false;
            }

            return true;
        }

        private void AddToReimportQueue(string filePath)
        {
            _logger.Log("Adding {0} to re-import queue.", filePath);
            _fileWatcher.AddToReimportQueue(filePath);
        }

        private bool ProcessOneFile(ImportContext importContext)
        {
            var processInfos = GetProcessInfos(importContext);
            if (processInfos.Any(procInfo => !RunProcess(procInfo)))
            {
                return false;
            }
            _totalImportCount++;
            return true;
        }

        public void Stop()
        {
            if (_status == Status.Stopped)
                return;

            Task.Run(() =>
            {
                _fileWatcher.Stop();
                _totalImportCount = 0;

                if (_worker != null && _worker.IsBusy)
                {
                    _status = Status.Stopping;
                    CancelAsync();
                }
                else if(_status != Status.Error)
                {
                    _status = Status.Stopped;
                }
                _uiControl.ChangeConfigUiStatus(this);

                if (_panoramaPinger != null)
                {
                    _panoramaPinger.Stop();
                }
            });
        }

        public bool IsIntegrateAllChecked(IAutoQcLogger logger, MainSettings mainSettings)
        {
            try
            {
                using (var stream = new FileStream(mainSettings.SkylineFilePath, FileMode.Open))
                {
                    using (XmlReader reader = XmlReader.Create(stream))
                    {
                        reader.MoveToContent();

                        var done = false;
                        while (reader.Read() && !done)
                        {
                            switch (reader.NodeType)
                            {
                                case XmlNodeType.Element:

                                    if (reader.Name == "transition_integration")
                                    {
                                        if (reader.MoveToAttribute("integrate_all"))
                                        {
                                            bool integrateAll;
                                            Boolean.TryParse(reader.Value, out integrateAll);
                                            return integrateAll;
                                        }
                                        done = true;
                                    }
                                    break;
                                case XmlNodeType.EndElement:
                                    if (reader.Name.Equals("transition_settings")) // We have come too far
                                    {
                                        done = true;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError("Exception reading file {0}. Exception details are: ", mainSettings.SkylineFilePath);
                logger.LogException(e);
                return false;
            }
            logger.LogError("\"Integrate all\" is not checked for the Skyline document. This setting is under the \"Settings\" menu in Skyline, and should be checked for " +
                            " documents with QC results.");
            return false;
        }

        public bool ReadLastAcquiredFileDate(IAutoQcLogger logger, IProcessControl processControl)
        {
            logger.Log("Getting the acquisition date on the newest file imported into the Skyline document.", 1, 0);
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (exeDir == null)
            {
                logger.LogError("Cound not get path to the Skyline report file");
                return false;

            }
            var skyrFile = Path.Combine(exeDir, "FileAcquisitionTime.skyr");
            var reportFile = Path.Combine(Config.MainSettings.SkylineFileDir, "AcquisitionTimes.csv");

            // Export a report from the given Skyline file
            var args =
                string.Format(
                    @" --in=""{0}"" --report-conflict-resolution=overwrite --report-add=""{1}"" --report-name=""{2}"" --report-file=""{3}""",
                    Config.MainSettings.SkylineFilePath, skyrFile, "AcquisitionTimes", reportFile);

            var procInfo = new ProcessInfo(MainForm.SkylineRunnerPath, MainForm.SKYLINE_RUNNER, args, args);
            if (!processControl.RunProcess(procInfo))
            {
                logger.LogError("Error getting the last acquired file date from the Skyline document.");
                return false;
            }
            // Read the exported report to get the last AcquiredTime for imported results in the Skyline doucment.
            if (!File.Exists(reportFile))
            {
                logger.LogError("Could not find report outout {0}", reportFile);
                return false;
            }

            try
            {
                var lastAcquiredFileDate = GetLastAcquiredFileDate(reportFile, logger);
                Config.MainSettings.LastAcquiredFileDate = lastAcquiredFileDate;
                if (!lastAcquiredFileDate.Equals(DateTime.MinValue))
                {
                    logger.Log("The most recent acquisition date in the Skyline document is {0}", lastAcquiredFileDate);
                }
                else
                {
                    logger.Log("The Skyline document does not have any imported results.");  
                }
            }
            catch (IOException e)
            {
                logger.LogError("Exception reading file {0}. Exception details are: ", reportFile);
                logger.LogException(e);
                return false;
            }
            return true;
        }

        private static DateTime GetLastAcquiredFileDate(string reportFile, IAutoQcLogger logger)
        {
            var lastAcq = new DateTime();

            using (var reader = new StreamReader(reportFile))
            {
                string line; // Read the column headers
                var first = true;

                while ((line = reader.ReadLine()) != null)
                {
                    if (first)
                    {
                        first = false;
                        continue;
                    }

                    var values = line.Split(',');
                    if (values.Length == 3)
                    {
                        DateTime acqDate = new DateTime();
                        try
                        {
                            acqDate = DateTime.Parse(values[1]); // Acquired time, not modified time.
                        }
                        catch (Exception e)
                        {
                            logger.LogError("Error parsing acquired time from Skyline report: {0}", reportFile);
                            logger.LogException(e);
                        }
                        if (acqDate.CompareTo(lastAcq) == 1)
                        {
                            lastAcq = acqDate;
                        }
                    }
                }
            }

            return lastAcq;
        }

        private void Log(string message, params Object[] args)
        {
            _logger.Log(message, args);    
        }

        private void LogError(string message, params Object[] args)
        {
            _logger.LogError(message, args);
        }

        private void LogException(Exception e)
        {
            _logger.LogException(e);
        }

        private void CancelAsync()
        {
            _worker.CancelAsync();
            StopProcess();
        }

        public bool IsBusy()
        {
            return !IsStopped();
        }

        public bool IsStopping()
        {
            return _status == Status.Stopping;
        }

        public bool IsStopped()
        {
            return _status == Status.Stopped || _status == Status.Error;
        }

        public bool IsStarting()
        {
            return _status == Status.Starting;
        }

        public bool IsRunning()
        {
            return _status == Status.Running;
            // return _worker.IsBusy;
        }

        public bool IsError()
        {
            return _status == Status.Error;
        }

        #region [Implementation of IProcessControl interface]
        public IEnumerable<ProcessInfo> GetProcessInfos(ImportContext importContext)
        {
            var processInfos = new List<ProcessInfo>();

            var runBefore = Config.MainSettings.RunBefore(importContext);
            if (runBefore != null)
            {
                runBefore.WorkingDirectory = importContext.WorkingDir;
                processInfos.Add(runBefore);
            }
            

            var skylineRunnerArgs = GetSkylineRunnerArgs(importContext);
            var argsToPrint = GetSkylineRunnerArgs(importContext, true);
            var skylineRunner = new ProcessInfo(MainForm.SkylineRunnerPath, MainForm.SKYLINE_RUNNER, skylineRunnerArgs, argsToPrint);
            processInfos.Add(skylineRunner);
            
            var runAfter = Config.MainSettings.RunAfter(importContext);
            if (runAfter != null)
            {
                processInfos.Add(runAfter);
            }

            return processInfos;
        }

        private string GetSkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            var args = new StringBuilder();

            args.AppendLine();
            args.Append(Config.MainSettings.SkylineRunnerArgs(importContext, toPrint));
            args.AppendLine();
            args.Append(Config.PanoramaSettings.SkylineRunnerArgs(importContext, toPrint));

            return args.ToString();
        }

        public bool RunProcess(ProcessInfo processInfo)
        {
            _processRunner = new ProcessRunner(_logger);
            return _processRunner.RunProcess(processInfo);
        }

        public void StopProcess()
        {
            _processRunner.StopProcess();
        }

        #endregion
    }

    public interface IProcessControl
    {
        IEnumerable<ProcessInfo> GetProcessInfos(ImportContext importContext);
        bool RunProcess(ProcessInfo processInfo);
        void StopProcess();
    }
}
