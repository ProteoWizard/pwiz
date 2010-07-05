namespace pwiz.Skyline.Controls
{
    partial class PopupPickList
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
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.tbbOk = new System.Windows.Forms.ToolStripButton();
            this.tbbCancel = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.tbbFilter = new System.Windows.Forms.ToolStripButton();
            this.tbbAutoManageChildren = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.tbbFind = new System.Windows.Forms.ToolStripButton();
            this.cbItems = new System.Windows.Forms.CheckBox();
            this.textSearch = new System.Windows.Forms.TextBox();
            this.cbSynchronize = new System.Windows.Forms.CheckBox();
            this.pickListMulti = new System.Windows.Forms.ListBox();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Left;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tbbOk,
            this.tbbCancel,
            this.toolStripSeparator1,
            this.tbbFilter,
            this.tbbAutoManageChildren,
            this.toolStripSeparator2,
            this.tbbFind});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(24, 249);
            this.toolStrip1.TabIndex = 3;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // tbbOk
            // 
            this.tbbOk.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbOk.Image = global::pwiz.Skyline.Properties.Resources.GreenCheck;
            this.tbbOk.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tbbOk.Name = "tbbOk";
            this.tbbOk.Size = new System.Drawing.Size(21, 20);
            this.tbbOk.Text = "toolStripButton3";
            this.tbbOk.ToolTipText = "OK";
            this.tbbOk.Click += new System.EventHandler(this.tbbOk_Click);
            // 
            // tbbCancel
            // 
            this.tbbCancel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbCancel.Image = global::pwiz.Skyline.Properties.Resources.RedX;
            this.tbbCancel.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tbbCancel.Name = "tbbCancel";
            this.tbbCancel.Size = new System.Drawing.Size(21, 20);
            this.tbbCancel.ToolTipText = "Cancel";
            this.tbbCancel.Click += new System.EventHandler(this.tbbCancel_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(21, 6);
            // 
            // tbbFilter
            // 
            this.tbbFilter.Checked = true;
            this.tbbFilter.CheckOnClick = true;
            this.tbbFilter.CheckState = System.Windows.Forms.CheckState.Checked;
            this.tbbFilter.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbFilter.Image = global::pwiz.Skyline.Properties.Resources.Filter;
            this.tbbFilter.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tbbFilter.Name = "tbbFilter";
            this.tbbFilter.Size = new System.Drawing.Size(21, 20);
            this.tbbFilter.ToolTipText = "Filter";
            this.tbbFilter.Click += new System.EventHandler(this.tbbFilter_Click);
            // 
            // tbbAutoManageChildren
            // 
            this.tbbAutoManageChildren.CheckOnClick = true;
            this.tbbAutoManageChildren.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbAutoManageChildren.Image = global::pwiz.Skyline.Properties.Resources.WandProhibit;
            this.tbbAutoManageChildren.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tbbAutoManageChildren.Name = "tbbAutoManageChildren";
            this.tbbAutoManageChildren.Size = new System.Drawing.Size(21, 20);
            this.tbbAutoManageChildren.Click += new System.EventHandler(this.tbbAutoManageChildren_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(21, 6);
            // 
            // tbbFind
            // 
            this.tbbFind.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbFind.Image = global::pwiz.Skyline.Properties.Resources.Find;
            this.tbbFind.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tbbFind.Name = "tbbFind";
            this.tbbFind.Size = new System.Drawing.Size(21, 20);
            this.tbbFind.ToolTipText = "Find (Ctrl + F)";
            this.tbbFind.Click += new System.EventHandler(this.tbbFind_Click);
            // 
            // cbItems
            // 
            this.cbItems.AutoSize = true;
            this.cbItems.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbItems.Location = new System.Drawing.Point(32, 2);
            this.cbItems.Name = "cbItems";
            this.cbItems.Size = new System.Drawing.Size(56, 17);
            this.cbItems.TabIndex = 1;
            this.cbItems.Text = "Items";
            this.cbItems.UseVisualStyleBackColor = true;
            this.cbItems.CheckedChanged += new System.EventHandler(this.cbItems_CheckedChanged);
            // 
            // textSearch
            // 
            this.textSearch.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textSearch.Location = new System.Drawing.Point(51, 2);
            this.textSearch.Name = "textSearch";
            this.textSearch.Size = new System.Drawing.Size(352, 20);
            this.textSearch.TabIndex = 2;
            this.textSearch.Visible = false;
            this.textSearch.TextChanged += new System.EventHandler(this.textSearch_TextChanged);
            this.textSearch.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.textSearch_PreviewKeyDown);
            // 
            // cbSynchronize
            // 
            this.cbSynchronize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbSynchronize.AutoSize = true;
            this.cbSynchronize.Location = new System.Drawing.Point(32, 227);
            this.cbSynchronize.Name = "cbSynchronize";
            this.cbSynchronize.Size = new System.Drawing.Size(84, 17);
            this.cbSynchronize.TabIndex = 4;
            this.cbSynchronize.Text = "Synchronize";
            this.cbSynchronize.UseVisualStyleBackColor = true;
            // 
            // pickListMulti
            // 
            this.pickListMulti.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pickListMulti.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.pickListMulti.FormattingEnabled = true;
            this.pickListMulti.ItemHeight = 16;
            this.pickListMulti.Location = new System.Drawing.Point(29, 22);
            this.pickListMulti.Name = "pickListMulti";
            this.pickListMulti.Size = new System.Drawing.Size(374, 196);
            this.pickListMulti.TabIndex = 0;
            this.pickListMulti.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.pickListMulti_DrawItem);
            this.pickListMulti.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pickListMulti_MouseMove);
            this.pickListMulti.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pickListMulti_MouseDown);
            this.pickListMulti.MouseLeave += new System.EventHandler(this.pickListMulti_MouseLeave);
            this.pickListMulti.KeyDown += new System.Windows.Forms.KeyEventHandler(this.pickListMulti_KeyDown);
            // 
            // PopupPickList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(408, 249);
            this.ControlBox = false;
            this.Controls.Add(this.pickListMulti);
            this.Controls.Add(this.cbSynchronize);
            this.Controls.Add(this.textSearch);
            this.Controls.Add(this.cbItems);
            this.Controls.Add(this.toolStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PopupPickList";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.TopMost = true;
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton tbbCancel;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton tbbFilter;
        private System.Windows.Forms.CheckBox cbItems;
        private System.Windows.Forms.ToolStripButton tbbOk;
        private System.Windows.Forms.TextBox textSearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton tbbFind;
        private System.Windows.Forms.ToolStripButton tbbAutoManageChildren;
        private System.Windows.Forms.CheckBox cbSynchronize;
        private System.Windows.Forms.ListBox pickListMulti;
    }
}