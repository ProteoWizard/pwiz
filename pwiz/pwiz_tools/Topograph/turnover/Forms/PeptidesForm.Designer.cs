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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.btnAnalyzePeptides = new System.Windows.Forms.Button();
            this.btnAddSearchResults = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.findBox1 = new pwiz.Common.Controls.FindBox();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
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
            this.dataGridView.Location = new System.Drawing.Point(0, 27);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.Size = new System.Drawing.Size(941, 535);
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
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 152F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 124F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Controls.Add(this.btnAnalyzePeptides, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnAddSearchResults, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.findBox1, 3, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(941, 27);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // btnAnalyzePeptides
            // 
            this.btnAnalyzePeptides.Enabled = false;
            this.btnAnalyzePeptides.Location = new System.Drawing.Point(155, 3);
            this.btnAnalyzePeptides.Name = "btnAnalyzePeptides";
            this.btnAnalyzePeptides.Size = new System.Drawing.Size(117, 24);
            this.btnAnalyzePeptides.TabIndex = 4;
            this.btnAnalyzePeptides.Text = "Analyze Peptides...";
            this.btnAnalyzePeptides.UseVisualStyleBackColor = true;
            this.btnAnalyzePeptides.Click += new System.EventHandler(this.btnAnalyzePeptides_Click);
            // 
            // btnAddSearchResults
            // 
            this.btnAddSearchResults.Location = new System.Drawing.Point(3, 3);
            this.btnAddSearchResults.Name = "btnAddSearchResults";
            this.btnAddSearchResults.Size = new System.Drawing.Size(145, 23);
            this.btnAddSearchResults.TabIndex = 6;
            this.btnAddSearchResults.Text = "Add Search Results...";
            this.btnAddSearchResults.UseVisualStyleBackColor = true;
            this.btnAddSearchResults.Click += new System.EventHandler(this.btnAddSearchResults_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(279, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(40, 30);
            this.label1.TabIndex = 7;
            this.label1.Text = "Find:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // findBox1
            // 
            this.findBox1.DataGridView = this.dataGridView;
            this.findBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.findBox1.Location = new System.Drawing.Point(325, 3);
            this.findBox1.Name = "findBox1";
            this.findBox1.Size = new System.Drawing.Size(613, 24);
            this.findBox1.TabIndex = 8;
            // 
            // PeptidesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(941, 562);
            this.Controls.Add(this.dataGridView);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PeptidesForm";
            this.TabText = "PeptidesForm";
            this.Text = "PeptidesForm";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
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
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Button btnAnalyzePeptides;
        private System.Windows.Forms.Button btnAddSearchResults;
        private System.Windows.Forms.Label label1;
        private pwiz.Common.Controls.FindBox findBox1;

    }
}