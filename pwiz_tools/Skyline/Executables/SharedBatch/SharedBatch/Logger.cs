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
using SharedBatch.Properties;

 namespace SharedBatch
{

    public class Logger
    {
        private const string DATE_PATTERN = "^\\[.*\\]\x20*";


        private readonly string _filePath;

        public const int MaxLogLines = 10000;

        private int _lines;

        private static List<string> _errorFormats;
        private readonly List<string> _uiBuffer;

        private readonly object _uiBufferLock = new object();

        private readonly object _fileLock = new object();

        private FileStream _fileStream;
        private const int StreamDefaultBufferSize = 4096;



        public Logger(string logFilePath, string logName)
        {
            var logFolder = FileUtil.GetDirectory(logFilePath);
            Directory.CreateDirectory(logFolder);

            InitializeErrorFormats();

            _filePath = logFilePath;
            _uiBuffer = new List<string>();
            Name = logName;
            Init();
        }

        public string Name;

        public delegate void UiLog(string text);

        public delegate void UiLogError(string text);

        public string LogFile => _filePath;

        public string LogFileName => Path.GetFileName(_filePath);

        public bool WillTruncate => _lines > MaxLogLines;

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
            var fullMessage = string.Join(Environment.NewLine, text);
            if (fullMessage.Equals(_lastLogMessage)) return;
            _lastLogMessage = fullMessage;
            lock (_fileLock)
            {
                using (var fileStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    using (var streamWriter =
                        new StreamWriter(fileStream, Encoding.UTF8, StreamDefaultBufferSize, true))
                    {
                        foreach (var line in text)
                        {
                            streamWriter.WriteLine(line);
                            streamWriter.Flush();
                        }
                    }

                }
            }
            lock (_uiBufferLock)
            {
                _uiBuffer.AddRange(text);
            }
            _lines += text.Length;
        }

        public void Log(params string[] text)
        {
            if (text.Length > 0)
            {
                text[0] = GetDate() + text[0];
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

        private static string GetDate()
        {
            return "[" + DateTime.Now.ToString("G") + "]    ";
        }

        public void DisplayLogFromFile()
        {
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
                            if (startBlockRegex.IsMatch(line)) // add check for 0 len
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

                    File.Copy(_filePath, timestampFileName);
                    File.Delete(_filePath);
                    Init();
                    var newFilePath = Path.Combine(FileUtil.GetDirectory(_filePath), timestampFileName);
                    return new Logger(newFilePath, timestampFileName);
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

    }
}
