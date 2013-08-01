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
            this.peptideSearchSplitContainer = new System.Windows.Forms.SplitContainer();
            this.label1 = new System.Windows.Forms.Label();
            this.textCutoff = new System.Windows.Forms.TextBox();
            this.btnRemFile = new System.Windows.Forms.Button();
            this.listSearchFiles = new System.Windows.Forms.ListBox();
            this.label7 = new System.Windows.Forms.Label();
            this.btnAddFile = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.peptideSearchSplitContainer)).BeginInit();
            this.peptideSearchSplitContainer.Panel1.SuspendLayout();
            this.peptideSearchSplitContainer.Panel2.SuspendLayout();
            this.peptideSearchSplitContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // peptideSearchSplitContainer
            // 
            this.peptideSearchSplitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.peptideSearchSplitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.peptideSearchSplitContainer.IsSplitterFixed = true;
            this.peptideSearchSplitContainer.Location = new System.Drawing.Point(0, 2);
            this.peptideSearchSplitContainer.Name = "peptideSearchSplitContainer";
            this.peptideSearchSplitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // peptideSearchSplitContainer.Panel1
            // 
            this.peptideSearchSplitContainer.Panel1.Controls.Add(this.label1);
            this.peptideSearchSplitContainer.Panel1.Controls.Add(this.textCutoff);
            // 
            // peptideSearchSplitContainer.Panel2
            // 
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.btnRemFile);
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.listSearchFiles);
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.label7);
            this.peptideSearchSplitContainer.Panel2.Controls.Add(this.btnAddFile);
            this.peptideSearchSplitContainer.Size = new System.Drawing.Size(381, 310);
            this.peptideSearchSplitContainer.SplitterDistance = 53;
            this.peptideSearchSplitContainer.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Cut-off score:";
            // 
            // textCutoff
            // 
            this.textCutoff.Location = new System.Drawing.Point(14, 26);
            this.textCutoff.Name = "textCutoff";
            this.textCutoff.Size = new System.Drawing.Size(100, 20);
            this.textCutoff.TabIndex = 1;
            // 
            // btnRemFile
            // 
            this.btnRemFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemFile.Enabled = false;
            this.btnRemFile.Location = new System.Drawing.Point(285, 54);
            this.btnRemFile.Name = "btnRemFile";
            this.btnRemFile.Size = new System.Drawing.Size(93, 23);
            this.btnRemFile.TabIndex = 3;
            this.btnRemFile.Text = "&Remove Files";
            this.btnRemFile.UseVisualStyleBackColor = true;
            this.btnRemFile.Click += new System.EventHandler(this.btnRemFile_Click);
            // 
            // listSearchFiles
            // 
            this.listSearchFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listSearchFiles.FormattingEnabled = true;
            this.listSearchFiles.Location = new System.Drawing.Point(14, 25);
            this.listSearchFiles.Name = "listSearchFiles";
            this.listSearchFiles.Size = new System.Drawing.Size(265, 212);
            this.listSearchFiles.TabIndex = 1;
            this.listSearchFiles.SelectedIndexChanged += new System.EventHandler(this.listSearchFiles_SelectedIndexChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(14, 6);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(65, 13);
            this.label7.TabIndex = 0;
            this.label7.Text = "&Search files:";
            // 
            // btnAddFile
            // 
            this.btnAddFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddFile.Location = new System.Drawing.Point(285, 25);
            this.btnAddFile.Name = "btnAddFile";
            this.btnAddFile.Size = new System.Drawing.Size(93, 23);
            this.btnAddFile.TabIndex = 2;
            this.btnAddFile.Text = "&Add Files...";
            this.btnAddFile.UseVisualStyleBackColor = true;
            this.btnAddFile.Click += new System.EventHandler(this.btnAddFile_Click);
            // 
            // BuildPeptideSearchLibraryControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.peptideSearchSplitContainer);
            this.Name = "BuildPeptideSearchLibraryControl";
            this.Size = new System.Drawing.Size(381, 315);
            this.peptideSearchSplitContainer.Panel1.ResumeLayout(false);
            this.peptideSearchSplitContainer.Panel1.PerformLayout();
            this.peptideSearchSplitContainer.Panel2.ResumeLayout(false);
            this.peptideSearchSplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.peptideSearchSplitContainer)).EndInit();
            this.peptideSearchSplitContainer.ResumeLayout(false);
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

    }
}
