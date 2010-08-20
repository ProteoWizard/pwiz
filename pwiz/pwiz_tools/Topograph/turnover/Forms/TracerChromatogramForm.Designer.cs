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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.cbxAutoFindPeak = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxPrecursorPool = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxTurnover = new System.Windows.Forms.TextBox();
            this.gridViewTracerPercents = new System.Windows.Forms.DataGridView();
            this.colTracer = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTracerPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cbxShowScore = new System.Windows.Forms.CheckBox();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colFormula = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAmount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTracerPercents)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
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
            this.splitContainer1.Size = new System.Drawing.Size(855, 485);
            this.splitContainer1.SplitterDistance = 285;
            this.splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.cbxAutoFindPeak, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxPrecursorPool, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxTurnover, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.gridViewTracerPercents, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.cbxShowScore, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.splitContainer2, 0, 4);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 5;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(285, 485);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // cbxAutoFindPeak
            // 
            this.cbxAutoFindPeak.AutoSize = true;
            this.cbxAutoFindPeak.Location = new System.Drawing.Point(3, 3);
            this.cbxAutoFindPeak.Name = "cbxAutoFindPeak";
            this.cbxAutoFindPeak.Size = new System.Drawing.Size(95, 17);
            this.cbxAutoFindPeak.TabIndex = 0;
            this.cbxAutoFindPeak.Text = "Auto find peak";
            this.cbxAutoFindPeak.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.cbxAutoFindPeak.UseVisualStyleBackColor = true;
            this.cbxAutoFindPeak.CheckedChanged += new System.EventHandler(this.cbxAutoFindPeak_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(136, 25);
            this.label1.TabIndex = 2;
            this.label1.Text = "Precursor Pool";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxPrecursorPool
            // 
            this.tbxPrecursorPool.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxPrecursorPool.Location = new System.Drawing.Point(145, 28);
            this.tbxPrecursorPool.Name = "tbxPrecursorPool";
            this.tbxPrecursorPool.ReadOnly = true;
            this.tbxPrecursorPool.Size = new System.Drawing.Size(137, 20);
            this.tbxPrecursorPool.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 50);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(136, 25);
            this.label2.TabIndex = 4;
            this.label2.Text = "Turnover";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxTurnover
            // 
            this.tbxTurnover.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxTurnover.Location = new System.Drawing.Point(145, 53);
            this.tbxTurnover.Name = "tbxTurnover";
            this.tbxTurnover.ReadOnly = true;
            this.tbxTurnover.Size = new System.Drawing.Size(137, 20);
            this.tbxTurnover.TabIndex = 5;
            // 
            // gridViewTracerPercents
            // 
            this.gridViewTracerPercents.AllowUserToAddRows = false;
            this.gridViewTracerPercents.AllowUserToDeleteRows = false;
            this.gridViewTracerPercents.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewTracerPercents.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTracer,
            this.colTracerPercent});
            this.tableLayoutPanel1.SetColumnSpan(this.gridViewTracerPercents, 2);
            this.gridViewTracerPercents.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridViewTracerPercents.Location = new System.Drawing.Point(3, 78);
            this.gridViewTracerPercents.Name = "gridViewTracerPercents";
            this.gridViewTracerPercents.ReadOnly = true;
            this.gridViewTracerPercents.Size = new System.Drawing.Size(279, 94);
            this.gridViewTracerPercents.TabIndex = 6;
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
            this.cbxShowScore.Location = new System.Drawing.Point(145, 3);
            this.cbxShowScore.Name = "cbxShowScore";
            this.cbxShowScore.Size = new System.Drawing.Size(84, 17);
            this.cbxShowScore.TabIndex = 7;
            this.cbxShowScore.Text = "Show Score";
            this.cbxShowScore.UseVisualStyleBackColor = true;
            this.cbxShowScore.CheckedChanged += new System.EventHandler(this.cbxShowScore_CheckedChanged);
            // 
            // splitContainer2
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.splitContainer2, 2);
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer2.Location = new System.Drawing.Point(3, 178);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.dataGridView1);
            this.splitContainer2.Size = new System.Drawing.Size(279, 304);
            this.splitContainer2.SplitterDistance = 137;
            this.splitContainer2.TabIndex = 8;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colFormula,
            this.colAmount});
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.Size = new System.Drawing.Size(279, 137);
            this.dataGridView1.TabIndex = 1;
            this.dataGridView1.SelectionChanged += new System.EventHandler(this.dataGridView1_SelectionChanged);
            // 
            // colFormula
            // 
            this.colFormula.HeaderText = "Formula";
            this.colFormula.Name = "colFormula";
            this.colFormula.ReadOnly = true;
            // 
            // colAmount
            // 
            this.colAmount.HeaderText = "Amount";
            this.colAmount.Name = "colAmount";
            this.colAmount.ReadOnly = true;
            // 
            // TracerChromatogramForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(855, 485);
            this.Controls.Add(this.splitContainer1);
            this.Name = "TracerChromatogramForm";
            this.TabText = "TracerChromatogramForm";
            this.Text = "TracerChromatogramForm";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTracerPercents)).EndInit();
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.CheckBox cbxAutoFindPeak;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFormula;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAmount;
        private System.Windows.Forms.CheckBox cbxShowScore;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxPrecursorPool;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxTurnover;
        private System.Windows.Forms.DataGridView gridViewTracerPercents;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracer;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracerPercent;
        private System.Windows.Forms.SplitContainer splitContainer2;
    }
}