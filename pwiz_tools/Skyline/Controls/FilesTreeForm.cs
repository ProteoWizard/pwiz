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
            filesTree.InitializeTree(SkylineWindow);
            filesTree.NodeMouseDoubleClick += FilesTree_TreeNodeMouseDoubleClick;
            filesTree.MouseMove += FilesTree_MouseMove;

            // FilesTree => context menu
            filesTreeContextMenu.Opening += FilesTree_ContextMenuStrip_Opening;
            filesTree.ContextMenuStrip = filesTreeContextMenu;

            // FilesTree => tooltips
            _nodeTip = new NodeTip(this) { Parent = TopLevelControl };
        }

        public FilesTree FilesTree => filesTree;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineWindow SkylineWindow { get; private set; }

        // CONSIDER: encapsulate right-click actions to a separate class
        public void ShowAuditLog()
        {
            SkylineWindow.ShowAuditLog();
        }

        public void OpenContainingFolder(string folderPath)
        {
            Process.Start(@"explorer.exe", $@"/select, ""{folderPath}""");
        }

        public void OpenLibraryExplorer(string libraryName)
        {
            SkylineWindow.OpenLibraryExplorer(libraryName);
        }

        public void OpenReplicate(string name)
        {
            SkylineWindow.ActivateReplicate(name);
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

        // Any TreeNode => Open Context Menu
        private void FilesTree_ContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            var selectedNode = FilesTree.SelectedNode;

            _nodeTip.HideTip();

            if (selectedNode.GetType() == typeof(SkylineRootTreeNode))
            {
                openContainingFolderMenuStripItem.Enabled = FilesTree.Root.FilePath != null;
            }
            else if (selectedNode.GetType() == typeof(ReplicateTreeNode))
            {
                openContainingFolderMenuStripItem.Enabled = true;
            }
            else if (selectedNode.GetType() == typeof(PeptideLibraryTreeNode))
            {
                openContainingFolderMenuStripItem.Enabled = true;
            }
            else
            {
                openContainingFolderMenuStripItem.Enabled = false;
            }
        }

        private void FilesTree_ShowContainingFolderMenuItem_Click(object sender, System.EventArgs e)
        {
            if (FilesTree.SelectedNode.Tag is IFileModel file)
            {
                OpenContainingFolder(file.FilePath);
            }
        }

        private void FilesTree_TreeNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            switch (e.Node)
            {
                case AuditLogTreeNode _:
                    ShowAuditLog();
                    break;
                case PeptideLibraryTreeNode peptideLibrary:
                    OpenLibraryExplorer(peptideLibrary.Text);
                    break;
                case ReplicateTreeNode replicate:
                    OpenReplicate(replicate.ChromatogramName);
                    break;
            }
        }
    }
}