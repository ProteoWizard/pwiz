/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class ConfigRunner
    {
        private BackgroundWorker _worker;

        private int _totalImportCount;

        //private SkylineBatchFileSystemWatcher _fileWatcher;

        private readonly IMainUiControl _uiControl;
        private ISkylineBatchLogger _logger;

        //private AsyncOperation asyncRunnerStatusChanged;
        

        public SkylineBatchConfig Config { get; private set; }


        private readonly object _lock = new object();
        private RunnerStatus _runnerStatus;

        public enum RunnerStatus
        {
            Waiting,
            Running,
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

            //asyncRunnerStatusChanged = AsyncOperationManager.CreateOperation(null);
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
            //EnableUiLogging();
            LogToUi(string.Format(Resources.Start_running_config_log_message, Config.Name));
            try
            {
                Config.Validate();

            } catch (ArgumentException e)
            {
                LogToUi("Error: " + e.Message);
                ChangeStatus(RunnerStatus.Error);
                LogToUi(string.Format(Resources.Terminated_running_config_log_message, Config.Name, GetStatus()));
                return;
            }
            //_logger.UpdateConfig(Config.Name);
           // _uiControl.UpdateUiConfigurations();

            var commands = new List<string>();

            var skylineRunner = SkylineSettings.GetSkylineCmdLineExePath;
            var templateFullName = Config.MainSettings.TemplateFilePath;
            var newSkylineFileName = Config.MainSettings.GetNewTemplatePath();
            var dataDir = Config.MainSettings.DataFolderPath;
            var namingPattern = Config.MainSettings.ReplicateNamingPattern;


            var rLocation = SkylineSettings.GetRscriptExeLocation;
            //var batchFile = Directory.GetCurrentDirectory() + "\\SkylineBatch.bat";



            // STEP 1: open skyline file and save copy to analysis folder
            var firstStep = string.Format("\"{0}\" --in=\"{1}\" --out=\"{2}\" ‑‑save‑settings", skylineRunner,
                templateFullName, newSkylineFileName);
            
            if (startStep <= 1)
                commands.Add(firstStep);

            // STEP 2: import data to new skyline file

            var secondStep = string.Format("\"{0}\" --in=\"{1}\" --import-all=\"{2}\" ", skylineRunner, newSkylineFileName, dataDir);
            secondStep += string.IsNullOrEmpty(namingPattern) ? "" : string.Format("--import-naming-pattern=\"{0}\" ", namingPattern);
            secondStep += string.Format("--reintegrate-model-name=\"{0}\" ", Config.Name);
            secondStep += " --reintegrate-create-model --reintegrate-overwrite-peaks --save";
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
                foreach (var script in report.rScripts)
                {
                    var workingDirectory = Config.MainSettings.AnalysisFolderPath;
                    scriptCommands.Add(string.Format("\"{0}\" \"{1}\" \"{2}\" 2>&1", rLocation, script, workingDirectory));
                }

            }
            if (startStep <= 3)
                commands.Add(thirdStep);
            commands.AddRange(scriptCommands); // step 4

            if (commands.Count > 1)
                commands[0] += " --version";
            await ExecuteCommandLine(commands);

            LogToUi(string.Format(Resources.Terminated_running_config_log_message, Config.Name, GetStatus()));
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
            cmd.Exited += (object sender, EventArgs e) =>
            {
                if (IsRunning())
                    ChangeStatus(RunnerStatus.Completed);
            };
            cmd.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    if (e.Data.StartsWith("Fatal error: ") || e.Data.StartsWith("Error: "))
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

            // end cmd and skylinerunner processes if runner has been stopped before completion
            if (!cmd.HasExited)
            {
                LogToUi(Resources.Process_terminated);
                var skylineRunnerName = Path.GetFileNameWithoutExtension(SkylineSettings.GetSkylineCmdLineExePath);
                Process[] processes = Process.GetProcessesByName(skylineRunnerName);
                foreach (var process in processes)
                {
                    process.Kill();
                }

                cmd.Kill();
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
            if (_uiControl != null)
                _uiControl.UpdateUiConfigurations();
        }

        public void Cancel()
        {
            if (IsRunning()) 
                ChangeStatus(RunnerStatus.Cancelled);
            else if (IsWaiting())
                ChangeStatus(RunnerStatus.Stopped);                                                                                            
        }
        

       
        public bool IsBusy()
        {
            return IsRunning() || IsWaiting();
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

        public bool IsWaiting()
        {
            return _runnerStatus == RunnerStatus.Waiting;
        }

        
        
    }
}
