namespace pwiz.Topograph.ui.Controls
{
    partial class StatusBar
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblMemory = new System.Windows.Forms.Label();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.panelMemory = new System.Windows.Forms.Panel();
            this.panelChromatograms = new System.Windows.Forms.Panel();
            this.lblChromatograms = new System.Windows.Forms.Label();
            this.panelResults = new System.Windows.Forms.Panel();
            this.lblResults = new System.Windows.Forms.Label();
            this.updateTimer = new System.Windows.Forms.Timer(this.components);
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.flowLayoutPanel1.SuspendLayout();
            this.panelMemory.SuspendLayout();
            this.panelChromatograms.SuspendLayout();
            this.panelResults.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblMemory
            // 
            this.lblMemory.BackColor = System.Drawing.Color.Transparent;
            this.lblMemory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMemory.Location = new System.Drawing.Point(0, 0);
            this.lblMemory.Margin = new System.Windows.Forms.Padding(3);
            this.lblMemory.Name = "lblMemory";
            this.lblMemory.Size = new System.Drawing.Size(128, 19);
            this.lblMemory.TabIndex = 0;
            this.lblMemory.Text = "0/0 MB";
            this.lblMemory.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.toolTip.SetToolTip(this.lblMemory, "Memory Usage");
            this.lblMemory.Click += new System.EventHandler(this.LblMemoryClick);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.panelMemory);
            this.flowLayoutPanel1.Controls.Add(this.panelChromatograms);
            this.flowLayoutPanel1.Controls.Add(this.panelResults);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(215, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(517, 22);
            this.flowLayoutPanel1.TabIndex = 1;
            // 
            // panelMemory
            // 
            this.panelMemory.Controls.Add(this.lblMemory);
            this.panelMemory.Location = new System.Drawing.Point(386, 3);
            this.panelMemory.Name = "panelMemory";
            this.panelMemory.Size = new System.Drawing.Size(128, 19);
            this.panelMemory.TabIndex = 1;
            this.panelMemory.Paint += new System.Windows.Forms.PaintEventHandler(this.PanelMemoryPaint);
            // 
            // panelChromatograms
            // 
            this.panelChromatograms.Controls.Add(this.lblChromatograms);
            this.panelChromatograms.Location = new System.Drawing.Point(220, 3);
            this.panelChromatograms.Name = "panelChromatograms";
            this.panelChromatograms.Size = new System.Drawing.Size(160, 19);
            this.panelChromatograms.TabIndex = 2;
            this.panelChromatograms.Paint += new System.Windows.Forms.PaintEventHandler(this.PanelChromatogramsPaint);
            // 
            // lblChromatograms
            // 
            this.lblChromatograms.BackColor = System.Drawing.Color.Transparent;
            this.lblChromatograms.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblChromatograms.Location = new System.Drawing.Point(0, 0);
            this.lblChromatograms.Name = "lblChromatograms";
            this.lblChromatograms.Size = new System.Drawing.Size(160, 19);
            this.lblChromatograms.TabIndex = 3;
            this.lblChromatograms.Text = "0/0 Chromatograms";
            this.lblChromatograms.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panelResults
            // 
            this.panelResults.Controls.Add(this.lblResults);
            this.panelResults.Location = new System.Drawing.Point(86, 3);
            this.panelResults.Name = "panelResults";
            this.panelResults.Size = new System.Drawing.Size(128, 19);
            this.panelResults.TabIndex = 2;
            this.panelResults.Paint += new System.Windows.Forms.PaintEventHandler(this.PanelResultsPaint);
            // 
            // lblResults
            // 
            this.lblResults.BackColor = System.Drawing.Color.Transparent;
            this.lblResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblResults.Location = new System.Drawing.Point(0, 0);
            this.lblResults.Name = "lblResults";
            this.lblResults.Size = new System.Drawing.Size(128, 19);
            this.lblResults.TabIndex = 0;
            this.lblResults.Text = "0/0 Results";
            this.lblResults.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // updateTimer
            // 
            this.updateTimer.Enabled = true;
            this.updateTimer.Interval = 5000;
            this.updateTimer.Tick += new System.EventHandler(this.UpdateTimerTick);
            // 
            // StatusBar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "StatusBar";
            this.Size = new System.Drawing.Size(732, 22);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.panelMemory.ResumeLayout(false);
            this.panelChromatograms.ResumeLayout(false);
            this.panelResults.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblMemory;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Timer updateTimer;
        private System.Windows.Forms.Panel panelMemory;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.Panel panelChromatograms;
        private System.Windows.Forms.Label lblChromatograms;
        private System.Windows.Forms.Panel panelResults;
        private System.Windows.Forms.Label lblResults;
    }
}
