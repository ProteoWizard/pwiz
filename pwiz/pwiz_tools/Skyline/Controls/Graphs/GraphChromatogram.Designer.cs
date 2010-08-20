namespace pwiz.Skyline.Controls.Graphs
{
    partial class GraphChromatogram
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
            this.graphControl = new pwiz.MSGraph.MSGraphControl();
            this.toolBar = new System.Windows.Forms.ToolStrip();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.comboFiles = new System.Windows.Forms.ToolStripComboBox();
            this.toolBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // graphControl
            // 
            this.graphControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.graphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphControl.IsEnableVPan = false;
            this.graphControl.IsEnableVZoom = false;
            this.graphControl.IsShowCopyMessage = false;
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
            this.graphControl.MouseDownEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.graphControl_MouseDownEvent);
            this.graphControl.MouseUpEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.graphControl_MouseUpEvent);
            this.graphControl.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.graphControl_MouseMoveEvent);
            this.graphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.graphControl_ContextMenuBuilder);
            this.graphControl.ZoomEvent += new ZedGraph.ZedGraphControl.ZoomEventHandler(this.graphControl_ZoomEvent);
            // 
            // toolBar
            // 
            this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.comboFiles});
            this.toolBar.Location = new System.Drawing.Point(0, 0);
            this.toolBar.Name = "toolBar";
            this.toolBar.Size = new System.Drawing.Size(473, 25);
            this.toolBar.TabIndex = 1;
            this.toolBar.Visible = false;
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Size = new System.Drawing.Size(28, 22);
            this.toolStripLabel1.Text = "File:";
            // 
            // comboFiles
            // 
            this.comboFiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFiles.MaxDropDownItems = 15;
            this.comboFiles.Name = "comboFiles";
            this.comboFiles.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.comboFiles.Size = new System.Drawing.Size(200, 25);
            this.comboFiles.SelectedIndexChanged += new System.EventHandler(this.comboFiles_SelectedIndexChanged);
            // 
            // GraphChromatogram
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(473, 386);
            this.Controls.Add(this.toolBar);
            this.Controls.Add(this.graphControl);
            this.DockAreas = DigitalRune.Windows.Docking.DockAreas.Document;
            this.HideOnClose = true;
            this.KeyPreview = true;
            this.Name = "GraphChromatogram";
            this.TabText = "Chromatograms";
            this.VisibleChanged += new System.EventHandler(this.GraphChromatogram_VisibleChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GraphChromatogram_KeyDown);
            this.toolBar.ResumeLayout(false);
            this.toolBar.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private pwiz.MSGraph.MSGraphControl graphControl;
        private System.Windows.Forms.ToolStrip toolBar;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripComboBox comboFiles;
    }
}