namespace pwiz.Skyline.Alerts
{
    partial class MessageDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MessageDlg));
            this.btnOk = new System.Windows.Forms.Button();
            this.labelMessage = new System.Windows.Forms.Label();
            this.panelMessageLabel = new System.Windows.Forms.Panel();
            this.buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnMoreInfo = new System.Windows.Forms.Button();
            this.panelMessageBox = new System.Windows.Forms.Panel();
            this.tbxDetail = new System.Windows.Forms.TextBox();
            this.panelMessageLabel.SuspendLayout();
            this.buttonPanel.SuspendLayout();
            this.panelMessageBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // labelMessage
            // 
            resources.ApplyResources(this.labelMessage, "labelMessage");
            this.labelMessage.Name = "labelMessage";
            // 
            // panelMessageLabel
            // 
            this.panelMessageLabel.BackColor = System.Drawing.SystemColors.Window;
            this.panelMessageLabel.Controls.Add(this.labelMessage);
            resources.ApplyResources(this.panelMessageLabel, "panelMessageLabel");
            this.panelMessageLabel.Name = "panelMessageLabel";
            // 
            // buttonPanel
            // 
            this.buttonPanel.Controls.Add(this.btnMoreInfo);
            this.buttonPanel.Controls.Add(this.btnOk);
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
            // panelMessageBox
            // 
            this.panelMessageBox.Controls.Add(this.panelMessageLabel);
            this.panelMessageBox.Controls.Add(this.buttonPanel);
            resources.ApplyResources(this.panelMessageBox, "panelMessageBox");
            this.panelMessageBox.Name = "panelMessageBox";
            // 
            // tbxDetail
            // 
            resources.ApplyResources(this.tbxDetail, "tbxDetail");
            this.tbxDetail.Name = "tbxDetail";
            this.tbxDetail.ReadOnly = true;
            // 
            // MessageDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.Controls.Add(this.panelMessageBox);
            this.Controls.Add(this.tbxDetail);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MessageDlg";
            this.ShowInTaskbar = false;
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MessageDlg_KeyDown);
            this.panelMessageLabel.ResumeLayout(false);
            this.panelMessageLabel.PerformLayout();
            this.buttonPanel.ResumeLayout(false);
            this.panelMessageBox.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label labelMessage;
        private System.Windows.Forms.Panel panelMessageLabel;
        private System.Windows.Forms.FlowLayoutPanel buttonPanel;
        private System.Windows.Forms.Button btnMoreInfo;
        private System.Windows.Forms.Panel panelMessageBox;
        private System.Windows.Forms.TextBox tbxDetail;
    }
}