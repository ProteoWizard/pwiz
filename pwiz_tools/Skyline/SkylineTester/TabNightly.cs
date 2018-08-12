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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32.TaskScheduler;
using ZedGraph;

namespace SkylineTester
{
    public class TabNightly : TabBase, SkylineTesterWindow.IMemoryGraphContainer
    {
        public const string NIGHTLY_TASK_NAME = "SkylineTester scheduled run"; // Not to be confused with the SkylineNightly task

        private const int MINUTES_PER_INCREMENT = 60; // 1 hour

        private Timer _updateTimer;
        private Timer _stopTimer;
        private string _revision;
        private SkylineTesterWindow.BuildDirs _saveSelectedBuild; 
        private readonly List<string> _labels = new List<string>();
        private readonly List<string> _findTest = new List<string>();

        public TabNightly()
        {
            WindowThumbnail.MainWindow = MainWindow;
            MainWindow.InitLogSelector(MainWindow.NightlyRunDate, MainWindow.NightlyViewLog);
            MainWindow.NightlyLogFile = (MainWindow.Summary.Runs.Count > 0) 
                ? MainWindow.Summary.GetLogFile(MainWindow.Summary.Runs[MainWindow.Summary.Runs.Count - 1]) 
                : null;

            MainWindow.InitNightly();
            MainWindow.DefaultButton = MainWindow.RunNightly;
        }

        public override void Enter()
        {
            if (MainWindow.NightlyRunDate.SelectedIndex == -1 && MainWindow.NightlyRunDate.Items.Count > 0)
                MainWindow.NightlyRunDate.SelectedIndex = 0;
            if (MainWindow.NightlyBuildType.SelectedIndex == -1)
                MainWindow.NightlyBuildType.SelectedIndex = 0;

            MainWindow.NightlyDeleteRun.Enabled = MainWindow.Summary.Runs.Count > 0;

            UpdateThumbnail();
            UpdateGraph();
            UpdateHistory();

            using (TaskService ts = new TaskService())
            {
                var task = ts.FindTask(NIGHTLY_TASK_NAME);
                MainWindow.DeleteNightlyTask.Enabled = (task != null);
            }
        }

        public override void Leave()
        {
            MainWindow.NightlyThumbnail.ProcessId = 0;
        }

        public override bool Run()
        {
            if (!MainWindow.HasBuildPrerequisites)
                return false;

            // When run from SkylineNightly, don't overwrite the nightly scheduled task.  Just start the nightly run immediately.
            if (MainWindow.NightlyExit.Checked)
            {
                RunUI(StartNightly, 500);
                return true;
            }

            if (MainWindow.NightlyRunIndefinitely.Checked)
                MainWindow.NightlyStartTime.Value = DateTime.Now;

            var startTime = DateTime.Parse(MainWindow.NightlyStartTime.Text);

            if (MainWindow.ShiftKeyPressed)
            {
                var result = MessageBox.Show(
                    MainWindow, 
                    "Nightly build will start 15 seconds after you press OK.", 
                    "Run nightly",
                    MessageBoxButtons.OKCancel);
                if (result == DialogResult.Cancel)
                    return false;
                startTime = DateTime.Now + TimeSpan.FromSeconds(15);
                MainWindow.NightlyStartTime.Value = startTime;
            }

            var skytFile = Path.Combine(MainWindow.ExeDir, "SkylineNightly.skytr");
            MainWindow.Save(skytFile);

            if (!MainWindow.NightlyRunIndefinitely.Checked)
                ScheduleTask(startTime, skytFile);

            if (MainWindow.ShiftKeyPressed)
            {
                MainWindow.Close();
                return false;
            }

            if (startTime <= DateTime.Now && startTime + TimeSpan.FromMinutes(5) > DateTime.Now)
            {
                RunUI(StartNightly, 500);
                return true;
            }

            var hours = (startTime - DateTime.Now).Hours;
            var minutes = (startTime - DateTime.Now).Minutes;
            if (hours < 0)
                hours += 24;
            var delay = (hours > 0) ? hours : minutes;
            var units = (hours > 0) ? "hours" : "minutes";
            MainWindow.SetStatus("SkylineTester scheduled build will start about {0} {1} from now ({2}).  If you also have a scheduled SkylineNightly run on this machine make sure they don't overlap.".With(delay, units, DateTime.Now.ToString(CultureInfo.InvariantCulture)));
            return false;
        }

