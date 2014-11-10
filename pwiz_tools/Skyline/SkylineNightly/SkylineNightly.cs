using System;
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

            using (TaskService ts = new TaskService())
            {
                var task = ts.FindTask(Program.NIGHTLY_TASK_NAME);
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
            Settings.Default.NightlyFolder = textBoxFolder.Text;
            Settings.Default.Save();

            // Create new scheduled task to run the nightly build.
            using (TaskService ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(Program.NIGHTLY_TASK_NAME, false);
                if (enabled.Checked)
                {
                    // Create a new task definition and assign properties
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = "Skyline nightly build/test"; // Not L10N
                    td.Principal.LogonType = TaskLogonType.InteractiveToken;

                    // Add a trigger that will fire the task every other day
                    DailyTrigger dt = (DailyTrigger)td.Triggers.Add(new DailyTrigger { DaysInterval = 1 });
                    dt.StartBoundary = startTime.Value;
                    dt.ExecutionTimeLimit = new TimeSpan(23, 30, 0);
                    dt.Enabled = true;
                    td.Settings.WakeToRun = true;

                    // Add an action that will launch SkylineTester whenever the trigger fires
                    var assembly = Assembly.GetExecutingAssembly();
                    td.Actions.Add(new ExecAction(assembly.Location, Program.SCHEDULED_ARG)); // Not L10N

                    // Register the task in the root folder
                    ts.RootFolder.RegisterTaskDefinition(Program.NIGHTLY_TASK_NAME, td);
                }
            }

            Close();
        }

        private void StartTimeChanged(object sender, EventArgs e)
        {
            var end = startTime.Value + TimeSpan.FromHours(9);
            endTime.Text = end.ToShortTimeString();
        }
    }
}
