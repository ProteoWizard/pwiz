using System;

namespace pwiz.Skyline.Controls.Graphs
{
    partial class AllChromatogramsGraph
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AllChromatogramsGraph));
            this.imageListLock = new System.Windows.Forms.ImageList(this.components);
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.btnHide = new System.Windows.Forms.Button();
            this.imageListPushPin = new System.Windows.Forms.ImageList(this.components);
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelFileName = new System.Windows.Forms.Label();
            this.panelError = new System.Windows.Forms.Panel();
            this.textBoxError = new pwiz.Skyline.Controls.Graphs.DisabledRichTextBox();
            this.btnCopyText = new System.Windows.Forms.Button();
            this.cbMoreInfo = new System.Windows.Forms.CheckBox();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnAutoCloseWindow = new System.Windows.Forms.ToolStripButton();
            this.btnAutoScaleGraphs = new System.Windows.Forms.ToolStripButton();
            this.panelFileList = new System.Windows.Forms.Panel();
            this.flowFileStatus = new System.Windows.Forms.FlowLayoutPanel();
            this.panelStatus = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.lblDuration = new System.Windows.Forms.Label();
            this.progressBarTotal = new System.Windows.Forms.ProgressBar();
            this.graphChromatograms = new pwiz.Skyline.Controls.Graphs.AsyncChromatogramsGraph2();
            this.elapsedTimer = new System.Windows.Forms.Timer(this.components);
            this.panelError.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.panelFileList.SuspendLayout();
            this.panelStatus.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // imageListLock
            // 
            this.imageListLock.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListLock.ImageStream")));
            this.imageListLock.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListLock.Images.SetKeyName(0, "Locked.bmp");
            this.imageListLock.Images.SetKeyName(1, "Unlocked.bmp");
            // 
            // btnHide
            // 
            resources.ApplyResources(this.btnHide, "btnHide");
            this.btnHide.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnHide.Name = "btnHide";
            this.toolTip1.SetToolTip(this.btnHide, resources.GetString("btnHide.ToolTip"));
            this.btnHide.UseVisualStyleBackColor = true;
            this.btnHide.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // imageListPushPin
            // 
            this.imageListPushPin.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListPushPin.ImageStream")));
            this.imageListPushPin.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListPushPin.Images.SetKeyName(0, "Pindownlight.bmp");
            this.imageListPushPin.Images.SetKeyName(1, "Pinleftlight.bmp");
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // labelFileName
            // 
            resources.ApplyResources(this.labelFileName, "labelFileName");
            this.labelFileName.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.labelFileName.Name = "labelFileName";
            // 
            // panelError
            // 
            this.panelError.BackColor = System.Drawing.SystemColors.Window;
            this.panelError.Controls.Add(this.textBoxError);
            this.panelError.Controls.Add(this.btnCopyText);
            this.panelError.Controls.Add(this.cbMoreInfo);
            resources.ApplyResources(this.panelError, "panelError");
            this.panelError.Name = "panelError";
            // 
            // textBoxError
            // 
            this.textBoxError.BackColor = System.Drawing.SystemColors.ControlLight;
            this.textBoxError.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxError.Cursor = System.Windows.Forms.Cursors.Default;
            resources.ApplyResources(this.textBoxError, "textBoxError");
            this.textBoxError.Name = "textBoxError";
            this.textBoxError.ReadOnly = true;
            this.textBoxError.ShowSelectionMargin = true;
            // 
            // btnCopyText
            // 
            resources.ApplyResources(this.btnCopyText, "btnCopyText");
            this.btnCopyText.Name = "btnCopyText";
            this.btnCopyText.UseVisualStyleBackColor = true;
            this.btnCopyText.Click += new System.EventHandler(this.btnCopyText_Click);
            // 
            // cbMoreInfo
            // 
            resources.ApplyResources(this.cbMoreInfo, "cbMoreInfo");
            this.cbMoreInfo.Name = "cbMoreInfo";
            this.cbMoreInfo.UseVisualStyleBackColor = true;
            this.cbMoreInfo.CheckedChanged += new System.EventHandler(this.cbShowErrorDetails_CheckedChanged);
            // 
            // toolStrip1
            // 
            this.toolStrip1.BackColor = System.Drawing.SystemColors.Window;
            resources.ApplyResources(this.toolStrip1, "toolStrip1");
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnAutoCloseWindow,
            this.btnAutoScaleGraphs});
            this.toolStrip1.Name = "toolStrip1";
            // 
            // btnAutoCloseWindow
            // 
            this.btnAutoCloseWindow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnAutoCloseWindow, "btnAutoCloseWindow");
            this.btnAutoCloseWindow.Name = "btnAutoCloseWindow";
            this.btnAutoCloseWindow.Click += new System.EventHandler(this.btnAutoCloseWindow_Click);
            // 
            // btnAutoScaleGraphs
            // 
            this.btnAutoScaleGraphs.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.btnAutoScaleGraphs, "btnAutoScaleGraphs");
            this.btnAutoScaleGraphs.Name = "btnAutoScaleGraphs";
            this.btnAutoScaleGraphs.Click += new System.EventHandler(this.btnAutoScaleGraphs_Click);
            // 
            // panelFileList
            // 
            this.panelFileList.BackColor = System.Drawing.SystemColors.Window;
            this.panelFileList.Controls.Add(this.flowFileStatus);
            resources.ApplyResources(this.panelFileList, "panelFileList");
            this.panelFileList.Name = "panelFileList";
            // 
            // flowFileStatus
            // 
            resources.ApplyResources(this.flowFileStatus, "flowFileStatus");
            this.flowFileStatus.BackColor = System.Drawing.SystemColors.ControlLight;
            this.flowFileStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.flowFileStatus.Name = "flowFileStatus";
            // 
            // panelStatus
            // 
            this.panelStatus.BackColor = System.Drawing.SystemColors.Control;
            this.panelStatus.Controls.Add(this.panel2);
            resources.ApplyResources(this.panelStatus, "panelStatus");
            this.panelStatus.Name = "panelStatus";
            // 
            // panel2
            // 
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Controls.Add(this.btnCancel);
            this.panel2.Controls.Add(this.lblDuration);
            this.panel2.Controls.Add(this.btnHide);
            this.panel2.Controls.Add(this.progressBarTotal);
            this.panel2.Name = "panel2";
            // 
            // lblDuration
            // 
            resources.ApplyResources(this.lblDuration, "lblDuration");
            this.lblDuration.BackColor = System.Drawing.SystemColors.Control;
            this.lblDuration.Name = "lblDuration";
            // 
            // progressBarTotal
            // 
            resources.ApplyResources(this.progressBarTotal, "progressBarTotal");
            this.progressBarTotal.Name = "progressBarTotal";
            // 
            // graphChromatograms
            // 
            resources.ApplyResources(this.graphChromatograms, "graphChromatograms");
            this.graphChromatograms.IsCanceled = false;
            this.graphChromatograms.Key = null;
            this.graphChromatograms.Name = "graphChromatograms";
            this.graphChromatograms.ScaleIsLocked = false;
            // 
            // elapsedTimer
            // 
            this.elapsedTimer.Enabled = true;
            this.elapsedTimer.Interval = 500;
            // 
            // AllChromatogramsGraph
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ControlBox = false;
            this.Controls.Add(this.panelError);
            this.Controls.Add(this.graphChromatograms);
            this.Controls.Add(this.labelFileName);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.panelFileList);
            this.Controls.Add(this.panelStatus);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AllChromatogramsGraph";
            this.ShowInTaskbar = false;
            this.panelError.ResumeLayout(false);
            this.panelError.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.panelFileList.ResumeLayout(false);
            this.panelFileList.PerformLayout();
            this.panelStatus.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnHide;
        private System.Windows.Forms.ProgressBar progressBarTotal;
        private System.Windows.Forms.Label lblDuration;
        private AsyncChromatogramsGraph2 graphChromatograms;
        private System.Windows.Forms.ImageList imageListLock;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ImageList imageListPushPin;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnAutoCloseWindow;
        private System.Windows.Forms.ToolStripButton btnAutoScaleGraphs;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Panel panelFileList;
        private System.Windows.Forms.FlowLayoutPanel flowFileStatus;
        private System.Windows.Forms.Panel panelStatus;
        private System.Windows.Forms.Label labelFileName;
        private DisabledRichTextBox textBoxError;
        private System.Windows.Forms.Panel panelError;
        private System.Windows.Forms.Button btnCopyText;
        private System.Windows.Forms.CheckBox cbMoreInfo;
        private System.Windows.Forms.Timer elapsedTimer;
    }
}