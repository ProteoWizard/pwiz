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
        // The OkButtonText setter will be used in SkylineBatch
        public string OkButtonText { get; set; }
        public PanoramaFolderBrowser FolderBrowser { get; private set; }
        public string SelectedPath { get; private set; }
        public bool IsLoaded { get; private set; }

        private string _treeState;

        public PanoramaDirectoryPicker(List<PanoramaServer> servers, string state, bool showWebDavFolders = false, string selectedPath = null)
        {
            InitializeComponent();
            FolderBrowser = new PanoramaFolderBrowser(servers, state, false, selectedPath, showWebDavFolders)
            {
                Dock = DockStyle.Fill
            };
            FolderBrowser.NodeClick += DirectoryPicker_MouseClick;
            folderPanel.Controls.Add(FolderBrowser);
            up.Enabled = false;
            SelectedPath = selectedPath;
            back.Enabled = false;
            forward.Enabled = false;
        }

        private void Open_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Yes;
            Close();
        }


        private void DirectoryPicker_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(OkButtonText))
            {
                open.Text = OkButtonText;
            }
            urlLink.Text = FolderBrowser.GetSelectedUri();

        }

        private void Back_Click(object sender, EventArgs e)
        {
            back.Enabled = FolderBrowser.BackEnabled();
            FolderBrowser.BackClick();
            UpdateButtonState();
        }

        private void Forward_Click(object sender, EventArgs e)
        {
            forward.Enabled = FolderBrowser.ForwardEnabled();
            FolderBrowser.ForwardClick();
            UpdateButtonState();
        }


        private void DirectoryPicker_MouseClick(object sender, EventArgs e)
        {
            urlLink.Text = FolderBrowser.GetSelectedUri();
            up.Enabled = FolderBrowser.UpEnabled();
            forward.Enabled = false;
            back.Enabled = FolderBrowser.BackEnabled();
        }

        private void DirectoryPicker_FormClosing(object sender, FormClosingEventArgs e)
        {
            _treeState = FolderBrowser.ClosingState();
            SelectedPath = FolderBrowser.GetSelectedFolderPath();
            
        }

        private void UpdateButtonState()
        {
            up.Enabled = FolderBrowser.UpEnabled();
            forward.Enabled = FolderBrowser.ForwardEnabled();
            back.Enabled = FolderBrowser.BackEnabled();
            urlLink.Text = FolderBrowser.GetSelectedUri();
        }

        private void Up_Click(object sender, EventArgs e)
        {
            up.Enabled = FolderBrowser.UpEnabled();
            FolderBrowser.UpClick();
            UpdateButtonState();
            forward.Enabled = false;
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void UrlLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Right:
                    contextMenuStrip.Show(Cursor.Position);
                    break;
                case MouseButtons.Left:
                    Process.Start(urlLink.Text);
                    break;
            }
        }

        private void CopyLinkAddressToolStripMenuItem_Click(object sender, EventArgs e)
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
            if (string.IsNullOrEmpty(_treeState))
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
            Open_Click(this, EventArgs.Empty);
        }

        public void ClickCancel()
        { 
            Cancel_Click(this, EventArgs.Empty);
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
