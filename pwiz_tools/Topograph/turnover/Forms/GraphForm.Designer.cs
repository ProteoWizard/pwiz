namespace pwiz.Topograph.ui.Forms
{
    partial class GraphForm
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
            this.comboGraph = new System.Windows.Forms.ComboBox();
            this.cbxByCohort = new System.Windows.Forms.CheckBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colCohort = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHalfLife = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colInitialTurnover = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colScore = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
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
            this.splitContainer1.Size = new System.Drawing.Size(682, 342);
            this.splitContainer1.SplitterDistance = 412;
            this.splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.comboGraph, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.cbxByCohort, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.dataGridView1, 0, 3);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(412, 342);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(200, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "Graph";
            // 
            // comboGraph
            // 
            this.comboGraph.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboGraph.FormattingEnabled = true;
            this.comboGraph.Location = new System.Drawing.Point(209, 3);
            this.comboGraph.Name = "comboGraph";
            this.comboGraph.Size = new System.Drawing.Size(108, 21);
            this.comboGraph.TabIndex = 1;
            this.comboGraph.SelectedIndexChanged += new System.EventHandler(this.comboGraph_SelectedIndexChanged);
            // 
            // cbxByCohort
            // 
            this.cbxByCohort.AutoSize = true;
            this.cbxByCohort.Checked = true;
            this.cbxByCohort.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxByCohort.Location = new System.Drawing.Point(3, 28);
            this.cbxByCohort.Name = "cbxByCohort";
            this.cbxByCohort.Size = new System.Drawing.Size(72, 17);
            this.cbxByCohort.TabIndex = 2;
            this.cbxByCohort.Text = "By Cohort";
            this.cbxByCohort.UseVisualStyleBackColor = true;
            this.cbxByCohort.CheckedChanged += new System.EventHandler(this.cbxByCohort_CheckedChanged);
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colCohort,
            this.colHalfLife,
            this.colInitialTurnover,
            this.colScore});
            this.tableLayoutPanel1.SetColumnSpan(this.dataGridView1, 2);
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(3, 78);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.Size = new System.Drawing.Size(406, 261);
            this.dataGridView1.TabIndex = 3;
            // 
            // colCohort
            // 
            this.colCohort.HeaderText = "Cohort";
            this.colCohort.Name = "colCohort";
            this.colCohort.ReadOnly = true;
            // 
            // colHalfLife
            // 
            this.colHalfLife.HeaderText = "Half Life";
            this.colHalfLife.Name = "colHalfLife";
            this.colHalfLife.ReadOnly = true;
            // 
            // colInitialTurnover
            // 
            this.colInitialTurnover.HeaderText = "Initial Turnover";
            this.colInitialTurnover.Name = "colInitialTurnover";
            this.colInitialTurnover.ReadOnly = true;
            // 
            // colScore
            // 
            this.colScore.HeaderText = "Score";
            this.colScore.Name = "colScore";
            this.colScore.ReadOnly = true;
            // 
            // GraphForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(682, 342);
            this.Controls.Add(this.splitContainer1);
            this.Name = "GraphForm";
            this.TabText = "GraphForm";
            this.Text = "GraphForm";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboGraph;
        private System.Windows.Forms.CheckBox cbxByCohort;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCohort;
        private System.Windows.Forms.DataGridViewTextBoxColumn colHalfLife;
        private System.Windows.Forms.DataGridViewTextBoxColumn colInitialTurnover;
        private System.Windows.Forms.DataGridViewTextBoxColumn colScore;
    }
}