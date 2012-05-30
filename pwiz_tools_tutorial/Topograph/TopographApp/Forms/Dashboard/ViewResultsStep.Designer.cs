namespace pwiz.Topograph.ui.Forms.Dashboard
{
    partial class ViewResultsStep
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblShowResults = new System.Windows.Forms.Label();
            this.linkResultsPerReplicate = new System.Windows.Forms.LinkLabel();
            this.linkResultsByCohort = new System.Windows.Forms.LinkLabel();
            this.lblHalfLives = new System.Windows.Forms.Label();
            this.linkHalfLives = new System.Windows.Forms.LinkLabel();
            this.linkDataFiles = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // lblShowResults
            // 
            this.lblShowResults.AutoSize = true;
            this.lblShowResults.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblShowResults.Location = new System.Drawing.Point(0, 0);
            this.lblShowResults.Name = "lblShowResults";
            this.lblShowResults.Size = new System.Drawing.Size(297, 13);
            this.lblShowResults.TabIndex = 0;
            this.lblShowResults.Text = "Topograph can show you the data that have been calculated";
            // 
            // linkResultsPerReplicate
            // 
            this.linkResultsPerReplicate.AutoSize = true;
            this.linkResultsPerReplicate.Dock = System.Windows.Forms.DockStyle.Top;
            this.linkResultsPerReplicate.Location = new System.Drawing.Point(0, 13);
            this.linkResultsPerReplicate.Name = "linkResultsPerReplicate";
            this.linkResultsPerReplicate.Size = new System.Drawing.Size(218, 13);
            this.linkResultsPerReplicate.TabIndex = 1;
            this.linkResultsPerReplicate.TabStop = true;
            this.linkResultsPerReplicate.Text = "Results with one row per peptide and sample";
            this.linkResultsPerReplicate.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkResultsPerReplicate_LinkClicked);
            // 
            // linkResultsByCohort
            // 
            this.linkResultsByCohort.AutoSize = true;
            this.linkResultsByCohort.Dock = System.Windows.Forms.DockStyle.Top;
            this.linkResultsByCohort.Location = new System.Drawing.Point(0, 26);
            this.linkResultsByCohort.Name = "linkResultsByCohort";
            this.linkResultsByCohort.Size = new System.Drawing.Size(201, 13);
            this.linkResultsByCohort.TabIndex = 2;
            this.linkResultsByCohort.TabStop = true;
            this.linkResultsByCohort.Text = "Results grouped by protein and/or cohort";
            this.linkResultsByCohort.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkResultsByCohort_LinkClicked);
            // 
            // lblHalfLives
            // 
            this.lblHalfLives.AutoSize = true;
            this.lblHalfLives.Location = new System.Drawing.Point(3, 39);
            this.lblHalfLives.Name = "lblHalfLives";
            this.lblHalfLives.Size = new System.Drawing.Size(240, 13);
            this.lblHalfLives.TabIndex = 3;
            this.lblHalfLives.Text = "Topograph can calculate the half lives of proteins";
            // 
            // linkHalfLives
            // 
            this.linkHalfLives.AutoSize = true;
            this.linkHalfLives.Location = new System.Drawing.Point(3, 65);
            this.linkHalfLives.Name = "linkHalfLives";
            this.linkHalfLives.Size = new System.Drawing.Size(74, 13);
            this.linkHalfLives.TabIndex = 5;
            this.linkHalfLives.TabStop = true;
            this.linkHalfLives.Text = "View half lives";
            this.linkHalfLives.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkHalfLives_LinkClicked);
            // 
            // linkDataFiles
            // 
            this.linkDataFiles.AutoSize = true;
            this.linkDataFiles.Location = new System.Drawing.Point(3, 52);
            this.linkDataFiles.Name = "linkDataFiles";
            this.linkDataFiles.Size = new System.Drawing.Size(183, 13);
            this.linkDataFiles.TabIndex = 4;
            this.linkDataFiles.TabStop = true;
            this.linkDataFiles.Text = "Set cohort and time points of samples";
            this.linkDataFiles.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkDataFiles_LinkClicked);
            // 
            // ViewResultsStep
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.linkHalfLives);
            this.Controls.Add(this.linkDataFiles);
            this.Controls.Add(this.lblHalfLives);
            this.Controls.Add(this.linkResultsByCohort);
            this.Controls.Add(this.linkResultsPerReplicate);
            this.Controls.Add(this.lblShowResults);
            this.Name = "ViewResultsStep";
            this.Size = new System.Drawing.Size(508, 215);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblShowResults;
        private System.Windows.Forms.LinkLabel linkResultsPerReplicate;
        private System.Windows.Forms.LinkLabel linkResultsByCohort;
        private System.Windows.Forms.Label lblHalfLives;
        private System.Windows.Forms.LinkLabel linkHalfLives;
        private System.Windows.Forms.LinkLabel linkDataFiles;

    }
}
