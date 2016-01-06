namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class BuildPeptideSearchLibraryControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BuildPeptideSearchLibraryControl));
            this.peptideSearchSplitContainer = new System.Windows.Forms.SplitContainer();
            this.label1 = new System.Windows.Forms.Label();
            this.textCutoff = new System.Windows.Forms.TextBox();
            this.cbIncludeAmbiguousMatches = new System.Windows.Forms.CheckBox();
            this.grpWorkflow = new System.Windows.Forms.GroupBox();
            this.radioDIA = new System.Windows.Forms.RadioButton();
            this.radioPRM = new System.Windows.Forms.RadioButton();
            this.radioDDA = new System.Windows.Forms.RadioButton();
            this.cbFilterForDocumentPeptides = new System.Windows.Forms.CheckBox();
            this.btnRemFile = new System.Windows.Forms.Button();
            this.listSearchFiles = new System.Windows.Forms.ListBox();
            this.label7 = new System.Windows.Forms.Label();
            this.btnAddFile = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.peptideSearchSplitContainer)).BeginInit();
            this.peptideSearchSplitContainer.Panel1.SuspendLayout();
            this.peptideSearchSplitContainer.Panel2.SuspendLayout();
            this.peptideSearchSplitContainer.SuspendLayout();
            this.grpWorkflow.SuspendLayout();
            this.SuspendLayout();
            // 
            // peptideSearchSplitContainer
            // 
            resources.ApplyResources(this.peptideSearchSplitContainer, "peptideSearchSplitContainer");
            this.peptideSearchSplitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.peptideSearchSplitContainer.Name = "peptideSearchSplitContainer";
            // 
            // peptideSearchSplitContainer.Panel1
            // 
            this.peptideSearchSplitContainer.Panel1.Controls.Add(this.label1);
            this.peptideSearchSplitContainer.Panel1.Controls.Add(this.textCutoff);
            // 
            // peptideSearchSplitContainer.Panel2
            // 
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.cbIncludeAmbiguousMatches);
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.grpWorkflow);
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.cbFilterForDocumentPeptides);
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.btnRemFile);
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.listSearchFiles);
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.label7);
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.btnAddFile);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textCutoff
            // 
            resources.ApplyResources(this.textCutoff, "textCutoff");
            this.textCutoff.Name = "textCutoff";
            // 
            // cbIncludeAmbiguousMatches
            // 
            resources.ApplyResources(this.cbIncludeAmbiguousMatches, "cbIncludeAmbiguousMatches");
            this.cbIncludeAmbiguousMatches.Name = "cbIncludeAmbiguousMatches";
            this.cbIncludeAmbiguousMatches.UseVisualStyleBackColor = true;
            // 
            // grpWorkflow
            // 
            resources.ApplyResources(this.grpWorkflow, "grpWorkflow");
            this.grpWorkflow.Controls.Add(this.radioDIA);
            this.grpWorkflow.Controls.Add(this.radioPRM);
            this.grpWorkflow.Controls.Add(this.radioDDA);
            this.grpWorkflow.Name = "grpWorkflow";
            this.grpWorkflow.TabStop = false;
            // 
            // radioDIA
            // 
            resources.ApplyResources(this.radioDIA, "radioDIA");
            this.radioDIA.Name = "radioDIA";
            this.radioDIA.UseVisualStyleBackColor = true;
            // 
            // radioPRM
            // 
            resources.ApplyResources(this.radioPRM, "radioPRM");
            this.radioPRM.Name = "radioPRM";
            this.radioPRM.UseVisualStyleBackColor = true;
            // 
            // radioDDA
            // 
            resources.ApplyResources(this.radioDDA, "radioDDA");
            this.radioDDA.Checked = true;
            this.radioDDA.Name = "radioDDA";
            this.radioDDA.TabStop = true;
            this.radioDDA.UseVisualStyleBackColor = true;
            // 
            // cbFilterForDocumentPeptides
            // 
            resources.ApplyResources(this.cbFilterForDocumentPeptides, "cbFilterForDocumentPeptides");
            this.cbFilterForDocumentPeptides.Name = "cbFilterForDocumentPeptides";
            this.cbFilterForDocumentPeptides.UseVisualStyleBackColor = true;
            // 
            // btnRemFile
            // 
            resources.ApplyResources(this.btnRemFile, "btnRemFile");
            this.btnRemFile.Name = "btnRemFile";
            this.btnRemFile.UseVisualStyleBackColor = true;
            this.btnRemFile.Click += new System.EventHandler(this.btnRemFile_Click);
            // 
            // listSearchFiles
            // 
            resources.ApplyResources(this.listSearchFiles, "listSearchFiles");
            this.listSearchFiles.FormattingEnabled = true;
            this.listSearchFiles.Name = "listSearchFiles";
            this.listSearchFiles.SelectedIndexChanged += new System.EventHandler(this.listSearchFiles_SelectedIndexChanged);
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // btnAddFile
            // 
            resources.ApplyResources(this.btnAddFile, "btnAddFile");
            this.btnAddFile.Name = "btnAddFile";
            this.btnAddFile.UseVisualStyleBackColor = true;
            this.btnAddFile.Click += new System.EventHandler(this.btnAddFile_Click);
            // 
            // BuildPeptideSearchLibraryControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.peptideSearchSplitContainer);
            this.Name = "BuildPeptideSearchLibraryControl";
            this.peptideSearchSplitContainer.Panel1.ResumeLayout(false);
            this.peptideSearchSplitContainer.Panel1.PerformLayout();
            this.peptideSearchSplitContainer.Panel2.ResumeLayout(false);
            this.peptideSearchSplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.peptideSearchSplitContainer)).EndInit();
            this.peptideSearchSplitContainer.ResumeLayout(false);
            this.grpWorkflow.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer peptideSearchSplitContainer;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textCutoff;
        private System.Windows.Forms.Button btnRemFile;
        private System.Windows.Forms.ListBox listSearchFiles;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button btnAddFile;
        private System.Windows.Forms.CheckBox cbFilterForDocumentPeptides;
        private System.Windows.Forms.GroupBox grpWorkflow;
        private System.Windows.Forms.RadioButton radioDDA;
        private System.Windows.Forms.RadioButton radioDIA;
        private System.Windows.Forms.RadioButton radioPRM;
        private System.Windows.Forms.CheckBox cbIncludeAmbiguousMatches;

    }
}
