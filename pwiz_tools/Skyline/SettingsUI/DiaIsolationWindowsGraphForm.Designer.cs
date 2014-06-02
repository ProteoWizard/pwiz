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
            // DiaIsolationWindowsGraphForm
            // 
            this.AcceptButton = this.btnClose;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
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
        private readonly string marginType;
        private readonly bool isIsolation;
        private System.Windows.Forms.Button btnClose;
    }
}