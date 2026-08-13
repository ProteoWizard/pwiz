namespace pwiz.Skyline.Menus
{
    partial class DetectionsContextMenu
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DetectionsContextMenu));
            this.contextMenuDetections = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.detectionsTargetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsTargetPrecursorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsTargetPeptideToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsGraphTypeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsGraphTypeReplicateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsGraphTypeHistogramToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsToolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.detectionsShowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsShowSelectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsShowLegendToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsShowMeanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsShowAtLeastNToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsYScaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsYScaleOneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsYScalePercentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsToolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.detectionsPropertiesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsToolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuDetections.SuspendLayout();
            this.SuspendLayout();
            //
            // contextMenuDetections
            //
            this.contextMenuDetections.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsTargetToolStripMenuItem,
            this.detectionsGraphTypeToolStripMenuItem,
            this.detectionsToolStripSeparator1,
            this.detectionsShowToolStripMenuItem,
            this.detectionsYScaleToolStripMenuItem,
            this.detectionsToolStripSeparator2,
            this.detectionsPropertiesToolStripMenuItem,
            this.detectionsToolStripSeparator3});
            this.contextMenuDetections.Name = "contextMenuDetections";
            resources.ApplyResources(this.contextMenuDetections, "contextMenuDetections");
            //
            // detectionsTargetToolStripMenuItem
            //
            this.detectionsTargetToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsTargetPrecursorToolStripMenuItem,
            this.detectionsTargetPeptideToolStripMenuItem});
            this.detectionsTargetToolStripMenuItem.Name = "detectionsTargetToolStripMenuItem";
            resources.ApplyResources(this.detectionsTargetToolStripMenuItem, "detectionsTargetToolStripMenuItem");
            //
            // detectionsTargetPrecursorToolStripMenuItem
            //
            this.detectionsTargetPrecursorToolStripMenuItem.Name = "detectionsTargetPrecursorToolStripMenuItem";
            resources.ApplyResources(this.detectionsTargetPrecursorToolStripMenuItem, "detectionsTargetPrecursorToolStripMenuItem");
            this.detectionsTargetPrecursorToolStripMenuItem.Tag = 0;
            this.detectionsTargetPrecursorToolStripMenuItem.Click += new System.EventHandler(this.detectionsTargetPrecursorToolStripMenuItem_Click);
            //
            // detectionsTargetPeptideToolStripMenuItem
            //
            this.detectionsTargetPeptideToolStripMenuItem.Name = "detectionsTargetPeptideToolStripMenuItem";
            resources.ApplyResources(this.detectionsTargetPeptideToolStripMenuItem, "detectionsTargetPeptideToolStripMenuItem");
            this.detectionsTargetPeptideToolStripMenuItem.Tag = 1;
            this.detectionsTargetPeptideToolStripMenuItem.Click += new System.EventHandler(this.detectionsTargetPeptideToolStripMenuItem_Click);
            //
            // detectionsGraphTypeToolStripMenuItem
            //
            this.detectionsGraphTypeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsGraphTypeReplicateToolStripMenuItem,
            this.detectionsGraphTypeHistogramToolStripMenuItem});
            this.detectionsGraphTypeToolStripMenuItem.Name = "detectionsGraphTypeToolStripMenuItem";
            resources.ApplyResources(this.detectionsGraphTypeToolStripMenuItem, "detectionsGraphTypeToolStripMenuItem");
            //
            // detectionsGraphTypeReplicateToolStripMenuItem
            //
            this.detectionsGraphTypeReplicateToolStripMenuItem.Name = "detectionsGraphTypeReplicateToolStripMenuItem";
            resources.ApplyResources(this.detectionsGraphTypeReplicateToolStripMenuItem, "detectionsGraphTypeReplicateToolStripMenuItem");
            this.detectionsGraphTypeReplicateToolStripMenuItem.Click += new System.EventHandler(this.detectionsGraphTypeReplicateToolStripMenuItem_Click);
            //
            // detectionsGraphTypeHistogramToolStripMenuItem
            //
            this.detectionsGraphTypeHistogramToolStripMenuItem.Name = "detectionsGraphTypeHistogramToolStripMenuItem";
            resources.ApplyResources(this.detectionsGraphTypeHistogramToolStripMenuItem, "detectionsGraphTypeHistogramToolStripMenuItem");
            this.detectionsGraphTypeHistogramToolStripMenuItem.Click += new System.EventHandler(this.detectionsGraphTypeHistogramToolStripMenuItem_Click);
            //
            // detectionsToolStripSeparator1
            //
            this.detectionsToolStripSeparator1.Name = "detectionsToolStripSeparator1";
            resources.ApplyResources(this.detectionsToolStripSeparator1, "detectionsToolStripSeparator1");
            //
            // detectionsShowToolStripMenuItem
            //
            this.detectionsShowToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsShowSelectionToolStripMenuItem,
            this.detectionsShowLegendToolStripMenuItem,
            this.detectionsShowMeanToolStripMenuItem,
            this.detectionsShowAtLeastNToolStripMenuItem});
            this.detectionsShowToolStripMenuItem.Name = "detectionsShowToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowToolStripMenuItem, "detectionsShowToolStripMenuItem");
            //
            // detectionsShowSelectionToolStripMenuItem
            //
            this.detectionsShowSelectionToolStripMenuItem.Name = "detectionsShowSelectionToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowSelectionToolStripMenuItem, "detectionsShowSelectionToolStripMenuItem");
            this.detectionsShowSelectionToolStripMenuItem.Click += new System.EventHandler(this.detectionsShowSelectionToolStripMenuItem_Click);
            //
            // detectionsShowLegendToolStripMenuItem
            //
            this.detectionsShowLegendToolStripMenuItem.Name = "detectionsShowLegendToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowLegendToolStripMenuItem, "detectionsShowLegendToolStripMenuItem");
            this.detectionsShowLegendToolStripMenuItem.Click += new System.EventHandler(this.detectionsShowLegendToolStripMenuItem_Click);
            //
            // detectionsShowMeanToolStripMenuItem
            //
            this.detectionsShowMeanToolStripMenuItem.Name = "detectionsShowMeanToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowMeanToolStripMenuItem, "detectionsShowMeanToolStripMenuItem");
            this.detectionsShowMeanToolStripMenuItem.Click += new System.EventHandler(this.detectionsShowMeanToolStripMenuItem_Click);
            //
            // detectionsShowAtLeastNToolStripMenuItem
            //
            this.detectionsShowAtLeastNToolStripMenuItem.Name = "detectionsShowAtLeastNToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowAtLeastNToolStripMenuItem, "detectionsShowAtLeastNToolStripMenuItem");
            this.detectionsShowAtLeastNToolStripMenuItem.Click += new System.EventHandler(this.detectionsShowAtLeastNToolStripMenuItem_Click);
            //
            // detectionsYScaleToolStripMenuItem
            //
            this.detectionsYScaleToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsYScaleOneToolStripMenuItem,
            this.detectionsYScalePercentToolStripMenuItem});
            this.detectionsYScaleToolStripMenuItem.Name = "detectionsYScaleToolStripMenuItem";
            resources.ApplyResources(this.detectionsYScaleToolStripMenuItem, "detectionsYScaleToolStripMenuItem");
            //
            // detectionsYScaleOneToolStripMenuItem
            //
            this.detectionsYScaleOneToolStripMenuItem.Name = "detectionsYScaleOneToolStripMenuItem";
            resources.ApplyResources(this.detectionsYScaleOneToolStripMenuItem, "detectionsYScaleOneToolStripMenuItem");
            this.detectionsYScaleOneToolStripMenuItem.Tag = 1;
            this.detectionsYScaleOneToolStripMenuItem.Click += new System.EventHandler(this.detectionsYScaleOneToolStripMenuItem_Click);
            //
            // detectionsYScalePercentToolStripMenuItem
            //
            this.detectionsYScalePercentToolStripMenuItem.Name = "detectionsYScalePercentToolStripMenuItem";
            resources.ApplyResources(this.detectionsYScalePercentToolStripMenuItem, "detectionsYScalePercentToolStripMenuItem");
            this.detectionsYScalePercentToolStripMenuItem.Tag = 0;
            this.detectionsYScalePercentToolStripMenuItem.Click += new System.EventHandler(this.detectionsYScalePercentToolStripMenuItem_Click);
            //
            // detectionsToolStripSeparator2
            //
            this.detectionsToolStripSeparator2.Name = "detectionsToolStripSeparator2";
            resources.ApplyResources(this.detectionsToolStripSeparator2, "detectionsToolStripSeparator2");
            //
            // detectionsPropertiesToolStripMenuItem
            //
            this.detectionsPropertiesToolStripMenuItem.Name = "detectionsPropertiesToolStripMenuItem";
            resources.ApplyResources(this.detectionsPropertiesToolStripMenuItem, "detectionsPropertiesToolStripMenuItem");
            this.detectionsPropertiesToolStripMenuItem.Click += new System.EventHandler(this.detectionsPropertiesToolStripMenuItem_Click);
            //
            // detectionsToolStripSeparator3
            //
            this.detectionsToolStripSeparator3.Name = "detectionsToolStripSeparator3";
            resources.ApplyResources(this.detectionsToolStripSeparator3, "detectionsToolStripSeparator3");
            //
            // DetectionsContextMenu
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "DetectionsContextMenu";
            this.contextMenuDetections.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip contextMenuDetections;
        private System.Windows.Forms.ToolStripMenuItem detectionsTargetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsTargetPrecursorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsTargetPeptideToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsGraphTypeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsGraphTypeReplicateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsGraphTypeHistogramToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator detectionsToolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowSelectionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowLegendToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowMeanToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowAtLeastNToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsYScaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsYScaleOneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsYScalePercentToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator detectionsToolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem detectionsPropertiesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator detectionsToolStripSeparator3;
    }
}
