namespace pwiz.Topograph.ui.Forms
{
    partial class TracerChromatogramForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.cbxAutoFindPeak = new System.Windows.Forms.CheckBox();
            this.gridViewTracerPercents = new System.Windows.Forms.DataGridView();
            this.colTracer = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTracerPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cbxShowScore = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbxScore = new System.Windows.Forms.TextBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colFormula = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAreaPct = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSlopePct = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colArea = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStartTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colEndTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCorr = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxTracerPercentByAreas = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxTracerPercentBySlopes = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.comboAdjustPeaks = new System.Windows.Forms.ComboBox();
            this.btnAdjustPeaks = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxRetentionTimeShift = new System.Windows.Forms.TextBox();
            this.cbxPeaksAsVerticalLines = new System.Windows.Forms.CheckBox();
            this.cbxPeaksAsHorizontalLines = new System.Windows.Forms.CheckBox();
            this.cbxSmooth = new System.Windows.Forms.CheckBox();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTracerPercents)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.panel1.SuspendLayout();
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
            this.splitContainer1.Size = new System.Drawing.Size(861, 458);
            this.splitContainer1.SplitterDistance = 379;
            this.splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.cbxAutoFindPeak, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.gridViewTracerPercents, 0, 8);
            this.tableLayoutPanel1.Controls.Add(this.cbxShowScore, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.tbxScore, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.dataGridView1, 0, 9);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxTracerPercentByAreas, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbxTracerPercentBySlopes, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 7);
            this.tableLayoutPanel1.Controls.Add(this.panel1, 1, 7);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.tbxRetentionTimeShift, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.cbxPeaksAsVerticalLines, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.cbxPeaksAsHorizontalLines, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.cbxSmooth, 1, 6);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 10;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(379, 458);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // cbxAutoFindPeak
            // 
            this.cbxAutoFindPeak.AutoSize = true;
            this.cbxAutoFindPeak.Location = new System.Drawing.Point(3, 3);
            this.cbxAutoFindPeak.Name = "cbxAutoFindPeak";
            this.cbxAutoFindPeak.Size = new System.Drawing.Size(95, 17);
            this.cbxAutoFindPeak.TabIndex = 0;
            this.cbxAutoFindPeak.Text = "&Auto find peak";
            this.cbxAutoFindPeak.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.cbxAutoFindPeak.UseVisualStyleBackColor = true;
            this.cbxAutoFindPeak.CheckedChanged += new System.EventHandler(this.cbxAutoFindPeak_CheckedChanged);
            // 
            // gridViewTracerPercents
            // 
            this.gridViewTracerPercents.AllowUserToAddRows = false;
            this.gridViewTracerPercents.AllowUserToDeleteRows = false;
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle7.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle7.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle7.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle7.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewTracerPercents.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle7;
            this.gridViewTracerPercents.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewTracerPercents.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTracer,
            this.colTracerPercent});
            this.tableLayoutPanel1.SetColumnSpan(this.gridViewTracerPercents, 2);
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle8.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle8.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle8.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewTracerPercents.DefaultCellStyle = dataGridViewCellStyle8;
            this.gridViewTracerPercents.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridViewTracerPercents.Location = new System.Drawing.Point(3, 203);
            this.gridViewTracerPercents.Name = "gridViewTracerPercents";
            this.gridViewTracerPercents.ReadOnly = true;
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle9.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle9.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle9.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle9.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle9.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle9.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewTracerPercents.RowHeadersDefaultCellStyle = dataGridViewCellStyle9;
            this.gridViewTracerPercents.Size = new System.Drawing.Size(373, 94);
            this.gridViewTracerPercents.TabIndex = 14;
            // 
            // colTracer
            // 
            this.colTracer.HeaderText = "Tracer";
            this.colTracer.Name = "colTracer";
            this.colTracer.ReadOnly = true;
            // 
            // colTracerPercent
            // 
            this.colTracerPercent.HeaderText = "Percent";
            this.colTracerPercent.Name = "colTracerPercent";
            this.colTracerPercent.ReadOnly = true;
            // 
            // cbxShowScore
            // 
            this.cbxShowScore.AutoSize = true;
            this.cbxShowScore.Location = new System.Drawing.Point(192, 3);
            this.cbxShowScore.Name = "cbxShowScore";
            this.cbxShowScore.Size = new System.Drawing.Size(84, 17);
            this.cbxShowScore.TabIndex = 1;
            this.cbxShowScore.Text = "Show S&core";
            this.cbxShowScore.UseVisualStyleBackColor = true;
            this.cbxShowScore.CheckedChanged += new System.EventHandler(this.cbxShowScore_CheckedChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 100);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(183, 25);
            this.label3.TabIndex = 8;
            this.label3.Text = "Deconvo&lution Score";
            // 
            // tbxScore
            // 
            this.tbxScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxScore.Location = new System.Drawing.Point(192, 103);
            this.tbxScore.Name = "tbxScore";
            this.tbxScore.ReadOnly = true;
            this.tbxScore.Size = new System.Drawing.Size(184, 20);
            this.tbxScore.TabIndex = 9;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            dataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle10.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle10.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle10.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle10.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle10.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle10.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle10;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colFormula,
            this.colAreaPct,
            this.colSlopePct,
            this.colArea,
            this.colStartTime,
            this.colEndTime,
            this.colCorr});
            this.tableLayoutPanel1.SetColumnSpan(this.dataGridView1, 2);
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle11.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle11.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle11.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle11.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle11.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle11.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle11;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(3, 303);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            dataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle12.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle12.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle12.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle12.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle12.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle12.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle12;
            this.dataGridView1.Size = new System.Drawing.Size(373, 152);
            this.dataGridView1.TabIndex = 15;
            this.dataGridView1.SelectionChanged += new System.EventHandler(this.dataGridView1_SelectionChanged);
            // 
            // colFormula
            // 
            this.colFormula.HeaderText = "Formula";
            this.colFormula.Name = "colFormula";
            this.colFormula.ReadOnly = true;
            // 
            // colAreaPct
            // 
            this.colAreaPct.HeaderText = "Area %";
            this.colAreaPct.Name = "colAreaPct";
            this.colAreaPct.ReadOnly = true;
            // 
            // colSlopePct
            // 
            this.colSlopePct.HeaderText = "Slope %";
            this.colSlopePct.Name = "colSlopePct";
            this.colSlopePct.ReadOnly = true;
            // 
            // colArea
            // 
            this.colArea.HeaderText = "Area";
            this.colArea.Name = "colArea";
            this.colArea.ReadOnly = true;
            // 
            // colStartTime
            // 
            this.colStartTime.HeaderText = "Start";
            this.colStartTime.Name = "colStartTime";
            this.colStartTime.ReadOnly = true;
            // 
            // colEndTime
            // 
            this.colEndTime.HeaderText = "End";
            this.colEndTime.Name = "colEndTime";
            this.colEndTime.ReadOnly = true;
            // 
            // colCorr
            // 
            this.colCorr.HeaderText = "Corr";
            this.colCorr.Name = "colCorr";
            this.colCorr.ReadOnly = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 50);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(123, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Tracer Percent By Areas";
            // 
            // tbxTracerPercentByAreas
            // 
            this.tbxTracerPercentByAreas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxTracerPercentByAreas.Location = new System.Drawing.Point(192, 53);
            this.tbxTracerPercentByAreas.Name = "tbxTracerPercentByAreas";
            this.tbxTracerPercentByAreas.ReadOnly = true;
            this.tbxTracerPercentByAreas.Size = new System.Drawing.Size(184, 20);
            this.tbxTracerPercentByAreas.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 75);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(128, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Tracer Percent By Slopes";
            // 
            // tbxTracerPercentBySlopes
            // 
            this.tbxTracerPercentBySlopes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxTracerPercentBySlopes.Location = new System.Drawing.Point(192, 78);
            this.tbxTracerPercentBySlopes.Name = "tbxTracerPercentBySlopes";
            this.tbxTracerPercentBySlopes.ReadOnly = true;
            this.tbxTracerPercentBySlopes.Size = new System.Drawing.Size(184, 20);
            this.tbxTracerPercentBySlopes.TabIndex = 7;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 175);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(68, 13);
            this.label4.TabIndex = 13;
            this.label4.Text = "Adjust peaks";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.comboAdjustPeaks);
            this.panel1.Controls.Add(this.btnAdjustPeaks);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(192, 178);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(184, 19);
            this.panel1.TabIndex = 16;
            // 
            // comboAdjustPeaks
            // 
            this.comboAdjustPeaks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboAdjustPeaks.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAdjustPeaks.FormattingEnabled = true;
            this.comboAdjustPeaks.Items.AddRange(new object[] {
            "Full",
            "Overlapping",
            "Narrow"});
            this.comboAdjustPeaks.Location = new System.Drawing.Point(0, 0);
            this.comboAdjustPeaks.Name = "comboAdjustPeaks";
            this.comboAdjustPeaks.Size = new System.Drawing.Size(153, 21);
            this.comboAdjustPeaks.TabIndex = 0;
            // 
            // btnAdjustPeaks
            // 
            this.btnAdjustPeaks.AutoSize = true;
            this.btnAdjustPeaks.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnAdjustPeaks.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnAdjustPeaks.Location = new System.Drawing.Point(153, 0);
            this.btnAdjustPeaks.Name = "btnAdjustPeaks";
            this.btnAdjustPeaks.Size = new System.Drawing.Size(31, 19);
            this.btnAdjustPeaks.TabIndex = 1;
            this.btnAdjustPeaks.Text = "Go";
            this.btnAdjustPeaks.UseVisualStyleBackColor = true;
            this.btnAdjustPeaks.Click += new System.EventHandler(this.btnAdjustPeaks_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 125);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(103, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "Retention Time Shift";
            // 
            // tbxRetentionTimeShift
            // 
            this.tbxRetentionTimeShift.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxRetentionTimeShift.Location = new System.Drawing.Point(192, 128);
            this.tbxRetentionTimeShift.Name = "tbxRetentionTimeShift";
            this.tbxRetentionTimeShift.ReadOnly = true;
            this.tbxRetentionTimeShift.Size = new System.Drawing.Size(184, 20);
            this.tbxRetentionTimeShift.TabIndex = 11;
            // 
            // cbxPeaksAsVerticalLines
            // 
            this.cbxPeaksAsVerticalLines.AutoSize = true;
            this.cbxPeaksAsVerticalLines.Location = new System.Drawing.Point(3, 28);
            this.cbxPeaksAsVerticalLines.Name = "cbxPeaksAsVerticalLines";
            this.cbxPeaksAsVerticalLines.Size = new System.Drawing.Size(137, 17);
            this.cbxPeaksAsVerticalLines.TabIndex = 2;
            this.cbxPeaksAsVerticalLines.Text = "Peaks As &Vertical Lines";
            this.cbxPeaksAsVerticalLines.UseVisualStyleBackColor = true;
            this.cbxPeaksAsVerticalLines.CheckedChanged += new System.EventHandler(this.cbxPeaksAsVerticalLines_CheckedChanged);
            // 
            // cbxPeaksAsHorizontalLines
            // 
            this.cbxPeaksAsHorizontalLines.AutoSize = true;
            this.cbxPeaksAsHorizontalLines.Checked = true;
            this.cbxPeaksAsHorizontalLines.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxPeaksAsHorizontalLines.Location = new System.Drawing.Point(192, 28);
            this.cbxPeaksAsHorizontalLines.Name = "cbxPeaksAsHorizontalLines";
            this.cbxPeaksAsHorizontalLines.Size = new System.Drawing.Size(149, 17);
            this.cbxPeaksAsHorizontalLines.TabIndex = 3;
            this.cbxPeaksAsHorizontalLines.Text = "Peaks As &Horizontal Lines";
            this.cbxPeaksAsHorizontalLines.UseVisualStyleBackColor = true;
            this.cbxPeaksAsHorizontalLines.CheckedChanged += new System.EventHandler(this.cbxPeaksAsHorizontalLines_CheckedChanged);
            // 
            // cbxSmooth
            // 
            this.cbxSmooth.AutoSize = true;
            this.cbxSmooth.Location = new System.Drawing.Point(192, 153);
            this.cbxSmooth.Name = "cbxSmooth";
            this.cbxSmooth.Size = new System.Drawing.Size(62, 17);
            this.cbxSmooth.TabIndex = 12;
            this.cbxSmooth.Text = "S&mooth";
            this.cbxSmooth.UseVisualStyleBackColor = true;
            // 
            // TracerChromatogramForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(861, 458);
            this.Controls.Add(this.splitContainer1);
            this.Name = "TracerChromatogramForm";
            this.TabText = "TracerChromatogramForm";
            this.Text = "Tracer Chromatograms";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTracerPercents)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.CheckBox cbxAutoFindPeak;
        private System.Windows.Forms.CheckBox cbxShowScore;
        private System.Windows.Forms.DataGridView gridViewTracerPercents;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbxScore;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxTracerPercentByAreas;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxTracerPercentBySlopes;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnAdjustPeaks;
        private System.Windows.Forms.ComboBox comboAdjustPeaks;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFormula;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAreaPct;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSlopePct;
        private System.Windows.Forms.DataGridViewTextBoxColumn colArea;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStartTime;
        private System.Windows.Forms.DataGridViewTextBoxColumn colEndTime;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCorr;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracer;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracerPercent;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxRetentionTimeShift;
        private System.Windows.Forms.CheckBox cbxPeaksAsVerticalLines;
        private System.Windows.Forms.CheckBox cbxPeaksAsHorizontalLines;
        private System.Windows.Forms.CheckBox cbxSmooth;
    }
}