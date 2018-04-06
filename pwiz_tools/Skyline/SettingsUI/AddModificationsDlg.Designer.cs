namespace pwiz.Skyline.SettingsUI
{
    partial class AddModificationsDlg
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddModificationsDlg));
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.listMatched = new System.Windows.Forms.CheckedListBox();
            this.btnAdd = new System.Windows.Forms.Button();
            this.menuAdd = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addSelectedModificationsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addStructuralModificationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addHeavyModificationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.cbSelectAll = new System.Windows.Forms.CheckBox();
            this.listUnmatched = new System.Windows.Forms.ListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.menuAdd.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // listMatched
            // 
            resources.ApplyResources(this.listMatched, "listMatched");
            this.listMatched.CheckOnClick = true;
            this.listMatched.FormattingEnabled = true;
            this.listMatched.Name = "listMatched";
            this.listMatched.Sorted = true;
            this.listMatched.ThreeDCheckBoxes = true;
            // 
            // btnAdd
            // 
            resources.ApplyResources(this.btnAdd, "btnAdd");
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // menuAdd
            // 
            this.menuAdd.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addSelectedModificationsToolStripMenuItem,
            this.addStructuralModificationToolStripMenuItem,
            this.addHeavyModificationToolStripMenuItem});
            this.menuAdd.Name = "menuAdd";
            resources.ApplyResources(this.menuAdd, "menuAdd");
            // 
            // addSelectedModificationsToolStripMenuItem
            // 
            this.addSelectedModificationsToolStripMenuItem.Name = "addSelectedModificationsToolStripMenuItem";
            resources.ApplyResources(this.addSelectedModificationsToolStripMenuItem, "addSelectedModificationsToolStripMenuItem");
            this.addSelectedModificationsToolStripMenuItem.Click += new System.EventHandler(this.addSelectedModificationsToolStripMenuItem_Click);
            // 
            // addStructuralModificationToolStripMenuItem
            // 
            this.addStructuralModificationToolStripMenuItem.Name = "addStructuralModificationToolStripMenuItem";
            resources.ApplyResources(this.addStructuralModificationToolStripMenuItem, "addStructuralModificationToolStripMenuItem");
            this.addStructuralModificationToolStripMenuItem.Click += new System.EventHandler(this.addStructuralModificationToolStripMenuItem_Click);
            // 
            // addHeavyModificationToolStripMenuItem
            // 
            this.addHeavyModificationToolStripMenuItem.Name = "addHeavyModificationToolStripMenuItem";
            resources.ApplyResources(this.addHeavyModificationToolStripMenuItem, "addHeavyModificationToolStripMenuItem");
            this.addHeavyModificationToolStripMenuItem.Click += new System.EventHandler(this.addHeavyModificationToolStripMenuItem_Click);
            // 
            // splitContainer
            // 
            resources.ApplyResources(this.splitContainer, "splitContainer");
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.cbSelectAll);
            this.splitContainer.Panel1.Controls.Add(this.listMatched);
            this.splitContainer.Panel1.Controls.Add(this.label1);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.listUnmatched);
            this.splitContainer.Panel2.Controls.Add(this.label2);
            // 
            // cbSelectAll
            // 
            resources.ApplyResources(this.cbSelectAll, "cbSelectAll");
            this.cbSelectAll.Name = "cbSelectAll";
            this.cbSelectAll.UseVisualStyleBackColor = true;
            this.cbSelectAll.CheckedChanged += new System.EventHandler(this.cbSelectAll_CheckedChanged);
            // 
            // listUnmatched
            // 
            resources.ApplyResources(this.listUnmatched, "listUnmatched");
            this.listUnmatched.FormattingEnabled = true;
            this.listUnmatched.Name = "listUnmatched";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // AddModificationsDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AddModificationsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.menuAdd.ResumeLayout(false);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckedListBox listMatched;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.ContextMenuStrip menuAdd;
        private System.Windows.Forms.ToolStripMenuItem addStructuralModificationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addHeavyModificationToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox cbSelectAll;
        private System.Windows.Forms.ToolStripMenuItem addSelectedModificationsToolStripMenuItem;
        private System.Windows.Forms.ListBox listUnmatched;
    }
}