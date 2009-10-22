namespace pwiz.Topograph.ui.Forms
{
    partial class TracerAmountsForm
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
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.dataGridViewTracerFormulas = new System.Windows.Forms.DataGridView();
            this.colTracerFormula = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTracerFormulaPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxScore = new System.Windows.Forms.TextBox();
            this.dataGridViewTracerPercentages = new System.Windows.Forms.DataGridView();
            this.colTracerName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTracerPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewTracerFormulas)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewTracerPercentages)).BeginInit();
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
            this.splitContainer1.Panel1.Controls.Add(this.tableLayoutPanel2);
            this.splitContainer1.Size = new System.Drawing.Size(893, 264);
            this.splitContainer1.SplitterDistance = 301;
            this.splitContainer1.TabIndex = 1;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.Controls.Add(this.dataGridViewTracerFormulas, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.tbxScore, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.dataGridViewTracerPercentages, 0, 1);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 3;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(301, 264);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // dataGridViewTracerFormulas
            // 
            this.dataGridViewTracerFormulas.AllowUserToAddRows = false;
            this.dataGridViewTracerFormulas.AllowUserToDeleteRows = false;
            this.dataGridViewTracerFormulas.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewTracerFormulas.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTracerFormula,
            this.colTracerFormulaPercent});
            this.tableLayoutPanel2.SetColumnSpan(this.dataGridViewTracerFormulas, 2);
            this.dataGridViewTracerFormulas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewTracerFormulas.Location = new System.Drawing.Point(3, 128);
            this.dataGridViewTracerFormulas.Name = "dataGridViewTracerFormulas";
            this.dataGridViewTracerFormulas.ReadOnly = true;
            this.dataGridViewTracerFormulas.Size = new System.Drawing.Size(295, 133);
            this.dataGridViewTracerFormulas.TabIndex = 1;
            // 
            // colTracerFormula
            // 
            this.colTracerFormula.HeaderText = "Tracer Formula";
            this.colTracerFormula.Name = "colTracerFormula";
            this.colTracerFormula.ReadOnly = true;
            this.colTracerFormula.Width = 150;
            // 
            // colTracerFormulaPercent
            // 
            this.colTracerFormulaPercent.HeaderText = "Percent";
            this.colTracerFormulaPercent.Name = "colTracerFormulaPercent";
            this.colTracerFormulaPercent.ReadOnly = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(144, 25);
            this.label1.TabIndex = 2;
            this.label1.Text = "Score";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxScore
            // 
            this.tbxScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxScore.Location = new System.Drawing.Point(153, 3);
            this.tbxScore.Name = "tbxScore";
            this.tbxScore.ReadOnly = true;
            this.tbxScore.Size = new System.Drawing.Size(145, 20);
            this.tbxScore.TabIndex = 3;
            // 
            // dataGridViewTracerPercentages
            // 
            this.dataGridViewTracerPercentages.AllowUserToAddRows = false;
            this.dataGridViewTracerPercentages.AllowUserToDeleteRows = false;
            this.dataGridViewTracerPercentages.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewTracerPercentages.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTracerName,
            this.colTracerPercent});
            this.tableLayoutPanel2.SetColumnSpan(this.dataGridViewTracerPercentages, 2);
            this.dataGridViewTracerPercentages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewTracerPercentages.Location = new System.Drawing.Point(3, 28);
            this.dataGridViewTracerPercentages.Name = "dataGridViewTracerPercentages";
            this.dataGridViewTracerPercentages.ReadOnly = true;
            this.dataGridViewTracerPercentages.Size = new System.Drawing.Size(295, 94);
            this.dataGridViewTracerPercentages.TabIndex = 4;
            // 
            // colTracerName
            // 
            this.colTracerName.HeaderText = "Tracer";
            this.colTracerName.Name = "colTracerName";
            this.colTracerName.ReadOnly = true;
            // 
            // colTracerPercent
            // 
            this.colTracerPercent.HeaderText = "Percent";
            this.colTracerPercent.Name = "colTracerPercent";
            this.colTracerPercent.ReadOnly = true;
            // 
            // TracerAmountsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(893, 264);
            this.Controls.Add(this.splitContainer1);
            this.Name = "TracerAmountsForm";
            this.Text = "TracerAmountsForm";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewTracerFormulas)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewTracerPercentages)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.DataGridView dataGridViewTracerFormulas;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxScore;
        private System.Windows.Forms.DataGridView dataGridViewTracerPercentages;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracerName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracerPercent;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracerFormula;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracerFormulaPercent;
    }
}