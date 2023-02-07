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
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.logXContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logYAxisContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSampleTypesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleBatchContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSelectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showFiguresOfMeritContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.calibrationGraphControl1 = new pwiz.Skyline.Controls.Graphs.Calibration.CalibrationGraphControl();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
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
            this.showFiguresOfMeritContextMenuItem});
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
            // calibrationGraphControl1
            // 
            resources.ApplyResources(this.calibrationGraphControl1, "calibrationGraphControl1");
            this.calibrationGraphControl1.GraphTitle = "Title";
            this.calibrationGraphControl1.ModeUIAwareFormHelper = null;
            this.calibrationGraphControl1.Name = "calibrationGraphControl1";
            this.calibrationGraphControl1.PointClicked += new System.Action<pwiz.Skyline.Model.DocSettings.AbsoluteQuantification.CalibrationPoint>(this.calibrationGraphControl1_PointClicked);
            // 
            // CalibrationForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.calibrationGraphControl1);
            this.KeyPreview = true;
            this.Name = "CalibrationForm";
            this.ShowInTaskbar = false;
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CalibrationForm_KeyDown);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem logXContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSampleTypesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSelectionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showFiguresOfMeritContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logYAxisContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleBatchContextMenuItem;
        private CalibrationGraphControl calibrationGraphControl1;
    }
}