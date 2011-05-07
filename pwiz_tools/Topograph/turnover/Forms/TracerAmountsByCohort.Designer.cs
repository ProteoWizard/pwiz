namespace pwiz.Topograph.ui.Forms
{
    partial class TracerAmountsByCohort
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.cbxByProtein = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxMinScore = new System.Windows.Forms.TextBox();
            this.cbxShowTurnover = new System.Windows.Forms.CheckBox();
            this.cbxShowCount = new System.Windows.Forms.CheckBox();
            this.cbxTracerPercentSlope = new System.Windows.Forms.CheckBox();
            this.cbxTracerPercentAreas = new System.Windows.Forms.CheckBox();
            this.cbxShowPrecursorEnrichment = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.cbxGroupByCohort = new System.Windows.Forms.CheckBox();
            this.cbxGroupByTimePoint = new System.Windows.Forms.CheckBox();
            this.cbxGroupBySample = new System.Windows.Forms.CheckBox();
            this.cbxShowStdDev = new System.Windows.Forms.CheckBox();
            this.cbxShowStdErr = new System.Windows.Forms.CheckBox();
            this.btnRequery = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.cbxAreaUnderCurve = new System.Windows.Forms.CheckBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colPeptide = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colProteinKey = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colProteinDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dataGridViewSummary = new System.Windows.Forms.DataGridView();
            this.colSummaryQuantity = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryAvgValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryMedianValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryMeanStdDev = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryMeanStdErr = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryValueCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryStdDevStdErrCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.recordNavBar1 = new pwiz.Common.Controls.RecordNavBar();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSummary)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.Controls.Add(this.cbxByProtein, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinScore, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.cbxShowTurnover, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.cbxShowCount, 2, 5);
            this.tableLayoutPanel1.Controls.Add(this.cbxTracerPercentSlope, 3, 4);
            this.tableLayoutPanel1.Controls.Add(this.cbxTracerPercentAreas, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.cbxShowPrecursorEnrichment, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.cbxGroupByCohort, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.cbxGroupByTimePoint, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.cbxGroupBySample, 3, 2);
            this.tableLayoutPanel1.Controls.Add(this.cbxShowStdDev, 2, 4);
            this.tableLayoutPanel1.Controls.Add(this.cbxShowStdErr, 4, 4);
            this.tableLayoutPanel1.Controls.Add(this.btnRequery, 4, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnSave, 4, 3);
            this.tableLayoutPanel1.Controls.Add(this.cbxAreaUnderCurve, 3, 5);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 6;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(754, 157);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // cbxByProtein
            // 
            this.cbxByProtein.AutoSize = true;
            this.cbxByProtein.Location = new System.Drawing.Point(103, 3);
            this.cbxByProtein.Name = "cbxByProtein";
            this.cbxByProtein.Size = new System.Drawing.Size(74, 17);
            this.cbxByProtein.TabIndex = 0;
            this.cbxByProtein.Text = "By Protein";
            this.cbxByProtein.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(94, 25);
            this.label1.TabIndex = 2;
            this.label1.Text = "Minimum Score:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // tbxMinScore
            // 
            this.tbxMinScore.Location = new System.Drawing.Point(103, 28);
            this.tbxMinScore.Name = "tbxMinScore";
            this.tbxMinScore.Size = new System.Drawing.Size(100, 20);
            this.tbxMinScore.TabIndex = 3;
            this.tbxMinScore.Text = "0";
            // 
            // cbxShowTurnover
            // 
            this.cbxShowTurnover.AutoSize = true;
            this.cbxShowTurnover.Location = new System.Drawing.Point(3, 128);
            this.cbxShowTurnover.Name = "cbxShowTurnover";
            this.cbxShowTurnover.Size = new System.Drawing.Size(69, 17);
            this.cbxShowTurnover.TabIndex = 9;
            this.cbxShowTurnover.Text = "Turnover";
            this.cbxShowTurnover.UseVisualStyleBackColor = true;
            this.cbxShowTurnover.CheckedChanged += new System.EventHandler(this.cbx_ColumnVisibilityChanged);
            // 
            // cbxShowCount
            // 
            this.cbxShowCount.AutoSize = true;
            this.cbxShowCount.Location = new System.Drawing.Point(330, 128);
            this.cbxShowCount.Name = "cbxShowCount";
            this.cbxShowCount.Size = new System.Drawing.Size(54, 17);
            this.cbxShowCount.TabIndex = 8;
            this.cbxShowCount.Text = "Count";
            this.cbxShowCount.UseVisualStyleBackColor = true;
            this.cbxShowCount.CheckedChanged += new System.EventHandler(this.cbx_ColumnVisibilityChanged);
            // 
            // cbxTracerPercentSlope
            // 
            this.cbxTracerPercentSlope.AutoSize = true;
            this.cbxTracerPercentSlope.Checked = true;
            this.cbxTracerPercentSlope.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxTracerPercentSlope.Location = new System.Drawing.Point(430, 103);
            this.cbxTracerPercentSlope.Name = "cbxTracerPercentSlope";
            this.cbxTracerPercentSlope.Size = new System.Drawing.Size(102, 17);
            this.cbxTracerPercentSlope.TabIndex = 7;
            this.cbxTracerPercentSlope.Text = "Tracer % (slope)";
            this.cbxTracerPercentSlope.UseVisualStyleBackColor = true;
            this.cbxTracerPercentSlope.CheckedChanged += new System.EventHandler(this.cbx_ColumnVisibilityChanged);
            // 
            // cbxTracerPercentAreas
            // 
            this.cbxTracerPercentAreas.AutoSize = true;
            this.cbxTracerPercentAreas.Checked = true;
            this.cbxTracerPercentAreas.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxTracerPercentAreas.Location = new System.Drawing.Point(103, 103);
            this.cbxTracerPercentAreas.Name = "cbxTracerPercentAreas";
            this.cbxTracerPercentAreas.Size = new System.Drawing.Size(103, 17);
            this.cbxTracerPercentAreas.TabIndex = 6;
            this.cbxTracerPercentAreas.Text = "Tracer % (areas)";
            this.cbxTracerPercentAreas.UseVisualStyleBackColor = true;
            this.cbxTracerPercentAreas.CheckedChanged += new System.EventHandler(this.cbx_ColumnVisibilityChanged);
            // 
            // cbxShowPrecursorEnrichment
            // 
            this.cbxShowPrecursorEnrichment.AutoSize = true;
            this.cbxShowPrecursorEnrichment.Location = new System.Drawing.Point(103, 128);
            this.cbxShowPrecursorEnrichment.Name = "cbxShowPrecursorEnrichment";
            this.cbxShowPrecursorEnrichment.Size = new System.Drawing.Size(127, 17);
            this.cbxShowPrecursorEnrichment.TabIndex = 11;
            this.cbxShowPrecursorEnrichment.Text = "Precursor Enrichment";
            this.cbxShowPrecursorEnrichment.UseVisualStyleBackColor = true;
            this.cbxShowPrecursorEnrichment.CheckedChanged += new System.EventHandler(this.cbx_ColumnVisibilityChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 75);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(80, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "Show Columns:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 50);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(51, 13);
            this.label4.TabIndex = 12;
            this.label4.Text = "Group By";
            // 
            // cbxGroupByCohort
            // 
            this.cbxGroupByCohort.AutoSize = true;
            this.cbxGroupByCohort.Checked = true;
            this.cbxGroupByCohort.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxGroupByCohort.Location = new System.Drawing.Point(330, 53);
            this.cbxGroupByCohort.Name = "cbxGroupByCohort";
            this.cbxGroupByCohort.Size = new System.Drawing.Size(57, 17);
            this.cbxGroupByCohort.TabIndex = 13;
            this.cbxGroupByCohort.Text = "Cohort";
            this.cbxGroupByCohort.UseVisualStyleBackColor = true;
            // 
            // cbxGroupByTimePoint
            // 
            this.cbxGroupByTimePoint.AutoSize = true;
            this.cbxGroupByTimePoint.Checked = true;
            this.cbxGroupByTimePoint.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxGroupByTimePoint.Location = new System.Drawing.Point(103, 53);
            this.cbxGroupByTimePoint.Name = "cbxGroupByTimePoint";
            this.cbxGroupByTimePoint.Size = new System.Drawing.Size(76, 17);
            this.cbxGroupByTimePoint.TabIndex = 14;
            this.cbxGroupByTimePoint.Text = "Time Point";
            this.cbxGroupByTimePoint.UseVisualStyleBackColor = true;
            // 
            // cbxGroupBySample
            // 
            this.cbxGroupBySample.AutoSize = true;
            this.cbxGroupBySample.Checked = true;
            this.cbxGroupBySample.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxGroupBySample.Location = new System.Drawing.Point(430, 53);
            this.cbxGroupBySample.Name = "cbxGroupBySample";
            this.cbxGroupBySample.Size = new System.Drawing.Size(61, 17);
            this.cbxGroupBySample.TabIndex = 15;
            this.cbxGroupBySample.Text = "Sample";
            this.cbxGroupBySample.UseVisualStyleBackColor = true;
            // 
            // cbxShowStdDev
            // 
            this.cbxShowStdDev.AutoSize = true;
            this.cbxShowStdDev.Location = new System.Drawing.Point(330, 103);
            this.cbxShowStdDev.Name = "cbxShowStdDev";
            this.cbxShowStdDev.Size = new System.Drawing.Size(62, 17);
            this.cbxShowStdDev.TabIndex = 16;
            this.cbxShowStdDev.Text = "StdDev";
            this.cbxShowStdDev.UseVisualStyleBackColor = true;
            this.cbxShowStdDev.CheckedChanged += new System.EventHandler(this.cbx_ColumnVisibilityChanged);
            // 
            // cbxShowStdErr
            // 
            this.cbxShowStdErr.AutoSize = true;
            this.cbxShowStdErr.Location = new System.Drawing.Point(657, 103);
            this.cbxShowStdErr.Name = "cbxShowStdErr";
            this.cbxShowStdErr.Size = new System.Drawing.Size(55, 17);
            this.cbxShowStdErr.TabIndex = 17;
            this.cbxShowStdErr.Text = "StdErr";
            this.cbxShowStdErr.UseVisualStyleBackColor = true;
            this.cbxShowStdErr.CheckedChanged += new System.EventHandler(this.cbx_ColumnVisibilityChanged);
            // 
            // btnRequery
            // 
            this.btnRequery.Location = new System.Drawing.Point(657, 53);
            this.btnRequery.Name = "btnRequery";
            this.btnRequery.Size = new System.Drawing.Size(75, 19);
            this.btnRequery.TabIndex = 1;
            this.btnRequery.Text = "Requery";
            this.btnRequery.UseVisualStyleBackColor = true;
            this.btnRequery.Click += new System.EventHandler(this.btnRequery_Click);
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(657, 78);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 19);
            this.btnSave.TabIndex = 18;
            this.btnSave.Text = "Save...";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // cbxAreaUnderCurve
            // 
            this.cbxAreaUnderCurve.AutoSize = true;
            this.cbxAreaUnderCurve.Location = new System.Drawing.Point(430, 128);
            this.cbxAreaUnderCurve.Name = "cbxAreaUnderCurve";
            this.cbxAreaUnderCurve.Size = new System.Drawing.Size(111, 17);
            this.cbxAreaUnderCurve.TabIndex = 19;
            this.cbxAreaUnderCurve.Text = "Area Under Curve";
            this.cbxAreaUnderCurve.UseVisualStyleBackColor = true;
            this.cbxAreaUnderCurve.CheckedChanged += new System.EventHandler(this.cbx_ColumnVisibilityChanged);
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
            this.colProteinKey,
            this.colProteinDescription,
            this.colProteinName});
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
            this.dataGridView1.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridView1.Size = new System.Drawing.Size(754, 173);
            this.dataGridView1.TabIndex = 1;
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
            // colProteinKey
            // 
            this.colProteinKey.HeaderText = "Protein";
            this.colProteinKey.Name = "colProteinKey";
            this.colProteinKey.ReadOnly = true;
            this.colProteinKey.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colProteinKey.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colProteinDescription
            // 
            this.colProteinDescription.HeaderText = "Protein Description";
            this.colProteinDescription.Name = "colProteinDescription";
            this.colProteinDescription.ReadOnly = true;
            // 
            // colProteinName
            // 
            this.colProteinName.HeaderText = "Protein Name";
            this.colProteinName.Name = "colProteinName";
            this.colProteinName.ReadOnly = true;
            this.colProteinName.Visible = false;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 157);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dataGridViewSummary);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.recordNavBar1);
            this.splitContainer1.Panel2.Controls.Add(this.dataGridView1);
            this.splitContainer1.Size = new System.Drawing.Size(754, 271);
            this.splitContainer1.SplitterDistance = 94;
            this.splitContainer1.TabIndex = 2;
            // 
            // dataGridViewSummary
            // 
            this.dataGridViewSummary.AllowUserToAddRows = false;
            this.dataGridViewSummary.AllowUserToDeleteRows = false;
            this.dataGridViewSummary.AllowUserToOrderColumns = true;
            this.dataGridViewSummary.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewSummary.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSummaryQuantity,
            this.colSummaryAvgValue,
            this.colSummaryMedianValue,
            this.colSummaryMeanStdDev,
            this.colSummaryMeanStdErr,
            this.colSummaryValueCount,
            this.colSummaryStdDevStdErrCount});
            this.dataGridViewSummary.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewSummary.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewSummary.Name = "dataGridViewSummary";
            this.dataGridViewSummary.ReadOnly = true;
            this.dataGridViewSummary.Size = new System.Drawing.Size(754, 94);
            this.dataGridViewSummary.TabIndex = 0;
            // 
            // colSummaryQuantity
            // 
            this.colSummaryQuantity.HeaderText = "Quantity";
            this.colSummaryQuantity.Name = "colSummaryQuantity";
            this.colSummaryQuantity.ReadOnly = true;
            // 
            // colSummaryAvgValue
            // 
            this.colSummaryAvgValue.HeaderText = "Mean Value";
            this.colSummaryAvgValue.Name = "colSummaryAvgValue";
            this.colSummaryAvgValue.ReadOnly = true;
            // 
            // colSummaryMedianValue
            // 
            this.colSummaryMedianValue.HeaderText = "Median Value";
            this.colSummaryMedianValue.Name = "colSummaryMedianValue";
            this.colSummaryMedianValue.ReadOnly = true;
            // 
            // colSummaryMeanStdDev
            // 
            this.colSummaryMeanStdDev.HeaderText = "Mean StdDev";
            this.colSummaryMeanStdDev.Name = "colSummaryMeanStdDev";
            this.colSummaryMeanStdDev.ReadOnly = true;
            // 
            // colSummaryMeanStdErr
            // 
            this.colSummaryMeanStdErr.HeaderText = "Mean StdErr";
            this.colSummaryMeanStdErr.Name = "colSummaryMeanStdErr";
            this.colSummaryMeanStdErr.ReadOnly = true;
            // 
            // colSummaryValueCount
            // 
            this.colSummaryValueCount.HeaderText = "Value Count";
            this.colSummaryValueCount.Name = "colSummaryValueCount";
            this.colSummaryValueCount.ReadOnly = true;
            // 
            // colSummaryStdDevStdErrCount
            // 
            this.colSummaryStdDevStdErrCount.HeaderText = "StdDev/StdErr Count";
            this.colSummaryStdDevStdErrCount.Name = "colSummaryStdDevStdErrCount";
            this.colSummaryStdDevStdErrCount.ReadOnly = true;
            // 
            // recordNavBar1
            // 
            this.recordNavBar1.DataGridView = this.dataGridView1;
            this.recordNavBar1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.recordNavBar1.Location = new System.Drawing.Point(0, 152);
            this.recordNavBar1.Name = "recordNavBar1";
            this.recordNavBar1.Size = new System.Drawing.Size(754, 21);
            this.recordNavBar1.TabIndex = 2;
            // 
            // TracerAmountsByCohort
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(754, 428);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "TracerAmountsByCohort";
            this.TabText = "TracerAmountsByCohort";
            this.Text = "TracerAmountsByCohort";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSummary)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.CheckBox cbxByProtein;
        private System.Windows.Forms.Button btnRequery;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxMinScore;
        private System.Windows.Forms.DataGridViewLinkColumn colPeptide;
        private System.Windows.Forms.DataGridViewLinkColumn colProteinKey;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinName;
        private System.Windows.Forms.CheckBox cbxTracerPercentAreas;
        private System.Windows.Forms.CheckBox cbxTracerPercentSlope;
        private System.Windows.Forms.CheckBox cbxShowCount;
        private System.Windows.Forms.CheckBox cbxShowTurnover;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox cbxShowPrecursorEnrichment;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox cbxGroupByCohort;
        private System.Windows.Forms.CheckBox cbxGroupByTimePoint;
        private System.Windows.Forms.CheckBox cbxGroupBySample;
        private System.Windows.Forms.CheckBox cbxShowStdDev;
        private System.Windows.Forms.CheckBox cbxShowStdErr;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dataGridViewSummary;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryQuantity;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryAvgValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryMedianValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryMeanStdDev;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryMeanStdErr;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryValueCount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryStdDevStdErrCount;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.CheckBox cbxAreaUnderCurve;
        private pwiz.Common.Controls.RecordNavBar recordNavBar1;
    }
}