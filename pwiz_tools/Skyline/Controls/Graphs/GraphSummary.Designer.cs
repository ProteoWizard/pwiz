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
            this.comboOriginalReplicates = new System.Windows.Forms.ComboBox();
            this.comboBoxTargetReplicates = new System.Windows.Forms.ComboBox();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
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
            // comboOriginalReplicates
            // 
            this.comboOriginalReplicates.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboOriginalReplicates, "comboOriginalReplicates");
            this.comboOriginalReplicates.FormattingEnabled = true;
            this.comboOriginalReplicates.Name = "comboOriginalReplicates";
            this.comboOriginalReplicates.SelectedIndexChanged += new System.EventHandler(this.toolStripComboBoxOriginalReplicate_SelectedIndexChanged);
            // 
            // comboBoxTargetReplicates
            // 
            this.comboBoxTargetReplicates.BackColor = System.Drawing.SystemColors.Window;
            this.comboBoxTargetReplicates.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.comboBoxTargetReplicates, "comboBoxTargetReplicates");
            this.comboBoxTargetReplicates.FormattingEnabled = true;
            this.comboBoxTargetReplicates.Name = "comboBoxTargetReplicates";
            this.comboBoxTargetReplicates.SelectedIndexChanged += new System.EventHandler(this.toolStripComboBoxTargetReplicate_SelectedIndexChanged);
            // 
            // splitContainer
            // 
            resources.ApplyResources(this.splitContainer, "splitContainer");
            this.splitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.label1);
            this.splitContainer.Panel1.Controls.Add(this.comboOriginalReplicates);
            this.splitContainer.Panel1.Controls.Add(this.comboBoxTargetReplicates);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.graphControl);
            this.splitContainer.SizeChanged += new System.EventHandler(this.toolStrip_Resize);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // GraphSummary
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer);
            this.HideOnClose = true;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GraphSummary";
            this.ShowInTaskbar = false;
            this.VisibleChanged += new System.EventHandler(this.GraphSummary_VisibleChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GraphSummary_KeyDown);
            this.Resize += new System.EventHandler(this.GraphSummary_Resize);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ZedGraph.ZedGraphControl graphControl;
        private System.Windows.Forms.ComboBox comboOriginalReplicates;
        private System.Windows.Forms.ComboBox comboBoxTargetReplicates;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Label label1;

    }
}