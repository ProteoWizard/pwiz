namespace pwiz.Skyline.EditUI
{
    partial class EditSpectrumFilterDlg
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditSpectrumFilterDlg));
            this.dataGridViewEx1 = new pwiz.Skyline.Controls.DataGridViewEx();
            this.propertyColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.operationColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.valueColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cbCreateCopy = new System.Windows.Forms.CheckBox();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.valueDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelClauses = new System.Windows.Forms.Panel();
            this.toolStripFilter = new System.Windows.Forms.ToolStrip();
            this.btnDeleteFilter = new System.Windows.Forms.ToolStripButton();
            this.panelMsLevel = new System.Windows.Forms.Panel();
            this.groupBoxMSLevel = new System.Windows.Forms.GroupBox();
            this.radioMS2 = new System.Windows.Forms.RadioButton();
            this.radioMs1 = new System.Windows.Forms.RadioButton();
            this.lblDescription = new System.Windows.Forms.Label();
            this.panelEditor = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewEx1)).BeginInit();
            this.panelClauses.SuspendLayout();
            this.toolStripFilter.SuspendLayout();
            this.panelMsLevel.SuspendLayout();
            this.groupBoxMSLevel.SuspendLayout();
            this.panelEditor.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridViewEx1
            // 
            this.dataGridViewEx1.AutoGenerateColumns = false;
            this.dataGridViewEx1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewEx1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridViewEx1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewEx1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.propertyColumn,
            this.operationColumn,
            this.valueColumn});
            this.dataGridViewEx1.DataSource = new pwiz.Skyline.EditUI.EditSpectrumFilterDlg.Row[0];
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewEx1.DefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridViewEx1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewEx1.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewEx1.Name = "dataGridViewEx1";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewEx1.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridViewEx1.Size = new System.Drawing.Size(682, 174);
            this.dataGridViewEx1.TabIndex = 0;
            this.dataGridViewEx1.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewEx1_DataError);
            this.dataGridViewEx1.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.dataGridViewEx1_EditingControlShowing);
            // 
            // propertyColumn
            // 
            this.propertyColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.propertyColumn.DataPropertyName = "Property";
            this.propertyColumn.HeaderText = "Property";
            this.propertyColumn.Name = "propertyColumn";
            this.propertyColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.propertyColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.propertyColumn.Width = 150;
            // 
            // operationColumn
            // 
            this.operationColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.operationColumn.DataPropertyName = "Operation";
            this.operationColumn.HeaderText = "Operation";
            this.operationColumn.Name = "operationColumn";
            this.operationColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.operationColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.operationColumn.Width = 150;
            // 
            // valueColumn
            // 
            this.valueColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.valueColumn.DataPropertyName = "Value";
            this.valueColumn.HeaderText = "Value";
            this.valueColumn.Name = "valueColumn";
            // 
            // cbCreateCopy
            // 
            this.cbCreateCopy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbCreateCopy.AutoSize = true;
            this.cbCreateCopy.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.cbCreateCopy.Location = new System.Drawing.Point(714, 84);
            this.cbCreateCopy.Name = "cbCreateCopy";
            this.cbCreateCopy.Size = new System.Drawing.Size(83, 17);
            this.cbCreateCopy.TabIndex = 7;
            this.cbCreateCopy.Text = "&Create copy";
            this.cbCreateCopy.UseVisualStyleBackColor = true;
            // 
            // btnReset
            // 
            this.btnReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnReset.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnReset.Location = new System.Drawing.Point(714, 200);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(75, 23);
            this.btnReset.TabIndex = 8;
            this.btnReset.Text = "&Reset";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(713, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOk.Location = new System.Drawing.Point(713, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // valueDataGridViewTextBoxColumn
            // 
            this.valueDataGridViewTextBoxColumn.DataPropertyName = "Value";
            this.valueDataGridViewTextBoxColumn.HeaderText = "Value";
            this.valueDataGridViewTextBoxColumn.Name = "valueDataGridViewTextBoxColumn";
            // 
            // panelClauses
            // 
            this.panelClauses.Controls.Add(this.dataGridViewEx1);
            this.panelClauses.Controls.Add(this.toolStripFilter);
            this.panelClauses.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelClauses.Location = new System.Drawing.Point(0, 62);
            this.panelClauses.Name = "panelClauses";
            this.panelClauses.Size = new System.Drawing.Size(706, 174);
            this.panelClauses.TabIndex = 10;
            // 
            // toolStripFilter
            // 
            this.toolStripFilter.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStripFilter.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnDeleteFilter});
            this.toolStripFilter.Location = new System.Drawing.Point(682, 0);
            this.toolStripFilter.Name = "toolStripFilter";
            this.toolStripFilter.Size = new System.Drawing.Size(24, 174);
            this.toolStripFilter.TabIndex = 2;
            this.toolStripFilter.Text = "toolStrip1";
            // 
            // btnDeleteFilter
            // 
            this.btnDeleteFilter.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDeleteFilter.Image = ((System.Drawing.Image)(resources.GetObject("btnDeleteFilter.Image")));
            this.btnDeleteFilter.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDeleteFilter.Name = "btnDeleteFilter";
            this.btnDeleteFilter.Size = new System.Drawing.Size(21, 20);
            this.btnDeleteFilter.Text = "Delete";
            this.btnDeleteFilter.Click += new System.EventHandler(this.btnDeleteFilter_Click);
            // 
            // panelMsLevel
            // 
            this.panelMsLevel.Controls.Add(this.groupBoxMSLevel);
            this.panelMsLevel.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelMsLevel.Location = new System.Drawing.Point(0, 19);
            this.panelMsLevel.Name = "panelMsLevel";
            this.panelMsLevel.Size = new System.Drawing.Size(706, 43);
            this.panelMsLevel.TabIndex = 11;
            // 
            // groupBoxMSLevel
            // 
            this.groupBoxMSLevel.Controls.Add(this.radioMS2);
            this.groupBoxMSLevel.Controls.Add(this.radioMs1);
            this.groupBoxMSLevel.Location = new System.Drawing.Point(0, 0);
            this.groupBoxMSLevel.Name = "groupBoxMSLevel";
            this.groupBoxMSLevel.Size = new System.Drawing.Size(391, 40);
            this.groupBoxMSLevel.TabIndex = 0;
            this.groupBoxMSLevel.TabStop = false;
            this.groupBoxMSLevel.Text = "MS Level";
            // 
            // radioMS2
            // 
            this.radioMS2.AutoSize = true;
            this.radioMS2.Location = new System.Drawing.Point(132, 17);
            this.radioMS2.Name = "radioMS2";
            this.radioMS2.Size = new System.Drawing.Size(85, 17);
            this.radioMS2.TabIndex = 1;
            this.radioMS2.TabStop = true;
            this.radioMS2.Text = "MS2 or more";
            this.radioMS2.UseVisualStyleBackColor = true;
            // 
            // radioMs1
            // 
            this.radioMs1.AutoSize = true;
            this.radioMs1.Location = new System.Drawing.Point(6, 17);
            this.radioMs1.Name = "radioMs1";
            this.radioMs1.Size = new System.Drawing.Size(47, 17);
            this.radioMs1.TabIndex = 0;
            this.radioMs1.TabStop = true;
            this.radioMs1.Text = "MS1";
            this.radioMs1.UseVisualStyleBackColor = true;
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblDescription.Location = new System.Drawing.Point(0, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Padding = new System.Windows.Forms.Padding(3);
            this.lblDescription.Size = new System.Drawing.Size(66, 19);
            this.lblDescription.TabIndex = 12;
            this.lblDescription.Text = "Description";
            // 
            // panelEditor
            // 
            this.panelEditor.Controls.Add(this.panelClauses);
            this.panelEditor.Controls.Add(this.panelMsLevel);
            this.panelEditor.Controls.Add(this.lblDescription);
            this.panelEditor.Location = new System.Drawing.Point(1, -1);
            this.panelEditor.Name = "panelEditor";
            this.panelEditor.Size = new System.Drawing.Size(706, 236);
            this.panelEditor.TabIndex = 13;
            // 
            // EditSpectrumFilterDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 235);
            this.Controls.Add(this.cbCreateCopy);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.panelEditor);
            this.Name = "EditSpectrumFilterDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Spectrum Filter";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewEx1)).EndInit();
            this.panelClauses.ResumeLayout(false);
            this.panelClauses.PerformLayout();
            this.toolStripFilter.ResumeLayout(false);
            this.toolStripFilter.PerformLayout();
            this.panelMsLevel.ResumeLayout(false);
            this.groupBoxMSLevel.ResumeLayout(false);
            this.groupBoxMSLevel.PerformLayout();
            this.panelEditor.ResumeLayout(false);
            this.panelEditor.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Controls.DataGridViewEx dataGridViewEx1;
        private System.Windows.Forms.CheckBox cbCreateCopy;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.DataGridViewTextBoxColumn valueDataGridViewTextBoxColumn;
        private System.Windows.Forms.Panel panelClauses;
        private System.Windows.Forms.ToolStrip toolStripFilter;
        private System.Windows.Forms.ToolStripButton btnDeleteFilter;
        private System.Windows.Forms.DataGridViewComboBoxColumn propertyColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn operationColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn valueColumn;
        private System.Windows.Forms.Panel panelMsLevel;
        private System.Windows.Forms.GroupBox groupBoxMSLevel;
        private System.Windows.Forms.RadioButton radioMS2;
        private System.Windows.Forms.RadioButton radioMs1;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.Panel panelEditor;
    }
}