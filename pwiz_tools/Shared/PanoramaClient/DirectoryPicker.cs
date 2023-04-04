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


        private void cancel_Click_1(object sender, EventArgs e)
        {
            Close();
        }

        private void open_Click(object sender, EventArgs e)
        {
            //Return the selected folder path
            Folder = folders.FolderPath;
            MessageBox.Show(Folder);
        }

        private void checkBox1_CheckedChanged_1(object sender, EventArgs e)
        {
            var type = checkBox1.Checked;
            folders.SwitchFolderType(type);
        }

        private void back_Click(object sender, EventArgs e)
        {

        }

        private void forward_Click(object sender, EventArgs e)
        {

        }

        private void up_Click(object sender, EventArgs e)
        {

        }


        private void DirectoryPicker_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(OKButtonText))
            {
                open.Text = "Open";
            }
            else
            {
                open.Text = OKButtonText;
            }
        }
    }
}
