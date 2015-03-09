namespace pwiz.Topograph.ui.Forms
{
    partial class AddSearchResultsForm
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
            this.tbxStatus = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.lblSearchResults = new System.Windows.Forms.Label();
            this.btnChooseSearchResults = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBoxBiblioSpec = new System.Windows.Forms.GroupBox();
            this.btnImportLibrary = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBoxBiblioSpec.SuspendLayout();
            this.SuspendLayout();
            // 
            // tbxStatus
            // 
            this.tbxStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxStatus.Location = new System.Drawing.Point(12, 214);
            this.tbxStatus.Multiline = true;
            this.tbxStatus.Name = "tbxStatus";
            this.tbxStatus.ReadOnly = true;
            this.tbxStatus.Size = new System.Drawing.Size(363, 18);
            this.tbxStatus.TabIndex = 1;
            this.tbxStatus.TabStop = false;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(310, 264);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(65, 27);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.BtnCancelClick);
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(12, 238);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(363, 20);
            this.progressBar.TabIndex = 2;
            // 
            // lblSearchResults
            // 
            this.lblSearchResults.AutoSize = true;
            this.lblSearchResults.Location = new System.Drawing.Point(6, 16);
            this.lblSearchResults.Name = "lblSearchResults";
            this.lblSearchResults.Size = new System.Drawing.Size(289, 13);
            this.lblSearchResults.TabIndex = 0;
            this.lblSearchResults.Text = "Topograph can read peptide search results in many formats.";
            // 
            // btnChooseSearchResults
            // 
            this.btnChooseSearchResults.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnChooseSearchResults.AutoSize = true;
            this.btnChooseSearchResults.Location = new System.Drawing.Point(233, 71);
            this.btnChooseSearchResults.Name = "btnChooseSearchResults";
            this.btnChooseSearchResults.Size = new System.Drawing.Size(123, 23);
            this.btnChooseSearchResults.TabIndex = 1;
            this.btnChooseSearchResults.Text = "Choose search results...";
            this.btnChooseSearchResults.UseVisualStyleBackColor = true;
            this.btnChooseSearchResults.Click += new System.EventHandler(this.BtnChooseSearchResultsClick);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.btnChooseSearchResults);
            this.groupBox1.Controls.Add(this.lblSearchResults);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(363, 100);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Read Search Results";
            // 
            // groupBoxBiblioSpec
            // 
            this.groupBoxBiblioSpec.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxBiblioSpec.Controls.Add(this.btnImportLibrary);
            this.groupBoxBiblioSpec.Controls.Add(this.label2);
            this.groupBoxBiblioSpec.Location = new System.Drawing.Point(12, 118);
            this.groupBoxBiblioSpec.Name = "groupBoxBiblioSpec";
            this.groupBoxBiblioSpec.Size = new System.Drawing.Size(363, 85);
            this.groupBoxBiblioSpec.TabIndex = 1;
            this.groupBoxBiblioSpec.TabStop = false;
            this.groupBoxBiblioSpec.Text = "BiblioSpec spectral library";
            // 
            // btnImportLibrary
            // 
            this.btnImportLibrary.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnImportLibrary.AutoSize = true;
            this.btnImportLibrary.Location = new System.Drawing.Point(218, 56);
            this.btnImportLibrary.Name = "btnImportLibrary";
            this.btnImportLibrary.Size = new System.Drawing.Size(138, 23);
            this.btnImportLibrary.TabIndex = 1;
            this.btnImportLibrary.Text = "Import BiblioSpec library...";
            this.btnImportLibrary.UseVisualStyleBackColor = true;
            this.btnImportLibrary.Click += new System.EventHandler(this.BtnImportLibraryClick);
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(6, 16);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(351, 27);
            this.label2.TabIndex = 0;
            this.label2.Text = "If you have a BiblioSpec spectral library file, you can import the retention time" +
                "s";
            // 
            // AddSearchResultsForm
            // 
            this.AcceptButton = this.btnChooseSearchResults;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(384, 303);
            this.Controls.Add(this.groupBoxBiblioSpec);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tbxStatus);
            this.Controls.Add(this.groupBox1);
            this.Name = "AddSearchResultsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "Add Search Results";
            this.Text = "Add Search Results";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBoxBiblioSpec.ResumeLayout(false);
            this.groupBoxBiblioSpec.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox tbxStatus;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label lblSearchResults;
        private System.Windows.Forms.Button btnChooseSearchResults;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBoxBiblioSpec;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnImportLibrary;
    }
}