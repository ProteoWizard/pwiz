namespace pwiz.Topograph.ui.Forms
{
    partial class AnalyzePeptidesForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AnalyzePeptidesForm));
            this.label1 = new System.Windows.Forms.Label();
            this.tbxMinTracers = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxExcludeAas = new System.Windows.Forms.TextBox();
            this.btnCreateAnalyses = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tbxStatus = new System.Windows.Forms.TextBox();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.cbxIncludeMissingMS2 = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 130);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(253, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "Minimum Number of Amino Acid Labels";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // tbxMinTracers
            // 
            this.tbxMinTracers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinTracers.Location = new System.Drawing.Point(262, 133);
            this.tbxMinTracers.Name = "tbxMinTracers";
            this.tbxMinTracers.Size = new System.Drawing.Size(253, 20);
            this.tbxMinTracers.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 205);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(253, 25);
            this.label2.TabIndex = 2;
            this.label2.Text = "Exclude peptides containing these amino acids";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // tbxExcludeAas
            // 
            this.tbxExcludeAas.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxExcludeAas.Location = new System.Drawing.Point(262, 208);
            this.tbxExcludeAas.Name = "tbxExcludeAas";
            this.tbxExcludeAas.Size = new System.Drawing.Size(253, 20);
            this.tbxExcludeAas.TabIndex = 3;
            // 
            // btnCreateAnalyses
            // 
            this.btnCreateAnalyses.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCreateAnalyses.Location = new System.Drawing.Point(327, 394);
            this.btnCreateAnalyses.Name = "btnCreateAnalyses";
            this.btnCreateAnalyses.Size = new System.Drawing.Size(98, 25);
            this.btnCreateAnalyses.TabIndex = 4;
            this.btnCreateAnalyses.Text = "Create Analyses";
            this.btnCreateAnalyses.UseVisualStyleBackColor = true;
            this.btnCreateAnalyses.Click += new System.EventHandler(this.btnCreateAnalyses_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(431, 394);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // tbxStatus
            // 
            this.tbxStatus.AcceptsReturn = true;
            this.tbxStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.tbxStatus.Location = new System.Drawing.Point(0, 309);
            this.tbxStatus.Multiline = true;
            this.tbxStatus.Name = "tbxStatus";
            this.tbxStatus.ReadOnly = true;
            this.tbxStatus.Size = new System.Drawing.Size(518, 49);
            this.tbxStatus.TabIndex = 6;
            this.tbxStatus.Visible = false;
            // 
            // progressBar
            // 
            this.progressBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.progressBar.Location = new System.Drawing.Point(0, 358);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(518, 23);
            this.progressBar.TabIndex = 7;
            this.progressBar.Visible = false;
            // 
            // cbxIncludeMissingMS2
            // 
            this.cbxIncludeMissingMS2.AutoSize = true;
            this.cbxIncludeMissingMS2.Location = new System.Drawing.Point(3, 283);
            this.cbxIncludeMissingMS2.Name = "cbxIncludeMissingMS2";
            this.cbxIncludeMissingMS2.Size = new System.Drawing.Size(183, 17);
            this.cbxIncludeMissingMS2.TabIndex = 8;
            this.cbxIncludeMissingMS2.Text = "Include Samples Without MS2 ID";
            this.cbxIncludeMissingMS2.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoEllipsis = true;
            this.tableLayoutPanel1.SetColumnSpan(this.label3, 2);
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(512, 80);
            this.label3.TabIndex = 9;
            this.label3.Text = resources.GetString("label3.Text");
            // 
            // label4
            // 
            this.label4.AutoEllipsis = true;
            this.tableLayoutPanel1.SetColumnSpan(this.label4, 2);
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(3, 80);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(512, 50);
            this.label4.TabIndex = 10;
            this.label4.Text = "If you only want to analyze peptides that have one or more (potentially) labeled " +
                "amino acids in them, then specify that here.";
            this.label4.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // label5
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.label5, 2);
            this.label5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label5.Location = new System.Drawing.Point(3, 155);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(512, 50);
            this.label5.TabIndex = 11;
            this.label5.Text = "If you want to avoid analyzing peptides which contain particular amino acids, the" +
                "n specify the single letter amino acid codes here.";
            this.label5.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // label6
            // 
            this.label6.AutoEllipsis = true;
            this.tableLayoutPanel1.SetColumnSpan(this.label6, 2);
            this.label6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label6.Location = new System.Drawing.Point(3, 230);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(512, 50);
            this.label6.TabIndex = 12;
            this.label6.Text = resources.GetString("label6.Text");
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.cbxIncludeMissingMS2, 0, 6);
            this.tableLayoutPanel1.Controls.Add(this.label6, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinTracers, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.tbxExcludeAas, 1, 4);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 7;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(518, 309);
            this.tableLayoutPanel1.TabIndex = 13;
            // 
            // AnalyzePeptidesForm
            // 
            this.AcceptButton = this.btnCreateAnalyses;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(518, 429);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnCreateAnalyses);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.tbxStatus);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AnalyzePeptidesForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "AnalyzePeptidesForm";
            this.Text = "Analyze Peptides";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxMinTracers;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxExcludeAas;
        private System.Windows.Forms.Button btnCreateAnalyses;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox tbxStatus;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.CheckBox cbxIncludeMissingMS2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;

    }
}