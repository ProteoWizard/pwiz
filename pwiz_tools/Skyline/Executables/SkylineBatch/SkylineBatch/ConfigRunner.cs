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
using System.Drawing;
using System.Threading.Tasks;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class ConfigRunner : IConfigRunner
    {
        public static readonly string ALLOW_NEWLINE_SAVE_VERSION = "20.2.1.415"; // TODO(Ali): Make sure this matches future Skyline-daily release with --save fix


        private readonly IMainUiControl _uiControl;
        private readonly Logger _logger;

        private readonly object _lock = new object();
        private RunnerStatus _runnerStatus;
        private readonly ProcessRunner _processRunner;

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

            ChangeStatus(RunnerStatus.Running);
            var startTime = DateTime.Now;
            Config.MainSettings.CreateAnalysisFolderIfNonexistent();

            if (startStep != 5)
            {
                var multiLine = await Config.SkylineSettings.HigherVersion(ALLOW_NEWLINE_SAVE_VERSION, _processRunner);
                if (IsRunning())
                {
                    // Writes the batch commands for steps 1-4 to a file
                    var commandFile = WriteBatchCommandsToFile(startStep, multiLine);
                    var command = string.Format("--batch-commands=\"{0}\"", commandFile);
                    await _processRunner.Run(Config.SkylineSettings.CmdPath, command);
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
            var delta = endTime - startTime;
            var timeString = delta.Hours > 0 ? delta.ToString(@"hh\:mm\:ss") : string.Format("{0} minutes", delta.ToString(@"mm\:ss"));
            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, Config.Name, GetStatus()));
            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, "Runtime", timeString));
        }

        public string WriteBatchCommandsToFile(int startStep, bool multiLine)
        {
            // Writes commands to the log and a file for batch processing
            var commandWriter = new CommandWriter(_logger, multiLine);

            // STEP 1: open skyline file and save copy to analysis folder
            if (startStep == 1)
            {
                Config.WriteOpenSkylineTemplateCommand(commandWriter);
                Config.WriteMsOneCommand(commandWriter);
                Config.WriteMsMsCommand(commandWriter);
                Config.WriteRetentionTimeCommand(commandWriter);
                Config.WriteAddDecoysCommand(commandWriter);
                Config.WriteSaveToResultsFile(commandWriter);
                Config.WriteSaveSettingsCommand(commandWriter);
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
            {
                Config.WriteRefineCommands(commandWriter);
                commandWriter.EndCommandGroup();
            }

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

            return commandWriter.ReturnCommandFile();
        }

        private void DataReceived(string data)
        {
            if (IsRunning() && data != null && _logger != null)
            {
                if (data.StartsWith("Error") || data.StartsWith("Fatal error"))
                    _logger.LogError(data);
                else
                    _logger.Log(data);
            }
        }

        public void ChangeStatus(RunnerStatus runnerStatus)
        {
            lock (_lock)
            {
                if (_runnerStatus == runnerStatus)
                {
                    return;
                }
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
