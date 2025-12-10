
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

            if (FilesTree != null)
            {
                FilesTree.NodeMouseDoubleClick -= FilesTree_TreeNodeMouseDoubleClick;
                FilesTree.MouseDown -= FilesTree_MouseDown;
                FilesTree.MouseMove -= FilesTree_MouseMove;
                FilesTree.LostFocus -= FilesTree_LostFocus;
                FilesTree.BeforeLabelEdit -= FilesTree_BeforeLabelEdit;
                FilesTree.AfterLabelEdit -= FilesTree_AfterLabelEdit;
                FilesTree.AfterNodeEdit -= FilesTree_AfterLabelEdit;
                FilesTree.DragEnter -= FilesTree_DragEnter;
                FilesTree.DragLeave -= FilesTree_DragLeave;
                FilesTree.DragOver -= FilesTree_DragOver;
                FilesTree.DragDrop -= FilesTree_DragDrop;
                FilesTree.QueryContinueDrag -= FilesTree_QueryContinueDrag;
                FilesTree.KeyDown -= FilesTree_KeyDown;
                FilesTree.BeforeCollapse -= FilesTree_BeforeCollapse;
            }

            SkylineWindow.DocumentSavedEvent -= OnDocumentSavedEvent;
            SkylineWindow.DocumentUIChangedEvent -= OnDocumentUIChangedEvent;

            if (filesTreeContextMenu != null)
            {
                filesTreeContextMenu.Opening -= FilesTree_ContextMenuStrip_Opening;
            }

            if (_dropTargetRemove != null)
            {
                _dropTargetRemove.DragEnter -= DropTargetRemove_DragEnter;
                _dropTargetRemove.DragDrop -= DropTargetRemove_DragDrop;
                _dropTargetRemove.QueryContinueDrag -= FilesTree_QueryContinueDrag;
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
            this.showFileNamesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.libraryExplorerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.manageResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openContainingFolderMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openLibraryInLibraryExplorerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectReplicateMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openAuditLogMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debugRefreshTreeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel1.SuspendLayout();
            this.filesTreeContextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // filesTree
            // 
            resources.ApplyResources(this.filesTree, "filesTree");
            this.filesTree.AutoExpandSingleNodes = true;
            this.filesTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.filesTree.HideSelection = false;
            this.filesTree.ItemHeight = 16;
            this.filesTree.Name = "filesTree";
            this.filesTree.RestoredFromPersistentString = false;
            this.filesTree.UseKeysOverride = false;
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
            this.removeAllMenuItem,
            this.removeMenuItem,
            this.debugRefreshTreeMenuItem,
            this.contextMenuSeparator,
            this.showFileNamesMenuItem});
            this.filesTreeContextMenu.Name = "contextMenuStrip1";
            resources.ApplyResources(this.filesTreeContextMenu, "filesTreeContextMenu");
            //
            // showFileNamesMenuItem
            //
            this.showFileNamesMenuItem.CheckOnClick = true;
            this.showFileNamesMenuItem.Name = "showFileNamesMenuItem";
            resources.ApplyResources(this.showFileNamesMenuItem, "showFileNamesMenuItem");
            this.showFileNamesMenuItem.Click += new System.EventHandler(this.FilesTree_ShowFileNamesMenuItem);
            //
            // contextMenuSeparator
            //
            this.contextMenuSeparator.Name = "contextMenuSeparator";
            resources.ApplyResources(this.contextMenuSeparator, "contextMenuSeparator");
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
        private System.Windows.Forms.ToolStripMenuItem removeAllMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem debugRefreshTreeMenuItem;
    }
}
