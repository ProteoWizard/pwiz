using System;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class AddPanoramaTemplate : Form
    {
        //private FilePathControl _folderControl;
        private string _folderPath;

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
            var addText = btnSave.Text;
            btnSave.Text = Resources.AddServerForm_btnAdd_Click_Verifying;
            btnSave.Enabled = false;
            var valid = true;
            try
            {
                PanoramaServer = PanoramaFileFromUI();
            }
            catch (Exception ex)
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
                btnSave.Enabled = true;
                btnSave.Text = addText;
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            textPassword.Text = string.Empty;
            textUrl.Text = string.Empty;
            textUserName.Text = string.Empty;
        }

        private PanoramaFile PanoramaFileFromUI()
        {
            if (textUrl.Text == string.Empty &&
                textUserName.Text == string.Empty &&
                textPassword.Text == string.Empty)
                return null;
            return PanoramaFile.PanoramaFileFromUI(new Server(textUrl.Text, textUserName.Text, textPassword.Text), _folderPath);
        }
    }
}
