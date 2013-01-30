namespace pwiz.Topograph.ui.Forms.Dashboard
{
    partial class AddSearchResultsStep
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
            this.labelDescription = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnAddSearchResults = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panelUpdateProteinNames = new System.Windows.Forms.Panel();
            this.btnChooseFastaFile = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.panelUpdateProteinNames.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelDescription
            // 
            this.labelDescription.AutoSize = true;
            this.labelDescription.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelDescription.Location = new System.Drawing.Point(0, 0);
            this.labelDescription.Name = "labelDescription";
            this.labelDescription.Size = new System.Drawing.Size(424, 13);
            this.labelDescription.TabIndex = 1;
            this.labelDescription.Text = "Topograph reads search results from peptide search engines such as Sequest or Mas" +
    "cot";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStatus.Location = new System.Drawing.Point(0, 13);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(303, 13);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "No peptide search results have been added to this workspace.";
            // 
            // btnAddSearchResults
            // 
            this.btnAddSearchResults.AutoSize = true;
            this.btnAddSearchResults.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnAddSearchResults.Location = new System.Drawing.Point(0, 0);
            this.btnAddSearchResults.Name = "btnAddSearchResults";
            this.btnAddSearchResults.Size = new System.Drawing.Size(120, 23);
            this.btnAddSearchResults.TabIndex = 3;
            this.btnAddSearchResults.Text = "Add Search Results...";
            this.btnAddSearchResults.UseVisualStyleBackColor = true;
            this.btnAddSearchResults.Click += new System.EventHandler(this.BtnAddSearchResultsOnClick);
            // 
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.btnAddSearchResults);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 26);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(458, 26);
            this.panel1.TabIndex = 4;
            // 
            // panelUpdateProteinNames
            // 
            this.panelUpdateProteinNames.AutoSize = true;
            this.panelUpdateProteinNames.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelUpdateProteinNames.Controls.Add(this.btnChooseFastaFile);
            this.panelUpdateProteinNames.Controls.Add(this.label1);
            this.panelUpdateProteinNames.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelUpdateProteinNames.Location = new System.Drawing.Point(0, 52);
            this.panelUpdateProteinNames.Name = "panelUpdateProteinNames";
            this.panelUpdateProteinNames.Size = new System.Drawing.Size(458, 42);
            this.panelUpdateProteinNames.TabIndex = 5;
            // 
            // btnChooseFastaFile
            // 
            this.btnChooseFastaFile.AutoSize = true;
            this.btnChooseFastaFile.Location = new System.Drawing.Point(-3, 16);
            this.btnChooseFastaFile.Name = "btnChooseFastaFile";
            this.btnChooseFastaFile.Size = new System.Drawing.Size(129, 23);
            this.btnChooseFastaFile.TabIndex = 1;
            this.btnChooseFastaFile.Text = "Browse for FASTA file...";
            this.btnChooseFastaFile.UseVisualStyleBackColor = true;
            this.btnChooseFastaFile.Click += new System.EventHandler(this.BtnChooseFastaFileOnClick);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(346, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "You can point Topograph at a FASTA file to set the protein descriptions.";
            // 
            // AddSearchResultsStep
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.panelUpdateProteinNames);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.labelDescription);
            this.Name = "AddSearchResultsStep";
            this.Size = new System.Drawing.Size(458, 189);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panelUpdateProteinNames.ResumeLayout(false);
            this.panelUpdateProteinNames.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelDescription;
        private System.Windows.Forms.Button btnAddSearchResults;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panelUpdateProteinNames;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnChooseFastaFile;
    }
}
