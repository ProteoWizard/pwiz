namespace pwiz.Skyline.Controls.Graphs
{
    sealed partial class AreaCVToolbar
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AreaCVToolbar));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.toolStripComboGroup = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripLabel2 = new System.Windows.Forms.ToolStripLabel();
            this.toolStripNumericDetections = new pwiz.Skyline.Controls.ToolStripNumericUpDown();
            this.toolStripLabel3 = new System.Windows.Forms.ToolStripLabel();
            this.toolStripProperties = new System.Windows.Forms.ToolStripButton();
            this.toolStripLabel4 = new System.Windows.Forms.ToolStripLabel();
            this.toolStripComboNormalizedTo = new System.Windows.Forms.ToolStripComboBox();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.BackColor = System.Drawing.SystemColors.ControlLight;
            resources.ApplyResources(this.toolStrip1, "toolStrip1");
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.toolStripComboGroup,
            this.toolStripLabel2,
            this.toolStripNumericDetections,
            this.toolStripLabel3,
            this.toolStripProperties,
            this.toolStripLabel4,
            this.toolStripComboNormalizedTo});
            this.toolStrip1.Name = "toolStrip1";
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Name = "toolStripLabel1";
            resources.ApplyResources(this.toolStripLabel1, "toolStripLabel1");
            // 
            // toolStripComboGroup
            // 
            this.toolStripComboGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.toolStripComboGroup.Margin = new System.Windows.Forms.Padding(4);
            this.toolStripComboGroup.Name = "toolStripComboGroup";
            resources.ApplyResources(this.toolStripComboGroup, "toolStripComboGroup");
            // 
            // toolStripLabel2
            // 
            this.toolStripLabel2.Name = "toolStripLabel2";
            resources.ApplyResources(this.toolStripLabel2, "toolStripLabel2");
            // 
            // toolStripNumericDetections
            // 
            resources.ApplyResources(this.toolStripNumericDetections, "toolStripNumericDetections");
            this.toolStripNumericDetections.Name = "toolStripNumericDetections";
            // 
            // toolStripLabel3
            // 
            this.toolStripLabel3.Name = "toolStripLabel3";
            resources.ApplyResources(this.toolStripLabel3, "toolStripLabel3");
            // 
            // toolStripProperties
            // 
            this.toolStripProperties.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripProperties.BackColor = System.Drawing.SystemColors.Control;
            this.toolStripProperties.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripProperties.Name = "toolStripProperties";
            resources.ApplyResources(this.toolStripProperties, "toolStripProperties");
            this.toolStripProperties.Click += new System.EventHandler(this.toolStripProperties_Click);
            // 
            // toolStripLabel4
            // 
            this.toolStripLabel4.Name = "toolStripLabel4";
            resources.ApplyResources(this.toolStripLabel4, "toolStripLabel4");
            // 
            // toolStripComboNormalizedTo
            // 
            this.toolStripComboNormalizedTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.toolStripComboNormalizedTo.Margin = new System.Windows.Forms.Padding(4);
            this.toolStripComboNormalizedTo.Name = "toolStripComboNormalizedTo";
            resources.ApplyResources(this.toolStripComboNormalizedTo, "toolStripComboNormalizedTo");
            this.toolStripComboNormalizedTo.SelectedIndexChanged += new System.EventHandler(this.toolStripComboNormalizedTo_SelectedIndexChanged);
            // 
            // AreaCVToolbar
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Controls.Add(this.toolStrip1);
            this.Name = "AreaCVToolbar";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripComboBox toolStripComboGroup;
        private System.Windows.Forms.ToolStripLabel toolStripLabel2;
        private System.Windows.Forms.ToolStripButton toolStripProperties;
        private System.Windows.Forms.ToolStripLabel toolStripLabel3;
        private ToolStripNumericUpDown toolStripNumericDetections;
        private System.Windows.Forms.ToolStripLabel toolStripLabel4;
        private System.Windows.Forms.ToolStripComboBox toolStripComboNormalizedTo;
    }
}
