using System;
using System.Threading;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class PanoramaFileForm : Form
    {
        private string _folderPath;
        private CancellationTokenSource _cancelSource;
        private IMainUiControl _mainControl;

        public PanoramaFileForm(Server editingServer, string path, string title, IMainUiControl mainControl, SkylineBatchConfigManagerState state)
        {
            InitializeComponent();
            Icon = Program.Icon();
            State = state;

            path = path ?? string.Empty;
            _folderPath = FileUtil.GetPathDirectory(path);
            _mainControl = mainControl;

            UpdateRemoteSourceList();

            if (editingServer != null)
            {
                textRelativePath.Text = editingServer.RelativePath;
                comboRemoteFileSource.SelectedItem = editingServer.FileSource.Name;
            }


            Shown += (sender, args) => Text = title;

        }

        public PanoramaFile PanoramaServer;

        public SkylineBatchConfigManagerState State;

        private void UpdateRemoteSourceList()
        {
            comboRemoteFileSource.Items.Clear();
            foreach (var name in State.FileSources.Keys)
                comboRemoteFileSource.Items.Add(name);
            comboRemoteFileSource.Items.Add("<Edit>");
            comboRemoteFileSource.Items.Add("<Add>");
        }


        private void btnSave_Click(object sender, EventArgs e)
        {
            if (comboRemoteFileSource.SelectedIndex == -1 && textRelativePath.Text == string.Empty)
            {

            }
                var remoteFileSource = comboRemoteFileSource.SelectedIndex > -1
                ? State.FileSources[comboRemoteFileSource.SelectedItem.ToString()]
                : null;

            _cancelSource = new CancellationTokenSource();
            btnSave.Text = Resources.AddServerForm_btnAdd_Click_Verifying;
            btnSave.Enabled = false;

            new Thread(() =>
            {
                try
                {
                    var panoramaServer = PanoramaFileFromUI(remoteFileSource, _cancelSource.Token);
                    DoneValidatingServer(panoramaServer, null);
                }
                catch (Exception ex)
                {
                    DoneValidatingServer(null, ex);
                }

            }).Start();
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
            comboRemoteFileSource.SelectedIndex = -1;
            textRelativePath.Text = string.Empty;
        }

        private PanoramaFile PanoramaFileFromUI(RemoteFileSource remoteFileSource, CancellationToken cancelToken)
        {
            if (remoteFileSource == null && string.IsNullOrEmpty(textRelativePath.Text))
                return null;
            return PanoramaFile.PanoramaFileFromUI(remoteFileSource, textRelativePath.Text, _folderPath, cancelToken);
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

        private void comboRemoteFileSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Edit
            if (comboRemoteFileSource.SelectedIndex == comboRemoteFileSource.Items.Count - 2)
            {
                var editSourceForm = new EditRemoteFileSourcesForm(_mainControl, State);
                var dialogResult = editSourceForm.ShowDialog(this);
                State = editSourceForm.State;
                UpdateRemoteSourceList();
                if (DialogResult.OK == dialogResult && editSourceForm.LastEditedName != null)
                    comboRemoteFileSource.SelectedItem = editSourceForm.LastEditedName;
                else
                    comboRemoteFileSource.SelectedIndex = -1;
            }
            // Add
            else if (comboRemoteFileSource.SelectedIndex == comboRemoteFileSource.Items.Count - 1)
            {
                var remoteSourceForm = new RemoteSourceForm(null, _mainControl, State);
                var dialogResult = remoteSourceForm.ShowDialog(this);
                State = remoteSourceForm.State;
                UpdateRemoteSourceList();
                if (DialogResult.OK == dialogResult)
                    comboRemoteFileSource.SelectedItem = remoteSourceForm.RemoteFileSource.Name;
                else
                    comboRemoteFileSource.SelectedIndex = -1;
            }
        }
    }
}
