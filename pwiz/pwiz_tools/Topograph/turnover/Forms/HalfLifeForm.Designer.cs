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
            this.cbxLogPlot = new System.Windows.Forms.CheckBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.tbxInitialPercent = new System.Windows.Forms.TextBox();
            this.tbxFinalPercent = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.tbxRateConstant = new System.Windows.Forms.TextBox();
            this.tbxHalfLife = new System.Windows.Forms.TextBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colPeptide = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTimePoint = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new pwiz.Topograph.ui.Controls.ValidationStatusColumn();
            this.colCohort = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTracerPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colScore = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.cbxFixedInitialPercent = new System.Windows.Forms.CheckBox();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
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
            this.splitContainer1.Size = new System.Drawing.Size(719, 299);
            this.splitContainer1.SplitterDistance = 284;
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
            this.tableLayoutPanel1.Controls.Add(this.cbxLogPlot, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.label6, 0, 6);
            this.tableLayoutPanel1.Controls.Add(this.label7, 0, 7);
            this.tableLayoutPanel1.Controls.Add(this.tbxInitialPercent, 1, 6);
            this.tableLayoutPanel1.Controls.Add(this.tbxFinalPercent, 1, 7);
            this.tableLayoutPanel1.Controls.Add(this.label8, 0, 8);
            this.tableLayoutPanel1.Controls.Add(this.label9, 0, 9);
            this.tableLayoutPanel1.Controls.Add(this.tbxRateConstant, 1, 8);
            this.tableLayoutPanel1.Controls.Add(this.tbxHalfLife, 1, 9);
            this.tableLayoutPanel1.Controls.Add(this.cbxFixedInitialPercent, 0, 10);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 13;
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
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(284, 296);
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
            this.label2.TabIndex = 1;
            this.label2.Text = "Protein Name";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxPeptide
            // 
            this.tbxPeptide.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxPeptide.Location = new System.Drawing.Point(153, 3);
            this.tbxPeptide.Name = "tbxPeptide";
            this.tbxPeptide.ReadOnly = true;
            this.tbxPeptide.Size = new System.Drawing.Size(128, 20);
            this.tbxPeptide.TabIndex = 3;
            // 
            // tbxProtein
            // 
            this.tbxProtein.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxProtein.Location = new System.Drawing.Point(153, 28);
            this.tbxProtein.Name = "tbxProtein";
            this.tbxProtein.ReadOnly = true;
            this.tbxProtein.Size = new System.Drawing.Size(128, 20);
            this.tbxProtein.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 75);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(144, 25);
            this.label3.TabIndex = 2;
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
            this.comboCohort.Size = new System.Drawing.Size(128, 21);
            this.comboCohort.TabIndex = 5;
            this.comboCohort.SelectedIndexChanged += new System.EventHandler(this.comboCohort_SelectedIndexChanged);
            // 
            // tbxProteinDescription
            // 
            this.tbxProteinDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxProteinDescription.Location = new System.Drawing.Point(153, 53);
            this.tbxProteinDescription.Name = "tbxProteinDescription";
            this.tbxProteinDescription.ReadOnly = true;
            this.tbxProteinDescription.Size = new System.Drawing.Size(128, 20);
            this.tbxProteinDescription.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(3, 50);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(144, 25);
            this.label4.TabIndex = 7;
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
            this.tbxMinScore.Size = new System.Drawing.Size(128, 20);
            this.tbxMinScore.TabIndex = 9;
            this.tbxMinScore.TextChanged += new System.EventHandler(this.tbxMinScore_TextChanged);
            // 
            // cbxLogPlot
            // 
            this.cbxLogPlot.AutoSize = true;
            this.cbxLogPlot.Location = new System.Drawing.Point(3, 128);
            this.cbxLogPlot.Name = "cbxLogPlot";
            this.cbxLogPlot.Size = new System.Drawing.Size(65, 17);
            this.cbxLogPlot.TabIndex = 10;
            this.cbxLogPlot.Text = "Log Plot";
            this.cbxLogPlot.UseVisualStyleBackColor = true;
            this.cbxLogPlot.CheckedChanged += new System.EventHandler(this.cbxLogPlot_CheckedChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label6.Location = new System.Drawing.Point(3, 150);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(144, 25);
            this.label6.TabIndex = 11;
            this.label6.Text = "Initial Percent";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label7.Location = new System.Drawing.Point(3, 175);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(144, 25);
            this.label7.TabIndex = 12;
            this.label7.Text = "Final Percent";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxInitialPercent
            // 
            this.tbxInitialPercent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxInitialPercent.Location = new System.Drawing.Point(153, 153);
            this.tbxInitialPercent.Name = "tbxInitialPercent";
            this.tbxInitialPercent.Size = new System.Drawing.Size(128, 20);
            this.tbxInitialPercent.TabIndex = 13;
            this.tbxInitialPercent.TextChanged += new System.EventHandler(this.tbxInitialPercent_TextChanged);
            // 
            // tbxFinalPercent
            // 
            this.tbxFinalPercent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxFinalPercent.Location = new System.Drawing.Point(153, 178);
            this.tbxFinalPercent.Name = "tbxFinalPercent";
            this.tbxFinalPercent.Size = new System.Drawing.Size(128, 20);
            this.tbxFinalPercent.TabIndex = 14;
            this.tbxFinalPercent.TextChanged += new System.EventHandler(this.tbxFinalPercent_TextChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(3, 200);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(75, 13);
            this.label8.TabIndex = 15;
            this.label8.Text = "Rate Constant";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(3, 225);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(46, 13);
            this.label9.TabIndex = 16;
            this.label9.Text = "Half Life";
            // 
            // tbxRateConstant
            // 
            this.tbxRateConstant.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxRateConstant.Location = new System.Drawing.Point(153, 203);
            this.tbxRateConstant.Name = "tbxRateConstant";
            this.tbxRateConstant.ReadOnly = true;
            this.tbxRateConstant.Size = new System.Drawing.Size(128, 20);
            this.tbxRateConstant.TabIndex = 17;
            // 
            // tbxHalfLife
            // 
            this.tbxHalfLife.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxHalfLife.Location = new System.Drawing.Point(153, 228);
            this.tbxHalfLife.Name = "tbxHalfLife";
            this.tbxHalfLife.ReadOnly = true;
            this.tbxHalfLife.Size = new System.Drawing.Size(128, 20);
            this.tbxHalfLife.TabIndex = 18;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptide,
            this.colFile,
            this.colTimePoint,
            this.colStatus,
            this.colCohort,
            this.colTracerPercent,
            this.colScore});
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(719, 219);
            this.dataGridView1.TabIndex = 1;
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
            // 
            // colTracerPercent
            // 
            this.colTracerPercent.HeaderText = "Tracer %";
            this.colTracerPercent.Name = "colTracerPercent";
            // 
            // colScore
            // 
            this.colScore.HeaderText = "Score";
            this.colScore.Name = "colScore";
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
            this.splitContainer2.Panel1.Controls.Add(this.splitContainer1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.dataGridView1);
            this.splitContainer2.Size = new System.Drawing.Size(719, 522);
            this.splitContainer2.SplitterDistance = 299;
            this.splitContainer2.TabIndex = 1;
            // 
            // cbxFixedInitialPercent
            // 
            this.cbxFixedInitialPercent.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.cbxFixedInitialPercent, 2);
            this.cbxFixedInitialPercent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbxFixedInitialPercent.Location = new System.Drawing.Point(3, 253);
            this.cbxFixedInitialPercent.Name = "cbxFixedInitialPercent";
            this.cbxFixedInitialPercent.Size = new System.Drawing.Size(278, 19);
            this.cbxFixedInitialPercent.TabIndex = 19;
            this.cbxFixedInitialPercent.Text = "Hold Initial Percent Constant";
            this.cbxFixedInitialPercent.UseVisualStyleBackColor = true;
            this.cbxFixedInitialPercent.CheckedChanged += new System.EventHandler(this.cbxFixedInitialPercent_CheckedChanged);
            // 
            // HalfLifeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(719, 522);
            this.Controls.Add(this.splitContainer2);
            this.Name = "HalfLifeForm";
            this.TabText = "HalfLifeForm";
            this.Text = "HalfLifeForm";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
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
        private System.Windows.Forms.DataGridViewLinkColumn colPeptide;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTimePoint;
        private ValidationStatusColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCohort;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracerPercent;
        private System.Windows.Forms.DataGridViewTextBoxColumn colScore;
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
    }
}