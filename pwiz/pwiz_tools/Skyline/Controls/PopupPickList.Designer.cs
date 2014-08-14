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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PopupPickList));
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
            resources.ApplyResources(this.toolStrip1, "toolStrip1");
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tbbOk,
            this.tbbCancel,
            this.toolStripSeparator1,
            this.tbbFilter,
            this.tbbAutoManageChildren,
            this.toolStripSeparator2,
            this.tbbFind});
            this.toolStrip1.Name = "toolStrip1";
            // 
            // tbbOk
            // 
            this.tbbOk.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbOk.Image = global::pwiz.Skyline.Properties.Resources.GreenCheck;
            resources.ApplyResources(this.tbbOk, "tbbOk");
            this.tbbOk.Name = "tbbOk";
            this.tbbOk.Click += new System.EventHandler(this.tbbOk_Click);
            // 
            // tbbCancel
            // 
            this.tbbCancel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbCancel.Image = global::pwiz.Skyline.Properties.Resources.RedX;
            resources.ApplyResources(this.tbbCancel, "tbbCancel");
            this.tbbCancel.Name = "tbbCancel";
            this.tbbCancel.Click += new System.EventHandler(this.tbbCancel_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // tbbFilter
            // 
            this.tbbFilter.Checked = true;
            this.tbbFilter.CheckOnClick = true;
            this.tbbFilter.CheckState = System.Windows.Forms.CheckState.Checked;
            this.tbbFilter.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbFilter.Image = global::pwiz.Skyline.Properties.Resources.Filter;
            resources.ApplyResources(this.tbbFilter, "tbbFilter");
            this.tbbFilter.Name = "tbbFilter";
            this.tbbFilter.Click += new System.EventHandler(this.tbbFilter_Click);
            // 
            // tbbAutoManageChildren
            // 
            this.tbbAutoManageChildren.CheckOnClick = true;
            this.tbbAutoManageChildren.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbAutoManageChildren.Image = global::pwiz.Skyline.Properties.Resources.WandProhibit;
            resources.ApplyResources(this.tbbAutoManageChildren, "tbbAutoManageChildren");
            this.tbbAutoManageChildren.Name = "tbbAutoManageChildren";
            this.tbbAutoManageChildren.Click += new System.EventHandler(this.tbbAutoManageChildren_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(this.toolStripSeparator2, "toolStripSeparator2");
            // 
            // tbbFind
            // 
            this.tbbFind.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbFind.Image = global::pwiz.Skyline.Properties.Resources.Find;
            resources.ApplyResources(this.tbbFind, "tbbFind");
            this.tbbFind.Name = "tbbFind";
            this.tbbFind.Click += new System.EventHandler(this.tbbFind_Click);
            // 
            // cbItems
            // 
            resources.ApplyResources(this.cbItems, "cbItems");
            this.cbItems.Name = "cbItems";
            this.cbItems.UseVisualStyleBackColor = true;
            this.cbItems.CheckedChanged += new System.EventHandler(this.cbItems_CheckedChanged);
            // 
            // textSearch
            // 
            resources.ApplyResources(this.textSearch, "textSearch");
            this.textSearch.Name = "textSearch";
            this.textSearch.TextChanged += new System.EventHandler(this.textSearch_TextChanged);
            this.textSearch.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.textSearch_PreviewKeyDown);
            // 
            // cbSynchronize
            // 
            resources.ApplyResources(this.cbSynchronize, "cbSynchronize");
            this.cbSynchronize.Name = "cbSynchronize";
            this.cbSynchronize.UseVisualStyleBackColor = true;
            // 
            // pickListMulti
            // 
            resources.ApplyResources(this.pickListMulti, "pickListMulti");
            this.pickListMulti.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.pickListMulti.FormattingEnabled = true;
            this.pickListMulti.Name = "pickListMulti";
            this.pickListMulti.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.pickListMulti_DrawItem);
            this.pickListMulti.KeyDown += new System.Windows.Forms.KeyEventHandler(this.pickListMulti_KeyDown);
            this.pickListMulti.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pickListMulti_MouseDown);
            this.pickListMulti.MouseLeave += new System.EventHandler(this.pickListMulti_MouseLeave);
            this.pickListMulti.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pickListMulti_MouseMove);
            // 
            // PopupPickList
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
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