        private static void ScheduleTask(DateTime startTime, string skytFile)
        {
            using (TaskService ts = new TaskService())
            {
                // Create a new task definition and assign properties
                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "SkylineTester scheduled build/test";
                td.Principal.LogonType = TaskLogonType.InteractiveToken;

                // Using ProcessPriorityClass.High seems like cheating, but it's not:
                // A normal user-initiated app has
                //   TaskPriority = 8, I/O Priority = Normal, Memory Priority = 5
                // Default priority for Task Scheduler launch provides
                //   TaskPriority = 6, I/O Priority = Low, Memory Priority = 3
                //ProcessPriorityClass.Normal provides the launched task with
                //   TaskPriority = 8, I/O Priority = Normal, Memory Priority = 4 (not quite as good as user-launched)
                // ProcessPriorityClass.High provides SkylineTester with
                //   TaskPriority = 13, I/O Priority = Normal, Memory Priority = 5
                // but gives TestRunner the standard user values of
                //   TaskPriority = 8, I/O Priority = Normal, Memory Priority = 5
                td.Settings.Priority = ProcessPriorityClass.High;

                // Add a trigger that will fire the task every other day
                DailyTrigger dt = (DailyTrigger) td.Triggers.Add(new DailyTrigger {DaysInterval = 1});
                dt.StartBoundary = startTime;
                dt.ExecutionTimeLimit = new TimeSpan(23, 30, 0);
                dt.Enabled = true;
                bool canWakeToRun = false;
                try
                {
                    td.Settings.WakeToRun = true;
                    canWakeToRun = true;
                }
// ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }

                if (!canWakeToRun)
                    MessageBox.Show(
                        "Warning: There was an error creating a task that can wake your computer from sleep." +
                        " You can use the Task Scheduler to modify the task to wake up, or make sure your computer is awake at " +
                        startTime.ToShortTimeString());

                // Add an action that will launch SkylineTester whenever the trigger fires
                td.Actions.Add(new ExecAction(MainWindow.Exe, skytFile.Quote(), MainWindow.ExeDir));

                // Register the task in the root folder
                ts.RootFolder.RegisterTaskDefinition(NIGHTLY_TASK_NAME, td);
            }
            MainWindow.DeleteNightlyTask.Enabled = true;
        }

        public override bool Stop(bool success)
        {
            MainWindow.SelectBuild(
                success
                ? (_architecture == 32 ? SkylineTesterWindow.BuildDirs.nightly32 : SkylineTesterWindow.BuildDirs.nightly64)
                : _saveSelectedBuild);

            _updateTimer.Stop();
            _updateTimer = null;

            if (_stopTimer != null)
            {
                _stopTimer.Stop();
                _stopTimer = null;
            }

            UpdateNightly();
            MainWindow.Summary.Save();
            MainWindow.NewNightlyRun = null;

            return true;
        }

        public override void Stopped()
        {
            // Once everything is fully stopped, close the main window, if appropriate
            if (MainWindow.NightlyExit.Checked)
                RunUI(() => MainWindow.Close());
        }

        public override void MemoryGraphClick(int index)
        {
            if (index < _findTest.Count)
                Find(_findTest[index], 0);
        }

        public override void Cancel()
        {
            bool runAgain = MainWindow.NightlyRunIndefinitely.Checked;
            if (Math.Abs(MainWindow.RunElapsedTime.TotalMinutes - (int) MainWindow.NightlyDuration.Value * MINUTES_PER_INCREMENT) > 5)
                runAgain = false;

            base.Cancel();

            if (runAgain)
            {
                // Automaticly run again, but delayed to allow everything else to close before
                // trying to delete all the files of the project that was just running.
                // The recursive deletion can leave the directory locked and denying access
                // until the computer is restarted.
                _runAgainTimer = new Timer { Interval = 30 * 1000 };    // 30 seconds - to be safe
                _runAgainTimer.Tick += (o, args) =>
                {
                    _runAgainTimer.Stop();
                    _runAgainTimer = null;
                    MainWindow.Run();
                };
                _runAgainTimer.Start();

            }
        }

        private Timer _runAgainTimer;

