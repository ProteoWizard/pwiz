namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class ImportResultsDIAControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportResultsDIAControl));
            this.labelResultsFiles = new System.Windows.Forms.Label();
            this.listResultsFiles = new System.Windows.Forms.ListBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.cbAutoRetry = new System.Windows.Forms.CheckBox();
            this.comboSimultaneousFiles = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelResultsFiles
            // 
            resources.ApplyResources(this.labelResultsFiles, "labelResultsFiles");
            this.labelResultsFiles.Name = "labelResultsFiles";
            // 
            // listResultsFiles
            // 
            resources.ApplyResources(this.listResultsFiles, "listResultsFiles");
            this.listResultsFiles.FormattingEnabled = true;
            this.listResultsFiles.Name = "listResultsFiles";
            this.listResultsFiles.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnRemove
            // 
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // cbAutoRetry
            // 
            resources.ApplyResources(this.cbAutoRetry, "cbAutoRetry");
            this.cbAutoRetry.Name = "cbAutoRetry";
            this.cbAutoRetry.UseVisualStyleBackColor = true;
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
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // ImportResultsDIAControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.cbAutoRetry);
            this.Controls.Add(this.comboSimultaneousFiles);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.listResultsFiles);
            this.Controls.Add(this.labelResultsFiles);
            this.Name = "ImportResultsDIAControl";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelResultsFiles;
        private System.Windows.Forms.ListBox listResultsFiles;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.CheckBox cbAutoRetry;
        private System.Windows.Forms.ComboBox comboSimultaneousFiles;
        private System.Windows.Forms.Label label1;
    }
}
