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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZedGraph;

namespace SkylineTester
{
    public class TabQuality : TabBase
    {
        private Timer _updateTimer;
        private readonly List<string> _labels = new List<string>();
        private readonly List<string> _findTest = new List<string>();
        private Summary.Run _lastRun;

        public TabQuality()
        {
            WindowThumbnail.MainWindow = MainWindow;
        }

        public override void Enter()
        {
            UpdateQuality();
            MainWindow.DefaultButton = MainWindow.RunQuality;
            MainWindow.ButtonViewLog.Enabled = (MainWindow.LastRunName == "Quality" &&
                                                File.Exists(MainWindow.DefaultLogFile));
        }

        public void RunFromTestsTab()
        {
            StartQuality();
        }

        public override void Leave()
        {
            MainWindow.QualityThumbnail.ProcessId = 0;
        }

        public override bool Run()
        {
            MainWindow.LastRunName = "Quality";
            MainWindow.ButtonViewLog.Enabled = true;
            StartQuality();
            return true;
        }

        public override bool Stop(bool success)
        {
            _updateTimer.Stop();
            _updateTimer = null;

            UpdateQuality();
            MainWindow.NewNightlyRun = _lastRun = null;
            return true;
        }

        public override int Find(string text, int position)
        {
            return VerifyFind(text, position, "Quality");
        }

        public override void MemoryGraphClick(int index)
        {
            if (index < _findTest.Count)
                VerifyFind(_findTest[index], 0, "Quality");
        }

        private void StartQuality()
        {
            _labels.Clear();
            _findTest.Clear();

            MainWindow.SetStatus("Running quality pass...");
            MainWindow.ResetElapsedTime();

            MainWindow.TestsRun = 0;

            MainWindow.CommandShell.LogFile = MainWindow.DefaultLogFile;
            if (File.Exists(MainWindow.DefaultLogFile))
                Try.Multi<Exception>(() => File.Delete(MainWindow.DefaultLogFile), 4, false);
            MainWindow.NewNightlyRun = _lastRun = new Summary.Run
            {
                Date = DateTime.Now
            };

            StartLog("Quality", MainWindow.DefaultLogFile);

            _updateTimer = new Timer {Interval = 300};
            _updateTimer.Tick += (s, a) => RunUI(UpdateQuality);
            _updateTimer.Start();

            var args = "offscreen=on quality=on{0} pass0={1} pass1={2} {3}{4}{5}".With(
                MainWindow.QualityPassDefinite.Checked ? " loop=" + int.Parse(MainWindow.QualityPassCount.Text) : "",
                MainWindow.Pass0.Checked.ToString(),
                MainWindow.Pass1.Checked.ToString(),
                MainWindow.QualityChooseTests.Checked ? TabTests.GetTestList() : "",
                MainWindow.QualityChooseTests.Checked ? " perftests=on" : "",  // In case any perf tests are explicitly selected - no harm if they aren't
                MainWindow.QualtityTestSmallMolecules.Checked ? " testsmallmolecules=on" : "");
            MainWindow.AddTestRunner(args);

            MainWindow.RunCommands();
        }

        private void UpdateQuality()
        {
            if (MainWindow.Tabs.SelectedTab != MainWindow.QualityPage || _lastRun == null)
                return;

            UpdateThumbnail();
            UpdateRun();
            UpdateGraph();
        }

        private void UpdateThumbnail()
        {
            MainWindow.QualityThumbnail.ProcessId = MainWindow.TestRunnerProcessId;
            MainWindow.QualityTestName.Text = MainWindow.TestRunnerProcessId != 0
                ? MainWindow.RunningTestName
                : null;
        }

        private BackgroundWorker _updateWorker;

