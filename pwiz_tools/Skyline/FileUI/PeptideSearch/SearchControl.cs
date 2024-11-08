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
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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

            public override string ToString()
            {
                return ToString(true); // For debugging convenience
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
        protected string lastSegmentName;
        protected void UpdateSearchEngineProgress(IProgressStatus status)
        {
            string message = status.IsError ? status.ErrorException.ToString() : status.Message;

            if (status.IsError)
            {
                MessageDlg.ShowWithException(Program.MainWindow, status.ErrorException.Message, status.ErrorException);
                return;
            }

            // set progressBarText with the first message for a segment
            if (status.SegmentCount > 0 && status.Segment != lastSegment)
            {
                lastSegment = status.Segment;
            }

            if (status.SegmentName != lastSegmentName)
            {
                lastSegmentName = status.SegmentName;
                progressBar.CustomText = status.SegmentName;
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
            if (Enumerable.Range(Math.Max(0, _progressTextItems.Count - 10), Math.Min(10, _progressTextItems.Count))
                .Any(i => _progressTextItems[i].Message == message))
            {
                return;
            }

            if (message.EndsWith(@"%") && double.TryParse(message.Substring(0, message.Length - 1), out _))
            {
                // Don't update text if the message is just a percent complete update (e.g. "13%") - that gets parsed in ProcessRunner.Run
                return;
            }

            var newEntry = new ProgressEntry(DateTime.Now, message);
            _progressTextItems.Add(newEntry);
            txtSearchProgress.AppendLineWithAutoScroll($@"{newEntry.ToString(showTimestampsCheckbox.Checked)}{Environment.NewLine}");
        }

        public void SetProgressBarDisplayStyle(ProgressBarDisplayText style)
        {
            progressBar.DisplayStyle = style;
        }

        public void SetProgressBarText(string message)
        {
            progressBar.CustomText = message;
            if (progressBar.DisplayStyle == ProgressBarDisplayText.CustomText)
            {
                Invalidate();
            }
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
            txtSearchProgress.AppendText(TextUtil.LineSeparate(_progressTextItems.Select(entry
                => entry.ToString(showTimestampsCheckbox.Checked))) + Environment.NewLine);

        }

        public string LogText => txtSearchProgress.Text;

        public bool HasUI => true;
        public bool IsCanceled => _cancelToken.IsCancellationRequested;

        /// progress updates from AbstractDdaConverter (should be prefixed by the file currently being processed)
        public virtual UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            if (IsCanceled)
                return UpdateProgressResponse.cancel;

            if (InvokeRequired)
                Invoke(new MethodInvoker(() => UpdateProgress(status)));
            else
                UpdateSearchEngineProgress(status);

            return UpdateProgressResponse.normal;
        }

        public class ParallelRunnerProgressControl : MultiProgressControl, IProgressMonitor
        {
            private readonly SearchControl _hostControl;

            public ParallelRunnerProgressControl(SearchControl hostControl)
            {
                _hostControl = hostControl;
                ProgressSplit.Panel2Collapsed = true;
            }

            // ReSharper disable once InconsistentlySynchronizedField
            public bool IsCanceled => _hostControl.IsCanceled;

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                if (IsCanceled || status.IsCanceled)
                    return UpdateProgressResponse.cancel;

                var match = Regex.Match(status.Message, @"(.*)\:\:(.*)");
                Assume.IsTrue(match.Success && match.Groups.Count == 3,
                    @"ParallelRunnerProgressDlg requires a message like file::message to indicate which file's progress is being updated");

                lock (this)
                {
                    // only make the MultiProgressControl visible if it's actually used
                    if (RowCount == 0)
                    {
                        var hostDialog = _hostControl.Parent;
                        hostDialog.BeginInvoke(new MethodInvoker(() =>
                        {
                            _hostControl.progressSplitContainer.Panel1Collapsed = false;
                            hostDialog.Size = new Size(Math.Min(
                                Screen.FromControl(hostDialog).Bounds.Width * 90 / 100,
                                hostDialog.Width * 2), hostDialog.Height);
                        }));
                    }

                    string name = match.Groups[1].Value;
                    string message = match.Groups[2].Value;
                    Update(name, status.PercentComplete, message, status.ErrorException != null);
                    return IsCanceled ? UpdateProgressResponse.cancel : UpdateProgressResponse.normal;
                }
            }

            public bool HasUI => true;
        }
    }
}
