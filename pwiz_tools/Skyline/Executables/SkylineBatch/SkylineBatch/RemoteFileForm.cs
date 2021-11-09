using System;
using System.Threading;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class RemoteFileForm : Form
    {
        private CancellationTokenSource _cancelSource;
        private IMainUiControl _mainControl;
        private RemoteFileControl _remoteFileControl;

        public RemoteFileForm(Server editingServer, string path, string title, IMainUiControl mainControl, SkylineBatchConfigManagerState state)
        {
            InitializeComponent();
            Icon = Program.Icon();

            path = path ?? string.Empty;
            _mainControl = mainControl;
            
            _remoteFileControl = new RemoteFileControl(_mainControl, state, editingServer, FileUtil.GetPathDirectory(path), false);
            _remoteFileControl.Dock = DockStyle.Fill;
            _remoteFileControl.Show();
            panelRemoteFile.Controls.Add(_remoteFileControl);

            Shown += (sender, args) =>
            {
                Text = title;
            };

        }

        public PanoramaFile PanoramaServer;

        public SkylineBatchConfigManagerState State => _remoteFileControl.State;
        
        private void btnSave_Click(object sender, EventArgs e)
        {
            _cancelSource = new CancellationTokenSource();
            btnSave.Text = Resources.AddServerForm_btnAdd_Click_Verifying;
            btnSave.Enabled = false;
            _remoteFileControl.CheckPanoramaServer(_cancelSource.Token, DoneValidatingServer);
        }

        private void DoneValidatingServer(PanoramaFile panoramaFile, Exception error)
        {
            var cancelled = _cancelSource.IsCancellationRequested;
            _cancelSource = null;
            if (cancelled || error != null)
            {
                if (error != null)
                    RunUi(() => { AlertDlg.ShowError(this, Program.AppName(), error.Message); });
                RunUi(() =>
                {
                    btnSave.Enabled = true;
                    btnSave.Text = Resources.AddPanoramaTemplate_DoneValidatingServer_Save;
                });
                return;
            }

            PanoramaServer = panoramaFile;
            DialogResult = DialogResult.OK;
        }

        private void CancelValidate()
        {
            if (_cancelSource != null)
                _cancelSource.Cancel();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            _remoteFileControl.Clear();
        }

        private void AddPanoramaTemplate_FormClosing(object sender, FormClosingEventArgs e)
        {
            CancelValidate();
        }




        private void RunUi(Action action)
        {
            try
            {
                Invoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
