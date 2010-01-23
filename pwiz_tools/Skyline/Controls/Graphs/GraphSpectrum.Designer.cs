namespace pwiz.Skyline.Controls.Graphs
{
    partial class GraphSpectrum
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
            pwiz.MSGraph.MSGraphPane msGraphPane1 = new pwiz.MSGraph.MSGraphPane();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphSpectrum));
            this.graphControl = new pwiz.MSGraph.MSGraphControl();
            this.SuspendLayout();
            // 
            // graphControl
            // 
            this.graphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphControl.IsShowCopyMessage = false;
            msGraphPane1.AllowCurveOverlap = false;
            msGraphPane1.BaseDimension = 8F;
            msGraphPane1.CurrentItemType = pwiz.MSGraph.MSGraphItemType.Unknown;
            msGraphPane1.IsAlignGrids = false;
            msGraphPane1.IsBoundedRanges = false;
            msGraphPane1.IsFontsScaled = false;
            msGraphPane1.IsIgnoreInitial = false;
            msGraphPane1.IsIgnoreMissing = false;
            msGraphPane1.IsPenWidthScaled = false;
            msGraphPane1.LineType = ZedGraph.LineType.Normal;
            msGraphPane1.Rect = ((System.Drawing.RectangleF)(resources.GetObject("msGraphPane1.Rect")));
            msGraphPane1.Tag = null;
            msGraphPane1.TitleGap = 0.5F;
            this.graphControl.GraphPane = msGraphPane1;
            this.graphControl.IsEnableVPan = false;
            this.graphControl.IsEnableVZoom = false;
            this.graphControl.Location = new System.Drawing.Point(0, 0);
            this.graphControl.Name = "graphControl";
            this.graphControl.ScrollGrace = 0;
            this.graphControl.ScrollMaxX = 0;
            this.graphControl.ScrollMaxY = 0;
            this.graphControl.ScrollMaxY2 = 0;
            this.graphControl.ScrollMinX = 0;
            this.graphControl.ScrollMinY = 0;
            this.graphControl.ScrollMinY2 = 0;
            this.graphControl.Size = new System.Drawing.Size(473, 386);
            this.graphControl.TabIndex = 0;
            this.graphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.graphControl_ContextMenuBuilder);
            // 
            // GraphSpectrum
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(473, 386);
            this.Controls.Add(this.graphControl);
            this.DockAreas = DigitalRune.Windows.Docking.DockAreas.Document;
            this.HideOnClose = true;
            this.KeyPreview = true;
            this.Name = "GraphSpectrum";
            this.TabText = "MS/MS Spectrum";
            this.Text = "GraphSpectrum";
            this.VisibleChanged += new System.EventHandler(this.GraphSpectrum_VisibleChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GraphSpectrum_KeyDown);
            this.ResumeLayout(false);

        }

        #endregion

        private pwiz.MSGraph.MSGraphControl graphControl;
    }
}