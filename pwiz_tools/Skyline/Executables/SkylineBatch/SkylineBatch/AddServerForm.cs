using System;
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

            if (editingServerInfo != null)
            {
                textUrl.Text = editingServerInfo.Url;
                textUserName.Text = editingServerInfo.UserName;
                textPassword.Text = editingServerInfo.Password;
                textNamingPattern.Text = editingServerInfo.DataNamingPattern;
            }
        }

        public DataServerInfo Server;

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var addText = btnAdd.Text;
            btnAdd.Text = Resources.AddServerForm_btnAdd_Click_Verifying;
            btnAdd.Enabled = false;
            Server = GetServerFromUi();
            var valid = true;
            try
            {
                Server.Validate();
            }
            catch (ArgumentException ex)
            {
                valid = false;
                AlertDlg.ShowError(this, Program.AppName(), ex.Message);
            }
            if (valid)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                btnAdd.Enabled = true;
                btnAdd.Text = addText;
            }
        
        }

        private DataServerInfo GetServerFromUi()
        {
            return DataServerInfo.ServerFromUi(textUrl.Text, textUserName.Text, textPassword.Text, textNamingPattern.Text);
        }

        private void btnRemoveServer_Click(object sender, EventArgs e)
        {
            Server = null;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
