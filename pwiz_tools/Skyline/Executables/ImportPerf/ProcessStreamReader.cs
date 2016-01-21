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
using System.Threading;

namespace ImportPerf
{
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
        private bool _isOutComplete;
        private bool _isErrComplete;
        private Exception _readException;

        private readonly List<string> _readLines = new List<string>();

        public ProcessStreamReader(Process process)
        {
            Thread threadOut = new Thread(() => ReadStream(process.StandardOutput, ref _isOutComplete));
            Thread threadErr = new Thread(() => ReadStream(process.StandardError, ref _isErrComplete));
            threadOut.Start();
            threadErr.Start();
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
                    if (_readException != null)
                        throw _readException;
                    if (_isOutComplete && _isErrComplete)
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