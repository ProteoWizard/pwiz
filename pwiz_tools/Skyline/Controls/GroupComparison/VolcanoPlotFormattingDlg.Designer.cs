namespace pwiz.Skyline.Controls.GroupComparison
{
    partial class VolcanoPlotFormattingDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VolcanoPlotFormattingDlg));
            this.colorDialog1 = new System.Windows.Forms.ColorDialog();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.regexColorRowGrid1 = new pwiz.Skyline.Controls.GroupComparison.RegexColorRowGrid();
            this.advancedCheckBox = new System.Windows.Forms.CheckBox();
            this.layoutLabelsBox = new System.Windows.Forms.CheckBox();
            this.toolStripFormatting = new System.Windows.Forms.ToolStrip();
            this.btnDeleteRule = new System.Windows.Forms.ToolStripButton();
            this.btnMoveRuleUp = new System.Windows.Forms.ToolStripButton();
            this.btnMoveRuleDown = new System.Windows.Forms.ToolStripButton();
            this.toolStripFormatting.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            resources.ApplyResources(this.button1, "button1");
            this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button1.Name = "button1";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            resources.ApplyResources(this.button2, "button2");
            this.button2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button2.Name = "button2";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // regexColorRowGrid1
            // 
            this.regexColorRowGrid1.AllowUserToAddRows = true;
            this.regexColorRowGrid1.AllowUserToOrderColumns = false;
            resources.ApplyResources(this.regexColorRowGrid1, "regexColorRowGrid1");
            this.regexColorRowGrid1.Name = "regexColorRowGrid1";
            this.regexColorRowGrid1.OnCellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.regexColorRowGrid1_OnCellValueChanged);
            this.regexColorRowGrid1.OnCellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.regexColorRowGrid1_OnCellClick);
            // 
            // advancedCheckBox
            // 
            resources.ApplyResources(this.advancedCheckBox, "advancedCheckBox");
            this.advancedCheckBox.Name = "advancedCheckBox";
            this.advancedCheckBox.UseVisualStyleBackColor = true;
            this.advancedCheckBox.CheckedChanged += new System.EventHandler(this.advancedCheckBox_CheckedChanged);
            // 
            // layoutLabelsBox
            // 
            resources.ApplyResources(this.layoutLabelsBox, "layoutLabelsBox");
            this.layoutLabelsBox.Name = "layoutLabelsBox";
            this.layoutLabelsBox.UseVisualStyleBackColor = true;
            this.layoutLabelsBox.CheckedChanged += new System.EventHandler(this.layoutLabelsBox_CheckedChanged);
            //
            // toolStripFormatting
            //
            this.toolStripFormatting.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.toolStripFormatting.AutoSize = false;
            this.toolStripFormatting.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStripFormatting.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripFormatting.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnDeleteRule,
            this.btnMoveRuleUp,
            this.btnMoveRuleDown});
            this.toolStripFormatting.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.VerticalStackWithOverflow;
            this.toolStripFormatting.Location = new System.Drawing.Point(624, 12);
            this.toolStripFormatting.Name = "toolStripFormatting";
            this.toolStripFormatting.Size = new System.Drawing.Size(26, 80);
            this.toolStripFormatting.TabIndex = 4;
            //
            // btnDeleteRule
            //
            this.btnDeleteRule.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDeleteRule.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            this.btnDeleteRule.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDeleteRule.Name = "btnDeleteRule";
            this.btnDeleteRule.Size = new System.Drawing.Size(23, 20);
            this.btnDeleteRule.Click += new System.EventHandler(this.btnDeleteRule_Click);
            //
            // btnMoveRuleUp
            //
            this.btnMoveRuleUp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnMoveRuleUp.Image = global::pwiz.Skyline.Properties.Resources.up_pro32;
            this.btnMoveRuleUp.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnMoveRuleUp.Name = "btnMoveRuleUp";
            this.btnMoveRuleUp.Size = new System.Drawing.Size(23, 20);
            this.btnMoveRuleUp.Click += new System.EventHandler(this.btnMoveRuleUp_Click);
            //
            // btnMoveRuleDown
            //
            this.btnMoveRuleDown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnMoveRuleDown.Image = global::pwiz.Skyline.Properties.Resources.down_pro32;
            this.btnMoveRuleDown.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnMoveRuleDown.Name = "btnMoveRuleDown";
            this.btnMoveRuleDown.Size = new System.Drawing.Size(23, 20);
            this.btnMoveRuleDown.Click += new System.EventHandler(this.btnMoveRuleDown_Click);
            //
            // VolcanoPlotFormattingDlg
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.toolStripFormatting);
            this.Controls.Add(this.advancedCheckBox);
            this.Controls.Add(this.layoutLabelsBox);
            this.Controls.Add(this.regexColorRowGrid1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "VolcanoPlotFormattingDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.toolStripFormatting.ResumeLayout(false);
            this.toolStripFormatting.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ColorDialog colorDialog1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private RegexColorRowGrid regexColorRowGrid1;
        private System.Windows.Forms.CheckBox advancedCheckBox;
        private System.Windows.Forms.CheckBox layoutLabelsBox;
        private System.Windows.Forms.ToolStrip toolStripFormatting;
        private System.Windows.Forms.ToolStripButton btnDeleteRule;
        private System.Windows.Forms.ToolStripButton btnMoveRuleUp;
        private System.Windows.Forms.ToolStripButton btnMoveRuleDown;

    }
}