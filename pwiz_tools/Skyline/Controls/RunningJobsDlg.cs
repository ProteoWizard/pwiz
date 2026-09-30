/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Shows what is running in the background and lets the user stop it: the operations they sent there with a
    /// LongWaitDlg's "Run in Background", and the ones a tool left running when it stopped waiting for them.
    /// Reached from Tools > Running Jobs, or by double-clicking the status bar where their progress shows.
    /// </summary>
    public partial class RunningJobsDlg : FormEx
    {
        public RunningJobsDlg()
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            RefreshJobs();
        }

        /// <summary>
        /// Puts what is running into the list. Rows are matched to jobs by id and updated in place, so the row the
        /// user has selected stays selected (and stays theirs to cancel) while its progress advances underneath.
        /// </summary>
        private void RefreshJobs()
        {
            var jobs = BackgroundJobs.Running;
            listJobs.BeginUpdate();
            try
            {
                // Drop the rows of jobs that have finished.
                foreach (var item in listJobs.Items.Cast<ListViewItem>().ToArray())
                {
                    if (jobs.All(job => !Equals(job.JobId, item.Tag)))
                    {
                        listJobs.Items.Remove(item);
                    }
                }

                foreach (var job in jobs)
                {
                    var item = listJobs.Items.Cast<ListViewItem>().FirstOrDefault(row => Equals(job.JobId, row.Tag));
                    if (item == null)
                    {
                        item = new ListViewItem(job.Description) { Tag = job.JobId };
                        item.SubItems.Add(string.Empty);
                        item.SubItems.Add(string.Empty);
                        listJobs.Items.Add(item);
                    }
                    else
                    {
                        item.Text = job.Description;
                    }
                    item.SubItems[1].Text = job.Message ?? string.Empty;
                    item.SubItems[2].Text = GetProgressText(job);
                }

                // Keep a job selected, so Cancel Job always has an obvious target - most of the time there is only
                // the one, and the user came here to stop it.
                if (listJobs.SelectedItems.Count == 0 && listJobs.Items.Count > 0)
                {
                    listJobs.Items[0].Selected = true;
                }
            }
            finally
            {
                listJobs.EndUpdate();
            }
            UpdateButtons();
        }

        // A percentage, except while the job cannot say how far along it is (-1, which shows as a marquee in the
        // status bar), or once it has been asked to stop and is finishing what it was doing.
        private string GetProgressText(JobProgressStatus job)
        {
            if (BackgroundJobs.IsCancelRequested(job.JobId))
                return ControlsResources.RunningJobsDlg_GetProgressText_Stopping;
            if (job.PercentComplete < 0)
                return string.Empty;
            return job.PercentComplete.ToString(@"0'%'", CultureInfo.CurrentCulture);
        }

        private void UpdateButtons()
        {
            btnCancelJob.Enabled = SelectedJobId.HasValue;
        }

        public Guid? SelectedJobId
        {
            get
            {
                var item = listJobs.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
                return (Guid?) item?.Tag;
            }
        }

        /// <summary>
        /// Asks the selected job to stop. It stops at its next cancellation check, so the row stays until it has -
        /// reading "Stopping" in the meantime. Called Stop in the UI, where "Cancel" would be read as cancelling
        /// the dialog; it is cancellation underneath, which is all a job can be asked for.
        /// </summary>
        public void CancelSelectedJob()
        {
            var jobId = SelectedJobId;
            if (jobId.HasValue)
            {
                BackgroundJobs.Cancel(jobId.Value);
            }
            RefreshJobs();
        }

        public int JobCount
        {
            get { return listJobs.Items.Count; }
        }

        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            RefreshJobs();
        }

        private void listJobs_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void btnCancelJob_Click(object sender, EventArgs e)
        {
            CancelSelectedJob();
        }
    }
}
