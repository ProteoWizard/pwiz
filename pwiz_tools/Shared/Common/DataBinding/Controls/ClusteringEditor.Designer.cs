namespace pwiz.Common.DataBinding.Controls
{
    partial class ClusteringEditor
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.rowFieldPanel = new System.Windows.Forms.Panel();
            this.comboRowColumn = new System.Windows.Forms.ComboBox();
            this.availableFieldsTreeRows = new pwiz.Common.DataBinding.Controls.Editor.AvailableFieldsTree();
            this.columnFieldPanel = new System.Windows.Forms.Panel();
            this.comboColumnColumn = new System.Windows.Forms.ComboBox();
            this.availableFieldsTreeColumns = new pwiz.Common.DataBinding.Controls.Editor.AvailableFieldsTree();
            this.tableLayoutPanel1.SuspendLayout();
            this.rowFieldPanel.SuspendLayout();
            this.columnFieldPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.rowFieldPanel, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.columnFieldPanel, 0, 1);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(12, 12);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(776, 462);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // rowFieldPanel
            // 
            this.rowFieldPanel.Controls.Add(this.availableFieldsTreeRows);
            this.rowFieldPanel.Controls.Add(this.comboRowColumn);
            this.rowFieldPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rowFieldPanel.Location = new System.Drawing.Point(3, 3);
            this.rowFieldPanel.Name = "rowFieldPanel";
            this.rowFieldPanel.Size = new System.Drawing.Size(332, 225);
            this.rowFieldPanel.TabIndex = 0;
            // 
            // comboRowColumn
            // 
            this.comboRowColumn.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboRowColumn.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRowColumn.FormattingEnabled = true;
            this.comboRowColumn.Location = new System.Drawing.Point(3, 3);
            this.comboRowColumn.Name = "comboRowColumn";
            this.comboRowColumn.Size = new System.Drawing.Size(329, 21);
            this.comboRowColumn.TabIndex = 0;
            this.comboRowColumn.SelectedIndexChanged += new System.EventHandler(this.comboRowColumn_SelectedIndexChanged);
            // 
            // availableFieldsTreeRows
            // 
            this.availableFieldsTreeRows.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.availableFieldsTreeRows.CheckedColumns = new pwiz.Common.DataBinding.PropertyPath[0];
            this.availableFieldsTreeRows.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
            this.availableFieldsTreeRows.ImageIndex = 0;
            this.availableFieldsTreeRows.Location = new System.Drawing.Point(3, 30);
            this.availableFieldsTreeRows.Name = "availableFieldsTreeRows";
            this.availableFieldsTreeRows.SelectedImageIndex = 0;
            this.availableFieldsTreeRows.ShowNodeToolTips = true;
            this.availableFieldsTreeRows.Size = new System.Drawing.Size(326, 192);
            this.availableFieldsTreeRows.TabIndex = 1;
            // 
            // columnFieldPanel
            // 
            this.columnFieldPanel.Controls.Add(this.availableFieldsTreeColumns);
            this.columnFieldPanel.Controls.Add(this.comboColumnColumn);
            this.columnFieldPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.columnFieldPanel.Location = new System.Drawing.Point(3, 234);
            this.columnFieldPanel.Name = "columnFieldPanel";
            this.columnFieldPanel.Size = new System.Drawing.Size(332, 225);
            this.columnFieldPanel.TabIndex = 1;
            // 
            // comboColumnColumn
            // 
            this.comboColumnColumn.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboColumnColumn.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboColumnColumn.FormattingEnabled = true;
            this.comboColumnColumn.Location = new System.Drawing.Point(3, 3);
            this.comboColumnColumn.Name = "comboColumnColumn";
            this.comboColumnColumn.Size = new System.Drawing.Size(326, 21);
            this.comboColumnColumn.TabIndex = 0;
            this.comboColumnColumn.SelectedIndexChanged += new System.EventHandler(this.comboColumnColumn_SelectedIndexChanged);
            // 
            // availableFieldsTreeColumns
            // 
            this.availableFieldsTreeColumns.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.availableFieldsTreeColumns.CheckedColumns = new pwiz.Common.DataBinding.PropertyPath[0];
            this.availableFieldsTreeColumns.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
            this.availableFieldsTreeColumns.ImageIndex = 0;
            this.availableFieldsTreeColumns.Location = new System.Drawing.Point(3, 30);
            this.availableFieldsTreeColumns.Name = "availableFieldsTreeColumns";
            this.availableFieldsTreeColumns.SelectedImageIndex = 0;
            this.availableFieldsTreeColumns.ShowNodeToolTips = true;
            this.availableFieldsTreeColumns.Size = new System.Drawing.Size(326, 192);
            this.availableFieldsTreeColumns.TabIndex = 2;
            // 
            // ClusteringEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 535);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "ClusteringEditor";
            this.Text = "ClusteringEditor";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.rowFieldPanel.ResumeLayout(false);
            this.columnFieldPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel rowFieldPanel;
        private Editor.AvailableFieldsTree availableFieldsTreeRows;
        private System.Windows.Forms.ComboBox comboRowColumn;
        private System.Windows.Forms.Panel columnFieldPanel;
        private Editor.AvailableFieldsTree availableFieldsTreeColumns;
        private System.Windows.Forms.ComboBox comboColumnColumn;
    }
}