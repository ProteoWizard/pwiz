/*
 * Copyright 2025 University of Washington - Seattle, WA
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

using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Util;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Controls
{
    public partial class FilesTreeForm : DockableFormEx, ITipDisplayer
    {
        private NodeTip _nodeTip;
        private readonly MoveThreshold _moveThreshold = new MoveThreshold(5, 5);

        public FilesTreeForm()
        {
            InitializeComponent();
        }

        public FilesTreeForm(SkylineWindow skylineWindow) : this()
        {
            SkylineWindow = skylineWindow;

            // FilesTree
            filesTree.NodeMouseDoubleClick += FilesTree_TreeNodeMouseDoubleClick;
            filesTree.MouseMove += FilesTree_MouseMove;
            filesTree.LostFocus += FilesTree_LostFocus;

            // FilesTree => context menu
            filesTreeContextMenu.Opening += FilesTree_ContextMenuStrip_Opening;
            filesTree.ContextMenuStrip = filesTreeContextMenu;

            // FilesTree => tooltips
            _nodeTip = new NodeTip(this) { Parent = TopLevelControl };

            filesTree.InitializeTree(SkylineWindow);
        }

        public FilesTree FilesTree => filesTree;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineWindow SkylineWindow { get; private set; }

        public void ShowAuditLog()
        {
            SkylineWindow.ShowAuditLog();
        }

        public void OpenContainingFolder(TreeNode selectedNode)
        {
            if (FilesTree.SelectedNode is FileNode fileNode)
            {
                Process.Start(@"explorer.exe", $@"/select,""{fileNode.LocalFilePath}""");
            }
        }

        public void OpenLibraryExplorer(string libraryName)
        {
            SkylineWindow.OpenLibraryExplorer(libraryName);
        }

        public void ActivateReplicate(FilesTreeNode node)
        {
            // Replicates open using the replicate name, which is pulled from the parent tree node which
            // avoids adding a pointer to the parent in SrmSettings and adding a new field on the
            // replicate file's data model
            if (node.Parent is ReplicateTreeNode parent)
                SkylineWindow.ActivateReplicate(parent.Name);
        }

        public void ShowManageResultsDialog()
        {
            SkylineWindow.ManageResults();
        }

        public void ShowSpectralLibrariesDialog()
        {
            SkylineWindow.ViewSpectralLibraries();
        }

        public void FilesTree_MouseMove(Point location)
        {
            if (!_moveThreshold.Moved(location))
                return;

            _moveThreshold.Location = null;

            var node = FilesTree.GetNodeAt(location) as TreeNodeMS;
            var tipProvider = node as ITipProvider;

            if (tipProvider != null && !tipProvider.HasTip)
                tipProvider = null;

            if (tipProvider != null)
            {
                var rectCapture = node.BoundsMS;
                if (!rectCapture.Contains(location))
                    _nodeTip.HideTip();
                else
                    _nodeTip.SetTipProvider(tipProvider, rectCapture, location);
            }
            else
            {
                _nodeTip?.HideTip();
            }
        }

        #region ITipDisplayer implementation

        public Rectangle ScreenRect => Screen.GetBounds(FilesTree);

        public bool AllowDisplayTip => FilesTree.Focused;

        public Rectangle RectToScreen(Rectangle r)
        {
            return FilesTree.RectangleToScreen(r);
        }

        #endregion

        protected override string GetPersistentString()
        {
            return base.GetPersistentString() + @"|" + FilesTree.GetPersistentString();
        }

        // FilesTree => display ToolTip
        private void FilesTree_MouseMove(object sender, MouseEventArgs e)
        {
            FilesTree_MouseMove(e.Location);
        }

        private void FilesTree_LostFocus(object sender, EventArgs e)
        {
            _nodeTip?.HideTip();
        }

        // TreeNode => Open Context Menu
        private void FilesTree_ContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            _nodeTip.HideTip();

            switch (FilesTree.SelectedNode)
            {
                case FolderNode folder when folder.FolderType == FolderType.replicates:
                    manageResultsToolStripMenuItem.Visible = true;
                    manageResultsToolStripMenuItem.Enabled = true;
                    libraryExplorerToolStripMenuItem.Visible = false;
                    openContainingFolderMenuStripItem.Visible = false;
                    return;
                case FolderNode folder when folder.FolderType == FolderType.peptide_libraries:
                    manageResultsToolStripMenuItem.Visible = false;
                    libraryExplorerToolStripMenuItem.Visible = true;
                    openContainingFolderMenuStripItem.Visible = false;
                    return;
                case FileNode file when file.Model.Type != FileType.replicate:
                    manageResultsToolStripMenuItem.Visible = false;
                    libraryExplorerToolStripMenuItem.Visible = false;

                    // only offer the option if the file currently exists and isn't removed or deleted
                    openContainingFolderMenuStripItem.Visible = true;
                    openContainingFolderMenuStripItem.Enabled = file.LocalFileExists();
                    return;
                default:
                    e.Cancel = true;
                    break;
            }
        }

        private void FilesTree_TreeNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            switch (e.Node)
            {
                case SkylineAuditLogTreeNode _:
                    ShowAuditLog();
                    break;
                case PeptideLibraryTreeNode peptideLibrary:
                    OpenLibraryExplorer(peptideLibrary.Text);
                    break;
                case ReplicateSampleFileTreeNode replicate:
                    ActivateReplicate(replicate);
                    break;
            }
        }

        private void FilesTree_ShowContainingFolderMenuItem(object sender, EventArgs e)
        {
            OpenContainingFolder(FilesTree.SelectedNode);
        }

        private void FilesTree_ManageResultsMenuItem(object sender, EventArgs e)
        {
            ShowManageResultsDialog();
        }

        private void FilesTree_LibraryExplorerMenuItem(object sender, EventArgs e) 
        {
            ShowSpectralLibrariesDialog();
        }
    }
}