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
            this.label4 = new System.Windows.Forms.Label();
            this.tbxInitialPrecursorEnrichment = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxFinalPrecursorEnrichment = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbxIntermediateLevelCount = new System.Windows.Forms.TextBox();
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.PrecursorEnrichment = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AmountOfPeptide = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tbxScore = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbxInitialPrecursorEnrichment, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxFinalPrecursorEnrichment, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxIntermediateLevelCount, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.dataGridView, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.tbxScore, 1, 3);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 5;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(346, 398);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(3, 75);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(167, 25);
            this.label4.TabIndex = 2;
            this.label4.Text = "Score";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxInitialPrecursorEnrichment
            // 
            this.tbxInitialPrecursorEnrichment.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxInitialPrecursorEnrichment.Location = new System.Drawing.Point(176, 3);
            this.tbxInitialPrecursorEnrichment.Name = "tbxInitialPrecursorEnrichment";
            this.tbxInitialPrecursorEnrichment.Size = new System.Drawing.Size(167, 20);
            this.tbxInitialPrecursorEnrichment.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(167, 25);
            this.label1.TabIndex = 1;
            this.label1.Text = "Initial Precursor Enrichment";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(167, 25);
            this.label2.TabIndex = 2;
            this.label2.Text = "Final Precursor Enrichment";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxFinalPrecursorEnrichment
            // 
            this.tbxFinalPrecursorEnrichment.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxFinalPrecursorEnrichment.Location = new System.Drawing.Point(176, 28);
            this.tbxFinalPrecursorEnrichment.Name = "tbxFinalPrecursorEnrichment";
            this.tbxFinalPrecursorEnrichment.Size = new System.Drawing.Size(167, 20);
            this.tbxFinalPrecursorEnrichment.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 50);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(167, 25);
            this.label3.TabIndex = 4;
            this.label3.Text = "# of Intermediate Levels";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxIntermediateLevelCount
            // 
            this.tbxIntermediateLevelCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxIntermediateLevelCount.Location = new System.Drawing.Point(176, 53);
            this.tbxIntermediateLevelCount.Name = "tbxIntermediateLevelCount";
            this.tbxIntermediateLevelCount.Size = new System.Drawing.Size(167, 20);
            this.tbxIntermediateLevelCount.TabIndex = 5;
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.PrecursorEnrichment,
            this.AmountOfPeptide});
            this.tableLayoutPanel1.SetColumnSpan(this.dataGridView, 2);
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.Location = new System.Drawing.Point(3, 103);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.Size = new System.Drawing.Size(340, 292);
            this.dataGridView.TabIndex = 6;
            // 
            // PrecursorEnrichment
            // 
            this.PrecursorEnrichment.HeaderText = "Precursor Enrichment";
            this.PrecursorEnrichment.Name = "PrecursorEnrichment";
            this.PrecursorEnrichment.ReadOnly = true;
            // 
            // AmountOfPeptide
            // 
            this.AmountOfPeptide.HeaderText = "Amount Of Peptide";
            this.AmountOfPeptide.Name = "AmountOfPeptide";
            this.AmountOfPeptide.ReadOnly = true;
            // 
            // tbxScore
            // 
            this.tbxScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxScore.Location = new System.Drawing.Point(176, 78);
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
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox tbxInitialPrecursorEnrichment;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxFinalPrecursorEnrichment;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbxIntermediateLevelCount;
        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn PrecursorEnrichment;
        private System.Windows.Forms.DataGridViewTextBoxColumn AmountOfPeptide;
        private SplitContainer splitContainer1;
        private Label label4;
        private TextBox tbxScore;
    }
}