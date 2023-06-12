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
using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    public partial class PanoramaDirectoryPicker : Form
    {

        private PanoramaFolderBrowser folders;
        public string State;
        public string Selected;
        public string SelectedPath;
        public List<PanoramaServer> Servers { get; }

        public PanoramaDirectoryPicker(List<PanoramaServer> servers, string state, bool showSkyFolders = false, bool showWebDavFolders = false, string selectedPath = null)
        {
            InitializeComponent();
            Servers = servers;
            folders = new PanoramaFolderBrowser( false, showSkyFolders, state, Servers, selectedPath);
            folders.Dock = DockStyle.Fill;
            folderPanel.Controls.Add(folders);
            folders.NodeClick += DirectoryPicker_MouseClick;
            up.Enabled = false;
            SelectedPath = selectedPath;
            back.Enabled = false;
            forward.Enabled = false;
            if (showWebDavFolders)
            {
                folders.ShowWebDav = true;
            }
        }

        public string Folder { get; set; }
        public string OKButtonText { get; set; }



        private void open_Click(object sender, EventArgs e)
        {
            //Return the selected folder path
            Folder = folders.FolderPath;
            DialogResult = DialogResult.Yes;
            Close();
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


        public void DirectoryPicker_MouseClick(object sender, EventArgs e)
        {
            up.Enabled = folders.UpEnabled();
            forward.Enabled = false;
            back.Enabled = folders.BackEnabled();
        }

        private void DirectoryPicker_FormClosing(object sender, FormClosingEventArgs e)
        {
            State = folders.ClosingState();
            Selected = string.Concat(folders.ActiveServer.URI, "_webdav", folders.FolderPath);
        }

        private void checkEnabled()
        {
            up.Enabled = folders.UpEnabled();
            forward.Enabled = folders.ForwardEnabled();
            back.Enabled = folders.BackEnabled();
        }

        private void up_Click(object sender, EventArgs e)
        {
            up.Enabled = folders.UpEnabled();
            folders.UpClick();
            checkEnabled();
            forward.Enabled = false;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
