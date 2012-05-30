namespace pwiz.Skyline.SettingsUI
{
    partial class EditIsolationSchemeDlg
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditIsolationSchemeDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.rbUseResultsData = new System.Windows.Forms.RadioButton();
            this.textRightPrecursorFilterMz = new System.Windows.Forms.TextBox();
            this.cbAsymIsolation = new System.Windows.Forms.CheckBox();
            this.labelTh = new System.Windows.Forms.Label();
            this.textPrecursorFilterMz = new System.Windows.Forms.TextBox();
            this.labelIsolationWidth = new System.Windows.Forms.Label();
            this.rbPrespecified = new System.Windows.Forms.RadioButton();
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cbSpecifyTarget = new System.Windows.Forms.CheckBox();
            this.labelWindowsPerScan = new System.Windows.Forms.Label();
            this.textWindowsPerScan = new System.Windows.Forms.TextBox();
            this.comboMargins = new System.Windows.Forms.ComboBox();
            this.labelMargins = new System.Windows.Forms.Label();
            this.btnGraph = new System.Windows.Forms.Button();
            this.btnCalculate = new System.Windows.Forms.Button();
            this.gridIsolationWindows = new pwiz.Skyline.Controls.DataGridViewEx();
            this.startDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.endDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.targetDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.startMarginDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.endMarginDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.editIsolationWindowBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.cbMultiplexed = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.gridIsolationWindows)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.editIsolationWindowBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(345, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 20;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(345, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 19;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // rbUseResultsData
            // 
            this.rbUseResultsData.AutoSize = true;
            this.rbUseResultsData.Checked = true;
            this.rbUseResultsData.Location = new System.Drawing.Point(15, 79);
            this.rbUseResultsData.Name = "rbUseResultsData";
            this.rbUseResultsData.Size = new System.Drawing.Size(177, 17);
            this.rbUseResultsData.TabIndex = 2;
            this.rbUseResultsData.TabStop = true;
            this.rbUseResultsData.Text = "&Use results data isolation targets";
            this.rbUseResultsData.UseVisualStyleBackColor = true;
            this.rbUseResultsData.CheckedChanged += new System.EventHandler(this.rbFromResultsData_CheckedChanged);
            // 
            // textRightPrecursorFilterMz
            // 
            this.textRightPrecursorFilterMz.Location = new System.Drawing.Point(80, 123);
            this.textRightPrecursorFilterMz.Name = "textRightPrecursorFilterMz";
            this.textRightPrecursorFilterMz.Size = new System.Drawing.Size(39, 20);
            this.textRightPrecursorFilterMz.TabIndex = 5;
            // 
            // cbAsymIsolation
            // 
            this.cbAsymIsolation.AutoSize = true;
            this.cbAsymIsolation.Location = new System.Drawing.Point(34, 149);
            this.cbAsymIsolation.Name = "cbAsymIsolation";
            this.cbAsymIsolation.Size = new System.Drawing.Size(79, 17);
            this.cbAsymIsolation.TabIndex = 7;
            this.cbAsymIsolation.Text = "&Asymmetric";
            this.cbAsymIsolation.UseVisualStyleBackColor = true;
            this.cbAsymIsolation.CheckedChanged += new System.EventHandler(this.cbAsymIsolation_CheckedChanged);
            // 
            // labelTh
            // 
            this.labelTh.AutoSize = true;
            this.labelTh.Location = new System.Drawing.Point(122, 126);
            this.labelTh.Name = "labelTh";
            this.labelTh.Size = new System.Drawing.Size(20, 13);
            this.labelTh.TabIndex = 6;
            this.labelTh.Text = "Th";
            // 
            // textPrecursorFilterMz
            // 
            this.textPrecursorFilterMz.Location = new System.Drawing.Point(34, 123);
            this.textPrecursorFilterMz.Name = "textPrecursorFilterMz";
            this.textPrecursorFilterMz.Size = new System.Drawing.Size(39, 20);
            this.textPrecursorFilterMz.TabIndex = 4;
            // 
            // labelIsolationWidth
            // 
            this.labelIsolationWidth.AutoSize = true;
            this.labelIsolationWidth.Location = new System.Drawing.Point(31, 106);
            this.labelIsolationWidth.Name = "labelIsolationWidth";
            this.labelIsolationWidth.Size = new System.Drawing.Size(77, 13);
            this.labelIsolationWidth.TabIndex = 3;
            this.labelIsolationWidth.Text = "Isolation &width:";
            // 
            // rbPrespecified
            // 
            this.rbPrespecified.AutoSize = true;
            this.rbPrespecified.Location = new System.Drawing.Point(15, 194);
            this.rbPrespecified.Name = "rbPrespecified";
            this.rbPrespecified.Size = new System.Drawing.Size(168, 17);
            this.rbPrespecified.TabIndex = 8;
            this.rbPrespecified.Text = "&Prespecified isolation windows";
            this.rbPrespecified.UseVisualStyleBackColor = true;
            // 
            // textName
            // 
            this.textName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textName.Location = new System.Drawing.Point(15, 26);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(304, 20);
            this.textName.TabIndex = 1;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 9);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Name:";
            // 
            // cbSpecifyTarget
            // 
            this.cbSpecifyTarget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cbSpecifyTarget.AutoSize = true;
            this.cbSpecifyTarget.Enabled = false;
            this.cbSpecifyTarget.Location = new System.Drawing.Point(278, 464);
            this.cbSpecifyTarget.Name = "cbSpecifyTarget";
            this.cbSpecifyTarget.Size = new System.Drawing.Size(91, 17);
            this.cbSpecifyTarget.TabIndex = 17;
            this.cbSpecifyTarget.Text = "Specify &target";
            this.cbSpecifyTarget.UseVisualStyleBackColor = true;
            this.cbSpecifyTarget.CheckedChanged += new System.EventHandler(this.cbSpecifyTarget_CheckedChanged);
            // 
            // labelWindowsPerScan
            // 
            this.labelWindowsPerScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelWindowsPerScan.AutoSize = true;
            this.labelWindowsPerScan.Enabled = false;
            this.labelWindowsPerScan.Location = new System.Drawing.Point(50, 465);
            this.labelWindowsPerScan.Name = "labelWindowsPerScan";
            this.labelWindowsPerScan.Size = new System.Drawing.Size(98, 13);
            this.labelWindowsPerScan.TabIndex = 13;
            this.labelWindowsPerScan.Text = "&Windows per scan:";
            // 
            // textWindowsPerScan
            // 
            this.textWindowsPerScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textWindowsPerScan.Location = new System.Drawing.Point(154, 462);
            this.textWindowsPerScan.Name = "textWindowsPerScan";
            this.textWindowsPerScan.Size = new System.Drawing.Size(39, 20);
            this.textWindowsPerScan.TabIndex = 14;
            // 
            // comboMargins
            // 
            this.comboMargins.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.comboMargins.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMargins.Enabled = false;
            this.comboMargins.FormattingEnabled = true;
            this.comboMargins.Location = new System.Drawing.Point(278, 437);
            this.comboMargins.Name = "comboMargins";
            this.comboMargins.Size = new System.Drawing.Size(142, 21);
            this.comboMargins.TabIndex = 16;
            this.comboMargins.SelectedIndexChanged += new System.EventHandler(this.comboMargins_SelectedIndexChanged);
            // 
            // labelMargins
            // 
            this.labelMargins.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.labelMargins.AutoSize = true;
            this.labelMargins.Enabled = false;
            this.labelMargins.Location = new System.Drawing.Point(275, 421);
            this.labelMargins.Name = "labelMargins";
            this.labelMargins.Size = new System.Drawing.Size(47, 13);
            this.labelMargins.TabIndex = 15;
            this.labelMargins.Text = "Margi&ns:";
            // 
            // btnGraph
            // 
            this.btnGraph.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnGraph.Enabled = false;
            this.btnGraph.Location = new System.Drawing.Point(345, 191);
            this.btnGraph.Name = "btnGraph";
            this.btnGraph.Size = new System.Drawing.Size(75, 23);
            this.btnGraph.TabIndex = 10;
            this.btnGraph.Text = "&Graph...";
            this.btnGraph.UseVisualStyleBackColor = true;
            // 
            // btnCalculate
            // 
            this.btnCalculate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCalculate.Enabled = false;
            this.btnCalculate.Location = new System.Drawing.Point(264, 191);
            this.btnCalculate.Name = "btnCalculate";
            this.btnCalculate.Size = new System.Drawing.Size(75, 23);
            this.btnCalculate.TabIndex = 9;
            this.btnCalculate.Text = "&Calculate...";
            this.btnCalculate.UseVisualStyleBackColor = true;
            this.btnCalculate.Click += new System.EventHandler(this.btnCalculate_Click);
            // 
            // gridIsolationWindows
            // 
            this.gridIsolationWindows.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridIsolationWindows.AutoGenerateColumns = false;
            this.gridIsolationWindows.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridIsolationWindows.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridIsolationWindows.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridIsolationWindows.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.startDataGridViewTextBoxColumn,
            this.endDataGridViewTextBoxColumn,
            this.targetDataGridViewTextBoxColumn,
            this.startMarginDataGridViewTextBoxColumn,
            this.endMarginDataGridViewTextBoxColumn});
            this.gridIsolationWindows.DataSource = this.editIsolationWindowBindingSource;
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle7.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle7.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle7.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle7.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridIsolationWindows.DefaultCellStyle = dataGridViewCellStyle7;
            this.gridIsolationWindows.Enabled = false;
            this.gridIsolationWindows.Location = new System.Drawing.Point(34, 220);
            this.gridIsolationWindows.Name = "gridIsolationWindows";
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle8.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle8.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle8.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridIsolationWindows.RowHeadersDefaultCellStyle = dataGridViewCellStyle8;
            this.gridIsolationWindows.Size = new System.Drawing.Size(386, 184);
            this.gridIsolationWindows.TabIndex = 11;
            // 
            // startDataGridViewTextBoxColumn
            // 
            this.startDataGridViewTextBoxColumn.DataPropertyName = "Start";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle2.Format = "N4";
            this.startDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.startDataGridViewTextBoxColumn.HeaderText = "Start";
            this.startDataGridViewTextBoxColumn.Name = "startDataGridViewTextBoxColumn";
            // 
            // endDataGridViewTextBoxColumn
            // 
            this.endDataGridViewTextBoxColumn.DataPropertyName = "End";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle3.Format = "N4";
            this.endDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle3;
            this.endDataGridViewTextBoxColumn.HeaderText = "End";
            this.endDataGridViewTextBoxColumn.Name = "endDataGridViewTextBoxColumn";
            // 
            // targetDataGridViewTextBoxColumn
            // 
            this.targetDataGridViewTextBoxColumn.DataPropertyName = "Target";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle4.Format = "N4";
            this.targetDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle4;
            this.targetDataGridViewTextBoxColumn.HeaderText = "Target";
            this.targetDataGridViewTextBoxColumn.Name = "targetDataGridViewTextBoxColumn";
            // 
            // startMarginDataGridViewTextBoxColumn
            // 
            this.startMarginDataGridViewTextBoxColumn.DataPropertyName = "StartMargin";
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle5.Format = "N4";
            this.startMarginDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle5;
            this.startMarginDataGridViewTextBoxColumn.HeaderText = "Start margin";
            this.startMarginDataGridViewTextBoxColumn.Name = "startMarginDataGridViewTextBoxColumn";
            // 
            // endMarginDataGridViewTextBoxColumn
            // 
            this.endMarginDataGridViewTextBoxColumn.DataPropertyName = "EndMargin";
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle6.Format = "N4";
            this.endMarginDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle6;
            this.endMarginDataGridViewTextBoxColumn.HeaderText = "End margin";
            this.endMarginDataGridViewTextBoxColumn.Name = "endMarginDataGridViewTextBoxColumn";
            // 
            // editIsolationWindowBindingSource
            // 
            this.editIsolationWindowBindingSource.DataSource = typeof(pwiz.Skyline.SettingsUI.EditIsolationWindow);
            // 
            // cbMultiplexed
            // 
            this.cbMultiplexed.AutoSize = true;
            this.cbMultiplexed.Enabled = false;
            this.cbMultiplexed.Location = new System.Drawing.Point(34, 437);
            this.cbMultiplexed.Name = "cbMultiplexed";
            this.cbMultiplexed.Size = new System.Drawing.Size(132, 17);
            this.cbMultiplexed.TabIndex = 12;
            this.cbMultiplexed.Text = "&Multiplexed acquisition";
            this.cbMultiplexed.UseVisualStyleBackColor = true;
            this.cbMultiplexed.CheckedChanged += new System.EventHandler(this.cbMultiplexed_CheckedChanged);
            // 
            // EditIsolationSchemeDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(436, 500);
            this.Controls.Add(this.cbMultiplexed);
            this.Controls.Add(this.btnCalculate);
            this.Controls.Add(this.btnGraph);
            this.Controls.Add(this.labelMargins);
            this.Controls.Add(this.comboMargins);
            this.Controls.Add(this.textWindowsPerScan);
            this.Controls.Add(this.labelWindowsPerScan);
            this.Controls.Add(this.cbSpecifyTarget);
            this.Controls.Add(this.gridIsolationWindows);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.rbPrespecified);
            this.Controls.Add(this.textRightPrecursorFilterMz);
            this.Controls.Add(this.cbAsymIsolation);
            this.Controls.Add(this.labelTh);
            this.Controls.Add(this.textPrecursorFilterMz);
            this.Controls.Add(this.labelIsolationWidth);
            this.Controls.Add(this.rbUseResultsData);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditIsolationSchemeDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Isolation Scheme";
            ((System.ComponentModel.ISupportInitialize)(this.gridIsolationWindows)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.editIsolationWindowBindingSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.RadioButton rbUseResultsData;
        private System.Windows.Forms.TextBox textRightPrecursorFilterMz;
        private System.Windows.Forms.CheckBox cbAsymIsolation;
        private System.Windows.Forms.Label labelTh;
        private System.Windows.Forms.TextBox textPrecursorFilterMz;
        private System.Windows.Forms.Label labelIsolationWidth;
        private System.Windows.Forms.RadioButton rbPrespecified;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private Controls.DataGridViewEx gridIsolationWindows;
        private System.Windows.Forms.CheckBox cbSpecifyTarget;
        private System.Windows.Forms.Label labelWindowsPerScan;
        private System.Windows.Forms.TextBox textWindowsPerScan;
        private System.Windows.Forms.ComboBox comboMargins;
        private System.Windows.Forms.Label labelMargins;
        private System.Windows.Forms.Button btnGraph;
        private System.Windows.Forms.Button btnCalculate;
        private System.Windows.Forms.BindingSource editIsolationWindowBindingSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn startDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn endDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn targetDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn startMarginDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn endMarginDataGridViewTextBoxColumn;
        private System.Windows.Forms.CheckBox cbMultiplexed;
    }
}