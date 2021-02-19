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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
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

            var skylineRunner = Config.SkylineSettings.CmdPath;
            var templateFullName = Config.MainSettings.TemplateFilePath;
            var msOneResolvingPower = Config.FileSettings.MsOneResolvingPower;
            var msMsResolvingPower = Config.FileSettings.MsMsResolvingPower;
            var retentionTime = Config.FileSettings.RetentionTime;
            var addDecoys = Config.FileSettings.AddDecoys;
            var shuffleDecoys = Config.FileSettings.ShuffleDecoys;
            var trainMProfit = Config.FileSettings.TrainMProphet;
            var refine = false;
            var newSkylineFileName = Config.MainSettings.GetResultsFilePath();
            var dataDir = Config.MainSettings.DataFolderPath;
            var namingPattern = Config.MainSettings.ReplicateNamingPattern;

            Config.MainSettings.CreateAnalysisFolderIfNonexistent();
            // Writes commands to the log and a file for batch processing
            var commandWriter = new CommandWriter(_logger, Config.SkylineSettings, newSkylineFileName);
            commandWriter.Start();

            // STEP 1: open skyline file and save copy to analysis folder
            if (startStep == 1)
            {
                commandWriter.Write("--in=\"{0}\"", templateFullName);
                if (!string.IsNullOrEmpty(msOneResolvingPower)) commandWriter.Write("--full-scan-precursor-res={0}", msOneResolvingPower);
                if (!string.IsNullOrEmpty(msMsResolvingPower)) commandWriter.Write("--full-scan-product-res={0}", msMsResolvingPower);
                if (!string.IsNullOrEmpty(retentionTime)) commandWriter.Write("--full-scan-rt-filter-tolerance={0}", retentionTime);
                if (addDecoys) commandWriter.Write("--decoys-add={0} ", shuffleDecoys ? "shuffle" : "reverse");
                commandWriter.Write("--out=\"{0}\" ‑‑save‑settings ", newSkylineFileName);
                commandWriter.EndCommandGroup();
            }
            else
            {
                commandWriter.Write("--in=\"{0}\"", newSkylineFileName);
            }
            
            // STEP 2: import data to new skyline file
            if (startStep <= 2)
            {
                commandWriter.Write("--import-all=\"{0}\"", dataDir);
                if (!string.IsNullOrEmpty(namingPattern)) commandWriter.Write("--import-naming-pattern=\"{0}\"", namingPattern);
                if (trainMProfit) commandWriter.Write("--reintegrate-model-name=\"{0}\" --reintegrate-create-model --reintegrate-overwrite-peaks", Config.Name);
                commandWriter.Write("--save");
                if (refine)
                {
                    var refinedFile = Path.Combine(Config.MainSettings.AnalysisFolderPath,
                        Path.GetFileNameWithoutExtension(newSkylineFileName) + "_refined.sky");
                    //writer.WriteLine();
                    commandWriter.Write("--out=\"{0}\" --refine-cv-remove-above-cutoff=20 --refine-cv-global-normalize=equalize_medians --refine-qvalue-cutoff=0.01 --refine-minimum-detections=4", refinedFile);
                }
                commandWriter.EndCommandGroup();
            }
            
            // STEP 3: ouput report(s) for completed analysis
            if (startStep <= 3)
            {
                foreach (var report in Config.ReportSettings.Reports)
                {
                    var newReportPath = Config.MainSettings.AnalysisFolderPath + "\\" + report.Name + ".csv";
                    commandWriter.Write("--report-add=\"{0}\" --report-conflict-resolution=overwrite ", report.ReportPath);
                    commandWriter.Write("--report-name=\"{0}\" --report-file=\"{1}\" --report-invariant ", new []{report.Name, newReportPath});
                    commandWriter.EndCommandGroup();
                }
            }

            var commandFile = commandWriter.ReturnCommandFile();
            var command = string.Format("--version --batch-commands=\"{0}\"", commandFile);
            if (startStep != 4) await ExecuteProcess(skylineRunner, command);

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
