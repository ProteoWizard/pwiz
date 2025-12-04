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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditRemoteAccountDlg));
            this.lblServerUrl = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.textServerURL = new System.Windows.Forms.TextBox();
            this.textPassword = new System.Windows.Forms.TextBox();
            this.lblPassword = new System.Windows.Forms.Label();
            this.lblUsername = new System.Windows.Forms.Label();
            this.textUsername = new System.Windows.Forms.TextBox();
            this.btnTest = new System.Windows.Forms.Button();
            this.comboAccountType = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBoxUnifi = new System.Windows.Forms.GroupBox();
            this.tbxClientId = new System.Windows.Forms.TextBox();
            this.lblClientId = new System.Windows.Forms.Label();
            this.tbxClientSecret = new System.Windows.Forms.TextBox();
            this.lblClientSecret = new System.Windows.Forms.Label();
            this.tbxClientScope = new System.Windows.Forms.TextBox();
            this.lblClientScope = new System.Windows.Forms.Label();
            this.tbxIdentityServer = new System.Windows.Forms.TextBox();
            this.lblIdentityServer = new System.Windows.Forms.Label();
            this.wizardPagesByAccountType = new pwiz.Skyline.Controls.WizardPages();
            this.tabUnifiSettings = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.cbArdiaDeleteRawAfterImport = new System.Windows.Forms.CheckBox();
            this.btnLogoutArdia = new System.Windows.Forms.Button();
            this.textArdiaAlias_Username = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textArdiaServerURL = new System.Windows.Forms.TextBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.lblAlias = new System.Windows.Forms.Label();
            this.textAlias = new System.Windows.Forms.TextBox();
            this.groupBoxUnifi.SuspendLayout();
            this.wizardPagesByAccountType.SuspendLayout();
            this.tabUnifiSettings.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblServerUrl
            // 
            resources.ApplyResources(this.lblServerUrl, "lblServerUrl");
            this.lblServerUrl.Name = "lblServerUrl";
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
            // textServerURL
            // 
            resources.ApplyResources(this.textServerURL, "textServerURL");
            this.textServerURL.Name = "textServerURL";
            this.textServerURL.TextChanged += new System.EventHandler(this.text_TextChanged);
            // 
            // textPassword
            // 
            resources.ApplyResources(this.textPassword, "textPassword");
            this.textPassword.Name = "textPassword";
            this.textPassword.UseSystemPasswordChar = true;
            this.textPassword.TextChanged += new System.EventHandler(this.text_TextChanged);
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
            this.textUsername.TextChanged += new System.EventHandler(this.text_TextChanged);

            // 
            // textAlias
            // 
            resources.ApplyResources(this.textAlias, "textAlias");
            this.textAlias.Name = "textAlias";
            this.toolTip1.SetToolTip(this.textAlias, resources.GetString("btnLogoutArdia.ToolTip"));

            // 
            // lblAlias
            // 
            resources.ApplyResources(this.lblAlias, "lblAlias");
            this.lblAlias.Name = "lblAlias";

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
            this.groupBoxUnifi.Controls.Add(this.tbxClientId);
            this.groupBoxUnifi.Controls.Add(this.lblClientId);
            this.groupBoxUnifi.Controls.Add(this.tbxClientSecret);
            this.groupBoxUnifi.Controls.Add(this.lblClientSecret);
            this.groupBoxUnifi.Controls.Add(this.tbxClientScope);
            this.groupBoxUnifi.Controls.Add(this.lblClientScope);
            this.groupBoxUnifi.Controls.Add(this.tbxIdentityServer);
            this.groupBoxUnifi.Controls.Add(this.lblIdentityServer);
            this.groupBoxUnifi.Name = "groupBoxUnifi";
            this.groupBoxUnifi.TabStop = false;
            // 
            // tbxClientId
            // 
            resources.ApplyResources(this.tbxClientId, "tbxClientId");
            this.tbxClientId.Name = "tbxClientId";
            this.tbxClientId.TextChanged += new System.EventHandler(this.text_TextChanged);
            // 
            // lblClientId
            // 
            resources.ApplyResources(this.lblClientId, "lblClientId");
            this.lblClientId.Name = "lblClientId";
            // 
            // tbxClientSecret
            // 
            resources.ApplyResources(this.tbxClientSecret, "tbxClientSecret");
            this.tbxClientSecret.Name = "tbxClientSecret";
            this.tbxClientSecret.UseSystemPasswordChar = true;
            this.tbxClientSecret.TextChanged += new System.EventHandler(this.text_TextChanged);
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
            this.tbxClientScope.TextChanged += new System.EventHandler(this.text_TextChanged);
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
            this.tbxIdentityServer.TextChanged += new System.EventHandler(this.text_TextChanged);
            // 
            // lblIdentityServer
            // 
            resources.ApplyResources(this.lblIdentityServer, "lblIdentityServer");
            this.lblIdentityServer.Name = "lblIdentityServer";
            // 
            // wizardPagesByAccountType
            // 
            resources.ApplyResources(this.wizardPagesByAccountType, "wizardPagesByAccountType");
            this.wizardPagesByAccountType.Controls.Add(this.tabUnifiSettings);
            this.wizardPagesByAccountType.Controls.Add(this.tabPage2);
            this.wizardPagesByAccountType.Multiline = true;
            this.wizardPagesByAccountType.Name = "wizardPagesByAccountType";
            this.wizardPagesByAccountType.SelectedIndex = 0;
            this.wizardPagesByAccountType.TabStop = false;
            // 
            // tabUnifiSettings
            // 
            this.tabUnifiSettings.BackColor = System.Drawing.SystemColors.Control;
            this.tabUnifiSettings.Controls.Add(this.groupBoxUnifi);
            this.tabUnifiSettings.Controls.Add(this.textUsername);
            this.tabUnifiSettings.Controls.Add(this.lblUsername);
            this.tabUnifiSettings.Controls.Add(this.lblPassword);
            this.tabUnifiSettings.Controls.Add(this.textPassword);
            this.tabUnifiSettings.Controls.Add(this.lblServerUrl);
            this.tabUnifiSettings.Controls.Add(this.textServerURL);
            resources.ApplyResources(this.tabUnifiSettings, "tabUnifiSettings");
            this.tabUnifiSettings.Name = "tabUnifiSettings";
            // 
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage2.Controls.Add(this.cbArdiaDeleteRawAfterImport);
            this.tabPage2.Controls.Add(this.btnLogoutArdia);
            this.tabPage2.Controls.Add(this.textArdiaAlias_Username);
            this.tabPage2.Controls.Add(this.label2);
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Controls.Add(this.textArdiaServerURL);
            resources.ApplyResources(this.tabPage2, "tabPage2");
            this.tabPage2.Name = "tabPage2";
            // 
            // cbArdiaDeleteRawAfterImport
            // 
            resources.ApplyResources(this.cbArdiaDeleteRawAfterImport, "cbArdiaDeleteRawAfterImport");
            this.cbArdiaDeleteRawAfterImport.Name = "cbArdiaDeleteRawAfterImport";
            this.cbArdiaDeleteRawAfterImport.UseVisualStyleBackColor = true;
            // 
            // btnLogoutArdia
            // 
            resources.ApplyResources(this.btnLogoutArdia, "btnLogoutArdia");
            this.btnLogoutArdia.Name = "btnLogoutArdia";
            this.toolTip1.SetToolTip(this.btnLogoutArdia, resources.GetString("btnLogoutArdia.ToolTip"));
            this.btnLogoutArdia.UseVisualStyleBackColor = true;
            this.btnLogoutArdia.Click += new System.EventHandler(this.btnLogoutArdia_Click);
            // 
            // textArdiaAlias_Username
            // 
            resources.ApplyResources(this.textArdiaAlias_Username, "textArdiaAlias_Username");
            this.textArdiaAlias_Username.Name = "textArdiaAlias_Username";
            this.textArdiaAlias_Username.TextChanged += new System.EventHandler(this.text_TextChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textArdiaServerURL
            // 
            resources.ApplyResources(this.textArdiaServerURL, "textArdiaServerURL");
            this.textArdiaServerURL.Name = "textArdiaServerURL";
            this.textArdiaServerURL.TextChanged += new System.EventHandler(this.text_TextChanged);
            // 
            // EditRemoteAccountDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnTest);
            this.Controls.Add(this.wizardPagesByAccountType);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboAccountType);
            this.Controls.Add(this.textAlias);
            this.Controls.Add(this.lblAlias);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditRemoteAccountDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.groupBoxUnifi.ResumeLayout(false);
            this.groupBoxUnifi.PerformLayout();
            this.wizardPagesByAccountType.ResumeLayout(false);
            this.tabUnifiSettings.ResumeLayout(false);
            this.tabUnifiSettings.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblServerUrl;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.TextBox textServerURL;
        internal System.Windows.Forms.TextBox textPassword;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.Label lblUsername;
        internal System.Windows.Forms.TextBox textUsername;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.ComboBox comboAccountType;
        private System.Windows.Forms.Label label1;

        internal System.Windows.Forms.TextBox textAlias;
        private System.Windows.Forms.Label lblAlias;

        private System.Windows.Forms.GroupBox groupBoxUnifi;
        private System.Windows.Forms.Label lblIdentityServer;
        private System.Windows.Forms.TextBox tbxClientSecret;
        private System.Windows.Forms.Label lblClientSecret;
        private System.Windows.Forms.TextBox tbxClientScope;
        private System.Windows.Forms.Label lblClientScope;
        private System.Windows.Forms.TextBox tbxIdentityServer;
        private Controls.WizardPages wizardPagesByAccountType;
        private System.Windows.Forms.TabPage tabUnifiSettings;
        private System.Windows.Forms.TabPage tabPage2;
        internal System.Windows.Forms.TextBox textArdiaAlias_Username;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textArdiaServerURL;
        private System.Windows.Forms.Button btnLogoutArdia;
        private System.Windows.Forms.CheckBox cbArdiaDeleteRawAfterImport;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.TextBox tbxClientId;
        private System.Windows.Forms.Label lblClientId;
    }
}