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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SharedBatch.Properties;

 namespace SharedBatch
{

    public class Logger
    {

        private static HashSet<Regex> _errorFormats;

        //public static string LOG_FOLDER;

        public const long MaxLogSize = 10 * 1024 * 1024; // 10MB
        private const int MaxBackups = 5;
        public const int MaxLogLines = 10000;
        public static string LogTruncatedMessage = Resources.Logger_DisplayLog_____Log_truncated_____Full_log_is_in__0_;


        private string _lastMessage = string.Empty; // To avoid logging duplicate messages.

        private readonly string _filePath;
        public readonly string Name;

        private readonly object _lock = new object();

        private IMainUiControl _mainUi;
        
        private StreamReader _streamReader;
        private StreamWriter _streamWriter;

        private Queue<string> _memLogMessages;
        private const int MemLogSize = 100; // Keep the last 100 log messages in memory
        private StringBuilder _logBuffer; // To be used when the log file is unavailable for writing
        private const int LogBufferSize = 10240;
        private const int StreamReaderDefaultBufferSize = 4096;
        
        public Logger(string logFilePath, string logName, IMainUiControl mainUi = null):this(logFilePath, logName, true, mainUi)
        {
        }

        public Logger(string logFilePath, string logName, bool init, IMainUiControl mainUi = null)
        {
            if (_errorFormats == null)
            {
                _errorFormats = new HashSet<Regex>();
                AddErrorMatch(string.Format(Resources.Logger_LogErrorToFile_ERROR___0_, ".*"));
            }

            _filePath = logFilePath;
            _mainUi = mainUi;
            Name = logName;

            if (init)
            {
                var logFolder = FileUtil.GetDirectory(logFilePath);
                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }

                Init();
            }
        }


