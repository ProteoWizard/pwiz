using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MSAmanda.Core;
using MSAmanda.Utils;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
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
            //this.mSAmandaSearchWrapper = mSAmandaSearch;
            //InitializeEngine();
            //AmandaSearchTask = Task<bool>.Factory.StartNew(RunSearch);
        }

        private void UpdateSearchEngineProgress(string message)
        {
            txtSearchProgress.Text += message + "\r\n";
        }


       

        private InstrumentSetting GenerateIntrumentSettings()
        {
            InstrumentSetting setting = new InstrumentSetting();
            //setting.
            return setting;
        }

        private CancellationTokenSource cancelToken;
        private Task<bool> t;
       

        public async void RunSearch()
        {
            if (string.IsNullOrEmpty(txtSearchProgress.Text))
            {
                //search for first time
                ImportPeptideSearch.SearchEngine.SearchProgessChanged += SearchEngine_MessageNotificationEvent;
            }
            txtSearchProgress.Text = "";
            btnCancel.Enabled = true;
            UpdateSearchEngineProgress("Starting search...");
            cancelToken = new CancellationTokenSource();
            t = Task<bool>.Factory.StartNew(() => ImportPeptideSearch.SearchEngine.Run(cancelToken),cancelToken.Token);
            await t;
            //todo set result files
            if (cancelToken.IsCancellationRequested)
            {
                UpdateSearchEngineProgress("Search canceled.");
            } else if (!t.Result)
            {
                UpdateSearchEngineProgress("Search failed.");
            }
            else
            {
                UpdateSearchEngineProgress("Search done.");
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
