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
            this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)(this.webView)).BeginInit();
            this.SuspendLayout();
            // 
            // webView
            // 
            this.webView.AllowExternalDrop = true;
            this.webView.CreationProperties = null;
            this.webView.DefaultBackgroundColor = System.Drawing.Color.White;
            resources.ApplyResources(this.webView, "webView");
            this.webView.Name = "webView";
            this.webView.ZoomFactor = 1D;
            // 
            // ArdiaLoginDlg
            // 
            resources.ApplyResources(this, "$this");
            this.Controls.Add(this.webView);
            this.Name = "ArdiaLoginDlg";
            this.ShowIcon = false;
            ((System.ComponentModel.ISupportInitialize)(this.webView)).EndInit();
            this.ResumeLayout(false);

        }
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;
        #endregion
    }
}