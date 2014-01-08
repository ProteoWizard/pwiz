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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TestRunnerLib;
using ZedGraph;

namespace SkylineTester
{
    partial class SkylineTesterWindow
    {
        private void Run(object sender, EventArgs e)
        {
            // Stop running task.
            if (_runningTab != null)
            {
                Stop(null, null);
                return;
            }

            commandShell.ClearLog();
            errorsShell.ClearLog();

            // Prepare to start task.
            _runningTab = _tabs[tabs.SelectedIndex];
            if (!_runningTab.Run())
                _runningTab = null;
            if (_runningTab == null)    // note: may be cleared by Run() (e.g., Cancel in DeleteWindow)
                return;

            foreach (var runButton in _runButtons)
                runButton.Text = "Stop";
            buttonStop.Enabled = buttonErrorsStop.Enabled = true;

            // Update elapsed time display.
            _runStartTime = DateTime.Now;
            _runTimer = new Timer { Interval = 1000 };
            _runTimer.Tick += (s, a) =>
            {
                var elapsedTime = DateTime.Now - _runStartTime;
                statusRunTime.Text = "{0}:{1:D2}:{2:D2}".With(elapsedTime.Hours, elapsedTime.Minutes, elapsedTime.Seconds);
            };
            _runTimer.Start();
        }

        public void ResetElapsedTime()
        {
            _runStartTime = DateTime.Now;
        }

        private void Stop(object sender, EventArgs e)
        {
            _runningTab.Cancel();
        }

        public void Done()
        {
            _runningTab = null;

            foreach (var runButton in _runButtons)
                runButton.Text = "Run";
            buttonStop.Enabled = buttonErrorsStop.Enabled = false;

            if (_runTimer != null)
            {
                _runTimer.Stop();
                _runTimer.Dispose();
                _runTimer = null;
            }

            SetStatus();
        }

        public string GetBuildRoot()
        {
            var root = buildRoot.Text;
            if (!Path.IsPathRooted(root))
                root = Path.Combine(RootDir, root);
            return root;
        }

        public DirectoryInfo GetSkylineDirectory(string startDirectory)
        {
            string skylinePath = startDirectory;
            var skylineDirectory = skylinePath != null ? new DirectoryInfo(skylinePath) : null;
            while (skylineDirectory != null && skylineDirectory.Name != "Skyline")
                skylineDirectory = skylineDirectory.Parent;
            return skylineDirectory;
        }

        private string GetTestRunnerDirectory()
        {
            var root = GetBuildRoot();
            var testRunner = ChooseMostRecentFile(
                Path.Combine(root, @"pwiz_tools\Skyline\bin\x86\Release\TestRunner.exe"),
                Path.Combine(root, @"pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe"),
                Path.Combine(ExeDir, "TestRunner.exe"));
            return Path.GetDirectoryName(testRunner);
        }

        private string ChooseMostRecentFile(params string[] files)
        {
            string mostRecent = null;
            foreach (var file in files)
            {
                if (!File.Exists(file))
                    continue;
                if (mostRecent != null)
                {
                    var fileDate = File.GetLastWriteTime(file);
                    var otherDate = File.GetLastWriteTime(mostRecent);
                    if (fileDate < otherDate)
                        continue;
                }
                mostRecent = file;
            }
            return mostRecent;
        }

        public void AddTestRunner(string args)
        {
            MemoryChartWindow.Start("TestRunnerMemory.log");
            TestsRun = 0;
            Try.Multi<Exception>(() => Directory.Delete(_resultsDir, true), 4, false);

            var testRunner = Path.Combine(GetTestRunnerDirectory(), "TestRunner.exe");
            _testRunnerIndex = commandShell.Add(
                "{0} random=off status=on results={1} {2} {3}",
                testRunner.Quote(),
                _resultsDir.Quote(),
                runWithDebugger.Checked ? "Debug" : "",
                args);
        }

        public void ClearLog()
        {
            commandShell.ClearLog();
            errorsShell.ClearLog();
            _testRunnerIndex = int.MaxValue;
        }

        public void RunCommands()
        {
            RunningTestName = null;
            commandShell.FinishedOneCommand = () => { RunningTestName = null; };
            commandShell.Run(CommandsDone);
        }

        public int TestRunnerProcessId
        {
            get
            {
                return (commandShell.NextCommand == _testRunnerIndex + 1)
                    ? commandShell.ProcessId
                    : 0;
            }
        }

        private int _testRunnerIndex;

        public void CommandsDone(bool success)
        {
            commandShell.UpdateLog();

            if (commandShell.NextCommand > _testRunnerIndex)
            {
                _testRunnerIndex = int.MaxValue;

                // Report test results.
                var testRunner = Path.Combine(GetTestRunnerDirectory(), "TestRunner.exe");
                commandShell.NextCommand = commandShell.Add("{0} report={1}", testRunner.Quote(), commandShell.LogFile.Quote());
                RunCommands();
                return;
            }

            commandShell.Done(success);

            if (_runningTab != null && _runningTab.Stop(success))
                _runningTab = null;
            if (_runningTab == null)
                Done();
        }

        private IEnumerable<string> GetLanguageNames()
        {
            foreach (var language in GetLanguages())
            {
                string name;
                if (_languageNames.TryGetValue(language, out name))
                    yield return name;
            }
        }

        private IEnumerable<string> GetLanguages()
        {
            return (new FindLanguages(GetTestRunnerDirectory(), "en", "fr").Enumerate());
        }

        public void InitLanguages(ComboBox comboBox)
        {
            comboBox.Items.Clear();
            var languages = GetLanguageNames().ToList();
            foreach (var language in languages)
                comboBox.Items.Add(language);
            comboBox.SelectedIndex = 0;
        }

