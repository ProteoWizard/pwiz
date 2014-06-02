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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphSpectrum));
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.comboSpectrum = new System.Windows.Forms.ToolStripComboBox();
            this.toolBar = new System.Windows.Forms.ToolStrip();
            this.GraphPanel = new System.Windows.Forms.Panel();
            this.graphControl = new pwiz.MSGraph.MSGraphControl();
            this.toolBar.SuspendLayout();
            this.GraphPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Name = "toolStripLabel1";
            resources.ApplyResources(this.toolStripLabel1, "toolStripLabel1");
            // 
            // comboSpectrum
            // 
            this.comboSpectrum.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboSpectrum, "comboSpectrum");
            this.comboSpectrum.Name = "comboSpectrum";
            this.comboSpectrum.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.comboSpectrum.SelectedIndexChanged += new System.EventHandler(this.comboSpectrum_SelectedIndexChanged);
            // 
            // toolBar
            // 
            this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.comboSpectrum});
            resources.ApplyResources(this.toolBar, "toolBar");
            this.toolBar.Name = "toolBar";
            // 
            // GraphPanel
            // 
            this.GraphPanel.Controls.Add(this.graphControl);
            resources.ApplyResources(this.GraphPanel, "GraphPanel");
            this.GraphPanel.Name = "GraphPanel";
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
            // 
            // GraphSpectrum
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.GraphPanel);
            this.Controls.Add(this.toolBar);
            this.HideOnClose = true;
            this.KeyPreview = true;
            this.Name = "GraphSpectrum";
            this.VisibleChanged += new System.EventHandler(this.GraphSpectrum_VisibleChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GraphSpectrum_KeyDown);
            this.toolBar.ResumeLayout(false);
            this.toolBar.PerformLayout();
            this.GraphPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripComboBox comboSpectrum;
        private System.Windows.Forms.ToolStrip toolBar;
        private System.Windows.Forms.Panel GraphPanel;
        private pwiz.MSGraph.MSGraphControl graphControl;


    }
}