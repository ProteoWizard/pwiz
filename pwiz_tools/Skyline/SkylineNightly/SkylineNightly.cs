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
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32.TaskScheduler;
using SkylineNightly.Properties;

namespace SkylineNightly
{
    public partial class SkylineNightly : Form
    {
        public SkylineNightly()
        {
            InitializeComponent();
            startTime.Value = DateTime.Parse(Settings.Default.StartTime);
            textBoxFolder.Text = Settings.Default.NightlyFolder;
            if (Settings.Default.IntegrationBranchRun != 0)
            {
                if (Settings.Default.IntegrationBranchRun == 1)
                    comboBoxOptions.SelectedIndex = 4;
                else
                    comboBoxOptions.SelectedIndex = 5;
            }
            else if (Settings.Default.StressTests)
                comboBoxOptions.SelectedIndex = 3;
            else if (Settings.Default.ReleaseBranch)
                comboBoxOptions.SelectedIndex = 2;
            else if (Settings.Default.PerfTests)
                comboBoxOptions.SelectedIndex = 1;
            else
                comboBoxOptions.SelectedIndex = 0;
            if (string.IsNullOrEmpty(textBoxFolder.Text))
            {
                // Nightly test directory based on user Documents directory
                // requires extra knowledge and set-up to disable Windows indexing
                // and possibly other services like automated back-ups.
                // Much better to just use a directory at the root of either a
                // larger D: drive, or the C: drive.
                string defaultDir = @"D:\Nightly"; // Not L10N
                if (!Directory.Exists(@"D:\")) // Not L10N
                    defaultDir = @"C:\Nightly"; // Not L10N
                textBoxFolder.Text = Path.Combine(defaultDir);
            }

            using (var ts = new TaskService())
            {
                var task = ts.FindTask(Nightly.NightlyTaskName) ?? ts.FindTask(Nightly.NightlyTaskNameWithUser);
                enabled.Checked = (task != null);
            }
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
                MessageBox.Show(this, "Relative paths to the Documents folder are no longer allowed.\r\n" + // Not L10N
                                      "Please specify a full path, ideally outside your Documents folder."); // Not L10N
                return;
            }
            if (!Directory.Exists(nightlyFolder))
                Directory.CreateDirectory(nightlyFolder);
            Settings.Default.NightlyFolder = nightlyFolder;
            Settings.Default.PerfTests = comboBoxOptions.SelectedIndex == 1;
            Settings.Default.ReleaseBranch = comboBoxOptions.SelectedIndex == 2;
            Settings.Default.StressTests = comboBoxOptions.SelectedIndex == 3;
            if (comboBoxOptions.SelectedIndex == 4)
                Settings.Default.IntegrationBranchRun = 1;
            else if (comboBoxOptions.SelectedIndex == 5)
                Settings.Default.IntegrationBranchRun = 2;
            else
                Settings.Default.IntegrationBranchRun = 0;
            Settings.Default.Save();

            // Create new scheduled task to run the nightly build.
            using (var ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(Nightly.NightlyTaskName, false);
                ts.RootFolder.DeleteTask(Nightly.NightlyTaskNameWithUser, false);
                if (enabled.Checked)
                {
                    // Create a new task definition and assign properties
                    var td = ts.NewTask();
                    td.RegistrationInfo.Description = "Skyline nightly build/test"; // Not L10N
                    td.Principal.LogonType = TaskLogonType.InteractiveToken;

                    // Add a trigger that will fire the task every other day
                    var dt = (DailyTrigger)td.Triggers.Add(new DailyTrigger { DaysInterval = 1 });
                    var scheduledTime = startTime.Value;
                    var now = DateTime.Now;
                    if (scheduledTime < now + TimeSpan.FromMinutes(1) && scheduledTime + TimeSpan.FromMinutes(3) > now)
                        scheduledTime = now + TimeSpan.FromMinutes(2);
                    dt.StartBoundary = scheduledTime;
                    int durationHours;
                    var runType = RunType(out durationHours);
                    var maxHours = runType.Equals(Program.SCHEDULED_STRESSTESTS_ARG) ? 167 : 23;
                    dt.ExecutionTimeLimit = new TimeSpan(maxHours, 30, 0);
                    dt.Enabled = true;
                    td.Settings.WakeToRun = true;

                    // Add an action that will launch SkylineTester whenever the trigger fires
                    var assembly = Assembly.GetExecutingAssembly();
                    td.Actions.Add(new ExecAction(assembly.Location, runType)); // Not L10N

                    // Register the task in the root folder
                    ts.RootFolder.RegisterTaskDefinition(Nightly.NightlyTaskNameWithUser, td);
                }
            }

            Close();
        }

        private string RunType(out int durationHours)
        {
            string arg;
            switch (comboBoxOptions.SelectedIndex)
            {
                default:
                    arg = Program.SCHEDULED_ARG;
                    durationHours = Nightly.DEFAULT_DURATION_HOURS;
                    break;
                case 1:
                    arg = Program.SCHEDULED_PERFTESTS_ARG;
                    durationHours = Nightly.PERF_DURATION_HOURS;
                    break;
                case 2:
                    arg = Program.SCHEDULED_RELEASE_BRANCH_ARG;
                    durationHours = Nightly.PERF_DURATION_HOURS;
                    break;
                case 3:
                    arg = Program.SCHEDULED_STRESSTESTS_ARG;
                    durationHours = -1; // Unlimited
                    break;
                case 4:
                    arg = Program.SCHEDULED_INTEGRATION_ARG;
                    durationHours = Nightly.DEFAULT_DURATION_HOURS;
                    break;
                case 5:
                    arg = Program.SCHEDULED_INTEGRATION_TRUNK_ARG;
                    durationHours = Nightly.DEFAULT_DURATION_HOURS + Nightly.DEFAULT_DURATION_HOURS;
                    break;
                case 6:
                    arg = Program.SCHEDULED_PERFTEST_AND_TRUNK_ARG;
                    durationHours = Nightly.DEFAULT_DURATION_HOURS + Nightly.PERF_DURATION_HOURS;
                    break;
                case 7:
                    arg = Program.SCHEDULED_TRUNK_AND_TRUNK_ARG;
                    durationHours = 2 * Nightly.DEFAULT_DURATION_HOURS;
                    break;
            }
            return arg;
        }

        private void StartTimeChanged(object sender, EventArgs e)
        {
            int durationHours;
            var runType = RunType(out durationHours);
            var end = startTime.Value + TimeSpan.FromHours(durationHours);
            endTime.Text = (runType.Equals(Program.SCHEDULED_STRESSTESTS_ARG))?"no limit":end.ToShortTimeString(); // Not L10N
        }

        private void Now_Click(object sender, EventArgs e)
        {
            startTime.Value = DateTime.Now;
        }

        private void buttonFolder_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog
            {
                // ReSharper disable LocalizableElement
                Description = "Select or create a nightly build folder.", // Not L10N
                // ReSharper restore LocalizableElement
                ShowNewFolderButton = true,
                SelectedPath = textBoxFolder.Text
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    textBoxFolder.Text = dlg.SelectedPath;
                }
            }
        }

        private void comboBoxOptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            StartTimeChanged(sender, e); // End time display may depend on run type
        }
    }
}
