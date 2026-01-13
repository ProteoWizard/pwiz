/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Text;
using System.Threading;
using pwiz.Common.CommonResources;

namespace pwiz.Common.SystemUtil
{
    public interface IProcessRunner
    {
        string StatusPrefix { get; set; }
        string HideLinePrefix { get; set; }
        void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref IProgressStatus status,
            ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, bool forceTempfilesCleanup = false);
        void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref IProgressStatus status,
                 TextWriter writer, ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal,
                 bool forceTempfilesCleanup = false,
                 Func<string, int, bool> outputAndExitCodeAreGoodFunc = null,
                 bool updateProgressPercentage = true);
    }

    public class ProcessRunner : IProcessRunner
    {
        public string StatusPrefix { get; set; }

        public Encoding OutputEncoding { get; set; }

        public string MessagePrefix { get; set; }
        public bool ShowCommandAndArgs { get; set; }
        private readonly List<string> _messageLog = new List<string>();
        private string _tmpDirForCleanup;

        /// <summary>
        /// When greater than zero, this value is used to track progress percent complete.
        /// </summary>
        public int ExpectedOutputLinesCount { get; set; }
        /// <summary>
        /// This value tracks the total number of lines output on last call to Run, and is used to help track significant deviation from ExpectedOutputLinesCount.
        /// </summary>
        public int OutputLinesGenerated { get; private set; }

        /// <summary>
        /// When set to true this value prevents progress status message updates based on process output.
        /// </summary>
        public bool SilenceStatusMessageUpdates { get; set; }

        /// <summary>
        /// Used in R package installation. We print progress % for processRunner progress
        /// but we dont want that output to be shown to the user when we display the output
        /// of the installation script to the immediate window. 
        /// Any line that starts with the HideLinePrefix will not be written to the writer.
        /// </summary>
        public string HideLinePrefix { get; set; }

        public static bool GoodIfExitCodeIsZero(string stderr, int exitCode)
        {
            return exitCode == 0;
        }

        public void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref IProgressStatus status,
            ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, bool forceTempfilesCleanup = false)
        {
            Run(psi, stdin, progress, ref status, null, priorityClass, forceTempfilesCleanup);
        }

        public void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref IProgressStatus status, TextWriter writer,
            ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal,
            bool forceTempfilesCleanup = false,
            Func<string, int, bool> outputAndExitCodeAreGoodFunc = null,
            bool updateProgressPercentage = true)
        {
            // Make sure required streams are redirected.
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = stdin != null;
            if (OutputEncoding != null)
                psi.StandardOutputEncoding = psi.StandardErrorEncoding = OutputEncoding;

            outputAndExitCodeAreGoodFunc ??= GoodIfExitCodeIsZero;

            _messageLog.Clear();
            var cmd = $@"{psi.FileName} {psi.Arguments}";

            // Optionally create a subdir in the current TMP directory, run the new process with TMP set to that so we can clean it out afterward
            _tmpDirForCleanup = forceTempfilesCleanup ? SetTmpDirForCleanup(psi) : null;

            Process proc = null;
            var msgFailureStartingCommand = $@"Failure starting command ""{cmd}"".";
            try
            {
                proc = Process.Start(psi);
                if (proc == null)
                {
                    throw new IOException(msgFailureStartingCommand);
                }
            }
            catch (Exception x)
            {
                throw new IOException(msgFailureStartingCommand, x);
            }

            try
            {
                proc.PriorityClass = priorityClass;
            }
            catch
            {
                // Ignore
            }
            if (stdin != null)
            {
                try
                {
                    proc.StandardInput.WriteLine(stdin);
                }
                finally
                {
                    proc.StandardInput.Close();
                }
            }

            if (ShowCommandAndArgs)
            {
                foreach (var msg in new[]{string.Empty, MessageResources.ProcessRunner_Run_Run_command_, cmd, string.Empty, string.Empty})
                {
                    if (!string.IsNullOrEmpty(msg))
                    {
                        _messageLog.Add(msg);
                    }
                    status = status.ChangeMessage(msg); // Each message will be displayed in a separate line
                    progress.UpdateProgress(status);
                }
            }
            StringBuilder sbOutput = null;
            if (writer == null)
            {
                sbOutput = new StringBuilder();
                writer = new StringWriter(sbOutput);
            }

            try
            {
                var reader = new ProcessStreamReader(proc, StatusPrefix == null && MessagePrefix == null);
                StringBuilder sbError = new StringBuilder();
                int percentLast = 0;
                string line;
                OutputLinesGenerated = 0;
                while ((line = reader.ReadLine(progress)) != null)
                {
                    if (writer != null && (HideLinePrefix == null || !line.StartsWith(HideLinePrefix)))
                    {
                        writer.WriteLine(line);
                        OutputLinesGenerated++;
                    }

                    string lineLower = line.ToLowerInvariant();
                    if (progress == null || lineLower.StartsWith(@"error") || lineLower.StartsWith(@"warning"))
                    {
                        sbError.AppendLine(line);
                    }
                    else // if (progress != null)
                    {
                        if (progress.IsCanceled)
                        {
                            if (!proc.HasExited)
                            {
                                proc.Kill();
                            }
                            progress.UpdateProgress(status = status.Cancel());
                            CleanupTmpDir(psi); // Clean out any tempfiles left behind, if forceTempfilesCleanup was set
                            return;
                        }

                        if (MessagePrefix != null && line.StartsWith(MessagePrefix))
                        {
                            _messageLog.Add(line.Substring(MessagePrefix.Length));
                        }
                        else if (updateProgressPercentage && line.EndsWith(@"%"))
                        {
                            double percent;
                            string[] parts = line.Split(' ');
                            string percentPart = parts[parts.Length - 1];
                            if (double.TryParse(percentPart.Substring(0, percentPart.Length - 1), out percent))
                            {
                                percentLast = (int)percent;
                                status = status.ChangePercentComplete(percentLast);
                                if (percent >= 100 && status.SegmentCount > 0)
                                    status = status.NextSegment();
                                progress.UpdateProgress(status);
                            }
                        }
                        else if (StatusPrefix == null || line.StartsWith(StatusPrefix))
                        {
                            // Remove prefix, if there is one.
                            if (StatusPrefix != null)
                                line = line.Substring(StatusPrefix.Length);

                            var statusOld = status;
                            if (!SilenceStatusMessageUpdates)
                                status = status.ChangeMessage(line);

                            if (updateProgressPercentage && ExpectedOutputLinesCount > 0)
                            {
                                percentLast = Math.Min(99, OutputLinesGenerated * 100 / ExpectedOutputLinesCount);
                                if (percentLast != status.PercentComplete)
                                    status = status.ChangePercentComplete(percentLast);
                            }
                            if (!ReferenceEquals(status, statusOld))
                                progress.UpdateProgress(status);
                        }
                    }
                }
                proc.WaitForExit();
                int exit = proc.ExitCode;

                if (!outputAndExitCodeAreGoodFunc(reader.GetErrorLines(), exit))
                {
                    line = proc.StandardError.ReadLine();
                    if (line != null)
                        sbError.AppendLine(line);
                    if (sbError.Length == 0)
                    {
                        sbError.AppendLine(@"Error occurred running process.");
                        sbError.Append(reader.GetErrorLines());
                    }

                    string processPath = Path.GetDirectoryName(psi.FileName)?.Length == 0
                        ? Path.Combine(Environment.CurrentDirectory, psi.FileName)
                        : psi.FileName;
                    // ReSharper disable LocalizableElement
                    sbError.AppendFormat("\r\nCommand-line: {0} {1}\r\nWorking directory: {2}{3}\r\nExit code: {4}", processPath,
                        CommonTextUtil.SpaceSeparate(proc.StartInfo.Arguments), psi.WorkingDirectory,
                        stdin != null ? "\r\nStandard input:\r\n" + stdin : string.Empty, PInvoke.Kernel32.FormatExitCode(exit));
                    // ReSharper restore LocalizableElement
                    throw new IOException(sbError.ToString());
                }

                // Make to complete the status, if the process succeeded, but never
                // printed 100% to the console
                if (updateProgressPercentage && percentLast < 100)
                {
                    status = status.ChangePercentComplete(100);
                    if (status.SegmentCount > 0)
                        status = status.NextSegment();
                    if (progress != null)
                        progress.UpdateProgress(status);
                }

            }
            catch (Exception ex)  // CONSIDER: Should we handle more types like WrapAndThrowException does?
            {
                if (sbOutput != null)
                    ThrowExceptionWithOutput(ex, sbOutput.ToString());

                throw;
            }
            finally
            {
                if (!proc.HasExited)
                    try { proc.Kill(); } catch (InvalidOperationException) { }

                CleanupTmpDir(psi); // Clean out any tempfiles left behind, if forceTempfilesCleanup was set
            }
        }

        private void ThrowExceptionWithOutput(Exception exception, string output)
        {
            var sbText = new StringBuilder();
            sbText.AppendLine(exception.Message)
                .AppendLine()
                .AppendLine(@"Output:")
                .AppendLine(output);
            throw new IOException(exception.Message, new IOException(sbText.ToString(), exception));
        }

        // Many external tools that we call can't deal with unicode characters, this helps with temp files they may create
        public void ChangeTmpDirEnvironmentVariableToNonUnicodePath(ProcessStartInfo psi)
        {
            ChangeEnvironmentVariableToNonUnicodePath(psi, @"TMP");
            ChangeEnvironmentVariableToNonUnicodePath(psi,@"TEMP");
        }

        // Look for unicode characters in path for in environment variable value, replace with 8.3
        // Path has to exist, and volume has to support 8.3 format
        public void ChangeEnvironmentVariableToNonUnicodePath(ProcessStartInfo psi, string key)
        {
            var tmp = PathEx.GetNonUnicodePath(psi.Environment[key]);
            if (!string.IsNullOrEmpty(tmp))
            {
                psi.Environment[key] = tmp;
            }
        }

        // Clean out any tempfiles left behind, if forceTempfilesCleanup was set
        private void CleanupTmpDir(ProcessStartInfo psi)
        {
            if (!string.IsNullOrEmpty(_tmpDirForCleanup))
            {
                var maxRetry = 4;
                for (var retryCount = 0; retryCount++ < maxRetry;)
                {
                    try
                    {
                        if (Directory.Exists(_tmpDirForCleanup))
                            Directory.Delete(_tmpDirForCleanup, true);
                        psi.Environment[@"TMP"] = Path.GetDirectoryName(_tmpDirForCleanup); // restore previous TMP value in case ProcessStartInfo is re-used
                        return;
                    }
                    catch (Exception e)
                    {
                        _messageLog.Add($@"warning: failed attempt {retryCount}/{maxRetry} for cleanup of temporary directory ""{_tmpDirForCleanup}"": {e.Message}");
                        Thread.Sleep(500);
                    }
                }
            }
        }

        // Create a subdir in the current TMP directory, run the new process with TMP set to that so we can clean it out afterward
        private string SetTmpDirForCleanup(ProcessStartInfo psi)
        {
            string tmpDirForCleanup = null;

            if (psi.UseShellExecute)
            {
                _messageLog.Add(@"warning: UseShellExecute is set, cannot change environment for tempfile cleanup");
            }
            else
            {
                try
                {
                    tmpDirForCleanup = psi.Environment.TryGetValue(@"TMP", out var value) ? value : Path.GetTempPath();
                    var exeName = string.Empty;
                    if (!string.IsNullOrEmpty(psi.FileName))
                    {
                        // Name the directory so as to be more obviously associated with the process
                        exeName = Path.GetFileNameWithoutExtension(psi.FileName);
                        if (!string.IsNullOrEmpty(exeName))
                        {
                            tmpDirForCleanup = Path.Combine(tmpDirForCleanup, exeName + "_" + Path.GetRandomFileName());
                        }
                    }

                    if (Directory.Exists(tmpDirForCleanup) || File.Exists(tmpDirForCleanup))
                    {
                        _messageLog.Add($@"Could not create unique TMP dir ""{tmpDirForCleanup}"" for process {exeName}, it already exists");
                    }
                    Directory.CreateDirectory(tmpDirForCleanup);
                    psi.Environment[@"TMP"] = tmpDirForCleanup; // Process will create its tempfiles here
                }
                catch (Exception e)
                {
                    _messageLog.Add(
                        $@"warning: could not create directory {tmpDirForCleanup} for tempfile cleanup: {e.Message}");
                    tmpDirForCleanup = null;
                }
            }

            return tmpDirForCleanup;
        }

        public IEnumerable<string> MessageLog()
        {
            return _messageLog;
        }

    }
}
