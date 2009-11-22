namespace pwiz.Topograph.ui.Forms
{
    partial class PeptidesForm
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
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.colSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProtein = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMaxTracerCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSearchResultCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxMinTracerCount = new System.Windows.Forms.TextBox();
            this.tbxExcludeAas = new System.Windows.Forms.TextBox();
            this.btnAnalyzePeptides = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.btnAddSearchResults = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSequence,
            this.colProtein,
            this.colProteinDescription,
            this.colMaxTracerCount,
            this.colSearchResultCount});
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.Size = new System.Drawing.Size(519, 393);
            this.dataGridView.TabIndex = 0;
            this.dataGridView.RowHeaderMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView_RowHeaderMouseDoubleClick);
            // 
            // colSequence
            // 
            this.colSequence.HeaderText = "Sequence";
            this.colSequence.Name = "colSequence";
            this.colSequence.ReadOnly = true;
            this.colSequence.Width = 150;
            // 
            // colProtein
            // 
            this.colProtein.HeaderText = "Protein";
            this.colProtein.Name = "colProtein";
            this.colProtein.ReadOnly = true;
            // 
            // colProteinDescription
            // 
            this.colProteinDescription.HeaderText = "Protein Description";
            this.colProteinDescription.Name = "colProteinDescription";
            this.colProteinDescription.ReadOnly = true;
            this.colProteinDescription.Width = 150;
            // 
            // colMaxTracerCount
            // 
            this.colMaxTracerCount.HeaderText = "Max Tracers";
            this.colMaxTracerCount.Name = "colMaxTracerCount";
            this.colMaxTracerCount.ReadOnly = true;
            this.colMaxTracerCount.Width = 90;
            // 
            // colSearchResultCount
            // 
            this.colSearchResultCount.HeaderText = "# Data Files";
            this.colSearchResultCount.Name = "colSearchResultCount";
            this.colSearchResultCount.ReadOnly = true;
            this.colSearchResultCount.Width = 90;
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
            this.splitContainer1.Panel2.Controls.Add(this.dataGridView);
            this.splitContainer1.Size = new System.Drawing.Size(749, 393);
            this.splitContainer1.SplitterDistance = 226;
            this.splitContainer1.TabIndex = 2;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinTracerCount, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbxExcludeAas, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.btnAnalyzePeptides, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnAddSearchResults, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 6;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(226, 393);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 85);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(107, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "Min Tracer Count";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 110);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(107, 25);
            this.label2.TabIndex = 1;
            this.label2.Text = "Exclude Amino Acids";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMinTracerCount
            // 
            this.tbxMinTracerCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinTracerCount.Location = new System.Drawing.Point(116, 88);
            this.tbxMinTracerCount.Name = "tbxMinTracerCount";
            this.tbxMinTracerCount.Size = new System.Drawing.Size(107, 20);
            this.tbxMinTracerCount.TabIndex = 2;
            this.tbxMinTracerCount.Leave += new System.EventHandler(this.tbxMinTracerCount_Leave);
            // 
            // tbxExcludeAas
            // 
            this.tbxExcludeAas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxExcludeAas.Location = new System.Drawing.Point(116, 113);
            this.tbxExcludeAas.Name = "tbxExcludeAas";
            this.tbxExcludeAas.Size = new System.Drawing.Size(107, 20);
            this.tbxExcludeAas.TabIndex = 3;
            this.tbxExcludeAas.Leave += new System.EventHandler(this.tbxExcludeAas_Leave);
            // 
            // btnAnalyzePeptides
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.btnAnalyzePeptides, 2);
            this.btnAnalyzePeptides.Enabled = false;
            this.btnAnalyzePeptides.Location = new System.Drawing.Point(3, 33);
            this.btnAnalyzePeptides.Name = "btnAnalyzePeptides";
            this.btnAnalyzePeptides.Size = new System.Drawing.Size(117, 24);
            this.btnAnalyzePeptides.TabIndex = 4;
            this.btnAnalyzePeptides.Text = "Analyze Peptides...";
            this.btnAnalyzePeptides.UseVisualStyleBackColor = true;
            this.btnAnalyzePeptides.Click += new System.EventHandler(this.btnAnalyzePeptides_Click);
            // 
            // label3
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.label3, 2);
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 60);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(220, 25);
            this.label3.TabIndex = 5;
            this.label3.Text = "Filter displayed peptides:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnAddSearchResults
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.btnAddSearchResults, 2);
            this.btnAddSearchResults.Location = new System.Drawing.Point(3, 3);
            this.btnAddSearchResults.Name = "btnAddSearchResults";
            this.btnAddSearchResults.Size = new System.Drawing.Size(145, 23);
            this.btnAddSearchResults.TabIndex = 6;
            this.btnAddSearchResults.Text = "Add Search Results...";
            this.btnAddSearchResults.UseVisualStyleBackColor = true;
            this.btnAddSearchResults.Click += new System.EventHandler(this.btnAddSearchResults_Click);
            // 
            // PeptidesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(749, 393);
            this.Controls.Add(this.splitContainer1);
            this.Name = "PeptidesForm";
            this.TabText = "PeptidesForm";
            this.Text = "PeptidesForm";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProtein;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMaxTracerCount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSearchResultCount;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxMinTracerCount;
        private System.Windows.Forms.TextBox tbxExcludeAas;
        private System.Windows.Forms.Button btnAnalyzePeptides;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnAddSearchResults;

    }
}