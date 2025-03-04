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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model.FilesView;
using Process = System.Diagnostics.Process;
using pwiz.Skyline.SettingsUI;

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

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FilesTree FilesTree => filesTree;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineWindow SkylineWindow { get; }

        protected override string GetPersistentString()
        {
            return base.GetPersistentString() + @"|" + FilesTree.GetPersistentString();
        }

        public void OpenAuditLog()
        {
            SkylineWindow.ShowAuditLog();
        }

        public void OpenContainingFolder(TreeNode selectedNode)
        {
            if (FilesTree.SelectedNode is FilesTreeNode filesTreeNode)
            {
                Process.Start(@"explorer.exe", $@"/select,""{filesTreeNode.LocalFilePath}""");
            }
        }

        public void OpenLibraryExplorerDialog(TreeNode selectedNode)
        {
            var model = (SpectralLibrary)((FilesTreeNode)selectedNode).Model;
            var libraryName = model.Name;

            var index = SkylineWindow.OwnedForms.IndexOf(form => form is ViewLibraryDlg);

            // Change the selected library if Library Explorer is already available
            if (index != -1)
            {
                var viewLibraryDlg = SkylineWindow.OwnedForms[index] as ViewLibraryDlg;
                viewLibraryDlg?.Activate();
                viewLibraryDlg?.ChangeSelectedLibrary(libraryName);
            }
            // If not, open a new Library Explorer.
            else
            {
                SkylineWindow.OpenLibraryExplorer(libraryName);
            }
        }

        // Replicates open using the replicate name, which is pulled from the parent tree node which
        // avoids adding a pointer to the parent in SrmSettings and adding a new field on the
        // replicate file's data model
        public void ActivateReplicate(TreeNode selectedNode)
        {
            var filesTreeNode = (FilesTreeNode)selectedNode;
            Replicate model;
            switch (filesTreeNode.Model)
            {
                case ReplicateSampleFile _:
                    model = (Replicate)((FilesTreeNode)filesTreeNode.Parent).Model;
                    break;
                case Replicate replicate:
                    model = replicate;
                    break;
                default:
                    return;
            }

            SkylineWindow.ActivateReplicate(model.Name);
        }

        public void OpenManageResultsDialog()
        {
            SkylineWindow.ManageResults();
        }

        public void OpenLibraryExplorerDialog()
        {
            SkylineWindow.ViewSpectralLibraries();
        }

        #region ITipDisplayer implementation

        public Rectangle ScreenRect => Screen.GetBounds(FilesTree);

        public bool AllowDisplayTip => FilesTree.Focused;

        public Rectangle RectToScreen(Rectangle r)
        {
            return FilesTree.RectangleToScreen(r);
        }

        #endregion

        // TreeNode => Open Context Menu
        private void FilesTree_ContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            _nodeTip.HideTip();

            var filesTreeNode = FilesTree.SelectedNode as FilesTreeNode;
            
            libraryExplorerToolStripMenuItem.Visible = false;
            manageResultsToolStripMenuItem.Visible = false;
            openAuditLogMenuItem.Visible = false;
            openLibraryInLibraryExplorerMenuItem.Visible = false;
            openContainingFolderMenuStripItem.Visible = false;
            selectReplicateMenuItem.Visible = false;

            switch (filesTreeNode?.Model)
            {
                case ReplicatesFolder _:
                    manageResultsToolStripMenuItem.Visible = true;
                    manageResultsToolStripMenuItem.Enabled = true;
                    return;
                case Replicate _:
                case ReplicateSampleFile _:
                    selectReplicateMenuItem.Visible = true;

                    // only offer Open Containing Folder option if the file currently exists - e.g. it hasn't been removed or deleted
                    openContainingFolderMenuStripItem.Visible = true;
                    openContainingFolderMenuStripItem.Enabled = filesTreeNode.Model.LocalFileExists();
                    break;
                case SpectralLibrariesFolder _:
                    libraryExplorerToolStripMenuItem.Visible = true;
                    return;
                case SpectralLibrary _:
                    openLibraryInLibraryExplorerMenuItem.Visible = true;

                    // only offer Open Containing Folder option if the file currently exists - e.g. it hasn't been removed or deleted
                    openContainingFolderMenuStripItem.Visible = true;
                    openContainingFolderMenuStripItem.Enabled = filesTreeNode.Model.LocalFileExists();
                    break;
                case SkylineAuditLog _:
                    openAuditLogMenuItem.Visible = true;
                    
                    // only offer Open Containing Folder option if the file currently exists - e.g. it hasn't been removed or deleted
                    openContainingFolderMenuStripItem.Visible = true;
                    openContainingFolderMenuStripItem.Enabled = filesTreeNode.Model.LocalFileExists();
                    break;
                case BackgroundProteome _:
                case IonMobilityLibrary _:
                case OptimizationLibrary _:
                case RTCalc _:
                case SkylineChromatogramCache _:
                case SkylineViewFile _:
                case SkylineFileModel _:
                    // only offer Open Containing Folder option if the file currently exists - e.g. it hasn't been removed or deleted
                    openContainingFolderMenuStripItem.Visible = true;
                    openContainingFolderMenuStripItem.Enabled = filesTreeNode.Model.LocalFileExists();
                    return;
                default:
                    e.Cancel = true;
                    break;
            }
        }

        private void FilesTree_TreeNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var filesTreeNode = e.Node as FilesTreeNode;

            switch (filesTreeNode?.Model)
            {
                case SkylineAuditLog _:
                    OpenAuditLog();
                    break;
                case SpectralLibrary _:
                    OpenLibraryExplorerDialog(filesTreeNode); 
                    break;
                case ReplicateSampleFile _:
                    ActivateReplicate(filesTreeNode);
                    break;
            }
        }

        private void FilesTree_OpenContainingFolderMenuItem(object sender, EventArgs e)
        {
            OpenContainingFolder(FilesTree.SelectedNode);
        }

        private void FilesTree_ManageResultsMenuItem(object sender, EventArgs e)
        {
            OpenManageResultsDialog();
        }

        private void FilesTree_OpenLibraryExplorerMenuItem(object sender, EventArgs e) 
        {
            OpenLibraryExplorerDialog();
        }

        private void FilesTree_OpenAuditLogMenuItem(object sender, EventArgs e)
        {
            OpenAuditLog();
        }

        private void FilesTree_ActivateReplicateMenuItem(object sender, EventArgs e)
        {
            ActivateReplicate(FilesTree.SelectedNode);
        }

        private void FilesTree_OpenLibraryInLibraryExplorerMenuItem(object sender, EventArgs e)
        {
            OpenLibraryExplorerDialog(FilesTree.SelectedNode);
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

        private void FilesTree_MouseMove(Point location)
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
    }
}