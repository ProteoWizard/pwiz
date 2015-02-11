namespace pwiz.Skyline.Controls.GroupComparison
{
    partial class FoldChangeGrid
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FoldChangeGrid));
            this.databoundGridControl = new pwiz.Skyline.Controls.Databinding.DataboundGridControl();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolButtonShowGraph = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonChangeSettings = new System.Windows.Forms.ToolStripButton();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            resources.ApplyResources(this.databoundGridControl, "databoundGridControl");
            this.databoundGridControl.Name = "databoundGridControl";
            // 
            // toolStrip
            // 
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolButtonShowGraph,
            this.toolStripButtonChangeSettings});
            resources.ApplyResources(this.toolStrip, "toolStrip");
            this.toolStrip.Name = "toolStrip";
            // 
            // toolButtonShowGraph
            // 
            this.toolButtonShowGraph.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(this.toolButtonShowGraph, "toolButtonShowGraph");
            this.toolButtonShowGraph.Name = "toolButtonShowGraph";
            this.toolButtonShowGraph.Click += new System.EventHandler(this.toolButtonShowGraph_Click);
            // 
            // toolStripButtonChangeSettings
            // 
            this.toolStripButtonChangeSettings.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(this.toolStripButtonChangeSettings, "toolStripButtonChangeSettings");
            this.toolStripButtonChangeSettings.Name = "toolStripButtonChangeSettings";
            this.toolStripButtonChangeSettings.Click += new System.EventHandler(this.toolStripButtonChangeSettings_Click);
            // 
            // FoldChangeGrid
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.databoundGridControl);
            this.Controls.Add(this.toolStrip);
            this.Name = "FoldChangeGrid";
            this.ShowInTaskbar = false;
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Databinding.DataboundGridControl databoundGridControl;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton toolButtonShowGraph;
        private System.Windows.Forms.ToolStripButton toolStripButtonChangeSettings;
    }
}
