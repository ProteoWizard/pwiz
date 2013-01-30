using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    partial class DataFileSummary
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.btnCreateFileAnalyses = new System.Windows.Forms.Button();
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.colStatus = new pwiz.Topograph.ui.Controls.ValidationStatusColumn();
            this.colSequence = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colPeakStart = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPeakEnd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTurnover = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colApe = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.recordNavBar1 = new pwiz.Common.Controls.RecordNavBar();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tableLayoutPanel1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.dataGridView);
            this.splitContainer1.Panel2.Controls.Add(this.recordNavBar1);
            this.splitContainer1.Size = new System.Drawing.Size(602, 264);
            this.splitContainer1.SplitterDistance = 200;
            this.splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.btnCreateFileAnalyses, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(200, 264);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // btnCreateFileAnalyses
            // 
            this.btnCreateFileAnalyses.Location = new System.Drawing.Point(3, 3);
            this.btnCreateFileAnalyses.Name = "btnCreateFileAnalyses";
            this.btnCreateFileAnalyses.Size = new System.Drawing.Size(94, 19);
            this.btnCreateFileAnalyses.TabIndex = 0;
            this.btnCreateFileAnalyses.Text = "Create Analyses";
            this.btnCreateFileAnalyses.UseVisualStyleBackColor = true;
            this.btnCreateFileAnalyses.Click += new System.EventHandler(this.BtnCreateFileAnalysesOnClick);
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colStatus,
            this.colSequence,
            this.colPeakStart,
            this.colPeakEnd,
            this.colTurnover,
            this.colApe});
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.Location = new System.Drawing.Point(0, 21);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.Size = new System.Drawing.Size(398, 243);
            this.dataGridView.TabIndex = 0;
            this.dataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewOnCellEndEdit);
            this.dataGridView.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewOnCellContentClick);
            // 
            // colStatus
            // 
            this.colStatus.DisplayMember = "Display";
            this.colStatus.HeaderText = "Status";
            this.colStatus.Name = "colStatus";
            this.colStatus.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.colStatus.ValueMember = "Value";
            // 
            // colSequence
            // 
            this.colSequence.HeaderText = "Sequence";
            this.colSequence.Name = "colSequence";
            this.colSequence.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colSequence.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colPeakStart
            // 
            this.colPeakStart.HeaderText = "Peak Start";
            this.colPeakStart.Name = "colPeakStart";
            this.colPeakStart.ReadOnly = true;
            // 
            // colPeakEnd
            // 
            this.colPeakEnd.HeaderText = "Peak End";
            this.colPeakEnd.Name = "colPeakEnd";
            this.colPeakEnd.ReadOnly = true;
            // 
            // colTurnover
            // 
            this.colTurnover.HeaderText = "Turnover";
            this.colTurnover.Name = "colTurnover";
            // 
            // colApe
            // 
            this.colApe.HeaderText = "APE";
            this.colApe.Name = "colApe";
            // 
            // recordNavBar1
            // 
            this.recordNavBar1.DataGridView = this.dataGridView;
            this.recordNavBar1.Dock = System.Windows.Forms.DockStyle.Top;
            this.recordNavBar1.Location = new System.Drawing.Point(0, 0);
            this.recordNavBar1.Name = "recordNavBar1";
            this.recordNavBar1.Size = new System.Drawing.Size(398, 21);
            this.recordNavBar1.TabIndex = 1;
            // 
            // DataFileSummary
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(602, 264);
            this.Controls.Add(this.splitContainer1);
            this.Name = "DataFileSummary";
            this.TabText = "DataFileSummary";
            this.Text = "DataFileSummary";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.Button btnCreateFileAnalyses;
        private ValidationStatusColumn colStatus;
        private System.Windows.Forms.DataGridViewLinkColumn colSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeakStart;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeakEnd;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTurnover;
        private System.Windows.Forms.DataGridViewTextBoxColumn colApe;
        private pwiz.Common.Controls.RecordNavBar recordNavBar1;
    }
}