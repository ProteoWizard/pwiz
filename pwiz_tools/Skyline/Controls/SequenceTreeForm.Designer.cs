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
            this.sequenceTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sequenceTree.AutoExpandSingleNodes = true;
            this.sequenceTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.sequenceTree.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.sequenceTree.HideSelection = false;
            this.sequenceTree.ItemHeight = 16;
            this.sequenceTree.LabelEdit = true;
            this.sequenceTree.Location = new System.Drawing.Point(-1, -1);
            this.sequenceTree.Name = "sequenceTree";
            this.sequenceTree.Size = new System.Drawing.Size(301, 552);
            this.sequenceTree.TabIndex = 3;
            this.sequenceTree.UseKeysOverride = false;
            // 
            // toolBarResults
            // 
            this.toolBarResults.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBarResults.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.labelResults,
            this.comboResults});
            this.toolBarResults.Location = new System.Drawing.Point(0, 0);
            this.toolBarResults.Name = "toolBarResults";
            this.toolBarResults.Size = new System.Drawing.Size(299, 25);
            this.toolBarResults.TabIndex = 4;
            this.toolBarResults.Visible = false;
            this.toolBarResults.Resize += new System.EventHandler(this.toolBarResults_Resize);
            // 
            // labelResults
            // 
            this.labelResults.Name = "labelResults";
            this.labelResults.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.labelResults.Size = new System.Drawing.Size(63, 22);
            this.labelResults.Text = "&Replicates:";
            // 
            // comboResults
            // 
            this.comboResults.AutoSize = false;
            this.comboResults.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboResults.MaxDropDownItems = 12;
            this.comboResults.Name = "comboResults";
            this.comboResults.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.comboResults.Size = new System.Drawing.Size(160, 23);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.sequenceTree);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(299, 550);
            this.panel1.TabIndex = 5;
            // 
            // SequenceTreeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(299, 550);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.toolBarResults);
            this.HideOnClose = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SequenceTreeForm";
            this.ShowInTaskbar = false;
            this.TabText = "Targets";
            this.Text = "SequenceTreeForm";
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
