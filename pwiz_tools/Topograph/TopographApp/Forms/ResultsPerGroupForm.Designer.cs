using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Internal;

namespace pwiz.Topograph.ui.Forms
{
    partial class ResultsPerGroupForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label4 = new System.Windows.Forms.Label();
            this.cbxGroupByCohort = new System.Windows.Forms.CheckBox();
            this.cbxGroupByTimePoint = new System.Windows.Forms.CheckBox();
            this.cbxGroupBySample = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboEvviesFilter = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbxMinAuc = new System.Windows.Forms.TextBox();
            this.btnRequery = new System.Windows.Forms.Button();
            this.cbxGroupByFile = new System.Windows.Forms.CheckBox();
            this.cbxByProtein = new System.Windows.Forms.CheckBox();
            this.tbxMinScore = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxMinTurnoverScore = new System.Windows.Forms.TextBox();
            this.dataGridView1 = new BoundDataGridView();
            this.bindingSource1 = new BindingListSource(this.components);
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.navBar1 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.dataGridViewSummary = new System.Windows.Forms.DataGridView();
            this.colSummaryQuantity = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryAvgValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryMedianValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryMeanStdDev = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryMeanStdErr = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryValueCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSummaryStdDevStdErrCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSummary)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 122F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 218F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.cbxGroupByCohort, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.cbxGroupByTimePoint, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.cbxGroupBySample, 3, 2);
            this.tableLayoutPanel1.Controls.Add(this.label2, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.comboEvviesFilter, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.label3, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinAuc, 3, 1);
            this.tableLayoutPanel1.Controls.Add(this.btnRequery, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.cbxGroupByFile, 4, 2);
            this.tableLayoutPanel1.Controls.Add(this.cbxByProtein, 4, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinScore, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinTurnoverScore, 1, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(892, 77);
            this.tableLayoutPanel1.TabIndex = 0;
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
            this.cbxGroupByCohort.Location = new System.Drawing.Point(351, 53);
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
            this.cbxGroupByTimePoint.Location = new System.Drawing.Point(125, 53);
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
            this.cbxGroupBySample.Location = new System.Drawing.Point(569, 53);
            this.cbxGroupBySample.Name = "cbxGroupBySample";
            this.cbxGroupBySample.Size = new System.Drawing.Size(61, 17);
            this.cbxGroupBySample.TabIndex = 15;
            this.cbxGroupBySample.Text = "Sample";
            this.cbxGroupBySample.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(351, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(212, 25);
            this.label2.TabIndex = 20;
            this.label2.Text = "Outlier Filter (experimental)";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // comboEvviesFilter
            // 
            this.comboEvviesFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboEvviesFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEvviesFilter.FormattingEnabled = true;
            this.comboEvviesFilter.Location = new System.Drawing.Point(569, 3);
            this.comboEvviesFilter.Name = "comboEvviesFilter";
            this.comboEvviesFilter.Size = new System.Drawing.Size(220, 21);
            this.comboEvviesFilter.TabIndex = 34;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(351, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(212, 25);
            this.label3.TabIndex = 35;
            this.label3.Text = "Minimum AUC";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMinAuc
            // 
            this.tbxMinAuc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinAuc.Location = new System.Drawing.Point(569, 28);
            this.tbxMinAuc.Name = "tbxMinAuc";
            this.tbxMinAuc.Size = new System.Drawing.Size(220, 20);
            this.tbxMinAuc.TabIndex = 36;
            // 
            // btnRequery
            // 
            this.btnRequery.Location = new System.Drawing.Point(795, 3);
            this.btnRequery.Name = "btnRequery";
            this.btnRequery.Size = new System.Drawing.Size(75, 19);
            this.btnRequery.TabIndex = 1;
            this.btnRequery.Text = "Run Query";
            this.btnRequery.UseVisualStyleBackColor = true;
            this.btnRequery.Click += new System.EventHandler(this.btnRequery_Click);
            // 
            // cbxGroupByFile
            // 
            this.cbxGroupByFile.AutoSize = true;
            this.cbxGroupByFile.Location = new System.Drawing.Point(795, 53);
            this.cbxGroupByFile.Name = "cbxGroupByFile";
            this.cbxGroupByFile.Size = new System.Drawing.Size(42, 17);
            this.cbxGroupByFile.TabIndex = 37;
            this.cbxGroupByFile.Text = "File";
            this.cbxGroupByFile.UseVisualStyleBackColor = true;
            this.cbxGroupByFile.CheckedChanged += new System.EventHandler(this.cbxGroupByFile_CheckedChanged);
            // 
            // cbxByProtein
            // 
            this.cbxByProtein.AutoSize = true;
            this.cbxByProtein.Location = new System.Drawing.Point(795, 28);
            this.cbxByProtein.Name = "cbxByProtein";
            this.cbxByProtein.Size = new System.Drawing.Size(74, 17);
            this.cbxByProtein.TabIndex = 0;
            this.cbxByProtein.Text = "By Protein";
            this.cbxByProtein.UseVisualStyleBackColor = true;
            // 
            // tbxMinScore
            // 
            this.tbxMinScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinScore.Location = new System.Drawing.Point(125, 3);
            this.tbxMinScore.Name = "tbxMinScore";
            this.tbxMinScore.Size = new System.Drawing.Size(220, 20);
            this.tbxMinScore.TabIndex = 3;
            this.tbxMinScore.Text = "0";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(116, 25);
            this.label1.TabIndex = 2;
            this.label1.Text = "Minimum Score:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label5.Location = new System.Drawing.Point(3, 25);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(116, 25);
            this.label5.TabIndex = 38;
            this.label5.Text = "Min Turnover Score:";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMinTurnoverScore
            // 
            this.tbxMinTurnoverScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinTurnoverScore.Location = new System.Drawing.Point(125, 28);
            this.tbxMinTurnoverScore.Name = "tbxMinTurnoverScore";
            this.tbxMinTurnoverScore.Size = new System.Drawing.Size(220, 20);
            this.tbxMinTurnoverScore.TabIndex = 39;
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
            this.dataGridView1.DataSource = this.bindingSource1;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 25);
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
            this.dataGridView1.Size = new System.Drawing.Size(892, 199);
            this.dataGridView1.TabIndex = 1;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.Location = new System.Drawing.Point(0, 77);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dataGridView1);
            this.splitContainer1.Panel1.Controls.Add(this.navBar1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.dataGridViewSummary);
            this.splitContainer1.Size = new System.Drawing.Size(892, 351);
            this.splitContainer1.SplitterDistance = 224;
            this.splitContainer1.TabIndex = 2;
            // 
            // navBar1
            // 
            this.navBar1.AutoSize = true;
            this.navBar1.BindingListSource = this.bindingSource1;
            this.navBar1.Dock = System.Windows.Forms.DockStyle.Top;
            this.navBar1.Location = new System.Drawing.Point(0, 0);
            this.navBar1.Name = "navBar1";
            this.navBar1.Size = new System.Drawing.Size(892, 25);
            this.navBar1.TabIndex = 2;
            this.navBar1.WaitingMessage = "Press \"Run Query\" button to see data";
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
            this.dataGridViewSummary.Size = new System.Drawing.Size(892, 123);
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
            this.colSummaryStdDevStdErrCount.Width = 135;
            // 
            // ResultsPerGroupForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(892, 428);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "ResultsPerGroupForm";
            this.TabText = "Results Per Group";
            this.Text = "Results Per Group";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSummary)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private BoundDataGridView dataGridView1;
        private System.Windows.Forms.CheckBox cbxByProtein;
        private System.Windows.Forms.Button btnRequery;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxMinScore;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox cbxGroupByCohort;
        private System.Windows.Forms.CheckBox cbxGroupByTimePoint;
        private System.Windows.Forms.CheckBox cbxGroupBySample;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dataGridViewSummary;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboEvviesFilter;
        private BindingListSource bindingSource1;
        private pwiz.Common.DataBinding.Controls.NavBar navBar1;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryQuantity;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryAvgValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryMedianValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryMeanStdDev;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryMeanStdErr;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryValueCount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSummaryStdDevStdErrCount;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbxMinAuc;
        private System.Windows.Forms.CheckBox cbxGroupByFile;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxMinTurnoverScore;
    }
}