        private void UpdateGraph()
        {
            var pane = MainWindow.GraphMemory.GraphPane;
            pane.CurveList.Clear();

            if (_lastRun == null)
            {
                MainWindow.LabelDuration.Text = "";
                MainWindow.LabelTestsRun.Text = "";
                MainWindow.LabelFailures.Text = "";
                MainWindow.LabelLeaks.Text = "";
                MainWindow.GraphMemory.Refresh();
                return;
            }

            MainWindow.LabelDuration.Text = (_lastRun.RunMinutes / 60) + ":" + (_lastRun.RunMinutes % 60).ToString("D2");
            MainWindow.LabelTestsRun.Text = _lastRun.TestsRun.ToString(CultureInfo.InvariantCulture);
            MainWindow.LabelFailures.Text = _lastRun.Failures.ToString(CultureInfo.InvariantCulture);
            MainWindow.LabelLeaks.Text = _lastRun.Leaks.ToString(CultureInfo.InvariantCulture);

            if (_updateWorker != null)
                return;

            _updateWorker = new BackgroundWorker();
            _updateWorker.DoWork += (sender, args) =>
            {
                var managedPointList = new PointPairList();
                var totalPointList = new PointPairList();

                var logFile = MainWindow.DefaultLogFile;
                _labels.Clear();
                _findTest.Clear();
                if (File.Exists(logFile))
                {
                    string[] logLines;
                    lock (MainWindow.CommandShell.LogLock)
                    {
                        logLines = File.ReadAllLines(logFile);
                    }

                    foreach (var line in logLines)
                    {
                        if (line.Length > 6 && line[0] == '[' && line[3] == ':' && line[6] == ']')
                        {
                            var i = line.IndexOf("failures, ", StringComparison.OrdinalIgnoreCase);
                            if (i < 0)
                                continue;

                            var testNumber = line.Substring(8, 7).Trim();
                            var testName = line.Substring(16, 46).TrimEnd();
                            var memory = line.Substring(i + 10).Split('/');
                            var managedMemory = double.Parse(memory[0]);
                            var totalMemory = double.Parse(memory[1].Split(' ')[0]);
                            var managedTag = "{0} MB\n{1:F2} {2:F1}".With(managedMemory, testNumber, testName);
                            var totalTag = "{0} MB\n{1:F2} {2:F1}".With(totalMemory, testNumber, testName);

                            if (managedPointList.Count > 0 && _labels[_labels.Count - 1] == testNumber)
                            {
                                managedPointList[managedPointList.Count - 1].Y = managedMemory;
                                totalPointList[totalPointList.Count - 1].Y = totalMemory;
                                managedPointList[managedPointList.Count - 1].Tag = managedTag;
                                totalPointList[totalPointList.Count - 1].Tag = totalTag;
                            }
                            else
                            {
                                _labels.Add(testNumber);
                                _findTest.Add(line.Substring(8, 54).TrimEnd() + " ");
                                managedPointList.Add(managedPointList.Count, managedMemory, managedTag);
                                totalPointList.Add(totalPointList.Count, totalMemory, totalTag);
                            }
                        }
                    }
                }

                RunUI(() =>
                {
                    pane.CurveList.Clear();

                    try
                    {
                        pane.XAxis.Scale.Min = 1;
                        pane.XAxis.Scale.Max = managedPointList.Count;
                        pane.XAxis.Scale.MinGrace = 0;
                        pane.XAxis.Scale.MaxGrace = 0;
                        pane.YAxis.Scale.MinGrace = 0.05;
                        pane.YAxis.Scale.MaxGrace = 0.05;
                        pane.XAxis.Scale.TextLabels = _labels.ToArray();
                        pane.Legend.FontSpec.Size = 11;
                        pane.Title.FontSpec.Size = 13;
                        pane.XAxis.Title.FontSpec.Size = 11;
                        pane.XAxis.Scale.FontSpec.Size = 11;

                        var managedMemoryCurve = pane.AddCurve("Managed", managedPointList, Color.Black, SymbolType.None);
                        var totalMemoryCurve = pane.AddCurve("Total", totalPointList, Color.Black, SymbolType.None);
                        managedMemoryCurve.Line.Fill = new Fill(Color.FromArgb(70, 150, 70), Color.FromArgb(150, 230, 150), -90);
                        totalMemoryCurve.Line.Fill = new Fill(Color.FromArgb(160, 120, 160), Color.FromArgb(220, 180, 220), -90);

                        pane.AxisChange();
                        MainWindow.GraphMemory.Refresh();
                    }
// ReSharper disable once EmptyGeneralCatchClause
                    catch (Exception)
                    {
                        // Weird: I got an exception assigning to TextLabels once.  No need
                        // to kill a whole quality run for that.
                    }
                });

                _updateWorker = null;
            };

            _updateWorker.RunWorkerAsync();
        }

        private void UpdateRun()
        {
            if (_lastRun == null)
                return;

            string lastTestResult;
            lock (_lastRun)
            {
                lastTestResult = MainWindow.LastTestResult;
            }

            if (lastTestResult != null)
            {
                var line = Regex.Replace(lastTestResult, @"\s+", " ").Trim();
                var parts = line.Split(' ');
                var failures = int.Parse(parts[4]);
                var managedMemory = Double.Parse(parts[6].Split('/')[0]);
                var totalMemory = Double.Parse(parts[6].Split('/')[1]);

                _lastRun.RunMinutes = (int)(DateTime.Now - _lastRun.Date).TotalMinutes;
                _lastRun.TestsRun = MainWindow.TestsRun;
                _lastRun.Failures = failures;
                _lastRun.ManagedMemory = (int)managedMemory;
                _lastRun.TotalMemory = (int)totalMemory;
            }
        }
    }
}
