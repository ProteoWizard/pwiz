using System;
using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    public partial class DirectoryPicker : Form
    {

        private FolderBrowser folders;

        public DirectoryPicker(Uri server, string user, string pass)
        {
            InitializeComponent();
            folders = new FolderBrowser(server, user, pass, false);
            folders.Dock = DockStyle.Fill;
            folderPanel.Controls.Add(folders);
        }

        public string Folder { get; set; }
        public string OKButtonText { get; set; }
        public string Server { get; set;  }


        private void cancel_Click_1(object sender, EventArgs e)
        {
            Close();
        }

        private void open_Click(object sender, EventArgs e)
        {
            //Return the selected folder path
            Folder = folders.FolderPath;
            DialogResult = DialogResult.Yes;
            Close();
        }

        private void checkBox1_CheckedChanged_1(object sender, EventArgs e)
        {
            var type = checkBox1.Checked;
            folders.SwitchFolderType(type);
        }


        private void DirectoryPicker_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(OKButtonText))
            {
                open.Text = @"Open";
            }
            else
            {
                open.Text = OKButtonText;
            }

        }
    }
}
