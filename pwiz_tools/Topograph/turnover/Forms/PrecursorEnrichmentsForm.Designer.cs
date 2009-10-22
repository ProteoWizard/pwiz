using System.Windows.Forms;

namespace pwiz.Topograph.ui.Forms
{
    partial class PrecursorEnrichmentsForm
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.gridViewTracerPercents = new System.Windows.Forms.DataGridView();
            this.colTracerName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTracerPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.tbxIntermediateLevelCount = new System.Windows.Forms.TextBox();
            this.gridViewFormulas = new System.Windows.Forms.DataGridView();
            this.colTracerFormula = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTracerFormulaPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tbxScore = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTracerPercents)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewFormulas)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.gridViewTracerPercents, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxIntermediateLevelCount, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.gridViewFormulas, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxScore, 1, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(346, 398);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // gridViewTracerPercents
            // 
            this.gridViewTracerPercents.AllowUserToAddRows = false;
            this.gridViewTracerPercents.AllowUserToDeleteRows = false;
            this.gridViewTracerPercents.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewTracerPercents.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTracerName,
            this.colTracerPercent});
            this.tableLayoutPanel1.SetColumnSpan(this.gridViewTracerPercents, 2);
            this.gridViewTracerPercents.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridViewTracerPercents.Location = new System.Drawing.Point(3, 53);
            this.gridViewTracerPercents.Name = "gridViewTracerPercents";
            this.gridViewTracerPercents.ReadOnly = true;
            this.gridViewTracerPercents.Size = new System.Drawing.Size(340, 94);
            this.gridViewTracerPercents.TabIndex = 9;
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
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(3, 25);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(167, 25);
            this.label4.TabIndex = 2;
            this.label4.Text = "Score";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(167, 25);
            this.label3.TabIndex = 4;
            this.label3.Text = "# of Intermediate Levels";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxIntermediateLevelCount
            // 
            this.tbxIntermediateLevelCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxIntermediateLevelCount.Location = new System.Drawing.Point(176, 3);
            this.tbxIntermediateLevelCount.Name = "tbxIntermediateLevelCount";
            this.tbxIntermediateLevelCount.Size = new System.Drawing.Size(167, 20);
            this.tbxIntermediateLevelCount.TabIndex = 5;
            // 
            // gridViewFormulas
            // 
            this.gridViewFormulas.AllowUserToAddRows = false;
            this.gridViewFormulas.AllowUserToDeleteRows = false;
            this.gridViewFormulas.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewFormulas.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTracerFormula,
            this.colTracerFormulaPercent});
            this.tableLayoutPanel1.SetColumnSpan(this.gridViewFormulas, 2);
            this.gridViewFormulas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridViewFormulas.Location = new System.Drawing.Point(3, 153);
            this.gridViewFormulas.Name = "gridViewFormulas";
            this.gridViewFormulas.ReadOnly = true;
            this.gridViewFormulas.Size = new System.Drawing.Size(340, 242);
            this.gridViewFormulas.TabIndex = 6;
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
            this.colTracerFormulaPercent.HeaderText = "Amount";
            this.colTracerFormulaPercent.Name = "colTracerFormulaPercent";
            this.colTracerFormulaPercent.ReadOnly = true;
            // 
            // tbxScore
            // 
            this.tbxScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxScore.Location = new System.Drawing.Point(176, 28);
            this.tbxScore.Name = "tbxScore";
            this.tbxScore.ReadOnly = true;
            this.tbxScore.Size = new System.Drawing.Size(167, 20);
            this.tbxScore.TabIndex = 8;
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
            this.splitContainer1.Size = new System.Drawing.Size(1052, 398);
            this.splitContainer1.SplitterDistance = 346;
            this.splitContainer1.TabIndex = 1;
            // 
            // PrecursorEnrichmentsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1052, 398);
            this.Controls.Add(this.splitContainer1);
            this.Name = "PrecursorEnrichmentsForm";
            this.TabText = "PrecursorEnrichmentsForm";
            this.Text = "PrecursorEnrichmentsForm";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTracerPercents)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewFormulas)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbxIntermediateLevelCount;
        private System.Windows.Forms.DataGridView gridViewFormulas;
        private SplitContainer splitContainer1;
        private Label label4;
        private TextBox tbxScore;
        private DataGridView gridViewTracerPercents;
        private DataGridViewTextBoxColumn colTracerName;
        private DataGridViewTextBoxColumn colTracerPercent;
        private DataGridViewTextBoxColumn colTracerFormula;
        private DataGridViewTextBoxColumn colTracerFormulaPercent;
    }
}