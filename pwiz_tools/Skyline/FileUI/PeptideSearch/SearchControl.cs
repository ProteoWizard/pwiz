/*
 * Original author: Viktoria Dorfer <viktoria.dorfer .at. fh-hagenberg.at>,
 *                  Bioinformatics Research Group, University of Applied Sciences Upper Austria
 *
 * Copyright 2020 University of Applied Sciences Upper Austria
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
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public abstract partial class SearchControl : UserControl, IProgressMonitor
    {
        public Action<IProgressStatus> UpdateUI;

        public delegate void SearchFinishedDelegate(bool success);
        public event SearchFinishedDelegate SearchFinished;
        protected virtual void OnSearchFinished(bool success)
        {
            SearchFinished?.Invoke(success);
        }

        protected class ProgressEntry
        {
            public ProgressEntry(DateTime timestamp, string message)
            {
                Timestamp = timestamp;
                Message = message;
            }

            public DateTime Timestamp { get; }
            public string Message { get; }

            public string ToString(bool showTimestamp)
            {
                // ReSharper disable LocalizableElement
                return showTimestamp ? $"[{Timestamp.ToString("yyyy/MM/dd HH:mm:ss")}]  {Message}" : Message;
                // ReSharper restore LocalizableElement
            }
        }

        protected List<ProgressEntry> _progressTextItems = new List<ProgressEntry>();

        public SearchControl()
        {
            InitializeComponent();
            UpdateUI = UpdateSearchEngineProgress;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <remarks>Moved from .Designer.cs file.</remarks>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            _cancelToken?.Cancel();

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        protected virtual void UpdateTaskbarProgress(TaskbarProgress.TaskbarStates state, int? percentComplete)
        {
            if (Program.MainWindow != null)
                Program.MainWindow.UpdateTaskbarProgress(state, percentComplete);
        }

        protected int lastSegment = -1;
        protected string lastMessage;
        protected void UpdateSearchEngineProgress(IProgressStatus status)
        {
            string message = status.IsError ? status.ErrorException.ToString() : status.Message;

            if (status.IsError)
            {
                MessageDlg.ShowWithException(Program.MainWindow, Resources.CommandLineTest_ConsoleAddFastaTest_Error, status.ErrorException);
                return;
            }

            // set progressBarText with the first message for a segment
            if (status.SegmentCount > 0 && status.Segment != lastSegment)
            {
                lastSegment = status.Segment;
                progressBar.CustomText = status.Message;
            }

            if (!status.WarningMessage.IsNullOrEmpty() && status.WarningMessage != lastMessage)
            {
                lastMessage = message = status.WarningMessage;
                progressBar.CustomText = status.WarningMessage;
            }

            int percentComplete = status.PercentComplete;
            if (status.SegmentCount > 0)
                percentComplete = status.Segment * 100 / status.SegmentCount + status.ZoomedPercentComplete / status.SegmentCount;

            UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.Normal, percentComplete);
            if (status.PercentComplete == -1)
                progressBar.Style = ProgressBarStyle.Marquee;
            else
            {
                progressBar.Value = percentComplete;
                progressBar.Style = ProgressBarStyle.Continuous;
            }

            // look at the last 10 lines for the same message and if found do not relog the same message
            if (_progressTextItems.Skip(Math.Max(0, _progressTextItems.Count - 10)).Any(entry => entry.Message == message))
                return;

            var newEntry = new ProgressEntry(DateTime.Now, message);
            _progressTextItems.Add(newEntry);
            txtSearchProgress.AppendLineWithAutoScroll($@"{newEntry.ToString(showTimestampsCheckbox.Checked)}{Environment.NewLine}");
        }

        protected CancellationTokenSource _cancelToken;

        public abstract void RunSearch();

        protected IProgressStatus UpdateSearchEngineProgressMilestone(IProgressStatus status,
            bool success, int segmentsComplete, string cancelledMessage, string failedMessage, string succeededMessage)
        {
            if (_cancelToken.IsCancellationRequested)
            {
                UpdateSearchEngineProgress(status = status.ChangeMessage(cancelledMessage));
                progressBar.Visible = false;
            }
            else if (!success)
            {
                UpdateSearchEngineProgress(status = status.ChangeMessage(failedMessage));
                Cancel();
            }
            else
            {
                if (segmentsComplete == status.SegmentCount)
                    status = status.ChangeSegments(0, 0);

                status = status.ChangeMessage(succeededMessage).Complete();
                UpdateSearchEngineProgress(status);

                if (status.SegmentCount > segmentsComplete)
                    status = status.ChangeSegments(segmentsComplete, status.SegmentCount);
            }

            return status;
        }

        protected void SearchEngine_MessageNotificationEvent(object sender, IProgressStatus status)
        {
            if (InvokeRequired)
            {
                Invoke(UpdateUI, status);
            }
        }

        public void Cancel()
        {
            _cancelToken?.Cancel();
            btnCancel.Enabled = false;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Cancel();
        }

        private void showTimestampsCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            RefreshProgressTextbox();
        }

        private void RefreshProgressTextbox()
        {
            txtSearchProgress.Clear();
            foreach (var entry in _progressTextItems)
                txtSearchProgress.AppendText($@"{entry.ToString(showTimestampsCheckbox.Checked)}{Environment.NewLine}");
        }

        public string LogText => txtSearchProgress.Text;

        public bool HasUI => true;
        public bool IsCanceled => _cancelToken.IsCancellationRequested;

        /// progress updates from AbstractDdaConverter (should be prefixed by the file currently being processed)
        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            if (IsCanceled)
                return UpdateProgressResponse.cancel;

            if (InvokeRequired)
                Invoke(new MethodInvoker(() => UpdateProgress(status)));
            else
                UpdateSearchEngineProgress(status.ChangeMessage(status.Message));

            return UpdateProgressResponse.normal;
        }
    }
}
