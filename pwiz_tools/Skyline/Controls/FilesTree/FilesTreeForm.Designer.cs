
namespace pwiz.Skyline.Controls.FilesTree
{
    public partial class FilesTreeForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            if (_nodeTip != null)
            {
                _nodeTip.Dispose();
                _nodeTip = null;
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FilesTreeForm));
            this.filesTree = new pwiz.Skyline.Controls.FilesTree.FilesTree();
            this.panel1 = new System.Windows.Forms.Panel();
            this.filesTreeContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.libraryExplorerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.manageResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openContainingFolderMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openLibraryInLibraryExplorerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectReplicateMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openAuditLogMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debugRefreshTreeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.showFileNamesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel1.SuspendLayout();
            this.filesTreeContextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // filesTree
            // 
            this.filesTree.AllowDrop = true;
            resources.ApplyResources(this.filesTree, "filesTree");
            this.filesTree.AutoExpandSingleNodes = true;
            this.filesTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.filesTree.HideSelection = false;
            this.filesTree.ItemHeight = 16;
            this.filesTree.LabelEdit = true;
            this.filesTree.Name = "filesTree";
            this.filesTree.RestoredFromPersistentString = false;
            this.filesTree.UseKeysOverride = false;
            this.filesTree.BeforeLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler(this.FilesTree_BeforeLabelEdit);
            this.filesTree.AfterLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler(this.FilesTree_AfterLabelEdit);
            this.filesTree.BeforeCollapse += new System.Windows.Forms.TreeViewCancelEventHandler(this.FilesTree_BeforeCollapse);
            this.filesTree.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.FilesTree_TreeNodeMouseDoubleClick);
            this.filesTree.DragDrop += new System.Windows.Forms.DragEventHandler(this.FilesTree_DragDrop);
            this.filesTree.DragEnter += new System.Windows.Forms.DragEventHandler(this.FilesTree_DragEnter);
            this.filesTree.DragOver += new System.Windows.Forms.DragEventHandler(this.FilesTree_DragOver);
            this.filesTree.DragLeave += new System.EventHandler(this.FilesTree_DragLeave);
            this.filesTree.QueryContinueDrag += new System.Windows.Forms.QueryContinueDragEventHandler(this.FilesTree_QueryContinueDrag);
            this.filesTree.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FilesTree_KeyDown);
            this.filesTree.MouseDown += new System.Windows.Forms.MouseEventHandler(this.FilesTree_MouseDown);
            this.filesTree.MouseLeave += new System.EventHandler(this.FilesTree_MouseLeave);
            this.filesTree.MouseMove += new System.Windows.Forms.MouseEventHandler(this.FilesTree_MouseMove);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.filesTree);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // filesTreeContextMenu
            // 
            this.filesTreeContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.libraryExplorerMenuItem,
            this.manageResultsMenuItem,
            this.openContainingFolderMenuItem,
            this.openLibraryInLibraryExplorerMenuItem,
            this.selectReplicateMenuItem,
            this.openAuditLogMenuItem,
            this.editMenuItem,
            this.removeAllMenuItem,
            this.removeMenuItem,
            this.debugRefreshTreeMenuItem,
            this.contextMenuSeparator,
            this.showFileNamesMenuItem});
            this.filesTreeContextMenu.Name = "contextMenuStrip1";
            resources.ApplyResources(this.filesTreeContextMenu, "filesTreeContextMenu");
            this.filesTreeContextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.FilesTree_ContextMenuStrip_Opening);
            // 
            // libraryExplorerMenuItem
            // 
            resources.ApplyResources(this.libraryExplorerMenuItem, "libraryExplorerMenuItem");
            this.libraryExplorerMenuItem.Name = "libraryExplorerMenuItem";
            this.libraryExplorerMenuItem.Click += new System.EventHandler(this.FilesTree_OpenLibraryExplorerMenuItem);
            // 
            // manageResultsMenuItem
            // 
            resources.ApplyResources(this.manageResultsMenuItem, "manageResultsMenuItem");
            this.manageResultsMenuItem.Name = "manageResultsMenuItem";
            this.manageResultsMenuItem.Click += new System.EventHandler(this.FilesTree_ManageResultsMenuItem);
            // 
            // openContainingFolderMenuItem
            // 
            this.openContainingFolderMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Folder;
            resources.ApplyResources(this.openContainingFolderMenuItem, "openContainingFolderMenuItem");
            this.openContainingFolderMenuItem.Name = "openContainingFolderMenuItem";
            this.openContainingFolderMenuItem.Click += new System.EventHandler(this.FilesTree_OpenContainingFolderMenuItem);
            // 
            // openLibraryInLibraryExplorerMenuItem
            // 
            this.openLibraryInLibraryExplorerMenuItem.Name = "openLibraryInLibraryExplorerMenuItem";
            resources.ApplyResources(this.openLibraryInLibraryExplorerMenuItem, "openLibraryInLibraryExplorerMenuItem");
            this.openLibraryInLibraryExplorerMenuItem.Click += new System.EventHandler(this.FilesTree_OpenLibraryInLibraryExplorerMenuItem);
            // 
            // selectReplicateMenuItem
            // 
            this.selectReplicateMenuItem.Name = "selectReplicateMenuItem";
            resources.ApplyResources(this.selectReplicateMenuItem, "selectReplicateMenuItem");
            this.selectReplicateMenuItem.Click += new System.EventHandler(this.FilesTree_ActivateReplicateMenuItem);
            // 
            // openAuditLogMenuItem
            // 
            this.openAuditLogMenuItem.Name = "openAuditLogMenuItem";
            resources.ApplyResources(this.openAuditLogMenuItem, "openAuditLogMenuItem");
            this.openAuditLogMenuItem.Click += new System.EventHandler(this.FilesTree_OpenAuditLogMenuItem);
            // 
            // editMenuItem
            // 
            this.editMenuItem.Name = "editMenuItem";
            resources.ApplyResources(this.editMenuItem, "editMenuItem");
            this.editMenuItem.Click += new System.EventHandler(this.FilesTree_EditMenuItem);
            // 
            // removeAllMenuItem
            // 
            this.removeAllMenuItem.Name = "removeAllMenuItem";
            resources.ApplyResources(this.removeAllMenuItem, "removeAllMenuItem");
            this.removeAllMenuItem.Click += new System.EventHandler(this.FilesTree_RemoveAllMenuItem);
            // 
            // removeMenuItem
            // 
            this.removeMenuItem.Name = "removeMenuItem";
            resources.ApplyResources(this.removeMenuItem, "removeMenuItem");
            this.removeMenuItem.Click += new System.EventHandler(this.FilesTree_RemoveMenuItem);
            // 
            // debugRefreshTreeMenuItem
            // 
            this.debugRefreshTreeMenuItem.Name = "debugRefreshTreeMenuItem";
            resources.ApplyResources(this.debugRefreshTreeMenuItem, "debugRefreshTreeMenuItem");
            this.debugRefreshTreeMenuItem.Click += new System.EventHandler(this.FilesTree_DebugRefreshTreeMenuItem);
            // 
            // contextMenuSeparator
            // 
            this.contextMenuSeparator.Name = "contextMenuSeparator";
            resources.ApplyResources(this.contextMenuSeparator, "contextMenuSeparator");
            // 
            // showFileNamesMenuItem
            // 
            this.showFileNamesMenuItem.CheckOnClick = true;
            this.showFileNamesMenuItem.Name = "showFileNamesMenuItem";
            resources.ApplyResources(this.showFileNamesMenuItem, "showFileNamesMenuItem");
            this.showFileNamesMenuItem.Click += new System.EventHandler(this.FilesTree_ShowFileNamesMenuItem);
            // 
            // FilesTreeForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.HideOnClose = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FilesTreeForm";
            this.ShowInTaskbar = false;
            this.panel1.ResumeLayout(false);
            this.filesTreeContextMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private FilesTree filesTree;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ContextMenuStrip filesTreeContextMenu;
        private System.Windows.Forms.ToolStripMenuItem showFileNamesMenuItem;
        private System.Windows.Forms.ToolStripSeparator contextMenuSeparator;
        private System.Windows.Forms.ToolStripMenuItem openContainingFolderMenuItem;
        private System.Windows.Forms.ToolStripMenuItem manageResultsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem libraryExplorerMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openLibraryInLibraryExplorerMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectReplicateMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openAuditLogMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeAllMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem debugRefreshTreeMenuItem;
    }
}
