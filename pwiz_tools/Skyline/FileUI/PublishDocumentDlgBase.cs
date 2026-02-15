/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class PublishDocumentDlgBase : FormEx
    {
        /// <summary>
        /// The <see cref="TreeView"/> is configured to use a 32-bit color depth. Subclasses are responsible for adding images to <see cref="TreeView.ImageList"/>
        /// </summary>
        /// <param name="docContainer"></param>
        /// <param name="fileName"></param>
        /// <param name="fileFormatOnDisk"></param>
        public PublishDocumentDlgBase(IDocumentUIContainer docContainer, string fileName, DocumentFormat? fileFormatOnDisk)
        {
            IsLoaded = false;
            InitializeComponent();
            Icon = Resources.Skyline;

            DocumentUIContainer = docContainer;
            DocumentFormat = fileFormatOnDisk;

            tbFilePath.Text = FileTimeEx.GetTimeStampedFileName(fileName);

            treeViewFolders.ImageList = new ImageList { ColorDepth = ColorDepth.Depth32Bit };

            ServerTreeStateRestorer = new TreeViewStateRestorer(treeViewFolders);
        }

        public bool IsLoaded { get; set; }
        public string FileName => tbFilePath.Text;
        public bool AnonymousServersCheckboxVisible => cbAnonymousServers.Visible;

        internal TreeViewStateRestorer ServerTreeStateRestorer { get; set; }
        internal IDocumentUIContainer DocumentUIContainer { get; }
        internal DocumentFormat? DocumentFormat { get; }

        private void PublishDocumentDlg_Load(object sender, EventArgs e)
        {
            HandleDialogLoad();

            ServerTreeStateRestorer.RestoreExpansionAndSelection(LoadExpansionAndSelection());
            ServerTreeStateRestorer.UpdateTopNode();

            treeViewFolders.Select();

            IsLoaded = true;
        }

        internal virtual void HandleDialogLoad()
        {
        }

        /// <summary>
        /// Handle [OK] button press. Subclasses are responsible for setting the appropriate <see cref="DialogResult"/>.
        /// </summary>
        internal virtual void HandleDialogOk()
        {
        }

        internal virtual void HandleUpload(Control parent, string localZipFilePath, string destinationPath)
        {
        }

        internal virtual string LoadExpansionAndSelection()
        {
            return string.Empty;
        }

        internal virtual void SaveExpansionAndSelection()
        {
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (treeViewFolders.SelectedNode == null || treeViewFolders.SelectedNode.Level == 0)
            {
                // Prompt the user to select a folder if no node is selected or only the top-level node
                // (the server name) is selected.
                MessageDlg.Show(this, Resources.PublishDocumentDlg_OkDialog_Please_select_a_folder);
                return;
            }

            HandleDialogOk();
        }

        public virtual void Upload(Control parent)
        {
            var folderPath = GetFolderPath(treeViewFolders.SelectedNode);
            var zipFilePath = tbFilePath.Text;

            HandleUpload(parent, folderPath, zipFilePath);
        }

        internal string GetFolderPath(TreeNode folderNode)
        {
            return GetFolderPath(folderNode.FullPath);
        }

        public virtual string GetFolderPath(string nodePath) 
        {
            // ReSharper disable LocalizableElement
            var folderPathSegments = nodePath.Split(new[] {"\\"}, StringSplitOptions.RemoveEmptyEntries);
            // ReSharper enable LocalizableElement

            var folderPath = string.Empty;
            // First segment is server name, so skip it
            for (var i = 1; i < folderPathSegments.Length; i++)
            {
                folderPath += folderPathSegments[i] + @"/";
            }
            return folderPath;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.InitialDirectory = Settings.Default.LibraryDirectory;
                dlg.SupportMultiDottedExtensions = true;
                dlg.DefaultExt = SrmDocumentSharing.EXT_SKY_ZIP;
                dlg.Filter = TextUtil.FileDialogFiltersAll(
                    FileUIResources.PublishDocumentDlg_btnBrowse_Click_Skyline_Shared_Documents,
                    SrmDocumentSharing.EXT);
                dlg.FileName = tbFilePath.Text;
                dlg.Title = FileUIResources.PublishDocumentDlg_btnBrowse_Click_Upload_Document;
                if (dlg.ShowDialog(Parent) == DialogResult.OK)
                {
                    tbFilePath.Text = dlg.FileName;
                }
            }
        }

        private TreeNode FindNode(TreeNode node, string item)
        {
            if (node.Text == item)
                return node;
            else
            {
                foreach (TreeNode childNode in node.Nodes)
                {
                    TreeNode nodeFound = FindNode(childNode, item);
                    if (nodeFound != null)
                        return nodeFound;
                }
            }
            return null;
        }

        public void SelectItem(string item)
        {
            foreach (TreeNode node in treeViewFolders.Nodes)
            {
                TreeNode selectedNode = FindNode(node, item);
                if (selectedNode != null)
                {
                    treeViewFolders.SelectedNode = selectedNode;
                    return;
                }
            }
        }

        public string GetSelectedNodeText()
        {
            return treeViewFolders.SelectedNode != null ? treeViewFolders.SelectedNode.Text : string.Empty;
        }

        private void treeViewFolders_DoubleClick(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void PublishDocumentDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveExpansionAndSelection();
        }
    }
}