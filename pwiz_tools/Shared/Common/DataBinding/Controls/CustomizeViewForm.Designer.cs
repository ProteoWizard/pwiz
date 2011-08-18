namespace pwiz.Common.DataBinding.Controls
{
    partial class CustomizeViewForm
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
            System.Windows.Forms.ColumnHeader colHdrName;
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageColumns = new System.Windows.Forms.TabPage();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.btnAddColumn = new System.Windows.Forms.Button();
            this.listViewColumns = new System.Windows.Forms.ListView();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnRemove = new System.Windows.Forms.ToolStripButton();
            this.btnUp = new System.Windows.Forms.ToolStripButton();
            this.btnDown = new System.Windows.Forms.ToolStripButton();
            this.cbxHidden = new System.Windows.Forms.CheckBox();
            this.groupBoxAggregate = new System.Windows.Forms.GroupBox();
            this.comboBoxAggregate = new System.Windows.Forms.ComboBox();
            this.groupBoxCaption = new System.Windows.Forms.GroupBox();
            this.tbxCaption = new System.Windows.Forms.TextBox();
            this.tabPageFilter = new System.Windows.Forms.TabPage();
            this.tabPageSort = new System.Windows.Forms.TabPage();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxViewName = new System.Windows.Forms.TextBox();
            this.groupBoxSublist = new System.Windows.Forms.GroupBox();
            this.comboSublist = new System.Windows.Forms.ComboBox();
            this.groupBoxProperties = new System.Windows.Forms.GroupBox();
            this.btnAdvanced = new System.Windows.Forms.Button();
            this.splitContainerAdvanced = new System.Windows.Forms.SplitContainer();
            this.availableFieldsTreeColumns = new pwiz.Common.DataBinding.Controls.AvailableFieldsTree();
            colHdrName = new System.Windows.Forms.ColumnHeader();
            this.tabControl1.SuspendLayout();
            this.tabPageColumns.SuspendLayout();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.groupBoxAggregate.SuspendLayout();
            this.groupBoxCaption.SuspendLayout();
            this.groupBoxSublist.SuspendLayout();
            this.groupBoxProperties.SuspendLayout();
            this.splitContainerAdvanced.Panel1.SuspendLayout();
            this.splitContainerAdvanced.Panel2.SuspendLayout();
            this.splitContainerAdvanced.SuspendLayout();
            this.SuspendLayout();
            // 
            // colHdrName
            // 
            colHdrName.Text = "Name";
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPageColumns);
            this.tabControl1.Controls.Add(this.tabPageFilter);
            this.tabControl1.Controls.Add(this.tabPageSort);
            this.tabControl1.Location = new System.Drawing.Point(1, 29);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(895, 339);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPageColumns
            // 
            this.tabPageColumns.Controls.Add(this.splitContainerAdvanced);
            this.tabPageColumns.Location = new System.Drawing.Point(4, 22);
            this.tabPageColumns.Name = "tabPageColumns";
            this.tabPageColumns.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageColumns.Size = new System.Drawing.Size(887, 313);
            this.tabPageColumns.TabIndex = 0;
            this.tabPageColumns.Text = "Columns";
            this.tabPageColumns.UseVisualStyleBackColor = true;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.btnAddColumn);
            this.splitContainer1.Panel1.Controls.Add(this.availableFieldsTreeColumns);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.listViewColumns);
            this.splitContainer1.Panel2.Controls.Add(this.toolStrip1);
            this.splitContainer1.Size = new System.Drawing.Size(608, 307);
            this.splitContainer1.SplitterDistance = 254;
            this.splitContainer1.TabIndex = 1;
            // 
            // btnAddColumn
            // 
            this.btnAddColumn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddColumn.Location = new System.Drawing.Point(207, 48);
            this.btnAddColumn.Name = "btnAddColumn";
            this.btnAddColumn.Size = new System.Drawing.Size(44, 23);
            this.btnAddColumn.TabIndex = 1;
            this.btnAddColumn.Text = "Add>";
            this.btnAddColumn.UseVisualStyleBackColor = true;
            this.btnAddColumn.Click += new System.EventHandler(this.btnAddColumn_Click);
            // 
            // listViewColumns
            // 
            this.listViewColumns.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            colHdrName});
            this.listViewColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewColumns.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listViewColumns.HideSelection = false;
            this.listViewColumns.Location = new System.Drawing.Point(0, 0);
            this.listViewColumns.Name = "listViewColumns";
            this.listViewColumns.Size = new System.Drawing.Size(326, 307);
            this.listViewColumns.TabIndex = 2;
            this.listViewColumns.UseCompatibleStateImageBehavior = false;
            this.listViewColumns.View = System.Windows.Forms.View.Details;
            this.listViewColumns.SelectedIndexChanged += new System.EventHandler(this.listViewColumns_SelectedIndexChanged);
            this.listViewColumns.SizeChanged += new System.EventHandler(this.listViewColumns_SizeChanged);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnRemove,
            this.btnUp,
            this.btnDown});
            this.toolStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStrip1.Location = new System.Drawing.Point(326, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(24, 307);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnRemove
            // 
            this.btnRemove.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRemove.Image = global::pwiz.Common.Properties.Resources.Delete;
            this.btnRemove.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(21, 20);
            this.btnRemove.Text = "Remove";
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnUp
            // 
            this.btnUp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUp.Image = global::pwiz.Common.Properties.Resources.up_pro32;
            this.btnUp.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(21, 20);
            this.btnUp.Text = "Up";
            this.btnUp.Click += new System.EventHandler(this.btnUp_Click);
            // 
            // btnDown
            // 
            this.btnDown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDown.Image = global::pwiz.Common.Properties.Resources.down_pro32;
            this.btnDown.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(21, 20);
            this.btnDown.Text = "Down";
            this.btnDown.Click += new System.EventHandler(this.btnDown_Click);
            // 
            // cbxHidden
            // 
            this.cbxHidden.AutoSize = true;
            this.cbxHidden.Location = new System.Drawing.Point(11, 128);
            this.cbxHidden.Name = "cbxHidden";
            this.cbxHidden.Size = new System.Drawing.Size(92, 17);
            this.cbxHidden.TabIndex = 5;
            this.cbxHidden.Text = "Not Displayed";
            this.cbxHidden.UseVisualStyleBackColor = true;
            this.cbxHidden.Visible = false;
            // 
            // groupBoxAggregate
            // 
            this.groupBoxAggregate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxAggregate.Controls.Add(this.comboBoxAggregate);
            this.groupBoxAggregate.Location = new System.Drawing.Point(11, 70);
            this.groupBoxAggregate.Name = "groupBoxAggregate";
            this.groupBoxAggregate.Size = new System.Drawing.Size(250, 52);
            this.groupBoxAggregate.TabIndex = 3;
            this.groupBoxAggregate.TabStop = false;
            this.groupBoxAggregate.Text = "Aggregate";
            this.groupBoxAggregate.Visible = false;
            // 
            // comboBoxAggregate
            // 
            this.comboBoxAggregate.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxAggregate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxAggregate.FormattingEnabled = true;
            this.comboBoxAggregate.Location = new System.Drawing.Point(6, 19);
            this.comboBoxAggregate.Name = "comboBoxAggregate";
            this.comboBoxAggregate.Size = new System.Drawing.Size(232, 21);
            this.comboBoxAggregate.TabIndex = 0;
            // 
            // groupBoxCaption
            // 
            this.groupBoxCaption.Controls.Add(this.tbxCaption);
            this.groupBoxCaption.Location = new System.Drawing.Point(11, 18);
            this.groupBoxCaption.Name = "groupBoxCaption";
            this.groupBoxCaption.Size = new System.Drawing.Size(183, 46);
            this.groupBoxCaption.TabIndex = 2;
            this.groupBoxCaption.TabStop = false;
            this.groupBoxCaption.Text = "Caption";
            // 
            // tbxCaption
            // 
            this.tbxCaption.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxCaption.Location = new System.Drawing.Point(6, 19);
            this.tbxCaption.Name = "tbxCaption";
            this.tbxCaption.Size = new System.Drawing.Size(165, 20);
            this.tbxCaption.TabIndex = 1;
            this.tbxCaption.Leave += new System.EventHandler(this.tbxCaption_Leave);
            // 
            // tabPageFilter
            // 
            this.tabPageFilter.Location = new System.Drawing.Point(4, 22);
            this.tabPageFilter.Name = "tabPageFilter";
            this.tabPageFilter.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageFilter.Size = new System.Drawing.Size(887, 313);
            this.tabPageFilter.TabIndex = 1;
            this.tabPageFilter.Text = "Filter";
            this.tabPageFilter.UseVisualStyleBackColor = true;
            // 
            // tabPageSort
            // 
            this.tabPageSort.Location = new System.Drawing.Point(4, 22);
            this.tabPageSort.Name = "tabPageSort";
            this.tabPageSort.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageSort.Size = new System.Drawing.Size(887, 313);
            this.tabPageSort.TabIndex = 2;
            this.tabPageSort.Text = "Sort";
            this.tabPageSort.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(817, 382);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(736, 382);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 2;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(64, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "View &Name:";
            // 
            // tbxViewName
            // 
            this.tbxViewName.Location = new System.Drawing.Point(82, 6);
            this.tbxViewName.Name = "tbxViewName";
            this.tbxViewName.Size = new System.Drawing.Size(214, 20);
            this.tbxViewName.TabIndex = 4;
            // 
            // groupBoxSublist
            // 
            this.groupBoxSublist.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxSublist.Controls.Add(this.comboSublist);
            this.groupBoxSublist.Location = new System.Drawing.Point(2, 261);
            this.groupBoxSublist.Name = "groupBoxSublist";
            this.groupBoxSublist.Size = new System.Drawing.Size(267, 46);
            this.groupBoxSublist.TabIndex = 5;
            this.groupBoxSublist.TabStop = false;
            this.groupBoxSublist.Text = "Sublist";
            // 
            // comboSublist
            // 
            this.comboSublist.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.comboSublist.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSublist.FormattingEnabled = true;
            this.comboSublist.Location = new System.Drawing.Point(6, 19);
            this.comboSublist.Name = "comboSublist";
            this.comboSublist.Size = new System.Drawing.Size(255, 21);
            this.comboSublist.TabIndex = 0;
            this.comboSublist.SelectedIndexChanged += new System.EventHandler(this.comboSublist_SelectedIndexChanged);
            // 
            // groupBoxProperties
            // 
            this.groupBoxProperties.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxProperties.Controls.Add(this.groupBoxCaption);
            this.groupBoxProperties.Controls.Add(this.cbxHidden);
            this.groupBoxProperties.Controls.Add(this.groupBoxAggregate);
            this.groupBoxProperties.Location = new System.Drawing.Point(2, 0);
            this.groupBoxProperties.Name = "groupBoxProperties";
            this.groupBoxProperties.Size = new System.Drawing.Size(267, 249);
            this.groupBoxProperties.TabIndex = 6;
            this.groupBoxProperties.TabStop = false;
            this.groupBoxProperties.Text = "Column Properties";
            // 
            // btnAdvanced
            // 
            this.btnAdvanced.AutoSize = true;
            this.btnAdvanced.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnAdvanced.Location = new System.Drawing.Point(510, 6);
            this.btnAdvanced.Name = "btnAdvanced";
            this.btnAdvanced.Size = new System.Drawing.Size(106, 23);
            this.btnAdvanced.TabIndex = 5;
            this.btnAdvanced.Text = "<< Hide &Advanced";
            this.btnAdvanced.UseVisualStyleBackColor = true;
            this.btnAdvanced.Click += new System.EventHandler(this.btnAdvanced_Click);
            // 
            // splitContainerAdvanced
            // 
            this.splitContainerAdvanced.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerAdvanced.Location = new System.Drawing.Point(3, 3);
            this.splitContainerAdvanced.Name = "splitContainerAdvanced";
            // 
            // splitContainerAdvanced.Panel1
            // 
            this.splitContainerAdvanced.Panel1.Controls.Add(this.splitContainer1);
            // 
            // splitContainerAdvanced.Panel2
            // 
            this.splitContainerAdvanced.Panel2.Controls.Add(this.groupBoxSublist);
            this.splitContainerAdvanced.Panel2.Controls.Add(this.groupBoxProperties);
            this.splitContainerAdvanced.Size = new System.Drawing.Size(881, 307);
            this.splitContainerAdvanced.SplitterDistance = 608;
            this.splitContainerAdvanced.TabIndex = 0;
            // 
            // availableFieldsTreeColumns
            // 
            this.availableFieldsTreeColumns.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.availableFieldsTreeColumns.CheckBoxes = true;
            this.availableFieldsTreeColumns.CheckedColumns = new pwiz.Common.DataBinding.IdentifierPath[0];
            this.availableFieldsTreeColumns.HideSelection = false;
            this.availableFieldsTreeColumns.Location = new System.Drawing.Point(3, 3);
            this.availableFieldsTreeColumns.Name = "availableFieldsTreeColumns";
            this.availableFieldsTreeColumns.RootColumn = null;
            this.availableFieldsTreeColumns.Size = new System.Drawing.Size(198, 301);
            this.availableFieldsTreeColumns.TabIndex = 0;
            this.availableFieldsTreeColumns.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.availableFieldsTreeColumns_AfterCheck);
            // 
            // CustomizeViewForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(895, 417);
            this.Controls.Add(this.btnAdvanced);
            this.Controls.Add(this.tbxViewName);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tabControl1);
            this.Name = "CustomizeViewForm";
            this.Text = "Customize View";
            this.tabControl1.ResumeLayout(false);
            this.tabPageColumns.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            this.splitContainer1.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.groupBoxAggregate.ResumeLayout(false);
            this.groupBoxCaption.ResumeLayout(false);
            this.groupBoxCaption.PerformLayout();
            this.groupBoxSublist.ResumeLayout(false);
            this.groupBoxProperties.ResumeLayout(false);
            this.groupBoxProperties.PerformLayout();
            this.splitContainerAdvanced.Panel1.ResumeLayout(false);
            this.splitContainerAdvanced.Panel2.ResumeLayout(false);
            this.splitContainerAdvanced.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageColumns;
        private System.Windows.Forms.TabPage tabPageFilter;
        private AvailableFieldsTree availableFieldsTreeColumns;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TabPage tabPageSort;
        private System.Windows.Forms.TextBox tbxCaption;
        private System.Windows.Forms.Button btnAddColumn;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxViewName;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnRemove;
        private System.Windows.Forms.ToolStripButton btnUp;
        private System.Windows.Forms.ToolStripButton btnDown;
        private System.Windows.Forms.ListView listViewColumns;
        private System.Windows.Forms.GroupBox groupBoxAggregate;
        private System.Windows.Forms.GroupBox groupBoxCaption;
        private System.Windows.Forms.CheckBox cbxHidden;
        private System.Windows.Forms.ComboBox comboBoxAggregate;
        private System.Windows.Forms.GroupBox groupBoxSublist;
        private System.Windows.Forms.ComboBox comboSublist;
        private System.Windows.Forms.GroupBox groupBoxProperties;
        private System.Windows.Forms.Button btnAdvanced;
        private System.Windows.Forms.SplitContainer splitContainerAdvanced;
    }
}