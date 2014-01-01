namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class MatchModificationsControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MatchModificationsControl));
            this.modificationsListBox = new System.Windows.Forms.CheckedListBox();
            this.labelModifications = new System.Windows.Forms.Label();
            this.unmatchedListBox = new System.Windows.Forms.ListBox();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.cbSelectAll = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnAddModification = new System.Windows.Forms.Button();
            this.menuAddModification = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuItemAddStructuralModification = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemAddHeavyModification = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.menuAddModification.SuspendLayout();
            this.SuspendLayout();
            // 
            // modificationsListBox
            // 
            resources.ApplyResources(this.modificationsListBox, "modificationsListBox");
            this.modificationsListBox.CheckOnClick = true;
            this.modificationsListBox.FormattingEnabled = true;
            this.modificationsListBox.Name = "modificationsListBox";
            this.modificationsListBox.Sorted = true;
            // 
            // labelModifications
            // 
            resources.ApplyResources(this.labelModifications, "labelModifications");
            this.labelModifications.Name = "labelModifications";
            // 
            // unmatchedListBox
            // 
            resources.ApplyResources(this.unmatchedListBox, "unmatchedListBox");
            this.unmatchedListBox.FormattingEnabled = true;
            this.unmatchedListBox.Name = "unmatchedListBox";
            // 
            // splitContainer
            // 
            resources.ApplyResources(this.splitContainer, "splitContainer");
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.cbSelectAll);
            this.splitContainer.Panel1.Controls.Add(this.modificationsListBox);
            this.splitContainer.Panel1.Controls.Add(this.labelModifications);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.label1);
            this.splitContainer.Panel2.Controls.Add(this.unmatchedListBox);
            this.splitContainer.TabStop = false;
            // 
            // cbSelectAll
            // 
            resources.ApplyResources(this.cbSelectAll, "cbSelectAll");
            this.cbSelectAll.Name = "cbSelectAll";
            this.cbSelectAll.UseVisualStyleBackColor = true;
            this.cbSelectAll.CheckedChanged += new System.EventHandler(this.cbSelectAll_CheckedChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // btnAddModification
            // 
            resources.ApplyResources(this.btnAddModification, "btnAddModification");
            this.btnAddModification.Name = "btnAddModification";
            this.btnAddModification.UseVisualStyleBackColor = true;
            this.btnAddModification.Click += new System.EventHandler(this.btnAddModification_Click);
            // 
            // menuAddModification
            // 
            this.menuAddModification.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemAddStructuralModification,
            this.menuItemAddHeavyModification});
            this.menuAddModification.Name = "menuAddModification";
            resources.ApplyResources(this.menuAddModification, "menuAddModification");
            // 
            // menuItemAddStructuralModification
            // 
            this.menuItemAddStructuralModification.Name = "menuItemAddStructuralModification";
            resources.ApplyResources(this.menuItemAddStructuralModification, "menuItemAddStructuralModification");
            this.menuItemAddStructuralModification.Click += new System.EventHandler(this.menuItemAddStructuralModification_Click);
            // 
            // menuItemAddHeavyModification
            // 
            this.menuItemAddHeavyModification.Name = "menuItemAddHeavyModification";
            resources.ApplyResources(this.menuItemAddHeavyModification, "menuItemAddHeavyModification");
            this.menuItemAddHeavyModification.Click += new System.EventHandler(this.menuItemAddHeavyModification_Click);
            // 
            // MatchModificationsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.btnAddModification);
            this.Name = "MatchModificationsControl";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.menuAddModification.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckedListBox modificationsListBox;
        private System.Windows.Forms.Label labelModifications;
        private System.Windows.Forms.ListBox unmatchedListBox;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnAddModification;
        private System.Windows.Forms.ContextMenuStrip menuAddModification;
        private System.Windows.Forms.ToolStripMenuItem menuItemAddStructuralModification;
        private System.Windows.Forms.ToolStripMenuItem menuItemAddHeavyModification;
        private System.Windows.Forms.CheckBox cbSelectAll;
    }
}
