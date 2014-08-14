namespace pwiz.Topograph.ui.Forms
{
    partial class RecalculateResultsForm
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
            this.tbxChromatogramsPresent = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxResultsPresent = new System.Windows.Forms.TextBox();
            this.tbxChromatogramsMissing = new System.Windows.Forms.TextBox();
            this.tbxResultsMissing = new System.Windows.Forms.TextBox();
            this.btnRegenerateChromatograms = new System.Windows.Forms.Button();
            this.btnRecalculateResults = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.Controls.Add(this.tbxChromatogramsPresent, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label2, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label3, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxResultsPresent, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxChromatogramsMissing, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxResultsMissing, 2, 2);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(12, 12);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(427, 100);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // tbxChromatogramsPresent
            // 
            this.tbxChromatogramsPresent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxChromatogramsPresent.Location = new System.Drawing.Point(145, 43);
            this.tbxChromatogramsPresent.Name = "tbxChromatogramsPresent";
            this.tbxChromatogramsPresent.ReadOnly = true;
            this.tbxChromatogramsPresent.Size = new System.Drawing.Size(136, 20);
            this.tbxChromatogramsPresent.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 40);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Chromatograms";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(145, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(43, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Present";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(287, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(42, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "Missing";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 80);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(42, 13);
            this.label4.TabIndex = 4;
            this.label4.Text = "Results";
            // 
            // tbxResultsPresent
            // 
            this.tbxResultsPresent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxResultsPresent.Location = new System.Drawing.Point(145, 83);
            this.tbxResultsPresent.Name = "tbxResultsPresent";
            this.tbxResultsPresent.ReadOnly = true;
            this.tbxResultsPresent.Size = new System.Drawing.Size(136, 20);
            this.tbxResultsPresent.TabIndex = 5;
            // 
            // tbxChromatogramsMissing
            // 
            this.tbxChromatogramsMissing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxChromatogramsMissing.Location = new System.Drawing.Point(287, 43);
            this.tbxChromatogramsMissing.Name = "tbxChromatogramsMissing";
            this.tbxChromatogramsMissing.ReadOnly = true;
            this.tbxChromatogramsMissing.Size = new System.Drawing.Size(137, 20);
            this.tbxChromatogramsMissing.TabIndex = 6;
            // 
            // tbxResultsMissing
            // 
            this.tbxResultsMissing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxResultsMissing.Location = new System.Drawing.Point(287, 83);
            this.tbxResultsMissing.Name = "tbxResultsMissing";
            this.tbxResultsMissing.ReadOnly = true;
            this.tbxResultsMissing.Size = new System.Drawing.Size(137, 20);
            this.tbxResultsMissing.TabIndex = 7;
            // 
            // btnRegenerateChromatograms
            // 
            this.btnRegenerateChromatograms.Location = new System.Drawing.Point(252, 161);
            this.btnRegenerateChromatograms.Name = "btnRegenerateChromatograms";
            this.btnRegenerateChromatograms.Size = new System.Drawing.Size(181, 23);
            this.btnRegenerateChromatograms.TabIndex = 1;
            this.btnRegenerateChromatograms.Text = "Regenerate Chromatograms";
            this.btnRegenerateChromatograms.UseVisualStyleBackColor = true;
            this.btnRegenerateChromatograms.Click += new System.EventHandler(this.BtnRegenerateChromatogramsOnClick);
            // 
            // btnRecalculateResults
            // 
            this.btnRecalculateResults.Location = new System.Drawing.Point(252, 190);
            this.btnRecalculateResults.Name = "btnRecalculateResults";
            this.btnRecalculateResults.Size = new System.Drawing.Size(181, 23);
            this.btnRecalculateResults.TabIndex = 2;
            this.btnRecalculateResults.Text = "Recalculate Results";
            this.btnRecalculateResults.UseVisualStyleBackColor = true;
            this.btnRecalculateResults.Click += new System.EventHandler(this.BtnRecalculateResultsOnClick);
            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new System.Drawing.Point(255, 118);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(178, 23);
            this.btnRefresh.TabIndex = 3;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.BtnRefreshOnClick);
            // 
            // RecalculateResultsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(445, 262);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.btnRecalculateResults);
            this.Controls.Add(this.btnRegenerateChromatograms);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "RecalculateResultsForm";
            this.TabText = "RecalculateResultsForm";
            this.Text = "RecalculateResultsForm";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxChromatogramsPresent;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxResultsPresent;
        private System.Windows.Forms.TextBox tbxChromatogramsMissing;
        private System.Windows.Forms.TextBox tbxResultsMissing;
        private System.Windows.Forms.Button btnRegenerateChromatograms;
        private System.Windows.Forms.Button btnRecalculateResults;
        private System.Windows.Forms.Button btnRefresh;
    }
}