using System;

namespace pwiz.Skyline.Controls.Graphs
{
    partial class AllChromatogramsGraph
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AllChromatogramsGraph));
            this.panelMultifileProgress = new System.Windows.Forms.Panel();
            this.progressBarAllFiles = new System.Windows.Forms.ProgressBar();
            this.lblFileCount = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.lblDuration = new System.Windows.Forms.Label();
            this.btnCancelFile = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnHide = new System.Windows.Forms.Button();
            this.panelGraph = new System.Windows.Forms.Panel();
            this.asyncGraph = new pwiz.Skyline.Controls.Graphs.AsyncChromatogramsGraph();
            this.panelFileProgress = new System.Windows.Forms.Panel();
            this.progressBarFile = new System.Windows.Forms.ProgressBar();
            this.lblFileName = new System.Windows.Forms.Label();
            this.lblWarning = new System.Windows.Forms.Label();
            this.panelMultifileProgress.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panelGraph.SuspendLayout();
            this.panelFileProgress.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelMultifileProgress
            // 
            resources.ApplyResources(this.panelMultifileProgress, "panelMultifileProgress");
            this.panelMultifileProgress.Controls.Add(this.progressBarAllFiles);
            this.panelMultifileProgress.Controls.Add(this.lblFileCount);
            this.panelMultifileProgress.Name = "panelMultifileProgress";
            // 
            // progressBarAllFiles
            // 
            resources.ApplyResources(this.progressBarAllFiles, "progressBarAllFiles");
            this.progressBarAllFiles.Name = "progressBarAllFiles";
            // 
            // lblFileCount
            // 
            resources.ApplyResources(this.lblFileCount, "lblFileCount");
            this.lblFileCount.Name = "lblFileCount";
            // 
            // panel2
            // 
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Controls.Add(this.lblDuration);
            this.panel2.Controls.Add(this.btnCancelFile);
            this.panel2.Controls.Add(this.btnCancel);
            this.panel2.Controls.Add(this.btnHide);
            this.panel2.Name = "panel2";
            // 
            // lblDuration
            // 
            resources.ApplyResources(this.lblDuration, "lblDuration");
            this.lblDuration.Name = "lblDuration";
            // 
            // btnCancelFile
            // 
            resources.ApplyResources(this.btnCancelFile, "btnCancelFile");
            this.btnCancelFile.Name = "btnCancelFile";
            this.btnCancelFile.UseVisualStyleBackColor = true;
            this.btnCancelFile.Click += new System.EventHandler(this.btnCancelFile_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnHide
            // 
            resources.ApplyResources(this.btnHide, "btnHide");
            this.btnHide.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnHide.Name = "btnHide";
            this.btnHide.UseVisualStyleBackColor = true;
            this.btnHide.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // panelGraph
            // 
            resources.ApplyResources(this.panelGraph, "panelGraph");
            this.panelGraph.Controls.Add(this.lblWarning);
            this.panelGraph.Controls.Add(this.asyncGraph);
            this.panelGraph.Name = "panelGraph";
            // 
            // asyncGraph
            // 
            resources.ApplyResources(this.asyncGraph, "asyncGraph");
            this.asyncGraph.Name = "asyncGraph";
            // 
            // panelFileProgress
            // 
            resources.ApplyResources(this.panelFileProgress, "panelFileProgress");
            this.panelFileProgress.Controls.Add(this.progressBarFile);
            this.panelFileProgress.Controls.Add(this.lblFileName);
            this.panelFileProgress.Name = "panelFileProgress";
            // 
            // progressBarFile
            // 
            resources.ApplyResources(this.progressBarFile, "progressBarFile");
            this.progressBarFile.Name = "progressBarFile";
            // 
            // lblFileName
            // 
            resources.ApplyResources(this.lblFileName, "lblFileName");
            this.lblFileName.Name = "lblFileName";
            // 
            // lblWarning
            // 
            resources.ApplyResources(this.lblWarning, "lblWarning");
            this.lblWarning.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblWarning.Name = "lblWarning";
            // 
            // AllChromatogramsGraph
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.CancelButton = this.btnHide;
            this.Controls.Add(this.panelFileProgress);
            this.Controls.Add(this.panelGraph);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panelMultifileProgress);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "AllChromatogramsGraph";
            this.ShowInTaskbar = false;
            this.panelMultifileProgress.ResumeLayout(false);
            this.panelMultifileProgress.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panelGraph.ResumeLayout(false);
            this.panelFileProgress.ResumeLayout(false);
            this.panelFileProgress.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelMultifileProgress;
        private System.Windows.Forms.ProgressBar progressBarAllFiles;
        private System.Windows.Forms.Label lblFileCount;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnCancelFile;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnHide;
        private System.Windows.Forms.Panel panelGraph;
        private System.Windows.Forms.Panel panelFileProgress;
        private System.Windows.Forms.ProgressBar progressBarFile;
        private System.Windows.Forms.Label lblFileName;
        private AsyncChromatogramsGraph asyncGraph;
        private System.Windows.Forms.Label lblDuration;
        private System.Windows.Forms.Label lblWarning;
    }
}