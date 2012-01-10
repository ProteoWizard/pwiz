using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    partial class HalfLifeForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxPeptide = new System.Windows.Forms.TextBox();
            this.tbxProtein = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboCohort = new System.Windows.Forms.ComboBox();
            this.tbxProteinDescription = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxMinScore = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.tbxInitialPercent = new System.Windows.Forms.TextBox();
            this.tbxFinalPercent = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.tbxRateConstant = new System.Windows.Forms.TextBox();
            this.tbxHalfLife = new System.Windows.Forms.TextBox();
            this.cbxLogPlot = new System.Windows.Forms.CheckBox();
            this.comboCalculationType = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.gridViewStats = new System.Windows.Forms.DataGridView();
            this.colStatsTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatsInclude = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colStatsMean = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatsMedian = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatsStdDev = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cbxFixedInitialPercent = new System.Windows.Forms.CheckBox();
            this.cbxBySample = new System.Windows.Forms.CheckBox();
            this.label11 = new System.Windows.Forms.Label();
            this.comboEvviesFilter = new System.Windows.Forms.ComboBox();
            this.tbxMinAuc = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colPeptide = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTimePoint = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new pwiz.Topograph.ui.Controls.ValidationStatusColumn();
            this.colCohort = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTracerPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSample = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colScore = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAuc = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPrecursorPool = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTurnover = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTurnoverScore = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPrecursorPoolAvg = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTurnoverAvg = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTurnoverScoreAvg = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.validationStatusColumn1 = new pwiz.Topograph.ui.Controls.ValidationStatusColumn();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tableLayoutPanel1);
            this.splitContainer1.Size = new System.Drawing.Size(948, 395);
            this.splitContainer1.SplitterDistance = 455;
            this.splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxPeptide, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxProtein, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.comboCohort, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbxProteinDescription, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinScore, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.label6, 0, 9);
            this.tableLayoutPanel1.Controls.Add(this.label7, 0, 10);
            this.tableLayoutPanel1.Controls.Add(this.tbxInitialPercent, 1, 9);
            this.tableLayoutPanel1.Controls.Add(this.tbxFinalPercent, 1, 10);
            this.tableLayoutPanel1.Controls.Add(this.label8, 0, 11);
            this.tableLayoutPanel1.Controls.Add(this.label9, 0, 12);
            this.tableLayoutPanel1.Controls.Add(this.tbxRateConstant, 1, 11);
            this.tableLayoutPanel1.Controls.Add(this.tbxHalfLife, 1, 12);
            this.tableLayoutPanel1.Controls.Add(this.cbxLogPlot, 0, 7);
            this.tableLayoutPanel1.Controls.Add(this.comboCalculationType, 1, 8);
            this.tableLayoutPanel1.Controls.Add(this.label10, 0, 8);
            this.tableLayoutPanel1.Controls.Add(this.gridViewStats, 0, 14);
            this.tableLayoutPanel1.Controls.Add(this.cbxFixedInitialPercent, 1, 13);
            this.tableLayoutPanel1.Controls.Add(this.cbxBySample, 0, 13);
            this.tableLayoutPanel1.Controls.Add(this.label11, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.comboEvviesFilter, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinAuc, 1, 6);
            this.tableLayoutPanel1.Controls.Add(this.label12, 0, 6);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 15;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(455, 395);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(144, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "Peptide";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(144, 25);
            this.label2.TabIndex = 2;
            this.label2.Text = "Protein Name";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxPeptide
            // 
            this.tbxPeptide.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxPeptide.Location = new System.Drawing.Point(153, 3);
            this.tbxPeptide.Name = "tbxPeptide";
            this.tbxPeptide.ReadOnly = true;
            this.tbxPeptide.Size = new System.Drawing.Size(299, 20);
            this.tbxPeptide.TabIndex = 1;
            // 
            // tbxProtein
            // 
            this.tbxProtein.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxProtein.Location = new System.Drawing.Point(153, 28);
            this.tbxProtein.Name = "tbxProtein";
            this.tbxProtein.ReadOnly = true;
            this.tbxProtein.Size = new System.Drawing.Size(299, 20);
            this.tbxProtein.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 75);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(144, 25);
            this.label3.TabIndex = 6;
            this.label3.Text = "Cohort";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // comboCohort
            // 
            this.comboCohort.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboCohort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCohort.FormattingEnabled = true;
            this.comboCohort.Location = new System.Drawing.Point(153, 78);
            this.comboCohort.Name = "comboCohort";
            this.comboCohort.Size = new System.Drawing.Size(299, 21);
            this.comboCohort.TabIndex = 7;
            this.comboCohort.SelectedIndexChanged += new System.EventHandler(this.comboCohort_SelectedIndexChanged);
            // 
            // tbxProteinDescription
            // 
            this.tbxProteinDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxProteinDescription.Location = new System.Drawing.Point(153, 53);
            this.tbxProteinDescription.Name = "tbxProteinDescription";
            this.tbxProteinDescription.ReadOnly = true;
            this.tbxProteinDescription.Size = new System.Drawing.Size(299, 20);
            this.tbxProteinDescription.TabIndex = 5;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(3, 50);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(144, 25);
            this.label4.TabIndex = 4;
            this.label4.Text = "Protein Description";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label5.Location = new System.Drawing.Point(3, 100);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(144, 25);
            this.label5.TabIndex = 8;
            this.label5.Text = "Minimum Score";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMinScore
            // 
            this.tbxMinScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinScore.Location = new System.Drawing.Point(153, 103);
            this.tbxMinScore.Name = "tbxMinScore";
            this.tbxMinScore.Size = new System.Drawing.Size(299, 20);
            this.tbxMinScore.TabIndex = 9;
            this.tbxMinScore.TextChanged += new System.EventHandler(this.tbxMinScore_TextChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label6.Location = new System.Drawing.Point(3, 225);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(144, 25);
            this.label6.TabIndex = 13;
            this.label6.Text = "Initial Percent";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label7.Location = new System.Drawing.Point(3, 250);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(144, 25);
            this.label7.TabIndex = 15;
            this.label7.Text = "Final Percent";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxInitialPercent
            // 
            this.tbxInitialPercent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxInitialPercent.Location = new System.Drawing.Point(153, 228);
            this.tbxInitialPercent.Name = "tbxInitialPercent";
            this.tbxInitialPercent.Size = new System.Drawing.Size(299, 20);
            this.tbxInitialPercent.TabIndex = 14;
            this.tbxInitialPercent.TextChanged += new System.EventHandler(this.tbxInitialPercent_TextChanged);
            // 
            // tbxFinalPercent
            // 
            this.tbxFinalPercent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxFinalPercent.Location = new System.Drawing.Point(153, 253);
            this.tbxFinalPercent.Name = "tbxFinalPercent";
            this.tbxFinalPercent.Size = new System.Drawing.Size(299, 20);
            this.tbxFinalPercent.TabIndex = 16;
            this.tbxFinalPercent.TextChanged += new System.EventHandler(this.tbxFinalPercent_TextChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(3, 275);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(75, 13);
            this.label8.TabIndex = 17;
            this.label8.Text = "Rate Constant";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(3, 300);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(46, 13);
            this.label9.TabIndex = 19;
            this.label9.Text = "Half Life";
            // 
            // tbxRateConstant
            // 
            this.tbxRateConstant.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxRateConstant.Location = new System.Drawing.Point(153, 278);
            this.tbxRateConstant.Name = "tbxRateConstant";
            this.tbxRateConstant.ReadOnly = true;
            this.tbxRateConstant.Size = new System.Drawing.Size(299, 20);
            this.tbxRateConstant.TabIndex = 18;
            // 
            // tbxHalfLife
            // 
            this.tbxHalfLife.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxHalfLife.Location = new System.Drawing.Point(153, 303);
            this.tbxHalfLife.Name = "tbxHalfLife";
            this.tbxHalfLife.ReadOnly = true;
            this.tbxHalfLife.Size = new System.Drawing.Size(299, 20);
            this.tbxHalfLife.TabIndex = 20;
            // 
            // cbxLogPlot
            // 
            this.cbxLogPlot.AutoSize = true;
            this.cbxLogPlot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbxLogPlot.Location = new System.Drawing.Point(3, 178);
            this.cbxLogPlot.Name = "cbxLogPlot";
            this.cbxLogPlot.Size = new System.Drawing.Size(144, 19);
            this.cbxLogPlot.TabIndex = 10;
            this.cbxLogPlot.Text = "Log Plot";
            this.cbxLogPlot.UseVisualStyleBackColor = true;
            this.cbxLogPlot.CheckedChanged += new System.EventHandler(this.cbxLogPlot_CheckedChanged);
            // 
            // comboCalculationType
            // 
            this.comboCalculationType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboCalculationType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCalculationType.FormattingEnabled = true;
            this.comboCalculationType.Items.AddRange(new object[] {
            "Tracer %",
            "Individual Precursor Pool",
            "Avg Precursor Pool",
            "Avg Precursor Pool (Old Way)"});
            this.comboCalculationType.Location = new System.Drawing.Point(153, 203);
            this.comboCalculationType.Name = "comboCalculationType";
            this.comboCalculationType.Size = new System.Drawing.Size(299, 21);
            this.comboCalculationType.TabIndex = 12;
            this.comboCalculationType.SelectedIndexChanged += new System.EventHandler(this.comboCalculationType_SelectedIndexChanged);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label10.Location = new System.Drawing.Point(3, 200);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(144, 25);
            this.label10.TabIndex = 11;
            this.label10.Text = "Calculation Type";
            this.label10.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // gridViewStats
            // 
            this.gridViewStats.AllowUserToAddRows = false;
            this.gridViewStats.AllowUserToDeleteRows = false;
            this.gridViewStats.AllowUserToOrderColumns = true;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewStats.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridViewStats.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewStats.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colStatsTime,
            this.colStatsInclude,
            this.colStatsMean,
            this.colStatsMedian,
            this.colStatsStdDev});
            this.tableLayoutPanel1.SetColumnSpan(this.gridViewStats, 2);
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewStats.DefaultCellStyle = dataGridViewCellStyle2;
            this.gridViewStats.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridViewStats.Location = new System.Drawing.Point(3, 353);
            this.gridViewStats.Name = "gridViewStats";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewStats.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.gridViewStats.Size = new System.Drawing.Size(449, 39);
            this.gridViewStats.TabIndex = 22;
            this.gridViewStats.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridViewStats_CellValueChanged);
            this.gridViewStats.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.gridViewStats_CellBeginEdit);
            this.gridViewStats.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridViewStats_CellEndEdit);
            // 
            // colStatsTime
            // 
            this.colStatsTime.HeaderText = "Time";
            this.colStatsTime.Name = "colStatsTime";
            this.colStatsTime.ReadOnly = true;
            this.colStatsTime.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colStatsTime.Width = 60;
            // 
            // colStatsInclude
            // 
            this.colStatsInclude.HeaderText = "Include";
            this.colStatsInclude.Name = "colStatsInclude";
            this.colStatsInclude.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colStatsInclude.Width = 50;
            // 
            // colStatsMean
            // 
            this.colStatsMean.HeaderText = "Mean";
            this.colStatsMean.Name = "colStatsMean";
            this.colStatsMean.ReadOnly = true;
            this.colStatsMean.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colStatsMean.Width = 90;
            // 
            // colStatsMedian
            // 
            this.colStatsMedian.HeaderText = "Median";
            this.colStatsMedian.Name = "colStatsMedian";
            this.colStatsMedian.ReadOnly = true;
            this.colStatsMedian.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colStatsMedian.Width = 90;
            // 
            // colStatsStdDev
            // 
            this.colStatsStdDev.HeaderText = "StdDev";
            this.colStatsStdDev.Name = "colStatsStdDev";
            this.colStatsStdDev.ReadOnly = true;
            this.colStatsStdDev.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colStatsStdDev.Width = 90;
            // 
            // cbxFixedInitialPercent
            // 
            this.cbxFixedInitialPercent.AutoSize = true;
            this.cbxFixedInitialPercent.Location = new System.Drawing.Point(153, 328);
            this.cbxFixedInitialPercent.Name = "cbxFixedInitialPercent";
            this.cbxFixedInitialPercent.Size = new System.Drawing.Size(160, 17);
            this.cbxFixedInitialPercent.TabIndex = 21;
            this.cbxFixedInitialPercent.Text = "Hold Initial Percent Constant";
            this.cbxFixedInitialPercent.UseVisualStyleBackColor = true;
            this.cbxFixedInitialPercent.CheckedChanged += new System.EventHandler(this.cbxFixedInitialPercent_CheckedChanged);
            // 
            // cbxBySample
            // 
            this.cbxBySample.AutoSize = true;
            this.cbxBySample.Location = new System.Drawing.Point(3, 328);
            this.cbxBySample.Name = "cbxBySample";
            this.cbxBySample.Size = new System.Drawing.Size(76, 17);
            this.cbxBySample.TabIndex = 24;
            this.cbxBySample.Text = "By Sample";
            this.cbxBySample.UseVisualStyleBackColor = true;
            this.cbxBySample.CheckedChanged += new System.EventHandler(this.cbxBySample_CheckedChanged);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label11.Location = new System.Drawing.Point(3, 125);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(144, 25);
            this.label11.TabIndex = 25;
            this.label11.Text = "Apply Evvie\'s Filter";
            this.label11.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // comboEvviesFilter
            // 
            this.comboEvviesFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboEvviesFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEvviesFilter.FormattingEnabled = true;
            this.comboEvviesFilter.Location = new System.Drawing.Point(153, 128);
            this.comboEvviesFilter.Name = "comboEvviesFilter";
            this.comboEvviesFilter.Size = new System.Drawing.Size(299, 21);
            this.comboEvviesFilter.TabIndex = 26;
            this.comboEvviesFilter.SelectedIndexChanged += new System.EventHandler(this.comboEvviesFilter_SelectedIndexChanged);
            // 
            // tbxMinAuc
            // 
            this.tbxMinAuc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinAuc.Location = new System.Drawing.Point(153, 153);
            this.tbxMinAuc.Name = "tbxMinAuc";
            this.tbxMinAuc.Size = new System.Drawing.Size(299, 20);
            this.tbxMinAuc.TabIndex = 27;
            this.tbxMinAuc.TextChanged += new System.EventHandler(this.tbxMinAuc_TextChanged);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label12.Location = new System.Drawing.Point(3, 150);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(144, 25);
            this.label12.TabIndex = 28;
            this.label12.Text = "Minimum Area Under Curve";
            this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptide,
            this.colFile,
            this.colTimePoint,
            this.colStatus,
            this.colCohort,
            this.colTracerPercent,
            this.colSample,
            this.colScore,
            this.colAuc,
            this.colPrecursorPool,
            this.colTurnover,
            this.colTurnoverScore,
            this.colPrecursorPoolAvg,
            this.colTurnoverAvg,
            this.colTurnoverScoreAvg});
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle5;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.Name = "dataGridView1";
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle6;
            this.dataGridView1.Size = new System.Drawing.Size(948, 123);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellEndEdit);
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
            // colFile
            // 
            this.colFile.HeaderText = "File";
            this.colFile.Name = "colFile";
            this.colFile.ReadOnly = true;
            // 
            // colTimePoint
            // 
            this.colTimePoint.HeaderText = "Time Point";
            this.colTimePoint.Name = "colTimePoint";
            this.colTimePoint.ReadOnly = true;
            // 
            // colStatus
            // 
            this.colStatus.DisplayMember = "Display";
            this.colStatus.HeaderText = "Status";
            this.colStatus.Name = "colStatus";
            this.colStatus.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.colStatus.ValueMember = "Value";
            // 
            // colCohort
            // 
            this.colCohort.HeaderText = "Cohort";
            this.colCohort.Name = "colCohort";
            this.colCohort.ReadOnly = true;
            // 
            // colTracerPercent
            // 
            this.colTracerPercent.HeaderText = "Tracer %";
            this.colTracerPercent.Name = "colTracerPercent";
            this.colTracerPercent.ReadOnly = true;
            // 
            // colSample
            // 
            this.colSample.HeaderText = "Sample";
            this.colSample.Name = "colSample";
            this.colSample.ReadOnly = true;
            this.colSample.Visible = false;
            // 
            // colScore
            // 
            this.colScore.HeaderText = "Score";
            this.colScore.Name = "colScore";
            this.colScore.ReadOnly = true;
            // 
            // colAuc
            // 
            this.colAuc.HeaderText = "AUC";
            this.colAuc.Name = "colAuc";
            this.colAuc.ReadOnly = true;
            // 
            // colPrecursorPool
            // 
            this.colPrecursorPool.HeaderText = "Precursor Pool (Ind)";
            this.colPrecursorPool.Name = "colPrecursorPool";
            this.colPrecursorPool.ReadOnly = true;
            this.colPrecursorPool.Width = 140;
            // 
            // colTurnover
            // 
            this.colTurnover.HeaderText = "Turnover (Ind)";
            this.colTurnover.Name = "colTurnover";
            this.colTurnover.ReadOnly = true;
            // 
            // colTurnoverScore
            // 
            this.colTurnoverScore.HeaderText = "Turnover Score (Ind)";
            this.colTurnoverScore.Name = "colTurnoverScore";
            this.colTurnoverScore.ReadOnly = true;
            // 
            // colPrecursorPoolAvg
            // 
            this.colPrecursorPoolAvg.HeaderText = "Precursor Pool (Avg)";
            this.colPrecursorPoolAvg.Name = "colPrecursorPoolAvg";
            this.colPrecursorPoolAvg.ReadOnly = true;
            this.colPrecursorPoolAvg.Width = 140;
            // 
            // colTurnoverAvg
            // 
            this.colTurnoverAvg.HeaderText = "Turnover (Avg)";
            this.colTurnoverAvg.Name = "colTurnoverAvg";
            this.colTurnoverAvg.ReadOnly = true;
            this.colTurnoverAvg.Width = 105;
            // 
            // colTurnoverScoreAvg
            // 
            this.colTurnoverScoreAvg.HeaderText = "Turnover Score (Avg)";
            this.colTurnoverScoreAvg.Name = "colTurnoverScoreAvg";
            this.colTurnoverScoreAvg.ReadOnly = true;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.splitContainer1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.dataGridView1);
            this.splitContainer2.Size = new System.Drawing.Size(948, 522);
            this.splitContainer2.SplitterDistance = 395;
            this.splitContainer2.TabIndex = 1;
            // 
            // validationStatusColumn1
            // 
            this.validationStatusColumn1.DisplayMember = "Display";
            this.validationStatusColumn1.HeaderText = "Status";
            this.validationStatusColumn1.Name = "validationStatusColumn1";
            this.validationStatusColumn1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.validationStatusColumn1.ValueMember = "Value";
            // 
            // HalfLifeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(948, 522);
            this.Controls.Add(this.splitContainer2);
            this.Name = "HalfLifeForm";
            this.TabText = "HalfLifeForm";
            this.Text = "HalfLifeForm";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbxPeptide;
        private System.Windows.Forms.TextBox tbxProtein;
        private System.Windows.Forms.ComboBox comboCohort;
        private System.Windows.Forms.TextBox tbxProteinDescription;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxMinScore;
        private System.Windows.Forms.CheckBox cbxLogPlot;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox tbxInitialPercent;
        private System.Windows.Forms.TextBox tbxFinalPercent;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox tbxRateConstant;
        private System.Windows.Forms.TextBox tbxHalfLife;
        private System.Windows.Forms.CheckBox cbxFixedInitialPercent;
        private System.Windows.Forms.ComboBox comboCalculationType;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.DataGridView gridViewStats;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatsTime;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colStatsInclude;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatsMean;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatsMedian;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatsStdDev;
        private System.Windows.Forms.CheckBox cbxBySample;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.ComboBox comboEvviesFilter;
        private ValidationStatusColumn validationStatusColumn1;
        private System.Windows.Forms.DataGridViewLinkColumn colPeptide;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTimePoint;
        private ValidationStatusColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCohort;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracerPercent;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSample;
        private System.Windows.Forms.DataGridViewTextBoxColumn colScore;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAuc;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPrecursorPool;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTurnover;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTurnoverScore;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPrecursorPoolAvg;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTurnoverAvg;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTurnoverScoreAvg;
        private System.Windows.Forms.TextBox tbxMinAuc;
        private System.Windows.Forms.Label label12;
    }
}