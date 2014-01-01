namespace pwiz.Common.DataBinding.Controls.Editor
{
    partial class FilterTab
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FilterTab));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainerFilter = new System.Windows.Forms.SplitContainer();
            this.availableFieldsTreeFilter = new pwiz.Common.DataBinding.Controls.Editor.AvailableFieldsTree();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnAddFilter = new System.Windows.Forms.Button();
            this.dataGridViewFilter = new System.Windows.Forms.DataGridView();
            this.colFilterColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFilterOperation = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colFilterOperand = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toolStripFilter = new System.Windows.Forms.ToolStrip();
            this.btnDeleteFilter = new System.Windows.Forms.ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerFilter)).BeginInit();
            this.splitContainerFilter.Panel1.SuspendLayout();
            this.splitContainerFilter.Panel2.SuspendLayout();
            this.splitContainerFilter.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFilter)).BeginInit();
            this.toolStripFilter.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainerFilter
            // 
            resources.ApplyResources(this.splitContainerFilter, "splitContainerFilter");
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
            // 
            // availableFieldsTreeFilter
            // 
            this.availableFieldsTreeFilter.CheckedColumns = new pwiz.Common.DataBinding.PropertyPath[0];
            resources.ApplyResources(this.availableFieldsTreeFilter, "availableFieldsTreeFilter");
            this.availableFieldsTreeFilter.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
            this.availableFieldsTreeFilter.HideSelection = false;
            this.availableFieldsTreeFilter.Name = "availableFieldsTreeFilter";
            this.availableFieldsTreeFilter.RootColumn = null;
            this.availableFieldsTreeFilter.ShowAdvancedFields = false;
            this.availableFieldsTreeFilter.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.AvailableFieldsTreeFilterOnAfterSelect);
            this.availableFieldsTreeFilter.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.AvailableFieldsTreeFilterOnNodeMouseDoubleClick);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnAddFilter);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // btnAddFilter
            // 
            resources.ApplyResources(this.btnAddFilter, "btnAddFilter");
            this.btnAddFilter.Name = "btnAddFilter";
            this.btnAddFilter.UseVisualStyleBackColor = true;
            this.btnAddFilter.Click += new System.EventHandler(this.BtnAddFilterOnClick);
            // 
            // dataGridViewFilter
            // 
            this.dataGridViewFilter.AllowUserToAddRows = false;
            this.dataGridViewFilter.AllowUserToDeleteRows = false;
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
            resources.ApplyResources(this.dataGridViewFilter, "dataGridViewFilter");
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
            this.dataGridViewFilter.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewFilterOnCellDoubleClick);
            this.dataGridViewFilter.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewFilterOnCellEndEdit);
            this.dataGridViewFilter.CellEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewFilterOnCellEnter);
            this.dataGridViewFilter.CurrentCellDirtyStateChanged += new System.EventHandler(this.DataGridViewFilterOnCurrentCellDirtyStateChanged);
            this.dataGridViewFilter.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewFilter_DataError);
            this.dataGridViewFilter.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.DataGridViewFilterOnEditingControlShowing);
            // 
            // colFilterColumn
            // 
            resources.ApplyResources(this.colFilterColumn, "colFilterColumn");
            this.colFilterColumn.Name = "colFilterColumn";
            this.colFilterColumn.ReadOnly = true;
            // 
            // colFilterOperation
            // 
            this.colFilterOperation.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            resources.ApplyResources(this.colFilterOperation, "colFilterOperation");
            this.colFilterOperation.Name = "colFilterOperation";
            // 
            // colFilterOperand
            // 
            resources.ApplyResources(this.colFilterOperand, "colFilterOperand");
            this.colFilterOperand.Name = "colFilterOperand";
            this.colFilterOperand.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colFilterOperand.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // toolStripFilter
            // 
            resources.ApplyResources(this.toolStripFilter, "toolStripFilter");
            this.toolStripFilter.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnDeleteFilter});
            this.toolStripFilter.Name = "toolStripFilter";
            // 
            // btnDeleteFilter
            // 
            this.btnDeleteFilter.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDeleteFilter.Image = global::pwiz.Common.Properties.Resources.Delete;
            resources.ApplyResources(this.btnDeleteFilter, "btnDeleteFilter");
            this.btnDeleteFilter.Name = "btnDeleteFilter";
            this.btnDeleteFilter.Click += new System.EventHandler(this.BtnDeleteFilterOnClick);
            // 
            // FilterTab
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainerFilter);
            this.Name = "FilterTab";
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
            this.ResumeLayout(false);

        }

        #endregion

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
    }
}
