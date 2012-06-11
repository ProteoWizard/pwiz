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
            this.toolStripLabel1.Size = new System.Drawing.Size(61, 22);
            this.toolStripLabel1.Text = "Spectrum:";
            // 
            // comboSpectrum
            // 
            this.comboSpectrum.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSpectrum.MaxDropDownItems = 15;
            this.comboSpectrum.Name = "comboSpectrum";
            this.comboSpectrum.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.comboSpectrum.Size = new System.Drawing.Size(200, 25);
            this.comboSpectrum.SelectedIndexChanged += new System.EventHandler(this.comboSpectrum_SelectedIndexChanged);
            // 
            // toolBar
            // 
            this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.comboSpectrum});
            this.toolBar.Location = new System.Drawing.Point(0, 0);
            this.toolBar.Name = "toolBar";
            this.toolBar.Size = new System.Drawing.Size(473, 25);
            this.toolBar.TabIndex = 2;
            this.toolBar.Visible = false;
            // 
            // GraphPanel
            // 
            this.GraphPanel.Controls.Add(this.graphControl);
            this.GraphPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GraphPanel.Location = new System.Drawing.Point(0, 0);
            this.GraphPanel.Name = "GraphPanel";
            this.GraphPanel.Size = new System.Drawing.Size(473, 386);
            this.GraphPanel.TabIndex = 3;
            // 
            // graphControl
            // 
            this.graphControl.Dock = System.Windows.Forms.DockStyle.Fill;
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
            this.graphControl.TabIndex = 1;
            this.graphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.graphControl_ContextMenuBuilder);
            // 
            // GraphSpectrum
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(473, 386);
            this.Controls.Add(this.GraphPanel);
            this.Controls.Add(this.toolBar);
            this.HideOnClose = true;
            this.KeyPreview = true;
            this.Name = "GraphSpectrum";
            this.TabText = "MS/MS Spectrum";
            this.Text = "GraphSpectrum";
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