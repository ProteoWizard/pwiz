/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using SkylineNightly.Properties;

namespace SkylineNightly
{
    public partial class SkylineNightly : Form
    {
        private const string REG_FILESYSTEM_KEY = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem";
        private const string REG_LONGPATHS_ENABLED = @"LongPathsEnabled";

        // In the order of the branch and type combo boxes. The pre-split combined run is not offered:
        // saving the form is how a machine moves off it.
        public static readonly Branch[] Branches = { Branch.master, Branch.release, Branch.integration };
        public static readonly RunType[] RunTypes = { RunType.standard, RunType.leak, RunType.perf, RunType.stress };

        public SkylineNightly()
        {
            InitializeComponent();

            // The saved modes may be pre-split names, which parse to the run they meant
            SelectRun(comboBoxBranch1, comboBoxType1, RunSpec.Parse(Settings.Default.mode1));
            if (Settings.Default.mode2 == string.Empty)
                comboBoxType2.SelectedIndex = RunTypes.Length; // RunTypes.Length == None
            else
                SelectRun(comboBoxBranch2, comboBoxType2, RunSpec.Parse(Settings.Default.mode2));

            startTime.Value = DateTime.Parse(Settings.Default.StartTime);
            textBoxFolder.Text = Settings.Default.NightlyFolder;

            if (string.IsNullOrEmpty(textBoxFolder.Text))
            {
                // Nightly test directory based on user Documents directory
                // requires extra knowledge and set-up to disable Windows indexing
                // and possibly other services like automated back-ups.
                // Much better to just use a directory at the root of either a
                // larger D: drive, or the C: drive.
                string defaultDir = @"D:\Nightly"; 
                if (!Directory.Exists(@"D:\")) 
                    defaultDir = @"C:\Nightly";
                textBoxFolder.Text = Path.Combine(defaultDir);
            }

            enabled.Checked = Nightly.NightlyTask != null;
        }

        private void Cancel(object sender, EventArgs e)
        {
            Close();
        }

        private void OK(object sender, EventArgs e)
        {
            Settings.Default.StartTime = startTime.Text;
            var nightlyFolder = textBoxFolder.Text;

            if (!Path.IsPathRooted(nightlyFolder))
            {
                // ReSharper disable once LocalizableElement
                MessageBox.Show(this, "Relative paths to the Documents folder are no longer allowed.\r\n" + 
                                      @"Please specify a full path, ideally outside your Documents folder.");
                return;
            }

            if (!Directory.Exists(nightlyFolder))
                Directory.CreateDirectory(nightlyFolder);

            var runSpec1 = GetRun1();
            var runSpec2 = GetRun2();
            if (runSpec2 != null && runSpec1.IsLong && runSpec2.IsLong)
            {
                // ReSharper disable LocalizableElement
                MessageBox.Show(this, string.Format("Two {0}-hour runs leave no margin in a day.\r\n" +
                                                    "Schedule at most one leak checking or perf run per machine, with a standard run or nothing as the other.",
                                                    RunSpec.LONG_DURATION_HOURS));
                // ReSharper restore LocalizableElement
                return;
            }

            Settings.Default.NightlyFolder = nightlyFolder;
            Settings.Default.mode1 = runSpec1.ToString();
            Settings.Default.mode2 = runSpec2?.ToString() ?? string.Empty;

            Settings.Default.Save();

            try
            {
            // Create new scheduled task to run the nightly build.
            using (var ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(Nightly.NightlyTaskName, false);
                ts.RootFolder.DeleteTask(Nightly.NightlyTaskNameWithUser, false);
                if (enabled.Checked)
                {
                    // Create a new task definition and assign properties
                    var td = ts.NewTask();
                    td.RegistrationInfo.Description = @"Skyline nightly build/test";
                    td.Principal.LogonType = TaskLogonType.InteractiveToken;

                    // Add a trigger that will fire the task every day
                    var dt = (DailyTrigger) td.Triggers.Add(new DailyTrigger { DaysInterval = 1 });
                    var scheduledTime = startTime.Value;
                    var now = DateTime.Now;
                    if (scheduledTime < now + TimeSpan.FromMinutes(1) && scheduledTime + TimeSpan.FromMinutes(3) > now)
                        scheduledTime = now + TimeSpan.FromMinutes(2);
                    dt.StartBoundary = scheduledTime;
                    int durationHours;
                    var runArguments = GetRunArguments(out durationHours);
                    var maxHours = durationHours == -1 ? 167 : 23; //If one of them is a stress test
                    dt.ExecutionTimeLimit = new TimeSpan(maxHours, 30, 0);
                    dt.Enabled = true;
                    td.Settings.WakeToRun = true;

                    // Using ProcessPriorityClass.High seems like cheating, but it's not:
                    // A normal user-initiated app has
                    //   TaskPriority = 8, I/O Priority = Normal, Memory Priority = 5
                    // Default priority for Task Scheduler launch provides
                    //   TaskPriority = 6, I/O Priority = Low, Memory Priority = 3
                    //ProcessPriorityClass.Normal provides the launched task with
                    //   TaskPriority = 8, I/O Priority = Normal, Memory Priority = 4 (not quite as good as user-launched)
                    // ProcessPriorityClass.High provides SkylineNightly with
                    //   TaskPriority = 13, I/O Priority = Normal, Memory Priority = 5
                    // but gives SkylineTester the standard user values of
                    //   TaskPriority = 8, I/O Priority = Normal, Memory Priority = 5
                    td.Settings.Priority = ProcessPriorityClass.High; 

                    // Add an action that will launch SkylineNightlyShim whenever the trigger fires
                    var assembly = Assembly.GetExecutingAssembly();
                    td.Actions.Add(new ExecAction(assembly.Location.Replace(@".exe", @"Shim.exe"), runArguments));

                    // Register the task in the root folder
                    ts.RootFolder.RegisterTaskDefinition(Nightly.NightlyTaskNameWithUser, td);

                    // Registry setting LongPathsEnabled here for python to install pip and configure packages required to run AlphaPeptDeep
                    Registry.SetValue(REG_FILESYSTEM_KEY, REG_LONGPATHS_ENABLED, 1);
                }
            }
            }
            catch (UnauthorizedAccessException exception)
            {
                // ReSharper disable LocalizableElement
                MessageBox.Show(string.Format("You need to run as Administrator to schedule a new task.\n\n {0}", exception));
                // ReSharper restore LocalizableElement
            }

            Close();
        }

        /// <summary>
        /// The scheduled task argument for the runs selected in the form, and their total hours,
        /// or -1 when one of them is a stress run with no limit.
        /// </summary>
        public string GetRunArguments(out int durationHours)
        {
            var runSpec1 = GetRun1();
            var runSpec2 = GetRun2();
            string result = @"run " + runSpec1;
            durationHours = (int)runSpec1.TargetDuration.TotalHours;
            if (runSpec2 != null)
            {
                result += @" " + runSpec2;
                durationHours += (int)runSpec2.TargetDuration.TotalHours;
            }

            if (runSpec1.IsStress || (runSpec2?.IsStress ?? false))
                durationHours = -1;

            return result;
        }

        private RunSpec GetRun1()
        {
            return new RunSpec(Branches[comboBoxBranch1.SelectedIndex], RunTypes[comboBoxType1.SelectedIndex]);
        }

        /// <summary>
        /// The second run of the day, or null for none.
        /// </summary>
        private RunSpec GetRun2()
        {
            if (comboBoxType2.SelectedIndex == RunTypes.Length || comboBoxType2.SelectedIndex == -1) // None, or not selected
                return null;
            return new RunSpec(Branches[comboBoxBranch2.SelectedIndex], RunTypes[comboBoxType2.SelectedIndex]);
        }

        private static void SelectRun(ComboBox comboBoxBranch, ComboBox comboBoxType, RunSpec runSpec)
        {
            comboBoxBranch.SelectedIndex = Array.IndexOf(Branches, runSpec.Branch);
            // The combined run shows as standard: it becomes the standard run when the form is saved
            var runType = runSpec.RunType == RunType.standard_leak ? RunType.standard : runSpec.RunType;
            comboBoxType.SelectedIndex = Array.IndexOf(RunTypes, runType);
        }

        private void StartTimeChanged(object sender, EventArgs e)
        {
            if (comboBoxBranch1.SelectedIndex == -1 || comboBoxType1.SelectedIndex == -1)
                return; // Still initializing
            int durationHours;
            GetRunArguments(out durationHours);
            endTime.Text = durationHours == -1 ? @"no limit" : (startTime.Value + TimeSpan.FromHours(durationHours)).ToShortTimeString();
        }

        private void Now_Click(object sender, EventArgs e)
        {
            startTime.Value = DateTime.Now;
        }

        private void buttonFolder_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                // ReSharper disable LocalizableElement
                dlg.Description = "Select or create a nightly build folder."; // ReSharper restore LocalizableElement
                dlg.ShowNewFolderButton = true;
                dlg.SelectedPath = textBoxFolder.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    textBoxFolder.Text = dlg.SelectedPath;
                }
            }
        }

        private void comboBoxRun_SelectedIndexChanged(object sender, EventArgs e)
        {
            // A second branch only means something with a second run type
            comboBoxBranch2.Enabled = comboBoxType2.SelectedIndex != RunTypes.Length;
            if (comboBoxBranch2.Enabled && comboBoxBranch2.SelectedIndex == -1)
                comboBoxBranch2.SelectedIndex = comboBoxBranch1.SelectedIndex;
            StartTimeChanged(sender, e); // End time display may depend on run type
        }
    }
}
