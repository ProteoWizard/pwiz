namespace pwiz.Topograph.ui.Forms
{
    partial class AlignmentForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AlignmentForm));
            this.label1 = new System.Windows.Forms.Label();
            this.comboTarget = new System.Windows.Forms.ComboBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.colDataFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRefinedSlope = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRefinedIntercept = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRefinedPointCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRawSlope = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRawIntercept = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTotalPointCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRawR = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.zedGraphControl = new pwiz.Topograph.ui.Controls.ZedGraphControlEx();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.panelStatus = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.progressBarStatus = new System.Windows.Forms.ProgressBar();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource)).BeginInit();
            this.panel1.SuspendLayout();
            this.panelStatus.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(1, 104);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(182, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Show retention times aligned against:";
            // 
            // comboTarget
            // 
            this.comboTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTarget.FormattingEnabled = true;
            this.comboTarget.Location = new System.Drawing.Point(223, 104);
            this.comboTarget.Name = "comboTarget";
            this.comboTarget.Size = new System.Drawing.Size(225, 21);
            this.comboTarget.TabIndex = 2;
            this.comboTarget.SelectedIndexChanged += new System.EventHandler(this.ComboTargetOnSelectedIndexChanged);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 131);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dataGridView);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.zedGraphControl);
            this.splitContainer1.Size = new System.Drawing.Size(927, 350);
            this.splitContainer1.SplitterDistance = 172;
            this.splitContainer1.TabIndex = 2;
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AutoGenerateColumns = false;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colDataFile,
            this.colRefinedSlope,
            this.colRefinedIntercept,
            this.colRefinedPointCount,
            this.colRawSlope,
            this.colRawIntercept,
            this.colTotalPointCount,
            this.colRawR});
            this.dataGridView.DataSource = this.bindingSource;
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.Size = new System.Drawing.Size(927, 172);
            this.dataGridView.TabIndex = 0;
            // 
            // colDataFile
            // 
            this.colDataFile.DataPropertyName = "DataFile";
            this.colDataFile.HeaderText = "Data File";
            this.colDataFile.Name = "colDataFile";
            this.colDataFile.ReadOnly = true;
            // 
            // colRefinedSlope
            // 
            this.colRefinedSlope.DataPropertyName = "RefinedSlope";
            this.colRefinedSlope.HeaderText = "Slope";
            this.colRefinedSlope.Name = "colRefinedSlope";
            this.colRefinedSlope.ReadOnly = true;
            // 
            // colRefinedIntercept
            // 
            this.colRefinedIntercept.DataPropertyName = "RefinedIntercept";
            this.colRefinedIntercept.HeaderText = "Intercept";
            this.colRefinedIntercept.Name = "colRefinedIntercept";
            this.colRefinedIntercept.ReadOnly = true;
            // 
            // colRefinedPointCount
            // 
            this.colRefinedPointCount.DataPropertyName = "RefinedPointCount";
            this.colRefinedPointCount.HeaderText = "# Points (after excluding outliers)";
            this.colRefinedPointCount.Name = "colRefinedPointCount";
            this.colRefinedPointCount.ReadOnly = true;
            // 
            // colRawSlope
            // 
            this.colRawSlope.DataPropertyName = "RawSlope";
            this.colRawSlope.HeaderText = "Raw Slope";
            this.colRawSlope.Name = "colRawSlope";
            this.colRawSlope.ReadOnly = true;
            // 
            // colRawIntercept
            // 
            this.colRawIntercept.DataPropertyName = "RawIntercept";
            this.colRawIntercept.HeaderText = "Raw Intercept";
            this.colRawIntercept.Name = "colRawIntercept";
            this.colRawIntercept.ReadOnly = true;
            // 
            // colTotalPointCount
            // 
            this.colTotalPointCount.DataPropertyName = "TotalPointCount";
            this.colTotalPointCount.HeaderText = "Total # Points";
            this.colTotalPointCount.Name = "colTotalPointCount";
            this.colTotalPointCount.ReadOnly = true;
            // 
            // colRawR
            // 
            this.colRawR.DataPropertyName = "RawR";
            this.colRawR.HeaderText = "Unrefined Corr Coeff (R)";
            this.colRawR.Name = "colRawR";
            this.colRawR.ReadOnly = true;
            // 
            // bindingSource
            // 
            this.bindingSource.CurrentChanged += new System.EventHandler(this.BindingSourceOnCurrentChanged);
            // 
            // zedGraphControl
            // 
            this.zedGraphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zedGraphControl.Location = new System.Drawing.Point(0, 0);
            this.zedGraphControl.Name = "zedGraphControl";
            this.zedGraphControl.ScrollGrace = 0D;
            this.zedGraphControl.ScrollMaxX = 0D;
            this.zedGraphControl.ScrollMaxY = 0D;
            this.zedGraphControl.ScrollMaxY2 = 0D;
            this.zedGraphControl.ScrollMinX = 0D;
            this.zedGraphControl.ScrollMinY = 0D;
            this.zedGraphControl.ScrollMinY2 = 0D;
            this.zedGraphControl.Size = new System.Drawing.Size(927, 174);
            this.zedGraphControl.TabIndex = 0;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.comboTarget);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(927, 131);
            this.panel1.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoEllipsis = true;
            this.label2.Location = new System.Drawing.Point(3, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(912, 91);
            this.label2.TabIndex = 4;
            this.label2.Text = resources.GetString("label2.Text");
            // 
            // panelStatus
            // 
            this.panelStatus.Controls.Add(this.lblStatus);
            this.panelStatus.Controls.Add(this.progressBarStatus);
            this.panelStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelStatus.Location = new System.Drawing.Point(0, 481);
            this.panelStatus.Name = "panelStatus";
            this.panelStatus.Size = new System.Drawing.Size(927, 38);
            this.panelStatus.TabIndex = 4;
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(12, 22);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(87, 13);
            this.lblStatus.TabIndex = 1;
            this.lblStatus.Text = "Status goes here";
            // 
            // progressBarStatus
            // 
            this.progressBarStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBarStatus.Location = new System.Drawing.Point(169, 12);
            this.progressBarStatus.Name = "progressBarStatus";
            this.progressBarStatus.Size = new System.Drawing.Size(746, 23);
            this.progressBarStatus.TabIndex = 0;
            // 
            // AlignmentForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(927, 519);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.panelStatus);
            this.Controls.Add(this.panel1);
            this.Name = "AlignmentForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "Retention Time Alignment";
            this.Text = "Retention Time Alignment";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panelStatus.ResumeLayout(false);
            this.panelStatus.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboTarget;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.BindingSource bindingSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDataFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRefinedSlope;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRefinedIntercept;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRefinedPointCount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRawSlope;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRawIntercept;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTotalPointCount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRawR;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label2;
        private Controls.ZedGraphControlEx zedGraphControl;
        private System.Windows.Forms.Panel panelStatus;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ProgressBar progressBarStatus;
    }
}