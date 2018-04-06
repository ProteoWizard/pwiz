namespace pwiz.Skyline.Alerts
{
    partial class ShareTypeDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ShareTypeDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.panelButtonBar = new System.Windows.Forms.FlowLayoutPanel();
            this.btnShare = new System.Windows.Forms.Button();
            this.groupBoxFileFormat = new System.Windows.Forms.GroupBox();
            this.comboSkylineVersion = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBoxShareType = new System.Windows.Forms.GroupBox();
            this.flowLayoutPanelMinimalComplete = new System.Windows.Forms.FlowLayoutPanel();
            this.radioMinimal = new System.Windows.Forms.RadioButton();
            this.radioComplete = new System.Windows.Forms.RadioButton();
            this.lblShareTypeFooter = new System.Windows.Forms.Label();
            this.lblLibraries = new System.Windows.Forms.Label();
            this.lblRetentionTimeCalculator = new System.Windows.Forms.Label();
            this.lblBackgroundProteome = new System.Windows.Forms.Label();
            this.lblShareTypeHeader = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panelFileFormat = new System.Windows.Forms.Panel();
            this.panelButtonBar.SuspendLayout();
            this.groupBoxFileFormat.SuspendLayout();
            this.groupBoxShareType.SuspendLayout();
            this.flowLayoutPanelMinimalComplete.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panelFileFormat.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // panelButtonBar
            // 
            this.panelButtonBar.BackColor = System.Drawing.SystemColors.Control;
            this.panelButtonBar.Controls.Add(this.btnCancel);
            this.panelButtonBar.Controls.Add(this.btnShare);
            resources.ApplyResources(this.panelButtonBar, "panelButtonBar");
            this.panelButtonBar.Name = "panelButtonBar";
            // 
            // btnShare
            // 
            resources.ApplyResources(this.btnShare, "btnShare");
            this.btnShare.Name = "btnShare";
            this.btnShare.UseVisualStyleBackColor = true;
            this.btnShare.Click += new System.EventHandler(this.btnShare_Click);
            // 
            // groupBoxFileFormat
            // 
            resources.ApplyResources(this.groupBoxFileFormat, "groupBoxFileFormat");
            this.groupBoxFileFormat.Controls.Add(this.comboSkylineVersion);
            this.groupBoxFileFormat.Controls.Add(this.label2);
            this.groupBoxFileFormat.Name = "groupBoxFileFormat";
            this.groupBoxFileFormat.TabStop = false;
            // 
            // comboSkylineVersion
            // 
            resources.ApplyResources(this.comboSkylineVersion, "comboSkylineVersion");
            this.comboSkylineVersion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSkylineVersion.FormattingEnabled = true;
            this.comboSkylineVersion.Name = "comboSkylineVersion";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // groupBoxShareType
            // 
            resources.ApplyResources(this.groupBoxShareType, "groupBoxShareType");
            this.groupBoxShareType.Controls.Add(this.flowLayoutPanelMinimalComplete);
            this.groupBoxShareType.Controls.Add(this.lblShareTypeFooter);
            this.groupBoxShareType.Controls.Add(this.lblLibraries);
            this.groupBoxShareType.Controls.Add(this.lblRetentionTimeCalculator);
            this.groupBoxShareType.Controls.Add(this.lblBackgroundProteome);
            this.groupBoxShareType.Controls.Add(this.lblShareTypeHeader);
            this.groupBoxShareType.Name = "groupBoxShareType";
            this.groupBoxShareType.TabStop = false;
            // 
            // flowLayoutPanelMinimalComplete
            // 
            resources.ApplyResources(this.flowLayoutPanelMinimalComplete, "flowLayoutPanelMinimalComplete");
            this.flowLayoutPanelMinimalComplete.Controls.Add(this.radioMinimal);
            this.flowLayoutPanelMinimalComplete.Controls.Add(this.radioComplete);
            this.flowLayoutPanelMinimalComplete.Name = "flowLayoutPanelMinimalComplete";
            // 
            // radioMinimal
            // 
            resources.ApplyResources(this.radioMinimal, "radioMinimal");
            this.radioMinimal.Name = "radioMinimal";
            this.radioMinimal.TabStop = true;
            this.radioMinimal.UseVisualStyleBackColor = true;
            // 
            // radioComplete
            // 
            resources.ApplyResources(this.radioComplete, "radioComplete");
            this.radioComplete.Name = "radioComplete";
            this.radioComplete.TabStop = true;
            this.radioComplete.UseVisualStyleBackColor = true;
            // 
            // lblShareTypeFooter
            // 
            resources.ApplyResources(this.lblShareTypeFooter, "lblShareTypeFooter");
            this.lblShareTypeFooter.Name = "lblShareTypeFooter";
            // 
            // lblLibraries
            // 
            resources.ApplyResources(this.lblLibraries, "lblLibraries");
            this.lblLibraries.Name = "lblLibraries";
            // 
            // lblRetentionTimeCalculator
            // 
            resources.ApplyResources(this.lblRetentionTimeCalculator, "lblRetentionTimeCalculator");
            this.lblRetentionTimeCalculator.Name = "lblRetentionTimeCalculator";
            // 
            // lblBackgroundProteome
            // 
            resources.ApplyResources(this.lblBackgroundProteome, "lblBackgroundProteome");
            this.lblBackgroundProteome.Name = "lblBackgroundProteome";
            // 
            // lblShareTypeHeader
            // 
            resources.ApplyResources(this.lblShareTypeHeader, "lblShareTypeHeader");
            this.lblShareTypeHeader.Name = "lblShareTypeHeader";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // panel2
            // 
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Controls.Add(this.groupBoxShareType);
            this.panel2.Controls.Add(this.label3);
            this.panel2.Name = "panel2";
            // 
            // panelFileFormat
            // 
            resources.ApplyResources(this.panelFileFormat, "panelFileFormat");
            this.panelFileFormat.Controls.Add(this.groupBoxFileFormat);
            this.panelFileFormat.Name = "panelFileFormat";
            // 
            // ShareTypeDlg
            // 
            this.AcceptButton = this.btnShare;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.panelFileFormat);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panelButtonBar);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShareTypeDlg";
            this.ShowInTaskbar = false;
            this.panelButtonBar.ResumeLayout(false);
            this.groupBoxFileFormat.ResumeLayout(false);
            this.groupBoxFileFormat.PerformLayout();
            this.groupBoxShareType.ResumeLayout(false);
            this.groupBoxShareType.PerformLayout();
            this.flowLayoutPanelMinimalComplete.ResumeLayout(false);
            this.flowLayoutPanelMinimalComplete.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panelFileFormat.ResumeLayout(false);
            this.panelFileFormat.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.FlowLayoutPanel panelButtonBar;
        private System.Windows.Forms.GroupBox groupBoxFileFormat;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnShare;
        private System.Windows.Forms.GroupBox groupBoxShareType;
        private System.Windows.Forms.RadioButton radioComplete;
        private System.Windows.Forms.RadioButton radioMinimal;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label lblLibraries;
        private System.Windows.Forms.Label lblRetentionTimeCalculator;
        private System.Windows.Forms.Label lblBackgroundProteome;
        private System.Windows.Forms.Label lblShareTypeHeader;
        private System.Windows.Forms.ComboBox comboSkylineVersion;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelMinimalComplete;
        private System.Windows.Forms.Label lblShareTypeFooter;
        private System.Windows.Forms.Panel panelFileFormat;
    }
}