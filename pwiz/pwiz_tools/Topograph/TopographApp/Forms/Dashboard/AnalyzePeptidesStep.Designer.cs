namespace pwiz.Topograph.ui.Forms.Dashboard
{
    partial class AnalyzePeptidesStep
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
            this.label1 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnAnalyzePeptides = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.linkPeptideAnalyses = new System.Windows.Forms.LinkLabel();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(578, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "If you add new search results to this workspace, or decide to analyze more peptid" +
                "es, you should create Peptide Analyses.";
            // 
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.linkPeptideAnalyses);
            this.panel1.Controls.Add(this.btnAnalyzePeptides);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 26);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(541, 26);
            this.panel1.TabIndex = 2;
            // 
            // btnAnalyzePeptides
            // 
            this.btnAnalyzePeptides.AutoSize = true;
            this.btnAnalyzePeptides.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnAnalyzePeptides.Location = new System.Drawing.Point(0, 0);
            this.btnAnalyzePeptides.Name = "btnAnalyzePeptides";
            this.btnAnalyzePeptides.Size = new System.Drawing.Size(106, 23);
            this.btnAnalyzePeptides.TabIndex = 0;
            this.btnAnalyzePeptides.Text = "Analyze peptides...";
            this.btnAnalyzePeptides.UseVisualStyleBackColor = true;
            this.btnAnalyzePeptides.Click += new System.EventHandler(this.BtnAnalyzePeptidesOnClick);
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStatus.Location = new System.Drawing.Point(0, 13);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(87, 13);
            this.lblStatus.TabIndex = 3;
            this.lblStatus.Text = "Status goes here";
            // 
            // linkPeptideAnalyses
            // 
            this.linkPeptideAnalyses.AutoSize = true;
            this.linkPeptideAnalyses.Location = new System.Drawing.Point(112, 5);
            this.linkPeptideAnalyses.Name = "linkPeptideAnalyses";
            this.linkPeptideAnalyses.Size = new System.Drawing.Size(114, 13);
            this.linkPeptideAnalyses.TabIndex = 1;
            this.linkPeptideAnalyses.TabStop = true;
            this.linkPeptideAnalyses.Text = "View Peptide Analyses";
            this.linkPeptideAnalyses.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkPeptideAnalysesOnLinkClicked);
            // 
            // AnalyzePeptidesStep
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.label1);
            this.Name = "AnalyzePeptidesStep";
            this.Size = new System.Drawing.Size(541, 150);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnAnalyzePeptides;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.LinkLabel linkPeptideAnalyses;
    }
}
