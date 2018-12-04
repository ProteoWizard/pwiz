namespace pwiz.Skyline.Controls.Graphs
{
    partial class RunToRunRegressionToolbar
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RunToRunRegressionToolbar));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.toolStripComboBoxTargetReplicates = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripComboOriginalReplicates = new System.Windows.Forms.ToolStripComboBox();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.BackColor = System.Drawing.SystemColors.ControlLight;
            this.toolStrip1.CanOverflow = false;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.toolStripComboBoxTargetReplicates,
            this.toolStripComboOriginalReplicates});
            resources.ApplyResources(this.toolStrip1, "toolStrip1");
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Resize += new System.EventHandler(this.toolStrip1_Resize);
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.toolStripLabel1.Name = "toolStripLabel1";
            resources.ApplyResources(this.toolStripLabel1, "toolStripLabel1");
            // 
            // toolStripComboBoxTargetReplicates
            // 
            resources.ApplyResources(this.toolStripComboBoxTargetReplicates, "toolStripComboBoxTargetReplicates");
            this.toolStripComboBoxTargetReplicates.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.toolStripComboBoxTargetReplicates.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.toolStripComboBoxTargetReplicates.Name = "toolStripComboBoxTargetReplicates";
            this.toolStripComboBoxTargetReplicates.SelectedIndexChanged += new System.EventHandler(this.toolStripComboBoxTargetReplicates_SelectedIndexChanged);
            // 
            // toolStripComboOriginalReplicates
            // 
            this.toolStripComboOriginalReplicates.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            resources.ApplyResources(this.toolStripComboOriginalReplicates, "toolStripComboOriginalReplicates");
            this.toolStripComboOriginalReplicates.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.toolStripComboOriginalReplicates.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.toolStripComboOriginalReplicates.Name = "toolStripComboOriginalReplicates";
            this.toolStripComboOriginalReplicates.SelectedIndexChanged += new System.EventHandler(this.toolStripComboOriginalReplicates_SelectedIndexChanged);
            // 
            // RunToRunRegressionToolbar
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.toolStrip1);
            this.Name = "RunToRunRegressionToolbar";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripComboBox toolStripComboBoxTargetReplicates;
        private System.Windows.Forms.ToolStripComboBox toolStripComboOriginalReplicates;
    }
}
