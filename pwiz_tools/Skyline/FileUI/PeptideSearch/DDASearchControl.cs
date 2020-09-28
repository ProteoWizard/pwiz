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
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class DDASearchControl : UserControl
    {
        private ImportPeptideSearch ImportPeptideSearch;

        public delegate void UpdateUIDelegate(IProgressStatus status);
        public UpdateUIDelegate UpdateUI;

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

        protected virtual void UpdateTaskbarProgress(TaskbarProgress.TaskbarStates state, int? percentComplete)
        {
            if (Program.MainWindow != null)
                Program.MainWindow.UpdateTaskbarProgress(state, percentComplete);
        }

        private void UpdateSearchEngineProgress(IProgressStatus status)
        {
            var newEntry = new ProgressEntry(DateTime.Now, status.Message);
            _progressTextItems.Add(newEntry);
            txtSearchProgress.AppendText($@"{newEntry.ToString(showTimestampsCheckbox.Checked)}{Environment.NewLine}");
            UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.Normal, status.PercentComplete);
            progressBar.Value = status.PercentComplete;
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

            ProgressStatus status = new ProgressStatus(Resources.DDASearchControl_SearchProgress_Starting_search);
            progressBar.Visible = true;

            UpdateSearchEngineProgress(status);
            cancelToken = new CancellationTokenSource();
            t = Task<bool>.Factory.StartNew(() => ImportPeptideSearch.SearchEngine.Run(cancelToken),cancelToken.Token);
            await t;
            if (cancelToken.IsCancellationRequested)
            {
                UpdateSearchEngineProgress(status.ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_canceled));
                progressBar.Visible = false;
            }
            else if (!t.Result)
            {
                UpdateSearchEngineProgress(status.ChangeWarningMessage(Resources.DDASearchControl_SearchProgress_Search_failed));
            }
            else
            {
                UpdateSearchEngineProgress(status.ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_done).Complete());
            }
            UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, 0);
            btnCancel.Enabled = false;
            OnSearchFinished?.Invoke(t.Result);
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
            cancelToken.Cancel();
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
    }
}
