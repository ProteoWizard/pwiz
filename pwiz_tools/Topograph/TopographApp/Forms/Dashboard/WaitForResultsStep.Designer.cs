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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WaitForResultsStep));
            this.lblDescription = new System.Windows.Forms.Label();
            this.progressBarChromatograms = new System.Windows.Forms.ProgressBar();
            this.lblChromatogramStatus = new System.Windows.Forms.Label();
            this.lblResultsStatus = new System.Windows.Forms.Label();
            this.progressBarResults = new System.Windows.Forms.ProgressBar();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.panelForeignLocks = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnCancelTasks = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.lblForeignLocks = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.panelForeignLocks.SuspendLayout();
            this.panel2.SuspendLayout();
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
            this.timer.Tick += new System.EventHandler(this.TimerOnTick);
            // 
            // panelForeignLocks
            // 
            this.panelForeignLocks.Controls.Add(this.panel2);
            this.panelForeignLocks.Controls.Add(this.label2);
            this.panelForeignLocks.Controls.Add(this.lblForeignLocks);
            this.panelForeignLocks.Controls.Add(this.label1);
            this.panelForeignLocks.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelForeignLocks.Location = new System.Drawing.Point(0, 85);
            this.panelForeignLocks.Name = "panelForeignLocks";
            this.panelForeignLocks.Size = new System.Drawing.Size(623, 173);
            this.panelForeignLocks.TabIndex = 5;
            this.panelForeignLocks.Visible = false;
            // 
            // panel2
            // 
            this.panel2.AutoSize = true;
            this.panel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel2.Controls.Add(this.btnCancelTasks);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 138);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(623, 29);
            this.panel2.TabIndex = 7;
            // 
            // btnCancelTasks
            // 
            this.btnCancelTasks.AutoSize = true;
            this.btnCancelTasks.Location = new System.Drawing.Point(3, 3);
            this.btnCancelTasks.Name = "btnCancelTasks";
            this.btnCancelTasks.Size = new System.Drawing.Size(111, 23);
            this.btnCancelTasks.TabIndex = 6;
            this.btnCancelTasks.Text = "Cancel Other Tasks";
            this.btnCancelTasks.UseVisualStyleBackColor = true;
            this.btnCancelTasks.Click += new System.EventHandler(this.BtnCancelTasksOnClick);
            // 
            // label2
            // 
            this.label2.Dock = System.Windows.Forms.DockStyle.Top;
            this.label2.Location = new System.Drawing.Point(0, 74);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(623, 64);
            this.label2.TabIndex = 2;
            this.label2.Text = resources.GetString("label2.Text");
            // 
            // lblForeignLocks
            // 
            this.lblForeignLocks.AutoSize = true;
            this.lblForeignLocks.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblForeignLocks.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblForeignLocks.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblForeignLocks.Location = new System.Drawing.Point(0, 61);
            this.lblForeignLocks.Name = "lblForeignLocks";
            this.lblForeignLocks.Size = new System.Drawing.Size(539, 13);
            this.lblForeignLocks.TabIndex = 1;
            this.lblForeignLocks.Text = "The database says that there are ## tasks being worked on by other instances of T" +
    "opograph.";
            // 
            // label1
            // 
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(623, 61);
            this.label1.TabIndex = 0;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // WaitForResultsStep
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelForeignLocks);
            this.Controls.Add(this.progressBarResults);
            this.Controls.Add(this.lblResultsStatus);
            this.Controls.Add(this.progressBarChromatograms);
            this.Controls.Add(this.lblChromatogramStatus);
            this.Controls.Add(this.lblDescription);
            this.Name = "WaitForResultsStep";
            this.Size = new System.Drawing.Size(623, 289);
            this.panelForeignLocks.ResumeLayout(false);
            this.panelForeignLocks.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
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
        private System.Windows.Forms.Panel panelForeignLocks;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lblForeignLocks;
        private System.Windows.Forms.Button btnCancelTasks;
        private System.Windows.Forms.Panel panel2;
    }
}
