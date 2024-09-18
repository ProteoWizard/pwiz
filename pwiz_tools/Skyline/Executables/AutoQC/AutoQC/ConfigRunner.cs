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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

        private AutoQCFileSystemWatcher _fileWatcher;
        private AnnotationsFileWatcher _annotationsFileWatcher;
        private bool _annotationsFileUpdated;
        readonly object _annotationsFileLock = new object();

        public bool Waiting { get; private set; }
        public bool AnnotationsFileUpdated
        {
            get
            {
                lock (_annotationsFileLock)
                {
                    return _annotationsFileUpdated;
                }
            }
            set
            {
                lock (_annotationsFileLock)
                {
                    _annotationsFileUpdated = value;
                }
            }
        }

        private readonly IMainUiControl _uiControl;
        private readonly Logger _logger;
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
        private bool _panoramaFatalError;
        private DateTime _panoramaErrorOccurred = DateTime.MinValue;
        private DateTime _lastUploadAt = DateTime.MinValue;

        private bool _annotationsImportError;
        private bool _annotationsFirstImport = true;

        public DateTime LastAcquiredFileDate;
        public DateTime LastArchivalDate;

        public ConfigRunner(AutoQcConfig config, Logger logger, IMainUiControl uiControl = null) : this(config, logger, RunnerStatus.Stopped, uiControl)
        {
        }

        public ConfigRunner(AutoQcConfig config, Logger logger, RunnerStatus runnerStatus, IMainUiControl uiControl = null)
        {
            _runnerStatus = runnerStatus;

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
            if (IsPending()) // Starting / Stopping / Validating
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

        private void CreateConfigDir()
        {
            var configDir = Config.GetConfigDir();
            if (!Directory.Exists(configDir))
            {
                try
                {
                    Directory.CreateDirectory(configDir);
                }
                catch (Exception e)
                {
                    var sb = new StringBuilder(string.Format(Resources.ConfigRunner_CreateConfigDir_Configuration_directory___0___could_not_be_created_for_configuration___1___
                        , configDir, GetConfigName()));
                    sb.AppendLine();
                    sb.AppendLine(e.Message);
                    throw new ConfigRunnerException(sb.ToString(), e);
                }
            }
        }

        public string GetLogDirectory()
        {
            return Path.GetDirectoryName(_logger.LogFile);
        }

        public void Start()
        {
            _panoramaUploadError = false;
            _panoramaFatalError = false;

            CreateConfigDir();
            _logger.Init(); // Create the log file after the config directory has been created.
            
            RunBackgroundWorker(RunConfiguration, ProcessFilesCompleted);
        }

        private void LogStartMessage()
        {
            var msg = new StringBuilder("Starting configuration...").AppendLine();
            msg.AppendLine(string.Format("Version: {0}", Program.Version()));
            msg.Append(Config).AppendLine();
            _logger.Log(msg.ToString());
        }

        public void ChangeStatus(RunnerStatus runnerStatus, bool updateUi = true)
        {
            lock (_lock)
            {
                _runnerStatus = runnerStatus;
                if (IsStopped() && updateUi) 
                {
                    ((MainForm)_uiControl)?.DisableConfig(Config, _runnerStatus);
                    return;
                }
            }
            if (updateUi) _uiControl?.UpdateUiConfigurations();
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
                Config.Validate(true);
            }
            catch (Exception x)
            {
                ((MainForm)_uiControl)?.SetConfigInvalid(Config); // TODO: Another way to add the configs to the invalid config list?
                SetErrorStateDisplayAndLogException(string.Format(Resources.ConfigRunner_RunConfiguration_Error_validating_configuration___0___, Config.Name), x, false);
                return;
            }
            
            try
            {
                LogStartMessage();

                _fileWatcher = new AutoQCFileSystemWatcher(_logger, this);
                if (Config.MainSettings.HasAnnotationsFile())
                {
                    _annotationsFileWatcher = new AnnotationsFileWatcher(_logger, this);
                }

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
                _annotationsFileWatcher?.Init(Config);

                Log("Running configuration...");
                ChangeStatus(RunnerStatus.Running);

                if (ProcessExistingFiles(e))
                {
                    ProcessNewFiles(e);
                }
                e.Result = COMPLETED;
            }
            catch (FileWatcherException x)
            {
                SetErrorStateDisplayAndLogException(string.Format(Resources.ConfigRunner_RunConfiguration_There_was_an_error_looking_for_files_for_configuration___0___, Config.Name), x);
            }
            catch (Exception x)
            {
                SetErrorStateDisplayAndLogException(string.Format(Resources.ConfigRunner_RunConfiguration_There_was_an_error_running_configuration___0___, Config.Name), x);
            }
        }

        private void SetErrorStateDisplayAndLogException(string message, Exception x, bool showException = true)
        {
            LogException(x, message);
            ChangeStatus(RunnerStatus.Error);
            if (showException)
            {
                _uiControl?.DisplayErrorWithException(message, x);
            }
            else
            {
                _uiControl?.DisplayError(TextUtil.LineSeparate(message, x.Message));
            }
        }

        private void ProcessNewFiles(DoWorkEventArgs e)
        {
            Log("Importing new files...");
            Waiting = false;
            while (true)
            {
                if (_worker.CancellationPending)
                {
                    e.Result = CANCELED;
                    break;
                }

                // Import the annotations file if it has changed
                ImportAnnotationsFileIfChanged();

                var filePath = _fileWatcher.GetFile();
               
                if (filePath != null)
                {
                    Waiting = false;

                    var importContext = new ImportContext(filePath);
                    var success = ImportFile(e, importContext);

                    if (success)
                    {
                        // Make one last attempt to import any old files. 
                        // Any files that still do not import successfully
                        // will be removed from the re-import queue.
                        TryReimportOldFiles(e, true);
                    }
                }
                else
                {
                    // Try to import any older files that resulted in an import error the first time.
                    TryReimportOldFiles(e, false);
                }

                if (_panoramaFatalError)
                {
                    LogError(
                        "Document could not be imported on Panorama. Stopping configuration. Please review the errors before restarting the configuration.");
                    break;
                }

                if (_panoramaUploadError)
                {
                    if (_lastUploadAt.AddMinutes(5) < DateTime.Now)
                    {
                        // Try uploading to Panorama every 5 minutes until we succeed
                        Log("Trying to upload the Skyline document to Panorama again. ");
                        UploadToPanorama(PanoramaArgs(), PanoramaArgs(true));
                    }

                    // If we haven't been able to upload to Panorama for 12 hours, stop with an error
                    if (!DateTime.MinValue.Equals(_panoramaErrorOccurred) && // _panoramaErrorOccurred.AddMinutes(5) < DateTime.Now)
                        _panoramaErrorOccurred.AddHours(12) < DateTime.Now)
                    {
                        LogError("Uploads to Panorama have not been successful in over 12 hours. Stopping configuration.");
                        break;
                    }
                }

                if (_fileWatcher.GetQueueCount() == 0)
                {
                    if (!Waiting)
                    {
                        Log("Waiting for files...");
                    }
                    Waiting = true;
                    Thread.Sleep(WAIT_FOR_NEW_FILE);
                }
            }
        }

        private void TryReimportOldFiles(DoWorkEventArgs e, bool forceImport)
        {
            var toReimport = new List<string>();
            var failed = new List<RawFile>();

            while (_fileWatcher.GetReimportQueueCount() > 0)
            {
                var file = _fileWatcher.GetNextFileToReimport();
                if (forceImport || file.TryReimport())
                {
                    toReimport.Add(file.FilePath);
                }
                else if (_fileWatcher.RawDataExists(file.FilePath))
                {
                    failed.Add(file);
                }
            }

            foreach (var file in failed)
            {
                if (_fileWatcher.RawDataExists(file.FilePath))
                {
                    _fileWatcher.AddToReimportQueue(file);
                }
            }

            if (toReimport.Count == 0) return;

            var importContext = new ImportContext(toReimport);

            while (importContext.GetNextFile() != null)
            {
                if (_worker.CancellationPending)
                {
                    e.Result = CANCELED;
                    return;
                }

                var filePath = importContext.GetCurrentFile();
                if (!_fileWatcher.RawDataExists(filePath))
                {
                    // User may have deleted this file.
                    Log(Resources.ConfigRunner_TryReimportOldFiles__0__no_longer_exists__Skipping___, filePath);
                    continue;
                }

                _logger.Log(string.Format(Resources.ConfigRunner_TryReimportOldFiles_Attempting_to_re_import__0__, filePath));
                if (!ImportFile(e, importContext, false))
                {
                    if (forceImport)
                    {
                        // forceImport is true when we attempt to import failed files after successfully importing a newer file.
                        // If the file still fails to import we will not add it back to the re-import queue.
                        _logger.Log(string.Format(Resources.ConfigRunner_TryReimportOldFiles__0__failed_to_import_successfully__Skipping___, filePath));
                    }
                    else
                    {
                        if (_fileWatcher.RawDataExists(filePath))
                        {
                            _logger.Log(string.Format(Resources.ConfigRunner_TryReimportOldFiles_Adding__0__to_the_re_import_queue_, filePath));
                            _fileWatcher.AddToReimportQueue(filePath);
                        }
                        else
                        {
                            _logger.Log(string.Format(Resources.ConfigRunner_TryReimportOldFiles__0__no_longer_exists__Skipping___, filePath));
                        }

                    }
                }
            }
        }

        private void ProcessFilesCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Task.Run(() =>
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
                else
                {
                    Log(Resources.ConfigRunner_ProcessFilesCompleted_Finished_running_configuration_);
                }

                Stop();
            });
        }

        private bool ProcessExistingFiles(DoWorkEventArgs e)
        {
            // Queue up any existing data files in the folder
            _logger.Log(Resources.ConfigRunner_ProcessExistingFiles_Importing_existing_files___);
            var files = _fileWatcher.GetExistingFiles();

            // Enable notifications on new files that get added to the folder.
            _fileWatcher.StartWatching();
            _annotationsFileWatcher?.StartWatching();

            if (files.Count == 0)
            {
                Log(Resources.ConfigRunner_ProcessExistingFiles_No_existing_files_found_);
                if (DocumentHasReplicates())
                {
                    ImportAnnotationsFileIfWatching(false);
                    UploadToPanorama(PanoramaArgs(), PanoramaArgs(true));
                }
                return true;
            }
            
            Log("Existing files found: {0}", files.Count);

            var importContext = new ImportContext(files, true);
            var skipped = 0;
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

                var lastAcquiredFileDate = LastAcquiredFileDate;
                var fileLastWriteTime = File.GetLastWriteTime(filePath);
                if (fileLastWriteTime.CompareTo(lastAcquiredFileDate.AddSeconds(1)) < 0)
                {
                    Log(Resources.ConfigRunner_ProcessExistingFiles__0__was_acquired___1___before_the_acquisition_date___2___on_the_last_imported_file_in_the_Skyline_document__Skipping___,
                        GetFilePathForLog(filePath),
                        fileLastWriteTime,
                        lastAcquiredFileDate);
                    skipped++;
                    continue;
                }

                ImportFile(e, importContext);
            }

            if (skipped == files.Count && DocumentHasReplicates())
            {
                ImportAnnotationsFileIfWatching(false);
                UploadToPanorama(PanoramaArgs(), PanoramaArgs(true));
            }
            Log(Resources.ConfigRunner_ProcessExistingFiles_Finished_importing_existing_files___);
            return true;
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
                    LogException(fse, Resources.ConfigRunner_ImportFile_Error_getting_status_of_file__0__, GetFilePathForLog(filePath));
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

            var resultsImported = ProcessOneFile(importContext);
            if (!resultsImported && addToReimportQueueOnFailure)
            {
                AddToReimportQueue(filePath);
            }

            return resultsImported;
        }

        private void AddToReimportQueue(string filePath)
        {
            _logger.Log(string.Format(Resources.ConfigRunner_TryReimportOldFiles_Adding__0__to_the_re_import_queue_,
                GetFilePathForLog(filePath)));
                _fileWatcher.AddToReimportQueue(filePath);
        }

        private bool ProcessOneFile(ImportContext importContext)
        {
            CreateZipArchiveBefore(importContext);

            var importSucceeded = ImportResults(importContext);

            // Import the annotations file after importing all the existing raw data. 
            if (importContext.ImportingMultiple && importContext.ImportingLast())
            {
                if (importContext.InitialImport)
                {
                    // Import annotations after importing any existing files when the config starts.
                    ImportAnnotationsFileIfWatching(false);
                }
                else if (importContext.ImportCount > 0)
                {
                    // If this is not the initial import (when a config starts) but we are re-importing previously failed imports,
                    // then import annotations only if new replicates were added and annotation import had failed previously.
                    ImportAnnotationsIfRequired();
                }
            }
            else if (!importContext.ImportingMultiple && importSucceeded)
            {
                ImportAnnotationsIfRequired();
            }

            UploadToPanorama(importContext);

            CreateZipArchiveAfter(importContext);

            return importSucceeded;
        }

        private bool ImportResults(ImportContext importContext)
        {
            var skylineRunnerArgs = ImportResultsFileArgs(importContext);
            var importResultsFileProc = new ProcessInfo(Config.SkylineSettings.CmdPath, skylineRunnerArgs, skylineRunnerArgs);
            var status = RunProcess(importResultsFileProc);
            if (status == ProcStatus.Error)
            {
                return false;
            }

            if (status != ProcStatus.Skipped)
            {
                importContext.IncrementImported();
            }

            return true;
        }

        private void UploadToPanorama(ImportContext importContext)
        {
            var panoramaUploadArgs = PanoramaArgs(importContext);
            if (!string.IsNullOrEmpty(panoramaUploadArgs))
            {
                if (importContext.ImportCount > 0 || (importContext.InitialImport && DocumentHasReplicates()))
                {
                    var argsToPrint = PanoramaArgs(importContext, true);
                    UploadToPanorama(panoramaUploadArgs, argsToPrint);
                }
                else
                {
                    // Nothing was imported, and the Skyline document did not have any imported results when the configuration was started.
                    LogError("No results were imported. Skipping upload to Panorama.");
                }
            }
        }

        private bool DocumentHasReplicates()
        {
            return !DateTime.MinValue.Equals(LastAcquiredFileDate);
        }

        private void UploadToPanorama(string panoramaUploadArgs, string argsToPrint)
        {
            if (!Config.PanoramaSettings.PublishToPanorama)
                return;
            _logger.Log("Uploading Skyline document to Panorama.");
            var uploadToPanoramaProc = new ProcessInfo(Config.SkylineSettings.CmdPath, panoramaUploadArgs, argsToPrint);
            var status = RunProcess(uploadToPanoramaProc);
            _lastUploadAt = DateTime.Now;
            if (status == ProcStatus.FatalPanoramaError)
            {
                _panoramaFatalError = true;
            }
            else if (status == ProcStatus.Error)
            {
                _panoramaUploadError = true;
                if (DateTime.MinValue.Equals(_panoramaErrorOccurred))
                {
                    // Set the time when the first error occurred
                    _panoramaErrorOccurred = _lastUploadAt;
                }
            }
            else
            {
                _panoramaUploadError = false;
                _panoramaErrorOccurred = DateTime.MinValue;
            }
        }

        private void CreateZipArchiveBefore(ImportContext importContext)
        {
            var runBeforeProc = RunBefore(importContext);
            if (runBeforeProc != null)
            {
                runBeforeProc.WorkingDirectory = importContext.WorkingDir;
                RunProcess(runBeforeProc); // Create a zip archive BEFORE if NOT importing existing files
            }
        }

        private void CreateZipArchiveAfter(ImportContext importContext)
        {
            var runAfter = RunAfter(importContext);
            if (runAfter != null && importContext.ImportCount > 0)
            {
                RunProcess(runAfter); // Create a zip archive AFTER importing all files if importing existing files
            }
        }

        private void ImportAnnotationsIfRequired()
        {
            if (_annotationsImportError || _annotationsFirstImport)
            {
                // Importing annotations if:
                // 1. annotations have not yet been imported
                // 2. previous attempt to import annotations was unsuccessful. 
                var logMessage = _annotationsImportError ? "Attempting to re-import the annotations file." : null;
                ImportAnnotationsFileIfWatching(false, logMessage);
                if (_annotationsImportError)
                {
                    LogError("There were errors importing the annotations file.");
                }
            }
        }

        private void ImportAnnotationsFileIfWatching(bool uploadToPanorama, string logMessage = null)
        {
            if (_annotationsFileWatcher != null)
            {
                Waiting = false;
                Log(logMessage ?? Resources.ConfigRunner_ImportAnnotationsFileIfWatching_Importing_annotations_file_);
                ImportAnnotationsFile(uploadToPanorama);
            }
        }

        private void ImportAnnotationsFileIfChanged()
        {
            if (AnnotationsFileUpdated)
            {
                AnnotationsFileUpdated = false;
                ImportAnnotationsFileIfWatching(true);
            }
        }

        private void ImportAnnotationsFile(bool uploadToPanorama = true)
        {
            var args = string.Format("--in=\"{0}\" --import-annotations=\"{1}\" --save", Config.MainSettings.SkylineFilePath, Config.MainSettings.AnnotationsFilePath);

            var procInfo = new ProcessInfo(Config.SkylineSettings.CmdPath, args, args);

            var status = RunProcess(procInfo);
            _annotationsImportError = status == ProcStatus.Error;
            _annotationsFirstImport = false;
            Log(!_annotationsImportError
                ? Resources.ConfigRunner_ImportAnnotationsFile_Annotations_file_was_imported_
                : Resources.ConfigRunner_ImportAnnotationsFile_Annotations_file_could_not_be_imported_);

            if (!_annotationsImportError && Config.PanoramaSettings.PublishToPanorama && uploadToPanorama)
            {
                // Upload to Panorama
                UploadToPanorama(PanoramaArgs(), PanoramaArgs(true));
            }
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
                _annotationsFileWatcher?.Stop();

                if (_worker != null && _worker.IsBusy)
                {
                    ChangeStatus(RunnerStatus.Stopping);
                    CancelAsync();
                }
                else if(_runnerStatus != RunnerStatus.Error)
                {
                    ChangeStatus(_panoramaUploadError || _panoramaFatalError ? RunnerStatus.Error : RunnerStatus.Stopped);
                }

                if (_runnerStatus == RunnerStatus.Stopped && (_panoramaUploadError || _panoramaFatalError || _annotationsImportError))
                {
                    ChangeStatus(RunnerStatus.Error);
                }

                if (IsStopped())
                {
                    _logger?.Close();
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
                                            bool.TryParse(reader.Value, out var integrateAll);
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
                LogException(logger, e, Resources.ConfigRunner_IsIntegrateAllChecked_Error_reading_file__0__, mainSettings.SkylineFilePath);
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
            var reportFile = Config.getConfigFilePath("AcquisitionTimes.csv");

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
                LastAcquiredFileDate = lastAcquiredFileDate;
                if (!lastAcquiredFileDate.Equals(DateTime.MinValue))
                {
                    logger.Log(string.Format(Resources.ConfigRunner_ReadLastAcquiredFileDate_The_most_recent_acquisition_date_in_the_Skyline_document_is__0_, lastAcquiredFileDate));
                }
                else
                {
                    logger.Log(Resources.ConfigRunner_ReadLastAcquiredFileDate_The_Skyline_document_does_not_have_any_imported_results_);  
                }
            }
            catch (IOException e)
            {
                LogException(logger, e, Resources.ConfigRunner_IsIntegrateAllChecked_Error_reading_file__0__, reportFile);
                return false;
            }
            return true;
        }

        private static DateTime GetLastAcquiredFileDate(string reportFile, Logger logger)
        {
            var lastAcq = DateTime.MinValue;

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
                            LogException(logger, e, Resources.ConfigRunner_GetLastAcquiredFileDate_Error_parsing_acquired_time_from_Skyline_report___0_, reportFile);
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

        public string GetArchiveArgs(DateTime archiveDate, DateTime currentDate)
        {
            if (currentDate.CompareTo(archiveDate) < 0)
                return null;

            if (currentDate.Year == archiveDate.Year && currentDate.Month == archiveDate.Month)
            {
                return null;
            }

            // Return args to archive the file: create a shared zip
            var archiveFileName = string.Format("{0}_{1:D4}_{2:D2}.sky.zip",
                Path.GetFileNameWithoutExtension(Config.MainSettings.SkylineFilePath),
                archiveDate.Year,
                archiveDate.Month);

            // Archive file will be written in the same directory as the Skyline file.
            return string.Format("--share-zip={0}", archiveFileName);
        }

        public DateTime GetLastArchivalDate()
        {
            return GetLastArchivalDate(new FileSystemUtil());
        }

        public DateTime GetLastArchivalDate(IFileSystemUtil fileUtil)
        {
            if (!DateTime.MinValue.Equals(LastArchivalDate))
            {
                return LastArchivalDate;
            }

            if (DocumentHasReplicates())
            {
                LastArchivalDate = LastAcquiredFileDate;
                return LastArchivalDate;
            }

            var fileName = Path.GetFileNameWithoutExtension(Config.MainSettings.SkylineFilePath);
            var pattern = fileName + "_\\d{4}_\\d{2}.sky.zip";
            var regex = new Regex(pattern);

            var skylineFileDir = Path.GetDirectoryName(Config.MainSettings.SkylineFilePath);

            // Look at any existing .sky.zip files to determine the last archival date
            // Look for shared zip files with file names like <skyline_file_name>_<yyyy>_<mm>.sky.zip
            var archiveFiles =
                fileUtil.GetSkyZipFiles(skylineFileDir)
                    .Where(f => regex.IsMatch(Path.GetFileName(f) ?? string.Empty))
                    .OrderBy(filePath => fileUtil.LastWriteTime(filePath))
                    .ToList();

            LastArchivalDate = archiveFiles.Any() ? fileUtil.LastWriteTime(archiveFiles.Last()) : DateTime.Today;

            return LastArchivalDate;
        }

        private void Log(string message, params object[] args)
        {
            _logger.Log(string.Format(message, args));    
        }

        private void LogError(string message, params object[] args)
        {
            _logger.LogError(string.Format(message, args));
        }

        private void LogException(Exception e, string message, params object[] args)
        {
            _logger.LogError(string.Format(message, args), e.ToString());
        }

        private static void LogException(Logger logger, Exception e, string message, params object[] args)
        {
            logger.LogError(string.Format(message, args), e.ToString());
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

        public static bool IsBusy(RunnerStatus status)
        {
            return !(status == RunnerStatus.Stopped || status == RunnerStatus.Error);
        }

        public bool IsPending()
        {
            return IsStarting() || IsStopping() || IsLoading();
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

        public bool IsLoading()
        {
            return _runnerStatus == RunnerStatus.Loading;
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

        public bool IsCanceling()
        {
            return _runnerStatus == RunnerStatus.Canceling;
        }

        public bool IsWaiting()
        {
            return _runnerStatus == RunnerStatus.Waiting;
        }

        public bool CanStart()
        {
            return IsStopped() || IsError();
        }

        public string ImportResultsFileArgs(ImportContext importContext)
        {
            // Get the current results time window
            var currentDate = DateTime.Today;
            var accumulationWindow = AccumulationWindow.Get(currentDate, Config.MainSettings.ResultsWindow);

            var args = new StringBuilder();
            // Input Skyline file
            args.Append(string.Format(" --in=\"{0}\"", Config.MainSettings.SkylineFilePath));

            string importOnOrAfter = string.Empty;
            if (importContext.InitialImport || !Config.MainSettings.RemoveResults)
            {
                // We are importing existing files in the folder.  The import-on-or-after is determined
                // by the last acquisition date on the files already imported in the Skyline document.
                // If the Skyline document does not have any results files, we will import all existing
                // files in the folder.
                if (DocumentHasReplicates())
                {
                    importOnOrAfter = string.Format(" --import-on-or-after={0}", LastAcquiredFileDate);
                }
            }
            else
            {
                importOnOrAfter = string.Format(" --import-on-or-after={0}",
                    accumulationWindow.StartDate.ToShortDateString());

                if (Config.MainSettings.RemoveResults)
                {
                    // Add arguments to remove files older than the start of the rolling window.   
                    args.Append(string.Format(" --remove-before={0}",
                        accumulationWindow.StartDate.ToShortDateString()));
                }
            }

            // Add arguments to import the results file
            args.Append(string.Format(" --import-file=\"{0}\"{1}", importContext.GetCurrentFile(), importOnOrAfter));

            // Save the Skyline file
            args.Append(" --save");

            return args.ToString();
        }

        private string PanoramaArgs(ImportContext importContext, bool toPrint = false)
        {
            if (!Config.PanoramaSettings.PublishToPanorama || importContext.ImportingMultiple && !importContext.ImportingLast())
            {
                // Do not upload to Panorama if we are importing existing documents and this is not the 
                // last file being imported.
                return string.Empty;
            }

            return PanoramaArgs(toPrint);
        }

        private string PanoramaArgs(bool toPrint = false)
        {
            var args = new StringBuilder();
            // Input Skyline file
            args.Append(string.Format(" --in=\"{0}\"", Config.MainSettings.SkylineFilePath));

            var passwdArg = toPrint ? string.Empty : string.Format(" --panorama-password=\"{0}\"", Config.PanoramaSettings.PanoramaPassword);
            var uploadArgs = string.Format(
                " --panorama-server=\"{0}\" --panorama-folder=\"{1}\" --panorama-username=\"{2}\" {3}",
                Config.PanoramaSettings.PanoramaServerUrl,
                Config.PanoramaSettings.PanoramaFolder,
                Config.PanoramaSettings.PanoramaUserEmail,
                passwdArg);
            return args.Append(uploadArgs).ToString();
        }

        public ProcessInfo RunBefore(ImportContext importContext)
        {
            string archiveArgs = null;
            var currentDate = DateTime.Today;
            if (!importContext.InitialImport)
            {
                // If we are NOT importing existing results, create an archive (if required) of the 
                // Skyline document BEFORE importing a results file.
                archiveArgs = GetArchiveArgs(GetLastArchivalDate(), currentDate);
            }
            if (string.IsNullOrEmpty(archiveArgs))
            {
                return null;
            }
            LastArchivalDate = currentDate;
            var args = string.Format("--in=\"{0}\" {1}", Config.MainSettings.SkylineFilePath, archiveArgs);
            return new ProcessInfo(Config.SkylineSettings.CmdPath, args, args);
        }

        public ProcessInfo RunAfter(ImportContext importContext)
        {
            string archiveArgs = null;
            var currentDate = DateTime.Today;
            if (importContext.InitialImport && importContext.ImportingLast())
            {
                // If we are importing existing files in the folder, create an archive (if required) of the 
                // Skyline document AFTER importing the last results file.
                var oldestFileDate = importContext.GetOldestImportedFileDate(LastAcquiredFileDate);
                var today = DateTime.Today;
                if (oldestFileDate.Year < today.Year || oldestFileDate.Month < today.Month)
                {
                    archiveArgs = GetArchiveArgs(currentDate.AddMonths(-1), currentDate);
                }
            }
            if (string.IsNullOrEmpty(archiveArgs))
            {
                return null;
            }
            LastArchivalDate = currentDate;
            var args = string.Format("--in=\"{0}\" {1}", Config.MainSettings.SkylineFilePath, archiveArgs);
            return new ProcessInfo(Config.SkylineSettings.CmdPath, args, args);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return false;
        }

        #region [Implementation of IProcessControl interface]
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
        ProcStatus RunProcess(ProcessInfo processInfo);
        void StopProcess();
    }

    public enum ProcStatus
    {
        Success,
        Error,
        FatalPanoramaError,
        Skipped
    }

    public class ConfigRunnerException : SystemException
    {
        public ConfigRunnerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ConfigRunnerException(string message) : base(message)
        {
        }
    }
}
