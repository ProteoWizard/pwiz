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
            this.boundDataGridView = new pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx();
            this.contextMenuResultsGrid = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.synchronizeSelectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.chooseColumnsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bindingListSource = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.navBar = new pwiz.Common.DataBinding.Controls.NavBar();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).BeginInit();
            this.contextMenuResultsGrid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).BeginInit();
            this.SuspendLayout();
            // 
            // boundDataGridView
            // 
            this.boundDataGridView.AutoGenerateColumns = false;
            this.boundDataGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.boundDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridView.ContextMenuStrip = this.contextMenuResultsGrid;
            this.boundDataGridView.DataSource = this.bindingListSource;
            resources.ApplyResources(this.boundDataGridView, "boundDataGridView");
            this.boundDataGridView.Name = "boundDataGridView";
            this.boundDataGridView.DataBindingComplete += new System.Windows.Forms.DataGridViewBindingCompleteEventHandler(this.boundDataGridView_DataBindingComplete);
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
            // bindingListSource
            // 
            this.bindingListSource.RowSource = new object[0];
            this.bindingListSource.CurrentChanged += new System.EventHandler(this.bindingListSource_CurrentChanged);
            this.bindingListSource.ListChanged += new System.ComponentModel.ListChangedEventHandler(this.bindingListSource_ListChanged);
            // 
            // navBar
            // 
            resources.ApplyResources(this.navBar, "navBar");
            this.navBar.BindingListSource = this.bindingListSource;
            this.navBar.Name = "navBar";
            this.navBar.ShowViewsButton = true;
            // 
            // LiveResultsGrid
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.boundDataGridView);
            this.Controls.Add(this.navBar);
            this.Name = "LiveResultsGrid";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).EndInit();
            this.contextMenuResultsGrid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Common.DataBinding.Controls.BindingListSource bindingListSource;
        private pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx boundDataGridView;
        private Common.DataBinding.Controls.NavBar navBar;
        private System.Windows.Forms.ContextMenuStrip contextMenuResultsGrid;
        private System.Windows.Forms.ToolStripMenuItem synchronizeSelectionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chooseColumnsToolStripMenuItem;
    }
}