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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxViewName = new System.Windows.Forms.TextBox();
            this.btnAdvanced = new System.Windows.Forms.Button();
            this.tabPageFilter = new System.Windows.Forms.TabPage();
            this.splitContainerFilter = new System.Windows.Forms.SplitContainer();
            this.availableFieldsTreeFilter = new pwiz.Common.DataBinding.Controls.AvailableFieldsTree();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnAddFilter = new System.Windows.Forms.Button();
            this.dataGridViewFilter = new System.Windows.Forms.DataGridView();
            this.colFilterColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFilterOperation = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colFilterOperand = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toolStripFilter = new System.Windows.Forms.ToolStrip();
            this.btnDeleteFilter = new System.Windows.Forms.ToolStripButton();
            this.tabPageColumns = new System.Windows.Forms.TabPage();
            this.splitContainerAdvanced = new System.Windows.Forms.SplitContainer();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.availableFieldsTreeColumns = new pwiz.Common.DataBinding.Controls.AvailableFieldsTree();
            this.listViewColumns = new System.Windows.Forms.ListView();
            this.toolStripColumns = new System.Windows.Forms.ToolStrip();
            this.btnRemove = new System.Windows.Forms.ToolStripButton();
            this.btnUp = new System.Windows.Forms.ToolStripButton();
            this.btnDown = new System.Windows.Forms.ToolStripButton();
            this.groupBoxSublist = new System.Windows.Forms.GroupBox();
            this.comboSublist = new System.Windows.Forms.ComboBox();
            this.groupBoxProperties = new System.Windows.Forms.GroupBox();
            this.groupBoxSortOrder = new System.Windows.Forms.GroupBox();
            this.comboSortOrder = new System.Windows.Forms.ComboBox();
            this.groupBoxCaption = new System.Windows.Forms.GroupBox();
            this.tbxCaption = new System.Windows.Forms.TextBox();
            this.cbxHidden = new System.Windows.Forms.CheckBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageSort = new System.Windows.Forms.TabPage();
            this.splitContainerSort = new System.Windows.Forms.SplitContainer();
            this.clbAvailableSortColumns = new System.Windows.Forms.CheckedListBox();
            this.dataGridViewSort = new System.Windows.Forms.DataGridView();
            this.colSortColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSortDirection = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.toolStripSort = new System.Windows.Forms.ToolStrip();
            this.btnSortRemove = new System.Windows.Forms.ToolStripButton();
            this.btnSortMoveUp = new System.Windows.Forms.ToolStripButton();
            this.btnSortMoveDown = new System.Windows.Forms.ToolStripButton();
            colHdrName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabPageFilter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerFilter)).BeginInit();
            this.splitContainerFilter.Panel1.SuspendLayout();
            this.splitContainerFilter.Panel2.SuspendLayout();
            this.splitContainerFilter.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFilter)).BeginInit();
            this.toolStripFilter.SuspendLayout();
            this.tabPageColumns.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerAdvanced)).BeginInit();
            this.splitContainerAdvanced.Panel1.SuspendLayout();
            this.splitContainerAdvanced.Panel2.SuspendLayout();
            this.splitContainerAdvanced.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.toolStripColumns.SuspendLayout();
            this.groupBoxSublist.SuspendLayout();
            this.groupBoxProperties.SuspendLayout();
            this.groupBoxSortOrder.SuspendLayout();
            this.groupBoxCaption.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPageSort.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerSort)).BeginInit();
            this.splitContainerSort.Panel1.SuspendLayout();
            this.splitContainerSort.Panel2.SuspendLayout();
            this.splitContainerSort.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSort)).BeginInit();
            this.toolStripSort.SuspendLayout();
            this.SuspendLayout();
            // 
            // colHdrName
            // 
            colHdrName.Text = "Name";
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
            // tabPageFilter
            // 
            this.tabPageFilter.Controls.Add(this.splitContainerFilter);
            this.tabPageFilter.Location = new System.Drawing.Point(4, 22);
            this.tabPageFilter.Name = "tabPageFilter";
            this.tabPageFilter.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageFilter.Size = new System.Drawing.Size(887, 313);
            this.tabPageFilter.TabIndex = 1;
            this.tabPageFilter.Text = "Filter";
            this.tabPageFilter.UseVisualStyleBackColor = true;
            // 
            // splitContainerFilter
            // 
            this.splitContainerFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerFilter.Location = new System.Drawing.Point(3, 3);
            this.splitContainerFilter.Name = "splitContainerFilter";
            // 
            // splitContainerFilter.Panel1
            // 
            this.splitContainerFilter.Panel1.Controls.Add(this.availableFieldsTreeFilter);
            this.splitContainerFilter.Panel1.Controls.Add(this.panel1);
            // 
            // splitContainerFilter.Panel2
            // 
            this.splitContainerFilter.Panel2.Controls.Add(this.dataGridViewFilter);
            this.splitContainerFilter.Panel2.Controls.Add(this.toolStripFilter);
            this.splitContainerFilter.Size = new System.Drawing.Size(881, 307);
            this.splitContainerFilter.SplitterDistance = 370;
            this.splitContainerFilter.TabIndex = 1;
            // 
            // availableFieldsTreeFilter
            // 
            this.availableFieldsTreeFilter.CheckedColumns = new pwiz.Common.DataBinding.IdentifierPath[0];
            this.availableFieldsTreeFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.availableFieldsTreeFilter.HideSelection = false;
            this.availableFieldsTreeFilter.Location = new System.Drawing.Point(0, 0);
            this.availableFieldsTreeFilter.Name = "availableFieldsTreeFilter";
            this.availableFieldsTreeFilter.RootColumn = null;
            this.availableFieldsTreeFilter.ShowAdvancedFields = false;
            this.availableFieldsTreeFilter.Size = new System.Drawing.Size(314, 307);
            this.availableFieldsTreeFilter.TabIndex = 0;
            this.availableFieldsTreeFilter.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.availableFieldsTreeFilter_AfterSelect);
            this.availableFieldsTreeFilter.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.availableFieldsTreeFilter_NodeMouseDoubleClick);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnAddFilter);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel1.Location = new System.Drawing.Point(314, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(56, 307);
            this.panel1.TabIndex = 1;
            // 
            // btnAddFilter
            // 
            this.btnAddFilter.AutoSize = true;
            this.btnAddFilter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnAddFilter.Location = new System.Drawing.Point(3, 134);
            this.btnAddFilter.Name = "btnAddFilter";
            this.btnAddFilter.Size = new System.Drawing.Size(51, 23);
            this.btnAddFilter.TabIndex = 0;
            this.btnAddFilter.Text = "Add >>";
            this.btnAddFilter.UseVisualStyleBackColor = true;
            this.btnAddFilter.Click += new System.EventHandler(this.btnAddFilter_Click);
            // 
            // dataGridViewFilter
            // 
            this.dataGridViewFilter.AllowUserToAddRows = false;
            this.dataGridViewFilter.AllowUserToDeleteRows = false;
            this.dataGridViewFilter.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewFilter.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridViewFilter.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewFilter.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colFilterColumn,
            this.colFilterOperation,
            this.colFilterOperand});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewFilter.DefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridViewFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewFilter.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewFilter.Name = "dataGridViewFilter";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewFilter.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridViewFilter.RowHeadersVisible = false;
            this.dataGridViewFilter.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewFilter.Size = new System.Drawing.Size(507, 307);
            this.dataGridViewFilter.TabIndex = 2;
            this.dataGridViewFilter.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewFilter_CellDoubleClick);
            this.dataGridViewFilter.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewFilter_CellEndEdit);
            this.dataGridViewFilter.CellEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewFilter_CellEnter);
            this.dataGridViewFilter.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridViewFilter_CurrentCellDirtyStateChanged);
            this.dataGridViewFilter.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.dataGridViewFilter_EditingControlShowing);
            // 
            // colFilterColumn
            // 
            this.colFilterColumn.HeaderText = "Column";
            this.colFilterColumn.Name = "colFilterColumn";
            this.colFilterColumn.ReadOnly = true;
            // 
            // colFilterOperation
            // 
            this.colFilterOperation.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colFilterOperation.HeaderText = "Operation";
            this.colFilterOperation.Name = "colFilterOperation";
            // 
            // colFilterOperand
            // 
            this.colFilterOperand.HeaderText = "Value";
            this.colFilterOperand.Name = "colFilterOperand";
            this.colFilterOperand.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colFilterOperand.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // toolStripFilter
            // 
            this.toolStripFilter.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStripFilter.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnDeleteFilter});
            this.toolStripFilter.Location = new System.Drawing.Point(475, 0);
            this.toolStripFilter.Name = "toolStripFilter";
            this.toolStripFilter.Size = new System.Drawing.Size(32, 307);
            this.toolStripFilter.TabIndex = 1;
            this.toolStripFilter.Text = "toolStrip1";
            // 
            // btnDeleteFilter
            // 
            this.btnDeleteFilter.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDeleteFilter.Image = global::pwiz.Common.Properties.Resources.Delete;
            this.btnDeleteFilter.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDeleteFilter.Name = "btnDeleteFilter";
            this.btnDeleteFilter.Size = new System.Drawing.Size(21, 20);
            this.btnDeleteFilter.Text = "Delete";
            this.btnDeleteFilter.Click += new System.EventHandler(this.btnDeleteFilter_Click);
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
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.availableFieldsTreeColumns);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.listViewColumns);
            this.splitContainer1.Panel2.Controls.Add(this.toolStripColumns);
            this.splitContainer1.Size = new System.Drawing.Size(608, 307);
            this.splitContainer1.SplitterDistance = 308;
            this.splitContainer1.TabIndex = 1;
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
            this.availableFieldsTreeColumns.ShowAdvancedFields = false;
            this.availableFieldsTreeColumns.Size = new System.Drawing.Size(303, 301);
            this.availableFieldsTreeColumns.TabIndex = 0;
            this.availableFieldsTreeColumns.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.availableFieldsTreeColumns_AfterCheck);
            this.availableFieldsTreeColumns.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.availableFieldsTreeColumns_NodeMouseDoubleClick);
            // 
            // listViewColumns
            // 
            this.listViewColumns.Activation = System.Windows.Forms.ItemActivation.TwoClick;
            this.listViewColumns.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            colHdrName});
            this.listViewColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewColumns.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listViewColumns.HideSelection = false;
            this.listViewColumns.Location = new System.Drawing.Point(0, 0);
            this.listViewColumns.Name = "listViewColumns";
            this.listViewColumns.Size = new System.Drawing.Size(264, 307);
            this.listViewColumns.TabIndex = 2;
            this.listViewColumns.UseCompatibleStateImageBehavior = false;
            this.listViewColumns.View = System.Windows.Forms.View.Details;
            this.listViewColumns.ItemActivate += new System.EventHandler(this.listViewColumns_ItemActivate);
            this.listViewColumns.SelectedIndexChanged += new System.EventHandler(this.listViewColumns_SelectedIndexChanged);
            this.listViewColumns.SizeChanged += new System.EventHandler(this.listViewColumns_SizeChanged);
            // 
            // toolStripColumns
            // 
            this.toolStripColumns.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStripColumns.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnRemove,
            this.btnUp,
            this.btnDown});
            this.toolStripColumns.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStripColumns.Location = new System.Drawing.Point(264, 0);
            this.toolStripColumns.Name = "toolStripColumns";
            this.toolStripColumns.Size = new System.Drawing.Size(32, 307);
            this.toolStripColumns.TabIndex = 1;
            this.toolStripColumns.Text = "toolStrip1";
            // 
            // btnRemove
            // 
            this.btnRemove.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRemove.Image = global::pwiz.Common.Properties.Resources.Delete;
            this.btnRemove.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(29, 20);
            this.btnRemove.Text = "Remove";
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnUp
            // 
            this.btnUp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUp.Image = global::pwiz.Common.Properties.Resources.up_pro32;
            this.btnUp.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(29, 20);
            this.btnUp.Text = "Up";
            this.btnUp.Click += new System.EventHandler(this.btnUp_Click);
            // 
            // btnDown
            // 
            this.btnDown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDown.Image = global::pwiz.Common.Properties.Resources.down_pro32;
            this.btnDown.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(29, 20);
            this.btnDown.Text = "Down";
            this.btnDown.Click += new System.EventHandler(this.btnDown_Click);
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
            this.groupBoxProperties.Controls.Add(this.groupBoxSortOrder);
            this.groupBoxProperties.Controls.Add(this.groupBoxCaption);
            this.groupBoxProperties.Controls.Add(this.cbxHidden);
            this.groupBoxProperties.Location = new System.Drawing.Point(2, 0);
            this.groupBoxProperties.Name = "groupBoxProperties";
            this.groupBoxProperties.Size = new System.Drawing.Size(267, 249);
            this.groupBoxProperties.TabIndex = 6;
            this.groupBoxProperties.TabStop = false;
            this.groupBoxProperties.Text = "Column Properties";
            // 
            // groupBoxSortOrder
            // 
            this.groupBoxSortOrder.Controls.Add(this.comboSortOrder);
            this.groupBoxSortOrder.Location = new System.Drawing.Point(11, 125);
            this.groupBoxSortOrder.Name = "groupBoxSortOrder";
            this.groupBoxSortOrder.Size = new System.Drawing.Size(250, 51);
            this.groupBoxSortOrder.TabIndex = 6;
            this.groupBoxSortOrder.TabStop = false;
            this.groupBoxSortOrder.Text = "Sort Order";
            // 
            // comboSortOrder
            // 
            this.comboSortOrder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboSortOrder.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSortOrder.FormattingEnabled = true;
            this.comboSortOrder.Items.AddRange(new object[] {
            "Not Sorted",
            "Ascending",
            "Descending"});
            this.comboSortOrder.Location = new System.Drawing.Point(6, 16);
            this.comboSortOrder.Name = "comboSortOrder";
            this.comboSortOrder.Size = new System.Drawing.Size(232, 21);
            this.comboSortOrder.TabIndex = 0;
            this.comboSortOrder.SelectedIndexChanged += new System.EventHandler(this.comboSortOrder_SelectedIndexChanged);
            // 
            // groupBoxCaption
            // 
            this.groupBoxCaption.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxCaption.Controls.Add(this.tbxCaption);
            this.groupBoxCaption.Location = new System.Drawing.Point(11, 18);
            this.groupBoxCaption.Name = "groupBoxCaption";
            this.groupBoxCaption.Size = new System.Drawing.Size(250, 46);
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
            this.tbxCaption.Size = new System.Drawing.Size(232, 20);
            this.tbxCaption.TabIndex = 1;
            this.tbxCaption.Leave += new System.EventHandler(this.tbxCaption_Leave);
            // 
            // cbxHidden
            // 
            this.cbxHidden.AutoSize = true;
            this.cbxHidden.Location = new System.Drawing.Point(6, 194);
            this.cbxHidden.Name = "cbxHidden";
            this.cbxHidden.Size = new System.Drawing.Size(152, 17);
            this.cbxHidden.TabIndex = 5;
            this.cbxHidden.Text = "Do not display in Grid View";
            this.cbxHidden.UseVisualStyleBackColor = true;
            this.cbxHidden.CheckedChanged += new System.EventHandler(this.cbxHidden_CheckedChanged);
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
            // tabPageSort
            // 
            this.tabPageSort.Controls.Add(this.splitContainerSort);
            this.tabPageSort.Location = new System.Drawing.Point(4, 22);
            this.tabPageSort.Name = "tabPageSort";
            this.tabPageSort.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageSort.Size = new System.Drawing.Size(887, 313);
            this.tabPageSort.TabIndex = 2;
            this.tabPageSort.Text = "Sort";
            this.tabPageSort.UseVisualStyleBackColor = true;
            // 
            // splitContainerSort
            // 
            this.splitContainerSort.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerSort.Location = new System.Drawing.Point(3, 3);
            this.splitContainerSort.Name = "splitContainerSort";
            // 
            // splitContainerSort.Panel1
            // 
            this.splitContainerSort.Panel1.Controls.Add(this.clbAvailableSortColumns);
            // 
            // splitContainerSort.Panel2
            // 
            this.splitContainerSort.Panel2.Controls.Add(this.dataGridViewSort);
            this.splitContainerSort.Panel2.Controls.Add(this.toolStripSort);
            this.splitContainerSort.Size = new System.Drawing.Size(881, 307);
            this.splitContainerSort.SplitterDistance = 458;
            this.splitContainerSort.TabIndex = 2;
            // 
            // clbAvailableSortColumns
            // 
            this.clbAvailableSortColumns.CheckOnClick = true;
            this.clbAvailableSortColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clbAvailableSortColumns.FormattingEnabled = true;
            this.clbAvailableSortColumns.Location = new System.Drawing.Point(0, 0);
            this.clbAvailableSortColumns.Name = "clbAvailableSortColumns";
            this.clbAvailableSortColumns.Size = new System.Drawing.Size(458, 307);
            this.clbAvailableSortColumns.TabIndex = 0;
            this.clbAvailableSortColumns.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.clbAvailableSortColumns_ItemCheck);
            // 
            // dataGridViewSort
            // 
            this.dataGridViewSort.AllowUserToAddRows = false;
            this.dataGridViewSort.AllowUserToDeleteRows = false;
            this.dataGridViewSort.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewSort.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewSort.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSortColumn,
            this.colSortDirection});
            this.dataGridViewSort.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewSort.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewSort.Name = "dataGridViewSort";
            this.dataGridViewSort.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewSort.Size = new System.Drawing.Size(395, 307);
            this.dataGridViewSort.TabIndex = 3;
            this.dataGridViewSort.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewSort_CellEndEdit);
            this.dataGridViewSort.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridViewSort_CurrentCellDirtyStateChanged);
            this.dataGridViewSort.SelectionChanged += new System.EventHandler(this.dataGridViewSort_SelectionChanged);
            // 
            // colSortColumn
            // 
            this.colSortColumn.HeaderText = "Column";
            this.colSortColumn.Name = "colSortColumn";
            this.colSortColumn.ReadOnly = true;
            // 
            // colSortDirection
            // 
            this.colSortDirection.HeaderText = "Direction";
            this.colSortDirection.Name = "colSortDirection";
            // 
            // toolStripSort
            // 
            this.toolStripSort.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStripSort.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnSortRemove,
            this.btnSortMoveUp,
            this.btnSortMoveDown});
            this.toolStripSort.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStripSort.Location = new System.Drawing.Point(395, 0);
            this.toolStripSort.Name = "toolStripSort";
            this.toolStripSort.Size = new System.Drawing.Size(24, 307);
            this.toolStripSort.TabIndex = 2;
            this.toolStripSort.Text = "toolStrip1";
            // 
            // btnSortRemove
            // 
            this.btnSortRemove.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnSortRemove.Image = global::pwiz.Common.Properties.Resources.Delete;
            this.btnSortRemove.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSortRemove.Name = "btnSortRemove";
            this.btnSortRemove.Size = new System.Drawing.Size(21, 20);
            this.btnSortRemove.Text = "Remove";
            this.btnSortRemove.Click += new System.EventHandler(this.btnSortRemove_Click);
            // 
            // btnSortMoveUp
            // 
            this.btnSortMoveUp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnSortMoveUp.Image = global::pwiz.Common.Properties.Resources.up_pro32;
            this.btnSortMoveUp.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSortMoveUp.Name = "btnSortMoveUp";
            this.btnSortMoveUp.Size = new System.Drawing.Size(21, 20);
            this.btnSortMoveUp.Text = "toolStripButton1";
            this.btnSortMoveUp.Click += new System.EventHandler(this.btnSortMoveUp_Click);
            // 
            // btnSortMoveDown
            // 
            this.btnSortMoveDown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnSortMoveDown.Image = global::pwiz.Common.Properties.Resources.down_pro32;
            this.btnSortMoveDown.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSortMoveDown.Name = "btnSortMoveDown";
            this.btnSortMoveDown.Size = new System.Drawing.Size(21, 20);
            this.btnSortMoveDown.Text = "Move Down";
            this.btnSortMoveDown.Click += new System.EventHandler(this.btnSortMoveDown_Click);
            // 
            // CustomizeViewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(895, 417);
            this.Controls.Add(this.btnAdvanced);
            this.Controls.Add(this.tbxViewName);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tabControl1);
            this.Name = "CustomizeViewForm";
            this.Text = "Customize View";
            this.tabPageFilter.ResumeLayout(false);
            this.splitContainerFilter.Panel1.ResumeLayout(false);
            this.splitContainerFilter.Panel2.ResumeLayout(false);
            this.splitContainerFilter.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerFilter)).EndInit();
            this.splitContainerFilter.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFilter)).EndInit();
            this.toolStripFilter.ResumeLayout(false);
            this.toolStripFilter.PerformLayout();
            this.tabPageColumns.ResumeLayout(false);
            this.splitContainerAdvanced.Panel1.ResumeLayout(false);
            this.splitContainerAdvanced.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerAdvanced)).EndInit();
            this.splitContainerAdvanced.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.toolStripColumns.ResumeLayout(false);
            this.toolStripColumns.PerformLayout();
            this.groupBoxSublist.ResumeLayout(false);
            this.groupBoxProperties.ResumeLayout(false);
            this.groupBoxProperties.PerformLayout();
            this.groupBoxSortOrder.ResumeLayout(false);
            this.groupBoxCaption.ResumeLayout(false);
            this.groupBoxCaption.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.tabPageSort.ResumeLayout(false);
            this.splitContainerSort.Panel1.ResumeLayout(false);
            this.splitContainerSort.Panel2.ResumeLayout(false);
            this.splitContainerSort.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerSort)).EndInit();
            this.splitContainerSort.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSort)).EndInit();
            this.toolStripSort.ResumeLayout(false);
            this.toolStripSort.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxViewName;
        private System.Windows.Forms.Button btnAdvanced;
        private System.Windows.Forms.TabPage tabPageFilter;
        private System.Windows.Forms.SplitContainer splitContainerFilter;
        private AvailableFieldsTree availableFieldsTreeFilter;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnAddFilter;
        private System.Windows.Forms.DataGridView dataGridViewFilter;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFilterColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn colFilterOperation;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFilterOperand;
        private System.Windows.Forms.ToolStrip toolStripFilter;
        private System.Windows.Forms.ToolStripButton btnDeleteFilter;
        private System.Windows.Forms.TabPage tabPageColumns;
        private System.Windows.Forms.SplitContainer splitContainerAdvanced;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private AvailableFieldsTree availableFieldsTreeColumns;
        private System.Windows.Forms.ListView listViewColumns;
        private System.Windows.Forms.ToolStrip toolStripColumns;
        private System.Windows.Forms.ToolStripButton btnRemove;
        private System.Windows.Forms.ToolStripButton btnUp;
        private System.Windows.Forms.ToolStripButton btnDown;
        private System.Windows.Forms.GroupBox groupBoxSublist;
        private System.Windows.Forms.ComboBox comboSublist;
        private System.Windows.Forms.GroupBox groupBoxProperties;
        private System.Windows.Forms.GroupBox groupBoxSortOrder;
        private System.Windows.Forms.ComboBox comboSortOrder;
        private System.Windows.Forms.GroupBox groupBoxCaption;
        private System.Windows.Forms.TextBox tbxCaption;
        private System.Windows.Forms.CheckBox cbxHidden;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageSort;
        private System.Windows.Forms.SplitContainer splitContainerSort;
        private System.Windows.Forms.CheckedListBox clbAvailableSortColumns;
        private System.Windows.Forms.ToolStrip toolStripSort;
        private System.Windows.Forms.ToolStripButton btnSortRemove;
        private System.Windows.Forms.ToolStripButton btnSortMoveUp;
        private System.Windows.Forms.ToolStripButton btnSortMoveDown;
        private System.Windows.Forms.DataGridView dataGridViewSort;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSortColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn colSortDirection;
    }
}