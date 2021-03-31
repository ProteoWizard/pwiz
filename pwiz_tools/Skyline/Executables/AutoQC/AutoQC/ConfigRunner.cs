﻿/*
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
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    public class ConfigRunner: IProcessControl, IConfigRunner
    {
        private BackgroundWorker _worker;

        private int _totalImportCount;

        private AutoQCFileSystemWatcher _fileWatcher;

        private readonly IMainUiControl _uiControl;
        private Logger _logger;
        private ProcessRunner _processRunner;

        public AutoQcConfig Config { get; }

        private PanoramaPinger _panoramaPinger;

        
        public const int WAIT_FOR_NEW_FILE = 5000;
        private const string CANCELED = "Canceled";
        private const string COMPLETED = "Completed";

        private readonly object _lock = new object();
        private RunnerStatus _runnerStatus;
        // This flag is set if a document failed to upload to Panorama for any reason.
        private bool _panoramaUploadError;

        public ConfigRunner(AutoQcConfig config, Logger logger, IMainUiControl uiControl = null)
        {
            _runnerStatus = RunnerStatus.Stopped;

            Config = config;

            _logger = logger;

            _uiControl = uiControl;
            
        }

        public IConfig GetConfig()
        {
            return Config;
        }

        public RunnerStatus GetStatus()
        {
            lock (_lock)
            {
                return _runnerStatus;
            }
        }

        public string GetDisplayStatus()
        {
            RunnerStatus status = _runnerStatus; // bypassed lock
            return status == RunnerStatus.Disconnected ? RunnerStatus.Running.ToString() : status.ToString();
        }

        public Color GetDisplayColor()
        {
            if (IsRunning())
                return Color.Green;
            if (IsDisconnected())
                return Color.Orange;
            if (IsError())
                return Color.Red;
            if (IsDisconnected())
                return Color.DarkOrange;
            return Color.Black;
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

        public Logger GetLogger()
        {
            return _logger;
        }

        private string GetConfigDir()
        {
            var skylineFileDir = Path.GetDirectoryName(Config.MainSettings.SkylineFilePath);
            if (skylineFileDir == null) throw new Exception("Cannot have a null Skyline file directory.");
            return Path.Combine(skylineFileDir, GetSafeName(Config.Name));
        }

        private void CreateConfigDir()
        {
            var configDir = GetConfigDir();
            if (!Directory.Exists(configDir))
            {
                try
                {
                    Directory.CreateDirectory(configDir);
                }
                catch (Exception e)
                {
                    var sb = new StringBuilder(string.Format("Configuration directory \"{0}\" could not be created for configuration \"{1}\""
                        , configDir, GetConfigName()));
                    sb.AppendLine();
                    sb.AppendLine(e.Message);
                    throw new ConfigRunnerException(sb.ToString(), e);
                }
            }
        }

        private static string GetSafeName(string name)
        {
            var invalidChars = new List<char>();
            invalidChars.AddRange(Path.GetInvalidFileNameChars());
            invalidChars.AddRange(Path.GetInvalidPathChars());
            var safeName = string.Join("_", name.Split(invalidChars.ToArray()));
            return safeName; // .TrimStart('.').TrimEnd('.');
        }

        public string GetLogDirectory()
        {
            return Path.GetDirectoryName(_logger.GetFile());
        }

        public void Start()
        {
            _panoramaUploadError = false;

            try
            {
                CreateConfigDir();
            }
            catch (Exception)
            {
                ChangeStatus(RunnerStatus.Error);
                throw;
            }
            
            RunBackgroundWorker(RunConfiguration, ProcessFilesCompleted);
        }

        public void ChangeStatus(RunnerStatus runnerStatus)
        {
            lock (_lock)
            {
                _runnerStatus = runnerStatus;
                if (_uiControl != null)
                {
                    _uiControl.UpdateUiConfigurations();
                }
            }
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
                ChangeStatus(RunnerStatus.Starting);
                
                Config.MainSettings.ValidateSettings();

                Config.PanoramaSettings.ValidateSettings();

                _fileWatcher = new AutoQCFileSystemWatcher(_logger, this);

                // Make sure "Integrate all" is checked in the Skyline settings
                if (!IsIntegrateAllChecked(_logger, Config.MainSettings))
                {
                    ChangeStatus(RunnerStatus.Error);
                    return;
                }

                // Export a report from the Skyline document to get the most recent acquisition date on the results files
                // imported into the document.
                if (!ReadLastAcquiredFileDate(_logger, this))
                {
                    ChangeStatus(RunnerStatus.Error);
                    return;
                }

                if (Config.PanoramaSettings.PublishToPanorama)
                {
                    Log("Initializing Panorama pinger...");
                    _panoramaPinger = new PanoramaPinger(Config.PanoramaSettings, _logger);
                    _panoramaPinger.Init();
                }

                _fileWatcher.Init(Config);

                Log("Starting configuration...");
                ChangeStatus(RunnerStatus.Running);

                if (ProcessExistingFiles(e))
                {
                    ProcessNewFiles(e);
                }
                e.Result = COMPLETED;
            }

            catch (ArgumentException x)
            {
                var err = new StringBuilder(string.Format("There was an error validating configuration \"{0}\"",
                    Config.Name));
                
                LogException(x, err.ToString());
                ChangeStatus(RunnerStatus.Error);

                err.AppendLine().AppendLine().Append(x.Message);
                if (_uiControl != null)
                    _uiControl.DisplayError("Configuration Validation Error:" + Environment.NewLine + err);
            }
            catch (FileWatcherException x)
            {
                var err = new StringBuilder(string.Format("There was an error looking for files for configuration \"{0}\".",
                    Config.Name));

                LogException(x, err.ToString());
                ChangeStatus(RunnerStatus.Error);

                err.AppendLine().AppendLine().Append(x.Message);
                if (_uiControl != null)
                    _uiControl.DisplayError("File Watcher Error" + Environment.NewLine + err);   
            }
            catch (Exception x)
            {
                LogException(x, "There was an error running configuration \"{0}\"",
                    Config.Name);
                ChangeStatus(RunnerStatus.Error);
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
                    e.Result = CANCELED;
                    break;
                }

                var filePath = _fileWatcher.GetFile();
               
                if (filePath != null)
                {
                    var importContext = new ImportContext(filePath) { TotalImportCount = _totalImportCount };
                    var success = ImportFile(e, importContext);
                    
                    if (_panoramaUploadError)
                    {
                        // If there was an error uploading to Panorama, we will stop here
                        break;
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
            var failed = new List<RawFile>();

            while (_fileWatcher.GetReimportQueueCount() > 0)
            {
                var file = _fileWatcher.GetNextFileToReimport();
                if (forceImport || file.TryReimport())
                {
                    var importContext = new ImportContext(file.FilePath) { TotalImportCount = _totalImportCount };
                    _logger.Log(Resources.ConfigRunner_TryReimportOldFiles_Attempting_to_re_import__0__, file.FilePath);
                    if (!ImportFile(e, importContext, false)) 
                    {
                        if (forceImport)
                        {
                            // forceImport is true when we attempt to import failed files after successfully importing a newer file.
                            // If the file still fails to import we will not add it back to the re-import queue.
                            _logger.Log(Resources.ConfigRunner_TryReimportOldFiles__0__failed_to_import_successfully__Skipping___, file.FilePath);     
                        }
                        else
                        {
                            if (_fileWatcher.RawDataExists(file.FilePath))
                            {
                                _logger.Log(Resources.ConfigRunner_TryReimportOldFiles_Adding__0__to_the_re_import_queue_, file.FilePath);
                                file.LastImportTime = DateTime.Now;
                                failed.Add(file);   
                            }
                            else
                            {
                                _logger.Log(Resources.ConfigRunner_TryReimportOldFiles__0__no_longer_exists__Skipping___, file.FilePath);
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
                LogException(e.Error, Resources.ConfigRunner_ProcessFilesCompleted_An_error_occurred_while_running_configuration_);  
            }
            else if (e.Result == null)
            {
                LogError(Resources.ConfigRunner_ProcessFilesCompleted_An_error_occurred__Stopping_configuration_);
            }
            else if (CANCELED.Equals(e.Result))
            {
                Log(Resources.ConfigRunner_ProcessFilesCompleted_Canceled_configuration_);
            }
            else if (_panoramaUploadError)
            {
                LogError(Resources.ConfigRunner_ProcessFilesCompleted_There_was_an_error_uploading_the_document_to_Panorama__Stopping_configuration_);    
            }
            else
            {
                Log(Resources.ConfigRunner_ProcessFilesCompleted_Finished_running_configuration_);
            }

            Stop();
        }

        private bool ProcessExistingFiles(DoWorkEventArgs e)
        {
            // Queue up any existing data files in the folder
            _logger.Log(Resources.ConfigRunner_ProcessExistingFiles_Importing_existing_files___);
            var files = _fileWatcher.GetExistingFiles();

            // Enable notifications on new files that get added to the folder.
            _fileWatcher.StartWatching();

            if (files.Count == 0)
            {
                Log(Resources.ConfigRunner_ProcessExistingFiles_No_existing_files_found_);
                return true;
            }
            
            Log("Existing files found: {0}", files.Count);

            var importContext = new ImportContext(files) {TotalImportCount = _totalImportCount};
            while (importContext.GetNextFile() != null)
            {
                if (_worker.CancellationPending)
                {
                    e.Result = CANCELED;
                    return false;
                }

                var filePath = importContext.GetCurrentFile();
                if (!_fileWatcher.RawDataExists(filePath))
                {
                    // User may have deleted this file.
                    Log(Resources.ConfigRunner_TryReimportOldFiles__0__no_longer_exists__Skipping___, filePath);
                    continue;
                }

                var lastAcquiredFileDate = Config.MainSettings.LastAcquiredFileDate;
                var fileLastWriteTime = File.GetLastWriteTime(filePath);
                if (fileLastWriteTime.CompareTo(lastAcquiredFileDate.AddSeconds(1)) < 0)
                {
                    Log(Resources.ConfigRunner_ProcessExistingFiles__0__was_acquired___1___before_the_acquisition_date___2___on_the_last_imported_file_in_the_Skyline_document__Skipping___,
                        GetFilePathForLog(filePath),
                        fileLastWriteTime,
                        lastAcquiredFileDate);
                    continue;
                }

                ImportFile(e, importContext);
            }

            Log(Resources.ConfigRunner_ProcessExistingFiles_Finished_importing_existing_files___);
            return !_panoramaUploadError;
        }

        private string GetFilePathForLog(string filePath)
        {
            if (Config.MainSettings.IncludeSubfolders)
            {
                return filePath;
            }
            return Path.GetFileName(filePath);
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
                    _logger.LogError(Resources.ConfigRunner_ImportFile__0__does_not_exist_, GetFilePathForLog(filePath));
                }
                else
                {
                    _logger.LogException(fse, Resources.ConfigRunner_ImportFile_Error_getting_status_of_file__0__, GetFilePathForLog(filePath));
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
                e.Result = CANCELED;
                return false;
            }

            _panoramaUploadError = false;
            var docImported = ProcessOneFile(importContext);
            if (!docImported)
            {
                if (addToReimportQueueOnFailure)
                {
                    AddToReimportQueue(filePath);
                }
            }
           
            return docImported;
        }

        private void AddToReimportQueue(string filePath)
        {
            _logger.Log(Resources.ConfigRunner_TryReimportOldFiles_Adding__0__to_the_re_import_queue_,
                GetFilePathForLog(filePath));
                _fileWatcher.AddToReimportQueue(filePath);
        }

        private bool ProcessOneFile(ImportContext importContext)
        {
            var processInfos = GetProcessInfos(importContext);
            bool docImportFailed = false;
            foreach (var processInfo in processInfos)
            {
                var status = RunProcess(processInfo);
                if (status == ProcStatus.PanoramaUploadError)
                {
                    _panoramaUploadError = true;
                }
                if (status == ProcStatus.DocImportError || status == ProcStatus.Error)
                {
                    docImportFailed = true;
                }   
            }
            
            if(!docImportFailed) _totalImportCount++;
            return !docImportFailed;
        }

        public void Cancel()
        {
            Stop();
        }

        public void Stop()
        {
            if (_runnerStatus == RunnerStatus.Stopped)
                return;

            Task.Run(() =>
            {
                _fileWatcher?.Stop();

                _totalImportCount = 0;

                if (_worker != null && _worker.IsBusy)
                {
                    ChangeStatus(RunnerStatus.Stopping);
                    CancelAsync();
                }
                else if(_runnerStatus != RunnerStatus.Error)
                {
                    ChangeStatus(RunnerStatus.Stopped);
                }

                if (_runnerStatus == RunnerStatus.Stopped && _panoramaUploadError)
                {
                    ChangeStatus(RunnerStatus.Error);
                }

                _panoramaPinger?.Stop();
            });
        }

        public bool IsIntegrateAllChecked(Logger logger, MainSettings mainSettings)
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
                logger.LogException(e, Resources.ConfigRunner_IsIntegrateAllChecked_Error_reading_file__0__, mainSettings.SkylineFilePath);
                return false;
            }
            logger.LogError(Resources.ConfigRunner_IsIntegrateAllChecked__Integrate_all__is_not_checked_for_the_Skyline_document__This_setting_is_under_the__Settings__menu_in_Skyline__and_should_be_checked_for__documents_with_QC_results_);
            return false;
        }

        public bool ReadLastAcquiredFileDate(Logger logger, IProcessControl processControl)
        {
            logger.Log(Resources.ConfigRunner_ReadLastAcquiredFileDate_Getting_the_acquisition_date_on_the_newest_file_imported_into_the_Skyline_document_);
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (exeDir == null)
            {
                logger.LogError(Resources.ConfigRunner_ReadLastAcquiredFileDate_Could_not_get_path_to_the_Skyline_report_file_);
                return false;

            }
            var skyrFile = Path.Combine(exeDir, "FileAcquisitionTime.skyr");
            var reportFile = Path.Combine(GetConfigDir(), "AcquisitionTimes.csv");

            // Export a report from the given Skyline file
            var args =
                string.Format(
                    @" --in=""{0}"" --report-conflict-resolution=overwrite --report-add=""{1}"" --report-name=""{2}"" --report-file=""{3}""",
                    Config.MainSettings.SkylineFilePath, skyrFile, "AcquisitionTimes", reportFile);

            var procInfo = new ProcessInfo(Config.SkylineSettings.CmdPath, args, args);
            if (processControl.RunProcess(procInfo) == ProcStatus.Error)
            {
                logger.LogError("Error getting the last acquired file date from the Skyline document.");
                return false;
            }
            // Read the exported report to get the last AcquiredTime for imported results in the Skyline doucment.
            if (!File.Exists(reportFile))
            {
                logger.LogError(Resources.ConfigRunner_ReadLastAcquiredFileDate_Could_not_find_report_output__0_, reportFile);
                return false;
            }

            try
            {
                var lastAcquiredFileDate = GetLastAcquiredFileDate(reportFile, logger);
                Config.MainSettings.LastAcquiredFileDate = lastAcquiredFileDate;
                if (!lastAcquiredFileDate.Equals(DateTime.MinValue))
                {
                    logger.Log(Resources.ConfigRunner_ReadLastAcquiredFileDate_The_most_recent_acquisition_date_in_the_Skyline_document_is__0_, lastAcquiredFileDate);
                }
                else
                {
                    logger.Log(Resources.ConfigRunner_ReadLastAcquiredFileDate_The_Skyline_document_does_not_have_any_imported_results_);  
                }
            }
            catch (IOException e)
            {
                logger.LogException(e, Resources.ConfigRunner_IsIntegrateAllChecked_Error_reading_file__0__, reportFile);
                return false;
            }
            return true;
        }

        private static DateTime GetLastAcquiredFileDate(string reportFile, Logger logger)
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
                            logger.LogException(e, Resources.ConfigRunner_GetLastAcquiredFileDate_Error_parsing_acquired_time_from_Skyline_report___0_, reportFile);
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
            _logger.Log(string.Format(message, args));    
        }
        
        private void LogError(string message, params Object[] args)
        {
            _logger.LogError(string.Format(message, args));
        }

        private void LogException(Exception e, string message, params Object[] args)
        {
            _logger.LogException(e, string.Format(message, args));
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
            return _runnerStatus == RunnerStatus.Stopping;
        }

        public bool IsStopped()
        {
            return _runnerStatus == RunnerStatus.Stopped || _runnerStatus == RunnerStatus.Error;
        }

        public bool IsStarting()
        {
            return _runnerStatus == RunnerStatus.Starting;
        }

        public bool IsRunning()
        {
            return _runnerStatus == RunnerStatus.Running;
        }

        public bool IsError()
        {
            return _runnerStatus == RunnerStatus.Error;
        }

        public bool IsDisconnected()
        {
            return _runnerStatus == RunnerStatus.Disconnected;
        }

        public bool CanStart()
        {
            return IsStopped() || IsDisconnected() || IsError();
        }

        public bool CanStop()
        {
            return IsRunning();
        }

        #region [Implementation of IProcessControl interface]
        public IEnumerable<ProcessInfo> GetProcessInfos(ImportContext importContext)
        {
            var processInfos = new List<ProcessInfo>();

            var runBefore = Config.RunBefore(importContext);
            if (runBefore != null)
            {
                runBefore.WorkingDirectory = importContext.WorkingDir;
                processInfos.Add(runBefore);
            }
            

            var skylineRunnerArgs = GetSkylineRunnerArgs(importContext);
            var argsToPrint = GetSkylineRunnerArgs(importContext, true);
            var skylineRunner = new ProcessInfo(Config.SkylineSettings.CmdPath, skylineRunnerArgs, argsToPrint);
            processInfos.Add(skylineRunner);
            
            var runAfter = Config.RunAfter(importContext);
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
            args.Append(" ");
            args.Append(Config.PanoramaSettings.SkylineRunnerArgs(importContext, toPrint));

            return args.ToString();
        }

        public ProcStatus RunProcess(ProcessInfo processInfo)
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
        ProcStatus RunProcess(ProcessInfo processInfo);
        void StopProcess();
    }

    public enum ProcStatus
    {
        Success,
        Error,
        DocImportError,
        PanoramaUploadError
    }

    public class ConfigRunnerException : SystemException
    {
        public ConfigRunnerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
