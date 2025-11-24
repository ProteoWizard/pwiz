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
        private bool _serverDataLoaded;

        /// <summary>
        /// This is the production version of the constructor, but it is only used in SkylineBatch.
        /// Since, Skyline does not use this dialog directly, it uses the test version below to ensure the class gets tested.
        /// </summary>
        /// <param name="servers">A list of <see cref="PanoramaServer"/> objects to use to populate the folder tree</param>
        /// <param name="stateString">A tree state string in the format understood by <see cref="TreeViewStateRestorer"/></param>
        /// <param name="showWebDavFolders">True to show folders using WebDAV, and false to use the JSON API from LabKey Server</param>
        /// <param name="selectedPath">A path string in the tree to select initially</param>
        public PanoramaDirectoryPicker(List<PanoramaServer> servers, string stateString, bool showWebDavFolders = false, string selectedPath = null)
        {
            InitializeComponent();

            _servers = servers;
            _treeState = stateString;
            _showWebDav = showWebDavFolders;
            _serverDataLoaded = showWebDavFolders; // WebDav does not require background data loading

            SelectedPath = selectedPath;

            InitializeDialog();
        }


        #region Test Support

        /// <summary>
        /// This constructor version is used in Skyline testing only.
        /// </summary>
        /// <param name="server">A single <see cref="PanoramaServer"/> object for which to show folders, passed directly as JSON</param>
        /// <param name="folderJson">The folder JSON for the server. The server is never called for its project folders</param>
        public PanoramaDirectoryPicker(PanoramaServer server, JToken folderJson)
        {
            InitializeComponent();

            _servers = new List<PanoramaServer> { server };
            _showWebDav = false;
            _serverDataLoaded = true; // Test class uses preloaded JSON, so no background data loading needed

            FolderBrowser = new TestPanoramaFolderBrowser(server, folderJson);

            InitializeDialog();

            IsLoaded = true;
        }

        #endregion

        private void InitializeDialog()
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
        }

        /// <summary>
        /// Loads server folder data from the web on a background thread.
        /// Must be called before ShowDialog() on a background thread.
        /// The TreeView will be populated automatically when the form loads (OnLoad lifecycle event).
        /// </summary>
        public void LoadServerData(IProgressMonitor progressMonitor)
        {
            Assume.IsFalse(IsHandleCreated, @"LoadServerData must be called before the form handle is created (before ShowDialog)");

            // Prevent duplicate loading if already called
            if (_serverDataLoaded)
                return;

            FolderBrowser.LoadServerData(progressMonitor);
            _serverDataLoaded = true;
        }

        /// <summary>
        /// Populates the TreeView with the loaded server data.
        /// Must be called on the UI thread after LoadServerData() completes.
        /// </summary>
        public void PopulateTreeView()
        {
            FolderBrowser?.PopulateTreeView();
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
            IsLoaded = true;
            
            // Populate TreeView with server data if it was loaded before the form was shown
            // Both LKContainerBrowser and WebDavBrowser require LoadServerData to be called first
            if (_serverDataLoaded)
            {
                PopulateTreeView();
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
    }
}