        public string GetCulture(ComboBox comboBox)
        {
            return _languageNames.First(x => x.Value == (string)comboBox.SelectedItem).Key;
        }

        public void RefreshLogs()
        {
            if (Tabs.SelectedTab == tabOutput)
                _tabOutput.Enter();
            if (Tabs.SelectedTab == tabErrors)
                _tabErrors.Enter();
        }

        public void InitLogSelector(ComboBox combo, Button openButton, bool showDefaultLog = true)
        {
            combo.Items.Clear();
            var summaryLog = Path.Combine(RootDir, QualityLogsDirectory, SummaryLog);
            if (Summary == null)
                Summary = new Summary(summaryLog);

            foreach (var run in Summary.Runs)
                AddRun(run, combo);


            if (showDefaultLog && File.Exists(DefaultLogFile))
                combo.Items.Insert(0, LastRunName + " output");

            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
                openButton.Enabled = true;
            }
            else
            {
                openButton.Enabled = false;
            }
        }

        public void AddRun(Summary.Run run, ComboBox combo)
        {
            var text = run.Date.ToString("M/d  h:mm tt");
            if (run.Revision > 0)
                text += "    (rev. " + run.Revision + ")";
            combo.Items.Insert(0, text);
        }

        public string GetSelectedLog(ComboBox combo)
        {
            if (combo.SelectedIndex == -1)
                return null;

            // Combo box items are reversed.
            int index = combo.SelectedIndex;
            return Char.IsDigit(combo.Items[index].ToString()[0])
                ? Summary.GetLogFile(Summary.Runs[combo.Items.Count - 1 - index])
                : DefaultLogFile;
        }

        public void OpenSelectedLog(ComboBox combo)
        {
            var file = GetSelectedLog(combo);
            if (File.Exists(file))
            {
                var editLogFile = new Process { StartInfo = { FileName = file } };
                editLogFile.Start();
            }
        }

        private void InitQuality()
        {
            qualityBuildType.SelectedIndex = 0;

            InitGraph(ref graphMemory, "Memory used");
            var pane = graphMemory.GraphPane;
            pane.XAxis.Type = AxisType.Text;
            pane.XAxis.Scale.FontSpec.Family = "Courier New";
            pane.XAxis.Scale.FontSpec.Size = 12;
            pane.XAxis.Scale.Align = AlignP.Outside;
            pane.XAxis.MajorTic.IsAllTics = false;
            pane.XAxis.MinorTic.IsAllTics = false;
            pane.XAxis.Scale.IsVisible = true;
            pane.XAxis.Scale.Format = "#";
            pane.XAxis.Scale.Mag = 0;
            pane.YAxis.IsVisible = true;
            pane.YAxis.Scale.FontSpec.Family = "Courier New";
            pane.YAxis.Scale.FontSpec.Size = 12;
            pane.YAxis.Scale.Align = AlignP.Inside;
            pane.YAxis.MinorTic.IsAllTics = false;
            pane.YAxis.Title.Text = "MB";
            pane.YAxis.Scale.Format = "#";
            pane.YAxis.Scale.Mag = 0;
            pane.Legend.IsVisible = true;
            pane.YAxis.Scale.FontSpec.Angle = 90;

            InitGraph(ref graphTestsRun, "Tests run");
            InitGraph(ref graphDuration, "Duration");
            InitGraph(ref graphFailures, "Failures");
            InitGraph(ref graphMemoryHistory, "Memory used");

            panelMemoryGraph.Controls.Add(graphMemory);
            historyTable.Controls.Add(graphMemoryHistory, 0, 0);
            historyTable.Controls.Add(graphFailures, 0, 0);
            historyTable.Controls.Add(graphDuration, 0, 0);
            historyTable.Controls.Add(graphTestsRun, 0, 0);
        }

        private void InitGraph(ref ZedGraphControl graph, string title)
        {
            graph = new ZedGraphControl
            {
                Dock = DockStyle.Fill,
                EditButtons = MouseButtons.Left,
                EditModifierKeys = Keys.None,
                IsEnableVPan = false,
                IsEnableVZoom = false,
                Margin = new Padding(5)
            };

            var pane = graph.GraphPane;
            pane.Title.Text = title;
            pane.Chart.Border.IsVisible = false;
            pane.YAxis.IsVisible = false;
            pane.X2Axis.IsVisible = false;
            pane.Y2Axis.IsVisible = false;
            pane.XAxis.MajorTic.IsOpposite = false;
            pane.YAxis.MajorTic.IsOpposite = false;
            pane.XAxis.MinorTic.IsOpposite = false;
            pane.YAxis.MinorTic.IsOpposite = false;
            pane.XAxis.MinorTic.IsAllTics = false;
            pane.XAxis.Title.IsVisible = false;
            pane.IsFontsScaled = false;
            pane.XAxis.Scale.MaxGrace = 0.0;
            pane.YAxis.Scale.MaxGrace = 0.05;
            pane.XAxis.Scale.FontSpec.Angle = 90;
            pane.Legend.IsVisible = false;
        }

        public bool HasBuildPrerequisites
        {
            get
            {
                if (Subversion == null)
                {
                    MessageBox.Show(
                        "Subversion is required to build Skyline.  You can install it from http://sourceforge.net/projects/win32svn/");
                    return false;
                }

                if (Devenv == null)
                {
                    MessageBox.Show("Visual Studio 10.0 is required to build Skyline.");
                    return false;
                }

                return true;
            }
        }

        private void RunUI(Action action)
        {
            Invoke(action);
        }
    }
}
