namespace pwiz.Skyline.SettingsUI
{
    partial class DefineExtractedMetadataDlg
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
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colResultFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSource = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMatch = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colExtractedValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.lblName = new System.Windows.Forms.Label();
            this.tbxRuleName = new System.Windows.Forms.TextBox();
            this.lblRegularExpression = new System.Windows.Forms.Label();
            this.tbxRegularExpression = new System.Windows.Forms.TextBox();
            this.lblTarget = new System.Windows.Forms.Label();
            this.comboMetadataTarget = new System.Windows.Forms.ComboBox();
            this.bindingSource1 = new System.Windows.Forms.BindingSource(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.panelButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colResultFile,
            this.colSource,
            this.colMatch,
            this.colExtractedValue});
            this.dataGridView1.Location = new System.Drawing.Point(3, 142);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.Size = new System.Drawing.Size(463, 260);
            this.dataGridView1.TabIndex = 1;
            // 
            // colResultFile
            // 
            this.colResultFile.DataPropertyName = "FileName";
            this.colResultFile.HeaderText = "Result File";
            this.colResultFile.Name = "colResultFile";
            this.colResultFile.ReadOnly = true;
            // 
            // colSource
            // 
            this.colSource.DataPropertyName = "SourceText";
            this.colSource.HeaderText = "Source Text";
            this.colSource.Name = "colSource";
            this.colSource.ReadOnly = true;
            // 
            // colMatch
            // 
            this.colMatch.DataPropertyName = "Match";
            this.colMatch.HeaderText = "Match";
            this.colMatch.Name = "colMatch";
            this.colMatch.ReadOnly = true;
            // 
            // colExtractedValue
            // 
            this.colExtractedValue.DataPropertyName = "ExtractedValue";
            this.colExtractedValue.HeaderText = "Extracted Value";
            this.colExtractedValue.Name = "colExtractedValue";
            this.colExtractedValue.ReadOnly = true;
            this.colExtractedValue.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colExtractedValue.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnCancel);
            this.panelButtons.Controls.Add(this.btnOK);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.panelButtons.Location = new System.Drawing.Point(0, 421);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(478, 29);
            this.panelButtons.TabIndex = 2;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(400, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOK.Location = new System.Drawing.Point(319, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 5;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // lblName
            // 
            this.lblName.AutoSize = true;
            this.lblName.Location = new System.Drawing.Point(12, 9);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(70, 13);
            this.lblName.TabIndex = 0;
            this.lblName.Text = "Name of rule:";
            // 
            // tbxRuleName
            // 
            this.tbxRuleName.Location = new System.Drawing.Point(12, 25);
            this.tbxRuleName.Name = "tbxRuleName";
            this.tbxRuleName.Size = new System.Drawing.Size(166, 20);
            this.tbxRuleName.TabIndex = 1;
            // 
            // lblRegularExpression
            // 
            this.lblRegularExpression.AutoSize = true;
            this.lblRegularExpression.Location = new System.Drawing.Point(12, 48);
            this.lblRegularExpression.Name = "lblRegularExpression";
            this.lblRegularExpression.Size = new System.Drawing.Size(100, 13);
            this.lblRegularExpression.TabIndex = 2;
            this.lblRegularExpression.Text = "Regular expression:";
            // 
            // tbxRegularExpression
            // 
            this.tbxRegularExpression.Location = new System.Drawing.Point(12, 64);
            this.tbxRegularExpression.Name = "tbxRegularExpression";
            this.tbxRegularExpression.Size = new System.Drawing.Size(268, 20);
            this.tbxRegularExpression.TabIndex = 3;
            this.tbxRegularExpression.Leave += new System.EventHandler(this.tbxRegularExpression_Leave);
            // 
            // lblTarget
            // 
            this.lblTarget.AutoSize = true;
            this.lblTarget.Location = new System.Drawing.Point(12, 88);
            this.lblTarget.Name = "lblTarget";
            this.lblTarget.Size = new System.Drawing.Size(98, 13);
            this.lblTarget.TabIndex = 4;
            this.lblTarget.Text = "Where to put value";
            // 
            // comboMetadataTarget
            // 
            this.comboMetadataTarget.FormattingEnabled = true;
            this.comboMetadataTarget.Location = new System.Drawing.Point(12, 104);
            this.comboMetadataTarget.Name = "comboMetadataTarget";
            this.comboMetadataTarget.Size = new System.Drawing.Size(268, 21);
            this.comboMetadataTarget.TabIndex = 5;
            // 
            // DefineExtractedMetadataDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(478, 450);
            this.Controls.Add(this.comboMetadataTarget);
            this.Controls.Add(this.lblTarget);
            this.Controls.Add(this.tbxRegularExpression);
            this.Controls.Add(this.lblRegularExpression);
            this.Controls.Add(this.tbxRuleName);
            this.Controls.Add(this.lblName);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.panelButtons);
            this.Name = "DefineExtractedMetadataDlg";
            this.Text = "DefineExtractedMetadataDlg";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.panelButtons.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.FlowLayoutPanel panelButtons;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox tbxRuleName;
        private System.Windows.Forms.Label lblRegularExpression;
        private System.Windows.Forms.TextBox tbxRegularExpression;
        private System.Windows.Forms.Label lblTarget;
        private System.Windows.Forms.ComboBox comboMetadataTarget;
        private System.Windows.Forms.DataGridViewTextBoxColumn colResultFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSource;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colMatch;
        private System.Windows.Forms.DataGridViewTextBoxColumn colExtractedValue;
        private System.Windows.Forms.BindingSource bindingSource1;
    }
}