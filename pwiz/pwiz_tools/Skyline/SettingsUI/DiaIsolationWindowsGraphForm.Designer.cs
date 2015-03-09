namespace pwiz.Skyline.SettingsUI
{
    partial class DiaIsolationWindowsGraphForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DiaIsolationWindowsGraphForm));
            this.zgIsolationGraph = new ZedGraph.ZedGraphControl();
            this.cbMargin = new System.Windows.Forms.CheckBox();
            this.btnClose = new System.Windows.Forms.Button();
            this.cbShowGaps = new System.Windows.Forms.CheckBox();
            this.cbShowOverlapsSingle = new System.Windows.Forms.CheckBox();
            this.cbShowOverlapRays = new System.Windows.Forms.CheckBox();
            this.labelWindow = new System.Windows.Forms.Label();
            this.labelWindowColor = new System.Windows.Forms.Label();
            this.labelMarginColor = new System.Windows.Forms.Label();
            this.labelGapColor = new System.Windows.Forms.Label();
            this.labelSingleOverlapColor = new System.Windows.Forms.Label();
            this.labelOverlapColor = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // zgIsolationGraph
            // 
            resources.ApplyResources(this.zgIsolationGraph, "zgIsolationGraph");
            this.zgIsolationGraph.Name = "zgIsolationGraph";
            this.zgIsolationGraph.ScrollGrace = 0D;
            this.zgIsolationGraph.ScrollMaxX = 0D;
            this.zgIsolationGraph.ScrollMaxY = 0D;
            this.zgIsolationGraph.ScrollMaxY2 = 0D;
            this.zgIsolationGraph.ScrollMinX = 0D;
            this.zgIsolationGraph.ScrollMinY = 0D;
            this.zgIsolationGraph.ScrollMinY2 = 0D;
            this.zgIsolationGraph.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zgIsolationGraph_ContextMenuBuilder);
            this.zgIsolationGraph.ZoomEvent += new ZedGraph.ZedGraphControl.ZoomEventHandler(this.zgIsolationWindow_ZoomEvent);
            this.zgIsolationGraph.ScrollEvent += new System.Windows.Forms.ScrollEventHandler(this.zgIsolationGraph_ScrollEvent);
            // 
            // cbMargin
            // 
            resources.ApplyResources(this.cbMargin, "cbMargin");
            this.cbMargin.Name = "cbMargin";
            this.cbMargin.UseVisualStyleBackColor = true;
            this.cbMargin.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // btnClose
            // 
            resources.ApplyResources(this.btnClose, "btnClose");
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Name = "btnClose";
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // cbShowGaps
            // 
            resources.ApplyResources(this.cbShowGaps, "cbShowGaps");
            this.cbShowGaps.Name = "cbShowGaps";
            this.cbShowGaps.UseVisualStyleBackColor = true;
            this.cbShowGaps.CheckedChanged += new System.EventHandler(this.cbShowGaps_CheckedChanged);
            // 
            // cbShowOverlapsSingle
            // 
            resources.ApplyResources(this.cbShowOverlapsSingle, "cbShowOverlapsSingle");
            this.cbShowOverlapsSingle.Name = "cbShowOverlapsSingle";
            this.cbShowOverlapsSingle.UseVisualStyleBackColor = true;
            this.cbShowOverlapsSingle.CheckedChanged += new System.EventHandler(this.cbShowOverlaps_CheckedChanged);
            // 
            // cbShowOverlapRays
            // 
            resources.ApplyResources(this.cbShowOverlapRays, "cbShowOverlapRays");
            this.cbShowOverlapRays.Name = "cbShowOverlapRays";
            this.cbShowOverlapRays.UseVisualStyleBackColor = true;
            this.cbShowOverlapRays.CheckedChanged += new System.EventHandler(this.cbShowOverlapRays_CheckedChanged);
            // 
            // labelWindow
            // 
            resources.ApplyResources(this.labelWindow, "labelWindow");
            this.labelWindow.Name = "labelWindow";
            // 
            // labelWindowColor
            // 
            resources.ApplyResources(this.labelWindowColor, "labelWindowColor");
            this.labelWindowColor.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.labelWindowColor.Name = "labelWindowColor";
            // 
            // labelMarginColor
            // 
            resources.ApplyResources(this.labelMarginColor, "labelMarginColor");
            this.labelMarginColor.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.labelMarginColor.Name = "labelMarginColor";
            // 
            // labelGapColor
            // 
            resources.ApplyResources(this.labelGapColor, "labelGapColor");
            this.labelGapColor.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.labelGapColor.Name = "labelGapColor";
            // 
            // labelSingleOverlapColor
            // 
            resources.ApplyResources(this.labelSingleOverlapColor, "labelSingleOverlapColor");
            this.labelSingleOverlapColor.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.labelSingleOverlapColor.Name = "labelSingleOverlapColor";
            // 
            // labelOverlapColor
            // 
            resources.ApplyResources(this.labelOverlapColor, "labelOverlapColor");
            this.labelOverlapColor.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.labelOverlapColor.Name = "labelOverlapColor";
            // 
            // DiaIsolationWindowsGraphForm
            // 
            this.AcceptButton = this.btnClose;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.Controls.Add(this.labelOverlapColor);
            this.Controls.Add(this.labelSingleOverlapColor);
            this.Controls.Add(this.labelGapColor);
            this.Controls.Add(this.labelMarginColor);
            this.Controls.Add(this.labelWindowColor);
            this.Controls.Add(this.labelWindow);
            this.Controls.Add(this.cbShowOverlapRays);
            this.Controls.Add(this.cbShowOverlapsSingle);
            this.Controls.Add(this.cbShowGaps);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.cbMargin);
            this.Controls.Add(this.zgIsolationGraph);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DiaIsolationWindowsGraphForm";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ZedGraph.ZedGraphControl zgIsolationGraph;
        private System.Windows.Forms.CheckBox cbMargin;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.CheckBox cbShowGaps;
        private System.Windows.Forms.CheckBox cbShowOverlapsSingle;
        private System.Windows.Forms.CheckBox cbShowOverlapRays;
        private System.Windows.Forms.Label labelWindow;
        private System.Windows.Forms.Label labelWindowColor;
        private System.Windows.Forms.Label labelMarginColor;
        private System.Windows.Forms.Label labelGapColor;
        private System.Windows.Forms.Label labelSingleOverlapColor;
        private System.Windows.Forms.Label labelOverlapColor;
    }
}