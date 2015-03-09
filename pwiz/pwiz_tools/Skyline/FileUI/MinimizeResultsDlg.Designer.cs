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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MinimizeResultsDlg));
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
            resources.ApplyResources(this.dataGridViewSizes, "dataGridViewSizes");
            this.dataGridViewSizes.AutoGenerateColumns = false;
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
            // 
            // colReplicateName
            // 
            this.colReplicateName.DataPropertyName = "Name";
            resources.ApplyResources(this.colReplicateName, "colReplicateName");
            this.colReplicateName.Name = "colReplicateName";
            this.colReplicateName.ReadOnly = true;
            // 
            // colCacheFileSize
            // 
            this.colCacheFileSize.DataPropertyName = "Size";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.colCacheFileSize.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.colCacheFileSize, "colCacheFileSize");
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
            resources.ApplyResources(this.colMinimizedRatio, "colMinimizedRatio");
            this.colMinimizedRatio.Name = "colMinimizedRatio";
            this.colMinimizedRatio.ReadOnly = true;
            // 
            // cbxDiscardUnmatchedChromatograms
            // 
            resources.ApplyResources(this.cbxDiscardUnmatchedChromatograms, "cbxDiscardUnmatchedChromatograms");
            this.cbxDiscardUnmatchedChromatograms.Name = "cbxDiscardUnmatchedChromatograms";
            this.cbxDiscardUnmatchedChromatograms.UseVisualStyleBackColor = true;
            this.cbxDiscardUnmatchedChromatograms.CheckedChanged += new System.EventHandler(this.cbxDiscardUnmatchedChromatograms_CheckedChanged);
            // 
            // tbxNoiseTimeRange
            // 
            resources.ApplyResources(this.tbxNoiseTimeRange, "tbxNoiseTimeRange");
            this.tbxNoiseTimeRange.Name = "tbxNoiseTimeRange";
            this.tbxNoiseTimeRange.Leave += new System.EventHandler(this.tbxNoiseTimeRange_Leave);
            // 
            // cbxLimitNoiseTime
            // 
            resources.ApplyResources(this.cbxLimitNoiseTime, "cbxLimitNoiseTime");
            this.cbxLimitNoiseTime.Name = "cbxLimitNoiseTime";
            this.cbxLimitNoiseTime.UseVisualStyleBackColor = true;
            this.cbxLimitNoiseTime.CheckedChanged += new System.EventHandler(this.cbxLimitNoiseTime_CheckedChanged);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnMinimize
            // 
            resources.ApplyResources(this.btnMinimize, "btnMinimize");
            this.btnMinimize.Name = "btnMinimize";
            this.btnMinimize.UseVisualStyleBackColor = true;
            this.btnMinimize.Click += new System.EventHandler(this.btnMinimize_Click);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // lblCurrentCacheFileSize
            // 
            resources.ApplyResources(this.lblCurrentCacheFileSize, "lblCurrentCacheFileSize");
            this.lblCurrentCacheFileSize.Name = "lblCurrentCacheFileSize";
            // 
            // lblSpaceSavings
            // 
            resources.ApplyResources(this.lblSpaceSavings, "lblSpaceSavings");
            this.lblSpaceSavings.Name = "lblSpaceSavings";
            // 
            // btnMinimizeAs
            // 
            resources.ApplyResources(this.btnMinimizeAs, "btnMinimizeAs");
            this.btnMinimizeAs.Name = "btnMinimizeAs";
            this.btnMinimizeAs.UseVisualStyleBackColor = true;
            this.btnMinimizeAs.Click += new System.EventHandler(this.btnMinimizeAs_Click);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.cbxLimitNoiseTime);
            this.flowLayoutPanel1.Controls.Add(this.tbxNoiseTimeRange);
            this.flowLayoutPanel1.Controls.Add(this.label3);
            resources.ApplyResources(this.flowLayoutPanel1, "flowLayoutPanel1");
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            // 
            // MinimizeResultsDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
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
            this.Load += new System.EventHandler(this.OnLoad);
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
