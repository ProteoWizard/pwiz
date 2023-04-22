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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditSpectrumFilterDlg));
            this.dataGridViewEx1 = new pwiz.Skyline.Controls.DataGridViewEx();
            this.cbCreateCopy = new System.Windows.Forms.CheckBox();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.valueDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel1 = new System.Windows.Forms.Panel();
            this.toolStripFilter = new System.Windows.Forms.ToolStrip();
            this.btnDeleteFilter = new System.Windows.Forms.ToolStripButton();
            this.propertyColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.operationColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.valueColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewEx1)).BeginInit();
            this.panel1.SuspendLayout();
            this.toolStripFilter.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridViewEx1
            // 
            this.dataGridViewEx1.AutoGenerateColumns = false;
            this.dataGridViewEx1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewEx1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewEx1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.propertyColumn,
            this.operationColumn,
            this.valueColumn});
            this.dataGridViewEx1.DataSource = new pwiz.Skyline.EditUI.EditSpectrumFilterDlg.Row[0];
            this.dataGridViewEx1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewEx1.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewEx1.Name = "dataGridViewEx1";
            this.dataGridViewEx1.Size = new System.Drawing.Size(677, 216);
            this.dataGridViewEx1.TabIndex = 0;
            this.dataGridViewEx1.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewEx1_DataError);
            this.dataGridViewEx1.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.dataGridViewEx1_EditingControlShowing);
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
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.dataGridViewEx1);
            this.panel1.Controls.Add(this.toolStripFilter);
            this.panel1.Location = new System.Drawing.Point(6, 7);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(701, 216);
            this.panel1.TabIndex = 10;
            // 
            // toolStripFilter
            // 
            this.toolStripFilter.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStripFilter.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnDeleteFilter});
            this.toolStripFilter.Location = new System.Drawing.Point(677, 0);
            this.toolStripFilter.Name = "toolStripFilter";
            this.toolStripFilter.Size = new System.Drawing.Size(24, 216);
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
            // EditSpectrumFilterDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 235);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.cbCreateCopy);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Name = "EditSpectrumFilterDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Spectrum Filter";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewEx1)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.toolStripFilter.ResumeLayout(false);
            this.toolStripFilter.PerformLayout();
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
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolStrip toolStripFilter;
        private System.Windows.Forms.ToolStripButton btnDeleteFilter;
        private System.Windows.Forms.DataGridViewComboBoxColumn propertyColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn operationColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn valueColumn;
    }
}