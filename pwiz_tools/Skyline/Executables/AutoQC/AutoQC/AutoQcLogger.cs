using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AutoQC
{
    public interface IAutoQcLogger
    {
        void Log(string message, params object[] args);
        void LogError(string message, params object[] args);
        void LogException(Exception exception);
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
        private bool _readingLog;

        private IMainUiControl _mainUi;

        public const string LogTruncatedMessage = "... Log truncated ... Full log is in {0}";

        public AutoQcLogger(string filePath)
        {
            _filePath = filePath;
        }

        private void WriteToFile(string message)
        {
            while (_readingLog)
            {
                // Wait if the file is being read to display in the log tab. This should not take very long.
                Thread.Sleep(1000);     
            }

            BackupLog();

            using (var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                using (var writer = new StreamWriter(fs))
                {
                    writer.WriteLine(message);
                }
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

        private void LogError(string message)
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

            var dateAndLine = GetDate() + line;
            if (_mainUi != null)
            {
                _mainUi.LogToUi(dateAndLine);
            }

            WriteToFile(dateAndLine);
        }

        public void LogException(Exception ex)
        {
            LogError(ex.Message);
            if (_mainUi != null)
            {
                _mainUi.LogErrorToUi(ex.StackTrace);
            }
            WriteToFile(ex.StackTrace);
        }

        public void LogError(string line, params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                line = string.Format(line, args);
            }

            if (_mainUi != null)
            {
                _mainUi.LogErrorToUi(line);
            }

            LogError(line);
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
            _readingLog = true;
            
            try
            {       
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

                foreach (var line in lines)
                {
                    var error = line.ToLower().Contains("error");
                    if (error)
                    {
                        _mainUi.LogErrorToUi(line,
                            false, // don't scroll to end
                            false); // don't truncate
                    }
                    else
                    {
                        _mainUi.LogToUi(line,
                            false, // don't scroll to end
                            false); // don't truncate
                    }
                }
            }
            finally
            {
                _readingLog = false;  
            }
        }

        #endregion
    }
}