        private void StartNightly()
        {
            _labels.Clear();
            _findTest.Clear();

            if (File.Exists(MainWindow.DefaultLogFile))
                Try.Multi<Exception>(() => File.Delete(MainWindow.DefaultLogFile), 4, false);
            var logsDirectory = MainWindow.GetLogsDir();
            if (!Directory.Exists(logsDirectory))
                Directory.CreateDirectory(logsDirectory);

            MainWindow.SetStatus("Running nightly pass...");
            var architecture = MainWindow.NightlyBuildType.SelectedIndex == 0 ? 32 : 64;
            MainWindow.SelectBuild(architecture == 32 ? SkylineTesterWindow.BuildDirs.nightly32 : SkylineTesterWindow.BuildDirs.nightly64);
            _saveSelectedBuild = MainWindow.SelectedBuild;
            MainWindow.ResetElapsedTime();

            MainWindow.TestsRun = 0;
            MainWindow.LastTestResult = null;
            MainWindow.NewNightlyRun = new Summary.Run
            {
                Date = DateTime.Now
            };
            MainWindow.Summary.Runs.Add(MainWindow.NewNightlyRun);
            MainWindow.AddRun(MainWindow.NewNightlyRun, MainWindow.NightlyRunDate);
            MainWindow.NightlyRunDate.SelectedIndex = 0;

            StartLog("Nightly", MainWindow.Summary.GetLogFile(MainWindow.NewNightlyRun));

            var revisionWorker = new BackgroundWorker();
            revisionWorker.DoWork += (s, a) => _revision = GetRevision(true);
            revisionWorker.RunWorkerAsync();

            _updateTimer = new Timer {Interval = 300};
            _updateTimer.Tick += (s, a) => RunUI(() =>
            {
                try
                {
                    UpdateNightly();
                }
                catch (Exception x)
                {
                    _updateTimer.Stop();

                    MessageBox.Show(string.Format("Unexpected Error: {0}", x));

                    Stop(false);
                }
            });

            _stopTimer = new Timer {Interval = (int) MainWindow.NightlyDuration.Value*MINUTES_PER_INCREMENT*60*1000};   // Interval in milliseconds
            _stopTimer.Tick += (s, a) => RunUI(() =>
            {
                if (_stopTimer != null)
                {
                    _stopTimer.Stop();
                    _stopTimer = null;
                }
                MainWindow.Stop();
            });

            _architecture = (MainWindow.NightlyBuildType.SelectedIndex == 0)
                ? 32
                : 64;
            var architectureList = new[] {_architecture};
            var branchUrl = MainWindow.NightlyBuildTrunk.Checked
                ? @"https://github.com/ProteoWizard/pwiz"
                : MainWindow.NightlyBranchUrl.Text;
            var buildRoot = Path.Combine(MainWindow.GetNightlyBuildRoot(), "pwiz");
            TabBuild.CreateBuildCommands(branchUrl, buildRoot, architectureList, true, false, false); // Just build Skyline.exe without testing it - that's about to happen anyway

            int stressTestLoopCount;
            if (!int.TryParse(MainWindow.NightlyRepeat.Text, out stressTestLoopCount))
                stressTestLoopCount = 0;
            MainWindow.AddTestRunner("offscreen=on quality=on loop=-1 " +
                (stressTestLoopCount > 1 || MainWindow.NightlyRunPerfTests.Checked ? "pass0=off pass1=off " : "pass0=on pass1=on ") + // Skip the special passes if we're here to do stresstests or perftests
                (MainWindow.NightlyRunPerfTests.Checked ? " perftests=on" : string.Empty) +
                (MainWindow.NightlyTestSmallMolecules.Checked ? " testsmallmolecules=on" : string.Empty) +
                " runsmallmoleculeversions=on" + // Run any provided tests that convert the document to small molecules (this is different from testsmallmolecules, which just adds the magic test node to every doc in every test)
                (MainWindow.NightlyRandomize.Checked ? " random=on" : " random=off") +
                (stressTestLoopCount > 1 ? " repeat=" + MainWindow.NightlyRepeat.Text : string.Empty));
            MainWindow.CommandShell.Add("# Nightly finished.");

            MainWindow.RunCommands();

            if (_updateTimer != null)
                _updateTimer.Start();
            if (_stopTimer != null)
                _stopTimer.Start();
        }

        private int _architecture;

        private void UpdateNightly()
        {
            UpdateRun();

            if (MainWindow.Tabs.SelectedIndex != MainWindow.NightlyTabIndex)
                return;

            UpdateThumbnail();
            if (MainWindow.NightlyRunDate.SelectedIndex == 0)
                UpdateGraph();
            UpdateHistory();
        }

        private void UpdateThumbnail()
        {
            MainWindow.NightlyThumbnail.ProcessId = _updateTimer != null ? MainWindow.TestRunnerProcessId : 0;
            MainWindow.NightlyTestName.Text = _updateTimer != null && MainWindow.TestRunnerProcessId != 0
                ? MainWindow.RunningTestName
                : null;
        }

