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
using System.Text.RegularExpressions;
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
        private readonly string _oldLog;
        private readonly string _logDir;
        private readonly string _nightlyLog;
        private readonly Nightly.RunMode _runMode;
        private readonly object _lock;

        private string _testerLog;
        private FileStream _fileStream;
        private bool _isHang;
        private bool _hangNotificationSent;
        private bool _debuggerAttached;
        private readonly byte[] _buffer;
        private readonly StringBuilder _builder;
        private string _logEntire;
        private string _logTail;
        private readonly Regex _testLineRegex;
        private string _lastTest;

        private readonly Timer _logChecker;

        public LogFileMonitor(string logDir, string nightlyLog, Nightly.RunMode runMode)
        {
            _oldLog = Nightly.GetLatestLog(logDir);
            _logDir = logDir;
            _nightlyLog = nightlyLog;
            _runMode = runMode;
            _testerLog = null;
            _lock = new object();
            _fileStream = null;
            _isHang = false;
            _hangNotificationSent = false;
            _debuggerAttached = false;
            _buffer = new byte[8192];
            _builder = new StringBuilder();
            _logEntire = string.Empty;
            _logTail = "";
            _testLineRegex = new Regex(@"\[\d\d:\d\d\] +\d+.\d+ +(\S+) +\(\w\w\)", RegexOptions.Compiled | RegexOptions.RightToLeft);
            _lastTest = null;
            _logChecker = new Timer(10000); // check log file every 10 seconds
            _logChecker.Elapsed += CheckLog;
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

        private int HangThreshold => _runMode != Nightly.RunMode.perf && _runMode != Nightly.RunMode.release_perf && _runMode != Nightly.RunMode.integration_perf ? 60 : 90;
        private string RunModeName => Enum.GetName(typeof(Nightly.RunMode), _runMode);

        public bool IsHang
        {
            get
            {
                lock (_lock)
                {
                    return _isHang;
                }
            }
        }

        public bool IsDebugger
        {
            get
            {
                if (_debuggerAttached)
                    return true;

                CheckLog(null, null);
                lock (_lock)
                {
                    return _debuggerAttached;
                }
            }
        }

        private void CheckLog(object source, ElapsedEventArgs e)
        {
            if (!Monitor.TryEnter(_lock))
                return;

            try
            {
                var signalTime = e?.SignalTime ?? DateTime.Now;

                if (_fileStream == null)
                {
                    var log = Nightly.GetLatestLog(_logDir);
                    if (Equals(log, _oldLog))
                        return;
                    _testerLog = log;
                    _fileStream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    _logEntire = string.Empty;
                }

                if (HangThreshold > 0)
                {
                    var lastWrite = File.GetLastWriteTime(_testerLog);
                    // Compare both local and UTC times to avoid false detections of hangs during DST transitions
                    _isHang = lastWrite.AddMinutes(HangThreshold) <= signalTime &&
                              lastWrite.ToUniversalTime().AddMinutes(HangThreshold) <= signalTime.ToUniversalTime();
                    if (_isHang)
                    {
                        if (!_hangNotificationSent)
                        {
                            _hangNotificationSent = true;

                            Log("Hang detected, posting to " + Nightly.LABKEY_EMAIL_NOTIFICATION_URL);
                            var subject = string.Format("[{0} ({1})] !!! TestResults alert", Environment.MachineName, RunModeName);
                            if (!string.IsNullOrEmpty(_lastTest))
                                subject += string.Format(" ({0})", _lastTest);
                            var message = new StringBuilder();
                            message.AppendFormat("{0} ({1})", Environment.MachineName, RunModeName);
                            message.AppendLine();
                            message.AppendLine("Hang detected");
                            message.AppendLine();
                            message.AppendFormat("Current time: {0} {1}" + Environment.NewLine, signalTime.ToShortDateString(), signalTime.ToShortTimeString());
                            message.AppendFormat("Log last modified: {0} {1}" + Environment.NewLine, lastWrite.ToShortDateString(), lastWrite.ToShortTimeString());
                            message.AppendLine();
                            if (string.IsNullOrEmpty(_lastTest))
                            {
                                message.AppendLine("No tests found. Complete log follows:");
                                message.AppendLine("----------------------------------------");
                                message.Append(_logEntire);
                                message.AppendLine();
                            }
                            else
                            {
                                message.AppendLine("----------------------------------------");
                                message.AppendLine("...");
                                message.Append(_logTail);
                                message.AppendLine();
                            }
                            SendEmailNotification(subject, message.ToString());
                        }
                        return;
                    }
                }

                var totalN = 0;
                for (var n = _fileStream.Read(_buffer, 0, _buffer.Length); n > 0; n = _fileStream.Read(_buffer, 0, _buffer.Length))
                {
                    totalN += n;
                    _builder.Append(Encoding.UTF8.GetString(_buffer, 0, n));
                    if (n < _buffer.Length)
                        break;
                }

                if (totalN == 0)
                    return;

                var s = _builder.ToString();
                _logEntire += s;

                var match = _testLineRegex.Match(s);
                if (match.Success)
                    _lastTest = match.Groups[1].Value;

                _logTail += s.Substring(s.Length - totalN);
                const int tailLineCount = 20;
                var tailLines = _logTail.Split('\n');
                _logTail = string.Join("\n", tailLines.Length > tailLineCount ? tailLines.Skip(tailLines.Length - tailLineCount) : tailLines);

                var lastBreak = s.LastIndexOf('\n');
                if (lastBreak < 0)
                    return;

                _builder.Remove(0, lastBreak + 1);
                foreach (var line in s.Substring(0, lastBreak).Split(new[] {"\r", "\n", "\r\n"}, StringSplitOptions.None))
                {
                    // process lines
                    if (!_debuggerAttached && line.Contains("#!!!!! DEBUGGING STARTED !!!!!"))
                    {
                        _debuggerAttached = true;
                    }
                }
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

        private static void SendEmailNotification(string subject, string message)
        {
            Nightly.SendEmailNotification(null, subject, message);
        }
    }
}
