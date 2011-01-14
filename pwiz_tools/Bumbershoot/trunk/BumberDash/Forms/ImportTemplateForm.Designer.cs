namespace BumberDash.Forms
{
    sealed partial class ImportTemplateForm
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
            this.TemplateRemove = new System.Windows.Forms.Button();
            this.TemplateAdd = new System.Windows.Forms.Button();
            this.OutputLabel = new System.Windows.Forms.Label();
            this.AvailableLabel = new System.Windows.Forms.Label();
            this.AvailableDGV = new System.Windows.Forms.DataGridView();
            this.AvailableNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AvailableProgramColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.OutputDGV = new System.Windows.Forms.DataGridView();
            this.OutputNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.OutputProgramColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ValueBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.AddAllButton = new System.Windows.Forms.Button();
            this.RemoveAllButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.AvailableDGV)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.OutputDGV)).BeginInit();
            this.SuspendLayout();
            // 
            // TemplateRemove
            // 
            this.TemplateRemove.Font = new System.Drawing.Font("Times New Roman", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TemplateRemove.Location = new System.Drawing.Point(129, 206);
            this.TemplateRemove.Name = "TemplateRemove";
            this.TemplateRemove.Size = new System.Drawing.Size(32, 23);
            this.TemplateRemove.TabIndex = 91;
            this.TemplateRemove.Text = "^";
            this.TemplateRemove.UseVisualStyleBackColor = true;
            this.TemplateRemove.Click += new System.EventHandler(this.TemplateRemove_Click);
            // 
            // TemplateAdd
            // 
            this.TemplateAdd.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TemplateAdd.Location = new System.Drawing.Point(88, 206);
            this.TemplateAdd.Name = "TemplateAdd";
            this.TemplateAdd.Size = new System.Drawing.Size(32, 23);
            this.TemplateAdd.TabIndex = 90;
            this.TemplateAdd.Text = "v";
            this.TemplateAdd.UseVisualStyleBackColor = true;
            this.TemplateAdd.Click += new System.EventHandler(this.TemplateAdd_Click);
            // 
            // OutputLabel
            // 
            this.OutputLabel.AutoSize = true;
            this.OutputLabel.Location = new System.Drawing.Point(9, 230);
            this.OutputLabel.Name = "OutputLabel";
            this.OutputLabel.Size = new System.Drawing.Size(55, 13);
            this.OutputLabel.TabIndex = 92;
            this.OutputLabel.Text = "To Import:";
            // 
            // AvailableLabel
            // 
            this.AvailableLabel.AutoSize = true;
            this.AvailableLabel.Location = new System.Drawing.Point(9, 9);
            this.AvailableLabel.Name = "AvailableLabel";
            this.AvailableLabel.Size = new System.Drawing.Size(53, 13);
            this.AvailableLabel.TabIndex = 93;
            this.AvailableLabel.Text = "Available:";
            // 
            // AvailableDGV
            // 
            this.AvailableDGV.AllowUserToAddRows = false;
            this.AvailableDGV.AllowUserToDeleteRows = false;
            this.AvailableDGV.AllowUserToResizeColumns = false;
            this.AvailableDGV.AllowUserToResizeRows = false;
            this.AvailableDGV.BackgroundColor = System.Drawing.SystemColors.Window;
            this.AvailableDGV.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.AvailableDGV.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.AvailableDGV.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.AvailableNameColumn,
            this.AvailableProgramColumn});
            this.AvailableDGV.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.AvailableDGV.GridColor = System.Drawing.SystemColors.Window;
            this.AvailableDGV.Location = new System.Drawing.Point(12, 25);
            this.AvailableDGV.MultiSelect = false;
            this.AvailableDGV.Name = "AvailableDGV";
            this.AvailableDGV.RowHeadersVisible = false;
            this.AvailableDGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.AvailableDGV.Size = new System.Drawing.Size(225, 175);
            this.AvailableDGV.TabIndex = 94;
            this.AvailableDGV.SelectionChanged += new System.EventHandler(this.AvailableDGV_SelectionChanged);
            // 
            // AvailableNameColumn
            // 
            this.AvailableNameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.AvailableNameColumn.HeaderText = "Name";
            this.AvailableNameColumn.Name = "AvailableNameColumn";
            // 
            // AvailableProgramColumn
            // 
            this.AvailableProgramColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.AvailableProgramColumn.FillWeight = 50F;
            this.AvailableProgramColumn.HeaderText = "Program";
            this.AvailableProgramColumn.Name = "AvailableProgramColumn";
            // 
            // OutputDGV
            // 
            this.OutputDGV.AllowUserToAddRows = false;
            this.OutputDGV.AllowUserToDeleteRows = false;
            this.OutputDGV.AllowUserToResizeColumns = false;
            this.OutputDGV.AllowUserToResizeRows = false;
            this.OutputDGV.BackgroundColor = System.Drawing.SystemColors.Window;
            this.OutputDGV.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.OutputDGV.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.OutputDGV.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.OutputNameColumn,
            this.OutputProgramColumn});
            this.OutputDGV.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.OutputDGV.GridColor = System.Drawing.SystemColors.Window;
            this.OutputDGV.Location = new System.Drawing.Point(12, 246);
            this.OutputDGV.MultiSelect = false;
            this.OutputDGV.Name = "OutputDGV";
            this.OutputDGV.RowHeadersVisible = false;
            this.OutputDGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.OutputDGV.Size = new System.Drawing.Size(225, 175);
            this.OutputDGV.TabIndex = 95;
            this.OutputDGV.SelectionChanged += new System.EventHandler(this.OutputDGV_SelectionChanged);
            // 
            // OutputNameColumn
            // 
            this.OutputNameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.OutputNameColumn.HeaderText = "Name";
            this.OutputNameColumn.Name = "OutputNameColumn";
            // 
            // OutputProgramColumn
            // 
            this.OutputProgramColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.OutputProgramColumn.FillWeight = 50F;
            this.OutputProgramColumn.HeaderText = "Program";
            this.OutputProgramColumn.Name = "OutputProgramColumn";
            // 
            // ValueBox
            // 
            this.ValueBox.Location = new System.Drawing.Point(260, 25);
            this.ValueBox.Multiline = true;
            this.ValueBox.Name = "ValueBox";
            this.ValueBox.Size = new System.Drawing.Size(222, 396);
            this.ValueBox.TabIndex = 96;
            this.ValueBox.WordWrap = false;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(257, 9);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(52, 13);
            this.label3.TabIndex = 97;
            this.label3.Text = "Contents:";
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(326, 431);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 98;
            this.okButton.Text = "Import";
            this.okButton.UseVisualStyleBackColor = true;
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(407, 431);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 99;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // AddAllButton
            // 
            this.AddAllButton.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.AddAllButton.Location = new System.Drawing.Point(12, 206);
            this.AddAllButton.Name = "AddAllButton";
            this.AddAllButton.Size = new System.Drawing.Size(70, 23);
            this.AddAllButton.TabIndex = 100;
            this.AddAllButton.Text = "Add All";
            this.AddAllButton.UseVisualStyleBackColor = true;
            this.AddAllButton.Click += new System.EventHandler(this.AddAllButton_Click);
            // 
            // RemoveAllButton
            // 
            this.RemoveAllButton.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.RemoveAllButton.Location = new System.Drawing.Point(167, 206);
            this.RemoveAllButton.Name = "RemoveAllButton";
            this.RemoveAllButton.Size = new System.Drawing.Size(70, 23);
            this.RemoveAllButton.TabIndex = 101;
            this.RemoveAllButton.Text = "Remove All";
            this.RemoveAllButton.UseVisualStyleBackColor = true;
            this.RemoveAllButton.Click += new System.EventHandler(this.RemoveAllButton_Click);
            // 
            // ImportTemplateForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(494, 466);
            this.Controls.Add(this.RemoveAllButton);
            this.Controls.Add(this.AddAllButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.ValueBox);
            this.Controls.Add(this.OutputDGV);
            this.Controls.Add(this.AvailableDGV);
            this.Controls.Add(this.AvailableLabel);
            this.Controls.Add(this.OutputLabel);
            this.Controls.Add(this.TemplateRemove);
            this.Controls.Add(this.TemplateAdd);
            this.Name = "ImportTemplateForm";
            this.Text = "Import";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ImportTemplateForm_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.AvailableDGV)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.OutputDGV)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button TemplateRemove;
        private System.Windows.Forms.Button TemplateAdd;
        private System.Windows.Forms.Label OutputLabel;
        private System.Windows.Forms.Label AvailableLabel;
        private System.Windows.Forms.DataGridView AvailableDGV;
        private System.Windows.Forms.DataGridViewTextBoxColumn AvailableNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn AvailableProgramColumn;
        private System.Windows.Forms.DataGridView OutputDGV;
        private System.Windows.Forms.TextBox ValueBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button AddAllButton;
        private System.Windows.Forms.Button RemoveAllButton;
        private System.Windows.Forms.DataGridViewTextBoxColumn OutputNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn OutputProgramColumn;
    }
}