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
using System.Threading.Tasks;
using System.Windows.Forms;
using MSAmanda.Utils;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

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
            cancelToken?.Cancel();

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
            string message = status.IsError ? status.ErrorException.ToString() : status.Message;

            if (status.IsError)
            {
                MessageDlg.ShowWithException(Program.MainWindow, Resources.CommandLineTest_ConsoleAddFastaTest_Error, status.ErrorException);
                return;
            }

            if (_progressTextItems.Count > 0 && status.Message == _progressTextItems[_progressTextItems.Count - 1].Message)
                return;

            var newEntry = new ProgressEntry(DateTime.Now, message);
            _progressTextItems.Add(newEntry);
            txtSearchProgress.AppendText($@"{newEntry.ToString(showTimestampsCheckbox.Checked)}{Environment.NewLine}");

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

        private CancellationTokenSource cancelToken;
        private Task<bool> t;

        public async void RunSearch()
        {
            ImportPeptideSearch.SearchEngine.SearchProgressChanged += SearchEngine_MessageNotificationEvent;
            txtSearchProgress.Text = string.Empty;
            _progressTextItems.Clear();
            btnCancel.Enabled = true;
            cancelToken = new CancellationTokenSource();
            IProgressStatus status = new ProgressStatus();
            progressBar.Visible = true;
            bool success = true;

            if (ImportPeptideSearch.DdaConverter != null)
            {
                status = status.ChangeSegments(0, ImportPeptideSearch.SearchEngine.SpectrumFileNames.Length * 2);

                t = Task<bool>.Factory.StartNew((statusObj) => ImportPeptideSearch.DdaConverter.Run(this, statusObj as IProgressStatus), status, cancelToken.Token);
                await t;
                success = t.Result;

                if (cancelToken.IsCancellationRequested)
                {
                    UpdateSearchEngineProgress(status.ChangeMessage(Resources.DDASearchControl_RunSearch_Conversion_cancelled_));
                    progressBar.Visible = false;
                    success = false;
                }
                else if (!t.Result)
                {
                    UpdateSearchEngineProgress(status.ChangeMessage(Resources.DDASearchControl_RunSearch_Conversion_failed_));
                    Cancel();
                }
                else
                {
                    UpdateSearchEngineProgress(status.ChangeMessage(Resources.DDASearchControl_RunSearch_Conversion_finished_).Complete());
                    status = status.ChangeSegments(ImportPeptideSearch.SearchEngine.SpectrumFileNames.Length,
                        ImportPeptideSearch.SearchEngine.SpectrumFileNames.Length * 2);
                }
            }
            else
                status = status.ChangeSegments(0, ImportPeptideSearch.SearchEngine.SpectrumFileNames.Length);

            if (success && !cancelToken.IsCancellationRequested)
            {
                status.ChangeMessage(Resources.DDASearchControl_SearchProgress_Starting_search);
                UpdateSearchEngineProgress(status);

                t = Task<bool>.Factory.StartNew(() => ImportPeptideSearch.SearchEngine.Run(cancelToken, status), cancelToken.Token);
                await t;
                success = t.Result;

                if (cancelToken.IsCancellationRequested)
                {
                    UpdateSearchEngineProgress(status.ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_canceled));
                    progressBar.Visible = false;
                    success = false;
                }
                else if (!t.Result)
                {
                    UpdateSearchEngineProgress(status.ChangeWarningMessage(Resources.DDASearchControl_SearchProgress_Search_failed));
                    Cancel();
                }
                else
                {
                    UpdateSearchEngineProgress(status.ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_done).ChangeSegments(0, 0).Complete());
                }
            }
            UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, 0);
            btnCancel.Enabled = false;
            OnSearchFinished?.Invoke(success);
            ImportPeptideSearch.SearchEngine.SearchProgressChanged -= SearchEngine_MessageNotificationEvent;
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
            cancelToken?.Cancel();
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
        public bool IsCanceled => cancelToken.IsCancellationRequested;

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
