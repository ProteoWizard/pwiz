namespace KeepResxW
{
    partial class KeepResxForm
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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.sendingPage = new System.Windows.Forms.TabPage();
            this.doneLabel = new System.Windows.Forms.Label();
            this.versionLabel = new System.Windows.Forms.Label();
            this.versionBox = new System.Windows.Forms.TextBox();
            this.languageCheckList = new System.Windows.Forms.CheckedListBox();
            this.outLanguageLabel = new System.Windows.Forms.Label();
            this.browse = new System.Windows.Forms.Button();
            this.selectFolderLabel = new System.Windows.Forms.Label();
            this.selectedPathBox = new System.Windows.Forms.TextBox();
            this.receivingPage = new System.Windows.Forms.TabPage();
            this.filesChangedLabel = new System.Windows.Forms.Label();
            this.fileChangesView = new System.Windows.Forms.ListView();
            this.receivingProgress = new System.Windows.Forms.ProgressBar();
            this.finishedReceiving = new System.Windows.Forms.Label();
            this.receivingProjBrowse = new System.Windows.Forms.Button();
            this.receivingProjBox = new System.Windows.Forms.TextBox();
            this.receivingProjLabel = new System.Windows.Forms.Label();
            this.expectedZH = new System.Windows.Forms.RadioButton();
            this.expectedLang = new System.Windows.Forms.Label();
            this.expectedJa = new System.Windows.Forms.RadioButton();
            this.receivingZipBrowse = new System.Windows.Forms.Button();
            this.zipLocation = new System.Windows.Forms.TextBox();
            this.zipLocLabel = new System.Windows.Forms.Label();
            this.sendingButton = new System.Windows.Forms.RadioButton();
            this.receivingButton = new System.Windows.Forms.RadioButton();
            this.confirmButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.tabControl1.SuspendLayout();
            this.sendingPage.SuspendLayout();
            this.receivingPage.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Appearance = System.Windows.Forms.TabAppearance.Buttons;
            this.tabControl1.Controls.Add(this.sendingPage);
            this.tabControl1.Controls.Add(this.receivingPage);
            this.tabControl1.ItemSize = new System.Drawing.Size(100, 25);
            this.tabControl1.Location = new System.Drawing.Point(18, 42);
            this.tabControl1.Multiline = true;
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(455, 616);
            this.tabControl1.TabIndex = 2;
            // 
            // sendingPage
            // 
            this.sendingPage.Controls.Add(this.doneLabel);
            this.sendingPage.Controls.Add(this.versionLabel);
            this.sendingPage.Controls.Add(this.versionBox);
            this.sendingPage.Controls.Add(this.languageCheckList);
            this.sendingPage.Controls.Add(this.outLanguageLabel);
            this.sendingPage.Controls.Add(this.browse);
            this.sendingPage.Controls.Add(this.selectFolderLabel);
            this.sendingPage.Controls.Add(this.selectedPathBox);
            this.sendingPage.Location = new System.Drawing.Point(4, 29);
            this.sendingPage.Name = "sendingPage";
            this.sendingPage.Padding = new System.Windows.Forms.Padding(3);
            this.sendingPage.Size = new System.Drawing.Size(447, 583);
            this.sendingPage.TabIndex = 0;
            this.sendingPage.Text = "sending";
            this.sendingPage.UseVisualStyleBackColor = true;
            // 
            // doneLabel
            // 
            this.doneLabel.AutoSize = true;
            this.doneLabel.Location = new System.Drawing.Point(6, 282);
            this.doneLabel.Name = "doneLabel";
            this.doneLabel.Size = new System.Drawing.Size(0, 20);
            this.doneLabel.TabIndex = 7;
            // 
            // versionLabel
            // 
            this.versionLabel.AutoSize = true;
            this.versionLabel.Location = new System.Drawing.Point(6, 77);
            this.versionLabel.Name = "versionLabel";
            this.versionLabel.Size = new System.Drawing.Size(125, 20);
            this.versionLabel.TabIndex = 3;
            this.versionLabel.Text = "Version number:";
            // 
            // versionBox
            // 
            this.versionBox.Location = new System.Drawing.Point(10, 100);
            this.versionBox.Name = "versionBox";
            this.versionBox.Size = new System.Drawing.Size(166, 26);
            this.versionBox.TabIndex = 4;
            // 
            // languageCheckList
            // 
            this.languageCheckList.CheckOnClick = true;
            this.languageCheckList.FormattingEnabled = true;
            this.languageCheckList.Items.AddRange(new object[] {
            "Chinese",
            "Japanese",
            "English"});
            this.languageCheckList.Location = new System.Drawing.Point(10, 173);
            this.languageCheckList.Name = "languageCheckList";
            this.languageCheckList.Size = new System.Drawing.Size(226, 88);
            this.languageCheckList.TabIndex = 6;
            // 
            // outLanguageLabel
            // 
            this.outLanguageLabel.AutoSize = true;
            this.outLanguageLabel.Location = new System.Drawing.Point(6, 150);
            this.outLanguageLabel.Name = "outLanguageLabel";
            this.outLanguageLabel.Size = new System.Drawing.Size(132, 20);
            this.outLanguageLabel.TabIndex = 5;
            this.outLanguageLabel.Text = "Output language:";
            // 
            // browse
            // 
            this.browse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.browse.Location = new System.Drawing.Point(349, 30);
            this.browse.Name = "browse";
            this.browse.Size = new System.Drawing.Size(92, 33);
            this.browse.TabIndex = 2;
            this.browse.Text = "Browse...";
            this.browse.UseVisualStyleBackColor = true;
            this.browse.Click += new System.EventHandler(this.browse_Click);
            // 
            // selectFolderLabel
            // 
            this.selectFolderLabel.AutoSize = true;
            this.selectFolderLabel.Location = new System.Drawing.Point(6, 10);
            this.selectFolderLabel.Name = "selectFolderLabel";
            this.selectFolderLabel.Size = new System.Drawing.Size(106, 20);
            this.selectFolderLabel.TabIndex = 0;
            this.selectFolderLabel.Text = "Project folder:";
            // 
            // selectedPathBox
            // 
            this.selectedPathBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.selectedPathBox.Location = new System.Drawing.Point(10, 33);
            this.selectedPathBox.Name = "selectedPathBox";
            this.selectedPathBox.Size = new System.Drawing.Size(333, 26);
            this.selectedPathBox.TabIndex = 1;
            // 
            // receivingPage
            // 
            this.receivingPage.Controls.Add(this.filesChangedLabel);
            this.receivingPage.Controls.Add(this.fileChangesView);
            this.receivingPage.Controls.Add(this.receivingProgress);
            this.receivingPage.Controls.Add(this.finishedReceiving);
            this.receivingPage.Controls.Add(this.receivingProjBrowse);
            this.receivingPage.Controls.Add(this.receivingProjBox);
            this.receivingPage.Controls.Add(this.receivingProjLabel);
            this.receivingPage.Controls.Add(this.expectedZH);
            this.receivingPage.Controls.Add(this.expectedLang);
            this.receivingPage.Controls.Add(this.expectedJa);
            this.receivingPage.Controls.Add(this.receivingZipBrowse);
            this.receivingPage.Controls.Add(this.zipLocation);
            this.receivingPage.Controls.Add(this.zipLocLabel);
            this.receivingPage.Location = new System.Drawing.Point(4, 29);
            this.receivingPage.Name = "receivingPage";
            this.receivingPage.Padding = new System.Windows.Forms.Padding(3);
            this.receivingPage.Size = new System.Drawing.Size(447, 583);
            this.receivingPage.TabIndex = 1;
            this.receivingPage.Text = "receiving";
            this.receivingPage.UseVisualStyleBackColor = true;
            // 
            // filesChangedLabel
            // 
            this.filesChangedLabel.AutoSize = true;
            this.filesChangedLabel.Location = new System.Drawing.Point(6, 317);
            this.filesChangedLabel.Name = "filesChangedLabel";
            this.filesChangedLabel.Size = new System.Drawing.Size(0, 20);
            this.filesChangedLabel.TabIndex = 12;
            this.filesChangedLabel.Visible = false;
            // 
            // fileChangesView
            // 
            this.fileChangesView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.fileChangesView.HideSelection = false;
            this.fileChangesView.Location = new System.Drawing.Point(6, 355);
            this.fileChangesView.Name = "fileChangesView";
            this.fileChangesView.Size = new System.Drawing.Size(435, 232);
            this.fileChangesView.TabIndex = 11;
            this.fileChangesView.UseCompatibleStateImageBehavior = false;
            this.fileChangesView.View = System.Windows.Forms.View.Details;
            this.fileChangesView.Visible = false;
            // 
            // receivingProgress
            // 
            this.receivingProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.receivingProgress.Location = new System.Drawing.Point(0, 248);
            this.receivingProgress.Name = "receivingProgress";
            this.receivingProgress.Size = new System.Drawing.Size(447, 23);
            this.receivingProgress.TabIndex = 9;
            this.receivingProgress.Visible = false;
            // 
            // finishedReceiving
            // 
            this.finishedReceiving.AutoSize = true;
            this.finishedReceiving.Location = new System.Drawing.Point(6, 274);
            this.finishedReceiving.Name = "finishedReceiving";
            this.finishedReceiving.Size = new System.Drawing.Size(73, 20);
            this.finishedReceiving.TabIndex = 10;
            this.finishedReceiving.Text = "Finished.";
            this.finishedReceiving.Visible = false;
            // 
            // receivingProjBrowse
            // 
            this.receivingProjBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.receivingProjBrowse.Location = new System.Drawing.Point(349, 97);
            this.receivingProjBrowse.Name = "receivingProjBrowse";
            this.receivingProjBrowse.Size = new System.Drawing.Size(92, 33);
            this.receivingProjBrowse.TabIndex = 5;
            this.receivingProjBrowse.Text = "Browse...";
            this.receivingProjBrowse.UseVisualStyleBackColor = true;
            this.receivingProjBrowse.Click += new System.EventHandler(this.receivingProjBrowse_Click);
            // 
            // receivingProjBox
            // 
            this.receivingProjBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.receivingProjBox.Location = new System.Drawing.Point(10, 100);
            this.receivingProjBox.Name = "receivingProjBox";
            this.receivingProjBox.Size = new System.Drawing.Size(333, 26);
            this.receivingProjBox.TabIndex = 4;
            // 
            // receivingProjLabel
            // 
            this.receivingProjLabel.AutoSize = true;
            this.receivingProjLabel.Location = new System.Drawing.Point(6, 77);
            this.receivingProjLabel.Name = "receivingProjLabel";
            this.receivingProjLabel.Size = new System.Drawing.Size(106, 20);
            this.receivingProjLabel.TabIndex = 3;
            this.receivingProjLabel.Text = "Project folder:";
            // 
            // expectedZH
            // 
            this.expectedZH.AutoSize = true;
            this.expectedZH.Location = new System.Drawing.Point(6, 175);
            this.expectedZH.Name = "expectedZH";
            this.expectedZH.Size = new System.Drawing.Size(92, 24);
            this.expectedZH.TabIndex = 7;
            this.expectedZH.TabStop = true;
            this.expectedZH.Text = "Chinese";
            this.expectedZH.UseVisualStyleBackColor = true;
            // 
            // expectedLang
            // 
            this.expectedLang.AutoSize = true;
            this.expectedLang.Location = new System.Drawing.Point(6, 152);
            this.expectedLang.Name = "expectedLang";
            this.expectedLang.Size = new System.Drawing.Size(326, 20);
            this.expectedLang.TabIndex = 6;
            this.expectedLang.Text = "What language are you expecting to receive?";
            // 
            // expectedJa
            // 
            this.expectedJa.AutoSize = true;
            this.expectedJa.Location = new System.Drawing.Point(6, 205);
            this.expectedJa.Name = "expectedJa";
            this.expectedJa.Size = new System.Drawing.Size(104, 24);
            this.expectedJa.TabIndex = 8;
            this.expectedJa.TabStop = true;
            this.expectedJa.Text = "Japanese";
            this.expectedJa.UseVisualStyleBackColor = true;
            // 
            // receivingZipBrowse
            // 
            this.receivingZipBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.receivingZipBrowse.Location = new System.Drawing.Point(349, 30);
            this.receivingZipBrowse.Name = "receivingZipBrowse";
            this.receivingZipBrowse.Size = new System.Drawing.Size(92, 33);
            this.receivingZipBrowse.TabIndex = 2;
            this.receivingZipBrowse.Text = "Browse...";
            this.receivingZipBrowse.UseVisualStyleBackColor = true;
            this.receivingZipBrowse.Click += new System.EventHandler(this.receivingZipBrowse_Click);
            // 
            // zipLocation
            // 
            this.zipLocation.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.zipLocation.Location = new System.Drawing.Point(10, 33);
            this.zipLocation.Name = "zipLocation";
            this.zipLocation.Size = new System.Drawing.Size(333, 26);
            this.zipLocation.TabIndex = 1;
            // 
            // zipLocLabel
            // 
            this.zipLocLabel.AutoSize = true;
            this.zipLocLabel.Location = new System.Drawing.Point(6, 10);
            this.zipLocLabel.Name = "zipLocLabel";
            this.zipLocLabel.Size = new System.Drawing.Size(59, 20);
            this.zipLocLabel.TabIndex = 0;
            this.zipLocLabel.Text = "Zip file:";
            // 
            // sendingButton
            // 
            this.sendingButton.AutoSize = true;
            this.sendingButton.Checked = true;
            this.sendingButton.Location = new System.Drawing.Point(18, 12);
            this.sendingButton.Name = "sendingButton";
            this.sendingButton.Size = new System.Drawing.Size(93, 24);
            this.sendingButton.TabIndex = 0;
            this.sendingButton.TabStop = true;
            this.sendingButton.Text = "&Sending";
            this.sendingButton.UseVisualStyleBackColor = true;
            this.sendingButton.CheckedChanged += new System.EventHandler(this.sendingButton_CheckedChanged);
            // 
            // receivingButton
            // 
            this.receivingButton.AutoSize = true;
            this.receivingButton.Location = new System.Drawing.Point(117, 12);
            this.receivingButton.Name = "receivingButton";
            this.receivingButton.Size = new System.Drawing.Size(103, 24);
            this.receivingButton.TabIndex = 1;
            this.receivingButton.Text = "&Receiving";
            this.receivingButton.UseVisualStyleBackColor = true;
            // 
            // confirmButton
            // 
            this.confirmButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.confirmButton.Location = new System.Drawing.Point(283, 664);
            this.confirmButton.Name = "confirmButton";
            this.confirmButton.Size = new System.Drawing.Size(92, 33);
            this.confirmButton.TabIndex = 3;
            this.confirmButton.Text = "Start";
            this.confirmButton.UseVisualStyleBackColor = true;
            this.confirmButton.Click += new System.EventHandler(this.confirmButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(381, 664);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(92, 33);
            this.cancelButton.TabIndex = 4;
            this.cancelButton.Text = "Close";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // KeepResxForm
            // 
            this.AcceptButton = this.confirmButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(485, 709);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.confirmButton);
            this.Controls.Add(this.sendingButton);
            this.Controls.Add(this.receivingButton);
            this.Controls.Add(this.tabControl1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "KeepResxForm";
            this.Text = "RESX for Translators";
            this.Load += new System.EventHandler(this.KeepResxForm_Load);
            this.tabControl1.ResumeLayout(false);
            this.sendingPage.ResumeLayout(false);
            this.sendingPage.PerformLayout();
            this.receivingPage.ResumeLayout(false);
            this.receivingPage.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage sendingPage;
        private System.Windows.Forms.TabPage receivingPage;
        private System.Windows.Forms.RadioButton sendingButton;
        private System.Windows.Forms.RadioButton receivingButton;
        private System.Windows.Forms.Button confirmButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button browse;
        private System.Windows.Forms.Label selectFolderLabel;
        private System.Windows.Forms.TextBox selectedPathBox;
        private System.Windows.Forms.CheckedListBox languageCheckList;
        private System.Windows.Forms.Label outLanguageLabel;
        private System.Windows.Forms.Label versionLabel;
        private System.Windows.Forms.TextBox versionBox;
        private System.Windows.Forms.Label doneLabel;
        private System.Windows.Forms.Label expectedLang;
        private System.Windows.Forms.RadioButton expectedJa;
        private System.Windows.Forms.Button receivingZipBrowse;
        private System.Windows.Forms.TextBox zipLocation;
        private System.Windows.Forms.Label zipLocLabel;
        private System.Windows.Forms.RadioButton expectedZH;
        private System.Windows.Forms.Button receivingProjBrowse;
        private System.Windows.Forms.TextBox receivingProjBox;
        private System.Windows.Forms.Label receivingProjLabel;
        private System.Windows.Forms.Label finishedReceiving;
        private System.Windows.Forms.ProgressBar receivingProgress;
        private System.Windows.Forms.ListView fileChangesView;
        private System.Windows.Forms.Label filesChangedLabel;
    }
}