        public void Init()
        {
            _logBuffer = new StringBuilder();
            _memLogMessages = new Queue<string>(MemLogSize);
            
            // Initialize - create blank log file if doesn't exist
            if (!File.Exists(_filePath))
            {
                using (File.Create(_filePath))
                {
                }
            }

            var logFileRead = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var logFileWrite = File.Open(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            // these need to be kept open while the program is running so log files can't be deleted outside of Skyline Batch
            _streamReader = new StreamReader(logFileRead, Encoding.Default, false, 
                StreamReaderDefaultBufferSize, true);
            _streamWriter = new StreamWriter(logFileWrite, Encoding.Default, StreamReaderDefaultBufferSize, true);
        }

        public void AddErrorMatch(string errorRegex)
        {
            var dateRegex = "[].*[] *";
            _errorFormats.Add(new Regex(dateRegex + errorRegex));
        }

        public string GetDirectory()
        {
            return Path.GetDirectoryName(_filePath);
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

                var dateAndMessage = GetDate() + message;

                try
                {
                    if (_logBuffer != null && _logBuffer.Length > 0)
                    {
                        // Append any log messages that were buffered while the log file was unavailable (e.g. due to network share being temporarily unavailable).
                        _streamWriter.Write(_logBuffer.ToString());
                        _streamWriter.Flush();
                        _logBuffer.Clear();
                    }
                    _streamWriter.WriteLine(dateAndMessage);
                    _streamWriter.Flush();

                    // Save log message in memory
                    if (_memLogMessages.Count == MemLogSize)
                    {
                        _memLogMessages.Dequeue();
                    }
                    _memLogMessages.Enqueue(dateAndMessage);
                }
                catch (Exception e)
                {
                    // If we cannot access the log file at this time, write to the buffer and the program log
                    WriteToBuffer(dateAndMessage);

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

        private static string GetDate()
        {
            return "[" + DateTime.Now.ToString("G") + "]    ";
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
                BackupLog(_filePath, 1);
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

        private void LogErrorToFile(string message)
        {
            WriteToFile(message);
        }


        #region [Logging methods; Implementation of ISkylineBatchLogger interface]

        public void Log(string line, params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                line = string.Format(line, args);
            }

            if (line.Equals(_lastMessage))
            {
                return;
            }
            _lastMessage = line;

            if (_mainUi != null)
            {
                _mainUi.LogToUi(Name, GetDate() + line);
            }

            WriteToFile(line);
        }

        public void LogException(Exception ex, string line, params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                line = string.Format(line, args);
            }
            line = string.Format(Resources.Logger_LogErrorToFile_ERROR___0_, line);

            var exStr = ex != null ? ex.ToString() : string.Empty;
            if (_mainUi != null)
            {
                _mainUi.LogErrorLinesToUi(Name,
                        new List<string> { GetDate() + line, exStr });
            }

            LogErrorToFile(string.Format("{0}\n{1}", line, exStr));
        }

        public void LogError(string line, params object[] args)
        {
            if (args != null && args.Length > 0)
                line = string.Format(line, args);
            if (line.StartsWith(Resources.Logger_LogError_Error_, StringComparison.CurrentCultureIgnoreCase))
                line = line.Substring(Resources.Logger_LogError_Error_.Length);
            line = string.Format(Resources.Logger_LogErrorToFile_ERROR___0_, line);
            LogErrorText(line);
        }

        public void LogErrorNoPrefix(string line, params object[] args)
        {
            if (args != null && args.Length > 0)
                line = string.Format(line, args);
            LogErrorText(line);
        }

        private void LogErrorText(string line)
        {
            if (_mainUi != null)
            {
                _mainUi.LogErrorToUi(Name,GetDate() + line);
            }
            LogErrorToFile(line);
        }

        public void LogProgramError(string line, params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                line = string.Format(line, args);
            }

            ProgramLog.Error(line);

            if (_mainUi != null)
            {
                _mainUi.LogErrorToUi(Name, GetDate() + line);
            }
        }
        
        private int LastPercent;
        private DateTime LastLogTime;
        private CancellationTokenSource cancelToken;
        private bool logging;
        private const int MIN_SECONDS_BETWEEN_LOGS = 4;


        public void LogPercent(int percent)
        {
            if (percent < 0)
            {
                LastPercent = percent;
            }
            if ((DateTime.Now - LastLogTime) > new TimeSpan(0, 0, MIN_SECONDS_BETWEEN_LOGS) &&
                percent != LastPercent)
            {
                if (percent == 0 && LastPercent < 0)
                {
                    LastLogTime = DateTime.Now;
                    LastPercent = 0;
                }
                else if (percent == 100)
                {
                    if (LastPercent != 0) Log(string.Format(Resources.Logger_LogPercent__0__, percent));
                    LastPercent = -1;
                }
                else
                {
                    LastLogTime = DateTime.Now;
                    Log(string.Format(Resources.Logger_LogPercent__0__, percent));
                }

            }
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

        public Logger Archive()
        {
            if (new FileInfo(_filePath).Length > 0)
            {
                var lastModified = File.GetLastWriteTime(_filePath);
                var timestampFileName = Path.GetDirectoryName(_filePath) + "\\" + Path.GetFileNameWithoutExtension(_filePath);
                timestampFileName += lastModified.ToString("_yyyyMMdd_HHmmss") + ".log";
                Close();

                File.Copy(GetFile(), timestampFileName);
                File.Delete(_filePath);
                Init();
                var newFilePath = Path.Combine(FileUtil.GetDirectory(_filePath), timestampFileName);
                return new Logger(newFilePath, timestampFileName, _mainUi);
            }

            return null;
        }

        public void LogToUi(IMainUiControl mainUi)
        {
            _mainUi = mainUi;
        }

        public void Close()
        {
            _streamWriter.Close();
            _streamWriter.BaseStream.Dispose();
            _streamReader.Close();
            _streamReader.BaseStream.Dispose();
        }

        public string GetFile()
        {
            return _filePath;
        }

        public string GetFileName()
        {
            return Path.GetFileName(_filePath);
        }

        public void DisableUiLogging()
        {
            _mainUi = null;
        }

        public void DisplayLog()
        {
            lock (_lock)
            {
                if (!File.Exists(_filePath))
                {
                    // If the log file is not accessible, display the contents of the in memory buffer and anything saved in the log buffer
                    _mainUi.LogErrorToUi(_filePath, string.Format(Resources.Logger_DisplayLog_Could_not_read_the_log_file___0___File_does_not_exist_, _filePath), false);
                    if (_memLogMessages != null && _memLogMessages.Count > 0)
                    {
                        _mainUi.LogErrorToUi(_filePath, string.Format(Resources.Logger_DisplayLog_Displaying_last__0__saved_log_messages_, _memLogMessages.Count), false);
                        string[] arr = _memLogMessages.ToArray();
                        foreach (var s in arr)
                        {
                            _mainUi.LogToUi(_filePath,s, false);
                        }
                    }

                    if (_logBuffer != null && _logBuffer.Length > 0)
                    {
                        _mainUi.LogErrorToUi(_filePath, Resources.Logger_DisplayLog_Displaying_messages_since_log_file_became_unavailable_, false);
                        _mainUi.LogToUi(_filePath, _logBuffer.ToString(), false);
                    }
                    return;
                }

                // Reset the stream reader to start from the beginning of the file
                _streamReader.Close();
                _streamReader.BaseStream.Dispose();
                var logFileRead = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                _streamReader = new StreamReader(logFileRead, Encoding.Default, false, 
                    StreamReaderDefaultBufferSize, true);
                
                // Read the log contents and display in the log tab.
                var lines = new List<string>();
                var truncated = false;
                var maxDisplaySize = MaxLogSize / 20;
                // If the log is too big don't display all of it.
                if (_streamReader.BaseStream.Length > maxDisplaySize)
                {
                    _streamReader.BaseStream.Seek(-maxDisplaySize, SeekOrigin.End);
                    truncated = true;
                }
                string lineText;
                while ((lineText = _streamReader.ReadLine()) != null)
                {
                    lines.Add(lineText);
                }

                if (lines.Count > MaxLogLines)
                {
                    lines = lines.GetRange(lines.Count - MaxLogLines - 1, MaxLogLines);
                    truncated = true;
                }
                if (truncated)
                {
                    _mainUi.LogErrorToUi(_filePath, string.Format(Resources.Logger_DisplayLog_____Log_truncated_____Full_log_is_in__0_, GetFile()), false);
                }

                var toLog = new List<string>();
                var lastLineErr = false;

                foreach (var line in lines)
                {
                    var isError = false;
                    foreach(var errorFormat in _errorFormats)
                        if (errorFormat.IsMatch(line))
                            isError = true;
                    if (isError)
                    {
                        if (!lastLineErr && toLog.Count > 0)
                        {
                            _mainUi.LogLinesToUi(Name, toLog);
                            toLog.Clear();
                        }
                        lastLineErr = true;
                    }
                    // check that input starts with timestamp, accounts for multi-line errors
                    else if (line.Length > 0 && line[0] == '[')
                    {
                        if (lastLineErr && toLog.Count > 0)
                        {
                            _mainUi.LogErrorLinesToUi(Name, toLog);
                            toLog.Clear();
                        }
                        lastLineErr = false;
                    }
                    toLog.Add(line);
                }
                if (toLog.Count > 0)
                {
                    if (lastLineErr)
                    {
                        _mainUi.LogErrorLinesToUi(Name, toLog);
                    }
                    else
                    {
                        _mainUi.LogLinesToUi(Name, toLog);
                    }
                }
            }
        }

        #endregion
    }
}

