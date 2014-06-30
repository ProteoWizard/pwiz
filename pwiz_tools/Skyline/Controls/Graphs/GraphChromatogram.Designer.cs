using System;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphChromatogram));
            this.graphControl = new pwiz.MSGraph.MSGraphControl();
            this.toolBar = new System.Windows.Forms.ToolStrip();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.comboFiles = new System.Windows.Forms.ToolStripComboBox();
            this.toolBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // graphControl
            // 
            resources.ApplyResources(this.graphControl, "graphControl");
            this.graphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphControl.IsEnableVPan = false;
            this.graphControl.IsEnableVZoom = false;
            this.graphControl.IsShowCopyMessage = false;
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
            this.graphControl.MouseUpEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.graphControl_MouseUpEvent);
            this.graphControl.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.graphControl_MouseMoveEvent);
            this.graphControl.MouseLeave += new System.EventHandler(this.graphControl_MouseLeaveEvent);
            // 
            // toolBar
            // 
            this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.comboFiles});
            resources.ApplyResources(this.toolBar, "toolBar");
            this.toolBar.Name = "toolBar";
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Name = "toolStripLabel1";
            resources.ApplyResources(this.toolStripLabel1, "toolStripLabel1");
            // 
            // comboFiles
            // 
            this.comboFiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboFiles, "comboFiles");
            this.comboFiles.Name = "comboFiles";
            this.comboFiles.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.comboFiles.SelectedIndexChanged += new System.EventHandler(this.comboFiles_SelectedIndexChanged);
            // 
            // GraphChromatogram
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.toolBar);
            this.Controls.Add(this.graphControl);
            this.DockAreas = DigitalRune.Windows.Docking.DockAreas.Document;
            this.HideOnClose = true;
            this.KeyPreview = true;
            this.Name = "GraphChromatogram";
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