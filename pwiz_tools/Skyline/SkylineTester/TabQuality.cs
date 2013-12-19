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
using TestRunnerLib;
using ZedGraph;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private const string QualityLogsDirectory = "Quality logs";
        private const string SummaryLog = "Summary.log";

        private Timer _startTimer;
        private Timer _endTimer;

        private void InitQuality()
        {
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
            var summaryLog = Path.Combine(_rootDir, QualityLogsDirectory, SummaryLog);
            if (!File.Exists(summaryLog))
                return;

            _summary = new Summary();
            _summary.Load(summaryLog);
            comboRunDate.Items.Clear();

            // Show latest 30 runs, max.
            if (_summary.Runs.Count > 30)
                _summary.Runs.RemoveRange(0, _summary.Runs.Count - 30);
            var runsChronological = new Summary.Run[_summary.Runs.Count];
            _summary.Runs.CopyTo(runsChronological);
            _summary.Runs.Reverse();
            foreach (var run in _summary.Runs)
                comboRunDate.Items.Add(run.Date);
            comboRunDate.SelectedIndex = 0;

            var labels = runsChronological.Select(run => run.Date.Month + "/" + run.Date.Day).ToArray();
            
            CreateGraph("Tests run", graphTestsRun, Color.LightSeaGreen,
                labels,
                runsChronological.Select(run => (double)run.TestsRun).ToArray());

            CreateGraph("Duration", graphDuration, Color.LightSteelBlue,
                labels,
                runsChronological.Select(run => (double)run.RunMinutes).ToArray());

            CreateGraph("Failures", graphFailures, Color.LightCoral,
                labels,
                runsChronological.Select(run => (double)run.Failures).ToArray());

            CreateGraph("Duration", graphMemoryHistory, Color.FromArgb(160, 120, 160),
                labels,
                runsChronological.Select(run => (double)run.TotalMemory).ToArray());
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
            if (QualityBuildFirst.Checked && !HasBuildPrerequisites)
                return;

            if (!ToggleRunButtons(tabQuality))
            {
                if (_endTimer != null)
                {
                    _endTimer.Stop();
                    _endTimer = null;
                }
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

            _logFile = "";
            Tabs.SelectTab(tabOutput);
            commandShell.ClearLog();

            if (QualityStartNow.Checked)
            {
                StartQuality();
                return;
            }

            commandShell.AddImmediate(Environment.NewLine + "# Waiting until {0} to start quality pass...", QualityStartTime.Text);

            var startTime = GetDateTime(QualityStartTime.Text);
            if (startTime < DateTime.Now)
                startTime = startTime.AddDays(1);
            var endTime = GetDateTime(QualityEndTime.Text);
            while (endTime < startTime)
                endTime = endTime.AddDays(1);
            _startTimer = new Timer
            {
                Interval = (int)(startTime - DateTime.Now).TotalMilliseconds
            };
            _startTimer.Tick += (o, args) =>
            {
                _startTimer.Stop();
                _startTimer = null;
                StartQuality();
                ScheduleEnd(endTime);
            };
            _startTimer.Start();
        }

        private DateTime GetDateTime(string time)
        {
            var timeParts = time.Split(':');
            var hour = int.Parse(timeParts[0]);
            if (timeParts[1].ToLower().Contains("pm"))
                hour += 12;
            var minute = int.Parse(timeParts[1].Split(' ')[0]);
            var now = DateTime.Now;
            return new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
        }

        private void ScheduleEnd(DateTime endTime)
        {
            _endTimer = new Timer
            {
                Interval = (int)(endTime - DateTime.Now).TotalMilliseconds
            };
            _endTimer.Tick += (o, args) =>
            {
                _endTimer.Stop();
                _endTimer = null;
                Invoke(new Action(() => Stop(null, null)));
            };
            _endTimer.Start();
        }

        private void StartQuality()
        {
            _qualityStartDate = DateTime.Now;
            _testsRun = 0;
            _lastTestResult = null;

            var revisionWorker = new BackgroundWorker();
            revisionWorker.DoWork += (sender, args) => _revision = GetRevision();
            revisionWorker.RunWorkerAsync();

            var qualityDirectory = Path.Combine(_rootDir, QualityLogsDirectory);
            if (!Directory.Exists(qualityDirectory))
                Directory.CreateDirectory(qualityDirectory);
            _logFile = Path.Combine(qualityDirectory, GetLogFile(_qualityStartDate));
            OpenOutput();

            commandShell.Add("# Quality run started {0}" + Environment.NewLine, _qualityStartDate.ToString("f"));

            if (QualityBuildFirst.Checked)
                GenerateBuildCommands();

            StartTestRunner(
                "offscreen=on culture=en-US,fr-FR" + (QualityChooseTests.Checked ? GetTestList() : ""),
                DoneQuality);
        }

        private DateTime _qualityStartDate;
        private int _testsRun;
        private int _revision;

        private void DoneQuality(bool success)
        {
            if (_lastTestResult != null)
            {
                var elapsedMinutes = (DateTime.Now - _qualityStartDate).TotalMinutes;
                var qualityDirectory = Path.Combine(_rootDir, QualityLogsDirectory);
                var summaryLog = Path.Combine(qualityDirectory, SummaryLog);

                var line = Regex.Replace(_lastTestResult, @"\s+", " ").Trim();
                var parts = line.Split(' ');
                var failures = int.Parse(parts[4]);
                var managedMemory = Double.Parse(parts[6].Split('/')[0]);
                var totalMemory = Double.Parse(parts[6].Split('/')[1]);

                var summary = new Summary();
                summary.Load(summaryLog);
                summary.Runs.Add(new Summary.Run
                {
                    Date = _qualityStartDate,
                    Revision = _revision,
                    RunMinutes = (int) elapsedMinutes,
                    TestsRun = _testsRun,
                    Failures = failures,
                    ManagedMemory = (int) managedMemory,
                    TotalMemory = (int) totalMemory
                });
                summary.Save(summaryLog);
            }

            TestRunnerDone(success);
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
                            Arguments = @"info -r HEAD " + skylineDirectory.FullName,
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
            string skylinePath = GetCurrentBuildDirectory();
            var skylineDirectory = skylinePath != null ? new DirectoryInfo(skylinePath) : null;
            while (skylineDirectory != null && skylineDirectory.Name != "Skyline")
                skylineDirectory = skylineDirectory.Parent;
            return skylineDirectory;
        }

        private void comboRunDate_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = comboRunDate.SelectedIndex;
            var run = _summary.Runs[index];
            labelRevision.Text = run.Revision.ToString(CultureInfo.InvariantCulture);
            labelDuration.Text = (run.RunMinutes / 60) + ":" + (run.RunMinutes % 60).ToString("D2");
            labelTestsRun.Text = run.TestsRun.ToString(CultureInfo.InvariantCulture);
            labelFailures.Text = run.Failures.ToString(CultureInfo.InvariantCulture);
            labelLeaks.Text = run.Leaks.ToString(CultureInfo.InvariantCulture);
            var logFile = Path.Combine(_rootDir, QualityLogsDirectory, GetLogFile(run.Date));
            bool hasLogFile = linkQualityLog.Enabled = File.Exists(logFile);

            var pane = graphMemory.GraphPane;
            pane.CurveList.Clear();

            if (hasLogFile)
            {
                var managedPointList = new PointPairList();
                var totalPointList = new PointPairList();

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
                pane.XAxis.Scale.Max = managedPointList.Count;
                pane.XAxis.Scale.MinGrace = 0;
                pane.XAxis.Scale.MaxGrace = 0;
                pane.YAxis.Scale.MinGrace = 0.05;
                pane.YAxis.Scale.MaxGrace = 0.05;
            }

            pane.AxisChange();
            graphMemory.Refresh();
        }

        private string GetLogFile(DateTime date)
        {
            return string.Format("{0}-{1:D2}-{2:D2}_{3:D2}-{4:D2}.log",
                date.Year, date.Month, date.Day, date.Hour, date.Minute);
        }

        private void linkQualityLog_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int index = comboRunDate.SelectedIndex;
            var run = _summary.Runs[index];
            var logFile = Path.Combine(_rootDir, QualityLogsDirectory, GetLogFile(run.Date));
            if (File.Exists(logFile))
            {
                var editLogFile = new Process { StartInfo = { FileName = logFile } };
                editLogFile.Start();
            }
        }
    }
}
