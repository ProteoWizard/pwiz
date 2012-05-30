namespace pwiz.Skyline.SettingsUI
{
    partial class BuildBackgroundProteomeDlg
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
            this.btnBrowse = new System.Windows.Forms.Button();
            this.labelFile = new System.Windows.Forms.Label();
            this.textPath = new System.Windows.Forms.TextBox();
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnAddFastaFile = new System.Windows.Forms.Button();
            this.tbxStatus = new System.Windows.Forms.TextBox();
            this.btnBuild = new System.Windows.Forms.Button();
            this.labelFasta = new System.Windows.Forms.Label();
            this.listboxFasta = new System.Windows.Forms.ListBox();
            this.labelFileNew = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnBrowse
            // 
            this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowse.Location = new System.Drawing.Point(263, 73);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(75, 23);
            this.btnBrowse.TabIndex = 5;
            this.btnBrowse.Text = "&Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // labelFile
            // 
            this.labelFile.AutoSize = true;
            this.labelFile.Location = new System.Drawing.Point(7, 60);
            this.labelFile.Name = "labelFile";
            this.labelFile.Size = new System.Drawing.Size(71, 13);
            this.labelFile.TabIndex = 2;
            this.labelFile.Text = "&Proteome file:";
            // 
            // textPath
            // 
            this.textPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textPath.Location = new System.Drawing.Point(10, 76);
            this.textPath.Name = "textPath";
            this.textPath.Size = new System.Drawing.Size(247, 20);
            this.textPath.TabIndex = 4;
            this.textPath.TextChanged += new System.EventHandler(this.textPath_TextChanged);
            // 
            // textName
            // 
            this.textName.Location = new System.Drawing.Point(10, 28);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(158, 20);
            this.textName.TabIndex = 1;
            this.textName.TextChanged += new System.EventHandler(this.textName_TextChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(7, 12);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Name:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(356, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 12;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(356, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 11;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnAddFastaFile
            // 
            this.btnAddFastaFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddFastaFile.Location = new System.Drawing.Point(356, 174);
            this.btnAddFastaFile.Name = "btnAddFastaFile";
            this.btnAddFastaFile.Size = new System.Drawing.Size(75, 24);
            this.btnAddFastaFile.TabIndex = 10;
            this.btnAddFastaFile.Text = "&Add File...";
            this.btnAddFastaFile.UseVisualStyleBackColor = true;
            this.btnAddFastaFile.Click += new System.EventHandler(this.btnAddFastaFile_Click);
            // 
            // tbxStatus
            // 
            this.tbxStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxStatus.Location = new System.Drawing.Point(10, 102);
            this.tbxStatus.Multiline = true;
            this.tbxStatus.Name = "tbxStatus";
            this.tbxStatus.ReadOnly = true;
            this.tbxStatus.Size = new System.Drawing.Size(328, 38);
            this.tbxStatus.TabIndex = 6;
            // 
            // btnBuild
            // 
            this.btnBuild.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBuild.Location = new System.Drawing.Point(356, 117);
            this.btnBuild.Name = "btnBuild";
            this.btnBuild.Size = new System.Drawing.Size(75, 23);
            this.btnBuild.TabIndex = 7;
            this.btnBuild.Text = "B&uild <<";
            this.btnBuild.UseVisualStyleBackColor = true;
            this.btnBuild.Click += new System.EventHandler(this.btnBuild_Click);
            // 
            // labelFasta
            // 
            this.labelFasta.AutoSize = true;
            this.labelFasta.Location = new System.Drawing.Point(7, 157);
            this.labelFasta.Name = "labelFasta";
            this.labelFasta.Size = new System.Drawing.Size(65, 13);
            this.labelFasta.TabIndex = 8;
            this.labelFasta.Text = "&FASTA files:";
            // 
            // listboxFasta
            // 
            this.listboxFasta.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.listboxFasta.FormattingEnabled = true;
            this.listboxFasta.Location = new System.Drawing.Point(10, 174);
            this.listboxFasta.Name = "listboxFasta";
            this.listboxFasta.Size = new System.Drawing.Size(328, 95);
            this.listboxFasta.TabIndex = 9;
            // 
            // labelFileNew
            // 
            this.labelFileNew.AutoSize = true;
            this.labelFileNew.Location = new System.Drawing.Point(152, 60);
            this.labelFileNew.Name = "labelFileNew";
            this.labelFileNew.Size = new System.Drawing.Size(105, 13);
            this.labelFileNew.TabIndex = 3;
            this.labelFileNew.Text = "&Output proteome file:";
            this.labelFileNew.Visible = false;
            // 
            // BuildBackgroundProteomeDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(443, 289);
            this.Controls.Add(this.labelFileNew);
            this.Controls.Add(this.listboxFasta);
            this.Controls.Add(this.labelFasta);
            this.Controls.Add(this.btnBuild);
            this.Controls.Add(this.tbxStatus);
            this.Controls.Add(this.btnAddFastaFile);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.labelFile);
            this.Controls.Add(this.textPath);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BuildBackgroundProteomeDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Background Proteome";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label labelFile;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnAddFastaFile;
        private System.Windows.Forms.TextBox tbxStatus;
        private System.Windows.Forms.Button btnBuild;
        private System.Windows.Forms.Label labelFasta;
        private System.Windows.Forms.ListBox listboxFasta;
        private System.Windows.Forms.Label labelFileNew;

    }
}