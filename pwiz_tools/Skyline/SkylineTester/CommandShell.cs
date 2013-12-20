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
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace SkylineTester
{
    public class CommandShell : RichTextBox
    {
        public string DefaultDirectory { get; set; }
        public Button StopButton { get; set; }
        public Func<string, bool> FilterFunc { get; set; } 

        private string _workingDirectory;
        private readonly List<string> _commands = new List<string>();
        private int _commandIndex;
        private Action<bool> _doneAction;
        private readonly StringBuilder _logBuffer = new StringBuilder();
        private bool _logEmpty;
        private bool _logWasScrolled;
        private Process _process;
        private BackgroundWorker _deleteTask;
        private Timer _outputTimer;

        public CommandShell()
        {
            HScroll += OnScroll;
            VScroll += OnScroll;
        }

        #region Add/run commands

        /// <summary>
        /// Add a command and arguments to be executed by the command shell by the Run
        /// command.  You can also add a comment line starting with '#'.  Multiple Add
        /// calls can be made to create a script of commands to be executed by Run.
        /// </summary>
        public void Add(string command, params object[] args)
        {
            _commands.Add(string.Format(command, args));
        }

        public void AddImmediate(string command, params object[] args)
        {
            Log(string.Format(command, args) + Environment.NewLine);
            UpdateLog(null, null);
        }

        public void ClearLog()
        {
            Clear();
            _logEmpty = true;
        }

        /// <summary>
        /// Run commands accumulated by one or more calls to Add.  An optional
        /// argument specifies a method to call when the commands are done
        /// (through successful completion, abort due to error, or abort due to
        /// user interrupt request).
        /// </summary>
        public void Run(Action<bool> doneAction = null)
        {
            _doneAction = doneAction;
            _commandIndex = 0;

            ClearLog();

            _outputTimer = new Timer { Interval = 500 };
            _outputTimer.Tick += UpdateLog;
            _outputTimer.Start();

            RunNext();
        }

        /// <summary>
        /// Run the next command in the script on the main thread.
        /// </summary>
        private void RunNext(object sender, EventArgs e)
        {
            Invoke(new Action(RunNext));
        }

        private void RunNext()
        {
            while (_commandIndex < _commands.Count)
            {
                var line = _commands[_commandIndex++];

                // Handle comment line.
                if (line.StartsWith("#"))
                {
                    Log(Environment.NewLine + line);
                    continue;
                }

                // Execute a command.
                Log("> " + line);

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
                    continue;
                }

                // Remove a directory.
                if (command == "rmdir")
                {
                    _deleteTask = new BackgroundWorker();
                    bool recursiveDelete = false;
                    if (words[0] == "/s")
                    {
                        recursiveDelete = true;
                        words.RemoveAt(0);
                    }
                    var deleteDir = words[0].Trim('"');
                    _deleteTask.DoWork += (o, a) =>
                    {
                        try
                        {
                            Try<Exception>(() => Directory.Delete(deleteDir, recursiveDelete), 4);
                            if (_deleteTask != null)
                            {
                                _deleteTask = null;
                                RunNext(null, null);
                                return;
                            }
                        }
// ReSharper disable once EmptyGeneralCatchClause
                        catch (Exception)
                        {
                        }

                        try
                        {
                            Invoke(new Action(() =>
                            {
                                _deleteTask = null;
                                MessageBox.Show("Can't delete " + deleteDir);
                                FinishLog();
                            }));
                        }
// ReSharper disable once EmptyGeneralCatchClause
                        catch (Exception)
                        {
                        }
                    };
                    _deleteTask.RunWorkerAsync();
                }

                // Run a command in a separate process.
                else
                {
                    StartProcess(
                        command,
                        args,
                        _workingDirectory);
                    _workingDirectory = DefaultDirectory;
                }
                return;
            }

            CommandsDone(true);
        }

        /// <summary>
        /// Handle completion of commands, either by successfully finishing,
        /// or due to error or user interrupt.
        /// </summary>
        private void CommandsDone(bool success)
        {
            _commands.Clear();
            if (_doneAction != null)
                _doneAction(success);
            if (!success)
                Log("# Stopped.");
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
            _process.OutputDataReceived += HandleOutput;
            _process.ErrorDataReceived += HandleOutput;
            _process.Exited += ProcessExit;
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        /// <summary>
        /// Handle a line of output/error data from the process.
        /// </summary>
        private void HandleOutput(object sender, DataReceivedEventArgs e)
        {
            var line = e.Data;
            if (FilterFunc != null && !FilterFunc(line))
                return;
            Log(line);
        }

        /// <summary>
        /// Stop the running process or background thread.
        /// </summary>
        public void Stop()
        {
            try
            {
                _deleteTask = null;
                if (_process != null && !_process.HasExited)
                    ProcessUtilities.KillProcessTree(_process);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Handle process exit (success, error, or interrupt).
        /// </summary>
        void ProcessExit(object sender, EventArgs e)
        {
            if (_process.ExitCode == 0)
                RunNext(null, null);
            else
            {
                try
                {
                    Invoke(new Action(() => CommandsDone(false)));
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
        private void Log(string line)
        {
            lock (_logBuffer)
            {
                if (_logBuffer.Length == 0 && _logEmpty)
                    _logBuffer.AppendLine();
                _logBuffer.AppendLine(line);
            }
        }

        /// <summary>
        /// Append buffered output to displayed log (and possibly to a log file).
        /// </summary>
        private void UpdateLog(object sender, EventArgs eventArgs)
        {
            // Delay update a little while if user was scrolling recently.
            if (_logWasScrolled)
            {
                _logWasScrolled = false;
                return;
            }

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
                File.AppendAllText(LogFile, logLines);

            // Scroll if text box is already scrolled to bottom.
            int previousLength = Math.Max(0, TextLength - 1);
            var point = GetPositionFromCharIndex(previousLength);
            bool autoScroll = (ClientRectangle.Bottom >= point.Y);

            StopButton.Focus(); // text box scrolls if it has focus!
            AppendText(logLines);

            if (autoScroll)
            {
                Select(Text.Length - 1, 0);
                ScrollToCaret();
            }

            DoPaint = false;
            ColorText(previousLength, "\n# ", Color.DarkGreen);
            ColorText(previousLength, "\n> ", Color.FromArgb(120, 120, 120));
            ColorText(previousLength, "\n...skipped ", Color.Orange);
            ColorText(previousLength, "\n...failed ", Color.Red);
            DoPaint = true;
        }

        /// <summary>
        /// Close the log after command execution is finished.
        /// </summary>
        private void FinishLog()
        {
            if (_outputTimer != null)
            {
                _outputTimer.Stop();
                _outputTimer = null;
            }

            // Show final log output.
            _logWasScrolled = false;
            UpdateLog(null, null);
        }

        /// <summary>
        /// Add color to log output.
        /// </summary>
        private void ColorText(int previousLength, string text, Color color)
        {
            while (true)
            {
                int startIndex = Text.IndexOf(text, previousLength, StringComparison.InvariantCulture);
                if (startIndex < 0)
                    return;
                int endIndex = Text.IndexOf('\n', startIndex + 1);
                if (endIndex < 0)
                    return;
                Select(startIndex, endIndex - startIndex);
                SelectionColor = color;
                previousLength = endIndex;
            }
        }

        private void OnScroll(object sender, EventArgs eventArgs)
        {
            _logWasScrolled = true;
        }


        const short WM_PAINT = 0x00f;

        // ReSharper disable once ConvertToConstant.Global
        public bool DoPaint = true;

        protected override void WndProc(ref Message m)
        {
            // Code courtesy of Mark Mihevc
            // sometimes we want to eat the paint message so we don't have to see all the
            // flicker from when we select the text to change the color.
            if (m.Msg == WM_PAINT && !DoPaint)
                m.Result = IntPtr.Zero; // not painting, must set this to IntPtr.Zero, otherwise serious problems.
            else
                base.WndProc(ref m); // message other than WM_PAINT, jsut do what you normally do.
        }

        #endregion

        private static void Try<TEx>(Action action, int loopCount, bool throwOnFailure = true, int milliseconds = 500)
            where TEx : Exception
        {
            for (int i = 1; i < loopCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (TEx)
                {
                    Thread.Sleep(milliseconds);
                }
            }

            // Try the last time, and let the exception go.
            if (throwOnFailure)
                action();
        }
    }
}
