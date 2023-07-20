/*
 * Original author: Sophie Pallanck <srpall .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace pwiz.PanoramaClient
{
    public partial class PanoramaDirectoryPicker : Form
    {

        public PanoramaDirectoryPicker(List<PanoramaServer> servers, string state, bool showWebDavFolders = false, string selectedPath = null)
        {
            InitializeComponent();
            Servers = servers;
            FolderBrowser = new PanoramaFolderBrowser( false,  state, Servers, selectedPath, showWebDavFolders);
            FolderBrowser.Dock = DockStyle.Fill;
            folderPanel.Controls.Add(FolderBrowser);
            FolderBrowser.NodeClick += DirectoryPicker_MouseClick;
            up.Enabled = false;
            SelectedPath = selectedPath;
            back.Enabled = false;
            forward.Enabled = false;
            FolderBrowser.ShowWebDav = showWebDavFolders;
        }

        public string OkButtonText { get; set; }
        public PanoramaFolderBrowser FolderBrowser;
        public string SelectedPath;
        public bool IsLoaded { get; set; }
        public List<PanoramaServer> Servers { get; }
        public PanoramaServer ActiveServer { get; private set; }
        public string TreeState { get; set; }



        private void open_Click(object sender, EventArgs e)
        {
            //Return the selected folder path
            DialogResult = DialogResult.Yes;
            Close();
        }


        private void DirectoryPicker_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(OkButtonText))
            {
                open.Text = OkButtonText;
            }
            urlLink.Text = FolderBrowser.SelectedUrl;

        }

        private void back_Click(object sender, EventArgs e)
        {
            back.Enabled = FolderBrowser.BackEnabled();
            FolderBrowser.BackClick();
            UpdateButtonState();
        }

        private void forward_Click(object sender, EventArgs e)
        {
            forward.Enabled = FolderBrowser.ForwardEnabled();
            FolderBrowser.ForwardClick();
            UpdateButtonState();
        }


        public void DirectoryPicker_MouseClick(object sender, EventArgs e)
        {
            urlLink.Text = FolderBrowser.SelectedUrl;
            up.Enabled = FolderBrowser.UpEnabled();
            forward.Enabled = false;
            back.Enabled = FolderBrowser.BackEnabled();
        }

        //TODO: What's the difference between SelectedPath and SelectedUrl
        private void DirectoryPicker_FormClosing(object sender, FormClosingEventArgs e)
        {
            TreeState = FolderBrowser.ClosingState();
            SelectedPath = string.Concat(FolderBrowser.ActiveServer.URI, PanoramaUtil.WEBDAV, FolderBrowser.FolderPath);
        }

        private void UpdateButtonState()
        {
            up.Enabled = FolderBrowser.UpEnabled();
            forward.Enabled = FolderBrowser.ForwardEnabled();
            back.Enabled = FolderBrowser.BackEnabled();
            urlLink.Text = FolderBrowser.SelectedUrl;
        }

        private void up_Click(object sender, EventArgs e)
        {
            up.Enabled = FolderBrowser.UpEnabled();
            FolderBrowser.UpClick();
            UpdateButtonState();
            forward.Enabled = false;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void urlLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip.Show(Cursor.Position);
            }
            else if (e.Button == MouseButtons.Left)
            {
                Process.Start(urlLink.Text);

            }
        }

        private void copyLinkAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(urlLink.Text);
        }


        #region MethodsForTesting
        public PanoramaDirectoryPicker()
        {
            InitializeComponent();
            IsLoaded = false;
        }

        public void InitializeTestDialog(Uri serverUri, string user, string pass, JToken folderJson)
        {
            var server = new PanoramaServer(serverUri, user, pass);
            FolderBrowser = new TestPanoramaFolderBrowser(server, folderJson);
            FolderBrowser.Dock = DockStyle.Fill;
            folderPanel.Controls.Add(FolderBrowser);
            FolderBrowser.NodeClick += DirectoryPicker_MouseClick;
            ActiveServer = server;
            if (string.IsNullOrEmpty(TreeState))
            {
                up.Enabled = false;
                back.Enabled = false;
                forward.Enabled = false;
            }
            else
            {
                up.Enabled = FolderBrowser.UpEnabled();
                back.Enabled = FolderBrowser.BackEnabled();
                forward.Enabled = FolderBrowser.ForwardEnabled();
            }
            IsLoaded = true;
        }

        public void ClickBack()
        {
            FolderBrowser.BackClick();
            UpdateButtonState();
        }

        public void ClickForward()
        {
            FolderBrowser.ForwardClick();
            UpdateButtonState();
        }

        public void ClickUp()
        {
            FolderBrowser.UpClick();
            UpdateButtonState();
        }

        public void ClickOpen()
        {
            open_Click(this, EventArgs.Empty);
        }

        public void ClickCancel()
        { 
            cancel_Click(this, EventArgs.Empty);
        }

        public bool UpEnabled()
        {
            return up.Enabled;
        }

        public bool BackEnabled()
        {
            return back.Enabled;
        }

        public bool ForwardEnabled()
        {
            return FolderBrowser.ForwardEnabled();
        }
        #endregion
    }
}
