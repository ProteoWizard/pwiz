using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentFTP;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class AddServerForm : Form
    {

        private CancellationTokenSource _cancelValidate;
        private readonly bool _serverRequired;

        public AddServerForm(DataServerInfo editingServerInfo, bool serverRequired = false)
        {
            InitializeComponent();
            Icon = Program.Icon();

            Server = editingServerInfo;
            _serverRequired = serverRequired;
            UpdateUiServer();

            if (_serverRequired)
                btnRemoveServer.Hide();
        }

        public DataServerInfo Server;
        public ServerConnector serverConnector { get; private set; }

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
                if (validationException != null)
                    UnsuccessfulConnect(validationException);
                else 
                    SuccessfulConnect(null);
                return;
            }


            _cancelValidate = new CancellationTokenSource();
            var connectToServer = new LongWaitOperation(_cancelValidate);
            serverConnector = new ServerConnector(Server);
            List<FtpListItem> serverFiles = null;
            Exception connectionException = null;
            connectToServer.Start(false,
                async (OnProgress) =>
                {
                    serverConnector.Connect(OnProgress);
                    serverFiles = serverConnector.GetFiles(Server, out connectionException);
                }, completed =>
                {
                    if (connectionException == null)
                        SuccessfulConnect(serverFiles);
                    else
                        UnsuccessfulConnect(connectionException);
                });
        }

        private void SuccessfulConnect(List<FtpListItem> ftpFiles)
        {
            if (Server != null)
            {
                if (ftpFiles.Count == 0)
                {
                    UnsuccessfulConnect(new ArgumentException(string.Format(
                        Resources
                            .DataServerInfo_Validate_There_were_no_files_found_at__0___Make_sure_the_URL__username__and_password_are_correct_and_try_again_,
                        Server.GetUrl())));
                    return;
                }
                var nameRegex = new Regex(Server.DataNamingPattern);
                var filesMatchRegex = false;
                foreach (var ftpFile in ftpFiles)
                {
                    if (nameRegex.IsMatch(ftpFile.Name))
                        filesMatchRegex = true;
                }

                if (!filesMatchRegex)
                {
                    UnsuccessfulConnect(new ArgumentException(
                        string.Format(
                            Resources
                                .DataServerInfo_Validate_None_of_the_file_names_on_the_server_matched_the_regular_expression___0_,
                            Server.DataNamingPattern) + Environment.NewLine +
                        Resources.DataServerInfo_Validate_Please_make_sure_your_regular_expression_is_correct_));
                    return;
                }
            }
            DialogResult = DialogResult.OK;
        }

        private void UnsuccessfulConnect(Exception e)
        {
            Invoke(new Action(() =>
            {
                if (e != null) AlertDlg.ShowError(this, Program.AppName(), e.Message);
                btnSave.Enabled = true;
                btnSave.Text = Resources.AddServerForm_FinishConnectToServer_Save;
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
                    throw new ArgumentException("The server cannot be empty. Please enter the server information.");
                return null;
            }
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
