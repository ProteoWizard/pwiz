using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FluentFTP;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class DataServerControl : UserControl, IValidatorControl
    {

        private DataServerInfo _server;

        public DataServerControl(DataServerInfo initialServer)
        {
            InitializeComponent();
            _server = initialServer;
        }

        public object GetVariable() => _server;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;
            return true;/*
            if (_server == null) return true; // server was removed
            try
            {
                _server.QuickValidate();
                return true;
            }
            catch (ArgumentException e)
            {
                errorMessage = e.Message;
                ServerError(errorMessage);
                return false;
            }*/
        }

        private void ServerConnected()
        {
            SetServerStatus(Resources.DataServerControl_ServerConnected_Connected, Color.Green);
        }

        private void ServerRemoved()
        {
            SetServerStatus(Resources.DataServerControl_ServerRemoved_No_server, Color.Blue);
        }

        private void ServerError(string errorMessage)
        {
            SetServerStatus(errorMessage, Color.Red);
        }

        private void SetServerStatus(string text, Color textColor)
        {
            textStatus.Text = text;
            textStatus.ForeColor = textColor;
        }

        private void btnTryReconnect_Click(object sender, EventArgs e)
        {
            btnTryReconnect.Text = "Connecting...";
            btnTryReconnect.Enabled = false;
            btnEditServer.Enabled = false;
            var serverConnector = new ServerConnector();
            serverConnector.AddServer(_server);
            _ = serverConnector.GetFiles(_server, (a, b) => { }, SuccessfulConnect, UnsuccessfulConnect);
        }

        private void SuccessfulConnect(List<FtpListItem> ftpFiles)
        {
            Invoke(new Action(() =>
            {
                ServerConnected();
                btnTryReconnect.Text = Resources.DataServerControl_SuccessfulConnect_Try_Reconnecting_to_Server;
                btnTryReconnect.Enabled = true;
                btnEditServer.Enabled = true;
            }));
        }

        private void UnsuccessfulConnect(Exception e)
        {
            Invoke(new Action(() =>
            {
                ServerError(e.Message);
                btnTryReconnect.Text = Resources.DataServerControl_SuccessfulConnect_Try_Reconnecting_to_Server;
                btnTryReconnect.Enabled = true;
                btnEditServer.Enabled = true;
            }));
        }

        private void btnEditServer_Click(object sender, EventArgs e)
        {
            var addServerForm = new AddServerForm(_server);
            if (DialogResult.OK == addServerForm.ShowDialog(this))
            {
                _server = addServerForm.Server;
                if (_server != null)
                    ServerConnected();
                else
                    ServerRemoved();
            }
        }

        public void SetInput(object variable)
        {
            _server = (DataServerInfo) variable;
        }
    }
}
