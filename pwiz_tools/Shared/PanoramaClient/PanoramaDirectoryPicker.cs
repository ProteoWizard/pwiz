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
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.PanoramaClient
{
    public partial class PanoramaDirectoryPicker : CommonFormEx
    {
        // The OkButtonText setter will be used in SkylineBatch
        public string OkButtonText { get; set; }
        public PanoramaFolderBrowser FolderBrowser { get; private set; }
        public string SelectedPath { get; private set; }
        public bool IsLoaded { get; private set; }

        private readonly List<PanoramaServer> _servers;
        private readonly bool _showWebDav;
        private string _treeState;

        public PanoramaDirectoryPicker(List<PanoramaServer> servers, string stateString, bool showWebDavFolders = false, string selectedPath = null)
        {
            InitializeComponent();

            _servers = servers;
            _treeState = stateString;
            _showWebDav = showWebDavFolders;

            SelectedPath = selectedPath;
        }

        public void InitializeDialog()
        {
            if (FolderBrowser == null)
            {
                if (_showWebDav)
                {
                    FolderBrowser = new WebDavBrowser(_servers.FirstOrDefault(), _treeState, SelectedPath);
                }
                else
                {
                    FolderBrowser = new LKContainerBrowser(_servers, _treeState, false, SelectedPath);
                }
            }
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
                up.Enabled = FolderBrowser.UpEnabled;
                back.Enabled = FolderBrowser.BackEnabled;
                forward.Enabled = FolderBrowser.ForwardEnabled;
            }

            IsLoaded = true;
        }

        private void Open_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.Yes;
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
            back.Enabled = FolderBrowser.BackEnabled;
            FolderBrowser.BackButtonClick();
            UpdateButtonState();
        }

        private void Forward_Click(object sender, EventArgs e)
        {
            forward.Enabled = FolderBrowser.ForwardEnabled;
            FolderBrowser.ForwardButtonClick();
            UpdateButtonState();
        }

        private void DirectoryPicker_MouseClick(object sender, EventArgs e)
        {
            urlLink.Text = FolderBrowser.GetSelectedUri();
            up.Enabled = FolderBrowser.UpEnabled;
            forward.Enabled = false;
            back.Enabled = FolderBrowser.BackEnabled;
        }

        private void DirectoryPicker_FormClosing(object sender, FormClosingEventArgs e)
        {
            _treeState = FolderBrowser.GetClosingTreeState();
            SelectedPath = FolderBrowser.GetSelectedFolderPath();
        }

        private void UpdateButtonState()
        {
            up.Enabled = FolderBrowser.UpEnabled;
            forward.Enabled = FolderBrowser.ForwardEnabled;
            back.Enabled = FolderBrowser.BackEnabled;
            urlLink.Text = FolderBrowser.GetSelectedUri();
        }

        private void Up_Click(object sender, EventArgs e)
        {
            up.Enabled = FolderBrowser.UpEnabled;
            FolderBrowser.UpButtonClick();
            UpdateButtonState();
            forward.Enabled = false;
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


        #region Test Support

        public PanoramaDirectoryPicker(Uri serverUri, string user, string pass, JToken folderJson)
        {
            InitializeComponent();

            var server = new PanoramaServer(serverUri, user, pass);
            _servers = new List<PanoramaServer> { server };

            FolderBrowser = new TestPanoramaFolderBrowser(server, folderJson);

            InitializeDialog();

            IsLoaded = true;
        }

        #endregion
    }
}
