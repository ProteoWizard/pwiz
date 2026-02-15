namespace pwiz.Skyline.Alerts
{
    partial class ArdiaLoginDlg
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
            _tempUserDataFolder.Dispose();
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ArdiaLoginDlg));
            this.label1 = new System.Windows.Forms.Label();
            this.wizardPagesMain = new pwiz.Skyline.Controls.WizardPages();
            this.tabTestWebview = new System.Windows.Forms.TabPage();
            this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.tabMainLoading = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.tabMainRegisterSkyline = new System.Windows.Forms.TabPage();
            this.wizardPagesRegisterPhases = new pwiz.Skyline.Controls.WizardPages();
            this.tabRegisterButton = new System.Windows.Forms.TabPage();
            this.btnRegisterSkyline = new System.Windows.Forms.Button();
            this.tabRegisterInProgress = new System.Windows.Forms.TabPage();
            this.label12 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.pnlRegisterFailedUserNotAuth = new System.Windows.Forms.Panel();
            this.btnRegisterSkyline_InErrorBlock = new System.Windows.Forms.Button();
            this.btnViewAccount = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.tabMainRegisterComplete = new System.Windows.Forms.TabPage();
            this.label16 = new System.Windows.Forms.Label();
            this.btnShowLoginTab = new System.Windows.Forms.Button();
            this.label15 = new System.Windows.Forms.Label();
            this.btnViewAccount_2 = new System.Windows.Forms.Button();
            this.label14 = new System.Windows.Forms.Label();
            this.tabMainLogin = new System.Windows.Forms.TabPage();
            this.wizardPagesLoginPhases = new pwiz.Skyline.Controls.WizardPages();
            this.tabLoginButton = new System.Windows.Forms.TabPage();
            this.btnLogin = new System.Windows.Forms.Button();
            this.tabLoginInProgress = new System.Windows.Forms.TabPage();
            this.label13 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.wizardPagesMain.SuspendLayout();
            this.tabTestWebview.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webView)).BeginInit();
            this.tabMainLoading.SuspendLayout();
            this.tabMainRegisterSkyline.SuspendLayout();
            this.wizardPagesRegisterPhases.SuspendLayout();
            this.tabRegisterButton.SuspendLayout();
            this.tabRegisterInProgress.SuspendLayout();
            this.pnlRegisterFailedUserNotAuth.SuspendLayout();
            this.tabMainRegisterComplete.SuspendLayout();
            this.tabMainLogin.SuspendLayout();
            this.wizardPagesLoginPhases.SuspendLayout();
            this.tabLoginButton.SuspendLayout();
            this.tabLoginInProgress.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // wizardPagesMain
            // 
            resources.ApplyResources(this.wizardPagesMain, "wizardPagesMain");
            this.wizardPagesMain.Controls.Add(this.tabTestWebview);
            this.wizardPagesMain.Controls.Add(this.tabMainLoading);
            this.wizardPagesMain.Controls.Add(this.tabMainRegisterSkyline);
            this.wizardPagesMain.Controls.Add(this.tabMainRegisterComplete);
            this.wizardPagesMain.Controls.Add(this.tabMainLogin);
            this.wizardPagesMain.Name = "wizardPagesMain";
            this.wizardPagesMain.SelectedIndex = 0;
            // 
            // tabTestWebview
            // 
            this.tabTestWebview.Controls.Add(this.webView);
            this.tabTestWebview.Controls.Add(this.label1);
            resources.ApplyResources(this.tabTestWebview, "tabTestWebview");
            this.tabTestWebview.Name = "tabTestWebview";
            this.tabTestWebview.UseVisualStyleBackColor = true;
            // 
            // webView
            // 
            this.webView.AllowExternalDrop = true;
            resources.ApplyResources(this.webView, "webView");
            this.webView.CreationProperties = null;
            this.webView.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView.Name = "webView";
            this.webView.ZoomFactor = 1D;
            // 
            // tabMainLoading
            // 
            this.tabMainLoading.Controls.Add(this.label2);
            resources.ApplyResources(this.tabMainLoading, "tabMainLoading");
            this.tabMainLoading.Name = "tabMainLoading";
            this.tabMainLoading.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // tabMainRegisterSkyline
            // 
            this.tabMainRegisterSkyline.Controls.Add(this.wizardPagesRegisterPhases);
            this.tabMainRegisterSkyline.Controls.Add(this.pnlRegisterFailedUserNotAuth);
            this.tabMainRegisterSkyline.Controls.Add(this.label4);
            this.tabMainRegisterSkyline.Controls.Add(this.label3);
            resources.ApplyResources(this.tabMainRegisterSkyline, "tabMainRegisterSkyline");
            this.tabMainRegisterSkyline.Name = "tabMainRegisterSkyline";
            this.tabMainRegisterSkyline.UseVisualStyleBackColor = true;
            // 
            // wizardPagesRegisterPhases
            // 
            this.wizardPagesRegisterPhases.Controls.Add(this.tabRegisterButton);
            this.wizardPagesRegisterPhases.Controls.Add(this.tabRegisterInProgress);
            resources.ApplyResources(this.wizardPagesRegisterPhases, "wizardPagesRegisterPhases");
            this.wizardPagesRegisterPhases.Name = "wizardPagesRegisterPhases";
            this.wizardPagesRegisterPhases.SelectedIndex = 0;
            // 
            // tabRegisterButton
            // 
            this.tabRegisterButton.Controls.Add(this.btnRegisterSkyline);
            resources.ApplyResources(this.tabRegisterButton, "tabRegisterButton");
            this.tabRegisterButton.Name = "tabRegisterButton";
            this.tabRegisterButton.UseVisualStyleBackColor = true;
            // 
            // btnRegisterSkyline
            // 
            resources.ApplyResources(this.btnRegisterSkyline, "btnRegisterSkyline");
            this.btnRegisterSkyline.Name = "btnRegisterSkyline";
            this.btnRegisterSkyline.UseVisualStyleBackColor = true;
            this.btnRegisterSkyline.Click += new System.EventHandler(this.btnRegisterSkyline_Click);
            // 
            // tabRegisterInProgress
            // 
            this.tabRegisterInProgress.Controls.Add(this.label12);
            this.tabRegisterInProgress.Controls.Add(this.label10);
            resources.ApplyResources(this.tabRegisterInProgress, "tabRegisterInProgress");
            this.tabRegisterInProgress.Name = "tabRegisterInProgress";
            this.tabRegisterInProgress.UseVisualStyleBackColor = true;
            // 
            // label12
            // 
            resources.ApplyResources(this.label12, "label12");
            this.label12.Name = "label12";
            // 
            // label10
            // 
            resources.ApplyResources(this.label10, "label10");
            this.label10.Name = "label10";
            // 
            // pnlRegisterFailedUserNotAuth
            // 
            this.pnlRegisterFailedUserNotAuth.Controls.Add(this.btnRegisterSkyline_InErrorBlock);
            this.pnlRegisterFailedUserNotAuth.Controls.Add(this.btnViewAccount);
            this.pnlRegisterFailedUserNotAuth.Controls.Add(this.label8);
            this.pnlRegisterFailedUserNotAuth.Controls.Add(this.label7);
            this.pnlRegisterFailedUserNotAuth.Controls.Add(this.label6);
            this.pnlRegisterFailedUserNotAuth.Controls.Add(this.label5);
            resources.ApplyResources(this.pnlRegisterFailedUserNotAuth, "pnlRegisterFailedUserNotAuth");
            this.pnlRegisterFailedUserNotAuth.Name = "pnlRegisterFailedUserNotAuth";
            // 
            // btnRegisterSkyline_InErrorBlock
            // 
            resources.ApplyResources(this.btnRegisterSkyline_InErrorBlock, "btnRegisterSkyline_InErrorBlock");
            this.btnRegisterSkyline_InErrorBlock.Name = "btnRegisterSkyline_InErrorBlock";
            this.btnRegisterSkyline_InErrorBlock.UseVisualStyleBackColor = true;
            this.btnRegisterSkyline_InErrorBlock.Click += new System.EventHandler(this.btnRegisterSkyline_InErrorBlock_Click);
            // 
            // btnViewAccount
            // 
            resources.ApplyResources(this.btnViewAccount, "btnViewAccount");
            this.btnViewAccount.Name = "btnViewAccount";
            this.btnViewAccount.UseVisualStyleBackColor = true;
            this.btnViewAccount.Click += new System.EventHandler(this.btnViewAccount_Click);
            // 
            // label8
            // 
            resources.ApplyResources(this.label8, "label8");
            this.label8.Name = "label8";
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // tabMainRegisterComplete
            // 
            this.tabMainRegisterComplete.Controls.Add(this.label16);
            this.tabMainRegisterComplete.Controls.Add(this.btnShowLoginTab);
            this.tabMainRegisterComplete.Controls.Add(this.label15);
            this.tabMainRegisterComplete.Controls.Add(this.btnViewAccount_2);
            this.tabMainRegisterComplete.Controls.Add(this.label14);
            resources.ApplyResources(this.tabMainRegisterComplete, "tabMainRegisterComplete");
            this.tabMainRegisterComplete.Name = "tabMainRegisterComplete";
            this.tabMainRegisterComplete.UseVisualStyleBackColor = true;
            // 
            // label16
            // 
            resources.ApplyResources(this.label16, "label16");
            this.label16.Name = "label16";
            // 
            // btnShowLoginTab
            // 
            resources.ApplyResources(this.btnShowLoginTab, "btnShowLoginTab");
            this.btnShowLoginTab.Name = "btnShowLoginTab";
            this.btnShowLoginTab.UseVisualStyleBackColor = true;
            this.btnShowLoginTab.Click += new System.EventHandler(this.btnShowLoginTab_Click);
            // 
            // label15
            // 
            resources.ApplyResources(this.label15, "label15");
            this.label15.Name = "label15";
            // 
            // btnViewAccount_2
            // 
            resources.ApplyResources(this.btnViewAccount_2, "btnViewAccount_2");
            this.btnViewAccount_2.Name = "btnViewAccount_2";
            this.btnViewAccount_2.UseVisualStyleBackColor = true;
            this.btnViewAccount_2.Click += new System.EventHandler(this.btnViewAccount_2_Click);
            // 
            // label14
            // 
            resources.ApplyResources(this.label14, "label14");
            this.label14.Name = "label14";
            // 
            // tabMainLogin
            // 
            this.tabMainLogin.Controls.Add(this.wizardPagesLoginPhases);
            this.tabMainLogin.Controls.Add(this.label11);
            resources.ApplyResources(this.tabMainLogin, "tabMainLogin");
            this.tabMainLogin.Name = "tabMainLogin";
            this.tabMainLogin.UseVisualStyleBackColor = true;
            // 
            // wizardPagesLoginPhases
            // 
            this.wizardPagesLoginPhases.Controls.Add(this.tabLoginButton);
            this.wizardPagesLoginPhases.Controls.Add(this.tabLoginInProgress);
            resources.ApplyResources(this.wizardPagesLoginPhases, "wizardPagesLoginPhases");
            this.wizardPagesLoginPhases.Name = "wizardPagesLoginPhases";
            this.wizardPagesLoginPhases.SelectedIndex = 0;
            // 
            // tabLoginButton
            // 
            this.tabLoginButton.Controls.Add(this.btnLogin);
            resources.ApplyResources(this.tabLoginButton, "tabLoginButton");
            this.tabLoginButton.Name = "tabLoginButton";
            this.tabLoginButton.UseVisualStyleBackColor = true;
            // 
            // btnLogin
            // 
            resources.ApplyResources(this.btnLogin, "btnLogin");
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);
            // 
            // tabLoginInProgress
            // 
            this.tabLoginInProgress.Controls.Add(this.label13);
            this.tabLoginInProgress.Controls.Add(this.label9);
            resources.ApplyResources(this.tabLoginInProgress, "tabLoginInProgress");
            this.tabLoginInProgress.Name = "tabLoginInProgress";
            this.tabLoginInProgress.UseVisualStyleBackColor = true;
            // 
            // label13
            // 
            resources.ApplyResources(this.label13, "label13");
            this.label13.Name = "label13";
            // 
            // label9
            // 
            resources.ApplyResources(this.label9, "label9");
            this.label9.Name = "label9";
            // 
            // label11
            // 
            resources.ApplyResources(this.label11, "label11");
            this.label11.Name = "label11";
            // 
            // ArdiaLoginDlg
            // 
            resources.ApplyResources(this, "$this");
            this.Controls.Add(this.wizardPagesMain);
            this.Name = "ArdiaLoginDlg";
            this.ShowIcon = false;
            this.wizardPagesMain.ResumeLayout(false);
            this.tabTestWebview.ResumeLayout(false);
            this.tabTestWebview.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webView)).EndInit();
            this.tabMainLoading.ResumeLayout(false);
            this.tabMainLoading.PerformLayout();
            this.tabMainRegisterSkyline.ResumeLayout(false);
            this.tabMainRegisterSkyline.PerformLayout();
            this.wizardPagesRegisterPhases.ResumeLayout(false);
            this.tabRegisterButton.ResumeLayout(false);
            this.tabRegisterInProgress.ResumeLayout(false);
            this.tabRegisterInProgress.PerformLayout();
            this.pnlRegisterFailedUserNotAuth.ResumeLayout(false);
            this.pnlRegisterFailedUserNotAuth.PerformLayout();
            this.tabMainRegisterComplete.ResumeLayout(false);
            this.tabMainRegisterComplete.PerformLayout();
            this.tabMainLogin.ResumeLayout(false);
            this.wizardPagesLoginPhases.ResumeLayout(false);
            this.tabLoginButton.ResumeLayout(false);
            this.tabLoginInProgress.ResumeLayout(false);
            this.tabLoginInProgress.PerformLayout();
            this.ResumeLayout(false);

        }
        #endregion

        private System.Windows.Forms.Label label1;
        private Controls.WizardPages wizardPagesMain;
        private System.Windows.Forms.TabPage tabTestWebview;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;
        private System.Windows.Forms.TabPage tabMainLoading;
        private System.Windows.Forms.TabPage tabMainRegisterSkyline;
        private System.Windows.Forms.TabPage tabMainLogin;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnRegisterSkyline;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Panel pnlRegisterFailedUserNotAuth;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button btnViewAccount;
        private System.Windows.Forms.Label label11;
        private Controls.WizardPages wizardPagesRegisterPhases;
        private System.Windows.Forms.TabPage tabRegisterButton;
        private System.Windows.Forms.TabPage tabRegisterInProgress;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Button btnRegisterSkyline_InErrorBlock;
        private Controls.WizardPages wizardPagesLoginPhases;
        private System.Windows.Forms.TabPage tabLoginButton;
        private System.Windows.Forms.TabPage tabLoginInProgress;
        private System.Windows.Forms.TabPage tabMainRegisterComplete;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Button btnShowLoginTab;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Button btnViewAccount_2;
        private System.Windows.Forms.Label label14;
    }
}