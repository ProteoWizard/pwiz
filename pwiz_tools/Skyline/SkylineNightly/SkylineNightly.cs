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
using Microsoft.Win32.TaskScheduler;
using SkylineNightly.Properties;

namespace SkylineNightly
{
    public partial class SkylineNightly : Form
    {
        public static readonly Nightly.RunMode[] RunModes = 
            { Nightly.RunMode.trunk, Nightly.RunMode.perf, Nightly.RunMode.release, Nightly.RunMode.stress, Nightly.RunMode.integration, Nightly.RunMode.release_perf, Nightly.RunMode.integration_perf };

        public SkylineNightly()
        {
            InitializeComponent();

            comboBoxOptions.SelectedIndex = Array.IndexOf(RunModes, Enum.Parse(typeof(Nightly.RunMode), Settings.Default.mode1, false));
            comboBoxOptions2.SelectedIndex = 
                Settings.Default.mode2 == string.Empty ? RunModes.Length : Array.IndexOf(RunModes, Enum.Parse(typeof(Nightly.RunMode), Settings.Default.mode2, false)); // RunModes.Count == None

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
                MessageBox.Show(this, "Relative paths to the Documents folder are no longer allowed.\r\n" + 
                                      @"Please specify a full path, ideally outside your Documents folder.");
                return;
            }

            if (!Directory.Exists(nightlyFolder))
                Directory.CreateDirectory(nightlyFolder);

            Settings.Default.NightlyFolder = nightlyFolder;
            Settings.Default.mode1 = RunModes[comboBoxOptions.SelectedIndex].ToString();
            Settings.Default.mode2 = comboBoxOptions2.SelectedIndex == RunModes.Length ? string.Empty : RunModes[comboBoxOptions2.SelectedIndex].ToString(); //RunModes.Length == None

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
                    var runType = RunType(out durationHours);
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
                    td.Actions.Add(new ExecAction(assembly.Location.Replace(@".exe", @"Shim.exe"), runType));

                    // Register the task in the root folder
                    ts.RootFolder.RegisterTaskDefinition(Nightly.NightlyTaskNameWithUser, td);
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

        public string RunType(out int durationHours)
        {
            durationHours = 0;
			string result = @"run ";

            int[] hours =
            {
                Nightly.DEFAULT_DURATION_HOURS, Nightly.PERF_DURATION_HOURS, Nightly.DEFAULT_DURATION_HOURS, -1,
                Nightly.DEFAULT_DURATION_HOURS, Nightly.PERF_DURATION_HOURS, Nightly.PERF_DURATION_HOURS
            };

            result += RunModes[comboBoxOptions.SelectedIndex].ToString();
            durationHours += hours[comboBoxOptions.SelectedIndex];

            if (comboBoxOptions2.SelectedIndex != RunModes.Length && comboBoxOptions2.SelectedIndex != -1) //!= none && != not selected
            {
				result += @" " + RunModes[comboBoxOptions2.SelectedIndex];
                durationHours += hours[comboBoxOptions2.SelectedIndex];
            }

            var stress = Array.IndexOf(RunModes, Nightly.RunMode.stress);  // 3 == Stress
            if (comboBoxOptions.SelectedIndex == stress || comboBoxOptions2.SelectedIndex == stress)
            {
                durationHours = -1;
            }

            return result;
        }

        private void StartTimeChanged(object sender, EventArgs e)
        {
            int durationHours;
            RunType(out durationHours);
            endTime.Text = durationHours == -1 ? @"no limit" : (startTime.Value + TimeSpan.FromHours(durationHours)).ToShortTimeString();
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
                Description = "Select or create a nightly build folder.", 
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

        private void comboBoxOptions2_SelectedIndexChanged(object sender, EventArgs e)
        {
            StartTimeChanged(sender, e); // End time display may depend on run type
        }
    }
}
