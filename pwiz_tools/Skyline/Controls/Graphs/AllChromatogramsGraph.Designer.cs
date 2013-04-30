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
            this.panelMultifileProgress = new System.Windows.Forms.Panel();
            this.progressBarAllFiles = new System.Windows.Forms.ProgressBar();
            this.lblFileCount = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnCancelFile = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnHide = new System.Windows.Forms.Button();
            this.panelGraph = new System.Windows.Forms.Panel();
            this.asyncGraph = new pwiz.Skyline.Controls.Graphs.AsyncChromatogramsGraph();
            this.panelFileProgress = new System.Windows.Forms.Panel();
            this.progressBarFile = new System.Windows.Forms.ProgressBar();
            this.lblFileName = new System.Windows.Forms.Label();
            this.panelMultifileProgress.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panelGraph.SuspendLayout();
            this.panelFileProgress.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelMultifileProgress
            // 
            this.panelMultifileProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelMultifileProgress.Controls.Add(this.progressBarAllFiles);
            this.panelMultifileProgress.Controls.Add(this.lblFileCount);
            this.panelMultifileProgress.Location = new System.Drawing.Point(9, 366);
            this.panelMultifileProgress.Margin = new System.Windows.Forms.Padding(2);
            this.panelMultifileProgress.Name = "panelMultifileProgress";
            this.panelMultifileProgress.Size = new System.Drawing.Size(589, 36);
            this.panelMultifileProgress.TabIndex = 1;
            // 
            // progressBarAllFiles
            // 
            this.progressBarAllFiles.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBarAllFiles.Location = new System.Drawing.Point(1, 16);
            this.progressBarAllFiles.Margin = new System.Windows.Forms.Padding(2);
            this.progressBarAllFiles.Name = "progressBarAllFiles";
            this.progressBarAllFiles.Size = new System.Drawing.Size(587, 19);
            this.progressBarAllFiles.TabIndex = 1;
            // 
            // lblFileCount
            // 
            this.lblFileCount.AutoSize = true;
            this.lblFileCount.Location = new System.Drawing.Point(2, 0);
            this.lblFileCount.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblFileCount.Name = "lblFileCount";
            this.lblFileCount.Size = new System.Drawing.Size(57, 13);
            this.lblFileCount.TabIndex = 0;
            this.lblFileCount.Text = "X of Y files";
            // 
            // panel2
            // 
            this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel2.Controls.Add(this.btnCancelFile);
            this.panel2.Controls.Add(this.btnCancel);
            this.panel2.Controls.Add(this.btnHide);
            this.panel2.Location = new System.Drawing.Point(9, 413);
            this.panel2.Margin = new System.Windows.Forms.Padding(2);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(589, 21);
            this.panel2.TabIndex = 2;
            // 
            // btnCancelFile
            // 
            this.btnCancelFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancelFile.Location = new System.Drawing.Point(299, 0);
            this.btnCancelFile.Margin = new System.Windows.Forms.Padding(2);
            this.btnCancelFile.Name = "btnCancelFile";
            this.btnCancelFile.Size = new System.Drawing.Size(94, 21);
            this.btnCancelFile.TabIndex = 0;
            this.btnCancelFile.Text = "Cancel &File";
            this.btnCancelFile.UseVisualStyleBackColor = true;
            this.btnCancelFile.Click += new System.EventHandler(this.btnCancelFile_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(397, 0);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(94, 21);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel I&mport";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnHide
            // 
            this.btnHide.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnHide.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnHide.Location = new System.Drawing.Point(495, 0);
            this.btnHide.Margin = new System.Windows.Forms.Padding(2);
            this.btnHide.Name = "btnHide";
            this.btnHide.Size = new System.Drawing.Size(94, 21);
            this.btnHide.TabIndex = 2;
            this.btnHide.Text = "&Hide";
            this.btnHide.UseVisualStyleBackColor = true;
            this.btnHide.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // panelGraph
            // 
            this.panelGraph.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelGraph.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelGraph.Controls.Add(this.asyncGraph);
            this.panelGraph.Location = new System.Drawing.Point(0, 0);
            this.panelGraph.Name = "panelGraph";
            this.panelGraph.Size = new System.Drawing.Size(607, 319);
            this.panelGraph.TabIndex = 14;
            // 
            // asyncGraph
            // 
            this.asyncGraph.Dock = System.Windows.Forms.DockStyle.Fill;
            this.asyncGraph.Location = new System.Drawing.Point(0, 0);
            this.asyncGraph.Name = "asyncGraph";
            this.asyncGraph.Size = new System.Drawing.Size(607, 319);
            this.asyncGraph.TabIndex = 0;
            // 
            // panelFileProgress
            // 
            this.panelFileProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelFileProgress.Controls.Add(this.progressBarFile);
            this.panelFileProgress.Controls.Add(this.lblFileName);
            this.panelFileProgress.Location = new System.Drawing.Point(9, 326);
            this.panelFileProgress.Name = "panelFileProgress";
            this.panelFileProgress.Size = new System.Drawing.Size(589, 36);
            this.panelFileProgress.TabIndex = 0;
            // 
            // progressBarFile
            // 
            this.progressBarFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBarFile.Location = new System.Drawing.Point(1, 16);
            this.progressBarFile.Margin = new System.Windows.Forms.Padding(2);
            this.progressBarFile.Name = "progressBarFile";
            this.progressBarFile.Size = new System.Drawing.Size(587, 19);
            this.progressBarFile.TabIndex = 1;
            // 
            // lblFileName
            // 
            this.lblFileName.AutoSize = true;
            this.lblFileName.Location = new System.Drawing.Point(2, 0);
            this.lblFileName.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblFileName.Name = "lblFileName";
            this.lblFileName.Size = new System.Drawing.Size(49, 13);
            this.lblFileName.TabIndex = 0;
            this.lblFileName.Text = "file name";
            // 
            // AllChromatogramsGraph
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.CancelButton = this.btnHide;
            this.ClientSize = new System.Drawing.Size(607, 446);
            this.Controls.Add(this.panelFileProgress);
            this.Controls.Add(this.panelGraph);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panelMultifileProgress);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "AllChromatogramsGraph";
            this.ShowInTaskbar = false;
            this.Text = "Loading chromatograms...";
            this.Resize += new System.EventHandler(this.WindowResize);
            this.panelMultifileProgress.ResumeLayout(false);
            this.panelMultifileProgress.PerformLayout();
            this.panel2.ResumeLayout(false);
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
    }
}