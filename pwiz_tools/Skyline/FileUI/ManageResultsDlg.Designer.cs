namespace pwiz.Skyline.FileUI
{
    partial class ManageResultsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ManageResultsDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnDown = new System.Windows.Forms.Button();
            this.btnUp = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnRemoveAll = new System.Windows.Forms.Button();
            this.btnRename = new System.Windows.Forms.Button();
            this.listResults = new System.Windows.Forms.ListBox();
            this.btnReimport = new System.Windows.Forms.Button();
            this.btnMinimize = new System.Windows.Forms.Button();
            this.btnRescore = new System.Windows.Forms.Button();
            this.manageResultsTabControl = new System.Windows.Forms.TabControl();
            this.replicatesTab = new System.Windows.Forms.TabPage();
            this.checkBoxRemoveLibraryRuns = new System.Windows.Forms.CheckBox();
            this.libRunsTab = new System.Windows.Forms.TabPage();
            this.checkBoxRemoveReplicates = new System.Windows.Forms.CheckBox();
            this.btnRemoveLibRun = new System.Windows.Forms.Button();
            this.btnRemoveAllLibs = new System.Windows.Forms.Button();
            this.listLibraries = new System.Windows.Forms.ListBox();
            this.manageResultsTabControl.SuspendLayout();
            this.replicatesTab.SuspendLayout();
            this.libRunsTab.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnDown
            // 
            resources.ApplyResources(this.btnDown, "btnDown");
            this.btnDown.Name = "btnDown";
            this.btnDown.UseVisualStyleBackColor = true;
            this.btnDown.Click += new System.EventHandler(this.btnDown_Click);
            // 
            // btnUp
            // 
            resources.ApplyResources(this.btnUp, "btnUp");
            this.btnUp.Name = "btnUp";
            this.btnUp.UseVisualStyleBackColor = true;
            this.btnUp.Click += new System.EventHandler(this.btnUp_Click);
            // 
            // btnRemove
            // 
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnRemoveAll
            // 
            resources.ApplyResources(this.btnRemoveAll, "btnRemoveAll");
            this.btnRemoveAll.Name = "btnRemoveAll";
            this.btnRemoveAll.UseVisualStyleBackColor = true;
            this.btnRemoveAll.Click += new System.EventHandler(this.btnRemoveAll_Click);
            // 
            // btnRename
            // 
            resources.ApplyResources(this.btnRename, "btnRename");
            this.btnRename.Name = "btnRename";
            this.btnRename.UseVisualStyleBackColor = true;
            this.btnRename.Click += new System.EventHandler(this.btnRename_Click);
            // 
            // listResults
            // 
            resources.ApplyResources(this.listResults, "listResults");
            this.listResults.FormattingEnabled = true;
            this.listResults.Name = "listResults";
            this.listResults.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listResults.SelectedIndexChanged += new System.EventHandler(this.listResults_SelectedIndexChanged);
            this.listResults.DoubleClick += new System.EventHandler(this.listResults_DoubleClick);
            // 
            // btnReimport
            // 
            resources.ApplyResources(this.btnReimport, "btnReimport");
            this.btnReimport.Name = "btnReimport";
            this.btnReimport.UseVisualStyleBackColor = true;
            this.btnReimport.Click += new System.EventHandler(this.btnReimport_Click);
            // 
            // btnMinimize
            // 
            resources.ApplyResources(this.btnMinimize, "btnMinimize");
            this.btnMinimize.Name = "btnMinimize";
            this.btnMinimize.UseVisualStyleBackColor = true;
            this.btnMinimize.Click += new System.EventHandler(this.btnMinimize_Click);
            // 
            // btnRescore
            // 
            resources.ApplyResources(this.btnRescore, "btnRescore");
            this.btnRescore.Name = "btnRescore";
            this.btnRescore.UseVisualStyleBackColor = true;
            this.btnRescore.Click += new System.EventHandler(this.btnRescore_Click);
            // 
            // manageResultsTabControl
            // 
            resources.ApplyResources(this.manageResultsTabControl, "manageResultsTabControl");
            this.manageResultsTabControl.Controls.Add(this.replicatesTab);
            this.manageResultsTabControl.Controls.Add(this.libRunsTab);
            this.manageResultsTabControl.Name = "manageResultsTabControl";
            this.manageResultsTabControl.SelectedIndex = 0;
            // 
            // replicatesTab
            // 
            this.replicatesTab.Controls.Add(this.checkBoxRemoveLibraryRuns);
            this.replicatesTab.Controls.Add(this.listResults);
            this.replicatesTab.Controls.Add(this.btnRescore);
            this.replicatesTab.Controls.Add(this.btnRemove);
            this.replicatesTab.Controls.Add(this.btnMinimize);
            this.replicatesTab.Controls.Add(this.btnUp);
            this.replicatesTab.Controls.Add(this.btnReimport);
            this.replicatesTab.Controls.Add(this.btnDown);
            this.replicatesTab.Controls.Add(this.btnRename);
            this.replicatesTab.Controls.Add(this.btnRemoveAll);
            resources.ApplyResources(this.replicatesTab, "replicatesTab");
            this.replicatesTab.Name = "replicatesTab";
            this.replicatesTab.UseVisualStyleBackColor = true;
            // 
            // checkBoxRemoveLibraryRuns
            // 
            resources.ApplyResources(this.checkBoxRemoveLibraryRuns, "checkBoxRemoveLibraryRuns");
            this.checkBoxRemoveLibraryRuns.Name = "checkBoxRemoveLibraryRuns";
            this.checkBoxRemoveLibraryRuns.UseVisualStyleBackColor = true;
            // 
            // libRunsTab
            // 
            this.libRunsTab.Controls.Add(this.checkBoxRemoveReplicates);
            this.libRunsTab.Controls.Add(this.btnRemoveLibRun);
            this.libRunsTab.Controls.Add(this.btnRemoveAllLibs);
            this.libRunsTab.Controls.Add(this.listLibraries);
            resources.ApplyResources(this.libRunsTab, "libRunsTab");
            this.libRunsTab.Name = "libRunsTab";
            this.libRunsTab.UseVisualStyleBackColor = true;
            // 
            // checkBoxRemoveReplicates
            // 
            resources.ApplyResources(this.checkBoxRemoveReplicates, "checkBoxRemoveReplicates");
            this.checkBoxRemoveReplicates.Name = "checkBoxRemoveReplicates";
            this.checkBoxRemoveReplicates.UseVisualStyleBackColor = true;
            // 
            // btnRemoveLibRun
            // 
            resources.ApplyResources(this.btnRemoveLibRun, "btnRemoveLibRun");
            this.btnRemoveLibRun.Name = "btnRemoveLibRun";
            this.btnRemoveLibRun.UseVisualStyleBackColor = true;
            this.btnRemoveLibRun.Click += new System.EventHandler(this.btnRemoveLibRun_Click);
            // 
            // btnRemoveAllLibs
            // 
            resources.ApplyResources(this.btnRemoveAllLibs, "btnRemoveAllLibs");
            this.btnRemoveAllLibs.Name = "btnRemoveAllLibs";
            this.btnRemoveAllLibs.UseVisualStyleBackColor = true;
            this.btnRemoveAllLibs.Click += new System.EventHandler(this.btnRemoveAllLibs_Click);
            // 
            // listLibraries
            // 
            resources.ApplyResources(this.listLibraries, "listLibraries");
            this.listLibraries.FormattingEnabled = true;
            this.listLibraries.Name = "listLibraries";
            this.listLibraries.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listLibraries.SelectedIndexChanged += new System.EventHandler(this.listLibraries_SelectedIndexChanged);
            // 
            // ManageResultsDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.manageResultsTabControl);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ManageResultsDlg";
            this.ShowInTaskbar = false;
            this.manageResultsTabControl.ResumeLayout(false);
            this.replicatesTab.ResumeLayout(false);
            this.replicatesTab.PerformLayout();
            this.libRunsTab.ResumeLayout(false);
            this.libRunsTab.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnDown;
        private System.Windows.Forms.Button btnUp;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnRemoveAll;
        private System.Windows.Forms.Button btnRename;
        private System.Windows.Forms.ListBox listResults;
        private System.Windows.Forms.Button btnReimport;
        private System.Windows.Forms.Button btnMinimize;
        private System.Windows.Forms.Button btnRescore;
        private System.Windows.Forms.TabControl manageResultsTabControl;
        private System.Windows.Forms.TabPage replicatesTab;
        private System.Windows.Forms.TabPage libRunsTab;
        private System.Windows.Forms.CheckBox checkBoxRemoveLibraryRuns;
        private System.Windows.Forms.Button btnRemoveLibRun;
        private System.Windows.Forms.Button btnRemoveAllLibs;
        private System.Windows.Forms.ListBox listLibraries;
        private System.Windows.Forms.CheckBox checkBoxRemoveReplicates;
    }
}
