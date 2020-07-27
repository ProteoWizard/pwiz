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
            txtSearchProgress.AppendText(status.Message + Environment.NewLine);
            UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.Normal, status.PercentComplete);
        }

        private InstrumentSetting GenerateIntrumentSettings()
        {
            return new InstrumentSetting();
        }

        private CancellationTokenSource cancelToken;
        private Task<bool> t;

        public async void RunSearch()
        {
            if (string.IsNullOrEmpty(txtSearchProgress.Text))
            {
                //search for first time
                ImportPeptideSearch.SearchEngine.SearchProgressChanged += SearchEngine_MessageNotificationEvent;
            }
            txtSearchProgress.Text = string.Empty;
            btnCancel.Enabled = true;

            ProgressStatus status = new ProgressStatus(Resources.DDASearchControl_SearchProgress_Starting_search);

            UpdateSearchEngineProgress(status);
            cancelToken = new CancellationTokenSource();
            t = Task<bool>.Factory.StartNew(() => ImportPeptideSearch.SearchEngine.Run(cancelToken),cancelToken.Token);
            await t;
            if (cancelToken.IsCancellationRequested)
            {
                UpdateSearchEngineProgress(status.ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_canceled));
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
        }

        private void SearchEngine_MessageNotificationEvent(object sender, IProgressStatus status)
        {
            if (InvokeRequired)
            {
                Invoke(UpdateUI, status);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            cancelToken.Cancel();
            btnCancel.Enabled = false;
        }
    }
}
