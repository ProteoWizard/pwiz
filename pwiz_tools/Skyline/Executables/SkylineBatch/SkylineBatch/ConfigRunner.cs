﻿/*
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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class ConfigRunner
    {

        private readonly IMainUiControl _uiControl;
        private readonly ISkylineBatchLogger _logger;

        private readonly object _lock = new object();
        private RunnerStatus _runnerStatus;

        public enum RunnerStatus
        {
            Waiting,
            Running,
            Cancelling,
            Cancelled,
            Stopped,
            Completed,
            Error
        }

        public ConfigRunner(SkylineBatchConfig config, ISkylineBatchLogger logger, IMainUiControl uiControl = null)
        {
            _runnerStatus = RunnerStatus.Stopped;
            Config = config;
            _uiControl = uiControl;
            _logger = logger;
        }
        
        public SkylineBatchConfig Config { get; }

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

            var skylineRunner = Config.SkylineSettings.CmdPath;
            var templateFullName = Config.MainSettings.TemplateFilePath;
            var msOneResolvingPower = Config.FileSettings.MsOneResolvingPower;
            var msMsResolvingPower = Config.FileSettings.MsMsResolvingPower;
            var retentionTime = Config.FileSettings.RetentionTime;
            var addDecoys = Config.FileSettings.AddDecoys;
            var shuffleDecoys = Config.FileSettings.ShuffleDecoys;
            var trainMProfit = Config.FileSettings.TrainMProphet;
            var newSkylineFileName = Config.MainSettings.GetResultsFilePath();
            var dataDir = Config.MainSettings.DataFolderPath;
            var namingPattern = Config.MainSettings.ReplicateNamingPattern;

            Config.MainSettings.CreateAnalysisFolderIfNonexistent();

            // STEP 1: open skyline file and save copy to analysis folder
            var firstStep = string.Format("--in=\"{0}\" ", templateFullName);
            firstStep += !string.IsNullOrEmpty(msOneResolvingPower) ? string.Format("--full-scan-precursor-res={0} ", msOneResolvingPower) : string.Empty;
            firstStep += !string.IsNullOrEmpty(msMsResolvingPower) ? string.Format("--full-scan-product-res={0} ", msMsResolvingPower) : string.Empty;
            firstStep += !string.IsNullOrEmpty(retentionTime) ? string.Format("--full-scan-rt-filter-tolerance={0} ", retentionTime) : string.Empty;
            firstStep += addDecoys ? string.Format("--decoys-add={0} ", shuffleDecoys ? "shuffle" : "reverse") : string.Empty;
            firstStep += string.Format("--out=\"{0}\" ‑‑save‑settings", newSkylineFileName);
            firstStep += startStep == 1 ? " --version" : string.Empty;
            if (startStep <= 1)
                await ExecuteProcess(skylineRunner, firstStep);

            // STEP 2: import data to new skyline file
            var secondStep = string.Format("--in=\"{0}\" --import-all=\"{1}\" ", newSkylineFileName, dataDir);
            secondStep += !string.IsNullOrEmpty(namingPattern) ? string.Format("--import-naming-pattern=\"{0}\" ", namingPattern) : string.Empty;
            secondStep += trainMProfit ? string.Format("--reintegrate-model-name=\"{0}\" --reintegrate-create-model --reintegrate-overwrite-peaks ", Config.Name) : string.Empty;
            secondStep += "--save";
            secondStep += startStep == 2 ? " --version" : string.Empty;
            if (startStep <= 2)
                await ExecuteProcess(skylineRunner, secondStep);
            
            // STEP 3: ouput report(s) for completed analysis
            foreach (var report in Config.ReportSettings.Reports)
            {
                var newReportPath = Config.MainSettings.AnalysisFolderPath + "\\" + report.Name + ".csv";
                var reportArguments = string.Format("--in=\"{0}\" ", newSkylineFileName);
                reportArguments += string.Format("--report-add=\"{0}\" --report-conflict-resolution=overwrite ", report.ReportPath);
                reportArguments += string.Format("--report-name=\"{0}\" --report-file=\"{1}\" --report-invariant ", report.Name, newReportPath);
                reportArguments += startStep == 3 && Config.ReportSettings.Reports.IndexOf(report) == 0 ? " --version" : string.Empty;
                if (startStep <= 3)
                    await ExecuteProcess(skylineRunner, reportArguments);
            }
            
            // STEP 4: run r scripts using csv files
            foreach (var report in Config.ReportSettings.Reports)
            {
                var newReportPath = Config.MainSettings.AnalysisFolderPath + "\\" + report.Name + ".csv";
                foreach (var scriptAndVersion in report.RScripts)
                {
                    var rVersionExe = Settings.Default.RVersions[scriptAndVersion.Item2];
                    var scriptArguments = string.Format("\"{0}\" \"{1}\" 2>&1", scriptAndVersion.Item1, newReportPath);
                    await ExecuteProcess(rVersionExe, scriptArguments);
                }
            }

            // Runner is still running if no errors or cancellations
            if (IsRunning()) ChangeStatus(RunnerStatus.Completed);
            var endTime = DateTime.Now;
            var delta = endTime - startTime;
            var timeString = delta.Hours > 0 ? delta.ToString(@"hh\:mm\:ss") : string.Format("{0} minutes", delta.ToString(@"mm\:ss"));
            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, Config.Name, GetStatus()));
            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, "Runtime", timeString));
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
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.EnableRaisingEvents = true;
            cmd.Exited += (sender, e) =>
            {
                if (IsRunning())
                    if (cmd.ExitCode != 0)
                        ChangeStatus(RunnerStatus.Error);
            };
            cmd.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null && _logger != null)
                {
                    _logger.Log(e.Data);
                }
            };
            cmd.Start();
            cmd.BeginOutputReadLine();
            while (!cmd.HasExited && IsRunning())
            {
                await Task.Delay(2000);
            }

            // end cmd and SkylineRunner/SkylineCmd processes if runner has been stopped before completion
            if (!cmd.HasExited)
            {
                LogToUi(Resources.ConfigRunner_ExecuteCommandLine_Process_terminated_);
                if (!cmd.HasExited) cmd.Kill();
                if (!IsError())
                    ChangeStatus(RunnerStatus.Cancelled);
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
                ChangeStatus(RunnerStatus.Cancelling);
            if (IsWaiting())
                ChangeStatus(RunnerStatus.Stopped);                                                                                            
        }

        public bool IsCancelling()
        {
            return _runnerStatus == RunnerStatus.Cancelling;
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
