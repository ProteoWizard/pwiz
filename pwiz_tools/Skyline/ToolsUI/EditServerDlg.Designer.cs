namespace pwiz.Skyline.ToolsUI
{
    partial class EditServerDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditServerDlg));
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.instructionLabel = new System.Windows.Forms.Label();
            this.InputPanel = new System.Windows.Forms.Panel();
            this.lblProjectInfo = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.textServerURL = new System.Windows.Forms.TextBox();
            this.textPassword = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.lblUsername = new System.Windows.Forms.Label();
            this.textUsername = new System.Windows.Forms.TextBox();
            this.InstructionPanel = new System.Windows.Forms.Panel();
            this.ComponentOrganizer = new System.Windows.Forms.TableLayoutPanel();
            this.InputPanel.SuspendLayout();
            this.InstructionPanel.SuspendLayout();
            this.ComponentOrganizer.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // instructionLabel
            // 
            resources.ApplyResources(this.instructionLabel, "instructionLabel");
            this.instructionLabel.Name = "instructionLabel";
            // 
            // InputPanel
            // 
            resources.ApplyResources(this.InputPanel, "InputPanel");
            this.InputPanel.Controls.Add(this.lblProjectInfo);
            this.InputPanel.Controls.Add(this.btnCancel);
            this.InputPanel.Controls.Add(this.btnOK);
            this.InputPanel.Controls.Add(this.label2);
            this.InputPanel.Controls.Add(this.textServerURL);
            this.InputPanel.Controls.Add(this.textPassword);
            this.InputPanel.Controls.Add(this.label1);
            this.InputPanel.Controls.Add(this.lblPassword);
            this.InputPanel.Controls.Add(this.lblUsername);
            this.InputPanel.Controls.Add(this.textUsername);
            this.InputPanel.Name = "InputPanel";
            // 
            // lblProjectInfo
            // 
            resources.ApplyResources(this.lblProjectInfo, "lblProjectInfo");
            this.lblProjectInfo.Name = "lblProjectInfo";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textServerURL
            // 
            resources.ApplyResources(this.textServerURL, "textServerURL");
            this.textServerURL.Name = "textServerURL";
            // 
            // textPassword
            // 
            resources.ApplyResources(this.textPassword, "textPassword");
            this.textPassword.Name = "textPassword";
            this.textPassword.UseSystemPasswordChar = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // lblPassword
            // 
            resources.ApplyResources(this.lblPassword, "lblPassword");
            this.lblPassword.Name = "lblPassword";
            // 
            // lblUsername
            // 
            resources.ApplyResources(this.lblUsername, "lblUsername");
            this.lblUsername.Name = "lblUsername";
            // 
            // textUsername
            // 
            resources.ApplyResources(this.textUsername, "textUsername");
            this.textUsername.Name = "textUsername";
            // 
            // InstructionPanel
            // 
            resources.ApplyResources(this.InstructionPanel, "InstructionPanel");
            this.InstructionPanel.Controls.Add(this.instructionLabel);
            this.InstructionPanel.Name = "InstructionPanel";
            // 
            // ComponentOrganizer
            // 
            resources.ApplyResources(this.ComponentOrganizer, "ComponentOrganizer");
            this.ComponentOrganizer.Controls.Add(this.InstructionPanel, 0, 0);
            this.ComponentOrganizer.Controls.Add(this.InputPanel, 0, 1);
            this.ComponentOrganizer.Name = "ComponentOrganizer";
            // 
            // EditServerDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.ComponentOrganizer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditServerDlg";
            this.ShowInTaskbar = false;
            this.InputPanel.ResumeLayout(false);
            this.InputPanel.PerformLayout();
            this.InstructionPanel.ResumeLayout(false);
            this.InstructionPanel.PerformLayout();
            this.ComponentOrganizer.ResumeLayout(false);
            this.ComponentOrganizer.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label lblProjectInfo;
        private System.Windows.Forms.TextBox textServerURL;
        internal System.Windows.Forms.TextBox textPassword;
        internal System.Windows.Forms.TextBox textUsername;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel InputPanel;
        private System.Windows.Forms.Label instructionLabel;
        private System.Windows.Forms.Panel InstructionPanel;
        private System.Windows.Forms.TableLayoutPanel ComponentOrganizer;
    }
}