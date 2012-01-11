namespace pwiz.Topograph.ui.Forms.Dashboard
{
    partial class WaitForResultsStep
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
            this.lblDescription = new System.Windows.Forms.Label();
            this.progressBarChromatograms = new System.Windows.Forms.ProgressBar();
            this.lblChromatogramStatus = new System.Windows.Forms.Label();
            this.lblResultsStatus = new System.Windows.Forms.Label();
            this.progressBarResults = new System.Windows.Forms.ProgressBar();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblDescription.Location = new System.Drawing.Point(0, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(434, 13);
            this.lblDescription.TabIndex = 0;
            this.lblDescription.Text = "Topograph can take a long time to generate the chromatograms and calculate your r" +
                "esults.";
            // 
            // progressBarChromatograms
            // 
            this.progressBarChromatograms.Dock = System.Windows.Forms.DockStyle.Top;
            this.progressBarChromatograms.Location = new System.Drawing.Point(0, 26);
            this.progressBarChromatograms.MarqueeAnimationSpeed = 0;
            this.progressBarChromatograms.Name = "progressBarChromatograms";
            this.progressBarChromatograms.Size = new System.Drawing.Size(623, 23);
            this.progressBarChromatograms.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBarChromatograms.TabIndex = 1;
            // 
            // lblChromatogramStatus
            // 
            this.lblChromatogramStatus.AutoSize = true;
            this.lblChromatogramStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblChromatogramStatus.Location = new System.Drawing.Point(0, 13);
            this.lblChromatogramStatus.Name = "lblChromatogramStatus";
            this.lblChromatogramStatus.Size = new System.Drawing.Size(176, 13);
            this.lblChromatogramStatus.TabIndex = 2;
            this.lblChromatogramStatus.Text = "Progress generating chromatograms";
            // 
            // lblResultsStatus
            // 
            this.lblResultsStatus.AutoSize = true;
            this.lblResultsStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblResultsStatus.Location = new System.Drawing.Point(0, 49);
            this.lblResultsStatus.Name = "lblResultsStatus";
            this.lblResultsStatus.Size = new System.Drawing.Size(135, 13);
            this.lblResultsStatus.TabIndex = 3;
            this.lblResultsStatus.Text = "Progress calculating results";
            // 
            // progressBarResults
            // 
            this.progressBarResults.Dock = System.Windows.Forms.DockStyle.Top;
            this.progressBarResults.Location = new System.Drawing.Point(0, 62);
            this.progressBarResults.MarqueeAnimationSpeed = 0;
            this.progressBarResults.Name = "progressBarResults";
            this.progressBarResults.Size = new System.Drawing.Size(623, 23);
            this.progressBarResults.TabIndex = 4;
            // 
            // timer
            // 
            this.timer.Enabled = true;
            this.timer.Interval = 60000;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // WaitForResultsStep
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.progressBarResults);
            this.Controls.Add(this.lblResultsStatus);
            this.Controls.Add(this.progressBarChromatograms);
            this.Controls.Add(this.lblChromatogramStatus);
            this.Controls.Add(this.lblDescription);
            this.Name = "WaitForResultsStep";
            this.Size = new System.Drawing.Size(623, 289);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.ProgressBar progressBarChromatograms;
        private System.Windows.Forms.Label lblChromatogramStatus;
        private System.Windows.Forms.Label lblResultsStatus;
        private System.Windows.Forms.ProgressBar progressBarResults;
        private System.Windows.Forms.Timer timer;
    }
}
