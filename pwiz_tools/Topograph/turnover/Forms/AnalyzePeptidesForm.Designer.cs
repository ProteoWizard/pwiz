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
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 7);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Min Tracer Count";
            // 
            // tbxMinTracers
            // 
            this.tbxMinTracers.Location = new System.Drawing.Point(135, 7);
            this.tbxMinTracers.Name = "tbxMinTracers";
            this.tbxMinTracers.Size = new System.Drawing.Size(131, 20);
            this.tbxMinTracers.TabIndex = 1;
            this.tbxMinTracers.Text = "1";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 35);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(106, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Exclude Amino Acids";
            // 
            // tbxExcludeAas
            // 
            this.tbxExcludeAas.Location = new System.Drawing.Point(135, 35);
            this.tbxExcludeAas.Name = "tbxExcludeAas";
            this.tbxExcludeAas.Size = new System.Drawing.Size(131, 20);
            this.tbxExcludeAas.TabIndex = 3;
            this.tbxExcludeAas.Text = "QN";
            // 
            // btnCreateAnalyses
            // 
            this.btnCreateAnalyses.Location = new System.Drawing.Point(87, 227);
            this.btnCreateAnalyses.Name = "btnCreateAnalyses";
            this.btnCreateAnalyses.Size = new System.Drawing.Size(98, 25);
            this.btnCreateAnalyses.TabIndex = 4;
            this.btnCreateAnalyses.Text = "Create Analyses";
            this.btnCreateAnalyses.UseVisualStyleBackColor = true;
            this.btnCreateAnalyses.Click += new System.EventHandler(this.btnCreateAnalyses_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(191, 229);
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
            this.tbxStatus.Location = new System.Drawing.Point(12, 94);
            this.tbxStatus.Multiline = true;
            this.tbxStatus.Name = "tbxStatus";
            this.tbxStatus.ReadOnly = true;
            this.tbxStatus.Size = new System.Drawing.Size(254, 49);
            this.tbxStatus.TabIndex = 6;
            this.tbxStatus.Visible = false;
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(12, 168);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(254, 23);
            this.progressBar.TabIndex = 7;
            this.progressBar.Visible = false;
            // 
            // AnalyzePeptidesForm
            // 
            this.AcceptButton = this.btnCreateAnalyses;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(287, 264);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.tbxStatus);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnCreateAnalyses);
            this.Controls.Add(this.tbxExcludeAas);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.tbxMinTracers);
            this.Controls.Add(this.label1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AnalyzePeptidesForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "AnalyzePeptidesForm";
            this.Text = "Analyze Peptides";
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

    }
}