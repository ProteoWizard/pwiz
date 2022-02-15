using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class DataServerForm : Form
    {

        private CancellationTokenSource _cancelValidate;
        private readonly bool _serverRequired;
        private readonly string _dataFolder;
        private bool _updated;

        public DataServerForm(DataServerInfo editingServerInfo, string folder, SkylineBatchConfigManagerState state, IMainUiControl mainControl, bool serverRequired = false)
        {
            InitializeComponent();
            Icon = Program.Icon();

            Server = editingServerInfo;
            _dataFolder = folder;
            _serverRequired = serverRequired;

            remoteFileControl = new RemoteFileControl(mainControl, state, editingServerInfo, folder, serverRequired, false);
            remoteFileControl.Dock = DockStyle.Fill;
            remoteFileControl.Show();
            panelRemoteFile.Controls.Add(remoteFileControl);

            remoteFileControl.AddRemoteFileChangedEventHandler(RemoteFileChangedByUser);
            remoteFileControl.AddRelativePathChangedEventHandler(RemoteFileChangedByUser);


            textNamingPattern.Text = editingServerInfo != null ? editingServerInfo.DataNamingPattern : string.Empty;

            _updated = editingServerInfo == null;
            UpdateLabel();

            if (serverRequired)
                btnRemoveServer.Hide();
        }


        public RemoteFileControl remoteFileControl;

        public DataServerInfo Server;
        public ServerConnector serverConnector { get; private set; }
        public SkylineBatchConfigManagerState State => remoteFileControl.State;

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (!_updated)
                CheckServer(true);
            else
            {
                Server = GetServerFromUi();
                if (Server == null && !_serverRequired)
                {
                    DialogResult = DialogResult.OK;
                    return;
                }
                serverConnector.GetFiles(Server, out Exception error);
                if (error != null)
                    AlertDlg.ShowError(this, Program.AppName(), error.Message);
                else
                    DialogResult = DialogResult.OK;
            }
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            CheckServer(false);
        }

        private void CheckServer(bool closeWhenDone)
        {
            Exception validationException = null;
            try
            {
                Server = GetServerFromUi();
            }
            catch (ArgumentException ex)
            {
                validationException = ex;
                Server = null;
            }

            if (Server == null)
            {
                if (validationException != null)
                    UnsuccessfulConnect(validationException);
                else
                    SuccessfulConnect(new List<ConnectedFileInfo>(), closeWhenDone);
                return;
            }


            _cancelValidate = new CancellationTokenSource();
            var connectToServer = new LongWaitOperation(new LongWaitDlg(this, Program.AppName(), Resources.AddServerForm_CheckServer_Connecting_to_server___));
            serverConnector = new ServerConnector(Server);
            Exception connectionException = null;
            List<ConnectedFileInfo> files = null;
            connectToServer.Start(true,
                (OnProgress, cancelToken) =>
                {
                    serverConnector.Connect(OnProgress, cancelToken);
                    files = serverConnector.GetFiles(Server, out connectionException);
                }, completed =>
                {
                    if (!completed)
                        return;
                    _updated = true;
                    Invoke(new Action(() =>
                    {
                        listBoxFileNames.Items.Clear();
                    }));

                    if (connectionException == null)
                        SuccessfulConnect(files, closeWhenDone);
                    else
                        UnsuccessfulConnect(connectionException);
                });
        }

        private void SuccessfulConnect(List<ConnectedFileInfo> files, bool closeWhenDone)
        {
            if (closeWhenDone)
                DialogResult = DialogResult.OK;
            else
            {
                _updated = true;
                Invoke(new Action(() =>
                {
                    UpdateLabel();
                    UpdateFileList(files);
                }));
            }
        }

        private void UnsuccessfulConnect(Exception e)
        {
            Invoke(new Action(() =>
            {
                if (e != null) AlertDlg.ShowError(this, Program.AppName(), e.Message);

            }));
            _cancelValidate = null;
        }

        private DataServerInfo GetServerFromUi()
        {
            Server server;
            try
            {
                server = remoteFileControl.ServerFromUI();
            }
            catch (ArgumentException e)
            {
                AlertDlg.ShowError(this, Program.AppName(), e.Message);
                return null;
            }

            return server != null ? new DataServerInfo(server.FileSource, server.RelativePath, textNamingPattern.Text, _dataFolder) : null;
        }

        private void btnRemoveServer_Click(object sender, EventArgs e)
        {
            _cancelValidate?.Cancel();
            remoteFileControl.Clear();
            textNamingPattern.Text = string.Empty;
            listBoxFileNames.Items.Clear();
            Server = null;
            textNamingPattern.Text = string.Empty;
           _updated = true;
            UpdateLabel();
        }
        
        private void linkLabelRegex_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.regular-expressions.info/reference.html");
        }

        private void AddServerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cancelValidate?.Cancel();
        }

        private void UpdateLabel()
        {
            listBoxFileNames.Enabled = _updated;
            listBoxFileNames.BackColor = _updated ? Color.White : Color.WhiteSmoke;
            btnUpdate.Enabled = !_updated;
            labelFileInfo.Visible = _updated;
        }

        private void textNamingPattern_TextChanged(object sender, EventArgs e)
        {
            if (_updated)
            {
                var files = serverConnector.GetFiles(GetServerFromUi(), out _)?? new List<ConnectedFileInfo>();
                UpdateFileList(files);
            }
        }

        private void UpdateFileList(List<ConnectedFileInfo> files)
        {
            listBoxFileNames.Items.Clear();
            long totalSize = 0;
            foreach (var file in files)
            {
                listBoxFileNames.Items.Add(file.FileName + "\t" + GetSizeString(file.Size));
                totalSize += file.Size;
            }
            
            if (files.Count != 1)
                labelFileInfo.Text = string.Format(Resources.AddServerForm_UpdateFileList__0__files___1_, listBoxFileNames.Items.Count, GetSizeString(totalSize));
            else
                labelFileInfo.Text = string.Format(Resources.AddServerForm_UpdateFileList__1_file___0_, GetSizeString(totalSize));
        }

        private string GetSizeString(long bytes)
        {
            var sizeInGB = Math.Round(bytes / 1000000000.0, 2);
            var sizeInKB = -1.0;
            if (sizeInGB < 1)
            {
                sizeInGB = -1;
                sizeInKB = Math.Round(bytes / 1000.0);
            }

            if (sizeInGB > 0)
                return string.Format(Resources.AddServerForm_UpdateFileList__0__GB, sizeInGB);
            return string.Format(Resources.AddServerForm_UpdateFileList__0__KB, sizeInKB);
        }

        private void RemoteFileChangedByUser(object sender, EventArgs e)
        {
            try
            {
                _updated = remoteFileControl.RemoteFileSourceFromUI() == null;
            } catch (ArgumentException)
            {
                _updated = false;
            }
            UpdateLabel();
        }
    }
}
