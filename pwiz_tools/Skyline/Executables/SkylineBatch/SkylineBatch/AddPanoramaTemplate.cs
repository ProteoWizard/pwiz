﻿using System;
using System.Threading;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class AddPanoramaTemplate : Form
    {
        //private FilePathControl _folderControl;
        private string _folderPath;
        private CancellationTokenSource _cancelSource;

        public AddPanoramaTemplate(Server editingServer, string path)
        {
            InitializeComponent();
            Icon = Program.Icon();

            path = path ?? string.Empty;
            _folderPath = FileUtil.GetInitialDirectory(path);


            if (editingServer != null)
            {
                textUrl.Text = editingServer.URI.AbsoluteUri;
                textUserName.Text = editingServer.Username;
                textPassword.Text = editingServer.Password;
            }

        }

        public PanoramaFile PanoramaServer;

        private void btnAdd_Click(object sender, EventArgs e)
        {
           _cancelSource = new CancellationTokenSource();
            btnSave.Text = Resources.AddServerForm_btnAdd_Click_Verifying;
            btnSave.Enabled = false;

            new Thread(() =>
            {
                try
                {
                    var panoramaServer = PanoramaFileFromUI(_cancelSource.Token);
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
            RunUi(Close);
        }

        private void CancelValidate()
        {
            if (_cancelSource != null)
                _cancelSource.Cancel();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            textPassword.Text = string.Empty;
            textUrl.Text = string.Empty;
            textUserName.Text = string.Empty;
        }

        private PanoramaFile PanoramaFileFromUI(CancellationToken cancelToken)
        {
            if (textUrl.Text == string.Empty &&
                textUserName.Text == string.Empty &&
                textPassword.Text == string.Empty)
                return null;
            return PanoramaFile.PanoramaFileFromUI(new Server(textUrl.Text, textUserName.Text, textPassword.Text), _folderPath, cancelToken);
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
