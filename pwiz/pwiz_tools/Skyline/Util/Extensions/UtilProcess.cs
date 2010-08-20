/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

namespace pwiz.Skyline.Util.Extensions
{
    public static class UtilProcess
    {
        public static void RunProcess(this ProcessStartInfo psi, IProgressMonitor progress, ref ProgressStatus status)
        {
            psi.RunProcess(null, null, progress, ref status);
        }

        public static void RunProcess(this ProcessStartInfo psi, string stdin)
        {
            var statusTemp = new ProgressStatus("");
            psi.RunProcess(stdin, null, null, ref statusTemp);
        }

        public static void RunProcess(this ProcessStartInfo psi, string stdin, string messagePrefix, IProgressMonitor progress, ref ProgressStatus status)
        {
            // Make sure required streams are redirected.
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            var proc = Process.Start(psi);
            if (proc == null)
                throw new IOException(string.Format("Failure starting {0} command.", psi.FileName));
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
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (progress == null || line.ToLower().StartsWith("error"))
                {
                    sbError.Append(line);
                }
                else // if (progress != null)
                {
                    if (progress.IsCanceled)
                    {
                        proc.Kill();
                        progress.UpdateProgress(status = status.Cancel());
                        return;
                    }

                    if (line.EndsWith("%"))
                    {
                        double percent;
                        string[] parts = line.Split(' ');
                        string percentPart = parts[parts.Length - 1];
                        if (double.TryParse(percentPart.Substring(0, percentPart.Length - 1), out percent))
                        {
                            status = status.ChangePercentComplete((int)percent);
                            if (percent >= 100 && status.SegmentCount > 0)
                                status = status.NextSegment();
                            progress.UpdateProgress(status);
                        }
                    }
                    else if (messagePrefix == null || line.StartsWith(messagePrefix))
                    {
                        // Remove prefix, if there is one.
                        if (messagePrefix != null)
                            line = line.Substring(messagePrefix.Length);

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
                    throw new Exception("Error occurred running process.");
                throw new IOException(sbError.ToString());
            }
        }
    }

    /// <summary>
    /// Class for reading both standard out and standard error from a process.
    /// <para>
    /// This is a tough problem, since TextReader.ReadLine() blocks, until it
    /// has a line to return, or the process ends.  It did not seem possible
    /// to solve this on a single thread to present real-time feedback to the
    /// user based on process output.
    /// </para><para>
    /// One solution presented on the web looked promising, but it did not
    /// correctly interleave output from both streams reliably.</para>
    /// </summary>
    public class ProcessStreamReader
    {
        private readonly Thread _threadOut;
        private bool _isOutComplete;
        private readonly Thread _threadErr;
        private bool _isErrComplete;
        private Exception _readException;

        private readonly List<string> _readLines = new List<string>();

        public ProcessStreamReader(Process process)
        {
            _threadOut = new Thread(() => ReadStream(process.StandardOutput, ref _isOutComplete));
            _threadErr = new Thread(() => ReadStream(process.StandardError, ref _isErrComplete));
            _threadOut.Start();
            _threadErr.Start();
        }

        /// <summary>
        /// Public access to read the next line from the interleaved output
        /// of both standard out and standard error.
        /// </summary>
        public string ReadLine()
        {
            lock (_readLines)
            {
                for (;;)
                {
                    if (_readLines.Count > 0)
                    {
                        string line = _readLines[0];
                        _readLines.RemoveAt(0);
                        return line;
                    }
                    else if (_readException != null)
                        throw _readException;
                    else if (_isOutComplete && _isErrComplete)
                        return null;

                    Monitor.Wait(_readLines);
                }
            }
        }

        /// <summary>
        /// Handles reading from a single stream, and noting its completion
        /// on a background thread.
        /// </summary>
        private void ReadStream(TextReader reader, ref bool isComplete)
        {
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lock (_readLines)
                    {
                        _readLines.Add(line);
                        Monitor.Pulse(_readLines);
                    }
                }

                lock(_readLines)
                {
                    isComplete = true;
                    Monitor.Pulse(_readLines);
                }
            }
            catch (Exception x)
            {
                lock (_readLines)
                {
                    _readException = x;
                    Monitor.Pulse(_readLines);
                }
            }
        }
    }
}
