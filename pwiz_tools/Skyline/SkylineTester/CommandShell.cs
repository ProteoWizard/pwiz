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
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace SkylineTester
{
    public class CommandShell : RichTextBox
    {
        public const int MAX_PROCESS_SILENCE_MINUTES = 60; // If a process is silent longer than this, assume it's hung
        public const int MAX_PROCESS_OUTPUT_DELAY = 700; // milliseconds 
        public const int RETRY_WAIT_SECONDS = 60; // Wait this long between retries
        private enum EXIT_TYPE {error_stop, error_restart, success};
        public string DefaultDirectory { get; set; }
        public Button StopButton { get; set; }
        public Func<string, bool> FilterFunc { get; set; }
        public Action<string> ColorLine { get; set; }
        public Action FinishedOneCommand { get; set; }
        public int RestartCount { get; set; }
        public int NextCommand { get; set; }
        public DateTime RunStartTime { get; set; }
        public bool IsUnattended { get; set; }
        public readonly object LogLock = new object();

        /// <summary>Checks whether our child process is being debugged.</summary>
        /// From https://www.codeproject.com/articles/670193/csharp-detect-if-debugger-is-attached
        /// The "remote" in CheckRemoteDebuggerPresent does not imply that the debugger
        /// necessarily resides on a different computer; instead, it indicates that the 
        /// debugger resides in a separate and parallel process.
        /// Use the IsDebuggerPresent function to detect whether the calling process 
        /// is running under the debugger.
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        private string _workingDirectory;
        private readonly List<string> _commands = new List<string>();
        private readonly HashSet<string> _commandsWithRetry = new HashSet<string>();
        private Action<bool> _doneAction;
        private Action _restartAction;
        private readonly StringBuilder _logBuffer = new StringBuilder();
        private bool _logEmpty;
        private Process _process;
        private bool _restartOnProcessFailure;
        private string _processName;
        private bool _processKilled;
        private Timer _outputTimer;
        private DateTime _lastOutputTime;

        #region Add/run commands

        // Insert a pause before next executed command
        public void InsertPause()
        {
            var pauseCommand = "timeout /T " + RETRY_WAIT_SECONDS + " /NOBREAK";
            if (_commands.Count == 0 || _commands[0] != pauseCommand)
            {
                _commands.Insert(0, pauseCommand);
            }
        }

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

        public int AddWithRetry(string command, params object[] args)
        {
            var result = Add(command, args);
            _commandsWithRetry.Add(_commands[result]);
            return result;
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
        public void Run(Action<bool> doneAction, Action restartAction)
        {
            _doneAction = doneAction;
            _restartAction = restartAction;
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
                        using (var deleteWindow = new DeleteWindow(deleteDir, IsUnattended))
                        {
                            deleteWindow.ShowDialog(GetParentForm());
                            if (deleteWindow.IsCancelled)
                                break;
                        }
                        if (Directory.Exists(deleteDir))
                        {
                            try
                            {
                                // One last try to either delete the directory or report an exception as to why this failed
                                Directory.Delete(deleteDir, true);
                            }
                            catch (Exception e)
                            {
                                Log(Environment.NewLine + "!!!! COMMAND FAILED !!!! unable to remove folder " + deleteDir + " : " + e);
                                CommandsDone(IsUnattended ? EXIT_TYPE.error_restart : EXIT_TYPE.error_stop);
                                return;
                            }
                        }
                    }
                }

                // Run a command in a separate process.
                else
                {
                    _restartOnProcessFailure = _commandsWithRetry.Contains(line);
                    try
                    {
                        StartProcess(
                            command,
                            args,
                            _workingDirectory);
                    }
                    catch (Exception e)
                    {
                        if (e is Win32Exception && e.Message.Contains("cannot find"))
                            Log(Environment.NewLine + "!!!! COMMAND FAILED !!!! Command not found " + command);
                        else
                            Log(Environment.NewLine + "!!!! COMMAND FAILED !!!! " + e);
                        CommandsDone(_restartOnProcessFailure ? EXIT_TYPE.error_restart : EXIT_TYPE.error_stop);    // Quit if any command fails
                    }
                    _workingDirectory = DefaultDirectory;
                    return;
                }
            }

            CommandsDone(EXIT_TYPE.success);
        }

        private Form GetParentForm()
        {
            Control parent = this;
            while (parent != null)
            {
                var parentForm = parent as Form;
                if (parentForm != null)
                    return parentForm;
                parent = parent.Parent;
            }

            return null;
        }

        /// <summary>
        /// Handle completion of commands, either by successfully finishing,
        /// or due to error or user interrupt.  Run on UI thread.
        /// </summary>
        private void CommandsDone(EXIT_TYPE exitType)
        {
            bool restart = false;
            
            if (exitType == EXIT_TYPE.error_restart)
            {
                // restart a maximum of 10 times and within 30 minutes of starting
                var MaxRestartCount = 10;
                if (RestartCount < MaxRestartCount && DateTime.UtcNow.Subtract(RunStartTime) < new TimeSpan(0, 0, 30, 0, 0)) 
                {
                    restart = true;
                    RestartCount++;
                    Log("# Will retry in " + RETRY_WAIT_SECONDS + " seconds (this will be retry #" + RestartCount + " of " + MaxRestartCount + ") " + DateTime.Now.ToString("f") + Environment.NewLine + Environment.NewLine);
                }
                else
                {
                    Log("# Retry count exceeded" + Environment.NewLine + Environment.NewLine);
                    exitType = EXIT_TYPE.error_stop;
                }
            }
            if(exitType == EXIT_TYPE.error_stop)
                Log("# Stopped " + DateTime.Now.ToString("f") + Environment.NewLine + Environment.NewLine);

            UpdateLog();
            if (restart)
            {
                _restartAction();
            }
            else
            {
                _commands.Clear();
                _doneAction(exitType == EXIT_TYPE.success);
            }
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

            // Configure git to fail if its https connection stalls out, so our retry logic can kick in
            _process.StartInfo.EnvironmentVariables.Add(@"GIT_HTTP_LOW_SPEED_LIMIT", @"1000"); // Fail if transfer rate falls below 1Kbps,
            _process.StartInfo.EnvironmentVariables.Add(@"GIT_HTTP_LOW_SPEED_TIME", @"300");   // and stays that way for 5 minutes

            _process.OutputDataReceived += HandleOutput;
            _process.ErrorDataReceived += HandleOutput;
            _process.Exited += ProcessExit;
            _process.Start();
            _processName = _process.ProcessName;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            ResetLastOutputTime();
        }

        public int ProcessId
        {
            get { return _process != null ? _process.Id : 0; }
        }

        private void ResetLastOutputTime() { _lastOutputTime = DateTime.UtcNow; }   // Use UtcNow to avoid hiccups with tests running during DST changeover
 
        private TimeSpan ElapsedTimeSinceLastOutput
        {
            get { return DateTime.UtcNow - _lastOutputTime; }  // Use UtcNow to avoid hiccups with tests running during DST changeover
        }
        /// <summary>
        /// Handle a line of output/error data from the process.
        /// </summary>
        private void HandleOutput(object sender, DataReceivedEventArgs e)
        {
            ResetLastOutputTime();
            Log(e.Data);
        }

        /// <summary>
        /// Stop the running process or background thread.
        /// </summary>
        public void Stop(bool preserveHungProcesses = false)
        {
            if (IsWaiting)
            {
                IsWaiting = false;  // Stop waiting
                return;
            }

            try
            {
                if (IsRunning)
                {
                    // If process has been quiet for a very long time, don't kill it, for forensic purposes
                    if (preserveHungProcesses && ElapsedTimeSinceLastOutput.TotalMinutes > MAX_PROCESS_SILENCE_MINUTES)
                    {
                        Log(string.Format("{0} has been silent for more than {1} minutes.  Leaving it running for forensic purposes.",
                           _process.Modules[0].FileName, MAX_PROCESS_SILENCE_MINUTES));
                    }
                    else
                    {
                        _processKilled = true;
                        ProcessUtilities.KillProcessTree(_process);
                    }
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }

        public bool IsRunning
        {
            get { return _process != null && !_process.HasExited; }
        }

        public bool IsWaiting { get; set; }

        public bool IsDebuggerAttached 
        {
            get
            {
                if (_process == null)
                    return false;
                var isDebuggerAttached=false;
                CheckRemoteDebuggerPresent(_process.Handle, ref isDebuggerAttached);
                return isDebuggerAttached;
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

            var exitCode = _process.ExitCode; // That's all the info you can get from a process that has exited - no name etc
            var processName = _process.ToString();
            _process = null;
            bool processKilled = _processKilled;
            _processKilled = false;

            if (exitCode == 0)
            {
                // Tricky: we have to wait for the final output of the last process to
                // be logged, otherwise the output from the next process may be interleaved.
                RunUI(() =>
                {
                    _exitTimer = new Timer {Interval = MAX_PROCESS_OUTPUT_DELAY};
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
                    if (!processKilled)
                        Log(Environment.NewLine + "# Process " + (_processName??string.Empty) + " had nonzero exit code " + exitCode + Environment.NewLine);
                    RunUI(() => CommandsDone(_restartOnProcessFailure && !processKilled ? EXIT_TYPE.error_restart : EXIT_TYPE.error_stop));
                }
// ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }
            }
        }

        #endregion

        #region Display/scroll log

        // VisibleLogFile is the file selected to view, LogFile is the file being currently written to
        public string VisibleLogFile { get; set; }

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
                VisibleLogFile = _logFile;
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
            if (VisibleLogFile == null || !VisibleLogFile.Equals(LogFile))
                return;
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
 
        public void Load(string file, bool isRunLogFile, Action loadDone = null)
        {
            if (isRunLogFile)
                _logFile = file;
            else
                VisibleLogFile = file;

            if (VisibleLogFile == null)
                VisibleLogFile = _logFile;

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
            var matchIndices = new int[lines.Length];
            for (var i = 0; i < lines.Length; i++)
            {
                matchIndices[i] = -1;
                for (var j = 0; j < _colorMatchList.Count; j++)
                {
                    var colorMatch = _colorMatchList[j];
                    if (lines[i].StartsWith(colorMatch.LineStart))
                    {
                        matchIndices[i] = j;
                        break;
                    }
                }
            }

            // Append and change color of all lines in the same RunUI.
            // Also don't color lines individual lines, instead search for continuous
            // sequences of lines of the same color
            RunUI(() =>
            {
                for (var i = 0; i < lines.Length; )
                {
                    var prev = i;
                    var addLines = new StringBuilder();
                    var item = matchIndices[i];
                    while (i < lines.Length && item == matchIndices[i])
                        addLines.AppendLine(lines[i++]);
                    var lineCount = i - prev;
                    var addLine = addLines.ToString();
                    AppendText(addLine);
                    if (item < 0)
                        continue;

                    Select(Text.Length - addLine.Length + lineCount, addLine.Length);
                    SelectionColor = _colorMatchList[item].LineColor;
                    Select(Text.Length - 1, 0);
                }
            });
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
