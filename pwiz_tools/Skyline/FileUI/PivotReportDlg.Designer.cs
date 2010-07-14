namespace pwiz.Skyline.FileUI
{
    partial class PivotReportDlg
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.treeView = new System.Windows.Forms.TreeView();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnAdd = new System.Windows.Forms.Button();
            this.lbxColumns = new System.Windows.Forms.ListBox();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnRemove = new System.Windows.Forms.ToolStripButton();
            this.btnUp = new System.Windows.Forms.ToolStripButton();
            this.btnDown = new System.Windows.Forms.ToolStripButton();
            this.panel3 = new System.Windows.Forms.Panel();
            this.textName = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnExecute = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.cbxPivotIsotopeLabel = new System.Windows.Forms.CheckBox();
            this.cbxPivotReplicate = new System.Windows.Forms.CheckBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.panel4 = new System.Windows.Forms.Panel();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(16, 52);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.treeView);
            this.splitContainer1.Panel1.Controls.Add(this.panel1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.lbxColumns);
            this.splitContainer1.Panel2.Controls.Add(this.toolStrip1);
            this.splitContainer1.Size = new System.Drawing.Size(609, 310);
            this.splitContainer1.SplitterDistance = 297;
            this.splitContainer1.TabIndex = 1;
            this.splitContainer1.TabStop = false;
            // 
            // treeView
            // 
            this.treeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView.HideSelection = false;
            this.treeView.Location = new System.Drawing.Point(0, 0);
            this.treeView.Name = "treeView";
            this.treeView.Size = new System.Drawing.Size(241, 310);
            this.treeView.TabIndex = 0;
            this.treeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView_AfterSelect);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnAdd);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel1.Location = new System.Drawing.Point(241, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(56, 310);
            this.panel1.TabIndex = 1;
            // 
            // btnAdd
            // 
            this.btnAdd.Enabled = false;
            this.btnAdd.Location = new System.Drawing.Point(4, 83);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(47, 23);
            this.btnAdd.TabIndex = 0;
            this.btnAdd.Text = "&Add >";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // lbxColumns
            // 
            this.lbxColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lbxColumns.FormattingEnabled = true;
            this.lbxColumns.IntegralHeight = false;
            this.lbxColumns.Location = new System.Drawing.Point(0, 0);
            this.lbxColumns.Name = "lbxColumns";
            this.lbxColumns.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lbxColumns.Size = new System.Drawing.Size(276, 310);
            this.lbxColumns.TabIndex = 0;
            this.lbxColumns.SelectedIndexChanged += new System.EventHandler(this.lbxColumns_SelectedIndexChanged);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnRemove,
            this.btnUp,
            this.btnDown});
            this.toolStrip1.Location = new System.Drawing.Point(276, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(32, 310);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnRemove
            // 
            this.btnRemove.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRemove.Enabled = false;
            this.btnRemove.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            this.btnRemove.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(29, 20);
            this.btnRemove.Text = "Remove";
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnUp
            // 
            this.btnUp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUp.Enabled = false;
            this.btnUp.Image = global::pwiz.Skyline.Properties.Resources.up_pro32;
            this.btnUp.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(29, 20);
            this.btnUp.Text = "Up";
            this.btnUp.Click += new System.EventHandler(this.btnUp_Click);
            // 
            // btnDown
            // 
            this.btnDown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDown.Enabled = false;
            this.btnDown.Image = global::pwiz.Skyline.Properties.Resources.down_pro32;
            this.btnDown.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(29, 20);
            this.btnDown.Text = "Down";
            this.btnDown.Click += new System.EventHandler(this.btnDown_Click);
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.textName);
            this.panel3.Controls.Add(this.label2);
            this.panel3.Controls.Add(this.btnExecute);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(625, 52);
            this.panel3.TabIndex = 0;
            // 
            // textName
            // 
            this.textName.Location = new System.Drawing.Point(91, 18);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(205, 20);
            this.textName.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(73, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "Report &Name:";
            // 
            // btnExecute
            // 
            this.btnExecute.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExecute.Location = new System.Drawing.Point(526, 18);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(75, 23);
            this.btnExecute.TabIndex = 4;
            this.btnExecute.Text = "&Preview...";
            this.btnExecute.UseVisualStyleBackColor = true;
            this.btnExecute.Click += new System.EventHandler(this.btnExecute_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.cbxPivotIsotopeLabel);
            this.panel2.Controls.Add(this.cbxPivotReplicate);
            this.panel2.Controls.Add(this.btnCancel);
            this.panel2.Controls.Add(this.btnOk);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 362);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(625, 46);
            this.panel2.TabIndex = 2;
            // 
            // cbxPivotIsotopeLabel
            // 
            this.cbxPivotIsotopeLabel.AutoSize = true;
            this.cbxPivotIsotopeLabel.Location = new System.Drawing.Point(147, 6);
            this.cbxPivotIsotopeLabel.Name = "cbxPivotIsotopeLabel";
            this.cbxPivotIsotopeLabel.Size = new System.Drawing.Size(117, 17);
            this.cbxPivotIsotopeLabel.TabIndex = 3;
            this.cbxPivotIsotopeLabel.Text = "Pivot Isotope Label";
            this.cbxPivotIsotopeLabel.UseVisualStyleBackColor = true;
            // 
            // cbxPivotReplicate
            // 
            this.cbxPivotReplicate.AutoSize = true;
            this.cbxPivotReplicate.Location = new System.Drawing.Point(12, 6);
            this.cbxPivotReplicate.Name = "cbxPivotReplicate";
            this.cbxPivotReplicate.Size = new System.Drawing.Size(129, 17);
            this.cbxPivotReplicate.TabIndex = 2;
            this.cbxPivotReplicate.Text = "Pivot Replicate Name";
            this.cbxPivotReplicate.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(537, 11);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(456, 11);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 0;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // panel4
            // 
            this.panel4.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel4.Location = new System.Drawing.Point(0, 52);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(16, 310);
            this.panel4.TabIndex = 3;
            // 
            // PivotReportDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(625, 408);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.panel4);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PivotReportDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Report";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            this.splitContainer1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TreeView treeView;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.ListBox lbxColumns;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Button btnExecute;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnRemove;
        private System.Windows.Forms.ToolStripButton btnUp;
        private System.Windows.Forms.ToolStripButton btnDown;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox cbxPivotReplicate;
        private System.Windows.Forms.CheckBox cbxPivotIsotopeLabel;
    }
}