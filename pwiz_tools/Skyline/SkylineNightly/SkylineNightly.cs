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
using System.Collections.Generic;
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
        public static readonly RunType[] RunTypes = { RunType.standard, RunType.leak, RunType.perf };

        // The last selection of each type combo that did not make two long runs, to switch back to
        private readonly Dictionary<ComboBox, int> _validTypeIndexes = new Dictionary<ComboBox, int>();

        public SkylineNightly()
        {
            InitializeComponent();

            var savedRuns = RunSpec.GetSavedRuns();
            SelectRun(comboBoxBranch1, comboBoxType1, savedRuns[0]);
            radioButtonTwoRuns.Checked = savedRuns.Length > 1;
            if (radioButtonTwoRuns.Checked)
                SelectRun(comboBoxBranch2, comboBoxType2, savedRuns[1]);
            else
                ShowSecondRun(false);

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

            // The type combos refuse this as it is chosen; this catches a pair loaded from saved settings
            if (WarnIfTwoLongRuns())
                return;

            Settings.Default.NightlyFolder = nightlyFolder;
            RunSpec.SaveRuns(GetRun1(), GetRun2());

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
                    dt.ExecutionTimeLimit = new TimeSpan(23, 30, 0);
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

                    // Add an action that will launch SkylineNightlyShim whenever the trigger fires. What
                    // to run is in the settings saved above; the shim just updates and says "run".
                    var assembly = Assembly.GetExecutingAssembly();
                    td.Actions.Add(new ExecAction(assembly.Location.Replace(@".exe", @"Shim.exe"), @"run"));

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
        /// The total hours of the runs selected in the form.
        /// </summary>
        private int GetDurationHours()
        {
            return (int)(GetRun1().TargetDuration.TotalHours + (GetRun2()?.TargetDuration.TotalHours ?? 0));
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
            if (!radioButtonTwoRuns.Checked || comboBoxBranch2.SelectedIndex == -1 || comboBoxType2.SelectedIndex == -1)
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
            endTime.Text = (startTime.Value + TimeSpan.FromHours(GetDurationHours())).ToShortTimeString();
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

        /// <summary>
        /// A machine may schedule one long run a day, not two. Shows why when both runs are long,
        /// and returns true if it did.
        /// </summary>
        private bool WarnIfTwoLongRuns()
        {
            var runSpec2 = GetRun2();
            if (runSpec2 == null || comboBoxType1.SelectedIndex == -1 || !GetRun1().IsLong || !runSpec2.IsLong)
                return false;
            // ReSharper disable LocalizableElement
            MessageBox.Show(this, string.Format("Two {0}-hour runs leave no margin in a day.\r\n" +
                                                "Schedule at most one leak checking or perf run per machine, with a standard run or nothing as the other.",
                                                RunSpec.LONG_DURATION_HOURS));
            // ReSharper restore LocalizableElement
            return true;
        }

        private void radioButtonTwoRuns_CheckedChanged(object sender, EventArgs e)
        {
            ShowSecondRun(radioButtonTwoRuns.Checked);
            StartTimeChanged(sender, e); // End time display may depend on run type
        }

        /// <summary>
        /// The Then row exists only with two runs a day. Shown for the first time, it starts
        /// from the first run's branch.
        /// </summary>
        private void ShowSecondRun(bool show)
        {
            label4.Visible = comboBoxBranch2.Visible = labelType2.Visible = comboBoxType2.Visible = show;
            if (!show)
                return;
            if (comboBoxBranch2.SelectedIndex == -1)
                comboBoxBranch2.SelectedIndex = comboBoxBranch1.SelectedIndex;
            if (comboBoxType2.SelectedIndex == -1)
                comboBoxType2.SelectedIndex = Array.IndexOf(RunTypes, RunType.standard);
        }

        private void comboBoxBranch_SelectedIndexChanged(object sender, EventArgs e)
        {
            StartTimeChanged(sender, e); // End time display may depend on run type
        }

        private void comboBoxType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var comboBoxType = (ComboBox)sender;
            // Not while the constructor is still loading saved settings, which OK checks instead
            if (IsHandleCreated && WarnIfTwoLongRuns())
            {
                comboBoxType.SelectedIndex = _validTypeIndexes[comboBoxType];
                return; // The change of selection just made calls back here
            }
            _validTypeIndexes[comboBoxType] = comboBoxType.SelectedIndex;
            StartTimeChanged(sender, e); // End time display may depend on run type
        }
    }
}
