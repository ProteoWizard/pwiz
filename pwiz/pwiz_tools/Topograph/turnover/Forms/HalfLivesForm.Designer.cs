namespace pwiz.Topograph.ui.Forms
{
    partial class HalfLivesForm
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
            this.cbxByProtein = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxMinScore = new System.Windows.Forms.TextBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colPeptide = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colProteinKey = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnRequery = new System.Windows.Forms.Button();
            this.tbxInitialTracerPercent = new System.Windows.Forms.TextBox();
            this.tbxFinalTracerPercent = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.findBox = new pwiz.Common.Controls.FindBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label2 = new System.Windows.Forms.Label();
            this.cbxFixYIntercept = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // cbxByProtein
            // 
            this.cbxByProtein.AutoSize = true;
            this.cbxByProtein.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbxByProtein.Location = new System.Drawing.Point(314, 3);
            this.cbxByProtein.Name = "cbxByProtein";
            this.cbxByProtein.Size = new System.Drawing.Size(94, 19);
            this.cbxByProtein.TabIndex = 0;
            this.cbxByProtein.Text = "By Protein";
            this.cbxByProtein.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(314, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(94, 25);
            this.label1.TabIndex = 1;
            this.label1.Text = "Minimum Score:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMinScore
            // 
            this.tbxMinScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinScore.Location = new System.Drawing.Point(414, 28);
            this.tbxMinScore.Name = "tbxMinScore";
            this.tbxMinScore.Size = new System.Drawing.Size(238, 20);
            this.tbxMinScore.TabIndex = 2;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptide,
            this.colProteinKey,
            this.colProteinDescription,
            this.colProteinName});
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 76);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.Size = new System.Drawing.Size(760, 335);
            this.dataGridView1.TabIndex = 3;
            this.dataGridView1.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellContentClick);
            // 
            // colPeptide
            // 
            this.colPeptide.HeaderText = "Peptide";
            this.colPeptide.Name = "colPeptide";
            this.colPeptide.ReadOnly = true;
            this.colPeptide.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colProteinKey
            // 
            this.colProteinKey.HeaderText = "Protein";
            this.colProteinKey.Name = "colProteinKey";
            this.colProteinKey.ReadOnly = true;
            this.colProteinKey.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // colProteinDescription
            // 
            this.colProteinDescription.HeaderText = "Protein Description";
            this.colProteinDescription.Name = "colProteinDescription";
            this.colProteinDescription.ReadOnly = true;
            this.colProteinDescription.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // colProteinName
            // 
            this.colProteinName.HeaderText = "Protein Name";
            this.colProteinName.Name = "colProteinName";
            this.colProteinName.ReadOnly = true;
            this.colProteinName.Visible = false;
            // 
            // btnRequery
            // 
            this.btnRequery.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnRequery.Location = new System.Drawing.Point(658, 3);
            this.btnRequery.Name = "btnRequery";
            this.btnRequery.Size = new System.Drawing.Size(99, 19);
            this.btnRequery.TabIndex = 4;
            this.btnRequery.Text = "Recalculate";
            this.btnRequery.UseVisualStyleBackColor = true;
            this.btnRequery.Click += new System.EventHandler(this.btnRequery_Click);
            // 
            // tbxInitialTracerPercent
            // 
            this.tbxInitialTracerPercent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxInitialTracerPercent.Location = new System.Drawing.Point(103, 3);
            this.tbxInitialTracerPercent.Name = "tbxInitialTracerPercent";
            this.tbxInitialTracerPercent.Size = new System.Drawing.Size(205, 20);
            this.tbxInitialTracerPercent.TabIndex = 5;
            // 
            // tbxFinalTracerPercent
            // 
            this.tbxFinalTracerPercent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxFinalTracerPercent.Location = new System.Drawing.Point(103, 28);
            this.tbxFinalTracerPercent.Name = "tbxFinalTracerPercent";
            this.tbxFinalTracerPercent.Size = new System.Drawing.Size(205, 20);
            this.tbxFinalTracerPercent.TabIndex = 7;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(94, 25);
            this.label3.TabIndex = 8;
            this.label3.Text = "Final Tracer %";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnSave
            // 
            this.btnSave.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSave.Enabled = false;
            this.btnSave.Location = new System.Drawing.Point(658, 28);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(99, 19);
            this.btnSave.TabIndex = 9;
            this.btnSave.Text = "Save...";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // findBox
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.findBox, 2);
            this.findBox.DataGridView = this.dataGridView1;
            this.findBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.findBox.Location = new System.Drawing.Point(414, 53);
            this.findBox.Name = "findBox";
            this.findBox.Size = new System.Drawing.Size(343, 20);
            this.findBox.TabIndex = 10;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(314, 50);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(94, 26);
            this.label4.TabIndex = 11;
            this.label4.Text = "Find:";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 46.3964F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 53.6036F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 104F));
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxInitialTracerPercent, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.btnSave, 4, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxFinalTracerPercent, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.btnRequery, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.cbxByProtein, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinScore, 3, 1);
            this.tableLayoutPanel1.Controls.Add(this.cbxFixYIntercept, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.findBox, 3, 2);
            this.tableLayoutPanel1.Controls.Add(this.label4, 2, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(760, 76);
            this.tableLayoutPanel1.TabIndex = 12;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(94, 25);
            this.label2.TabIndex = 7;
            this.label2.Text = "Initial Tracer %";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // cbxFixYIntercept
            // 
            this.cbxFixYIntercept.AutoSize = true;
            this.cbxFixYIntercept.Checked = true;
            this.cbxFixYIntercept.CheckState = System.Windows.Forms.CheckState.Checked;
            this.tableLayoutPanel1.SetColumnSpan(this.cbxFixYIntercept, 2);
            this.cbxFixYIntercept.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbxFixYIntercept.Location = new System.Drawing.Point(3, 53);
            this.cbxFixYIntercept.Name = "cbxFixYIntercept";
            this.cbxFixYIntercept.Size = new System.Drawing.Size(305, 20);
            this.cbxFixYIntercept.TabIndex = 12;
            this.cbxFixYIntercept.Text = "Hold Initial Tracer % Constant";
            this.cbxFixYIntercept.UseVisualStyleBackColor = true;
            // 
            // HalfLivesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(760, 411);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "HalfLivesForm";
            this.TabText = "HalfLivesForm";
            this.Text = "HalfLivesForm";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckBox cbxByProtein;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxMinScore;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btnRequery;
        private System.Windows.Forms.TextBox tbxInitialTracerPercent;
        private System.Windows.Forms.TextBox tbxFinalTracerPercent;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnSave;
        private pwiz.Common.Controls.FindBox findBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox cbxFixYIntercept;
        private System.Windows.Forms.DataGridViewLinkColumn colPeptide;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinKey;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinName;
    }
}