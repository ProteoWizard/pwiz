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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
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
            return status == RunnerStatus.Stopped ? "" : status.ToString();
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

            var commands = new List<string>();

            var skylineRunner = Config.SkylineSettings.CmdPath;
            var templateFullName = Config.MainSettings.TemplateFilePath;
            var msOneResolvingPower = Config.FileSettings.MsOneResolvingPower;
            var msMsResolvingPower = Config.FileSettings.MsMsResolvingPower;
            var retentionTime = Config.FileSettings.RetentionTime;
            var addDecoys = Config.FileSettings.AddDecoys;
            var shuffleDecoys = Config.FileSettings.ShuffleDecoys;
            var trainMProfit = Config.FileSettings.TrainMProfit;
            var newSkylineFileName = Config.MainSettings.GetNewTemplatePath();
            var dataDir = Config.MainSettings.DataFolderPath;
            var namingPattern = Config.MainSettings.ReplicateNamingPattern;

            if (!Directory.Exists(Config.MainSettings.AnalysisFolderPath))
                Directory.CreateDirectory(Config.MainSettings.AnalysisFolderPath);

            // STEP 1: open skyline file and save copy to analysis folder
            var firstStep = string.Format("\"{0}\" --in=\"{1}\" ", skylineRunner, templateFullName);

            firstStep += !string.IsNullOrEmpty(msOneResolvingPower) ? string.Format("--full-scan-precursor-res={0} ", msOneResolvingPower) : string.Empty;
            firstStep += !string.IsNullOrEmpty(msMsResolvingPower) ? string.Format("--full-scan-product-res={0} ", msMsResolvingPower) : string.Empty;
            firstStep += !string.IsNullOrEmpty(retentionTime) ? string.Format("--full-scan-rt-filter-tolerance={0} ", retentionTime) : string.Empty;
            firstStep += addDecoys ? string.Format("--decoys-add={0} ", shuffleDecoys ? "shuffle" : "reverse") : string.Empty;
            firstStep += string.Format("--out=\"{0}\" ‑‑save‑settings", newSkylineFileName);

            if (startStep <= 1)
                commands.Add(firstStep);

            // STEP 2: import data to new skyline file

            var secondStep = string.Format("\"{0}\" --in=\"{1}\" --import-all=\"{2}\" ", skylineRunner, newSkylineFileName, dataDir);
            secondStep += !string.IsNullOrEmpty(namingPattern) ? string.Format("--import-naming-pattern=\"{0}\" ", namingPattern) : string.Empty;
            secondStep += trainMProfit ? string.Format("--reintegrate-model-name=\"{0}\" --reintegrate-create-model --reintegrate-overwrite-peaks ", Config.Name) : string.Empty;
            secondStep += "--save";
            if (startStep <= 2)
                commands.Add(secondStep);

            // STEPS 3 & 4: ouput report(s) for completed analysis, run r scripts using csv files
            var thirdStep = string.Format("\"{0}\" --in=\"{1}\" ", skylineRunner, newSkylineFileName);
            var scriptCommands = new List<string>();
            foreach (var report in Config.ReportSettings.Reports)
            {

                var newReportPath = Config.MainSettings.AnalysisFolderPath + "\\" + report.Name + ".csv";
                thirdStep += string.Format("--report-add=\"{0}\" --report-conflict-resolution=overwrite ", report.ReportPath);
                thirdStep += string.Format("--report-name=\"{0}\" --report-file=\"{1}\" --report-invariant ", report.Name, newReportPath);
                foreach(var scriptAndVersion in report.RScripts)
                {
                    var rVersionExe = Settings.Default.RVersions[scriptAndVersion.Item2];
                    scriptCommands.Add(string.Format("\"{0}\" \"{1}\" \"{2}\" 2>&1", rVersionExe, scriptAndVersion.Item1, newReportPath));
                }

            }
            if (startStep <= 3)
                commands.Add(thirdStep);
            commands.AddRange(scriptCommands); // step 4

            if (commands.Count > 1)
                commands[0] += " --version";
            await ExecuteCommandLine(commands);

            LogToUi(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, Config.Name, GetStatus()));
        }

        public async Task ExecuteCommandLine(List<string> commands)
        {
            ChangeStatus(RunnerStatus.Running);

            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.EnableRaisingEvents = true;
            cmd.Exited += (sender, e) =>
            {
                if (IsRunning())
                    ChangeStatus(RunnerStatus.Completed);
            };
            cmd.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    if (e.Data.Contains("Fatal error: ") || e.Data.Contains("Error: "))
                        ChangeStatus(RunnerStatus.Error);

                    if (_logger != null)
                        _logger.Log(e.Data);
                }
            };
            cmd.Start();
            cmd.BeginOutputReadLine();
            foreach (string command in commands)
            {
                cmd.StandardInput.WriteLine(command);
                cmd.StandardInput.Flush();
            }
            cmd.StandardInput.Close();
            while (IsRunning())
            {
                await Task.Delay(2000);
            }

            // end cmd and SkylineRunner/SkylineCmd processes if runner has been stopped before completion
            if (!cmd.HasExited)
            {
                LogToUi(Resources.ConfigRunner_ExecuteCommandLine_Process_terminated_);
                await KillProcessChildren((UInt32)cmd.Id);
                if (!cmd.HasExited) cmd.Kill();
                if (!IsError())
                    ChangeStatus(RunnerStatus.Cancelled);
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
                if (collection.Count == 1)
                {
                    Program.LogInfo(string.Format(Resources.ConfigRunner_KillProcessChildren_Killing_a_processes_spawned_by_the_process_with_Id___0_, parentProcessId));
                }
                else
                {
                    Program.LogInfo(string.Format(Resources.ConfigRunner_KillProcessChildren_Killing__0__processes_spawned_by_the_process_with_Id___1_, collection.Count, parentProcessId));
                }

                foreach (var item in collection)
                {
                    UInt32 childProcessId = (UInt32)item["ProcessId"];
                    if (childProcessId != Process.GetCurrentProcess().Id)
                    {
                        await KillProcessChildren(childProcessId);

                        try
                        {
                            var childProcess = Process.GetProcessById((int)childProcessId);
                            Program.LogInfo(string.Format(Resources.ConfigRunner_KillProcessChildren_Killing_child_process___0___with_Id___1_, childProcess.ProcessName, childProcessId));
                            childProcess.Kill();
                        }
                        catch (ArgumentException)
                        {
                            Program.LogInfo(Resources.ConfigRunner_KillProcessChildren_The_child_process_has_already_been_terminated_);
                        }
                        catch (Win32Exception)
                        {
                            Program.LogInfo(Resources.ConfigRunner_KillProcessChildren_Cannot_kill_child_process_);
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
