using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    partial class PeptideAnalysisSummary
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxSequence = new System.Windows.Forms.TextBox();
            this.tbxFormula = new System.Windows.Forms.TextBox();
            this.tbxMinCharge = new System.Windows.Forms.TextBox();
            this.tbxMaxCharge = new System.Windows.Forms.TextBox();
            this.gridViewExcludedMzs = new pwiz.Topograph.ui.Controls.ExcludedMzsGrid();
            this.btnCreateAnalyses = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            this.tbxMonoMass = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.tbxAvgMass = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.tbxProtein = new System.Windows.Forms.TextBox();
            this.btnShowGraph = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxMassAccuracy = new System.Windows.Forms.TextBox();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.colStatus = new pwiz.Topograph.ui.Controls.ValidationStatusColumn();
            this.colDataFileLabel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCohort = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTimePoint = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPeakStart = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colPeakEnd = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colTracerPercent = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colScore = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colPrecursorEnrichment = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTurnover = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.zedGraphControl = new pwiz.Topograph.ui.Controls.ZedGraphControlEx();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewExcludedMzs)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
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
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(1245, 412);
            this.splitContainer1.SplitterDistance = 259;
            this.splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 123F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 6);
            this.tableLayoutPanel1.Controls.Add(this.tbxSequence, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxFormula, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinCharge, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.tbxMaxCharge, 1, 6);
            this.tableLayoutPanel1.Controls.Add(this.gridViewExcludedMzs, 0, 10);
            this.tableLayoutPanel1.Controls.Add(this.btnCreateAnalyses, 0, 9);
            this.tableLayoutPanel1.Controls.Add(this.label8, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbxMonoMass, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.label9, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.tbxAvgMass, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.label10, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxProtein, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnShowGraph, 1, 9);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 8);
            this.tableLayoutPanel1.Controls.Add(this.tbxMassAccuracy, 1, 8);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 11;
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
            this.tableLayoutPanel1.Size = new System.Drawing.Size(259, 412);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(117, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "Sequence";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(117, 25);
            this.label2.TabIndex = 1;
            this.label2.Text = "Formula";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 125);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(117, 25);
            this.label3.TabIndex = 2;
            this.label3.Text = "Minimum Charge";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(3, 150);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(117, 25);
            this.label4.TabIndex = 3;
            this.label4.Text = "Maximum Charge";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxSequence
            // 
            this.tbxSequence.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxSequence.Location = new System.Drawing.Point(126, 3);
            this.tbxSequence.Name = "tbxSequence";
            this.tbxSequence.ReadOnly = true;
            this.tbxSequence.Size = new System.Drawing.Size(130, 20);
            this.tbxSequence.TabIndex = 4;
            // 
            // tbxFormula
            // 
            this.tbxFormula.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxFormula.Location = new System.Drawing.Point(126, 28);
            this.tbxFormula.Name = "tbxFormula";
            this.tbxFormula.ReadOnly = true;
            this.tbxFormula.Size = new System.Drawing.Size(130, 20);
            this.tbxFormula.TabIndex = 5;
            // 
            // tbxMinCharge
            // 
            this.tbxMinCharge.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinCharge.Location = new System.Drawing.Point(126, 128);
            this.tbxMinCharge.Name = "tbxMinCharge";
            this.tbxMinCharge.Size = new System.Drawing.Size(130, 20);
            this.tbxMinCharge.TabIndex = 6;
            this.tbxMinCharge.Leave += new System.EventHandler(this.TbxMinChargeOnLeave);
            // 
            // tbxMaxCharge
            // 
            this.tbxMaxCharge.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMaxCharge.Location = new System.Drawing.Point(126, 153);
            this.tbxMaxCharge.Name = "tbxMaxCharge";
            this.tbxMaxCharge.Size = new System.Drawing.Size(130, 20);
            this.tbxMaxCharge.TabIndex = 7;
            this.tbxMaxCharge.TextChanged += new System.EventHandler(this.TbxMaxChargeOnTextChanged);
            // 
            // gridViewExcludedMzs
            // 
            this.gridViewExcludedMzs.AllowUserToAddRows = false;
            this.gridViewExcludedMzs.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewExcludedMzs.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridViewExcludedMzs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.tableLayoutPanel1.SetColumnSpan(this.gridViewExcludedMzs, 2);
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewExcludedMzs.DefaultCellStyle = dataGridViewCellStyle2;
            this.gridViewExcludedMzs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridViewExcludedMzs.Location = new System.Drawing.Point(3, 253);
            this.gridViewExcludedMzs.Name = "gridViewExcludedMzs";
            this.gridViewExcludedMzs.PeptideAnalysis = null;
            this.gridViewExcludedMzs.PeptideFileAnalysis = null;
            this.gridViewExcludedMzs.Size = new System.Drawing.Size(253, 156);
            this.gridViewExcludedMzs.TabIndex = 9;
            // 
            // btnCreateAnalyses
            // 
            this.btnCreateAnalyses.Location = new System.Drawing.Point(3, 228);
            this.btnCreateAnalyses.Name = "btnCreateAnalyses";
            this.btnCreateAnalyses.Size = new System.Drawing.Size(115, 19);
            this.btnCreateAnalyses.TabIndex = 8;
            this.btnCreateAnalyses.Text = "Create File Analyses";
            this.btnCreateAnalyses.UseVisualStyleBackColor = true;
            this.btnCreateAnalyses.Click += new System.EventHandler(this.BtnCreateAnalysesOnClick);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label8.Location = new System.Drawing.Point(3, 75);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(117, 25);
            this.label8.TabIndex = 16;
            this.label8.Text = "Mono Mass";
            this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMonoMass
            // 
            this.tbxMonoMass.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMonoMass.Location = new System.Drawing.Point(126, 78);
            this.tbxMonoMass.Name = "tbxMonoMass";
            this.tbxMonoMass.ReadOnly = true;
            this.tbxMonoMass.Size = new System.Drawing.Size(130, 20);
            this.tbxMonoMass.TabIndex = 17;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label9.Location = new System.Drawing.Point(3, 100);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(117, 25);
            this.label9.TabIndex = 18;
            this.label9.Text = "Average Mass";
            this.label9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxAvgMass
            // 
            this.tbxAvgMass.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxAvgMass.Location = new System.Drawing.Point(126, 103);
            this.tbxAvgMass.Name = "tbxAvgMass";
            this.tbxAvgMass.ReadOnly = true;
            this.tbxAvgMass.Size = new System.Drawing.Size(130, 20);
            this.tbxAvgMass.TabIndex = 19;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label10.Location = new System.Drawing.Point(3, 50);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(117, 25);
            this.label10.TabIndex = 20;
            this.label10.Text = "Protein";
            this.label10.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxProtein
            // 
            this.tbxProtein.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxProtein.Location = new System.Drawing.Point(126, 53);
            this.tbxProtein.Name = "tbxProtein";
            this.tbxProtein.ReadOnly = true;
            this.tbxProtein.Size = new System.Drawing.Size(130, 20);
            this.tbxProtein.TabIndex = 21;
            // 
            // btnShowGraph
            // 
            this.btnShowGraph.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnShowGraph.Location = new System.Drawing.Point(126, 228);
            this.btnShowGraph.Name = "btnShowGraph";
            this.btnShowGraph.Size = new System.Drawing.Size(130, 19);
            this.btnShowGraph.TabIndex = 22;
            this.btnShowGraph.Text = "Half Lives";
            this.btnShowGraph.UseVisualStyleBackColor = true;
            this.btnShowGraph.Click += new System.EventHandler(this.BtnShowGraphOnClick);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label5.Location = new System.Drawing.Point(3, 200);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(117, 25);
            this.label5.TabIndex = 23;
            this.label5.Text = "Mass Accuracy";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMassAccuracy
            // 
            this.tbxMassAccuracy.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMassAccuracy.Location = new System.Drawing.Point(126, 203);
            this.tbxMassAccuracy.Name = "tbxMassAccuracy";
            this.tbxMassAccuracy.Size = new System.Drawing.Size(130, 20);
            this.tbxMassAccuracy.TabIndex = 24;
            this.tbxMassAccuracy.Leave += new System.EventHandler(this.TbxMassAccuracyOnLeave);
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.dataGridView);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.zedGraphControl);
            this.splitContainer2.Size = new System.Drawing.Size(982, 412);
            this.splitContainer2.SplitterDistance = 206;
            this.splitContainer2.TabIndex = 1;
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AllowUserToOrderColumns = true;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colStatus,
            this.colDataFileLabel,
            this.colCohort,
            this.colTimePoint,
            this.colPeakStart,
            this.colPeakEnd,
            this.colTracerPercent,
            this.colScore,
            this.colPrecursorEnrichment,
            this.colTurnover});
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView.DefaultCellStyle = dataGridViewCellStyle4;
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Name = "dataGridView";
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle5;
            this.dataGridView.Size = new System.Drawing.Size(982, 206);
            this.dataGridView.TabIndex = 0;
            this.dataGridView.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewOnCellContentClick);
            this.dataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewOnCellEndEdit);
            this.dataGridView.RowHeaderMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.DataGridViewOnRowHeaderMouseDoubleClick);
            // 
            // colStatus
            // 
            this.colStatus.DisplayMember = "Display";
            this.colStatus.HeaderText = "Status";
            this.colStatus.Name = "colStatus";
            this.colStatus.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.colStatus.ValueMember = "Value";
            // 
            // colDataFileLabel
            // 
            this.colDataFileLabel.HeaderText = "Data File";
            this.colDataFileLabel.Name = "colDataFileLabel";
            // 
            // colCohort
            // 
            this.colCohort.HeaderText = "Cohort";
            this.colCohort.Name = "colCohort";
            // 
            // colTimePoint
            // 
            this.colTimePoint.HeaderText = "Time Point";
            this.colTimePoint.Name = "colTimePoint";
            // 
            // colPeakStart
            // 
            this.colPeakStart.HeaderText = "Peak Start";
            this.colPeakStart.Name = "colPeakStart";
            this.colPeakStart.ReadOnly = true;
            this.colPeakStart.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colPeakStart.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colPeakEnd
            // 
            this.colPeakEnd.HeaderText = "Peak End";
            this.colPeakEnd.Name = "colPeakEnd";
            this.colPeakEnd.ReadOnly = true;
            this.colPeakEnd.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colPeakEnd.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colTracerPercent
            // 
            this.colTracerPercent.HeaderText = "Tracer %";
            this.colTracerPercent.Name = "colTracerPercent";
            this.colTracerPercent.ReadOnly = true;
            this.colTracerPercent.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colTracerPercent.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colScore
            // 
            this.colScore.HeaderText = "Score";
            this.colScore.Name = "colScore";
            this.colScore.ReadOnly = true;
            this.colScore.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colScore.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colPrecursorEnrichment
            // 
            this.colPrecursorEnrichment.HeaderText = "Precursor Enrichment";
            this.colPrecursorEnrichment.Name = "colPrecursorEnrichment";
            this.colPrecursorEnrichment.ReadOnly = true;
            // 
            // colTurnover
            // 
            this.colTurnover.HeaderText = "Turnover";
            this.colTurnover.Name = "colTurnover";
            this.colTurnover.ReadOnly = true;
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
            this.zedGraphControl.Size = new System.Drawing.Size(982, 202);
            this.zedGraphControl.TabIndex = 0;
            this.zedGraphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.ZedGraphControlOnContextMenuBuilder);
            // 
            // PeptideAnalysisSummary
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1245, 412);
            this.Controls.Add(this.splitContainer1);
            this.Name = "PeptideAnalysisSummary";
            this.TabText = "PeptideComparisonForm";
            this.Text = "PeptideComparisonForm";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewExcludedMzs)).EndInit();
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxSequence;
        private System.Windows.Forms.TextBox tbxFormula;
        private System.Windows.Forms.TextBox tbxMinCharge;
        private System.Windows.Forms.TextBox tbxMaxCharge;
        private System.Windows.Forms.Button btnCreateAnalyses;
        private ExcludedMzsGrid gridViewExcludedMzs;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox tbxMonoMass;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox tbxAvgMass;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox tbxProtein;
        private System.Windows.Forms.Button btnShowGraph;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxMassAccuracy;
        private ValidationStatusColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDataFileLabel;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCohort;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTimePoint;
        private System.Windows.Forms.DataGridViewLinkColumn colPeakStart;
        private System.Windows.Forms.DataGridViewLinkColumn colPeakEnd;
        private System.Windows.Forms.DataGridViewLinkColumn colTracerPercent;
        private System.Windows.Forms.DataGridViewLinkColumn colScore;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPrecursorEnrichment;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTurnover;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private ZedGraphControlEx zedGraphControl;
    }
}