namespace pwiz.Topograph.ui.Forms
{
    partial class ResultsPerReplicateForm
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.checkedListBoxColumns = new System.Windows.Forms.CheckedListBox();
            this.btnRequery = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.findBox1 = new pwiz.Common.Controls.FindBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colPeptide = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colDataFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colArea = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTracerPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDeconvolutionScore = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCohort = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTimePoint = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSample = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIndPrecursorEnrichment = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIndTurnover = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIndTurnoverScore = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAvgPrecursorEnrichment = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAvgTurnover = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAvgTurnoverScore = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new pwiz.Topograph.ui.Controls.ValidationStatusColumn();
            this.label1 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tableLayoutPanel1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.dataGridView1);
            this.splitContainer1.Size = new System.Drawing.Size(736, 515);
            this.splitContainer1.SplitterDistance = 227;
            this.splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.Controls.Add(this.checkedListBoxColumns, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnRequery, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 2, 3);
            this.tableLayoutPanel1.Controls.Add(this.findBox1, 3, 3);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.btnSave, 3, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(736, 227);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // checkedListBoxColumns
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.checkedListBoxColumns, 2);
            this.checkedListBoxColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.checkedListBoxColumns.FormattingEnabled = true;
            this.checkedListBoxColumns.Location = new System.Drawing.Point(3, 53);
            this.checkedListBoxColumns.Name = "checkedListBoxColumns";
            this.checkedListBoxColumns.Size = new System.Drawing.Size(362, 139);
            this.checkedListBoxColumns.TabIndex = 0;
            // 
            // btnRequery
            // 
            this.btnRequery.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnRequery.Location = new System.Drawing.Point(552, 0);
            this.btnRequery.Margin = new System.Windows.Forms.Padding(0);
            this.btnRequery.Name = "btnRequery";
            this.btnRequery.Size = new System.Drawing.Size(184, 25);
            this.btnRequery.TabIndex = 1;
            this.btnRequery.Text = "Requery";
            this.btnRequery.UseVisualStyleBackColor = true;
            this.btnRequery.Click += new System.EventHandler(this.btnRequery_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(371, 202);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(27, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Find";
            // 
            // findBox1
            // 
            this.findBox1.DataGridView = this.dataGridView1;
            this.findBox1.Location = new System.Drawing.Point(555, 205);
            this.findBox1.Name = "findBox1";
            this.findBox1.Size = new System.Drawing.Size(178, 19);
            this.findBox1.TabIndex = 4;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptide,
            this.colDataFile,
            this.colArea,
            this.colTracerPercent,
            this.colDeconvolutionScore,
            this.colCohort,
            this.colTimePoint,
            this.colSample,
            this.colIndPrecursorEnrichment,
            this.colIndTurnover,
            this.colIndTurnoverScore,
            this.colProteinName,
            this.colProteinDescription,
            this.colAvgPrecursorEnrichment,
            this.colAvgTurnover,
            this.colAvgTurnoverScore,
            this.colStatus});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.Name = "dataGridView1";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridView1.Size = new System.Drawing.Size(736, 284);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.RowHeaderMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView1_RowHeaderMouseDoubleClick);
            this.dataGridView1.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellContentClick);
            // 
            // colPeptide
            // 
            this.colPeptide.HeaderText = "Peptide";
            this.colPeptide.Name = "colPeptide";
            this.colPeptide.ReadOnly = true;
            this.colPeptide.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colPeptide.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colDataFile
            // 
            this.colDataFile.HeaderText = "Data File";
            this.colDataFile.Name = "colDataFile";
            this.colDataFile.ReadOnly = true;
            // 
            // colArea
            // 
            this.colArea.HeaderText = "Area";
            this.colArea.Name = "colArea";
            this.colArea.ReadOnly = true;
            // 
            // colTracerPercent
            // 
            this.colTracerPercent.HeaderText = "Tracer Percent";
            this.colTracerPercent.Name = "colTracerPercent";
            this.colTracerPercent.ReadOnly = true;
            // 
            // colDeconvolutionScore
            // 
            this.colDeconvolutionScore.HeaderText = "Deconvolution Score";
            this.colDeconvolutionScore.Name = "colDeconvolutionScore";
            this.colDeconvolutionScore.ReadOnly = true;
            // 
            // colCohort
            // 
            this.colCohort.HeaderText = "Cohort";
            this.colCohort.Name = "colCohort";
            this.colCohort.ReadOnly = true;
            // 
            // colTimePoint
            // 
            this.colTimePoint.HeaderText = "Time Point";
            this.colTimePoint.Name = "colTimePoint";
            this.colTimePoint.ReadOnly = true;
            // 
            // colSample
            // 
            this.colSample.HeaderText = "Sample";
            this.colSample.Name = "colSample";
            this.colSample.ReadOnly = true;
            // 
            // colIndPrecursorEnrichment
            // 
            this.colIndPrecursorEnrichment.HeaderText = "Ind Precursor Enrichment";
            this.colIndPrecursorEnrichment.Name = "colIndPrecursorEnrichment";
            this.colIndPrecursorEnrichment.ReadOnly = true;
            // 
            // colIndTurnover
            // 
            this.colIndTurnover.HeaderText = "Ind Turnover";
            this.colIndTurnover.Name = "colIndTurnover";
            this.colIndTurnover.ReadOnly = true;
            // 
            // colIndTurnoverScore
            // 
            this.colIndTurnoverScore.HeaderText = "Ind Turnover Score";
            this.colIndTurnoverScore.Name = "colIndTurnoverScore";
            this.colIndTurnoverScore.ReadOnly = true;
            // 
            // colProteinName
            // 
            this.colProteinName.HeaderText = "Protein";
            this.colProteinName.Name = "colProteinName";
            this.colProteinName.ReadOnly = true;
            // 
            // colProteinDescription
            // 
            this.colProteinDescription.HeaderText = "Protein Description";
            this.colProteinDescription.Name = "colProteinDescription";
            this.colProteinDescription.ReadOnly = true;
            // 
            // colAvgPrecursorEnrichment
            // 
            this.colAvgPrecursorEnrichment.HeaderText = "Avg Precursor Enrichment";
            this.colAvgPrecursorEnrichment.Name = "colAvgPrecursorEnrichment";
            this.colAvgPrecursorEnrichment.ReadOnly = true;
            // 
            // colAvgTurnover
            // 
            this.colAvgTurnover.HeaderText = "Avg Turnover";
            this.colAvgTurnover.Name = "colAvgTurnover";
            this.colAvgTurnover.ReadOnly = true;
            // 
            // colAvgTurnoverScore
            // 
            this.colAvgTurnoverScore.HeaderText = "Avg Turnover Score";
            this.colAvgTurnoverScore.Name = "colAvgTurnoverScore";
            this.colAvgTurnoverScore.ReadOnly = true;
            // 
            // colStatus
            // 
            this.colStatus.DisplayMember = "Display";
            this.colStatus.HeaderText = "Status";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            this.colStatus.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.colStatus.ValueMember = "Value";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Show Columns:";
            // 
            // btnSave
            // 
            this.btnSave.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSave.Enabled = false;
            this.btnSave.Location = new System.Drawing.Point(552, 25);
            this.btnSave.Margin = new System.Windows.Forms.Padding(0);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(184, 25);
            this.btnSave.TabIndex = 6;
            this.btnSave.Text = "Save...";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // ResultsPerReplicateForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(736, 515);
            this.Controls.Add(this.splitContainer1);
            this.Name = "ResultsPerReplicateForm";
            this.TabText = "ResultsPerReplicate";
            this.Text = "ResultsPerReplicate";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.CheckedListBox checkedListBoxColumns;
        private System.Windows.Forms.Button btnRequery;
        private System.Windows.Forms.Label label2;
        private pwiz.Common.Controls.FindBox findBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.DataGridViewLinkColumn colPeptide;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDataFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn colArea;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracerPercent;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDeconvolutionScore;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCohort;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTimePoint;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSample;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIndPrecursorEnrichment;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIndTurnover;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIndTurnoverScore;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAvgPrecursorEnrichment;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAvgTurnover;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAvgTurnoverScore;
        private pwiz.Topograph.ui.Controls.ValidationStatusColumn colStatus;
    }
}