namespace pwiz.Skyline.Controls
{
    partial class SequenceTreeForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SequenceTreeForm));
            this.sequenceTree = new pwiz.Skyline.Controls.SequenceTree();
            this.toolBarResults = new System.Windows.Forms.ToolStrip();
            this.labelResults = new System.Windows.Forms.ToolStripLabel();
            this.comboResults = new System.Windows.Forms.ToolStripComboBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.toolBarResults.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // sequenceTree
            // 
            this.sequenceTree.AllowDrop = true;
            resources.ApplyResources(this.sequenceTree, "sequenceTree");
            this.sequenceTree.AutoExpandSingleNodes = true;
            this.sequenceTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.sequenceTree.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.sequenceTree.HideSelection = false;
            this.sequenceTree.ItemHeight = 16;
            this.sequenceTree.LabelEdit = true;
            this.sequenceTree.LockDefaultExpansion = false;
            this.sequenceTree.Name = "sequenceTree";
            this.sequenceTree.RestoredFromPersistentString = false;
            this.sequenceTree.UseKeysOverride = false;
            // 
            // toolBarResults
            // 
            this.toolBarResults.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBarResults.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.labelResults,
            this.comboResults});
            resources.ApplyResources(this.toolBarResults, "toolBarResults");
            this.toolBarResults.Name = "toolBarResults";
            this.toolBarResults.Resize += new System.EventHandler(this.toolBarResults_Resize);
            // 
            // labelResults
            // 
            this.labelResults.Name = "labelResults";
            this.labelResults.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            resources.ApplyResources(this.labelResults, "labelResults");
            // 
            // comboResults
            // 
            resources.ApplyResources(this.comboResults, "comboResults");
            this.comboResults.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboResults.Name = "comboResults";
            this.comboResults.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.sequenceTree);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // SequenceTreeForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.toolBarResults);
            this.HideOnClose = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SequenceTreeForm";
            this.ShowInTaskbar = false;
            this.toolBarResults.ResumeLayout(false);
            this.toolBarResults.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private SequenceTree sequenceTree;
        private System.Windows.Forms.ToolStrip toolBarResults;
        private System.Windows.Forms.ToolStripLabel labelResults;
        private System.Windows.Forms.ToolStripComboBox comboResults;
        private System.Windows.Forms.Panel panel1;
    }
}
