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
using System.Diagnostics;

namespace AutoQC
{
    public class ProcessInfo
    {
        public string Executable { get; private set; }
        public string ExeName { get; private set; }
        public string Args { get; private set; }
        public string ArgsToPrint { get; private set; }
        public string WorkingDirectory { get; set; }
        
        public ProcessInfo(string exe, string args)
        {
            Executable = exe;
            ExeName = Executable;
            Args = args;
            ArgsToPrint = args;
        }

        public ProcessInfo(string exe, string exeName, string args, string argsToPrint) : this (exe, args)
        {
            ExeName = exeName;
            ArgsToPrint = argsToPrint;
        }
    }

    public class ProcessRunner
    {
        private ProcessInfo _procInfo;
        private readonly IAutoQcLogger _logger;

        private Process _process;

        private bool _documentImportFailed;
        private bool _panoramaUploadFailed;
        private bool _errorLogged;

        public ProcessRunner(IAutoQcLogger logger)
        {
            _logger = logger;
        }

        private static Process CreateProcess(ProcessInfo procInfo)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = procInfo.Executable,
                    Arguments = procInfo.Args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = procInfo.WorkingDirectory
                },
                EnableRaisingEvents = true
            };
            return process;
        }

        public ProcStatus RunProcess(ProcessInfo processInfo)
        {
            _procInfo = processInfo;

            Log("Running {0} with args: ", _procInfo.ExeName);
            Log(_procInfo.ArgsToPrint);

            while (true)
            {
                int exitCode;
                try
                {
                    exitCode = CreateAndRunProcess();
                }
                catch (Exception e)
                {
                    LogException(e, "There was an exception running the process {0}", _procInfo.ExeName);
                    return ProcStatus.Error;
                }

                if (exitCode != 0)
                {
                    LogError("{0} exited with error code {1}.", _procInfo.ExeName, exitCode);
                    return ProcStatus.Error;
                }

                if (_errorLogged)
                {
                    LogError("{0} exited with code {1}. Error reported.", _procInfo.ExeName, exitCode);
                    return ProcStatus.Error;
                }
                if (_documentImportFailed)
                {
                    LogError("{0} exited with code {1}. Skyline document import failed.", _procInfo.ExeName, exitCode);
                    return ProcStatus.DocImportError;
                }
                if (_panoramaUploadFailed)
                {
                    LogError("{0} exited with code {1}. Panorama upload failed.", _procInfo.ExeName, exitCode);
                    return ProcStatus.PanoramaUploadError;
                }

                Log("{0} exited successfully.", _procInfo.ExeName);
                return ProcStatus.Success;
            }
        }

        protected virtual int CreateAndRunProcess()
        {
            _process = CreateProcess(_procInfo);
            _process.OutputDataReceived += WriteToLog;
            _process.ErrorDataReceived += WriteToLog;
            _process.Start();
            _process.BeginOutputReadLine();
            _process.StandardError.ReadToEnd();
            _process.WaitForExit();
            return _process.ExitCode;
        }

        // Handle a line of output/error data from the process.
        private void WriteToLog(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            WriteToLog(e.Data);
        }

        protected void WriteToLog(string message)
        {
            if (DetectError(message))
            {
                LogError(message);
            }
            else
            {
                 Log(message);
            }  
        }

        private bool DetectError(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            if (message.Contains("Failed importing"))
            {
                // TODO: fix in Skyline? These do not start with "Error"
                _documentImportFailed = true;
                return true;
            }
            if(message.StartsWith("Warning: Cannot read file") && message.EndsWith("Ignoring..."))
            {
                // This is the message for un-readable RAW files from Thermo instruments.
                _documentImportFailed = true;
                return true;
            }

            if (!message.StartsWith("Error")) return false;
            
            if (message.Contains("PanoramaImportErrorException"))
            {
                _panoramaUploadFailed = true;
            }
            else
            {
                _errorLogged = true;
            }
            return true;
        }

        private void Log(string message, params object[] args)
        {
            _logger.Log(message, args);
        }

        private void LogError(string message, params object[] args)
        {
            _logger.LogError(message, args);
        }

        private void LogException(Exception e, string message, params object[] args)
        {
            _logger.LogException(e, message, args);
        }

        protected ProcessInfo GetProcessInfo()
        {
            return _procInfo;
        }

        public void StopProcess()
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _process.Kill();
                }
                catch (Exception e)
                {
                    LogException(e, "Error killing process {0}", _procInfo.ExeName);
                }
            }
        }
    }
}