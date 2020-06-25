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
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class DDASearchControl : UserControl
    {
        private ImportPeptideSearch ImportPeptideSearch;

        public delegate void UpdateUIDelegate(string message);
        public UpdateUIDelegate UpdateUI;

        public delegate void SearchFinishedDelegate(bool success);
        public event SearchFinishedDelegate OnSearchFinished;

        public DDASearchControl(ImportPeptideSearch importPeptideSearch)
        {
            InitializeComponent();
            ImportPeptideSearch = importPeptideSearch;
            UpdateUI = new UpdateUIDelegate(UpdateSearchEngineProgress);
        }

        private void UpdateSearchEngineProgress(string message)
        {
            txtSearchProgress.Text += message + "\r\n";
            txtSearchProgress.ScrollToCaret();
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
            UpdateSearchEngineProgress(Resources.DDASearchControl_SearchProgress_Starting_search);
            cancelToken = new CancellationTokenSource();
            t = Task<bool>.Factory.StartNew(() => ImportPeptideSearch.SearchEngine.Run(cancelToken),cancelToken.Token);
            await t;
            if (cancelToken.IsCancellationRequested)
            {
                UpdateSearchEngineProgress(Resources.DDASearchControl_SearchProgress_Search_canceled);
            } else if (!t.Result)
            {
                UpdateSearchEngineProgress(Resources.DDASearchControl_SearchProgress_Search_failed);
            }
            else
            {
                UpdateSearchEngineProgress(Resources.DDASearchControl_SearchProgress_Search_done);
            }
            btnCancel.Enabled = false;
            OnSearchFinished?.Invoke(t.Result);
        }

        private void SearchEngine_MessageNotificationEvent(object sender, AbstractDdaSearchEngine.MessageEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(UpdateUI, e.Message);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            cancelToken.Cancel();
            btnCancel.Enabled = false;
        }
    }
}
