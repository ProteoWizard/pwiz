namespace pwiz.Skyline.FileUI
{
    partial class MinimizeResultsDlg
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.bindingSource1 = new System.Windows.Forms.BindingSource(this.components);
            this.dataGridViewSizes = new System.Windows.Forms.DataGridView();
            this.colReplicateName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCacheFileSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMinimizedRatio = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cbxDiscardUnmatchedChromatograms = new System.Windows.Forms.CheckBox();
            this.tbxNoiseTimeRange = new System.Windows.Forms.TextBox();
            this.cbxLimitNoiseTime = new System.Windows.Forms.CheckBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnMinimize = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.lblCurrentCacheFileSize = new System.Windows.Forms.Label();
            this.lblSpaceSavings = new System.Windows.Forms.Label();
            this.btnMinimizeAs = new System.Windows.Forms.Button();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSizes)).BeginInit();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridViewSizes
            // 
            this.dataGridViewSizes.AllowUserToAddRows = false;
            this.dataGridViewSizes.AllowUserToDeleteRows = false;
            this.dataGridViewSizes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridViewSizes.AutoGenerateColumns = false;
            this.dataGridViewSizes.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewSizes.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridViewSizes.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewSizes.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colReplicateName,
            this.colCacheFileSize,
            this.colMinimizedRatio});
            this.dataGridViewSizes.DataSource = this.bindingSource1;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewSizes.DefaultCellStyle = dataGridViewCellStyle4;
            this.dataGridViewSizes.Location = new System.Drawing.Point(12, 170);
            this.dataGridViewSizes.Name = "dataGridViewSizes";
            this.dataGridViewSizes.ReadOnly = true;
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewSizes.RowHeadersDefaultCellStyle = dataGridViewCellStyle5;
            this.dataGridViewSizes.RowHeadersVisible = false;
            this.dataGridViewSizes.Size = new System.Drawing.Size(438, 176);
            this.dataGridViewSizes.TabIndex = 5;
            // 
            // colReplicateName
            // 
            this.colReplicateName.DataPropertyName = "Name";
            this.colReplicateName.HeaderText = "Replicate";
            this.colReplicateName.Name = "colReplicateName";
            this.colReplicateName.ReadOnly = true;
            // 
            // colCacheFileSize
            // 
            this.colCacheFileSize.DataPropertyName = "Size";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.colCacheFileSize.DefaultCellStyle = dataGridViewCellStyle2;
            this.colCacheFileSize.HeaderText = "Current Size";
            this.colCacheFileSize.Name = "colCacheFileSize";
            this.colCacheFileSize.ReadOnly = true;
            // 
            // colMinimizedRatio
            // 
            this.colMinimizedRatio.DataPropertyName = "MinimizedSize";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle3.Format = "0%";
            dataGridViewCellStyle3.NullValue = null;
            this.colMinimizedRatio.DefaultCellStyle = dataGridViewCellStyle3;
            this.colMinimizedRatio.HeaderText = "Minimized Size";
            this.colMinimizedRatio.Name = "colMinimizedRatio";
            this.colMinimizedRatio.ReadOnly = true;
            // 
            // cbxDiscardUnmatchedChromatograms
            // 
            this.cbxDiscardUnmatchedChromatograms.AutoSize = true;
            this.cbxDiscardUnmatchedChromatograms.Location = new System.Drawing.Point(12, 71);
            this.cbxDiscardUnmatchedChromatograms.Name = "cbxDiscardUnmatchedChromatograms";
            this.cbxDiscardUnmatchedChromatograms.Size = new System.Drawing.Size(175, 17);
            this.cbxDiscardUnmatchedChromatograms.TabIndex = 1;
            this.cbxDiscardUnmatchedChromatograms.Text = "Discard unused chromatograms";
            this.cbxDiscardUnmatchedChromatograms.UseVisualStyleBackColor = true;
            this.cbxDiscardUnmatchedChromatograms.CheckedChanged += new System.EventHandler(this.cbxDiscardUnmatchedChromatograms_CheckedChanged);
            // 
            // tbxNoiseTimeRange
            // 
            this.tbxNoiseTimeRange.Location = new System.Drawing.Point(99, 3);
            this.tbxNoiseTimeRange.Name = "tbxNoiseTimeRange";
            this.tbxNoiseTimeRange.Size = new System.Drawing.Size(100, 20);
            this.tbxNoiseTimeRange.TabIndex = 1;
            this.tbxNoiseTimeRange.Text = "1";
            this.tbxNoiseTimeRange.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.tbxNoiseTimeRange.Leave += new System.EventHandler(this.tbxNoiseTimeRange_Leave);
            // 
            // cbxLimitNoiseTime
            // 
            this.cbxLimitNoiseTime.AutoSize = true;
            this.cbxLimitNoiseTime.Location = new System.Drawing.Point(3, 3);
            this.cbxLimitNoiseTime.Name = "cbxLimitNoiseTime";
            this.cbxLimitNoiseTime.Size = new System.Drawing.Size(90, 17);
            this.cbxLimitNoiseTime.TabIndex = 0;
            this.cbxLimitNoiseTime.Text = "Limit noise to ";
            this.cbxLimitNoiseTime.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.cbxLimitNoiseTime.UseVisualStyleBackColor = true;
            this.cbxLimitNoiseTime.CheckedChanged += new System.EventHandler(this.cbxLimitNoiseTime_CheckedChanged);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(371, 352);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnMinimize
            // 
            this.btnMinimize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMinimize.Location = new System.Drawing.Point(130, 352);
            this.btnMinimize.Name = "btnMinimize";
            this.btnMinimize.Size = new System.Drawing.Size(103, 23);
            this.btnMinimize.TabIndex = 6;
            this.btnMinimize.Text = "Minimize in place";
            this.btnMinimize.UseVisualStyleBackColor = true;
            this.btnMinimize.Click += new System.EventHandler(this.btnMinimize_Click);
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(12, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(437, 59);
            this.label2.TabIndex = 0;
            this.label2.Text = "To reduce the size of the Skyline cache file (.skyd), you can discard chromatogra" +
                "ms that are not used by this document, as well as limit the length of chromatogr" +
                "ams.";
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(205, 3);
            this.label3.Margin = new System.Windows.Forms.Padding(3);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(221, 17);
            this.label3.TabIndex = 2;
            this.label3.Text = "minutes before and after chromatogram peak.";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblCurrentCacheFileSize
            // 
            this.lblCurrentCacheFileSize.AutoSize = true;
            this.lblCurrentCacheFileSize.Location = new System.Drawing.Point(9, 123);
            this.lblCurrentCacheFileSize.Name = "lblCurrentCacheFileSize";
            this.lblCurrentCacheFileSize.Size = new System.Drawing.Size(209, 13);
            this.lblCurrentCacheFileSize.TabIndex = 3;
            this.lblCurrentCacheFileSize.Text = "The current size of the cache file is xxx MB";
            // 
            // lblSpaceSavings
            // 
            this.lblSpaceSavings.AutoSize = true;
            this.lblSpaceSavings.Location = new System.Drawing.Point(12, 145);
            this.lblSpaceSavings.Name = "lblSpaceSavings";
            this.lblSpaceSavings.Size = new System.Drawing.Size(137, 13);
            this.lblSpaceSavings.TabIndex = 4;
            this.lblSpaceSavings.Text = "Computing space savings...";
            // 
            // btnMinimizeAs
            // 
            this.btnMinimizeAs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMinimizeAs.Location = new System.Drawing.Point(239, 352);
            this.btnMinimizeAs.Name = "btnMinimizeAs";
            this.btnMinimizeAs.Size = new System.Drawing.Size(126, 23);
            this.btnMinimizeAs.TabIndex = 7;
            this.btnMinimizeAs.Text = "Minimize and save as...";
            this.btnMinimizeAs.UseVisualStyleBackColor = true;
            this.btnMinimizeAs.Click += new System.EventHandler(this.btnMinimizeAs_Click);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.cbxLimitNoiseTime);
            this.flowLayoutPanel1.Controls.Add(this.tbxNoiseTimeRange);
            this.flowLayoutPanel1.Controls.Add(this.label3);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(9, 94);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(440, 26);
            this.flowLayoutPanel1.TabIndex = 2;
            // 
            // MinimizeResultsDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(458, 387);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.btnMinimizeAs);
            this.Controls.Add(this.lblSpaceSavings);
            this.Controls.Add(this.lblCurrentCacheFileSize);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnMinimize);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.cbxDiscardUnmatchedChromatograms);
            this.Controls.Add(this.dataGridViewSizes);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MinimizeResultsDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Minimize Results";
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSizes)).EndInit();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.BindingSource bindingSource1;
        private System.Windows.Forms.DataGridView dataGridViewSizes;
        private System.Windows.Forms.CheckBox cbxDiscardUnmatchedChromatograms;
        private System.Windows.Forms.TextBox tbxNoiseTimeRange;
        private System.Windows.Forms.CheckBox cbxLimitNoiseTime;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnMinimize;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblCurrentCacheFileSize;
        private System.Windows.Forms.Label lblSpaceSavings;
        private System.Windows.Forms.Button btnMinimizeAs;
        private System.Windows.Forms.DataGridViewTextBoxColumn colReplicateName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCacheFileSize;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMinimizedRatio;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
    }
}
