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
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(241, 351);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 2;
            this.btnOk.Text = "&OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(322, 351);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(385, 27);
            this.label1.TabIndex = 0;
            this.label1.Text = "This library appears to contain the modifications listed below. Please select the" +
    " ones you wish to use with the library:";
            // 
            // listMatched
            // 
            this.listMatched.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listMatched.CheckOnClick = true;
            this.listMatched.FormattingEnabled = true;
            this.listMatched.Location = new System.Drawing.Point(0, 30);
            this.listMatched.Name = "listMatched";
            this.listMatched.Size = new System.Drawing.Size(385, 109);
            this.listMatched.Sorted = true;
            this.listMatched.TabIndex = 1;
            this.listMatched.ThreeDCheckBoxes = true;
            // 
            // btnAdd
            // 
            this.btnAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAdd.Location = new System.Drawing.Point(12, 351);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(125, 23);
            this.btnAdd.TabIndex = 1;
            this.btnAdd.Text = "Add to &Document...";
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
            this.menuAdd.Size = new System.Drawing.Size(230, 70);
            // 
            // addSelectedModificationsToolStripMenuItem
            // 
            this.addSelectedModificationsToolStripMenuItem.Name = "addSelectedModificationsToolStripMenuItem";
            this.addSelectedModificationsToolStripMenuItem.Size = new System.Drawing.Size(229, 22);
            this.addSelectedModificationsToolStripMenuItem.Text = "Add selected modifications";
            this.addSelectedModificationsToolStripMenuItem.Click += new System.EventHandler(this.addSelectedModificationsToolStripMenuItem_Click);
            // 
            // addStructuralModificationToolStripMenuItem
            // 
            this.addStructuralModificationToolStripMenuItem.Name = "addStructuralModificationToolStripMenuItem";
            this.addStructuralModificationToolStripMenuItem.Size = new System.Drawing.Size(229, 22);
            this.addStructuralModificationToolStripMenuItem.Text = "Add &structural modification...";
            this.addStructuralModificationToolStripMenuItem.Click += new System.EventHandler(this.addStructuralModificationToolStripMenuItem_Click);
            // 
            // addHeavyModificationToolStripMenuItem
            // 
            this.addHeavyModificationToolStripMenuItem.Name = "addHeavyModificationToolStripMenuItem";
            this.addHeavyModificationToolStripMenuItem.Size = new System.Drawing.Size(229, 22);
            this.addHeavyModificationToolStripMenuItem.Text = "Add &heavy modification...";
            this.addHeavyModificationToolStripMenuItem.Click += new System.EventHandler(this.addHeavyModificationToolStripMenuItem_Click);
            // 
            // splitContainer
            // 
            this.splitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer.Location = new System.Drawing.Point(12, 12);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
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
            this.splitContainer.Size = new System.Drawing.Size(385, 333);
            this.splitContainer.SplitterDistance = 166;
            this.splitContainer.TabIndex = 0;
            // 
            // cbSelectAll
            // 
            this.cbSelectAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbSelectAll.AutoSize = true;
            this.cbSelectAll.Location = new System.Drawing.Point(0, 145);
            this.cbSelectAll.Name = "cbSelectAll";
            this.cbSelectAll.Size = new System.Drawing.Size(120, 17);
            this.cbSelectAll.TabIndex = 2;
            this.cbSelectAll.Text = "Select / deselect &all";
            this.cbSelectAll.UseVisualStyleBackColor = true;
            this.cbSelectAll.CheckedChanged += new System.EventHandler(this.cbSelectAll_CheckedChanged);
            // 
            // listUnmatched
            // 
            this.listUnmatched.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listUnmatched.FormattingEnabled = true;
            this.listUnmatched.Location = new System.Drawing.Point(0, 33);
            this.listUnmatched.Name = "listUnmatched";
            this.listUnmatched.Size = new System.Drawing.Size(385, 108);
            this.listUnmatched.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Location = new System.Drawing.Point(0, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(385, 30);
            this.label2.TabIndex = 0;
            this.label2.Text = "The following modifications could not be interpreted:";
            // 
            // AddModificationsDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(409, 386);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(325, 225);
            this.Name = "AddModificationsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Add Modifications";
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