namespace pwiz.Skyline.ToolsUI
{
    partial class EditRemoteAccountDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditRemoteAccountDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnTest = new System.Windows.Forms.Button();
            this.comboAccountType = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBoxUnifi = new System.Windows.Forms.GroupBox();
            this.tbxClientSecret = new System.Windows.Forms.TextBox();
            this.lblClientSecret = new System.Windows.Forms.Label();
            this.tbxClientScope = new System.Windows.Forms.TextBox();
            this.lblClientScope = new System.Windows.Forms.Label();
            this.tbxIdentityServer = new System.Windows.Forms.TextBox();
            this.lblIdentityServer = new System.Windows.Forms.Label();
            this.pnlArdiaSettings = new System.Windows.Forms.Panel();
            this.cbDeleteRawAfterImport = new System.Windows.Forms.CheckBox();
            this.flowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlAccountTypeSelect = new System.Windows.Forms.Panel();
            this.pnlUsernameLabel = new System.Windows.Forms.Panel();
            this.lblUsername = new System.Windows.Forms.Label();
            this.pnlUsernameAliasLabel = new System.Windows.Forms.Panel();
            this.lblUsernameAlias = new System.Windows.Forms.Label();
            this.pnlUsernameInputField = new System.Windows.Forms.Panel();
            this.textUsername = new System.Windows.Forms.TextBox();
            this.pnlPassword = new System.Windows.Forms.Panel();
            this.textPassword = new System.Windows.Forms.TextBox();
            this.lblPassword = new System.Windows.Forms.Label();
            this.pnlServerURL = new System.Windows.Forms.Panel();
            this.textServerURL = new System.Windows.Forms.TextBox();
            this.lblServerUrl = new System.Windows.Forms.Label();
            this.groupBoxUnifi.SuspendLayout();
            this.pnlArdiaSettings.SuspendLayout();
            this.flowLayoutPanel.SuspendLayout();
            this.pnlAccountTypeSelect.SuspendLayout();
            this.pnlUsernameLabel.SuspendLayout();
            this.pnlUsernameAliasLabel.SuspendLayout();
            this.pnlUsernameInputField.SuspendLayout();
            this.pnlPassword.SuspendLayout();
            this.pnlServerURL.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnTest
            // 
            resources.ApplyResources(this.btnTest, "btnTest");
            this.btnTest.Name = "btnTest";
            this.btnTest.UseVisualStyleBackColor = true;
            this.btnTest.Click += new System.EventHandler(this.btnTest_Click);
            // 
            // comboAccountType
            // 
            this.comboAccountType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAccountType.DropDownWidth = 200;
            this.comboAccountType.FormattingEnabled = true;
            resources.ApplyResources(this.comboAccountType, "comboAccountType");
            this.comboAccountType.Name = "comboAccountType";
            this.comboAccountType.SelectedIndexChanged += new System.EventHandler(this.comboAccountType_SelectedIndexChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // groupBoxUnifi
            // 
            resources.ApplyResources(this.groupBoxUnifi, "groupBoxUnifi");
            this.groupBoxUnifi.Controls.Add(this.tbxClientSecret);
            this.groupBoxUnifi.Controls.Add(this.lblClientSecret);
            this.groupBoxUnifi.Controls.Add(this.tbxClientScope);
            this.groupBoxUnifi.Controls.Add(this.lblClientScope);
            this.groupBoxUnifi.Controls.Add(this.tbxIdentityServer);
            this.groupBoxUnifi.Controls.Add(this.lblIdentityServer);
            this.flowLayoutPanel.SetFlowBreak(this.groupBoxUnifi, true);
            this.groupBoxUnifi.Name = "groupBoxUnifi";
            this.groupBoxUnifi.TabStop = false;
            // 
            // tbxClientSecret
            // 
            resources.ApplyResources(this.tbxClientSecret, "tbxClientSecret");
            this.tbxClientSecret.Name = "tbxClientSecret";
            // 
            // lblClientSecret
            // 
            resources.ApplyResources(this.lblClientSecret, "lblClientSecret");
            this.lblClientSecret.Name = "lblClientSecret";
            // 
            // tbxClientScope
            // 
            resources.ApplyResources(this.tbxClientScope, "tbxClientScope");
            this.tbxClientScope.Name = "tbxClientScope";
            // 
            // lblClientScope
            // 
            resources.ApplyResources(this.lblClientScope, "lblClientScope");
            this.lblClientScope.Name = "lblClientScope";
            // 
            // tbxIdentityServer
            // 
            resources.ApplyResources(this.tbxIdentityServer, "tbxIdentityServer");
            this.tbxIdentityServer.Name = "tbxIdentityServer";
            // 
            // lblIdentityServer
            // 
            resources.ApplyResources(this.lblIdentityServer, "lblIdentityServer");
            this.lblIdentityServer.Name = "lblIdentityServer";
            // 
            // pnlArdiaSettings
            // 
            resources.ApplyResources(this.pnlArdiaSettings, "pnlArdiaSettings");
            this.pnlArdiaSettings.Controls.Add(this.cbDeleteRawAfterImport);
            this.flowLayoutPanel.SetFlowBreak(this.pnlArdiaSettings, true);
            this.pnlArdiaSettings.Name = "pnlArdiaSettings";
            // 
            // cbDeleteRawAfterImport
            // 
            resources.ApplyResources(this.cbDeleteRawAfterImport, "cbDeleteRawAfterImport");
            this.cbDeleteRawAfterImport.Name = "cbDeleteRawAfterImport";
            this.cbDeleteRawAfterImport.UseVisualStyleBackColor = true;
            // 
            // flowLayoutPanel
            // 
            resources.ApplyResources(this.flowLayoutPanel, "flowLayoutPanel");
            this.flowLayoutPanel.Controls.Add(this.pnlAccountTypeSelect);
            this.flowLayoutPanel.Controls.Add(this.pnlUsernameLabel);
            this.flowLayoutPanel.Controls.Add(this.pnlUsernameAliasLabel);
            this.flowLayoutPanel.Controls.Add(this.pnlUsernameInputField);
            this.flowLayoutPanel.Controls.Add(this.pnlPassword);
            this.flowLayoutPanel.Controls.Add(this.pnlServerURL);
            this.flowLayoutPanel.Controls.Add(this.pnlArdiaSettings);
            this.flowLayoutPanel.Controls.Add(this.groupBoxUnifi);
            this.flowLayoutPanel.Name = "flowLayoutPanel";
            this.flowLayoutPanel.Resize += new System.EventHandler(this.flowLayoutPanel_Resize);
            // 
            // pnlAccountTypeSelect
            // 
            resources.ApplyResources(this.pnlAccountTypeSelect, "pnlAccountTypeSelect");
            this.pnlAccountTypeSelect.Controls.Add(this.label1);
            this.pnlAccountTypeSelect.Controls.Add(this.comboAccountType);
            this.flowLayoutPanel.SetFlowBreak(this.pnlAccountTypeSelect, true);
            this.pnlAccountTypeSelect.Name = "pnlAccountTypeSelect";
            // 
            // pnlUsernameLabel
            // 
            this.pnlUsernameLabel.Controls.Add(this.lblUsername);
            resources.ApplyResources(this.pnlUsernameLabel, "pnlUsernameLabel");
            this.pnlUsernameLabel.Name = "pnlUsernameLabel";
            // 
            // lblUsername
            // 
            resources.ApplyResources(this.lblUsername, "lblUsername");
            this.lblUsername.Name = "lblUsername";
            // 
            // pnlUsernameAliasLabel
            // 
            this.pnlUsernameAliasLabel.Controls.Add(this.lblUsernameAlias);
            resources.ApplyResources(this.pnlUsernameAliasLabel, "pnlUsernameAliasLabel");
            this.pnlUsernameAliasLabel.Name = "pnlUsernameAliasLabel";
            // 
            // lblUsernameAlias
            // 
            resources.ApplyResources(this.lblUsernameAlias, "lblUsernameAlias");
            this.lblUsernameAlias.Name = "lblUsernameAlias";
            // 
            // pnlUsernameInputField
            // 
            this.pnlUsernameInputField.Controls.Add(this.textUsername);
            resources.ApplyResources(this.pnlUsernameInputField, "pnlUsernameInputField");
            this.pnlUsernameInputField.Name = "pnlUsernameInputField";
            // 
            // textUsername
            // 
            resources.ApplyResources(this.textUsername, "textUsername");
            this.textUsername.Name = "textUsername";
            // 
            // pnlPassword
            // 
            this.pnlPassword.Controls.Add(this.textPassword);
            this.pnlPassword.Controls.Add(this.lblPassword);
            resources.ApplyResources(this.pnlPassword, "pnlPassword");
            this.pnlPassword.Name = "pnlPassword";
            // 
            // textPassword
            // 
            resources.ApplyResources(this.textPassword, "textPassword");
            this.textPassword.Name = "textPassword";
            this.textPassword.UseSystemPasswordChar = true;
            // 
            // lblPassword
            // 
            resources.ApplyResources(this.lblPassword, "lblPassword");
            this.lblPassword.Name = "lblPassword";
            // 
            // pnlServerURL
            // 
            this.pnlServerURL.Controls.Add(this.textServerURL);
            this.pnlServerURL.Controls.Add(this.lblServerUrl);
            resources.ApplyResources(this.pnlServerURL, "pnlServerURL");
            this.pnlServerURL.Name = "pnlServerURL";
            // 
            // textServerURL
            // 
            resources.ApplyResources(this.textServerURL, "textServerURL");
            this.textServerURL.Name = "textServerURL";
            // 
            // lblServerUrl
            // 
            resources.ApplyResources(this.lblServerUrl, "lblServerUrl");
            this.lblServerUrl.Name = "lblServerUrl";
            // 
            // EditRemoteAccountDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.flowLayoutPanel);
            this.Controls.Add(this.btnTest);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditRemoteAccountDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.groupBoxUnifi.ResumeLayout(false);
            this.groupBoxUnifi.PerformLayout();
            this.pnlArdiaSettings.ResumeLayout(false);
            this.pnlArdiaSettings.PerformLayout();
            this.flowLayoutPanel.ResumeLayout(false);
            this.pnlAccountTypeSelect.ResumeLayout(false);
            this.pnlAccountTypeSelect.PerformLayout();
            this.pnlUsernameLabel.ResumeLayout(false);
            this.pnlUsernameLabel.PerformLayout();
            this.pnlUsernameAliasLabel.ResumeLayout(false);
            this.pnlUsernameAliasLabel.PerformLayout();
            this.pnlUsernameInputField.ResumeLayout(false);
            this.pnlUsernameInputField.PerformLayout();
            this.pnlPassword.ResumeLayout(false);
            this.pnlPassword.PerformLayout();
            this.pnlServerURL.ResumeLayout(false);
            this.pnlServerURL.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.ComboBox comboAccountType;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBoxUnifi;
        private System.Windows.Forms.Label lblIdentityServer;
        private System.Windows.Forms.TextBox tbxClientSecret;
        private System.Windows.Forms.Label lblClientSecret;
        private System.Windows.Forms.TextBox tbxClientScope;
        private System.Windows.Forms.Label lblClientScope;
        private System.Windows.Forms.TextBox tbxIdentityServer;
        private System.Windows.Forms.Panel pnlArdiaSettings;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel;
        private System.Windows.Forms.Panel pnlAccountTypeSelect;
        private System.Windows.Forms.CheckBox cbDeleteRawAfterImport;
        private System.Windows.Forms.Panel pnlUsernameInputField;
        internal System.Windows.Forms.TextBox textUsername;
        private System.Windows.Forms.Panel pnlServerURL;
        private System.Windows.Forms.TextBox textServerURL;
        private System.Windows.Forms.Label lblServerUrl;
        private System.Windows.Forms.Panel pnlPassword;
        internal System.Windows.Forms.TextBox textPassword;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.Panel pnlUsernameLabel;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.Panel pnlUsernameAliasLabel;
        private System.Windows.Forms.Label lblUsernameAlias;
    }
}