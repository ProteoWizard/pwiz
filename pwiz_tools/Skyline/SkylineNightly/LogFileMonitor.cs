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
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace SkylineNightly
{
    /// <summary>
    /// This class periodically checks the SkylineTester log file to see if an email should be sent
    /// out immediately (i.e. before the daily TestResults summary email).
    /// Email will be sent if a certain string is found in the log file, or if the log file has not
    /// been modified for a long period of time (indicating that a test is hanging) and are sent by
    /// posting to the sendEmailNotification action of the TestResults module.
    /// </summary>
    public class LogFileMonitor
    {
        public const string DELIMITER_START = "\n@@@ EMAIL NOTIFICATION";
        public const string DELIMITER_END = "\n@@@";

        private readonly string _oldLog;
        private readonly string _logDir;
        private readonly string _nightlyLog;
        private readonly int _hangThreshold;
        private readonly object _lock;

        private string _testerLog;
        private FileStream _fileStream;
        private DateTime _lastReportedHang;
        private readonly byte[] _buffer;
        private readonly StringBuilder _builder;

        private readonly Timer _logChecker;

        public LogFileMonitor(string logDir, string nightlyLog, int hangThreshold = -1)
        {
            _oldLog = Nightly.GetLatestLog(logDir);
            _logDir = logDir;
            _nightlyLog = nightlyLog;
            _hangThreshold = hangThreshold;
            _testerLog = null;
            _lock = new object();
            _fileStream = null;
            _lastReportedHang = new DateTime();
            _buffer = new byte[8192];
            _builder = new StringBuilder();
            _logChecker = new Timer(10000); // check log file every 10 seconds
            _logChecker.Elapsed += IntervalElapsed;
        }

        public void Start()
        {
            lock (_lock)
            {
                _logChecker.Start();
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _fileStream?.Dispose();
                _logChecker.Stop();
            }
        }

        public bool IsHanging()
        {
            try
            {
                return File.GetLastWriteTime(_testerLog).Equals(_lastReportedHang);
            }
            catch
            {
                return false;
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
                    _testerLog = log;
                    _fileStream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }

                if (_hangThreshold > 0)
                {
                    var lastWrite = File.GetLastWriteTime(_testerLog);
                    if (lastWrite.AddMinutes(_hangThreshold) <= e.SignalTime)
                    {
                        if (lastWrite > _lastReportedHang)
                        {
                            _lastReportedHang = lastWrite;
                            Log("Hang detected, posting to " + Nightly.LABKEY_EMAIL_NOTIFICATION_URL);
                            var message = new StringBuilder();
                            message.AppendLine(Environment.MachineName);
                            message.AppendLine("Hang detected");
                            message.AppendLine();
                            message.AppendFormat("Current time: {0} {1}" + Environment.NewLine, e.SignalTime.ToShortDateString(), e.SignalTime.ToShortTimeString());
                            message.AppendFormat("Log last modified: {0} {1}" + Environment.NewLine, lastWrite.ToShortDateString(), lastWrite.ToShortTimeString());
                            message.AppendLine();
                            message.AppendLine("----------------------------------------");
                            message.AppendLine("...");
                            var lines = File.ReadAllLines(_testerLog);
                            for (var i = Math.Max(lines.Length - 20, 0); i < lines.Length; i++)
                                message.AppendLine(lines[i]);
                            SendEmailNotification(message.ToString());
                        }
                        return;
                    }
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
