namespace pwiz.Skyline.Controls
{
    partial class ResultsGridForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ResultsGridForm));
            this.contextMenuResultsGrid = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.synchronizeSelectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.chooseColumnsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel1 = new System.Windows.Forms.Panel();
            this.resultsGrid = new pwiz.Skyline.Controls.ResultsGrid();
            this.recordNavBar1 = new pwiz.Common.Controls.RecordNavBar();
            this.contextMenuResultsGrid.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.resultsGrid)).BeginInit();
            this.SuspendLayout();
            // 
            // contextMenuResultsGrid
            // 
            this.contextMenuResultsGrid.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.synchronizeSelectionContextMenuItem,
            this.chooseColumnsToolStripMenuItem});
            this.contextMenuResultsGrid.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuResultsGrid, "contextMenuResultsGrid");
            this.contextMenuResultsGrid.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuResultsGrid_Opening);
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
            // panel1
            // 
            this.panel1.Controls.Add(this.resultsGrid);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // resultsGrid
            // 
            this.resultsGrid.AllowUserToAddRows = false;
            this.resultsGrid.AllowUserToDeleteRows = false;
            this.resultsGrid.AllowUserToOrderColumns = true;
            resources.ApplyResources(this.resultsGrid, "resultsGrid");
            this.resultsGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.resultsGrid.ContextMenuStrip = this.contextMenuResultsGrid;
            this.resultsGrid.Name = "resultsGrid";
            // 
            // recordNavBar1
            // 
            this.recordNavBar1.DataGridView = this.resultsGrid;
            resources.ApplyResources(this.recordNavBar1, "recordNavBar1");
            this.recordNavBar1.Name = "recordNavBar1";
            // 
            // ResultsGridForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.recordNavBar1);
            this.HideOnClose = true;
            this.Name = "ResultsGridForm";
            this.ShowInTaskbar = false;
            this.contextMenuResultsGrid.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.resultsGrid)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private ResultsGrid resultsGrid;
        private System.Windows.Forms.ContextMenuStrip contextMenuResultsGrid;
        private System.Windows.Forms.ToolStripMenuItem chooseColumnsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem synchronizeSelectionContextMenuItem;
        private pwiz.Common.Controls.RecordNavBar recordNavBar1;
        private System.Windows.Forms.Panel panel1;
    }
}