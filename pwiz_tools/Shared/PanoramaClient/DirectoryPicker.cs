using System;
using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    public partial class DirectoryPicker : Form
    {

        private FolderBrowser folders;
        public string State;

        public DirectoryPicker(Uri server, string user, string pass, bool showCheckBox, string state, bool showSkyFolders = false)
        {
            InitializeComponent();
            folders = new FolderBrowser(server, user, pass, false, showSkyFolders, state);
            folders.Dock = DockStyle.Fill;
            folderPanel.Controls.Add(folders);
            folders.NodeClick += MouseClick;
            up.Enabled = false;
            back.Enabled = false;
            forward.Enabled = false;
            checkBox1.Visible = showCheckBox;
        }

        public string Folder { get; set; }
        public string OKButtonText { get; set; }
        public string Server { get; set; }


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
            up.Enabled = folders.UpEnabled();
            back.Enabled = folders.BackEnabled();
            forward.Enabled = folders.ForwardEnabled();
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

        private void back_Click(object sender, EventArgs e)
        {
            back.Enabled = folders.BackEnabled();
            if (back.Enabled)
            {
                folders.BackClick();
            }
            back.Enabled = folders.BackEnabled();
            forward.Enabled = folders.ForwardEnabled();
            up.Enabled = folders.UpEnabled();
        }

        private void forward_Click(object sender, EventArgs e)
        {
            forward.Enabled = folders.ForwardEnabled();
            if (forward.Enabled)
            {
                folders.ForwardClick();
            }
            up.Enabled = folders.UpEnabled();
            back.Enabled = folders.BackEnabled();
            forward.Enabled = folders.ForwardEnabled();
        }

        private void up_Click(object sender, EventArgs e)
        {
            up.Enabled = folders.UpEnabled();
            if (up.Enabled)
            {
                folders.UpClick();
            }
            up.Enabled = folders.UpEnabled();
            back.Enabled = folders.BackEnabled();
            forward.Enabled = folders.ForwardEnabled();
        }

        public void MouseClick(object sender, EventArgs e)
        {
            up.Enabled = folders.UpEnabled();
            forward.Enabled = folders.ForwardEnabled();
            back.Enabled = folders.BackEnabled();
        }

        private void DirectoryPicker_FormClosing(object sender, FormClosingEventArgs e)
        {
            State = folders.ClosingState();
        }
    }
}
