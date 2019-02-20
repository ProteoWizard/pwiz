using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AutoQC
{
    public interface IAutoQcLogger
    {
        void Log(string message, params object[] args);
        void LogError(string message, params object[] args);
        void LogProgramError(string message, params object[] args);
        void LogException(Exception exception, string message, params object[] args);
        string GetFile();
        void DisableUiLogging();
        void LogToUi(IMainUiControl mainUi);
        void DisplayLog();
    }

    public class AutoQcLogger : IAutoQcLogger
    {
        public const long MaxLogSize = 10 * 1024 * 1024; // 10MB
        private const int _maxBackups = 5;
        public const int MaxLogLines = 5000;

        private string _lastMessage = string.Empty; // To avoid logging duplicate messages.

        private readonly string _filePath;
        private readonly string _configName;
        private readonly object _lock = new object();

        private IMainUiControl _mainUi;

        public const string LogTruncatedMessage = "... Log truncated ... Full log is in {0}";

        private Queue<string> _memLogMessages;
        private const int MEM_LOG_SIZE = 100; // Keep the last 100 log messages in memory
        private StringBuilder _logBuffer; // To be used when the log file is unavailable for writing
        private const int LOG_BUFFER_SIZE = 10240;

        public AutoQcLogger(string filePath, string configName)
        {
            _filePath = filePath;
            _configName = configName;
        }

        public void Init()
        {
            _logBuffer = new StringBuilder();
            _memLogMessages = new Queue<string>(MEM_LOG_SIZE);

            // Initialize - create the log file if it does not exist
            if (File.Exists(_filePath)) return;
            using (File.Create(_filePath))
            {
            } 
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
                    var err = new StringBuilder("Error occurred while trying to backup log file: ").AppendLine(_filePath);
                    err.AppendLine("Exception stack trace: ");
                    Program.LogError(_configName, err.ToString(), e);
                }

                var dateAndMessage = GetDate() + message;

                try
                {
                    using (var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        using (var writer = new StreamWriter(fs))
                        {
                            if (_logBuffer != null && _logBuffer.Length > 0)
                            {
                                // Append any log messages that were buffered while the log file was unavailable (e.g. due to network share being temporarily unavailable).
                                writer.Write(_logBuffer.ToString());
                                _logBuffer.Clear();
                            }
                            writer.WriteLine(dateAndMessage);
                        }
                    }

                    // Save log message in memory
                    if (_memLogMessages.Count == MEM_LOG_SIZE)
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
                        WriteToBuffer($"ERROR writing to the log file: {e.Message}. Check program log for details: {Program.GetProgramLogFilePath()}");
                    }
                    
                    Program.LogError(_configName, $"Error occurred writing to log file: {_filePath}. Attempted to write:");
                    Program.LogError(_configName, message);
                    if (!fileNotFound)
                    {
                        Program.LogError(_configName, "Exception stack trace:", e);
                    }
                    else
                    {
                        Program.LogError(_configName, $"Error message was {e.Message}.");
                    }
                }
            }
        }

        private void WriteToBuffer(string message)
        {
            if (_logBuffer.Length > LOG_BUFFER_SIZE)
            {
                return;
            }
            _logBuffer.AppendLine(message);

            if (_logBuffer.Length > LOG_BUFFER_SIZE)
            {
                _logBuffer.AppendLine("!!! LOG BUFFER IS FULL !!!");
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
            if (bkupIndex > _maxBackups)
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
            message = "ERROR: " + message;
            WriteToFile(message);
        }


        #region [Logging methods; Implementation of IAutoQcLogger interface]

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

            var exStr = ex != null ? ex.ToString() : "";
            if (_mainUi != null)
            {
                line = GetDate() + line;

                _mainUi.LogErrorLinesToUi(
                        new List<string> {line, exStr});
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

        public string GetFile()
        {
            return _filePath;
        }

        public void DisableUiLogging()
        {
            _mainUi = null;
        }

        public void LogToUi(IMainUiControl mainUi)
        {
            _mainUi = mainUi;
        }

        public void DisplayLog()
        {
            lock (_lock)
            {
                if (!File.Exists(_filePath))
                {
                    // If the log file is not accessible, display the contents of the in memory buffer and anything saved in the log buffer
                    _mainUi.LogErrorToUi($"Could not read the log file: {_filePath}. File does not exist", false, false);
                    if (_memLogMessages != null && _memLogMessages.Count > 0)
                    {
                        _mainUi.LogErrorToUi($"Displaying last {_memLogMessages.Count} saved log messages", false, false);
                        string[] arr = _memLogMessages.ToArray();
                        foreach (var s in arr)
                        {
                            _mainUi.LogToUi(s, false, false);
                        }
                    }
                    
                    if (_logBuffer != null &&_logBuffer.Length > 0)
                    {
                        _mainUi.LogErrorToUi($"Displaying messages since log file became unavailable", false, false);
                        _mainUi.LogToUi(_logBuffer.ToString(), false, false);
                    }
                    return;
                }

                // Read the log contents and display in the log tab.
                var lines = new List<string>();
                var truncated = false;
                using (
                    var reader =
                        new StreamReader(new FileStream(GetFile(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    )
                {
                    var maxDisplaySize = MaxLogSize/20;
                    // If the log is too big don't display all of it.
                    if (reader.BaseStream.Length > maxDisplaySize)
                    {
                        reader.BaseStream.Seek(-maxDisplaySize, SeekOrigin.End);
                        truncated = true;
                    }
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }

                    if (lines.Count > MaxLogLines)
                    {
                        lines = lines.GetRange(lines.Count - MaxLogLines - 1, MaxLogLines);
                        truncated = true;
                    }
                }

                if (truncated)
                {
                    _mainUi.LogErrorToUi(string.Format(LogTruncatedMessage, GetFile()), false);  
                }

                var toLog = new List<string>();
                var lastLineErr = false;

                foreach (var line in lines)
                {
                    var error = line.ToLower().Contains("error");
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
