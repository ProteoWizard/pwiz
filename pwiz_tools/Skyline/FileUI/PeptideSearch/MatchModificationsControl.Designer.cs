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
            this.modificationsListBox = new System.Windows.Forms.CheckedListBox();
            this.labelModifications = new System.Windows.Forms.Label();
            this.unmatchedListBox = new System.Windows.Forms.ListBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.label1 = new System.Windows.Forms.Label();
            this.btnAddModification = new System.Windows.Forms.Button();
            this.menuAddModification = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuItemAddStructuralModification = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemAddHeavyModification = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.menuAddModification.SuspendLayout();
            this.SuspendLayout();
            // 
            // modificationsListBox
            // 
            this.modificationsListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.modificationsListBox.CheckOnClick = true;
            this.modificationsListBox.FormattingEnabled = true;
            this.modificationsListBox.Location = new System.Drawing.Point(19, 42);
            this.modificationsListBox.Name = "modificationsListBox";
            this.modificationsListBox.Size = new System.Drawing.Size(340, 79);
            this.modificationsListBox.TabIndex = 4;
            // 
            // labelModifications
            // 
            this.labelModifications.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelModifications.Location = new System.Drawing.Point(19, 10);
            this.labelModifications.Name = "labelModifications";
            this.labelModifications.Size = new System.Drawing.Size(353, 29);
            this.labelModifications.TabIndex = 5;
            this.labelModifications.Text = "This library appears to contain the modifications listed below. Please select the" +
    " ones you would like to use in the Skyline document:";
            // 
            // unmatchedListBox
            // 
            this.unmatchedListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.unmatchedListBox.FormattingEnabled = true;
            this.unmatchedListBox.Location = new System.Drawing.Point(18, 25);
            this.unmatchedListBox.Name = "unmatchedListBox";
            this.unmatchedListBox.Size = new System.Drawing.Size(340, 82);
            this.unmatchedListBox.TabIndex = 6;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.modificationsListBox);
            this.splitContainer1.Panel1.Controls.Add(this.labelModifications);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.label1);
            this.splitContainer1.Panel2.Controls.Add(this.btnAddModification);
            this.splitContainer1.Panel2.Controls.Add(this.unmatchedListBox);
            this.splitContainer1.Size = new System.Drawing.Size(381, 312);
            this.splitContainer1.SplitterDistance = 123;
            this.splitContainer1.TabIndex = 7;
            this.splitContainer1.TabStop = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(18, 5);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(252, 13);
            this.label1.TabIndex = 7;
            this.label1.Text = "The following modifications could not be interpreted.";
            // 
            // btnAddModification
            // 
            this.btnAddModification.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAddModification.Location = new System.Drawing.Point(18, 137);
            this.btnAddModification.Name = "btnAddModification";
            this.btnAddModification.Size = new System.Drawing.Size(140, 23);
            this.btnAddModification.TabIndex = 8;
            this.btnAddModification.Text = "&Add modification...";
            this.btnAddModification.UseVisualStyleBackColor = true;
            this.btnAddModification.Click += new System.EventHandler(this.btnAddModification_Click);
            // 
            // menuAddModification
            // 
            this.menuAddModification.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemAddStructuralModification,
            this.menuItemAddHeavyModification});
            this.menuAddModification.Name = "menuAddModification";
            this.menuAddModification.Size = new System.Drawing.Size(230, 70);
            // 
            // menuItemAddStructuralModification
            // 
            this.menuItemAddStructuralModification.Name = "menuItemAddStructuralModification";
            this.menuItemAddStructuralModification.Size = new System.Drawing.Size(229, 22);
            this.menuItemAddStructuralModification.Text = "Add &structural modification...";
            this.menuItemAddStructuralModification.Click += new System.EventHandler(this.menuItemAddStructuralModification_Click);
            // 
            // menuItemAddHeavyModification
            // 
            this.menuItemAddHeavyModification.Name = "menuItemAddHeavyModification";
            this.menuItemAddHeavyModification.Size = new System.Drawing.Size(229, 22);
            this.menuItemAddHeavyModification.Text = "Add &heavy modification...";
            this.menuItemAddHeavyModification.Click += new System.EventHandler(this.menuItemAddHeavyModification_Click);
            // 
            // MatchModificationsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.splitContainer1);
            this.Name = "MatchModificationsControl";
            this.Size = new System.Drawing.Size(381, 315);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.menuAddModification.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckedListBox modificationsListBox;
        private System.Windows.Forms.Label labelModifications;
        private System.Windows.Forms.ListBox unmatchedListBox;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnAddModification;
        private System.Windows.Forms.ContextMenuStrip menuAddModification;
        private System.Windows.Forms.ToolStripMenuItem menuItemAddStructuralModification;
        private System.Windows.Forms.ToolStripMenuItem menuItemAddHeavyModification;
    }
}
