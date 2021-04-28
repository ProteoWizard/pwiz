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
using System.Globalization;
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
        public static readonly string REPORT_INVARIANT_VERSION = "21.1.0.0"; // TODO(Ali): replace this with release version name


        private readonly IMainUiControl _uiControl;
        private readonly Logger _logger;

        private readonly object _lock = new object();
        private RunnerStatus _runnerStatus;
        private readonly ProcessRunner _processRunner;
        private string _batchFile;

        private CancellationTokenSource _runningCancellationToken;

        public ConfigRunner(SkylineBatchConfig config, Logger logger, IMainUiControl uiControl = null)
        {
            _runnerStatus = RunnerStatus.Stopped;
            Config = config;
            _uiControl = uiControl;
            _logger = logger;

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
                _logger?.LogErrorNoPrefix(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, Config.Name, GetStatus()));
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
                var numberFormat = CultureInfo.CurrentCulture.GetFormat(typeof(NumberFormatInfo)) as NumberFormatInfo;
                var internationalSeparator = numberFormat != null && Equals(TextUtil.SEPARATOR_CSV.ToString(CultureInfo.InvariantCulture), numberFormat.NumberDecimalSeparator);
                var invariantReport = !internationalSeparator || await Config.SkylineSettings.HigherVersion(REPORT_INVARIANT_VERSION);
                if (IsRunning())
                {
                    // Writes the batch commands for steps 1-4 to a file
                    var commandWriter = new CommandWriter(_logger, multiLine, invariantReport);
                    WriteBatchCommandsToFile(commandWriter, startStep, invariantReport);
                    _batchFile = commandWriter.GetCommandFile();
                    // Runs steps 1-4
                    var command = string.Format("--batch-commands=\"{0}\"", _batchFile);
                    await _processRunner.Run(Config.SkylineSettings.CmdPath, command);
                    // Consider: deleting tmp command file
                }
                if (!invariantReport && IsRunning())
                {
                    foreach (var report in Config.ReportSettings.Reports)
                    {
                        _logger.Log(string.Format(Resources.ConfigRunner_Run_Converting__0__to_invariant_format___, report.Name));
                        if (!report.CultureSpecific)
                        {
                            var reportPath = Path.Combine(Config.MainSettings.AnalysisFolderPath,
                                report.Name + TextUtil.EXT_CSV);
                            _logger.Log(string.Format(Resources.ConfigRunner_Run_Reading__0_, report.Name + TextUtil.EXT_CSV));
                            string text = File.ReadAllText(reportPath);
                            _logger.Log(string.Format(Resources.ConfigRunner_Run_Updating__0_, report.Name + TextUtil.EXT_CSV));
                            text = text.Replace('\t', ',');
                            _logger.Log(string.Format(Resources.ConfigRunner_Run_Saving__0_, report.Name + TextUtil.EXT_CSV));
                            File.WriteAllText(reportPath, text);
                        }
                    }
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
            if (!IsError())
                LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, Config.Name, GetStatus()));
            else
                _logger?.LogErrorNoPrefix(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, Config.Name, GetStatus()));
            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, "Runtime", runTimeString));
            _uiControl?.UpdateUiConfigurations();
        }

        public void WriteBatchCommandsToFile(CommandWriter commandWriter, int startStep, bool invariantReport)
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
            var dataFilter = new Regex(mainSettings.Server.DataNamingPattern);
            var downloadingFilesEnum =
                from name in fileNames
                where dataFilter.IsMatch(name)
                select name;
            var downloadingFiles = downloadingFilesEnum.ToList();
            var skippingFiles = new List<string>();
            foreach (var downloadingFile in downloadingFiles)
            {
                var fileName = Path.Combine(mainSettings.DataFolderPath, downloadingFile);
                if (File.Exists(fileName) && allFiles[downloadingFile].Size != new FileInfo(fileName).Length)
                    skippingFiles.Add(downloadingFile);
            }

            if (skippingFiles.Count == downloadingFiles.Count) return;

            _logger.Log(string.Format(Resources.ConfigRunner_DownloadData_Found__0__matching_data_files_on__1__, downloadingFiles.Count, server.GetUrl()));
            foreach (var file in downloadingFiles)
                _logger.Log(file);
            _logger.Log(Resources.ConfigRunner_DownloadData_Starting_download___);
            
            var ftpClient = server.GetFtpClient();
            var source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            _runningCancellationToken = source;
            var i = 0;
            int triesOnFile = 0;
            while ( i < downloadingFiles.Count && IsRunning())
            {
                var fileName = downloadingFiles[i];
                if (triesOnFile == 0)
                {
                    _logger.Log(string.Format(Resources.ConfigRunner_DownloadData__0____1__of__2__, fileName, i + 1, downloadingFiles.Count));
                }
                
                if (skippingFiles.Contains(fileName))
                {
                    _logger.Log(Resources.ConfigRunner_DownloadData_Already_downloaded__Skipping_);
                    triesOnFile = 0;
                    i++;
                    continue;
                }

                Progress<FtpProgress> progress = new Progress<FtpProgress>(p =>
                {
                    _logger.LogPercent((int)Math.Floor(p.Progress));
                });
                try
                {
                    await ftpClient.ConnectAsync(token);
                    await ftpClient.DownloadFileAsync(Path.Combine(mainSettings.DataFolderPath, fileName),
                        server.FilePath(fileName), token: token, existsMode: FtpLocalExists.Overwrite, progress: progress);
                    await ftpClient.DisconnectAsync(token);
                    triesOnFile = 0;
                    i++;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogPercent(-1); // Stop logging percent
                }
                catch (Exception e)
                {
                    _logger.LogPercent(-1);
                    _logger.Log(e.Message);
                    if (triesOnFile < 3)
                    {
                        _logger.Log(Resources.ConfigRunner_DownloadData_Trying_again___);
                        triesOnFile++;
                    }
                    else
                    {
                        _logger.LogException(e,
                            Resources.ConfigRunner_DownloadData_An_error_occurred_while_downloading_the_data_files_);
                        ChangeStatus(RunnerStatus.Error);
                    }
                }
            }
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
                    LogBatchFile();
            }
        }

        private void LogBatchFile()
        {
            using (var reader = new StreamReader(_batchFile))
            {
                while(!reader.EndOfStream)
                    _logger.Log(reader.ReadLine());
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
                if (IsRunning() && _runningCancellationToken != null)
                    _runningCancellationToken.Cancel();
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
