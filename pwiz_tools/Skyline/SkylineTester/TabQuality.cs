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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZedGraph;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private const string QualityLogsDirectory = "Quality logs";
        private const string SummaryLog = "Summary.log";

        private WakeupTimer _endTimer;
        private WakeupTimer _startTimer;

        private void InitQuality()
        {
            BuildType.SelectedIndex = 0;

            InitGraph(graphMemory, "Memory used");
            var pane = graphMemory.GraphPane;
            pane.XAxis.MajorTic.IsAllTics = false;
            pane.XAxis.MinorTic.IsAllTics = false;
            pane.XAxis.Scale.IsVisible = false;
            pane.XAxis.Scale.Format = "#";
            pane.XAxis.Scale.Mag = 0;
            pane.YAxis.IsVisible = true;
            pane.YAxis.MinorTic.IsAllTics = false;
            pane.YAxis.Title.Text = "MB";
            pane.YAxis.Scale.Format = "#";
            pane.YAxis.Scale.Mag = 0;
            pane.Legend.IsVisible = true;
            pane.YAxis.Scale.FontSpec.Angle = 90;

            InitGraph(graphTestsRun, "Tests run");
            InitGraph(graphDuration, "Duration");
            InitGraph(graphFailures, "Failures");
            InitGraph(graphMemoryHistory, "Memory used");
        }

        private void InitGraph(ZedGraphControl graph, string title = null)
        {
            var pane = graph.GraphPane;
            if (title != null)
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

        private Summary _summary;

        private void OpenQuality()
        {
            comboRunDate.Items.Clear();

            var summaryLog = Path.Combine(_rootDir, QualityLogsDirectory, SummaryLog);
            if (_summary == null)
                _summary = new Summary(summaryLog);

            foreach (var run in _summary.Runs)
                AddRun(run);

            UpdateSelectedRun();
            UpdateHistory();
        }

        private void AddRun(Summary.Run run)
        {
            var text = run.Date.ToString("M/d  h:mm tt");
            if (run.Revision > 0)
                text += "    (rev. " + run.Revision + ")";
            comboRunDate.Items.Insert(0, text);
            comboRunDate.SelectedIndex = 0;
        }

        private void CreateGraph(string name, ZedGraphControl graph, Color color, string[] labels, double[] data)
        {
            var pane = graph.GraphPane;
            pane.CurveList.Clear();
            var bars = pane.AddBar(name, null, data, color);
            bars.Bar.Fill = new Fill(color);
            pane.XAxis.Scale.TextLabels = labels;
            pane.XAxis.Type = AxisType.Text;
            pane.AxisChange();
            graph.Refresh();
        }

        private void RunQuality(object sender, EventArgs e)
        {
            if (BuildType.SelectedIndex >= 3 && !HasBuildPrerequisites)
                return;

            if (!ToggleRunButtons(tabQuality))
            {
                statusLabel.Text = null;

                if (_endTimer != null)
                {
                    _endTimer.Stop();
                    _endTimer = null;
                }

                // Schedule stopped before it started.
                if (_startTimer != null)
                {
                    _startTimer.Stop();
                    _startTimer = null;
                    commandShell.AddImmediate("# Stopped.");
                }
                else
                {
                    commandShell.Stop();
                }
                return;
            }

            commandShell.LogFile = null;
            commandShell.ClearLog();

            if (QualityRunOne.Checked || QualityRunContinuously.Checked)
                StartQuality();
            else
                RunSchedule();
        }

        private bool _scheduleEnd;

        private void RunSchedule()
        {
            commandShell.ClearLog();

            var startTime = DateTime.Parse(QualityStartTime.Text);
            var endTime = DateTime.Parse(QualityEndTime.Text);
            if (endTime < startTime)
                endTime = endTime.AddDays(1);
            _endTimer = new WakeupTimer(endTime, () =>
            {
                _scheduleEnd = true;
                Stop(null, null);
            });

            var now = DateTime.Now;
            if (startTime <= now && now < endTime)
            {
                StartQuality();
                return;
            }

            commandShell.AddImmediate(Environment.NewLine + "# Waiting until {0} to start quality pass...", QualityStartTime.Text);
            statusLabel.Text = "Waiting to run quality pass at " + QualityStartTime.Text;

            _startTimer = new WakeupTimer(startTime, StartQuality);
        }

        private Summary.Run _newQualityRun;

        private void StartQuality()
        {
            _startTimer = null;
            statusLabel.Text = "Running quality pass...";
            _testsRun = 0;
            _lastTestResult = null;
            _newQualityRun = new Summary.Run
            {
                Date = DateTime.Now
            };
            _summary.Runs.Add(_newQualityRun);
            AddRun(_newQualityRun);

            var revisionWorker = new BackgroundWorker();
            revisionWorker.DoWork += (s, a) => _revision = GetRevision();
            revisionWorker.RunWorkerAsync();

            var qualityDirectory = Path.Combine(_rootDir, QualityLogsDirectory);
            if (!Directory.Exists(qualityDirectory))
                Directory.CreateDirectory(qualityDirectory);
            commandShell.LogFile = _summary.GetLogFile(_newQualityRun);
            linkLogFile.Text = commandShell.LogFile;

            commandShell.Add("# Quality run started {0}" + Environment.NewLine, _newQualityRun.Date.ToString("f"));

            if (BuildType.SelectedIndex == 3)
                GenerateBuildCommands(new[] {32});
            if (BuildType.SelectedIndex == 4)
                GenerateBuildCommands(new[] {64});

            var args = "offscreen=on quality=on loop=" + (QualityRunOne.Checked ? 1 : 0);
            StartTestRunner(
                args + (QualityChooseTests.Checked ? GetTestList() : ""),
                DoneQuality);

            _qualityTimer = new Timer {Interval = 1000};
            _qualityTimer.Tick += (s, a) => Invoke(new Action(UpdateQuality));
            _qualityTimer.Start();
        }

        private void UpdateQuality()
        {
            if (Tabs.SelectedTab == tabQuality)
            {
                UpdateRun();
                if (comboRunDate.SelectedIndex == 0)
                    UpdateSelectedRun();
                UpdateHistory();
            }
        }

        private Summary.Run GetSelectedRun()
        {
            return comboRunDate.SelectedIndex >= 0
                ? _summary.Runs[_summary.Runs.Count - 1 - comboRunDate.SelectedIndex]
                : null;
        }

        private void UpdateSelectedRun()
        {
            var pane = graphMemory.GraphPane;
            pane.CurveList.Clear();

            var run = GetSelectedRun();
            if (run == null)
            {
                labelDuration.Text = "";
                labelTestsRun.Text = "";
                labelFailures.Text = "";
                labelLeaks.Text = "";
                graphMemory.Refresh();
                return;
            }

            labelDuration.Text = (run.RunMinutes / 60) + ":" + (run.RunMinutes % 60).ToString("D2");
            labelTestsRun.Text = run.TestsRun.ToString(CultureInfo.InvariantCulture);
            labelFailures.Text = run.Failures.ToString(CultureInfo.InvariantCulture);
            labelLeaks.Text = run.Leaks.ToString(CultureInfo.InvariantCulture);

            var managedPointList = new PointPairList();
            var totalPointList = new PointPairList();

            var logFile = _summary.GetLogFile(run);
            if (File.Exists(logFile))
            {
                var logLines = File.ReadAllLines(logFile);
                foreach (var line in logLines)
                {
                    if (line.Length > 6 && line[0] == '[' && line[3] == ':' && line[6] == ']')
                    {
                        var i = line.IndexOf("failures, ", StringComparison.OrdinalIgnoreCase);
                        if (i < 0)
                            continue;
                        var memory = line.Substring(i + 10).Split('/');
                        var managedMemory = double.Parse(memory[0]);
                        var totalMemory = double.Parse(memory[1].Split(' ')[0]);
                        managedPointList.Add(managedPointList.Count, managedMemory);
                        totalPointList.Add(totalPointList.Count, totalMemory);
                    }
                }

                var managedMemoryCurve = pane.AddCurve("Managed", managedPointList, Color.Black, SymbolType.None);
                var totalMemoryCurve = pane.AddCurve("Total", totalPointList, Color.Black, SymbolType.None);
                managedMemoryCurve.Line.Fill = new Fill(Color.FromArgb(70, 150, 70), Color.FromArgb(150, 230, 150), -90);
                totalMemoryCurve.Line.Fill = new Fill(Color.FromArgb(160, 120, 160), Color.FromArgb(220, 180, 220), -90);
                pane.XAxis.Scale.Max = managedPointList.Count - 1;
                pane.XAxis.Scale.MinGrace = 0;
                pane.XAxis.Scale.MaxGrace = 0;
                pane.YAxis.Scale.MinGrace = 0.05;
                pane.YAxis.Scale.MaxGrace = 0.05;
            }

            pane.AxisChange();
            graphMemory.Refresh();
        }

        private void UpdateHistory()
        {
            var labels = _summary.Runs.Select(run => run.Date.Month + "/" + run.Date.Day).ToArray();

            CreateGraph("Tests run", graphTestsRun, Color.LightSeaGreen,
                labels,
                _summary.Runs.Select(run => (double)run.TestsRun).ToArray());

            CreateGraph("Duration", graphDuration, Color.LightSteelBlue,
                labels,
                _summary.Runs.Select(run => (double)run.RunMinutes).ToArray());

            CreateGraph("Failures", graphFailures, Color.LightCoral,
                labels,
                _summary.Runs.Select(run => (double)run.Failures).ToArray());

            CreateGraph("Duration", graphMemoryHistory, Color.FromArgb(160, 120, 160),
                labels,
                _summary.Runs.Select(run => (double)run.TotalMemory).ToArray());
        }

        private int _testsRun;
        private int _revision;
        private Timer _qualityTimer;

        private void DoneQuality(bool success)
        {
            _qualityTimer.Stop();
            _qualityTimer = null;

            UpdateRun();
            _summary.Save();
            _newQualityRun = null;

            _reportDone = null;
            if (_scheduleEnd)
            {
                _scheduleEnd = false;
                success = true;
                _reportDone = QualityRestart;
            }
            TestRunnerDone(success);
        }

        private void QualityRestart(bool success)
        {
            commandShell.UpdateLog();
            ReportDone(success);
            RunQuality(null, null);
        }

        private void UpdateRun()
        {
            string lastTestResult;
            lock (_newQualityRun)
            {
                lastTestResult = _lastTestResult;
            }

            if (lastTestResult != null)
            {
                var line = Regex.Replace(lastTestResult, @"\s+", " ").Trim();
                var parts = line.Split(' ');
                var failures = int.Parse(parts[4]);
                var managedMemory = Double.Parse(parts[6].Split('/')[0]);
                var totalMemory = Double.Parse(parts[6].Split('/')[1]);

                _newQualityRun.Revision = _revision;
                _newQualityRun.RunMinutes = (int) (DateTime.Now - _newQualityRun.Date).TotalMinutes;
                _newQualityRun.TestsRun = _testsRun;
                _newQualityRun.Failures = failures;
                _newQualityRun.ManagedMemory = (int) managedMemory;
                _newQualityRun.TotalMemory = (int) totalMemory;
            }
        }

        private int GetRevision()
        {
            // Get current SVN revision info.
            int revision = 0;
            try
            {
                var skylineDirectory = GetSkylineDirectory();

                if (skylineDirectory != null)
                {
                    Process svn = new Process
                    {
                        StartInfo =
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            FileName = _subversion,
                            Arguments = @"info -r BASE " + skylineDirectory.FullName,
                            CreateNoWindow = true
                        }
                    };
                    svn.Start();
                    string svnOutput = svn.StandardOutput.ReadToEnd();
                    svn.WaitForExit();
                    var revisionString = Regex.Match(svnOutput, @".*Revision: (\d+)").Groups[1].Value;
                    revision = int.Parse(revisionString);
                }
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch
            // ReSharper restore EmptyGeneralCatchClause
            {
            }

            return revision;
        }

        private DirectoryInfo GetSkylineDirectory()
        {
            return GetSkylineDirectory(GetCurrentBuildDirectory());
        }

        private DirectoryInfo GetSkylineDirectory(string startDirectory)
        {
            string skylinePath = startDirectory;
            var skylineDirectory = skylinePath != null ? new DirectoryInfo(skylinePath) : null;
            while (skylineDirectory != null && skylineDirectory.Name != "Skyline")
                skylineDirectory = skylineDirectory.Parent;
            return skylineDirectory;
        }

        private void comboRunDate_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSelectedRun();
        }

        private void buttonOpenLog_Click(object sender, EventArgs e)
        {
            var run = GetSelectedRun();
            if (run == null)
                return;

            var logFile = _summary.GetLogFile(run);
            if (File.Exists(logFile))
            {
                var editLogFile = new Process { StartInfo = { FileName = logFile } };
                editLogFile.Start();
            }
        }

        private void buttonDeleteRun_Click(object sender, EventArgs e)
        {
            if (_qualityTimer != null)
            {
                MessageBox.Show(this, "Can't delete a run while quality pass is running.");
                return;
            }

            var run = GetSelectedRun();
            if (run != null)
            {
                var logFile = _summary.GetLogFile(run);
                if (File.Exists(logFile))
                {
                    try
                    {
                        File.Delete(logFile);
                    }
                        // ReSharper disable once EmptyGeneralCatchClause
                    catch (Exception)
                    {
                    }
                }

                _summary.Runs.Remove(run);
            }
            OpenQuality();
        }
    }
}
