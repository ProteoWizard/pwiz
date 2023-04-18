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
using System.Threading;
using System.Windows.Forms;
using MSAmanda.Utils;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class DDASearchControl : UserControl, IProgressMonitor
    {
        private ImportPeptideSearch ImportPeptideSearch;

        public Action<IProgressStatus> UpdateUI;

        public delegate void SearchFinishedDelegate(bool success);
        public event SearchFinishedDelegate OnSearchFinished;

        private class ProgressEntry
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

        private List<ProgressEntry> _progressTextItems = new List<ProgressEntry>();

        public DDASearchControl(ImportPeptideSearch importPeptideSearch)
        {
            InitializeComponent();
            ImportPeptideSearch = importPeptideSearch;
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

        private void UpdateSearchEngineProgress(IProgressStatus status)
        {
            string message = (status.IsError ? status.ErrorException.ToString() : status.Message) ?? string.Empty;

            if (status.IsError)
            {
                MessageDlg.ShowWithException(Program.MainWindow, Resources.CommandLineTest_ConsoleAddFastaTest_Error, status.ErrorException);
                return;
            }
            if (IsDisposed)
            {
                return;
            }

            if (_progressTextItems.Count > 0 && status.Message == _progressTextItems[_progressTextItems.Count - 1].Message)
                return;

            if (!(message.EndsWith(@"%") && double.TryParse(message.Substring(0, message.Length-1), out _)))
            {
                // Don't update text if the message is just a percent complete update e.g. "13%"
                var newEntry = new ProgressEntry(DateTime.Now, message);
                _progressTextItems.Add(newEntry);
                txtSearchProgress.AppendText($@"{newEntry.ToString(showTimestampsCheckbox.Checked)}{Environment.NewLine}");
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
        }

        private InstrumentSetting GenerateIntrumentSettings()
        {
            return new InstrumentSetting();
        }

        private CancellationTokenSource _cancelToken;

        public void RunSearch()
        {
            ImportPeptideSearch.SearchEngine.SearchProgressChanged += SearchEngine_MessageNotificationEvent;
            txtSearchProgress.Text = string.Empty;
            _progressTextItems.Clear();
            btnCancel.Enabled = progressBar.Visible = true;

            _cancelToken = new CancellationTokenSource();

            ActionUtil.RunAsync(RunSearchAsync, @"DDA Search thread");
        }

        private void RunSearchAsync()
        {
            var filesCount = ImportPeptideSearch.SearchEngine.SpectrumFileNames.Length;

            IProgressStatus status = new ProgressStatus().ChangeSegments(0, filesCount);
            bool convertSuccess = true;

            if (ImportPeptideSearch.DdaConverter != null)
            {
                status = status.ChangeSegments(0, filesCount * 2);
                convertSuccess = ImportPeptideSearch.DdaConverter.Run(this, status) && !_cancelToken.IsCancellationRequested;

                Invoke(new MethodInvoker(() => status = UpdateSearchEngineProgressMilestone(status, convertSuccess, filesCount,
                    Resources.DDASearchControl_RunSearch_Conversion_cancelled_,
                    Resources.DDASearchControl_RunSearch_Conversion_failed_,
                    Resources.DDASearchControl_SearchProgress_Starting_search)));
            }

            bool searchSuccess = convertSuccess;
            if (convertSuccess)
            {
                searchSuccess = ImportPeptideSearch.SearchEngine.Run(_cancelToken, status) && !_cancelToken.IsCancellationRequested;

                Invoke(new MethodInvoker(() => UpdateSearchEngineProgressMilestone(status, searchSuccess, status.SegmentCount,
                    Resources.DDASearchControl_SearchProgress_Search_canceled,
                    Resources.DDASearchControl_SearchProgress_Search_failed,
                    Resources.DDASearchControl_SearchProgress_Search_done)));
            }

            Invoke(new MethodInvoker(() =>
            {
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, 0);
                btnCancel.Enabled = false;
                OnSearchFinished?.Invoke(searchSuccess);
                ImportPeptideSearch.SearchEngine.SearchProgressChanged -= SearchEngine_MessageNotificationEvent;
            }));
        }

        private IProgressStatus UpdateSearchEngineProgressMilestone(IProgressStatus status, bool success, int segmentsComplete,
            string cancelledMessage, string failedMessage, string succeededMessage)
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

        private void SearchEngine_MessageNotificationEvent(object sender, IProgressStatus status)
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
