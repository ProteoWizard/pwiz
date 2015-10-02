/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace SkylineTester
{
    public class CommandShell : RichTextBox
    {
        public string DefaultDirectory { get; set; }
        public Button StopButton { get; set; }
        public Func<string, bool> FilterFunc { get; set; }
        public Action<string> ColorLine { get; set; }
        public Action FinishedOneCommand { get; set; }
        public int NextCommand { get; set; }
        public readonly object LogLock = new object();

        private string _workingDirectory;
        private readonly List<string> _commands = new List<string>();
        private Action<bool> _doneAction;
        private readonly StringBuilder _logBuffer = new StringBuilder();
        private bool _logEmpty;
        private Process _process;
        private Timer _outputTimer;

        #region Add/run commands

        /// <summary>
        /// Add a command and arguments to be executed by the command shell by the Run
        /// command.  You can also add a comment line starting with '#'.  Multiple Add
        /// calls can be made to create a script of commands to be executed by Run.
        /// </summary>
        public int Add(string command, params object[] args)
        {
            _commands.Add(command.With(args));
            return _commands.Count - 1;
        }

        public void AddImmediate(string command, params object[] args)
        {
            Log(command.With(args) + Environment.NewLine);
            UpdateLog();
        }

        public void ClearLog()
        {
            lock (_logBuffer)
            {
                _logBuffer.Clear();
                _logEmpty = true;
            }

            Clear();
        }

        /// <summary>
        /// Run commands accumulated by one or more calls to Add.  An optional
        /// argument specifies a method to call when the commands are done
        /// (through successful completion, abort due to error, or abort due to
        /// user interrupt request).
        /// </summary>
        public void Run(Action<bool> doneAction)
        {
            _doneAction = doneAction;
            NextCommand = 0;

            _outputTimer = new Timer { Interval = 1000 };
            _outputTimer.Tick += UpdateLog;
            _outputTimer.Start();

            RunNext();
        }

        private void RunUI(Action action)
        {
            try
            {
                Invoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Run the next command in the script on the main thread.
        /// </summary>
        private void RunNext(object sender, EventArgs e)
        {
            RunUI(RunNext);
        }

        // Called on UI thread.
        private void RunNext()
        {
            UpdateLog();

            if (NextCommand > 0 && FinishedOneCommand != null)
                FinishedOneCommand();

            while (NextCommand < _commands.Count)
            {
                var line = _commands[NextCommand++];

                // Handle comment line.
                if (line.StartsWith("#"))
                {
                    Log(Environment.NewLine + line);
                    continue;
                }

                // Execute a command.
                Log("> " + line);
                UpdateLog();

                // Break apart arguments on space boundaries, but allow
                // quoted arguments to contain spaces.
                var parts = line.Split(' ');
                var words = new List<string>();
                for (int i = 0; i < parts.Length; i++)
                {
                    var word = parts[i];
                    if (word.StartsWith("\"") && !word.EndsWith("\""))
                    {
                        while (++i < parts.Length)
                        {
                            word += " " + parts[i];
                            if (parts[i].EndsWith("\""))
                                break;
                        }
                    }
                    words.Add(word);
                }

                var command = words[0].Trim('"');
                words.RemoveAt(0);
                var args = String.Join(" ", words);

                // Specify a working directory other than the default.
                if (command == "cd")
                {
                    _workingDirectory = words[0].Trim('"');
                }

                // Remove a directory.
                else if (command == "rmdir")
                {
                    if (words[0] == "/s")
                        words.RemoveAt(0);
                    var deleteDir = words[0].Trim('"');
                    if (Directory.Exists(deleteDir))
                    {
                        using (var deleteWindow = new DeleteWindow(deleteDir))
                        {
                            deleteWindow.ShowDialog();
                        }
                        if (Directory.Exists(deleteDir))
                        {
                            CommandsDone(false);
                            return;
                        }
                    }
                }

                // Run a command in a separate process.
                else
                {
                    try
                    {
                        StartProcess(
                            command,
                            args,
                            _workingDirectory);
                    }
                    catch (Exception e)
                    {
                        Log(Environment.NewLine + "!!!! COMMAND FAILED !!!! " + e);
                    }
                    _workingDirectory = DefaultDirectory;
                    return;
                }
            }

            CommandsDone(true);
        }

        /// <summary>
        /// Handle completion of commands, either by successfully finishing,
        /// or due to error or user interrupt.  Run on UI thread.
        /// </summary>
        private void CommandsDone(bool success)
        {
            if (!success)
                Log("# Stopped " + DateTime.Now.ToString("f") + Environment.NewLine + Environment.NewLine);
            UpdateLog();
            _commands.Clear();
            _doneAction(success);
        }

        // Run on UI thread.
        public void Done(bool success)
        {
            FinishLog();
            _process = null;
        }
        #endregion

        #region Manage processes

        /// <summary>
        /// Create a new process, but don't start it yet.
        /// </summary>
        private Process CreateProcess(string fileName, string workingDirectory = null)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            if (workingDirectory != null)
                process.StartInfo.WorkingDirectory = workingDirectory;

            return process;
        }

        /// <summary>
        /// Start a new process.
        /// </summary>
        /// <param name="exe">Path to executable.</param>
        /// <param name="arguments">Arguments.</param>
        /// <param name="workingDirectory">The working directory for the process.</param>
        private void StartProcess(
            string exe,
            string arguments,
            string workingDirectory)
        {
            _process = CreateProcess(exe, workingDirectory);
            _process.StartInfo.Arguments = arguments;
            _process.StartInfo.StandardOutputEncoding = Encoding.UTF8; // So we can read Japanese from TestRunner's console
            _process.StartInfo.StandardErrorEncoding = Encoding.UTF8; // So we can read Japanese from TestRunner's console
            _process.OutputDataReceived += HandleOutput;
            _process.ErrorDataReceived += HandleOutput;
            _process.Exited += ProcessExit;
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public int ProcessId
        {
            get { return _process != null ? _process.Id : 0; }
        }

        /// <summary>
        /// Handle a line of output/error data from the process.
        /// </summary>
        private void HandleOutput(object sender, DataReceivedEventArgs e)
        {
            Log(e.Data);
        }

        /// <summary>
        /// Stop the running process or background thread.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    ProcessUtilities.KillProcessTree(_process);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }

        private Timer _exitTimer;

        /// <summary>
        /// Handle process exit (success, error, or interrupt).
        /// </summary>
        void ProcessExit(object sender, EventArgs e)
        {
            if (_process == null)
                return;

            var exitCode = _process.ExitCode;
            _process = null;

            if (exitCode == 0)
            {
                // Tricky: we have to wait for the final output of the last process to
                // be logged, otherwise the output from the next process may be interleaved.
                RunUI(() =>
                {
                    _exitTimer = new Timer {Interval = 700};
                    _exitTimer.Tick += (o, args) =>
                    {
                        _exitTimer.Stop();
                        RunNext(null, null);
                    };
                    _exitTimer.Start();
                });
            }
            else
            {
                try
                {
                    RunUI(() => CommandsDone(false));
                }
// ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }
            }
        }

        #endregion

        #region Display/scroll log

        private string _logFile;

        public string LogFile
        {
            get { return _logFile; }
            set
            {
                _logFile = value;
                if (File.Exists(_logFile))
                {
                    try
                    {
                        File.Delete(_logFile);
                    }
// ReSharper disable once EmptyGeneralCatchClause
                    catch (Exception)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Add a line to the log buffer.
        /// </summary>
        public void Log(string line)
        {
            if (line == null)
                return;

            if (FilterFunc != null && !FilterFunc(line.Trim()))
                return;

            lock (_logBuffer)
            {
                if (_logBuffer.Length == 0 && _logEmpty)
                    _logBuffer.AppendLine();
                _logBuffer.AppendLine(line);
            }
        }

        public void UpdateLog()
        {
            UpdateLog(null, null);
        }

        /// <summary>
        /// Append buffered output to displayed log (and possibly to a log file).
        /// </summary>
        private void UpdateLog(object sender, EventArgs eventArgs)
        {
            if (_scrolling)
                return;

            // Get buffered log output.
            string logLines;
            lock (_logBuffer)
            {
                if (_logBuffer.Length == 0)
                    return;
                logLines = _logBuffer.ToString();
                _logBuffer.Clear();
                _logEmpty = false;
            }

            // Add to log file.
            if (!string.IsNullOrEmpty(LogFile))
            {
                lock (LogLock)
                {
                    File.AppendAllText(LogFile, logLines);
                }
            }

            // Scroll if text box is already scrolled to bottom.
            int previousLength = Math.Max(0, TextLength - 1);
            var point = GetPositionFromCharIndex(previousLength);
            bool autoScroll = (ClientRectangle.Bottom >= point.Y);

            if (Focused)
                StopButton.Focus(); // text box scrolls if it has focus!
            var addLines = logLines.Replace("\r", "");
            if (addLines.EndsWith("\n"))
                addLines = addLines.Substring(0, addLines.Length - 1);
            AddLines(addLines.Split('\n'));

            if (autoScroll)
            {
                Select(Text.Length - 1, 0);
                ScrollToCaret();
            }
        }

        /// <summary>
        /// Close the log after command execution is finished.  Run on UI thread.
        /// </summary>
        private void FinishLog()
        {
            if (_outputTimer != null)
            {
                _outputTimer.Stop();
                _outputTimer = null;
            }

            // Show final log output.
            UpdateLog();
        }

        private class ColorMatch
        {
            public string LineStart;
            public string LineContains;
            public Color LineColor;
        }

        private readonly List<ColorMatch> _colorMatchList = new List<ColorMatch>();

        public void AddColorPattern(string lineStart, Color lineColor)
        {
            _colorMatchList.Add(new ColorMatch {LineStart = lineStart, LineColor = lineColor});
        }
 
        public void AddColorPatternEx(string lineStart, string lineContains, Color lineColor)
        {
            _colorMatchList.Add(new ColorMatch {LineStart = lineStart, LineContains = lineContains, LineColor = lineColor});
        }
 
        public void Load(string file, Action loadDone = null)
        {
            _logFile = file;

            if (!File.Exists(file))
            {
                ClearLog();
                return;
            }

            // Load non-colored text quickly (we are on the UI thread here).
            LoadFile(file, RichTextBoxStreamType.PlainText);
            var text = Text;

            // Apply colors using a background thread.
            var colorWorker = new BackgroundWorker();
            colorWorker.DoWork += (sender, args) =>
            {
                IgnorePaint++;

                int startIndex = 0;
                while (true)
                {
                    // Scan for start of line.
                    while (startIndex < text.Length && text[startIndex] == '\n')
                        startIndex++;
                    if (startIndex == text.Length)
                        break;

                    // Find end of line.
                    int endIndex = text.IndexOf('\n', startIndex) + 1;
                    if (endIndex == 0)
                        break;

                    // Match line against various start patterns.
                    ColorMatch match = null;
                    foreach (var colorMatch in _colorMatchList)
                    {
                        var lineStart = text.Substring(startIndex, Math.Min(colorMatch.LineStart.Length, text.Length - startIndex));
                        if (lineStart == colorMatch.LineStart)
                        {
                            if (colorMatch.LineContains != null)
                            {
                                int lineContainsIndex = text.IndexOf(colorMatch.LineContains, startIndex,
                                    StringComparison.CurrentCulture);
                                if (lineContainsIndex < 0 || lineContainsIndex > text.IndexOf('\n', startIndex))
                                    continue;
                            }
                            match = colorMatch;
                            break;
                        }
                    }

                    // Color text if a match was found.
                    if (match != null)
                    {
                        int start = startIndex;
                        RunUI(() =>
                        {
                            Select(start, endIndex - start);
                            SelectionColor = match.LineColor;
                            if (ColorLine != null)
                                ColorLine(text.Substring(start, endIndex - start));
                        });
                    }

                    // Move to next line.
                    startIndex = endIndex;
                }

                IgnorePaint--;

                if (loadDone != null)
                    loadDone();
            };
            colorWorker.RunWorkerAsync();
        }

        public void AddLines(string[] lines)
        {
            IgnorePaint++;
            foreach (var line in lines)
            {
                ColorMatch match = null;
                foreach (var colorMatch in _colorMatchList)
                {
                    if (line.StartsWith(colorMatch.LineStart))
                    {
                        match = colorMatch;
                        break;
                    }
                }

                var addLine = line + Environment.NewLine;
                RunUI(() =>
                {
                    AppendText(addLine);
                    if (match != null)
                    {
                        Select(Text.Length - addLine.Length, addLine.Length);
                        SelectionColor = match.LineColor;
                        Select(Text.Length - 1, 0);
                    }
                });
            }
            IgnorePaint--;
        }

        private const short WM_PAINT = 0x00f;
        private const short WM_HSCROLL = 0x114;
        private const short WM_VSCROLL = 0x115;
        private const int SB_ENDSCROLL = 8;

        // ReSharper disable once ConvertToConstant.Global
        public int IgnorePaint { get; set; }

        private bool _scrolling;

        protected override void WndProc(ref Message m)
        {
            // Code courtesy of Mark Mihevc
            // sometimes we want to eat the paint message so we don't have to see all the
            // flicker from when we select the text to change the color.
            if (m.Msg == WM_PAINT && IgnorePaint != 0)
            {
                m.Result = IntPtr.Zero; // not painting, must set this to IntPtr.Zero, otherwise serious problems.
                return;
            }

            if (m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL)
                _scrolling = ((short)m.WParam != SB_ENDSCROLL);

            base.WndProc(ref m);
        }

        #endregion

    }
}
