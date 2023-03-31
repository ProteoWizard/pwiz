namespace pwiz.Skyline.EditUI.OptimizeTransitions
{
    partial class OptimizeTransitionsForm
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnApply = new System.Windows.Forms.Button();
            this.lblOriginal = new System.Windows.Forms.LinkLabel();
            this.lblOptimized = new System.Windows.Forms.LinkLabel();
            this.btnOptimizeDocumentTransitions = new System.Windows.Forms.Button();
            this.optimizeTransitionsSettingsControl1 = new pwiz.Skyline.EditUI.OptimizeTransitions.OptimizeTransitionsSettingsControl();
            this.calibrationGraphControl1 = new pwiz.Skyline.Controls.Graphs.Calibration.CalibrationGraphControl();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            this.databoundGridControl.Dock = System.Windows.Forms.DockStyle.Top;
            this.databoundGridControl.Location = new System.Drawing.Point(0, 94);
            this.databoundGridControl.Size = new System.Drawing.Size(800, 53);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnApply);
            this.panel1.Controls.Add(this.lblOriginal);
            this.panel1.Controls.Add(this.lblOptimized);
            this.panel1.Controls.Add(this.btnOptimizeDocumentTransitions);
            this.panel1.Controls.Add(this.optimizeTransitionsSettingsControl1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(800, 94);
            this.panel1.TabIndex = 1;
            // 
            // btnApply
            // 
            this.btnApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApply.Location = new System.Drawing.Point(635, 11);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(153, 23);
            this.btnApply.TabIndex = 3;
            this.btnApply.Text = "Apply Optimization";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            // 
            // lblOriginal
            // 
            this.lblOriginal.AutoSize = true;
            this.lblOriginal.Location = new System.Drawing.Point(3, 66);
            this.lblOriginal.Name = "lblOriginal";
            this.lblOriginal.Size = new System.Drawing.Size(62, 13);
            this.lblOriginal.TabIndex = 0;
            this.lblOriginal.TabStop = true;
            this.lblOriginal.Text = "Original {0}:";
            this.lblOriginal.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lblOriginal_LinkClicked);
            // 
            // lblOptimized
            // 
            this.lblOptimized.AutoSize = true;
            this.lblOptimized.Location = new System.Drawing.Point(202, 66);
            this.lblOptimized.Name = "lblOptimized";
            this.lblOptimized.Size = new System.Drawing.Size(73, 13);
            this.lblOptimized.TabIndex = 2;
            this.lblOptimized.TabStop = true;
            this.lblOptimized.Text = "Optimized {0}:";
            this.lblOptimized.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lblOptimized_LinkClicked);
            // 
            // btnOptimizeDocumentTransitions
            // 
            this.btnOptimizeDocumentTransitions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOptimizeDocumentTransitions.Location = new System.Drawing.Point(635, 40);
            this.btnOptimizeDocumentTransitions.Name = "btnOptimizeDocumentTransitions";
            this.btnOptimizeDocumentTransitions.Size = new System.Drawing.Size(153, 23);
            this.btnOptimizeDocumentTransitions.TabIndex = 1;
            this.btnOptimizeDocumentTransitions.Text = "Optimize Entire Document...";
            this.btnOptimizeDocumentTransitions.UseVisualStyleBackColor = true;
            this.btnOptimizeDocumentTransitions.Click += new System.EventHandler(this.btnOptimizeDocumentTransitions_Click);
            // 
            // optimizeTransitionsSettingsControl1
            // 
            this.optimizeTransitionsSettingsControl1.Location = new System.Drawing.Point(0, 0);
            this.optimizeTransitionsSettingsControl1.MinNumberOfTransitions = 4;
            this.optimizeTransitionsSettingsControl1.Name = "optimizeTransitionsSettingsControl1";
            this.optimizeTransitionsSettingsControl1.PreserveNonQuantitative = false;
            this.optimizeTransitionsSettingsControl1.Size = new System.Drawing.Size(629, 63);
            this.optimizeTransitionsSettingsControl1.SyncWithGlobalSettings = true;
            this.optimizeTransitionsSettingsControl1.TabIndex = 0;
            this.optimizeTransitionsSettingsControl1.SettingsChange += new System.EventHandler(this.optimizeTransitionsSettingsControl1_SettingsChanged);
            // 
            // calibrationGraphControl1
            // 
            this.calibrationGraphControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.calibrationGraphControl1.Location = new System.Drawing.Point(0, 0);
            this.calibrationGraphControl1.Name = "calibrationGraphControl1";
            this.calibrationGraphControl1.Size = new System.Drawing.Size(800, 173);
            this.calibrationGraphControl1.TabIndex = 2;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 147);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.calibrationGraphControl1);
            this.splitContainer1.Size = new System.Drawing.Size(800, 303);
            this.splitContainer1.SplitterDistance = 126;
            this.splitContainer1.TabIndex = 3;
            // 
            // OptimizeTransitionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.panel1);
            this.Name = "OptimizeTransitionsForm";
            this.TabText = "Optimize Transitions";
            this.Text = "Optimize Transitions";
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.databoundGridControl, 0);
            this.Controls.SetChildIndex(this.splitContainer1, 0);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private OptimizeTransitions.OptimizeTransitionsSettingsControl optimizeTransitionsSettingsControl1;
        private Controls.Graphs.Calibration.CalibrationGraphControl calibrationGraphControl1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btnOptimizeDocumentTransitions;
        private System.Windows.Forms.LinkLabel lblOriginal;
        private System.Windows.Forms.LinkLabel lblOptimized;
        private System.Windows.Forms.Button btnApply;
    }
}