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

        public RemoteFileForm(Server editingServer, string path, string title, IMainUiControl mainControl, SkylineBatchConfigManagerState state)
        {
            InitializeComponent();
            Icon = Program.Icon();

            path = path ?? string.Empty;
            _mainControl = mainControl;
            
            remoteFileControl = new RemoteFileControl(_mainControl, state, editingServer, FileUtil.GetPathDirectory(path), false);
            remoteFileControl.Dock = DockStyle.Fill;
            remoteFileControl.Show();
            panelRemoteFile.Controls.Add(remoteFileControl);

            Shown += (sender, args) =>
            {
                Text = title;
            };

        }


        public RemoteFileControl remoteFileControl;

        public PanoramaFile PanoramaServer;

        public SkylineBatchConfigManagerState State => remoteFileControl.State;
        
        private void btnSave_Click(object sender, EventArgs e)
        {
            _cancelSource = new CancellationTokenSource();
            btnSave.Text = Resources.AddServerForm_btnAdd_Click_Verifying;
            btnSave.Enabled = false;
            remoteFileControl.CheckPanoramaServer(_cancelSource.Token, DoneValidatingServer);
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
            remoteFileControl.Clear();
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
