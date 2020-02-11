/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace SkylineNightly
{
    public class LogFileMonitor
    {
        public const string DELIMITER_START = "\n@@@ EMAIL NOTIFICATION";
        public const string DELIMITER_END = "\n@@@";

        private readonly string _oldLog;
        private readonly string _logDir;
        private readonly string _nightlyLog;
        private readonly object _lock;

        private FileStream _fileStream;
        private readonly byte[] _buffer;
        private readonly StringBuilder _builder;

        private readonly Timer _logChecker;

        public LogFileMonitor(string logDir, string nightlyLog)
        {
            _oldLog = Nightly.GetLatestLog(logDir);
            _logDir = logDir;
            _nightlyLog = nightlyLog;
            _lock = new object();
            _fileStream = null;
            _buffer = new byte[8192];
            _builder = new StringBuilder();
            _logChecker = new Timer(10000); // check log file every 10 seconds
            _logChecker.Elapsed += IntervalElapsed;
        }

        public void Start()
        {
            lock (_lock)
            {
                _logChecker.Enabled = true;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _fileStream?.Dispose();
                _logChecker.Enabled = false;
            }
        }

        private void IntervalElapsed(object source, ElapsedEventArgs e)
        {
            if (!Monitor.TryEnter(_lock))
                return;

            try
            {
                if (_fileStream == null)
                {
                    var log = Nightly.GetLatestLog(_logDir);
                    if (Equals(log, _oldLog))
                        return;
                    _fileStream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }

                const double HANG_MINUTES = 30;
                var lastWrite = File.GetLastWriteTime(_logDir);
                if (lastWrite.AddMinutes(HANG_MINUTES) <= e.SignalTime)
                {
                    Log("Hang detected, posting to " + Nightly.LABKEY_EMAIL_NOTIFICATION_URL);
                    SendEmailNotification(string.Format("{0}\n\nLog file last modified at {1} {2}\n{3}",
                        Environment.MachineName, lastWrite.ToShortDateString(), lastWrite.ToShortTimeString(), File.ReadLines(_logDir).Last()));
                    return;
                }

                var totalN = 0;
                for (var n = _fileStream.Read(_buffer, 0, _buffer.Length);
                    n > 0;
                    n = _fileStream.Read(_buffer, 0, _buffer.Length))
                {
                    totalN += n;
                    _builder.Append(Encoding.UTF8.GetString(_buffer, 0, n));
                    if (n < _buffer.Length)
                        break;
                }

                if (totalN == 0)
                    return;

                var s = _builder.ToString();
                for (var start = s.IndexOf(DELIMITER_START, StringComparison.CurrentCulture); start != -1; start = s.IndexOf(DELIMITER_START, StringComparison.CurrentCulture))
                {
                    _builder.Remove(0, start);
                    s = _builder.ToString();
                    var end = s.IndexOf(DELIMITER_END, StringComparison.CurrentCulture);
                    if (end == -1)
                        return;

                    Log("Email notification found in log file, posting to " + Nightly.LABKEY_EMAIL_NOTIFICATION_URL);
                    SendEmailNotification(s.Substring(DELIMITER_START.Length, end - DELIMITER_START.Length));

                    _builder.Remove(0, end + DELIMITER_END.Length);
                }

                var lastBreak = s.LastIndexOf('\n');
                if (lastBreak > 0)
                    _builder.Remove(0, lastBreak + 1);
            }
            catch
            {
                // ignored
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }

        private void Log(string message)
        {
            Nightly.Log(_nightlyLog, message);
        }

        private static void SendEmailNotification(string message)
        {
            Nightly.SendEmailNotification(null, string.Format("[{0}] !!! TestResults alert", Environment.MachineName), message);
        }
    }
}