        private Summary.Run GetSelectedRun()
        {
            var run = MainWindow.NightlyRunDate.SelectedIndex >= 0
                ? MainWindow.Summary.Runs[MainWindow.Summary.Runs.Count - 1 - MainWindow.NightlyRunDate.SelectedIndex]
                : null;
            MainWindow.NightlyLogFile = (run != null) ? MainWindow.Summary.GetLogFile(run) : null;
            return run;
        }

        public void UpdateGraph()
        {
            MainWindow.UpdateMemoryGraph(this);
        }

        private void UpdateHistory()
        {
            var labels = MainWindow.Summary.Runs.Select(run => run.Date.Month + "/" + run.Date.Day).ToArray();

            CreateGraph("Tests run", MainWindow.GraphTestsRun, Color.LightSeaGreen,
                labels,
                MainWindow.Summary.Runs.Select(run => (double)run.TestsRun).ToArray());

            CreateGraph("Duration", MainWindow.GraphDuration, Color.LightSteelBlue,
                labels,
                MainWindow.Summary.Runs.Select(run => (double)run.RunMinutes).ToArray());

            CreateGraph("Failures", MainWindow.GraphFailures, Color.LightCoral,
                labels,
                MainWindow.Summary.Runs.Select(run => (double)run.Failures).ToArray());

            CreateGraph("Duration", MainWindow.GraphMemoryHistory, Color.FromArgb(160, 120, 160),
                labels,
                MainWindow.Summary.Runs.Select(run => (double)run.TotalMemory).ToArray());
        }

        private void UpdateRun()
        {
            string lastTestResult;
            lock (MainWindow.NewNightlyRun)
            {
                lastTestResult = MainWindow.LastTestResult;
            }

            if (lastTestResult != null)
            {
                var runFromLine = Summary.ParseRunFromStatusLine(lastTestResult);

                var lastRun = MainWindow.NewNightlyRun;
                lastRun.Revision = _revision;
                lastRun.RunMinutes = (int)(runFromLine.Date - lastRun.Date).TotalMinutes;
                lastRun.TestsRun = MainWindow.TestsRun;
                lastRun.ManagedMemory = runFromLine.ManagedMemory;
                lastRun.TotalMemory = runFromLine.TotalMemory;
                lastRun.UserHandles = runFromLine.UserHandles;
                lastRun.GdiHandles = runFromLine.GdiHandles;
            }
        }

        private static double? ParseMemory(string memoryText)
        {
            double memory;
            if (double.TryParse(memoryText, out memory))
                return memory;
            return null;
        }

        private string GetRevision(bool nuke)
        {
            // Get current git revision info in form of git hash.
            string revision = String.Empty;
            try
            {
                var buildRoot = MainWindow.GetBuildRoot()+"\\pwiz";
                if (Directory.Exists(buildRoot) && !nuke)
                {
                    revision = GitCommand(buildRoot, @"rev-parse HEAD"); // Commit hash for local repo
                }
                else
                {
                    revision = GitCommand(".", @"ls-remote -h " + TabBuild.GetBranchUrl()).Split(' ', '\t')[0]; // Commit hash for github repo
                }
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            return revision;
        }

        private static string GitCommand(string workingdir, string cmd)
        {
            Process git = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = MainWindow.Git,
                    WorkingDirectory = workingdir,
                    Arguments = cmd,
                    CreateNoWindow = true
                }
            };
            git.Start();
            var gitOutput = git.StandardOutput.ReadToEnd();
            git.WaitForExit();
            return gitOutput;
        }

        public void RunDateChanged()
        {
            UpdateGraph();
        }

