namespace pwiz.Skyline.Controls.Databinding
{
    partial class LiveResultsGrid
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
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LiveResultsGrid));
            this.contextMenuResultsGrid = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.synchronizeSelectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.chooseColumnsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuResultsGrid.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuResultsGrid
            // 
            this.contextMenuResultsGrid.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.synchronizeSelectionContextMenuItem,
            this.chooseColumnsToolStripMenuItem});
            this.contextMenuResultsGrid.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuResultsGrid, "contextMenuResultsGrid");
            this.contextMenuResultsGrid.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenu_Opening);
            // 
            // synchronizeSelectionContextMenuItem
            // 
            this.synchronizeSelectionContextMenuItem.CheckOnClick = true;
            this.synchronizeSelectionContextMenuItem.Name = "synchronizeSelectionContextMenuItem";
            resources.ApplyResources(this.synchronizeSelectionContextMenuItem, "synchronizeSelectionContextMenuItem");
            this.synchronizeSelectionContextMenuItem.Click += new System.EventHandler(this.synchronizeSelectionContextMenuItem_Click);
            // 
            // chooseColumnsToolStripMenuItem
            // 
            this.chooseColumnsToolStripMenuItem.Name = "chooseColumnsToolStripMenuItem";
            resources.ApplyResources(this.chooseColumnsToolStripMenuItem, "chooseColumnsToolStripMenuItem");
            this.chooseColumnsToolStripMenuItem.Click += new System.EventHandler(this.chooseColumnsToolStripMenuItem_Click);
            // 
            // LiveResultsGrid
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "LiveResultsGrid";
            this.contextMenuResultsGrid.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip contextMenuResultsGrid;
        private System.Windows.Forms.ToolStripMenuItem synchronizeSelectionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chooseColumnsToolStripMenuItem;
    }
}