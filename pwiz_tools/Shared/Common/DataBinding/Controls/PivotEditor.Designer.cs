namespace pwiz.Common.DataBinding.Controls
{
    partial class PivotEditor
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
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.valueButtonPanel = new System.Windows.Forms.Panel();
            this.valueButtons = new System.Windows.Forms.Panel();
            this.btnAddValue = new System.Windows.Forms.Button();
            this.comboAggregateOp = new System.Windows.Forms.ComboBox();
            this.availableColumnList = new pwiz.Common.DataBinding.Controls.ColumnListView();
            this.rowHeaderButtonPanel = new System.Windows.Forms.Panel();
            this.btnAddRowHeader = new System.Windows.Forms.Button();
            this.columnHeaderButtonPanel = new System.Windows.Forms.Panel();
            this.btnAddColumnHeader = new System.Windows.Forms.Button();
            this.rowHeadersPanel = new System.Windows.Forms.Panel();
            this.rowHeadersList = new pwiz.Common.DataBinding.Controls.ColumnListEditor();
            this.lblRowHeaders = new System.Windows.Forms.Label();
            this.panel3 = new System.Windows.Forms.Panel();
            this.columnHeadersList = new pwiz.Common.DataBinding.Controls.ColumnListEditor();
            this.lblColumnHeaders = new System.Windows.Forms.Label();
            this.panelValues = new System.Windows.Forms.Panel();
            this.valuesList = new pwiz.Common.DataBinding.Controls.ColumnListEditor();
            this.lblValues = new System.Windows.Forms.Label();
            this.tableLayoutPanel2.SuspendLayout();
            this.valueButtonPanel.SuspendLayout();
            this.valueButtons.SuspendLayout();
            this.rowHeaderButtonPanel.SuspendLayout();
            this.columnHeaderButtonPanel.SuspendLayout();
            this.rowHeadersPanel.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panelValues.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(548, 329);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(629, 329);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel2.ColumnCount = 3;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.Controls.Add(this.valueButtonPanel, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this.availableColumnList, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.rowHeaderButtonPanel, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.columnHeaderButtonPanel, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this.rowHeadersPanel, 2, 0);
            this.tableLayoutPanel2.Controls.Add(this.panel3, 2, 1);
            this.tableLayoutPanel2.Controls.Add(this.panelValues, 2, 2);
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 3);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 3;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(716, 307);
            this.tableLayoutPanel2.TabIndex = 2;
            // 
            // valueButtonPanel
            // 
            this.valueButtonPanel.Controls.Add(this.valueButtons);
            this.valueButtonPanel.Location = new System.Drawing.Point(261, 207);
            this.valueButtonPanel.Name = "valueButtonPanel";
            this.valueButtonPanel.Size = new System.Drawing.Size(194, 97);
            this.valueButtonPanel.TabIndex = 5;
            this.valueButtonPanel.Resize += new System.EventHandler(this.panelValueButtonsOuter_Resize);
            // 
            // valueButtons
            // 
            this.valueButtons.Controls.Add(this.btnAddValue);
            this.valueButtons.Controls.Add(this.comboAggregateOp);
            this.valueButtons.Location = new System.Drawing.Point(24, 23);
            this.valueButtons.Name = "valueButtons";
            this.valueButtons.Size = new System.Drawing.Size(146, 51);
            this.valueButtons.TabIndex = 2;
            // 
            // btnAddValue
            // 
            this.btnAddValue.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnAddValue.Location = new System.Drawing.Point(0, 0);
            this.btnAddValue.Name = "btnAddValue";
            this.btnAddValue.Size = new System.Drawing.Size(146, 28);
            this.btnAddValue.TabIndex = 0;
            this.btnAddValue.Text = "Add Value >>";
            this.btnAddValue.UseVisualStyleBackColor = true;
            this.btnAddValue.Click += new System.EventHandler(this.btnAddValue_Click);
            // 
            // comboAggregateOp
            // 
            this.comboAggregateOp.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.comboAggregateOp.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAggregateOp.FormattingEnabled = true;
            this.comboAggregateOp.Location = new System.Drawing.Point(0, 30);
            this.comboAggregateOp.Name = "comboAggregateOp";
            this.comboAggregateOp.Size = new System.Drawing.Size(146, 21);
            this.comboAggregateOp.TabIndex = 1;
            // 
            // availableColumnList
            // 
            this.availableColumnList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.availableColumnList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.availableColumnList.HideSelection = false;
            this.availableColumnList.Location = new System.Drawing.Point(3, 3);
            this.availableColumnList.Name = "availableColumnList";
            this.tableLayoutPanel2.SetRowSpan(this.availableColumnList, 3);
            this.availableColumnList.ShowItemToolTips = true;
            this.availableColumnList.Size = new System.Drawing.Size(252, 301);
            this.availableColumnList.TabIndex = 10;
            this.availableColumnList.UseCompatibleStateImageBehavior = false;
            this.availableColumnList.View = System.Windows.Forms.View.Details;
            this.availableColumnList.SelectedIndexChanged += new System.EventHandler(this.availableColumnList_SelectedIndexChanged);
            // 
            // rowHeaderButtonPanel
            // 
            this.rowHeaderButtonPanel.Controls.Add(this.btnAddRowHeader);
            this.rowHeaderButtonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rowHeaderButtonPanel.Location = new System.Drawing.Point(261, 3);
            this.rowHeaderButtonPanel.Name = "rowHeaderButtonPanel";
            this.rowHeaderButtonPanel.Size = new System.Drawing.Size(194, 96);
            this.rowHeaderButtonPanel.TabIndex = 11;
            this.rowHeaderButtonPanel.Resize += new System.EventHandler(this.rowHeaderButtonPanel_Resize);
            // 
            // btnAddRowHeader
            // 
            this.btnAddRowHeader.Location = new System.Drawing.Point(20, 34);
            this.btnAddRowHeader.Name = "btnAddRowHeader";
            this.btnAddRowHeader.Size = new System.Drawing.Size(154, 28);
            this.btnAddRowHeader.TabIndex = 3;
            this.btnAddRowHeader.Text = "Add Row Header >>";
            this.btnAddRowHeader.UseVisualStyleBackColor = true;
            this.btnAddRowHeader.Click += new System.EventHandler(this.btnAddRowHeader_Click);
            // 
            // columnHeaderButtonPanel
            // 
            this.columnHeaderButtonPanel.Controls.Add(this.btnAddColumnHeader);
            this.columnHeaderButtonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.columnHeaderButtonPanel.Location = new System.Drawing.Point(261, 105);
            this.columnHeaderButtonPanel.Name = "columnHeaderButtonPanel";
            this.columnHeaderButtonPanel.Size = new System.Drawing.Size(194, 96);
            this.columnHeaderButtonPanel.TabIndex = 12;
            this.columnHeaderButtonPanel.Resize += new System.EventHandler(this.columnHeaderButtonPanel_Resize);
            // 
            // btnAddColumnHeader
            // 
            this.btnAddColumnHeader.Location = new System.Drawing.Point(32, 37);
            this.btnAddColumnHeader.Name = "btnAddColumnHeader";
            this.btnAddColumnHeader.Size = new System.Drawing.Size(130, 23);
            this.btnAddColumnHeader.TabIndex = 4;
            this.btnAddColumnHeader.Text = "Add Column Header >>";
            this.btnAddColumnHeader.UseVisualStyleBackColor = true;
            this.btnAddColumnHeader.Click += new System.EventHandler(this.btnAddColumnHeader_Click);
            // 
            // rowHeadersPanel
            // 
            this.rowHeadersPanel.Controls.Add(this.rowHeadersList);
            this.rowHeadersPanel.Controls.Add(this.lblRowHeaders);
            this.rowHeadersPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rowHeadersPanel.Location = new System.Drawing.Point(461, 3);
            this.rowHeadersPanel.Name = "rowHeadersPanel";
            this.rowHeadersPanel.Size = new System.Drawing.Size(252, 96);
            this.rowHeadersPanel.TabIndex = 13;
            // 
            // rowHeadersList
            // 
            this.rowHeadersList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rowHeadersList.Location = new System.Drawing.Point(0, 19);
            this.rowHeadersList.Name = "rowHeadersList";
            this.rowHeadersList.Size = new System.Drawing.Size(252, 77);
            this.rowHeadersList.TabIndex = 7;
            // 
            // lblRowHeaders
            // 
            this.lblRowHeaders.AutoSize = true;
            this.lblRowHeaders.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblRowHeaders.Location = new System.Drawing.Point(0, 0);
            this.lblRowHeaders.Name = "lblRowHeaders";
            this.lblRowHeaders.Padding = new System.Windows.Forms.Padding(3);
            this.lblRowHeaders.Size = new System.Drawing.Size(78, 19);
            this.lblRowHeaders.TabIndex = 8;
            this.lblRowHeaders.Text = "Row Headers";
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.columnHeadersList);
            this.panel3.Controls.Add(this.lblColumnHeaders);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(461, 105);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(252, 96);
            this.panel3.TabIndex = 14;
            // 
            // columnHeadersList
            // 
            this.columnHeadersList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.columnHeadersList.Location = new System.Drawing.Point(0, 19);
            this.columnHeadersList.Name = "columnHeadersList";
            this.columnHeadersList.Size = new System.Drawing.Size(252, 77);
            this.columnHeadersList.TabIndex = 8;
            // 
            // lblColumnHeaders
            // 
            this.lblColumnHeaders.AutoSize = true;
            this.lblColumnHeaders.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblColumnHeaders.Location = new System.Drawing.Point(0, 0);
            this.lblColumnHeaders.Name = "lblColumnHeaders";
            this.lblColumnHeaders.Padding = new System.Windows.Forms.Padding(3);
            this.lblColumnHeaders.Size = new System.Drawing.Size(91, 19);
            this.lblColumnHeaders.TabIndex = 9;
            this.lblColumnHeaders.Text = "Column Headers";
            // 
            // panelValues
            // 
            this.panelValues.Controls.Add(this.valuesList);
            this.panelValues.Controls.Add(this.lblValues);
            this.panelValues.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelValues.Location = new System.Drawing.Point(461, 207);
            this.panelValues.Name = "panelValues";
            this.panelValues.Size = new System.Drawing.Size(252, 97);
            this.panelValues.TabIndex = 15;
            // 
            // valuesList
            // 
            this.valuesList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.valuesList.Location = new System.Drawing.Point(0, 19);
            this.valuesList.Name = "valuesList";
            this.valuesList.Size = new System.Drawing.Size(252, 78);
            this.valuesList.TabIndex = 9;
            // 
            // lblValues
            // 
            this.lblValues.AutoSize = true;
            this.lblValues.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblValues.Location = new System.Drawing.Point(0, 0);
            this.lblValues.Name = "lblValues";
            this.lblValues.Padding = new System.Windows.Forms.Padding(3);
            this.lblValues.Size = new System.Drawing.Size(45, 19);
            this.lblValues.TabIndex = 10;
            this.lblValues.Text = "Values";
            // 
            // PivotEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(716, 364);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tableLayoutPanel2);
            this.MinimizeBox = false;
            this.Name = "PivotEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Pivot Editor";
            this.tableLayoutPanel2.ResumeLayout(false);
            this.valueButtonPanel.ResumeLayout(false);
            this.valueButtons.ResumeLayout(false);
            this.rowHeaderButtonPanel.ResumeLayout(false);
            this.columnHeaderButtonPanel.ResumeLayout(false);
            this.rowHeadersPanel.ResumeLayout(false);
            this.rowHeadersPanel.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.panelValues.ResumeLayout(false);
            this.panelValues.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Button btnAddRowHeader;
        private System.Windows.Forms.Button btnAddColumnHeader;
        private System.Windows.Forms.Panel valueButtonPanel;
        private System.Windows.Forms.ComboBox comboAggregateOp;
        private System.Windows.Forms.Button btnAddValue;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private ColumnListEditor rowHeadersList;
        private ColumnListEditor columnHeadersList;
        private ColumnListEditor valuesList;
        private ColumnListView availableColumnList;
        private System.Windows.Forms.Panel rowHeaderButtonPanel;
        private System.Windows.Forms.Panel columnHeaderButtonPanel;
        private System.Windows.Forms.Panel rowHeadersPanel;
        private System.Windows.Forms.Label lblRowHeaders;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label lblColumnHeaders;
        private System.Windows.Forms.Panel panelValues;
        private System.Windows.Forms.Label lblValues;
        private System.Windows.Forms.Panel valueButtons;
    }
}