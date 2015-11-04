namespace pwiz.Skyline.Controls.Graphs.Calibration
{
    partial class CalibrationForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CalibrationForm));
            this.zedGraphControl = new ZedGraph.ZedGraphControl();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.logPlotContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSampleTypesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSelectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // zedGraphControl
            // 
            resources.ApplyResources(this.zedGraphControl, "zedGraphControl");
            this.zedGraphControl.IsShowCopyMessage = false;
            this.zedGraphControl.Name = "zedGraphControl";
            this.zedGraphControl.ScrollGrace = 0D;
            this.zedGraphControl.ScrollMaxX = 0D;
            this.zedGraphControl.ScrollMaxY = 0D;
            this.zedGraphControl.ScrollMaxY2 = 0D;
            this.zedGraphControl.ScrollMinX = 0D;
            this.zedGraphControl.ScrollMinY = 0D;
            this.zedGraphControl.ScrollMinY2 = 0D;
            this.zedGraphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraphControl_ContextMenuBuilder);
            this.zedGraphControl.MouseDownEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.zedGraphControl_MouseDownEvent);
            this.zedGraphControl.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.zedGraphControl_MouseMoveEvent);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.logPlotContextMenuItem,
            this.showToolStripMenuItem,
            this.showSampleTypesContextMenuItem,
            this.showLegendContextMenuItem,
            this.showSelectionContextMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuStrip1, "contextMenuStrip1");
            // 
            // logPlotContextMenuItem
            // 
            this.logPlotContextMenuItem.Name = "logPlotContextMenuItem";
            resources.ApplyResources(this.logPlotContextMenuItem, "logPlotContextMenuItem");
            this.logPlotContextMenuItem.Click += new System.EventHandler(this.logPlotToolStripMenuItem_Click);
            // 
            // showToolStripMenuItem
            // 
            this.showToolStripMenuItem.Name = "showToolStripMenuItem";
            resources.ApplyResources(this.showToolStripMenuItem, "showToolStripMenuItem");
            // 
            // showSampleTypesContextMenuItem
            // 
            this.showSampleTypesContextMenuItem.Name = "showSampleTypesContextMenuItem";
            resources.ApplyResources(this.showSampleTypesContextMenuItem, "showSampleTypesContextMenuItem");
            // 
            // showLegendContextMenuItem
            // 
            this.showLegendContextMenuItem.Name = "showLegendContextMenuItem";
            resources.ApplyResources(this.showLegendContextMenuItem, "showLegendContextMenuItem");
            this.showLegendContextMenuItem.Click += new System.EventHandler(this.showLegendContextMenuItem_Click);
            // 
            // showSelectionContextMenuItem
            // 
            this.showSelectionContextMenuItem.Name = "showSelectionContextMenuItem";
            resources.ApplyResources(this.showSelectionContextMenuItem, "showSelectionContextMenuItem");
            this.showSelectionContextMenuItem.Click += new System.EventHandler(this.showSelectionContextMenuItem_Click);
            // 
            // CalibrationForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.zedGraphControl);
            this.Name = "CalibrationForm";
            this.ShowInTaskbar = false;
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ZedGraph.ZedGraphControl zedGraphControl;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem logPlotContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSampleTypesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSelectionContextMenuItem;
    }
}