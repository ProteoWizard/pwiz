namespace pwiz.Skyline.Controls
{
    partial class GraphPeakArea
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphPeakArea));
            this.graphControl = new ZedGraph.ZedGraphControl();
            this.SuspendLayout();
            // 
            // graphControl
            // 
            this.graphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphControl.Location = new System.Drawing.Point(0, 0);
            this.graphControl.Name = "graphControl";
            this.graphControl.ScrollGrace = 0;
            this.graphControl.ScrollMaxX = 0;
            this.graphControl.ScrollMaxY = 0;
            this.graphControl.ScrollMaxY2 = 0;
            this.graphControl.ScrollMinX = 0;
            this.graphControl.ScrollMinY = 0;
            this.graphControl.ScrollMinY2 = 0;
            this.graphControl.Size = new System.Drawing.Size(624, 444);
            this.graphControl.TabIndex = 0;
            this.graphControl.MouseDownEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.graphControl_MouseDownEvent);
            this.graphControl.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.graphControl_MouseMoveEvent);
            this.graphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.graphControl_ContextMenuBuilder);
            // 
            // GraphPeakArea
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(624, 444);
            this.Controls.Add(this.graphControl);
            this.HideOnClose = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GraphPeakArea";
            this.ShowInTaskbar = false;
            this.TabText = "Peak Areas";
            this.Text = "GraphPeakArea";
            this.VisibleChanged += new System.EventHandler(this.GraphPeakArea_VisibleChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GraphPeakArea_KeyDown);
            this.Resize += new System.EventHandler(this.GraphPeakArea_Resize);
            this.ResumeLayout(false);

        }

        #endregion

        private ZedGraph.ZedGraphControl graphControl;

    }
}