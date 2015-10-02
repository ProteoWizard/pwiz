namespace pwiz.Skyline.Alerts
{
    partial class AlertDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AlertDlg));
            this.buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnMoreInfo = new System.Windows.Forms.Button();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.messageScrollPanel = new System.Windows.Forms.Panel();
            this.labelMessage = new System.Windows.Forms.Label();
            this.tbxDetail = new System.Windows.Forms.TextBox();
            this.buttonPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.messageScrollPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonPanel
            // 
            this.buttonPanel.BackColor = System.Drawing.SystemColors.Control;
            this.buttonPanel.Controls.Add(this.btnMoreInfo);
            resources.ApplyResources(this.buttonPanel, "buttonPanel");
            this.buttonPanel.Name = "buttonPanel";
            // 
            // btnMoreInfo
            // 
            resources.ApplyResources(this.btnMoreInfo, "btnMoreInfo");
            this.btnMoreInfo.Name = "btnMoreInfo";
            this.btnMoreInfo.UseVisualStyleBackColor = true;
            this.btnMoreInfo.Click += new System.EventHandler(this.btnMoreInfo_Click);
            // 
            // splitContainer
            // 
            resources.ApplyResources(this.splitContainer, "splitContainer");
            this.splitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.BackColor = System.Drawing.SystemColors.Window;
            this.splitContainer.Panel1.Controls.Add(this.messageScrollPanel);
            this.splitContainer.Panel1.Controls.Add(this.buttonPanel);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.tbxDetail);
            this.splitContainer.Panel2Collapsed = true;
            // 
            // messageScrollPanel
            // 
            resources.ApplyResources(this.messageScrollPanel, "messageScrollPanel");
            this.messageScrollPanel.Controls.Add(this.labelMessage);
            this.messageScrollPanel.Name = "messageScrollPanel";
            this.messageScrollPanel.Resize += new System.EventHandler(this.messageScrollPanel_Resize);
            // 
            // labelMessage
            // 
            resources.ApplyResources(this.labelMessage, "labelMessage");
            this.labelMessage.Name = "labelMessage";
            // 
            // tbxDetail
            // 
            resources.ApplyResources(this.tbxDetail, "tbxDetail");
            this.tbxDetail.Name = "tbxDetail";
            this.tbxDetail.ReadOnly = true;
            // 
            // AlertDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AlertDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MessageDlg_KeyDown);
            this.buttonPanel.ResumeLayout(false);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.messageScrollPanel.ResumeLayout(false);
            this.messageScrollPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel buttonPanel;
        private System.Windows.Forms.Button btnMoreInfo;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.TextBox tbxDetail;
        private System.Windows.Forms.Panel messageScrollPanel;
        private System.Windows.Forms.Label labelMessage;
    }
}