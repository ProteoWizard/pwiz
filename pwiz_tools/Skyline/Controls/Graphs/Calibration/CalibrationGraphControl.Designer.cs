namespace pwiz.Skyline.Controls.Graphs.Calibration
{
    partial class CalibrationGraphControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CalibrationGraphControl));
            this.zedGraphControl = new ZedGraph.ZedGraphControl();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.logXContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logYAxisContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSampleTypesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleBatchContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSelectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showFiguresOfMeritContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showBootstrapCurvesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.moreDisplayOptionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // zedGraphControl
            // 
            resources.ApplyResources(this.zedGraphControl, "zedGraphControl");
            this.zedGraphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.zedGraphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.zedGraphControl.IsShowCopyMessage = false;
            this.zedGraphControl.IsZoomOnMouseCenter = true;
            this.zedGraphControl.Name = "zedGraphControl";
            this.zedGraphControl.ScrollGrace = 0D;
            this.zedGraphControl.ScrollMaxX = 0D;
            this.zedGraphControl.ScrollMaxY = 0D;
            this.zedGraphControl.ScrollMaxY2 = 0D;
            this.zedGraphControl.ScrollMinX = 0D;
            this.zedGraphControl.ScrollMinY = 0D;
            this.zedGraphControl.ScrollMinY2 = 0D;
            this.zedGraphControl.MouseDownEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.zedGraphControl_MouseDownEvent);
            this.zedGraphControl.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.zedGraphControl_MouseMoveEvent);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.logXContextMenuItem,
            this.logYAxisContextMenuItem,
            this.showSampleTypesContextMenuItem,
            this.singleBatchContextMenuItem,
            this.showLegendContextMenuItem,
            this.showSelectionContextMenuItem,
            this.showFiguresOfMeritContextMenuItem,
            this.showBootstrapCurvesContextMenuItem,
            this.moreDisplayOptionsContextMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuStrip1, "contextMenuStrip1");
            // 
            // logXContextMenuItem
            // 
            this.logXContextMenuItem.Name = "logXContextMenuItem";
            resources.ApplyResources(this.logXContextMenuItem, "logXContextMenuItem");
            this.logXContextMenuItem.Click += new System.EventHandler(this.logXAxisContextMenuItem_Click);
            // 
            // logYAxisContextMenuItem
            // 
            this.logYAxisContextMenuItem.Name = "logYAxisContextMenuItem";
            resources.ApplyResources(this.logYAxisContextMenuItem, "logYAxisContextMenuItem");
            this.logYAxisContextMenuItem.Click += new System.EventHandler(this.logYAxisContextMenuItem_Click);
            // 
            // showSampleTypesContextMenuItem
            // 
            this.showSampleTypesContextMenuItem.Name = "showSampleTypesContextMenuItem";
            resources.ApplyResources(this.showSampleTypesContextMenuItem, "showSampleTypesContextMenuItem");
            // 
            // singleBatchContextMenuItem
            // 
            this.singleBatchContextMenuItem.Name = "singleBatchContextMenuItem";
            resources.ApplyResources(this.singleBatchContextMenuItem, "singleBatchContextMenuItem");
            this.singleBatchContextMenuItem.Click += new System.EventHandler(this.singleBatchContextMenuItem_Click);
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
            // showFiguresOfMeritContextMenuItem
            // 
            this.showFiguresOfMeritContextMenuItem.Name = "showFiguresOfMeritContextMenuItem";
            resources.ApplyResources(this.showFiguresOfMeritContextMenuItem, "showFiguresOfMeritContextMenuItem");
            this.showFiguresOfMeritContextMenuItem.Click += new System.EventHandler(this.showFiguresOfMeritContextMenuItem_Click);
            // 
            // showBootstrapCurvesContextMenuItem
            // 
            this.showBootstrapCurvesContextMenuItem.Name = "showBootstrapCurvesContextMenuItem";
            resources.ApplyResources(this.showBootstrapCurvesContextMenuItem, "showBootstrapCurvesContextMenuItem");
            this.showBootstrapCurvesContextMenuItem.Click += new System.EventHandler(this.showBootstrapCurvesToolStripMenuItem_Click);
            // 
            // moreDisplayOptionsContextMenuItem
            // 
            this.moreDisplayOptionsContextMenuItem.Name = "moreDisplayOptionsContextMenuItem";
            resources.ApplyResources(this.moreDisplayOptionsContextMenuItem, "moreDisplayOptionsContextMenuItem");
            this.moreDisplayOptionsContextMenuItem.Click += new System.EventHandler(this.moreDisplayOptionsContextMenuItem_Click);
            // 
            // CalibrationGraphControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.zedGraphControl);
            this.Name = "CalibrationGraphControl";
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ZedGraph.ZedGraphControl zedGraphControl;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem logXContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logYAxisContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSampleTypesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleBatchContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSelectionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showFiguresOfMeritContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showBootstrapCurvesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem moreDisplayOptionsContextMenuItem;
    }
}
