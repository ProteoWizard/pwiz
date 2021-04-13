/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class ConfigRunner : IConfigRunner
    {
        public static readonly string ALLOW_NEWLINE_SAVE_VERSION = "20.2.1.454";


        private readonly IMainUiControl _uiControl;
        private readonly Logger _logger;

        private readonly object _lock = new object();
        private RunnerStatus _runnerStatus;
        private readonly ProcessRunner _processRunner;
        private List<string> _batchCommandsToLog;

        private List<CancellationTokenSource> _runningCancellationTokens;

        public ConfigRunner(SkylineBatchConfig config, Logger logger, IMainUiControl uiControl = null)
        {
            _runnerStatus = RunnerStatus.Stopped;
            Config = config;
            _uiControl = uiControl;
            _logger = logger;
            _batchCommandsToLog = new List<string>();
            _runningCancellationTokens = new List<CancellationTokenSource>();

            _processRunner = new ProcessRunner()
            {
                OnDataReceived = DataReceived,
                OnException = (e, message) => _logger?.LogException(e, message),
                OnError = () =>
                {
                    if (IsRunning())
                        ChangeStatus(RunnerStatus.Error);
                }
            };
        }
        
        public SkylineBatchConfig Config { get; }
        public DateTime? StartTime { get; private set; }
        public TimeSpan? RunTime { get; private set; }

        public IConfig GetConfig()
        {
            return Config;
        }

        public Color GetDisplayColor()
        {
            return Color.Black;
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
            RunnerStatus status = GetStatus();
            return status == RunnerStatus.Stopped ? string.Empty : status.ToString();
        }

        public string GetConfigName()
        {
            return Config.Name;
        }


        private void LogToUi(string line)
        {
            if (_uiControl != null)
                _logger.Log(line);
        }

        public async Task Run(int startStep)
        {
            LogToUi(string.Format(Resources.ConfigRunner_Run________________________________Starting_Configuration___0_________________________________, Config.Name));
            try
            {
                Config.Validate();

            } catch (ArgumentException e)
            {
                LogToUi("Error: " + e.Message);
                ChangeStatus(RunnerStatus.Error);
                LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, Config.Name, GetStatus()));
                return;
            }

            StartTime = DateTime.Now;
            ChangeStatus(RunnerStatus.Running);
            Config.MainSettings.CreateAnalysisFolderIfNonexistent();

            if (startStep == 1 && Config.MainSettings.WillDownloadData)
            {
                await DownloadData();
            }
            
            if (startStep != 5 && IsRunning())
            {
                var multiLine = await Config.SkylineSettings.HigherVersion(ALLOW_NEWLINE_SAVE_VERSION, _processRunner);
                if (IsRunning())
                {
                    // Writes the batch commands for steps 1-4 to a file
                    var commandWriter = new CommandWriter(_logger, multiLine);
                    WriteBatchCommandsToFile(commandWriter, startStep);
                    _batchCommandsToLog = commandWriter.LogLines;
                    // Runs steps 1-4
                    var command = string.Format("--batch-commands=\"{0}\"", commandWriter.GetCommandFile());
                    await _processRunner.Run(Config.SkylineSettings.CmdPath, command);
                    // Consider: deleting tmp command file
                }
            }

            // STEP 5: run r scripts using csv files
            var rScriptsRunInformation = Config.GetScriptArguments();
            foreach(var rScript in rScriptsRunInformation)
                if (IsRunning())
                    await _processRunner.Run(rScript[RRunInfo.ExePath], rScript[RRunInfo.Arguments]);
            

            // Runner is still running if no errors or cancellations
            if (IsRunning()) ChangeStatus(RunnerStatus.Completed);
            if (IsCanceling()) ChangeStatus(RunnerStatus.Canceled);
            var endTime = DateTime.Now;
            // ReSharper disable once PossibleInvalidOperationException - StartTime is always defined here
            var delta = endTime - (DateTime)StartTime;
            RunTime = delta;
            var runTimeString = delta.Hours > 0 ? delta.ToString(@"hh\:mm\:ss") : delta.ToString(@"mm\:ss");
            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, Config.Name, GetStatus()));
            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, "Runtime", runTimeString));
            _uiControl?.UpdateUiConfigurations();
        }

        public void WriteBatchCommandsToFile(CommandWriter commandWriter, int startStep)
        {
            // STEP 1: open skyline file and save copy to analysis folder
            if (startStep == 1)
            {
                Config.WriteOpenSkylineTemplateCommand(commandWriter);
                Config.WriteMsOneCommand(commandWriter);
                Config.WriteMsMsCommand(commandWriter);
                Config.WriteRetentionTimeCommand(commandWriter);
                Config.WriteAddDecoysCommand(commandWriter);
                Config.WriteSaveToResultsFile(commandWriter);
                commandWriter.EndCommandGroup();
            }
            else if (startStep < 4)
            {
                Config.WriteOpenSkylineResultsCommand(commandWriter);
            }

            // STEP 2: import data to new skyline file
            if (startStep <= 2)
            {
                // import data and train model
                Config.WriteImportDataCommand(commandWriter);
                Config.WriteImportNamingPatternCommand(commandWriter);
                Config.WriteTrainMProphetCommand(commandWriter);
                Config.WriteImportAnnotationsCommand(commandWriter);
                Config.WriteSaveCommand(commandWriter);
                commandWriter.EndCommandGroup();
            }

            // STEP 3: refine file and save to new location
            if (startStep <= 3)
                Config.WriteRefineCommands(commandWriter);

            // STEP 4: output report(s) for completed analysis
            if (startStep <= 4)
            {
                if (Config.ReportSettings.UsesRefinedFile())
                {
                    if (!commandWriter.CurrentSkylineFile.Equals(Config.RefineSettings.OutputFilePath))
                        Config.WriteOpenRefineFileCommand(commandWriter);
                    Config.WriteRefinedFileReportCommands(commandWriter);
                }
                if (!commandWriter.CurrentSkylineFile.Equals(Config.MainSettings.GetResultsFilePath()))
                    Config.WriteOpenSkylineResultsCommand(commandWriter);
                Config.WriteResultsFileReportCommands(commandWriter);
            }
        }

        private async Task DownloadData()
        {
            var mainSettings = Config.MainSettings;
            var server = mainSettings.Server;
            if (!server.Validated)
                server.Validate();

            var allFiles = server.GetServerFiles;
            var fileNames = allFiles.Keys;
            var existingDataFiles = Directory.GetFiles(mainSettings.DataFolderPath).Length;
            var dataFilter = new Regex(mainSettings.DataNamingPattern);
            var downloadingFilesEnum =
                from name in fileNames
                where dataFilter.IsMatch(name)
                select name;
            var downloadingFiles = downloadingFilesEnum.ToList();
            if (downloadingFiles.Count == existingDataFiles) return;

            _logger.Log(string.Format(Resources.ConfigRunner_DownloadData_Downloading__0__data_files_from__1__, downloadingFiles.Count, server.Url));
            foreach (var file in downloadingFiles)
                _logger.Log(file);

            long totalSize = 0;
            foreach (var fileName in downloadingFiles)
                totalSize += allFiles[fileName].Size;
            long downloadedSize = 0;
            var ftpClient = server.GetFtpClient();
            var source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            _runningCancellationTokens.Add(source);
            ftpClient.Connect();
            foreach (var fileName in downloadingFiles)
            {
                if (!IsRunning()) break;
                var currentFileSize = (double) allFiles[fileName].Size;
                var size = downloadedSize;
                Progress<FtpProgress> progress = new Progress<FtpProgress>(p =>
                {
                    var fileSizeDownloaded = currentFileSize * p.Progress / 100;
                    var currentPercentComplete = Math.Floor((size + fileSizeDownloaded) / totalSize * 100);
                    _logger.Log(string.Format("{0}%", currentPercentComplete));
                });
                try
                {
                    await ftpClient.DownloadFileAsync(Path.Combine(mainSettings.DataFolderPath, fileName),
                        server.FilePath(fileName), token: token, existsMode: FtpLocalExists.Overwrite, progress: progress);
                }
                catch (OperationCanceledException)
                {
                    // pass
                }
                catch (Exception e)
                {
                    _logger.LogException(e, Resources.ConfigRunner_DownloadData_An_error_occurred_while_downloading_the_data_files_);
                    ChangeStatus(RunnerStatus.Error);
                }
                
                downloadedSize += allFiles[fileName].Size;
            }
            ftpClient.Disconnect();

            if (IsRunning())
                _logger.Log(string.Format("{0}%", 100));
        }

        

        private void DataReceived(string data)
        {
            if ((IsRunning() || IsCanceling()) && data != null && _logger != null)
            {
                if (data.StartsWith("Error") || data.StartsWith("Fatal error"))
                    _logger.LogError(data);
                else
                    _logger.Log(data);

                if (data.StartsWith("--batch-commands"))
                {
                    foreach (var line in _batchCommandsToLog)
                        _logger.Log(line);
                    _batchCommandsToLog = new List<string>();
                }
            }
        }

        public void ChangeStatus(RunnerStatus runnerStatus)
        {
            lock (_lock)
            {
                if (_runnerStatus == runnerStatus)
                    return;
                if (runnerStatus == RunnerStatus.Waiting)
                {
                    StartTime = null;
                    RunTime = null;
                }
                if (IsRunning())
                    foreach (var token in _runningCancellationTokens)
                        token.Cancel();
                _runnerStatus = runnerStatus;
            }
            _uiControl?.UpdateUiConfigurations();
        }

        public void Cancel()
        {
            if (IsRunning())
            {
                _processRunner.Cancel();
                ChangeStatus(RunnerStatus.Canceling);
            }
            if (IsWaiting())
                ChangeStatus(RunnerStatus.Stopped);                                                                                            
        }

        public bool IsCanceling()
        {
            return _runnerStatus == RunnerStatus.Canceling;
        }
        
        public bool IsBusy()
        {
            return IsRunning() || IsWaiting() || IsCanceling();
        }
        
        public bool IsStopped()
        {
            return _runnerStatus == RunnerStatus.Stopped;
        }

        public bool IsCompleted()
        {
            return _runnerStatus == RunnerStatus.Completed;
        }

        public bool IsRunning()
        {
            return _runnerStatus == RunnerStatus.Running;
        }

        public bool IsError()
        {
            return _runnerStatus == RunnerStatus.Error;
        }

        public bool IsWaiting()
        {
            return _runnerStatus == RunnerStatus.Waiting;
        }
    }
}
