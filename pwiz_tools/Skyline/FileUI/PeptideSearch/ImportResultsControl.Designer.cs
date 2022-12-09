namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class ImportResultsControl
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportResultsControl));
            this.resultsSplitContainer = new System.Windows.Forms.SplitContainer();
            this.label2 = new System.Windows.Forms.Label();
            this.listResultsFilesFound = new System.Windows.Forms.ListBox();
            this.browseToResultsFileButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.findResultsFilesButton = new System.Windows.Forms.Button();
            this.listResultsFilesMissing = new System.Windows.Forms.ListBox();
            this.cbExcludeSourceFiles = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.comboSimultaneousFiles = new System.Windows.Forms.ComboBox();
            this.cbAutoRetry = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.resultsSplitContainer)).BeginInit();
            this.resultsSplitContainer.Panel1.SuspendLayout();
            this.resultsSplitContainer.Panel2.SuspendLayout();
            this.resultsSplitContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // resultsSplitContainer
            // 
            resources.ApplyResources(this.resultsSplitContainer, "resultsSplitContainer");
            this.resultsSplitContainer.Name = "resultsSplitContainer";
            // 
            // resultsSplitContainer.Panel1
            // 
            this.resultsSplitContainer.Panel1.Controls.Add(this.label2);
            this.resultsSplitContainer.Panel1.Controls.Add(this.listResultsFilesFound);
            this.resultsSplitContainer.Panel1.Controls.Add(this.cbExcludeSourceFiles);
            // 
            // resultsSplitContainer.Panel2
            // 
            this.resultsSplitContainer.Panel2.Controls.Add(this.browseToResultsFileButton);
            this.resultsSplitContainer.Panel2.Controls.Add(this.label3);
            this.resultsSplitContainer.Panel2.Controls.Add(this.findResultsFilesButton);
            this.resultsSplitContainer.Panel2.Controls.Add(this.listResultsFilesMissing);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // listResultsFilesFound
            // 
            resources.ApplyResources(this.listResultsFilesFound, "listResultsFilesFound");
            this.listResultsFilesFound.FormattingEnabled = true;
            this.listResultsFilesFound.Name = "listResultsFilesFound";
            // 
            // browseToResultsFileButton
            // 
            resources.ApplyResources(this.browseToResultsFileButton, "browseToResultsFileButton");
            this.browseToResultsFileButton.Name = "browseToResultsFileButton";
            this.browseToResultsFileButton.UseVisualStyleBackColor = true;
            this.browseToResultsFileButton.Click += new System.EventHandler(this.browseToResultsFileButton_Click);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // findResultsFilesButton
            // 
            resources.ApplyResources(this.findResultsFilesButton, "findResultsFilesButton");
            this.findResultsFilesButton.Name = "findResultsFilesButton";
            this.findResultsFilesButton.UseVisualStyleBackColor = true;
            this.findResultsFilesButton.Click += new System.EventHandler(this.findResultsFilesButton_Click);
            // 
            // listResultsFilesMissing
            // 
            resources.ApplyResources(this.listResultsFilesMissing, "listResultsFilesMissing");
            this.listResultsFilesMissing.FormattingEnabled = true;
            this.listResultsFilesMissing.Name = "listResultsFilesMissing";
            // 
            // cbExcludeSourceFiles
            // 
            resources.ApplyResources(this.cbExcludeSourceFiles, "cbExcludeSourceFiles");
            this.cbExcludeSourceFiles.Name = "cbExcludeSourceFiles";
            this.toolTip1.SetToolTip(this.cbExcludeSourceFiles, resources.GetString("cbExcludeSourceFiles.ToolTip"));
            this.cbExcludeSourceFiles.UseVisualStyleBackColor = true;
            this.cbExcludeSourceFiles.CheckedChanged += new System.EventHandler(this.cbExcludeSourceFiles_CheckedChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // comboSimultaneousFiles
            // 
            resources.ApplyResources(this.comboSimultaneousFiles, "comboSimultaneousFiles");
            this.comboSimultaneousFiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSimultaneousFiles.FormattingEnabled = true;
            this.comboSimultaneousFiles.Items.AddRange(new object[] {
            resources.GetString("comboSimultaneousFiles.Items"),
            resources.GetString("comboSimultaneousFiles.Items1"),
            resources.GetString("comboSimultaneousFiles.Items2")});
            this.comboSimultaneousFiles.Name = "comboSimultaneousFiles";
            // 
            // cbAutoRetry
            // 
            resources.ApplyResources(this.cbAutoRetry, "cbAutoRetry");
            this.cbAutoRetry.Name = "cbAutoRetry";
            this.cbAutoRetry.UseVisualStyleBackColor = true;
            // 
            // ImportResultsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.cbAutoRetry);
            this.Controls.Add(this.comboSimultaneousFiles);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.resultsSplitContainer);
            this.Name = "ImportResultsControl";
            this.resultsSplitContainer.Panel1.ResumeLayout(false);
            this.resultsSplitContainer.Panel1.PerformLayout();
            this.resultsSplitContainer.Panel2.ResumeLayout(false);
            this.resultsSplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.resultsSplitContainer)).EndInit();
            this.resultsSplitContainer.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer resultsSplitContainer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ListBox listResultsFilesFound;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button findResultsFilesButton;
        private System.Windows.Forms.ListBox listResultsFilesMissing;
        private System.Windows.Forms.Button browseToResultsFileButton;
        private System.Windows.Forms.CheckBox cbExcludeSourceFiles;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboSimultaneousFiles;
        private System.Windows.Forms.CheckBox cbAutoRetry;
    }
}
