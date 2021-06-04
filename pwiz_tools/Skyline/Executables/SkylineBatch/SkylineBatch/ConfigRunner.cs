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
        public static readonly string REPORT_INVARIANT_VERSION = "21.1.0.146";


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
                OnException = (e, message) => _logger?.LogError(message, e.ToString()),
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

        public async Task Run(RunBatchOptions runOption, ServerConnector serverConnector)
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

            if ((runOption == RunBatchOptions.ALL || runOption == RunBatchOptions.DOWNLOAD_DATA) 
                && Config.MainSettings.WillDownloadData)
            {
                await DownloadData(serverConnector);
            }
            
            if ((runOption == RunBatchOptions.ALL ||
                 runOption == RunBatchOptions.FROM_TEMPLATE_COPY ||
                 runOption == RunBatchOptions.FROM_REFINE ||
                 runOption == RunBatchOptions.FROM_REPORT_EXPORT)
                && IsRunning())
            {
                var multiLine = await Config.SkylineSettings.HigherVersion(ALLOW_NEWLINE_SAVE_VERSION, _processRunner);
                var numberFormat = CultureInfo.CurrentCulture.GetFormat(typeof(NumberFormatInfo)) as NumberFormatInfo;
                var internationalSeparator = numberFormat != null && Equals(TextUtil.SEPARATOR_CSV.ToString(CultureInfo.InvariantCulture), numberFormat.NumberDecimalSeparator);
                var invariantReport = !internationalSeparator || await Config.SkylineSettings.HigherVersion(REPORT_INVARIANT_VERSION);
                if (IsRunning())
                {
                    // Writes the batch commands for steps 1-4 to a file
                    var commandWriter = new CommandWriter(_logger, multiLine, invariantReport);
                    WriteBatchCommandsToFile(commandWriter, runOption, invariantReport);
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

            // STEP 4: run r scripts using csv files
            if (runOption != RunBatchOptions.DOWNLOAD_DATA)
            {
                var rScriptsRunInformation = Config.GetScriptArguments();
                foreach (var rScript in rScriptsRunInformation)
                    if (IsRunning())
                        await _processRunner.Run(rScript[RRunInfo.ExePath], rScript[RRunInfo.Arguments]);
            }

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

        public void WriteBatchCommandsToFile(CommandWriter commandWriter, RunBatchOptions runOption, bool invariantReport)
        {
            // STEP 1: create results document and import data
            if (runOption <= RunBatchOptions.FROM_TEMPLATE_COPY)
            {
                // Delete existing .sky and .skyd results files
                var filesToDelete = FileUtil.GetFilesInFolder(Config.MainSettings.AnalysisFolderPath, TextUtil.EXT_SKY);
                filesToDelete.AddRange(FileUtil.GetFilesInFolder(Config.MainSettings.AnalysisFolderPath,
                    TextUtil.EXT_SKYD));
                foreach (var file in filesToDelete) 
                    File.Delete(file);

                Config.WriteOpenSkylineTemplateCommand(commandWriter);
                Config.WriteMsOneCommand(commandWriter);
                Config.WriteMsMsCommand(commandWriter);
                Config.WriteRetentionTimeCommand(commandWriter);
                Config.WriteAddDecoysCommand(commandWriter);
                Config.WriteSaveToResultsFile(commandWriter);
                commandWriter.EndCommandGroup();
                // import data
                Config.WriteImportDataCommand(commandWriter);
                Config.WriteImportNamingPatternCommand(commandWriter);
                Config.WriteTrainMProphetCommand(commandWriter);
                Config.WriteImportAnnotationsCommand(commandWriter);
                Config.WriteSaveCommand(commandWriter);
                commandWriter.EndCommandGroup();
            }
            else if (runOption < RunBatchOptions.FROM_REPORT_EXPORT)
            {
                Config.WriteOpenSkylineResultsCommand(commandWriter);
            }

            // STEP 2: refine file and save to new location
            if (runOption <= RunBatchOptions.FROM_REFINE)
                Config.WriteRefineCommands(commandWriter);

            // STEP 3: output report(s) for completed analysis
            if (runOption <= RunBatchOptions.FROM_REPORT_EXPORT)
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

        private async Task DownloadData(ServerConnector serverConnector)
        {
            var mainSettings = Config.MainSettings;
            var server = mainSettings.Server;
            Directory.CreateDirectory(mainSettings.DataFolderPath);

            var matchingFiles = server.GetDataFiles(serverConnector);
            var downloadingFiles = server.FilesToDownload(mainSettings.DataFolderPath, serverConnector);

            if (downloadingFiles.Count == 0) return;

            _logger.Log(string.Format(Resources.ConfigRunner_DownloadData_Found__0__matching_data_files_on__1__, matchingFiles.Count, server.GetUrl()));
            foreach (var file in matchingFiles.Keys)
                _logger.Log(file);
            _logger.Log(Resources.ConfigRunner_DownloadData_Starting_download___);

            var dataDriveName = mainSettings.DataFolderPath.Substring(0, 3);
            var ftpClient = server.GetFtpClient();
            var source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            _runningCancellationToken = source;
            var currentFileNumber = 0;
            foreach (var fileName in matchingFiles.Keys)
            {
                currentFileNumber++;
                _logger.Log(string.Format(Resources.ConfigRunner_DownloadData__0____1__of__2__, fileName, currentFileNumber, matchingFiles.Count));
                // 3 tries to download file
                int i;
                Exception exception = null;
                for (i = 0; i < 3; i++)
                {
                    if (!IsRunning()) return;
                    var filePath = Path.Combine(mainSettings.DataFolderPath, fileName);
                    if (!downloadingFiles.ContainsKey(filePath))
                    {
                        _logger.Log(Resources.ConfigRunner_DownloadData_Already_downloaded__Skipping_);
                        break;
                    }
                    if (downloadingFiles[filePath].Size + FileUtil.ONE_GB > FileUtil.GetTotalFreeSpace(dataDriveName))
                    {
                        _logger.LogError(string.Format(Resources.ConfigRunner_DownloadData_There_is_not_enough_remaining_disk_space_to_download__0___Free_up_some_disk_space_and_try_again_, fileName));
                        ChangeStatus(RunnerStatus.Error);
                        return;
                    }

                    if (i > 0)
                        _logger.Log(Resources.ConfigRunner_DownloadData_Trying_again___);

                    Progress<FtpProgress> progress = new Progress<FtpProgress>(p =>
                    {
                        _logger.LogPercent((int)Math.Floor(p.Progress));
                    });
                    try
                    {
                        await ftpClient.ConnectAsync(token);
                        var status = await ftpClient.DownloadFileAsync(filePath,
                            server.FilePath(fileName), token: token, existsMode: FtpLocalExists.Overwrite, progress: progress);
                        await ftpClient.DisconnectAsync(token);
                        if (status != FtpStatus.Success)
                            throw new Exception(Resources.ConfigRunner_DownloadData_File_download_failed_);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogPercent(-1); // Stop logging percent
                    }
                    catch (Exception e)
                    {
                        _logger.LogPercent(-1);
                        _logger.Log(e.Message);
                        exception = e;
                    }
                }

                if (i == 3)
                {
                    _logger.LogError(
                        Resources.ConfigRunner_DownloadData_An_error_occurred_while_downloading_the_data_files_, exception.Message);
                    ChangeStatus(RunnerStatus.Error);
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
