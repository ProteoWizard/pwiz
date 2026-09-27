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
    /// Lives in PortableUtil (pure BCL, no .resx) so both Skyline and Osprey can
    /// share it. The only host-specific input is error-line detection: a host that
    /// localizes its "Error:" prefix assigns <see cref="IsErrorMessage"/>.
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
            // A background loader can still report progress after the command finished and
            // closed this writer. Dropping the line is right - nothing is listening - but
            // throwing unwinds the loader thread and leaks whatever it had open.
            var writer = _writer;
            if (writer == null)
                return;
            var message = new StringBuilder();
            if (IsTimeStamped)
                // ReSharper disable LocalizableElement
                message.Append(DateTime.Now.ToString("[yyyy/MM/dd HH:mm:ss]\t"));
                // ReSharper restore LocalizableElement
            if (IsMemStamped)
            {
                lock (writer)
                {
                    // This can take long enough that we need to introduce a lock to keep
                    // output ordered as much as possible
                    message.Append(MemStamp(GC.GetTotalMemory(false)));
                    message.Append(MemStamp(Process.GetCurrentProcess().PrivateMemorySize64));
                }
            }
            message.Append(value);
            // Pass a string, not the StringBuilder: net8 added TextWriter.WriteLine(StringBuilder),
            // which routes through Write(char) - unimplemented by some TextWriter subclasses (e.g.
            // the Immediate Window sink) - so output was silently dropped. WriteLine(string) is the
            // overload every sink reliably overrides.
            writer.WriteLine(message.ToString());
            writer.Flush();

            if (!IsErrorReported && IsErrorMessage(value))
            {
                IsErrorReported = true;
            }
        }

        public const string ERROR_MESSAGE_HINT = @"Error:";

        /// <summary>
        /// The error prefix in each language Skyline and Osprey are translated into: English,
        /// Japanese and Chinese (full-width colon). Literals rather than resources because
        /// PortableUtil carries no .resx, and because a reader of a log - SkylineRunner, a
        /// regression gate - must recognize an error from ANY language, not only the current
        /// UI culture's. Mirrors SkylineRunner's ErrorChecker, which cannot reference this
        /// assembly because it ships as a single exe.
        /// </summary>
        public static readonly string[] ERROR_PREFIXES =
        {
            ERROR_MESSAGE_HINT,
            "\u30A8\u30E9\u30FC\uFF1A", // ja
            "\u9519\u8BEF\uFF1A"        // zh-CHS
        };

        /// <summary>
        /// True when a LOG line - as written, with any <c>--timestamp</c> / <c>--memstamp</c>
        /// columns in front - reports an error in any of <see cref="ERROR_PREFIXES"/>: the
        /// prefix starts the line or follows a tab.
        /// </summary>
        public static bool IsErrorLine(string line)
        {
            if (line == null)
                return false;
            foreach (var prefix in ERROR_PREFIXES)
            {
                int i = line.IndexOf(prefix, StringComparison.Ordinal);
                if (i == 0 || (i > 0 && line[i - 1] == '\t'))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Predicate deciding whether a written line marks an error (which flips
        /// <see cref="IsErrorReported"/>). Defaults to <see cref="DefaultIsErrorMessage"/>
        /// (any of <see cref="ERROR_PREFIXES"/>). A host that localizes its error prefix
        /// (e.g. Skyline) may assign a lambda that also checks the current culture's variant.
        /// CRITICAL: this is a Func invoked per line, so the host lambda re-resolves its
        /// localized string to the current UI culture on every call. NEVER capture a
        /// localized string in a static -- tests switch language in-process, so a frozen
        /// first-locale value would miss every later language's error lines.
        /// </summary>
        public static Func<string, bool> IsErrorMessage { get; set; } = DefaultIsErrorMessage;

        public static bool DefaultIsErrorMessage(string message)
        {
            if (message == null)
                return false;
            foreach (var prefix in ERROR_PREFIXES)
            {
                if (message.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
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
