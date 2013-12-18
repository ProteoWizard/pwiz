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
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
            graphMemory.GraphPane.XAxis.IsVisible = false;
            graphMemory.GraphPane.YAxis.IsVisible = true;
            graphMemory.GraphPane.YAxis.Title.Text = "MB";
            graphMemory.GraphPane.YAxis.MinorTic.IsAllTics = false;
            graphMemory.GraphPane.YAxis.Scale.FontSpec.Angle = 90;
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
            pane.XAxis.Scale.MaxGrace = 0.05;
            pane.YAxis.Scale.MaxGrace = 0.1;
            pane.XAxis.Scale.FontSpec.Angle = 90;
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
            _summary.Runs.Reverse();
            foreach (var run in _summary.Runs)
                comboRunDate.Items.Add(run.Date);
            comboRunDate.SelectedIndex = 0;
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
                    StopTestRunner();
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
            var qualityDirectory = Path.Combine(_rootDir, QualityLogsDirectory);
            if (!Directory.Exists(qualityDirectory))
                Directory.CreateDirectory(qualityDirectory);
            var summaryLog = Path.Combine(qualityDirectory, SummaryLog);
            _logFile = Path.Combine(qualityDirectory, GetLogFile(DateTime.Now));
            OpenOutput();

            if (QualityBuildFirst.Checked)
                GenerateBuildCommands();

            StartTestRunner(
                string.Format("offscreen=on culture=en-US,fr-FR summary={0}", Quote(summaryLog)) +
                    (QualityChooseTests.Checked ? GetTestList() : ""),
                DoneQuality);
        }

        private void DoneQuality(bool success)
        {
            TestRunnerDone(success);
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
            linkQualityLog.Enabled = File.Exists(logFile);
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
