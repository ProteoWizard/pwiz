namespace pwiz.Skyline.Controls.DataBinding
{
    partial class LiveReportForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LiveReportForm));
            this.navBar = new pwiz.Common.DataBinding.Controls.NavBar();
            this.bindingListSource = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.boundDataGridView = new pwiz.Common.DataBinding.Controls.BoundDataGridView();
            this.toolStripSkylineDataSource = new System.Windows.Forms.ToolStrip();
            this.toolStripDropDownRowSource = new System.Windows.Forms.ToolStripDropDownButton();
            this.proteinsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptidesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).BeginInit();
            this.toolStripSkylineDataSource.SuspendLayout();
            this.SuspendLayout();
            // 
            // navBar
            // 
            this.navBar.AutoSize = true;
            this.navBar.BindingListSource = this.bindingListSource;
            this.navBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.navBar.Location = new System.Drawing.Point(0, 0);
            this.navBar.Name = "navBar";
            this.navBar.Size = new System.Drawing.Size(772, 25);
            this.navBar.TabIndex = 0;
            // 
            // bindingListSource
            // 
            this.bindingListSource.RowSource = new object[0];
            this.bindingListSource.ViewInfo = null;
            // 
            // boundDataGridView
            // 
            this.boundDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridView.DataSource = this.bindingListSource;
            this.boundDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boundDataGridView.Location = new System.Drawing.Point(0, 25);
            this.boundDataGridView.Name = "boundDataGridView";
            this.boundDataGridView.Size = new System.Drawing.Size(772, 356);
            this.boundDataGridView.TabIndex = 1;
            // 
            // toolStripSkylineDataSource
            // 
            this.toolStripSkylineDataSource.AllowMerge = false;
            this.toolStripSkylineDataSource.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripDropDownRowSource,
            this.toolStripButton1});
            this.toolStripSkylineDataSource.Location = new System.Drawing.Point(0, 0);
            this.toolStripSkylineDataSource.Name = "toolStripSkylineDataSource";
            this.toolStripSkylineDataSource.Size = new System.Drawing.Size(772, 25);
            this.toolStripSkylineDataSource.TabIndex = 2;
            this.toolStripSkylineDataSource.Text = "toolStrip1";
            this.toolStripSkylineDataSource.Visible = false;
            // 
            // toolStripDropDownRowSource
            // 
            this.toolStripDropDownRowSource.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripDropDownRowSource.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.proteinsToolStripMenuItem,
            this.peptidesToolStripMenuItem,
            this.precursorsToolStripMenuItem,
            this.transitionsToolStripMenuItem});
            this.toolStripDropDownRowSource.Image = ((System.Drawing.Image)(resources.GetObject("toolStripDropDownRowSource.Image")));
            this.toolStripDropDownRowSource.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownRowSource.Name = "toolStripDropDownRowSource";
            this.toolStripDropDownRowSource.Size = new System.Drawing.Size(82, 22);
            this.toolStripDropDownRowSource.Text = "Row Source";
            // 
            // proteinsToolStripMenuItem
            // 
            this.proteinsToolStripMenuItem.Name = "proteinsToolStripMenuItem";
            this.proteinsToolStripMenuItem.Size = new System.Drawing.Size(132, 22);
            this.proteinsToolStripMenuItem.Text = "Proteins";
            this.proteinsToolStripMenuItem.Click += new System.EventHandler(this.ProteinsToolStripMenuItemOnClick);
            // 
            // peptidesToolStripMenuItem
            // 
            this.peptidesToolStripMenuItem.Name = "peptidesToolStripMenuItem";
            this.peptidesToolStripMenuItem.Size = new System.Drawing.Size(132, 22);
            this.peptidesToolStripMenuItem.Text = "Peptides";
            this.peptidesToolStripMenuItem.Click += new System.EventHandler(this.PeptidesToolStripMenuItemOnClick);
            // 
            // precursorsToolStripMenuItem
            // 
            this.precursorsToolStripMenuItem.Name = "precursorsToolStripMenuItem";
            this.precursorsToolStripMenuItem.Size = new System.Drawing.Size(132, 22);
            this.precursorsToolStripMenuItem.Text = "Precursors";
            this.precursorsToolStripMenuItem.Click += new System.EventHandler(this.precursorsToolStripMenuItem_Click);
            // 
            // transitionsToolStripMenuItem
            // 
            this.transitionsToolStripMenuItem.Name = "transitionsToolStripMenuItem";
            this.transitionsToolStripMenuItem.Size = new System.Drawing.Size(132, 22);
            this.transitionsToolStripMenuItem.Text = "Transitions";
            this.transitionsToolStripMenuItem.Click += new System.EventHandler(this.transitionsToolStripMenuItem_Click);
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(23, 22);
            this.toolStripButton1.Text = "toolStripButton1";
            // 
            // LiveReportForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(772, 381);
            this.Controls.Add(this.boundDataGridView);
            this.Controls.Add(this.navBar);
            this.Controls.Add(this.toolStripSkylineDataSource);
            this.Name = "LiveReportForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "Live Report";
            this.Text = "Live Report";
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).EndInit();
            this.toolStripSkylineDataSource.ResumeLayout(false);
            this.toolStripSkylineDataSource.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Common.DataBinding.Controls.NavBar navBar;
        private Common.DataBinding.Controls.BindingListSource bindingListSource;
        private Common.DataBinding.Controls.BoundDataGridView boundDataGridView;
        private System.Windows.Forms.ToolStrip toolStripSkylineDataSource;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownRowSource;
        private System.Windows.Forms.ToolStripMenuItem proteinsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptidesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem precursorsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transitionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
    }
}