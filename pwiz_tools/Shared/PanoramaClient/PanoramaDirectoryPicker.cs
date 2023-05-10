using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    public partial class PanoramaDirectoryPicker : Form
    {

        private FolderBrowser folders;
        public string State;
        public string Selected;
        public List<PanoramaServer> Servers { get; private set; }

        public PanoramaDirectoryPicker(List<PanoramaServer> servers, bool showCheckBox, string state, bool showSkyFolders = false)
        {
            InitializeComponent();
            Servers = servers;
            folders = new FolderBrowser( false, showSkyFolders, state, Servers);
            folders.Dock = DockStyle.Fill;
            folderPanel.Controls.Add(folders);
            folders.NodeClick += DirectoryPicker_MouseClick;
            up.Enabled = false;
            back.Enabled = false;
            forward.Enabled = false;
            checkBox1.Visible = showCheckBox;
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
            folders.BackClick();
            checkEnabled();
        }

        private void forward_Click(object sender, EventArgs e)
        {
            forward.Enabled = folders.ForwardEnabled();
            folders.ForwardClick();
            checkEnabled();
        }

        private void up_Click(object sender, EventArgs e)
        {
            up.Enabled = folders.UpEnabled();
            folders.UpClick();
            checkEnabled();
            forward.Enabled = false;
        }

        public void DirectoryPicker_MouseClick(object sender, EventArgs e)
        {
            up.Enabled = folders.UpEnabled();
            forward.Enabled = false;
            back.Enabled = folders.BackEnabled();
        }

        private void DirectoryPicker_FormClosing(object sender, FormClosingEventArgs e)
        {
            State = folders.ClosingState();
            Selected = folders.FolderPath;
        }

        private void checkEnabled()
        {
            up.Enabled = folders.UpEnabled();
            forward.Enabled = folders.ForwardEnabled();
            back.Enabled = folders.BackEnabled();
        }
    }
}
