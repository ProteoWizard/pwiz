using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    public partial class DirectoryPicker : Form
    {
        private Uri _serverUri;
        private string _user;
        private string _pass;
        private RemoteFolderBrowser folders;

        public DirectoryPicker(Uri server, string user, string pass)
        {
            _serverUri = server;
            _user = user;
            _pass = pass;
            InitializeComponent();
            folders = new RemoteFolderBrowser(server, user, pass, false);
            folders.Dock = DockStyle.Fill;
            folderPanel.Controls.Add(folders);
        }

        public string Folder { get; set; }

        private void cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void open_Click(object sender, EventArgs e)
        {
            //Return the selected folder path
            MessageBox.Show(folders.FolderPath);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            var type = checkBox1.Checked;
            folders.SwitchFolderType(type);
        }
    }
}
