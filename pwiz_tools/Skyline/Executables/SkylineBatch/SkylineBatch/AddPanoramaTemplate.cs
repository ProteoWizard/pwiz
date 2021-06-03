using System;
using System.IO;
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
                //initialPath = editingTemplate.PanoramaFile.DownloadFolder;
            }

            /*_folderControl = new FilePathControl("template file directory", initialPath, lastInputPath,
                SkylineTemplate.ValidateTemplateFile, PathDialogOptions.Folder);
            _folderControl.label2.Text = string.Empty;
            _folderControl.label2.Text = "Directory to download into:";
            _folderControl.Dock = DockStyle.Bottom;
            _folderControl.Show();
            panel1.Controls.Add(_folderControl);*/

        }

        public PanoramaFile PanoramaServer;

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var addText = btnAdd.Text;
            btnAdd.Text = Resources.AddServerForm_btnAdd_Click_Verifying;
            btnAdd.Enabled = false;
            var valid = true;
            try
            {
                //var uri = new Uri(textUrl.Text);
                //var server = PanoramaFile.ParseServer(new Server(textUrl.Text, textUserName.Text, textPassword.Text));
                //PanoramaFile.ValidatePanoramaServer(server);
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
                btnAdd.Enabled = true;
                btnAdd.Text = addText;
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
