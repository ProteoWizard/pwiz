﻿ /*
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
using SkylineBatch.Properties;

 namespace SkylineBatch
{
    public interface ISkylineBatchLogger
    {
        void Log(string message, params object[] args);
        void LogError(string message, params object[] args);
        void LogProgramError(string message, params object[] args);
        void LogException(Exception exception, string message, params object[] args);
        string GetFile();
        string GetFileName();
        void Delete();
        SkylineBatchLogger Archive();
        void DisplayLog();
    }

    public class SkylineBatchLogger : ISkylineBatchLogger
    {
        public static string LOG_FOLDER;

        public const long MaxLogSize = 10 * 1024 * 1024; // 10MB
        private const int MaxBackups = 5;
        public const int MaxLogLines = 5000;
        
        private string _lastMessage = string.Empty; // To avoid logging duplicate messages.

        private readonly string _filePath;

        private readonly object _lock = new object();

        private IMainUiControl _mainUi;
        
        private StreamReader _streamReader;
        private StreamWriter _streamWriter;

        private Queue<string> _memLogMessages;
        private const int MemLogSize = 100; // Keep the last 100 log messages in memory
        private StringBuilder _logBuffer; // To be used when the log file is unavailable for writing
        private const int LogBufferSize = 10240;
        private const int StreamReaderDefaultBufferSize = 4096;

        public SkylineBatchLogger(string logFileName, IMainUiControl mainUi = null)
        {
            if (LOG_FOLDER == null)
            {
                var roamingFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var localFolder = Path.Combine(Path.GetDirectoryName(roamingFolder), "local");
                LOG_FOLDER = Path.Combine(localFolder, Program.AppName());
                if (!Directory.Exists(LOG_FOLDER))
                {
                    Directory.CreateDirectory(LOG_FOLDER);
                }
            }
            _filePath = Path.Combine(LOG_FOLDER, logFileName);
            _mainUi = mainUi;
            Init();
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
                    var err = new StringBuilder(Resources.SkylineBatchLogger_WriteToFile_Error_occurred_while_trying_to_backup_log_file__).AppendLine(_filePath);
                    err.AppendLine(Resources.SkylineBatchLogger_WriteToFile_Exception_stack_trace__);
                    Program.LogError(err.ToString(), e);
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
                        WriteToBuffer(string.Format(Resources.SkylineBatchLogger_WriteToFile_ERROR_writing_to_the_log_file___0___Check_program_log_for_details___1_, e.Message, Program.GetProgramLogFilePath()));
                    }

                    Program.LogError(string.Format(Resources.SkylineBatchLogger_WriteToFile_Error_occurred_writing_to_log_file___0___Attempted_to_write_, _filePath));
                    Program.LogError(message);
                    if (!fileNotFound)
                    {
                        Program.LogError(Resources.SkylineBatchLogger_WriteToFile_Exception_stack_trace_, e);
                    }
                    else
                    {
                        Program.LogError(string.Format(Resources.SkylineBatchLogger_WriteToFile_Error_message_was__0__, e.Message));
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
                _logBuffer.AppendLine(Resources.SkylineBatchLogger_WriteToBuffer_____LOG_BUFFER_IS_FULL____);
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
            message = string.Format(Resources.SkylineBatchLogger_LogErrorToFile_ERROR___0_, message);
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
                _mainUi.LogToUi(GetDate() + line);
            }

            WriteToFile(line);
        }

        public void LogException(Exception ex, string line, params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                line = string.Format(line, args);
            }

            var exStr = ex != null ? ex.ToString() : string.Empty;
            if (_mainUi != null)
            {
                line = GetDate() + line;

                _mainUi.LogErrorLinesToUi(
                        new List<string> { line, exStr });
            }

            LogErrorToFile(string.Format("{0}\n{1}", line, exStr));
        }

        public void LogError(string line, params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                line = string.Format(line, args);
            }

            if (_mainUi != null)
            {
                _mainUi.LogErrorToUi(GetDate() + line);
            }

            LogErrorToFile(line);
        }

        public void LogProgramError(string line, params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                line = string.Format(line, args);
            }

            Program.LogError(line);

            if (_mainUi != null)
            {
                _mainUi.LogErrorToUi(GetDate() + line);
            }
        }

        public void Delete()
        {
            CloseFileStreams();
            File.Delete(_filePath);
        }

        public SkylineBatchLogger Archive()
        {
            if (new FileInfo(_filePath).Length > 0)
            {
                if (string.IsNullOrEmpty(LOG_FOLDER)) LOG_FOLDER = Path.GetDirectoryName(_filePath);
                var lastModified = File.GetLastWriteTime(_filePath);
                var timestampFileName = Path.GetDirectoryName(_filePath) + "\\" + Path.GetFileNameWithoutExtension(_filePath);
                timestampFileName += lastModified.ToString("_yyyyMMdd_HHmmss") + ".log";
                CloseFileStreams();

                File.Copy(GetFile(), timestampFileName);
                File.Delete(_filePath);
                Init();
                return new SkylineBatchLogger(timestampFileName, _mainUi);
            }

            return null;
        }

        private void CloseFileStreams()
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
                    _mainUi.LogErrorToUi(string.Format(Resources.SkylineBatchLogger_DisplayLog_Could_not_read_the_log_file___0___File_does_not_exist_, _filePath), false, false);
                    if (_memLogMessages != null && _memLogMessages.Count > 0)
                    {
                        _mainUi.LogErrorToUi(string.Format(Resources.SkylineBatchLogger_DisplayLog_Displaying_last__0__saved_log_messages_, _memLogMessages.Count), false, false);
                        string[] arr = _memLogMessages.ToArray();
                        foreach (var s in arr)
                        {
                            _mainUi.LogToUi(s, false, false);
                        }
                    }

                    if (_logBuffer != null && _logBuffer.Length > 0)
                    {
                        _mainUi.LogErrorToUi(Resources.SkylineBatchLogger_DisplayLog_Displaying_messages_since_log_file_became_unavailable_, false, false);
                        _mainUi.LogToUi(_logBuffer.ToString(), false, false);
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
                    _mainUi.LogErrorToUi(string.Format(Resources.SkylineBatchLogger_DisplayLog_____Log_truncated_____Full_log_is_in__0_, GetFile()), false);
                }

                var toLog = new List<string>();
                var lastLineErr = false;

                foreach (var line in lines)
                {
                    // CONSIDER: Find a different way to determine errors
                    var error = line.Contains("Fatal error: ") || line.Contains("Error: ");
                    if (error)
                    {
                        if (!lastLineErr && toLog.Count > 0)
                        {
                            _mainUi.LogLinesToUi(toLog);
                            toLog.Clear();
                        }
                        lastLineErr = true;
                        toLog.Add(line);
                    }
                    else
                    {
                        if (lastLineErr && toLog.Count > 0)
                        {
                            _mainUi.LogErrorLinesToUi(toLog);
                            toLog.Clear();
                        }
                        lastLineErr = false;
                        toLog.Add(line);
                    }
                }
                if (toLog.Count > 0)
                {
                    if (lastLineErr)
                    {
                        _mainUi.LogErrorLinesToUi(toLog);
                    }
                    else
                    {
                        _mainUi.LogLinesToUi(toLog);
                    }
                }
            }
        }

        #endregion
    }
}

