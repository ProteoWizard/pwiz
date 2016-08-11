using System;
using System.IO;

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
    }

    public class AutoQcLogger : IAutoQcLogger
    {
        private const long _maxFileSize = 10 * 1024 * 1024;
        private const int _maxBackups = 5;

        private string _lastMessage = string.Empty; // To avoid logging duplicate messages.

        private readonly string _filePath;

        private IMainUiControl _mainUi;

        public AutoQcLogger(string filePath)
        {
            _filePath = filePath;
        }

        private void WriteToFile(string message)
        {
            BackupLog();

            using (var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write("[" + DateTime.Now.ToString("G") + "]    ");
                    writer.WriteLine(message);
                }
            }
        }

        private void BackupLog()
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var size = new FileInfo(_filePath).Length;
            if (size >= _maxFileSize)
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

            if (_mainUi != null)
            {
                _mainUi.LogToUi(line);
            }

            WriteToFile(line);
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

        #endregion
    }
}
