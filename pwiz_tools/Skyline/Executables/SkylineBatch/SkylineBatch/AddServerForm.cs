using System;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class AddServerForm : Form
    {
        public AddServerForm()
        {
            InitializeComponent();
            Icon = Program.Icon();
        }

        public ServerInfo Server;

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

        private ServerInfo GetServerFromUi()
        {
            return new ServerInfo(textUrl.Text, textUserName.Text, textPassword.Text);
        }
    }
}
