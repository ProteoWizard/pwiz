using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class AddServerForm : Form
    {

        private CancellationTokenSource _cancelValidate;

        public AddServerForm(DataServerInfo editingServerInfo)
        {
            InitializeComponent();
            Icon = Program.Icon();

            Server = editingServerInfo;
            UpdateUiServer();
        }

        public DataServerInfo Server;

        private void btnAdd_Click(object sender, EventArgs e)
        {
            btnSave.Text = Resources.AddServerForm_btnAdd_Click_Verifying;
            btnSave.Enabled = false;

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
                FinishConnectToServer(validationException);
                return;
            }


            _cancelValidate = new CancellationTokenSource();
            var connectToServer = new LongWaitOperation(_cancelValidate);
            connectToServer.Start(false, (OnProgress) =>
            {
                try
                {
                    Server.Validate();
                }
                catch (ArgumentException ex)
                {
                    validationException = ex;
                }

                FinishConnectToServer(validationException);
            }, (success) => { });
        }

        private void FinishConnectToServer(Exception validationException)
        {
            if (validationException != null || (_cancelValidate != null && _cancelValidate.IsCancellationRequested))
            {
                if (validationException != null) AlertDlg.ShowError(this, Program.AppName(), validationException.Message);
                Invoke(new Action(() =>
                {
                    btnSave.Enabled = true;
                    btnSave.Text = Resources.AddServerForm_FinishConnectToServer_Save;
                }));
                _cancelValidate = null;
                return;
            }
            DialogResult = DialogResult.OK;

            Invoke(new Action(Close));
        }

        private DataServerInfo GetServerFromUi()
        {
            if (string.IsNullOrWhiteSpace(textUrl.Text) &&
                string.IsNullOrWhiteSpace(textUserName.Text) &&
                string.IsNullOrWhiteSpace(textPassword.Text) &&
                string.IsNullOrWhiteSpace(textNamingPattern.Text))
                return null;
            return DataServerInfo.ServerFromUi(textUrl.Text, textUserName.Text, textPassword.Text, textNamingPattern.Text);
        }

        private void btnRemoveServer_Click(object sender, EventArgs e)
        {
            _cancelValidate?.Cancel();
            Server = null;
            UpdateUiServer();
        }

        private void UpdateUiServer()
        {
            textUrl.Text = Server != null ? Server.GetUrl() : string.Empty;
            textUserName.Text = Server != null ? Server.UserName : string.Empty;
            textPassword.Text = Server != null ? Server.Password : string.Empty;
            textNamingPattern.Text = Server != null ? Server.DataNamingPattern : string.Empty;
        }

        private void linkLabelRegex_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.regular-expressions.info/reference.html");
        }

        private void AddServerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cancelValidate?.Cancel();
        }
    }
}
