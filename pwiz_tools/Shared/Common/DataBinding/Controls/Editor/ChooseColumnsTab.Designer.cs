namespace pwiz.Common.DataBinding.Controls.Editor
{
    partial class ChooseColumnsTab
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ColumnHeader colHdrName;
            this.availableFieldsTreeColumns = new pwiz.Common.DataBinding.Controls.Editor.AvailableFieldsTree();
            this.listViewColumns = new System.Windows.Forms.ListView();
            this.toolStripColumns = new System.Windows.Forms.ToolStrip();
            this.btnRemove = new System.Windows.Forms.ToolStripButton();
            this.btnUp = new System.Windows.Forms.ToolStripButton();
            this.btnDown = new System.Windows.Forms.ToolStripButton();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            colHdrName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.toolStripColumns.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // colHdrName
            // 
            colHdrName.Text = "Name";
            // 
            // availableFieldsTreeColumns
            // 
            this.availableFieldsTreeColumns.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.availableFieldsTreeColumns.CheckBoxes = true;
            this.availableFieldsTreeColumns.CheckedColumns = new pwiz.Common.DataBinding.PropertyPath[0];
            this.availableFieldsTreeColumns.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
            this.availableFieldsTreeColumns.HideSelection = false;
            this.availableFieldsTreeColumns.ImageIndex = 0;
            this.availableFieldsTreeColumns.Location = new System.Drawing.Point(3, 3);
            this.availableFieldsTreeColumns.Name = "availableFieldsTreeColumns";
            this.availableFieldsTreeColumns.RootColumn = null;
            this.availableFieldsTreeColumns.SelectedImageIndex = 0;
            this.availableFieldsTreeColumns.ShowAdvancedFields = false;
            this.availableFieldsTreeColumns.Size = new System.Drawing.Size(434, 301);
            this.availableFieldsTreeColumns.TabIndex = 0;
            this.availableFieldsTreeColumns.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.AvailableFieldsTreeColumnsOnAfterCheck);
            this.availableFieldsTreeColumns.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.AvailableFieldsTreeColumnsOnNodeMouseDoubleClick);
            // 
            // listViewColumns
            // 
            this.listViewColumns.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            colHdrName});
            this.listViewColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewColumns.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listViewColumns.HideSelection = false;
            this.listViewColumns.LabelEdit = true;
            this.listViewColumns.Location = new System.Drawing.Point(0, 0);
            this.listViewColumns.Name = "listViewColumns";
            this.listViewColumns.ShowItemToolTips = true;
            this.listViewColumns.Size = new System.Drawing.Size(411, 301);
            this.listViewColumns.TabIndex = 0;
            this.listViewColumns.UseCompatibleStateImageBehavior = false;
            this.listViewColumns.View = System.Windows.Forms.View.Details;
            this.listViewColumns.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.listViewColumns_AfterLabelEdit);
            this.listViewColumns.ItemActivate += new System.EventHandler(this.ListViewColumnsOnItemActivate);
            this.listViewColumns.SelectedIndexChanged += new System.EventHandler(this.ListViewColumnsOnSelectedIndexChanged);
            this.listViewColumns.SizeChanged += new System.EventHandler(this.ListViewColumnsOnSizeChanged);
            // 
            // toolStripColumns
            // 
            this.toolStripColumns.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStripColumns.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripColumns.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnRemove,
            this.btnUp,
            this.btnDown});
            this.toolStripColumns.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStripColumns.Location = new System.Drawing.Point(411, 0);
            this.toolStripColumns.Name = "toolStripColumns";
            this.toolStripColumns.Size = new System.Drawing.Size(24, 301);
            this.toolStripColumns.TabIndex = 1;
            this.toolStripColumns.Text = "toolStrip1";
            // 
            // btnRemove
            // 
            this.btnRemove.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRemove.Image = global::pwiz.Common.Properties.Resources.Delete;
            this.btnRemove.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(21, 20);
            this.btnRemove.Text = "Remove";
            this.btnRemove.Click += new System.EventHandler(this.BtnRemoveOnClick);
            // 
            // btnUp
            // 
            this.btnUp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUp.Image = global::pwiz.Common.Properties.Resources.up_pro32;
            this.btnUp.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(21, 20);
            this.btnUp.Text = "Up";
            this.btnUp.Click += new System.EventHandler(this.BtnUpOnClick);
            // 
            // btnDown
            // 
            this.btnDown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDown.Image = global::pwiz.Common.Properties.Resources.down_pro32;
            this.btnDown.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(21, 20);
            this.btnDown.Text = "Down";
            this.btnDown.Click += new System.EventHandler(this.BtnDownOnClick);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Controls.Add(this.availableFieldsTreeColumns, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panel1, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(881, 307);
            this.tableLayoutPanel1.TabIndex = 2;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.listViewColumns);
            this.panel1.Controls.Add(this.toolStripColumns);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(443, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(435, 301);
            this.panel1.TabIndex = 2;
            // 
            // ChooseColumnsTab
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "ChooseColumnsTab";
            this.Size = new System.Drawing.Size(881, 307);
            this.toolStripColumns.ResumeLayout(false);
            this.toolStripColumns.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private AvailableFieldsTree availableFieldsTreeColumns;
        private System.Windows.Forms.ListView listViewColumns;
        private System.Windows.Forms.ToolStrip toolStripColumns;
        private System.Windows.Forms.ToolStripButton btnRemove;
        private System.Windows.Forms.ToolStripButton btnUp;
        private System.Windows.Forms.ToolStripButton btnDown;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panel1;
    }
}
