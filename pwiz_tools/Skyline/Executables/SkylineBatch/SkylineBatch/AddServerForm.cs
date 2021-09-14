﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class AddServerForm : Form
    {

        private CancellationTokenSource _cancelValidate;
        private readonly bool _serverRequired;
        private readonly string _dataFolder;
        private bool _updated;

        public AddServerForm(DataServerInfo editingServerInfo, string folder, bool serverRequired = false)
        {
            InitializeComponent();
            Icon = Program.Icon();

            Server = editingServerInfo;
            _dataFolder = folder;
            _serverRequired = serverRequired;
            UpdateUiServer();

            if (_serverRequired)
                btnRemoveServer.Hide();
        }

        public DataServerInfo Server;
        public ServerConnector serverConnector { get; private set; }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (!_updated)
                CheckServer(true);
            else
            {
                Server = GetServerFromUi();
                if (Server == null)
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
            if (string.IsNullOrWhiteSpace(textUrl.Text) &&
                string.IsNullOrWhiteSpace(textUserName.Text) &&
                string.IsNullOrWhiteSpace(textPassword.Text) &&
                string.IsNullOrWhiteSpace(textNamingPattern.Text))
            {
                if (_serverRequired)
                    throw new ArgumentException(Resources.AddServerForm_GetServerFromUi_The_server_cannot_be_empty__Please_enter_the_server_information_);
                return null;
            }
            return DataServerInfo.ServerFromUi(textUrl.Text, textUserName.Text, textPassword.Text, !checkBoxNoEncryption.Checked, textNamingPattern.Text, _dataFolder);
        }

        private void btnRemoveServer_Click(object sender, EventArgs e)
        {
            _cancelValidate?.Cancel();
            listBoxFileNames.Items.Clear();
            Server = null;
            UpdateUiServer();
            _updated = true;
            UpdateLabel();
        }

        private void UpdateUiServer()
        {
            textUrl.Text = Server != null ? Server.GetUrl() : string.Empty;
            textUserName.Text = Server != null ? Server.Username : string.Empty;
            textPassword.Text = Server != null ? Server.Password : string.Empty;
            checkBoxNoEncryption.Enabled = Server != null && !string.IsNullOrEmpty(Server.Password);
            checkBoxNoEncryption.Checked = checkBoxNoEncryption.Enabled && Server != null && !Server.Encrypt;
            textNamingPattern.Text = Server == null || Server.DataNamingPattern.Equals(".*")
                ? string.Empty
                : Server.DataNamingPattern;
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

        private void text_TextChanged(object sender, EventArgs e)
        {
            _updated = false;
            serverConnector = null;
            UpdateLabel();

            checkBoxNoEncryption.Enabled = textPassword.Text.Length > 0;
            if (textPassword.Text.Length == 0)
                checkBoxNoEncryption.Checked = false;
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

        private void checkBoxNoEncryption_CheckedChanged(object sender, EventArgs e)
        {
            textPassword.PasswordChar = checkBoxNoEncryption.Checked ? '\0' : '*';
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
    }
}
