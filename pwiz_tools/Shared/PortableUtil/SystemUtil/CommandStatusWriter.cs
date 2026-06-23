/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// A <see cref="TextWriter"/> that routes all command-line output through a single
    /// sink, optionally prefixing each line with a timestamp and/or a managed/private
    /// memory stamp, and tracking whether any error message has been written.
    ///
    /// Lives in PortableUtil (pure BCL, no .resx) so both Skyline and OspreySharp can
    /// share it. The only host-specific input is the localized "Error:" prefix used to
    /// detect error lines: a host installs its localized variant via
    /// <see cref="AddErrorMessageHint"/>.
    /// </summary>
    public class CommandStatusWriter : TextWriter
    {
        private TextWriter _writer;

        public CommandStatusWriter(TextWriter writer)
            : base(writer.FormatProvider)
        {
            _writer = Synchronized(writer); // Make this thread safe for more predictable console output
        }

        public bool IsTimeStamped { get; set; }

        public bool IsMemStamped { get; set; }

        public bool IsErrorReported { get; private set; }

        public bool IsVerboseExceptions { get; set; }

        public override Encoding Encoding
        {
            get { return _writer.Encoding; }
        }

        protected override void Dispose(bool disposing)
        {
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }
        }

        public override void Flush()
        {
            _writer.Flush();
        }

        public override void Write(char value)
        {
            _writer.Write(value);
        }

        public override void Write(string value)
        {
            _writer.Write(value);
        }

        public override void WriteLine()
        {
            WriteLine(string.Empty);
        }

        public void WriteException(string formatMessage, string string0, Exception x1, bool lineSeparate = false)
        {
            if (!lineSeparate)
                WriteLine(formatMessage, string0, ExceptionString(x1));
            else
            {
                WriteLine(formatMessage, string0);
                WriteException(x1);
            }
        }
        public void WriteException(string formatMessage, Exception x, bool lineSeparate = false)
        {
            if (string.IsNullOrEmpty(formatMessage))
                WriteException(x);
            else if (!lineSeparate)
                WriteLine(formatMessage, ExceptionString(x));
            else
            {
                WriteLine(formatMessage);
                WriteException(x);
            }
        }
        public void WriteException(Exception x)
        {
            WriteLine(ExceptionString(x));
        }

        /// <summary>
        /// Get a string reporting the exception, with information depending on the verbose exception setting.
        /// </summary>
        /// <param name="x">Exception to be reported</param>
        /// <returns>A message reporting the exception</returns>
        private string ExceptionString(Exception x)
        {
            return IsVerboseExceptions ? x.ToString() : x.Message;
        }

        public override void WriteLine(string value)
        {
            var message = new StringBuilder();
            if (IsTimeStamped)
                // ReSharper disable LocalizableElement
                message.Append(DateTime.Now.ToString("[yyyy/MM/dd HH:mm:ss]\t"));
                // ReSharper restore LocalizableElement
            if (IsMemStamped)
            {
                lock (_writer)
                {
                    // This can take long enough that we need to introduce a lock to keep
                    // output ordered as much as possible
                    message.Append(MemStamp(GC.GetTotalMemory(false)));
                    message.Append(MemStamp(Process.GetCurrentProcess().PrivateMemorySize64));
                }
            }
            message.Append(value);
            _writer.WriteLine(message);
            Flush();

            if (IsErrorMessage(value))
            {
                IsErrorReported = true;
            }
        }

        public const string ERROR_MESSAGE_HINT = @"Error:";

        // Line prefixes that mark an error message, seeded with the invariant
        // ERROR_MESSAGE_HINT ("Error:") because Skyline-daily output may be unlocalized.
        // A host that localizes its "Error:" prefix (e.g. Skyline) adds the localized
        // variant once at startup via AddErrorMessageHint. Static because localization
        // is process-wide and a log-file writer swapped in mid-run must see the same
        // hints without re-propagating per-instance state.
        private static readonly List<string> ERROR_MESSAGE_HINTS = new List<string> { ERROR_MESSAGE_HINT };

        /// <summary>
        /// Adds a (typically localized) line prefix that marks an error message, beyond
        /// the invariant <see cref="ERROR_MESSAGE_HINT"/>. PortableUtil carries no .resx,
        /// so a host that localizes its "Error:" prefix installs the localized variant here.
        /// </summary>
        public static void AddErrorMessageHint(string hint)
        {
            if (string.IsNullOrEmpty(hint))
                return;
            lock (ERROR_MESSAGE_HINTS)
            {
                if (!ERROR_MESSAGE_HINTS.Contains(hint))
                    ERROR_MESSAGE_HINTS.Add(hint);
            }
        }

        private bool IsErrorMessage(string message)
        {
            if (message != null && !IsErrorReported)
            {
                lock (ERROR_MESSAGE_HINTS)
                {
                    foreach (var hint in ERROR_MESSAGE_HINTS)
                    {
                        if (message.StartsWith(hint, StringComparison.CurrentCulture))
                            return true;
                    }
                }
            }

            return false;
        }

        private string MemStamp(long memUsed)
        {
            const double mb = 1024 * 1024;
            // ReSharper disable LocalizableElement
            return string.Format("{0}\t", Math.Round(memUsed/mb));
            // ReSharper restore LocalizableElement
        }
    }
}