        public void DeleteRun()
        {
            if (_updateTimer != null)
            {
                MessageBox.Show(MainWindow, "Can't delete a run while nightly pass is running.");
                return;
            }

            if (MessageBox.Show(
                MainWindow,
                "Delete log for " + MainWindow.NightlyRunDate.Text.Replace("  ", " ") + "?",
                "Confirm delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.No)
                return;

            var run = GetSelectedRun();
            if (run != null)
            {
                var logFile = MainWindow.Summary.GetLogFile(run);
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

                MainWindow.Summary.Runs.Remove(run);
            }

            MainWindow.NightlyDeleteRun.Enabled = MainWindow.Summary.Runs.Count > 0;

            int selectedIndex = MainWindow.NightlyRunDate.SelectedIndex;
            MainWindow.InitLogSelector(MainWindow.NightlyRunDate, MainWindow.NightlyViewLog);
            MainWindow.NightlyRunDate.SelectedIndex = Math.Min(selectedIndex, MainWindow.NightlyRunDate.Items.Count - 1);
            UpdateGraph();
            UpdateHistory();
        }

        public void DeleteBuild()
        {
            var root = MainWindow.GetNightlyRoot();
            if (!Directory.Exists(root) ||
                MessageBox.Show(MainWindow, "Delete \"" + root + "\" folder?", "Confirm delete",
                    MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                return;
            }

            using (var deleteWindow = new DeleteWindow(root))
            {
                deleteWindow.ShowDialog();
            }
        }


        public void BrowseBuild()
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Select or create a root folder for build source files.",
                ShowNewFolderButton = true
            })
            {
                if (dlg.ShowDialog(MainWindow) == DialogResult.OK)
                {
                    var nightlyRoot = dlg.SelectedPath;
                    var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (nightlyRoot.StartsWith(userFolder))
                        nightlyRoot = nightlyRoot.Remove(0, userFolder.Length+1);
                    MainWindow.NightlyRoot.Text = nightlyRoot;

                    MainWindow.Summary = null;
                    MainWindow.InitLogSelector(MainWindow.NightlyRunDate, MainWindow.NightlyViewLog);
                    MainWindow.NightlyLogFile = (MainWindow.Summary.Runs.Count > 0)
                        ? MainWindow.Summary.GetLogFile(MainWindow.Summary.Runs[MainWindow.Summary.Runs.Count - 1])
                        : null;

                    MainWindow.InitNightly();
                    Enter();
                }
            }
        }

        private void CreateGraph(string name, ZedGraphControl graph, Color color, string[] labels, double[] data)
        {
            graph.IsShowPointValues = true;
            graph.PointValueEvent += GraphOnPointValueEvent;
            graph.MouseDownEvent += GraphOnMouseDownEvent;
            graph.MouseUpEvent += GraphOnMouseUpEvent;
            graph.MouseMoveEvent += GraphMouseMoveEvent;
            var pane = graph.GraphPane;
            pane.CurveList.Clear();
            var bars = pane.AddBar(name, null, data, color);
            for (int i = 0; i < bars.NPts; i++)
                bars[i].Tag = "{0}  ({1})".With(data[i], labels[i]);
            bars.Bar.Fill = new Fill(color);
            pane.Title.FontSpec.Size = 11;
            pane.XAxis.Scale.TextLabels = labels;
            pane.XAxis.Type = AxisType.Text;
            pane.XAxis.Scale.FontSpec.Size = 10;
            pane.XAxis.Scale.Align = AlignP.Inside;
            pane.AxisChange();
            graph.Refresh();
        }

        private bool GraphMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            CurveItem nearestCurve;
            int index;
            if (sender.GraphPane.FindNearestPoint(new PointF(e.X, e.Y), out nearestCurve, out index))
                sender.Cursor = Cursors.Hand;
            return false;
        }

        private int _cursorIndex;

        private string GraphOnPointValueEvent(ZedGraphControl sender, GraphPane pane, CurveItem curve, int iPt)
        {
            _cursorIndex = iPt;
            return (string)curve[iPt].Tag;
        }

        private Point _mouseDownLocation;

        private bool GraphOnMouseUpEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            if (mouseEventArgs.Button == MouseButtons.Left && mouseEventArgs.Location == _mouseDownLocation)
            {
                sender.Refresh();
                MainWindow.NightlyRunDate.SelectedIndex = MainWindow.NightlyRunDate.Items.Count - 1 - _cursorIndex;
            }
            return false;
        }

        private bool GraphOnMouseDownEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            if (mouseEventArgs.Button == MouseButtons.Left)
                _mouseDownLocation = mouseEventArgs.Location;
            return false;
        }


        SkylineTesterWindow.MemoryGraphLocation SkylineTesterWindow.IMemoryGraphContainer.Location
        {
            get { return SkylineTesterWindow.MemoryGraphLocation.nightly; }
        }

        Summary.Run SkylineTesterWindow.IMemoryGraphContainer.CurrentRun
        {
            get { return GetSelectedRun(); }
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
            get { return false; }
        }

        BackgroundWorker SkylineTesterWindow.IMemoryGraphContainer.UpdateWorker { get; set; }
    }
}
