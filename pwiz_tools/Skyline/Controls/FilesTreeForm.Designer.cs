
namespace pwiz.Skyline.Controls
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

            FilesTree.NodeMouseDoubleClick -= FilesTree_TreeNodeMouseDoubleClick;
            FilesTree.MouseMove -= FilesTree_MouseMove;

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FilesTreeForm));
            this.filesTree = new pwiz.Skyline.Controls.FilesTree();
            this.panel1 = new System.Windows.Forms.Panel();
            this.filesTreeContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.libraryExplorerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.manageResultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openContainingFolderMenuStripItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openLibraryInLibraryExplorerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectReplicateMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openAuditLogMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.filesTree.LabelEdit = true;
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
            this.libraryExplorerToolStripMenuItem,
            this.manageResultsToolStripMenuItem,
            this.openContainingFolderMenuStripItem,
            this.openLibraryInLibraryExplorerMenuItem,
            this.selectReplicateMenuItem,
            this.openAuditLogMenuItem});
            this.filesTreeContextMenu.Name = "contextMenuStrip1";
            resources.ApplyResources(this.filesTreeContextMenu, "filesTreeContextMenu");
            // 
            // libraryExplorerToolStripMenuItem
            // 
            resources.ApplyResources(this.libraryExplorerToolStripMenuItem, "libraryExplorerToolStripMenuItem");
            this.libraryExplorerToolStripMenuItem.Name = "libraryExplorerToolStripMenuItem";
            this.libraryExplorerToolStripMenuItem.Click += new System.EventHandler(this.FilesTree_OpenLibraryExplorerMenuItem);
            // 
            // manageResultsToolStripMenuItem
            // 
            resources.ApplyResources(this.manageResultsToolStripMenuItem, "manageResultsToolStripMenuItem");
            this.manageResultsToolStripMenuItem.Name = "manageResultsToolStripMenuItem";
            this.manageResultsToolStripMenuItem.Click += new System.EventHandler(this.FilesTree_ManageResultsMenuItem);
            // 
            // openContainingFolderMenuStripItem
            // 
            this.openContainingFolderMenuStripItem.Image = global::pwiz.Skyline.Properties.Resources.Folder;
            resources.ApplyResources(this.openContainingFolderMenuStripItem, "openContainingFolderMenuStripItem");
            this.openContainingFolderMenuStripItem.Name = "openContainingFolderMenuStripItem";
            this.openContainingFolderMenuStripItem.Click += new System.EventHandler(this.FilesTree_OpenContainingFolderMenuItem);
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
        private System.Windows.Forms.ToolStripMenuItem openContainingFolderMenuStripItem;
        private System.Windows.Forms.ToolStripMenuItem manageResultsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem libraryExplorerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openLibraryInLibraryExplorerMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectReplicateMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openAuditLogMenuItem;
    }
}
