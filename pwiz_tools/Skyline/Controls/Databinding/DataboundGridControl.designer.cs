namespace pwiz.Skyline.Controls.Databinding
{
    partial class DataboundGridControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DataboundGridControl));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.navBar = new pwiz.Common.DataBinding.Controls.NavBar();
            this.bindingListSource = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.sortAscendingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortDescendingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearSortToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.filterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearFilterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearAllFiltersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.fillDownToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.boundDataGridView = new pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).BeginInit();
            this.contextMenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // navBar
            // 
            resources.ApplyResources(this.navBar, "navBar");
            this.navBar.BindingListSource = this.bindingListSource;
            this.navBar.Name = "navBar";
            this.navBar.ShowViewsButton = true;
            // 
            // bindingListSource
            // 
            this.bindingListSource.RowSource = new object[0];
            this.bindingListSource.DataError += new System.Windows.Forms.BindingManagerDataErrorEventHandler(this.bindingListSource_DataError);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sortAscendingToolStripMenuItem,
            this.sortDescendingToolStripMenuItem,
            this.clearSortToolStripMenuItem,
            this.toolStripSeparator1,
            this.filterToolStripMenuItem,
            this.clearFilterToolStripMenuItem,
            this.clearAllFiltersToolStripMenuItem,
            this.toolStripSeparator2,
            this.fillDownToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            resources.ApplyResources(this.contextMenuStrip, "contextMenuStrip");
            this.contextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip_Opening);
            // 
            // sortAscendingToolStripMenuItem
            // 
            this.sortAscendingToolStripMenuItem.Name = "sortAscendingToolStripMenuItem";
            resources.ApplyResources(this.sortAscendingToolStripMenuItem, "sortAscendingToolStripMenuItem");
            this.sortAscendingToolStripMenuItem.Click += new System.EventHandler(this.sortAscendingToolStripMenuItem_Click);
            // 
            // sortDescendingToolStripMenuItem
            // 
            this.sortDescendingToolStripMenuItem.Name = "sortDescendingToolStripMenuItem";
            resources.ApplyResources(this.sortDescendingToolStripMenuItem, "sortDescendingToolStripMenuItem");
            this.sortDescendingToolStripMenuItem.Click += new System.EventHandler(this.sortDescendingToolStripMenuItem_Click);
            // 
            // clearSortToolStripMenuItem
            // 
            this.clearSortToolStripMenuItem.Name = "clearSortToolStripMenuItem";
            resources.ApplyResources(this.clearSortToolStripMenuItem, "clearSortToolStripMenuItem");
            this.clearSortToolStripMenuItem.Click += new System.EventHandler(this.clearSortToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // filterToolStripMenuItem
            // 
            this.filterToolStripMenuItem.Name = "filterToolStripMenuItem";
            resources.ApplyResources(this.filterToolStripMenuItem, "filterToolStripMenuItem");
            this.filterToolStripMenuItem.Click += new System.EventHandler(this.filterToolStripMenuItem_Click);
            // 
            // clearFilterToolStripMenuItem
            // 
            this.clearFilterToolStripMenuItem.Name = "clearFilterToolStripMenuItem";
            resources.ApplyResources(this.clearFilterToolStripMenuItem, "clearFilterToolStripMenuItem");
            this.clearFilterToolStripMenuItem.Click += new System.EventHandler(this.clearFilterToolStripMenuItem_Click);
            // 
            // clearAllFiltersToolStripMenuItem
            // 
            this.clearAllFiltersToolStripMenuItem.Name = "clearAllFiltersToolStripMenuItem";
            resources.ApplyResources(this.clearAllFiltersToolStripMenuItem, "clearAllFiltersToolStripMenuItem");
            this.clearAllFiltersToolStripMenuItem.Click += new System.EventHandler(this.clearAllFiltersToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(this.toolStripSeparator2, "toolStripSeparator2");
            // 
            // fillDownToolStripMenuItem
            // 
            this.fillDownToolStripMenuItem.Name = "fillDownToolStripMenuItem";
            resources.ApplyResources(this.fillDownToolStripMenuItem, "fillDownToolStripMenuItem");
            this.fillDownToolStripMenuItem.Click += new System.EventHandler(this.fillDownToolStripMenuItem_Click);
            // 
            // boundDataGridView
            // 
            this.boundDataGridView.AutoGenerateColumns = false;
            this.boundDataGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.boundDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.boundDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridView.ContextMenuStrip = this.contextMenuStrip;
            this.boundDataGridView.DataSource = this.bindingListSource;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.boundDataGridView.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.boundDataGridView, "boundDataGridView");
            this.boundDataGridView.Name = "boundDataGridView";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.boundDataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.boundDataGridView.CellContextMenuStripNeeded += new System.Windows.Forms.DataGridViewCellContextMenuStripNeededEventHandler(this.boundDataGridView_CellContextMenuStripNeeded);
            this.boundDataGridView.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.boundDataGridView_ColumnHeaderMouseClick);
            // 
            // DataboundGridControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.boundDataGridView);
            this.Controls.Add(this.navBar);
            this.Name = "DataboundGridControl";
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).EndInit();
            this.contextMenuStrip.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        protected Common.DataBinding.Controls.NavBar navBar;
        protected Common.DataBinding.Controls.BindingListSource bindingListSource;
        protected pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx boundDataGridView;
        private System.Windows.Forms.ToolStripMenuItem sortAscendingToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortDescendingToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearSortToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem filterToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearFilterToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearAllFiltersToolStripMenuItem;
        public System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem fillDownToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
    }
}