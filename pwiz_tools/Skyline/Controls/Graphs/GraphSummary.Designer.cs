namespace pwiz.Skyline.Controls.Graphs
{
    partial class GraphSummary
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphSummary));
            this.graphControl = new ZedGraph.ZedGraphControl();
            this.SuspendLayout();
            // 
            // graphControl
            // 
            resources.ApplyResources(this.graphControl, "graphControl");
            this.graphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphControl.IsShowCopyMessage = false;
            this.graphControl.IsSynchronizeXAxes = true;
            this.graphControl.IsZoomOnMouseCenter = true;
            this.graphControl.Name = "graphControl";
            this.graphControl.ScrollGrace = 0D;
            this.graphControl.ScrollMaxX = 0D;
            this.graphControl.ScrollMaxY = 0D;
            this.graphControl.ScrollMaxY2 = 0D;
            this.graphControl.ScrollMinX = 0D;
            this.graphControl.ScrollMinY = 0D;
            this.graphControl.ScrollMinY2 = 0D;
            this.graphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.graphControl_ContextMenuBuilder);
            this.graphControl.ZoomEvent += new ZedGraph.ZedGraphControl.ZoomEventHandler(this.graphControl_ZoomEvent);
            this.graphControl.MouseDownEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.graphControl_MouseDownEvent);
            this.graphControl.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.graphControl_MouseMoveEvent);
            // 
            // GraphSummary
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.graphControl);
            this.HideOnClose = true;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GraphSummary";
            this.ShowInTaskbar = false;
            this.VisibleChanged += new System.EventHandler(this.GraphSummary_VisibleChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GraphSummary_KeyDown);
            this.Resize += new System.EventHandler(this.GraphSummary_Resize);
            this.ResumeLayout(false);

        }

        #endregion

        private ZedGraph.ZedGraphControl graphControl;

    }
}