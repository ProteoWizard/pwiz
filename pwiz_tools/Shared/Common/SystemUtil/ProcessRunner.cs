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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace pwiz.Common.SystemUtil
{
    public interface IProcessRunner
    {
        string StatusPrefix { get; set; }
        string HideLinePrefix { get; set; }
        void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref ProgressStatus status);
        void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref ProgressStatus status,
                 TextWriter writer);
    }

    public class ProcessRunner : IProcessRunner
    {
        public string StatusPrefix { get; set; }

        public string MessagePrefix { get; set; }
        private readonly List<string> _messageLog = new List<string>();

        /// <summary>
        /// Used in R package installation. We print progress % for processRunner progress
        /// but we dont want that output to be shown to the user when we display the output
        /// of the installation script to the immediate window. 
        /// Any line that starts with the HideLinePrefix will not be written to the writer.
        /// </summary>
        public string HideLinePrefix { get; set; }

        public void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref ProgressStatus status)
        {
            Run(psi, stdin, progress,ref status, null);
        }

        public void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref ProgressStatus status, TextWriter writer)
        {
            // Make sure required streams are redirected.
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            _messageLog.Clear();

            var proc = Process.Start(psi);
            if (proc == null)
                throw new IOException(string.Format("Failure starting {0} command.", psi.FileName)); // Not L10N
            if (stdin != null)
            {
                try
                {
                    proc.StandardInput.Write(stdin);
                }
                finally
                {
                    proc.StandardInput.Close();
                }
            }

            var reader = new ProcessStreamReader(proc);
            StringBuilder sbError = new StringBuilder();
            int percentLast = 0;
            string line;
            while ((line = reader.ReadLine(progress)) != null)
            {
                if (writer != null && !line.StartsWith(HideLinePrefix))
                    writer.WriteLine(line);

                if (progress == null || line.ToLowerInvariant().StartsWith("error")) // Not L10N
                {
                    sbError.AppendLine(line);
                }
                else // if (progress != null)
                {
                    if (progress.IsCanceled)
                    {
                        proc.Kill();
                        progress.UpdateProgress(status = status.Cancel());
                        return;
                    }

                    if (!string.IsNullOrEmpty(MessagePrefix) && line.StartsWith(MessagePrefix))
                    {
                        _messageLog.Add(line.Substring(MessagePrefix.Length));
                    }
                    else if (line.EndsWith("%")) // Not L10N
                    {
                        double percent;
                        string[] parts = line.Split(' ');
                        string percentPart = parts[parts.Length - 1];
                        if (double.TryParse(percentPart.Substring(0, percentPart.Length - 1), out percent))
                        {
                            percentLast = (int) percent;
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

                        status = status.ChangeMessage(line);
                        progress.UpdateProgress(status);
                    }
                }
            }
            proc.WaitForExit();
            int exit = proc.ExitCode;
            if (exit != 0)
            {
                line = proc.StandardError.ReadLine();
                if (line != null)
                    sbError.AppendLine(line);
                if (sbError.Length == 0)
                    throw new IOException("Error occurred running process."); // Not L10N
                throw new IOException(sbError.ToString());
            }
            // Make to complete the status, if the process succeeded, but never
            // printed 100% to the console
            if (percentLast < 100)
            {
                status = status.ChangePercentComplete(100);
                if (status.SegmentCount > 0)
                    status = status.NextSegment();
                if (progress != null)
                    progress.UpdateProgress(status);
            }
        }

        public IEnumerable<string> MessageLog()
        {
            return _messageLog;
        }

        public class ProcessRunnerTester: IProcessRunner
        {
            public string stringToWriteToWriter { get; set; }
            public ProgressStatus progressStatus { get; set; }
            public bool shouldCancel { get; set; }
            public string StatusPrefix { get; set; }
            public string HideLinePrefix { get; set; }
            public void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref ProgressStatus status)
            {
                Run(psi, stdin, progress, ref status, null);
            }

            public void Run(ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref ProgressStatus status, TextWriter writer)
            {
                if (shouldCancel)
                {
                    status.Cancel();
                    progress.UpdateProgress(status = status.Cancel());
                    return;
                }

                if (!string.IsNullOrEmpty(stringToWriteToWriter))
                    writer.WriteLine(stringToWriteToWriter);
                status.ChangePercentComplete(100);
                progress.UpdateProgress(status);
            }
        }
    }
}
