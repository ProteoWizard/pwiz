 /*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SharedBatch.Properties;

 namespace SharedBatch
{

    public class Logger
    {
        private const string DATE_PATTERN = "^\\[.*\\]\x20*";
        private const string MEMSTAMP_PATTERN = "^[0-9]+\t[0-9]+\t";


        private readonly string _filePath;

        public const long MaxLogSize = 10 * 1024 * 1024; // 10MB
        private const int MaxBackups = 5;
        public const int MaxLogLines = 10000;

        private Queue<string> _memLogMessages;
        private const int MemLogSize = 100; // Keep the last 100 log messages in memory
        private StringBuilder _logBuffer; // To be used when the log file is unavailable for writing
        private const int LogBufferSize = 10240;

        private int _lines;

        private static List<string> _errorFormats;
        private readonly List<string> _uiBuffer;

        private readonly object _uiBufferLock = new object();

        private readonly object _fileLock = new object();
        private readonly object _lock = new object();

        private FileStream _fileStream;
        private const int StreamDefaultBufferSize = 4096;



        public Logger(string logFilePath, string logName, bool initialize)
        {
            InitializeErrorFormats();

            _filePath = logFilePath;
            _uiBuffer = new List<string>();
            Name = logName;
            if (initialize)
                Init();
        }

        public string Name;

        public delegate void UiLog(string text);

        public delegate void UiLogError(string text);

        public string LogFile => _filePath;

        public string LogDirectory => Path.GetDirectoryName(_filePath);

        public string LogFileName => Path.GetFileName(_filePath);

        public bool WillTruncate => _lines > MaxLogLines;

        public bool LogTestFormat = false;

        private static void InitializeErrorFormats()
        {
            if (_errorFormats != null) return;
            _errorFormats = new List<string>() { string.Format(Resources.Logger_LogErrorToFile_ERROR___0_, ".*") };
        }

        public static void AddErrorMatch(string errorPattern)
        {
            InitializeErrorFormats();
            if (!_errorFormats.Contains(errorPattern))
                _errorFormats.Add(errorPattern);
        }

        private Regex ErrorRegex()
        {
            string errorPatterns = string.Join("|", _errorFormats);
            if (_errorFormats.Count > 1) errorPatterns = "(" + errorPatterns + ")";
            return new Regex(DATE_PATTERN + errorPatterns);
        }

        public void Init()
        {
            Directory.CreateDirectory(FileUtil.GetDirectorySafe(_filePath));
            _logBuffer = new StringBuilder();
            _memLogMessages = new Queue<string>(MemLogSize);

            // Initialize - create blank log file if doesn't exist
            if (!File.Exists(_filePath))
            {
                using (File.Create(_filePath))
                {
                }
            }
            lock (_uiBuffer)
            {
                _uiBuffer.Clear();
                _fileStream = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            }
        }

        private string _lastLogMessage;

        // Logs the text to the log as is
        private void LogVerbatim(params string[] text)
        {
            WriteToFile(TextUtil.LineSeparate(text));
            lock (_uiBufferLock)
            {
                _uiBuffer.AddRange(text);
            }
            _lines += text.Length;
        }

        public void Log(params string[] text)
        {
            var memstampRegex = new Regex(MEMSTAMP_PATTERN);
            var messageNoTimestamp = TextUtil.LineSeparate(text);
            if (text.Length > 0 && !messageNoTimestamp.Equals(_lastLogMessage))
            {
                _lastLogMessage = messageNoTimestamp;
                if (!LogTestFormat || memstampRegex.IsMatch(text[0]))
                    text[0] = GetDate() + text[0];
                else
                {
                    // Add memstamp with zeros
                    text[0] = GetDate() + "0\t0\t" + text[0];
                }
                LogVerbatim(text);
            }
        }

        public void LogError(params string[] text)
        {
            string errorText = string.Join(Environment.NewLine, text);
            errorText = string.Format(Resources.Logger_LogErrorToFile_ERROR___0_, errorText);
            Log(errorText);
        }

        private int LastPercent;
        private DateTime LastLogTime;
        private const int MIN_SECONDS_BETWEEN_LOGS = 4;
        private object _percentLock = new object();


        public void LogPercent(int percent)
        {
            lock (_percentLock)
            {
                if (percent < 0 || percent < LastPercent || percent == 100)
                    return;
                if ((DateTime.Now - LastLogTime) > new TimeSpan(0, 0, MIN_SECONDS_BETWEEN_LOGS) &&
                    percent != LastPercent)
                {
                    // do not log 0%, gives fast operations a chance to skip percent logging
                    if (percent == 0)
                    {
                        if (LastPercent == -2)
                        {
                            LastPercent = -1;
                            LastLogTime = DateTime.Now;
                        }

                        return;
                    }

                    LastLogTime = DateTime.Now;
                    Log(string.Format(Resources.Logger_LogPercent__0__, percent));
                    LastPercent = Math.Max(LastPercent, percent);
                }
            }
        }

        public void StopLogPercent(bool completed)
        {
            lock (_percentLock)
            {
                if (completed && LastPercent >= 0)
                    Log(string.Format(Resources.Logger_LogPercent__0__, 100));
                LastPercent = -2;
                LastLogTime = DateTime.MinValue;
            }
        }

        private string GetDate()
        {
            if (LogTestFormat)
                return "[" + DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + "]\t";
            return "[" + DateTime.Now.ToString("G") + "]    ";
        }

        public void DisplayLogFromFile()
        {
            if (!File.Exists(_filePath)) return;
            var startBlockRegex = new Regex(DATE_PATTERN);
            lock (_uiBufferLock)
            {
                _uiBuffer.Clear();
                using (var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
                {
                    using (var streamReader = new StreamReader(fileStream))
                    {
                        while (!streamReader.EndOfStream)
                        {
                            var line = streamReader.ReadLine();
                            if (line == null) continue;
                            if (startBlockRegex.IsMatch(line) || _uiBuffer.Count == 0) // add check for 0 len
                                _uiBuffer.Add(line);
                            else
                                _uiBuffer[_uiBuffer.Count - 1] += Environment.NewLine + line;
                        }
                    }
                    _lines = _uiBuffer.Count;
                    _uiBuffer.RemoveRange(0, Math.Max(0, _lines - MaxLogLines));
                }
            }
        }

        public bool OutputLog(UiLog logToUi, UiLogError logErrorToUi)
        {
            lock (_uiBufferLock)
            {
                var errorRegex = ErrorRegex();
                var errorOutput = new List<string>();
                var normalOutput = new List<string>();
                foreach (var textBlock in _uiBuffer)
                {
                    if (errorRegex.IsMatch(textBlock))
                    {
                        LogUiOutput(normalOutput, logToUi);
                        errorOutput.Add(textBlock);
                    }
                    else
                    {
                        LogUiErrorOutput(errorOutput, logErrorToUi);
                        normalOutput.Add(textBlock);
                    }
                }
                LogUiOutput(normalOutput, logToUi);
                LogUiErrorOutput(errorOutput, logErrorToUi);
                var logChanged = _uiBuffer.Count > 0;
                _uiBuffer.Clear();
                return logChanged;
            }
        }

        private void LogUiErrorOutput(List<string> errorOutput, UiLogError logErrorToUi)
        {
            if (errorOutput.Count == 0) return;
            logErrorToUi(TextUtil.LineSeparate(errorOutput));
            errorOutput.Clear();
        }

        private void LogUiOutput(List<string> normalOutput, UiLog logToUi)
        {
            if (normalOutput.Count == 0) return;
            logToUi(TextUtil.LineSeparate(normalOutput));
            normalOutput.Clear();
        }



        public Logger Archive()
        {
            lock (_fileLock)
            {
                if (File.Exists(_filePath) && new FileInfo(_filePath).Length > 0)
                {
                    var lastModified = File.GetLastWriteTime(_filePath);
                    var timestampFileName = Path.GetDirectoryName(_filePath) + "\\" + Path.GetFileNameWithoutExtension(_filePath);
                    timestampFileName += lastModified.ToString("_yyyyMMdd_HHmmss") + TextUtil.EXT_LOG;
                    Close();

                    File.Copy(_filePath, timestampFileName, true);
                    File.Delete(_filePath);
                    Init();
                    var newFilePath = Path.Combine(FileUtil.GetDirectory(_filePath), timestampFileName);
                    return new Logger(newFilePath, timestampFileName, true);
                }

                return null;
            }
        }

        public void Close()
        {
            _fileStream.Close();
        }

        public void Delete()
        {
            try
            {
                Close();
            }
            catch (ObjectDisposedException)
            {
                // pass - file already closed
            }
            File.Delete(_filePath);
        }

        private void WriteToFile(string message)
        {
            // This should be an uncontested lock unless there is a thread in DisplayLog (which displays the log contents in the UI)
            // In this case we want to wait for that to finish before updating the log.
            lock (_lock)
            {
                try
                {
                    BackupLog();
                }
                catch (Exception e)
                {
                    var err = new StringBuilder(Resources.Logger_WriteToFile_Error_occurred_while_trying_to_backup_log_file__).AppendLine(_filePath);
                    err.AppendLine(Resources.Logger_WriteToFile_Exception_stack_trace__);
                    ProgramLog.Error(err.ToString(), e);
                }

                //var dateAndMessage = GetDate() + message;

                try
                {

                    using (var fileStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        using (var streamWriter =
                            new StreamWriter(fileStream, Encoding.UTF8, StreamDefaultBufferSize, true))
                        {
                            if (_logBuffer != null && _logBuffer.Length > 0)
                            {
                                // Append any log messages that were buffered while the log file was unavailable (e.g. due to network share being temporarily unavailable).
                                streamWriter.Write(_logBuffer.ToString());
                                streamWriter.Flush();
                                _logBuffer.Clear();
                            }
                            streamWriter.WriteLine(message);
                            streamWriter.Flush();
                        }

                    }

                    // Save log message in memory
                    if (_memLogMessages.Count == MemLogSize)
                    {
                        _memLogMessages.Dequeue();
                    }
                    _memLogMessages.Enqueue(message);
                }
                catch (Exception e)
                {
                    // If we cannot access the log file at this time, write to the buffer and the program log
                    WriteToBuffer(message);

                    var fileNotFound = e.GetType().IsAssignableFrom(typeof(FileNotFoundException));
                    if (!fileNotFound)
                    {
                        WriteToBuffer(string.Format(Resources.Logger_WriteToFile_ERROR_writing_to_the_log_file___0___Check_program_log_for_details___1_, e.Message, ProgramLog.GetProgramLogFilePath()));
                    }

                    ProgramLog.Error(string.Format(Resources.Logger_WriteToFile_Error_occurred_writing_to_log_file___0___Attempted_to_write_, _filePath));
                    ProgramLog.Error(message);
                    if (!fileNotFound)
                    {
                        ProgramLog.Error(Resources.Logger_WriteToFile_Exception_stack_trace_, e);
                    }
                    else
                    {
                        ProgramLog.Error(string.Format(Resources.Logger_WriteToFile_Error_message_was__0__, e.Message));
                    }
                }
            }
        }

        private void WriteToBuffer(string message)
        {
            if (_logBuffer.Length > LogBufferSize)
            {
                return;
            }
            _logBuffer.AppendLine(message);

            if (_logBuffer.Length > LogBufferSize)
            {
                _logBuffer.AppendLine(Resources.Logger_WriteToBuffer_____LOG_BUFFER_IS_FULL____);
            }
        }

        private void BackupLog()
        {
            if (!File.Exists(_filePath))
            {
                // Maybe the log file is on a mapped network drive and we have lost connection
                return;
            }

            var size = new FileInfo(_filePath).Length;
            if (size >= MaxLogSize)
            {
                Close();  // First close the open file handle
                BackupLog(_filePath, 1);
                using (File.Create(_filePath))
                {
                }
                _fileStream = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            }
        }

        private void BackupLog(string filePath, int bkupIndex)
        {
            if (bkupIndex > MaxBackups)
            {
                File.Delete(GetLogFilePath(filePath, bkupIndex - 1));
                return;
            }
            var backupFile = GetLogFilePath(filePath, bkupIndex);
            if (File.Exists(backupFile))
            {
                BackupLog(filePath, bkupIndex + 1);
            }

            var startFile = GetLogFilePath(filePath, bkupIndex - 1);
            File.Move(startFile, backupFile);
        }
        private static string GetLogFilePath(string filePath, int index)
        {
            return index == 0 ? filePath : filePath + "." + index;
        }

    }
}
