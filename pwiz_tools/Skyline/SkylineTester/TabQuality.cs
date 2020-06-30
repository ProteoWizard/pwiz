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
using System.IO;
using System.Windows.Forms;

namespace SkylineTester
{
    public class TabQuality : TabBase, SkylineTesterWindow.IMemoryGraphContainer
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
                MainWindow.QualityRunSmallMoleculeVersions.Checked ? " runsmallmoleculeversions=on" : "");
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

        public void UpdateGraph()
        {
            MainWindow.UpdateMemoryGraph(this);
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
                var runFromLine = Summary.ParseRunFromStatusLine(lastTestResult);

                _lastRun.RunMinutes = (int)(runFromLine.Date - _lastRun.Date).TotalMinutes;
                _lastRun.TestsRun = MainWindow.TestsRun;
                _lastRun.Failures = runFromLine.Failures;
                _lastRun.ManagedMemory = runFromLine.ManagedMemory;
                _lastRun.CommittedMemory = runFromLine.CommittedMemory;
                _lastRun.TotalMemory = runFromLine.TotalMemory;
                _lastRun.UserHandles = runFromLine.UserHandles;
                _lastRun.GdiHandles = runFromLine.GdiHandles;
            }
        }

        SkylineTesterWindow.MemoryGraphLocation SkylineTesterWindow.IMemoryGraphContainer.Location
        {
            get { return SkylineTesterWindow.MemoryGraphLocation.quality; }
        }

        Summary.Run SkylineTesterWindow.IMemoryGraphContainer.CurrentRun
        {
            get { return _lastRun; }
        }

        List<string> SkylineTesterWindow.IMemoryGraphContainer.Labels
        {
            get { return _labels; }
        }

        List<string> SkylineTesterWindow.IMemoryGraphContainer.FindTest
        {
            get { return _findTest; }
        }

        public bool UseRunningLogFile
        {
            get { return true; }
        }

        BackgroundWorker SkylineTesterWindow.IMemoryGraphContainer.UpdateWorker { get; set; }
    }
}
