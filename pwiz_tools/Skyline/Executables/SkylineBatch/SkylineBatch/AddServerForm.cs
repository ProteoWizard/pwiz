using System;
using System.Diagnostics;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class AddServerForm : Form
    {
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
            var addText = btnSave.Text;
            btnSave.Text = Resources.AddServerForm_btnAdd_Click_Verifying;
            btnSave.Enabled = false;

            if (Server != null)
            {
                try
                {
                    Server = GetServerFromUi();
                    Server.Validate();
                }
                catch (ArgumentException ex)
                {
                    AlertDlg.ShowError(this, Program.AppName(), ex.Message);
                    btnSave.Enabled = true;
                    btnSave.Text = addText;
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
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
    }
}
