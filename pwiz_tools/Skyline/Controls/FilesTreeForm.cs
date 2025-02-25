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
            if (FilesTree.SelectedNode is FilesTreeNode fileNode)
            {
                Process.Start(@"explorer.exe", $@"/select,""{fileNode.LocalFilePath}""");
            }
        }

        public void OpenLibraryExplorer(SpectralLibrary model)
        {
            SkylineWindow.OpenLibraryExplorer(model.Name);
        }

        public void ActivateReplicate(Replicate model)
        {
            // Replicates open using the replicate name, which is pulled from the parent tree node which
            // avoids adding a pointer to the parent in SrmSettings and adding a new field on the
            // replicate file's data model
            SkylineWindow.ActivateReplicate(model.Name);
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

            var filesTreeNode = FilesTree.SelectedNode as FilesTreeNode;

            switch (filesTreeNode?.Model)
            {
                // TODO: Menu Item: Open Library
                // TODO: Menu Item: Activate Replicate
                case ReplicatesFolder _:
                    manageResultsToolStripMenuItem.Visible = true;
                    manageResultsToolStripMenuItem.Enabled = true;
                    libraryExplorerToolStripMenuItem.Visible = false;
                    openContainingFolderMenuStripItem.Visible = false;
                    return;
                case SpectralLibrariesFolder _:
                    manageResultsToolStripMenuItem.Visible = false;
                    libraryExplorerToolStripMenuItem.Visible = true;
                    openContainingFolderMenuStripItem.Visible = false;
                    return;
                case BackgroundProteome _:
                case IonMobilityLibrary _:
                case OptimizationLibrary _:
                case ReplicateSampleFile _:
                case RTCalc _:
                case SkylineAuditLog _:
                case SkylineChromatogramCache _:
                case SkylineViewFile _:
                case SpectralLibrary _:
                    manageResultsToolStripMenuItem.Visible = false;
                    libraryExplorerToolStripMenuItem.Visible = false;
            
                    // only offer the Open Containing Folder option if the file currently exists - e.g. it hasn't been removed or deleted
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
                    ShowAuditLog();
                    break;
                case SpectralLibrary spectralLibrary:
                    OpenLibraryExplorer(spectralLibrary);
                    break;
                case ReplicateSampleFile _:
                    ActivateReplicate((Replicate)((FilesTreeNode)filesTreeNode.Parent).Model);
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