/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.IO;
using System.Text.RegularExpressions;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    public class ProcessInfo
    {
        public string Executable { get; }
        public string ExeName { get; }
        public string Args { get; }
        public string ArgsToPrint { get; }
        public string WorkingDirectory { get; set; }

        public ProcessInfo(string exePath, string args, string argsToPrint)
        {
            Executable = exePath;
            ExeName = Path.GetFileName(exePath);
            Args = args;
            ArgsToPrint = argsToPrint;
        }
    }

    public class ProcessRunner
    {
        // The exit code of a .NET launcher whose runtime files are missing
        private const int DOTNET_LIBRARY_MISSING = unchecked((int)0x80008083);

        private ProcessInfo _procInfo;
        private readonly Logger _logger;

        private Process _process;

        private bool _documentImportFailed;
        private bool _errorLogged;
        private bool _fileImportIgnored;
        private bool _fatalPanoramaError;
        private bool _skylineNotFound;

        public ProcessRunner(Logger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// True if the last process failed because the ClickOnce Skyline it starts is not installed, or because the
        /// SkylineCmd.exe it ran is left over from an uninstall and cannot start.
        /// </summary>
        public bool SkylineNotFound => _skylineNotFound;

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

            Log(string.Format(Resources.ProcessRunner_RunProcess_Running__0__with_args__, _procInfo.ExeName));
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
                    LogException(e, string.Format(Resources.ProcessRunner_RunProcess_There_was_an_exception_running_the_process__0_, _procInfo.ExeName));
                    return ProcStatus.Error;
                }

                // An uninstaller cannot delete a running SkylineCmd.exe or the files it has loaded, but removes the
                // .NET runtime files next to them. Every later run of that SkylineCmd.exe exits with this code.
                if (exitCode == DOTNET_LIBRARY_MISSING)
                    _skylineNotFound = true;

                if (_fileImportIgnored)
                {
                    return ProcStatus.Skipped;
                }

                if (_documentImportFailed)
                {
                    LogError(string.Format(Resources.ProcessRunner_RunProcess__0__exited_with_code__1___Skyline_document_import_failed_, _procInfo.ExeName, exitCode));
                    return ProcStatus.Error;
                }

                if (_fatalPanoramaError)
                {
                    return ProcStatus.FatalPanoramaError;
                }

                if (exitCode != 0)
                {
                    LogError(string.Format(Resources.ProcessRunner_RunProcess__0__exited_with_error_code__1__, _procInfo.ExeName, exitCode));
                    return ProcStatus.Error;
                }

                if (_errorLogged)
                {
                    LogError(string.Format(Resources.ProcessRunner_RunProcess__0__exited_with_code__1___Error_reported_, _procInfo.ExeName, exitCode));
                    return ProcStatus.Error;
                }

                Log(string.Format(Resources.ProcessRunner_RunProcess__0__exited_successfully_, _procInfo.ExeName));
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

        // Example: File write date 5/10/2024 11:20:12 AM is before --import-on-or-after date 7/2/2024 12:00:00 AM. Ignoring...
        private static readonly Regex fileIgnoredRegex =
            new Regex("File write date .* is before --import-on-or-after date .*\\. Ignoring", RegexOptions.Compiled);
        
        private bool DetectError(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            if (message.Contains("The file has already been imported. Ignoring...") ||
                message.Contains("No files left to import") ||
                fileIgnoredRegex.IsMatch(message))
            {
                _fileImportIgnored = true; // SkylineRunner will return an exit code of 2 which will cause the file to be put 
                                           // on the reimport queue. We don't want that.
               return false;
            }

            if (message.Contains("Failed importing"))
            {
                _documentImportFailed = true;
                return true;
            }
            if(message.StartsWith("Warning: Cannot read file") && message.EndsWith("Ignoring...") ||
               message.Contains("Unreadable Thermo file"))
            {
                // This is the message for un-readable RAW files from Thermo instruments.
                _documentImportFailed = true;
                return true;
            }

            if (message.Contains(@"QC folders allow new imports to add or remove peptides, but not completely change the list")
                || message.Contains(@"QC folders allow new imports to add or remove molecules, but not completely change the list")
                || message.Contains(@"Invalid audit log")
                || message.Contains(@"does not have permissions to upload to the Panorama folder")
                || message.Contains(@"You do not have permission to delete runs "))
            {
                _fatalPanoramaError = true;
                return true;
            }

            // SkylineRunner.exe and SkylineDailyRunner.exe print this when the ClickOnce Skyline is not installed.
            // "Could not connect to Skyline" is not counted. It means Skyline is installed but did not start in time.
            if (message.StartsWith("Error: Unable to find Skyline program"))
            {
                _skylineNotFound = true;
            }

            if (!message.StartsWith("Error")) return false;

            _errorLogged = true;
            return true;
        }

        private void Log(string message)
        {
            _logger.Log(message);
        }

        private void LogError(string message)
        {
            _logger.LogError(message);
        }

        private void LogException(Exception e, string message)
        {
            _logger.LogError(message, e.ToString());
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
                    LogException(e, string.Format("Error killing process {0}", _procInfo.ExeName));
                }
            }
        }
    }
}