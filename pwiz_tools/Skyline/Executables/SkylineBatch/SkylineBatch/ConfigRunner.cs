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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Management;
using System.Threading.Tasks;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class ConfigRunner : IConfigRunner
    {
        private readonly IMainUiControl _uiControl;
        private readonly Logger _logger;

        private readonly object _lock = new object();
        private RunnerStatus _runnerStatus;


        public ConfigRunner(SkylineBatchConfig config, Logger logger, IMainUiControl uiControl = null)
        {
            _runnerStatus = RunnerStatus.Stopped;
            Config = config;
            _uiControl = uiControl;
            _logger = logger;
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
                // Writes the batch commands for steps 1-4 to a file
                var commandFile = WriteBatchCommandsToFile(startStep);
                var command = string.Format("--batch-commands=\"{0}\" --version", commandFile);
                await ExecuteProcess(Config.SkylineSettings.CmdPath, command);
            }

            // STEP 5: run r scripts using csv files
            var rScriptsRunInformation = Config.GetScriptArguments();
            foreach(var rScript in rScriptsRunInformation)
                await ExecuteProcess(rScript[RRunInfo.ExePath], rScript[RRunInfo.Arguments]);

            // Runner is still running if no errors or cancellations
            if (IsRunning()) ChangeStatus(RunnerStatus.Completed);
            if (IsCancelling()) ChangeStatus(RunnerStatus.Canceled);
            var endTime = DateTime.Now;
            var delta = endTime - startTime;
            var timeString = delta.Hours > 0 ? delta.ToString(@"hh\:mm\:ss") : string.Format("{0} minutes", delta.ToString(@"mm\:ss"));
            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, Config.Name, GetStatus()));
            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, "Runtime", timeString));
        }

        public string WriteBatchCommandsToFile(int startStep)
        {
            var newSkylineFileName = Config.MainSettings.GetResultsFilePath();
            // Writes commands to the log and a file for batch processing
            var commandWriter = new CommandWriter(_logger, Config.SkylineSettings, newSkylineFileName);

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
        
        public async Task ExecuteProcess(string exeFile, string arguments)
        {
            if (!IsRunning()) return;

            _logger?.Log(arguments);
            Process cmd = new Process();
            cmd.StartInfo.FileName = exeFile;
            cmd.StartInfo.Arguments = arguments;
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardError = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.EnableRaisingEvents = true;
            cmd.Exited += (sender, e) =>
            {
                if (IsRunning())
                    if (cmd.ExitCode != 0)
                        ChangeStatus(RunnerStatus.Error);
            };
            cmd.OutputDataReceived += DataReceived;
            cmd.ErrorDataReceived += DataReceived;
            cmd.Start();
            cmd.BeginOutputReadLine();
            cmd.BeginErrorReadLine();
            // Add process to tracker so the OS will dispose of it if SkylineBatch exits/crashes
            ChildProcessTracker.AddProcess(cmd);
            while (!cmd.HasExited && IsRunning())
            {
                await Task.Delay(2000);
            }

            // end cmd and SkylineRunner/SkylineCmd processes if runner has been stopped before completion
            if (!cmd.HasExited)
            {
                // make sure no process children left running
                await KillProcessChildren((UInt32)cmd.Id);
                if (!cmd.HasExited) cmd.Kill();
            }
        }

        private void DataReceived(object s, DataReceivedEventArgs e)
        {
            if (e.Data != null && _logger != null)
            {
                if (e.Data.StartsWith("Error") || e.Data.StartsWith("Fatal error"))
                    _logger.LogError(e.Data);
                else
                    _logger.Log(e.Data);
            }
        }

        private async Task KillProcessChildren(UInt32 parentProcessId)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT * " +
                "FROM Win32_Process " +
                "WHERE ParentProcessId=" + parentProcessId);

            ManagementObjectCollection collection = searcher.Get();
            if (collection.Count > 0)
            {
                ProgramLog.Info("Killing [" + collection.Count + "] processes spawned by process with Id [" + parentProcessId + "]");
                foreach (var item in collection)
                {
                    UInt32 childProcessId = (UInt32)item["ProcessId"];
                    if (childProcessId != Process.GetCurrentProcess().Id)
                    {
                        await KillProcessChildren(childProcessId);

                        try
                        {
                            var childProcess = Process.GetProcessById((int)childProcessId);
                            ProgramLog.Info("Killing child process [" + childProcess.ProcessName + "] with Id [" + childProcessId + "]");
                            childProcess.Kill();
                        }
                        catch (ArgumentException)
                        {
                            ProgramLog.Info("Child process already terminated");
                        }
                        catch (Win32Exception)
                        {
                            ProgramLog.Info("Cannot kill windows child process.");
                        }
                    }
                }
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
                ChangeStatus(RunnerStatus.Canceling);
            if (IsWaiting())
                ChangeStatus(RunnerStatus.Stopped);                                                                                            
        }

        public bool IsCancelling()
        {
            return _runnerStatus == RunnerStatus.Canceling;
        }
        
        public bool IsBusy()
        {
            return IsRunning() || IsWaiting() || IsCancelling();
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
