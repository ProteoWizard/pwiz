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
            this.zedGraphControl = new ZedGraph.ZedGraphControl();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.logXContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logYAxisContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSampleTypesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleBatchContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSelectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showFiguresOfMeritContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showBootstrapCurvesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // zedGraphControl
            // 
            this.zedGraphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zedGraphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.zedGraphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.zedGraphControl.IsShowCopyMessage = false;
            this.zedGraphControl.IsZoomOnMouseCenter = true;
            this.zedGraphControl.Location = new System.Drawing.Point(0, 0);
            this.zedGraphControl.Name = "zedGraphControl";
            this.zedGraphControl.ScrollGrace = 0D;
            this.zedGraphControl.ScrollMaxX = 0D;
            this.zedGraphControl.ScrollMaxY = 0D;
            this.zedGraphControl.ScrollMaxY2 = 0D;
            this.zedGraphControl.ScrollMinX = 0D;
            this.zedGraphControl.ScrollMinY = 0D;
            this.zedGraphControl.ScrollMinY2 = 0D;
            this.zedGraphControl.Size = new System.Drawing.Size(150, 150);
            this.zedGraphControl.TabIndex = 1;
            this.zedGraphControl.MouseDownEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.zedGraphControl_MouseDownEvent);
            this.zedGraphControl.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.zedGraphControl_MouseMoveEvent);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.logXContextMenuItem,
            this.logYAxisContextMenuItem,
            this.showToolStripMenuItem,
            this.showSampleTypesContextMenuItem,
            this.singleBatchContextMenuItem,
            this.showLegendContextMenuItem,
            this.showSelectionContextMenuItem,
            this.showFiguresOfMeritContextMenuItem,
            this.showBootstrapCurvesToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(199, 224);
            // 
            // logXContextMenuItem
            // 
            this.logXContextMenuItem.Name = "logXContextMenuItem";
            this.logXContextMenuItem.Size = new System.Drawing.Size(198, 22);
            this.logXContextMenuItem.Text = "Log X Axis";
            this.logXContextMenuItem.Click += new System.EventHandler(this.logXAxisContextMenuItem_Click);
            // 
            // logYAxisContextMenuItem
            // 
            this.logYAxisContextMenuItem.Name = "logYAxisContextMenuItem";
            this.logYAxisContextMenuItem.Size = new System.Drawing.Size(198, 22);
            this.logYAxisContextMenuItem.Text = "Log Y Axis";
            this.logYAxisContextMenuItem.Click += new System.EventHandler(this.logYAxisContextMenuItem_Click);
            // 
            // showToolStripMenuItem
            // 
            this.showToolStripMenuItem.Name = "showToolStripMenuItem";
            this.showToolStripMenuItem.Size = new System.Drawing.Size(198, 22);
            this.showToolStripMenuItem.Text = "Show Calibration Curve";
            // 
            // showSampleTypesContextMenuItem
            // 
            this.showSampleTypesContextMenuItem.Name = "showSampleTypesContextMenuItem";
            this.showSampleTypesContextMenuItem.Size = new System.Drawing.Size(198, 22);
            this.showSampleTypesContextMenuItem.Text = "Show Sample Types";
            // 
            // singleBatchContextMenuItem
            // 
            this.singleBatchContextMenuItem.Name = "singleBatchContextMenuItem";
            this.singleBatchContextMenuItem.Size = new System.Drawing.Size(198, 22);
            this.singleBatchContextMenuItem.Text = "Single Batch";
            this.singleBatchContextMenuItem.Click += new System.EventHandler(this.singleBatchContextMenuItem_Click);
            // 
            // showLegendContextMenuItem
            // 
            this.showLegendContextMenuItem.Name = "showLegendContextMenuItem";
            this.showLegendContextMenuItem.Size = new System.Drawing.Size(198, 22);
            this.showLegendContextMenuItem.Text = "Show Legend";
            this.showLegendContextMenuItem.Click += new System.EventHandler(this.showLegendContextMenuItem_Click);
            // 
            // showSelectionContextMenuItem
            // 
            this.showSelectionContextMenuItem.Name = "showSelectionContextMenuItem";
            this.showSelectionContextMenuItem.Size = new System.Drawing.Size(198, 22);
            this.showSelectionContextMenuItem.Text = "Show Selection";
            this.showSelectionContextMenuItem.Click += new System.EventHandler(this.showSelectionContextMenuItem_Click);
            // 
            // showFiguresOfMeritContextMenuItem
            // 
            this.showFiguresOfMeritContextMenuItem.Name = "showFiguresOfMeritContextMenuItem";
            this.showFiguresOfMeritContextMenuItem.Size = new System.Drawing.Size(198, 22);
            this.showFiguresOfMeritContextMenuItem.Text = "Show Figures of Merit";
            this.showFiguresOfMeritContextMenuItem.Click += new System.EventHandler(this.showFiguresOfMeritContextMenuItem_Click);
            // 
            // showBootstrapCurvesToolStripMenuItem
            // 
            this.showBootstrapCurvesToolStripMenuItem.Name = "showBootstrapCurvesToolStripMenuItem";
            this.showBootstrapCurvesToolStripMenuItem.Size = new System.Drawing.Size(198, 22);
            this.showBootstrapCurvesToolStripMenuItem.Text = "Show Bootstrap Curves";
            this.showBootstrapCurvesToolStripMenuItem.Click += new System.EventHandler(this.showBootstrapCurvesToolStripMenuItem_Click);
            // 
            // CalibrationGraphControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
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
        private System.Windows.Forms.ToolStripMenuItem showToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSampleTypesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleBatchContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSelectionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showFiguresOfMeritContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showBootstrapCurvesToolStripMenuItem;
    }
}
