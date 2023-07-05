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


        public PanoramaDirectoryPicker(List<PanoramaServer> servers, string state, bool showSkyFolders = false, bool showWebDavFolders = false, string selectedPath = null)
        {
            InitializeComponent();
            Servers = servers;
            FolderBrowser = new PanoramaFolderBrowser( false, showSkyFolders, state, Servers, selectedPath);
            FolderBrowser.Dock = DockStyle.Fill;
            folderPanel.Controls.Add(FolderBrowser);
            FolderBrowser.NodeClick += DirectoryPicker_MouseClick;
            up.Enabled = false;
            Selected = selectedPath;
            back.Enabled = false;
            forward.Enabled = false;
            if (showWebDavFolders)
            {
                FolderBrowser.ShowWebDav = true;
            }
        }

        /// <summary>
        /// This constructor is used for testing purposes
        /// </summary>
        public PanoramaDirectoryPicker()
        {
            InitializeComponent();
            IsLoaded = false;
        }

        /// <summary>
        /// This method is used for testing purposes
        /// </summary>
        public void InitializeTestDialog(Uri serverUri, string user, string pass, JToken folderJson)
        {
            var server = new PanoramaServer(serverUri, user, pass);
            FolderBrowser = new PanoramaFolderBrowser(server, folderJson);
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

        public string Folder { get; set; }
        public string OKButtonText { get; set; }
        public PanoramaFolderBrowser FolderBrowser;
        public string State;
        public string Selected;
        public bool IsLoaded { get; set; }
        public List<PanoramaServer> Servers { get; }
        public PanoramaServer ActiveServer { get; private set; }
        public string TreeState { get; set; }



        private void open_Click(object sender, EventArgs e)
        {
            //Return the selected folder path
            Folder = FolderBrowser.FolderPath;
            DialogResult = DialogResult.Yes;
            Close();
        }


        private void DirectoryPicker_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(OKButtonText))
            {
                open.Text = OKButtonText;
            }
            urlLink.Text = FolderBrowser.SelectedUrl;

        }

        private void back_Click(object sender, EventArgs e)
        {
            back.Enabled = FolderBrowser.BackEnabled();
            FolderBrowser.BackClick();
            CheckEnabled();
        }

        private void forward_Click(object sender, EventArgs e)
        {
            forward.Enabled = FolderBrowser.ForwardEnabled();
            FolderBrowser.ForwardClick();
            CheckEnabled();
        }


        public void DirectoryPicker_MouseClick(object sender, EventArgs e)
        {
            urlLink.Text = FolderBrowser.SelectedUrl;
            up.Enabled = FolderBrowser.UpEnabled();
            forward.Enabled = false;
            back.Enabled = FolderBrowser.BackEnabled();
        }

        private void DirectoryPicker_FormClosing(object sender, FormClosingEventArgs e)
        {
            State = FolderBrowser.ClosingState();
            Selected = string.Concat(FolderBrowser.ActiveServer.URI, "_webdav", FolderBrowser.FolderPath);
        }

        private void CheckEnabled()
        {
            up.Enabled = FolderBrowser.UpEnabled();
            forward.Enabled = FolderBrowser.ForwardEnabled();
            back.Enabled = FolderBrowser.BackEnabled();
        }

        private void up_Click(object sender, EventArgs e)
        {
            up.Enabled = FolderBrowser.UpEnabled();
            FolderBrowser.UpClick();
            CheckEnabled();
            forward.Enabled = false;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// This method is used for testing purposes
        /// </summary>
        public void ClickBack()
        {
            FolderBrowser.BackClick();
            CheckEnabled();
        }

        /// <summary>
        /// This method is used for testing purposes
        /// </summary>
        public void ClickForward()
        {
            FolderBrowser.ForwardClick();
            CheckEnabled();
        }

        /// <summary>
        /// This method is used for testing purposes
        /// </summary>
        public void ClickUp()
        {
            FolderBrowser.UpClick();
            CheckEnabled();
        }

        /// <summary>
        /// This method is used for testing purposes
        /// </summary>
        public void ClickOpen()
        {
            open_Click(this, EventArgs.Empty);
        }

        /// <summary>
        /// This method is used for testing purposes
        /// </summary>
        public void ClickCancel()
        { 
            cancel_Click(this, EventArgs.Empty);
        }

        /// <summary>
        /// This method is used for testing purposes
        /// </summary>
        public bool UpEnabled()
        {
            return up.Enabled;
        }

        /// <summary>
        /// This method is used for testing purposes
        /// </summary>
        public bool BackEnabled()
        {
            return back.Enabled;
        }

        /// <summary>
        /// This method is used for testing purposes
        /// </summary>
        public bool ForwardEnabled()
        {
            return FolderBrowser.ForwardEnabled();
        }

        private void urlLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(urlLink.Text);
        }
    }
}
