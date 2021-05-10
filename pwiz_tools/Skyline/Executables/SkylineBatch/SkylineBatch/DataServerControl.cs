using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedBatch;

namespace SkylineBatch
{
    public partial class DataServerControl : UserControl, IValidatorControl
    {

        private DataServerInfo _server;

        private bool _checkedValidationOnce;

        //private bool _isValid;

        //private Exception _validationError;

        public DataServerControl(DataServerInfo initialServer)
        {
            InitializeComponent();
            _server = initialServer;
            //_isValid = AlreadyValidated();
        }

        public object GetVariable() => _server;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;

            try
            {
                _server.QuickValidate();
                return true;
            }
            catch (ArgumentException e)
            {
                labelStatus.Text = e.Message;
                return false;
            }
        }

        /*private bool AlreadyValidated()
        {
            try
            {
                _server.QuickValidate();
            }
            catch (ArgumentException e)
            {
                labelStatus.Text = e.Message;
                return false;
            }

            return true;
        }*/

        private void ServerConnected()
        {
            labelStatus.Text = "Connected";
            labelStatus.ForeColor = Color.Green;
        }

        private void btnTryReconnect_Click(object sender, EventArgs e)
        {
            var initialConnectText = btnTryReconnect.Text;
            btnTryReconnect.Text = "Connecting...";
            btnTryReconnect.Enabled = false;
            btnEditServer.Enabled = false;
            try
            {
                _server.Validate();
                ServerConnected();
            }
            catch (ArgumentException ex)
            {
               // _validationError = ex;
                labelStatus.Text = ex.Message;
            }
            btnTryReconnect.Text = initialConnectText;
            btnTryReconnect.Enabled = true;
            btnEditServer.Enabled = true;
        }

        private void btnEditServer_Click(object sender, EventArgs e)
        {
            var addServerForm = new AddServerForm(_server);
            if (DialogResult.OK == addServerForm.ShowDialog(this))
            {
                _server = addServerForm.Server;
                ServerConnected();
            }
        }

        public void SetInput(object variable)
        {
            _server = (DataServerInfo) variable;
        }

        private void maskedTextBox1_MaskInputRejected(object sender, MaskInputRejectedEventArgs e)
        {

        }
    }
}
