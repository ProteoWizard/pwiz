namespace SkylineMcpConnector
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.labelStatus = new System.Windows.Forms.Label();
            this.labelVersion = new System.Windows.Forms.Label();
            this.labelDocument = new System.Windows.Forms.Label();
            this.buttonSetup = new System.Windows.Forms.Button();
            this.buttonClose = new System.Windows.Forms.Button();
            this.checkClaudeDesktop = new System.Windows.Forms.CheckBox();
            this.checkClaudeCode = new System.Windows.Forms.CheckBox();
            this.checkGeminiCli = new System.Windows.Forms.CheckBox();
            this.checkVSCode = new System.Windows.Forms.CheckBox();
            this.checkCursor = new System.Windows.Forms.CheckBox();
            this.labelSetupStatus = new System.Windows.Forms.Label();
            this.groupBoxSetup = new System.Windows.Forms.GroupBox();
            this.groupBoxSetup.SuspendLayout();
            this.SuspendLayout();
            //
            // labelStatus
            //
            this.labelStatus.AutoSize = true;
            this.labelStatus.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelStatus.Location = new System.Drawing.Point(9, 9);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(110, 21);
            this.labelStatus.TabIndex = 0;
            this.labelStatus.Text = "Connecting...";
            //
            // labelVersion
            //
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(20, 50);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(65, 15);
            this.labelVersion.TabIndex = 1;
            this.labelVersion.Text = "Version: {0}";
            //
            // labelDocument
            //
            this.labelDocument.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelDocument.AutoEllipsis = true;
            this.labelDocument.Location = new System.Drawing.Point(20, 72);
            this.labelDocument.Name = "labelDocument";
            this.labelDocument.Size = new System.Drawing.Size(440, 15);
            this.labelDocument.TabIndex = 2;
            this.labelDocument.Text = "Document: {0}";
            //
            // buttonSetup
            //
            this.buttonSetup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSetup.Location = new System.Drawing.Point(312, 105);
            this.buttonSetup.Name = "buttonSetup";
            this.buttonSetup.Size = new System.Drawing.Size(75, 23);
            this.buttonSetup.TabIndex = 3;
            this.buttonSetup.Text = "&Setup >>";
            this.buttonSetup.Click += new System.EventHandler(this.buttonSetup_Click);
            //
            // buttonClose
            //
            this.buttonClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonClose.Location = new System.Drawing.Point(393, 105);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(75, 23);
            this.buttonClose.TabIndex = 4;
            this.buttonClose.Text = "Close";
            this.buttonClose.Click += new System.EventHandler(this.buttonClose_Click);
            //
            // checkClaudeDesktop
            //
            this.checkClaudeDesktop.AutoSize = true;
            this.checkClaudeDesktop.Location = new System.Drawing.Point(10, 24);
            this.checkClaudeDesktop.Name = "checkClaudeDesktop";
            this.checkClaudeDesktop.Size = new System.Drawing.Size(109, 19);
            this.checkClaudeDesktop.TabIndex = 0;
            this.checkClaudeDesktop.Text = "Claude &Desktop";
            this.checkClaudeDesktop.CheckedChanged += new System.EventHandler(this.checkClaudeDesktop_CheckedChanged);
            //
            // checkClaudeCode
            //
            this.checkClaudeCode.AutoSize = true;
            this.checkClaudeCode.Location = new System.Drawing.Point(10, 49);
            this.checkClaudeCode.Name = "checkClaudeCode";
            this.checkClaudeCode.Size = new System.Drawing.Size(94, 19);
            this.checkClaudeCode.TabIndex = 1;
            this.checkClaudeCode.Text = "Claude &Code";
            this.checkClaudeCode.CheckedChanged += new System.EventHandler(this.checkClaudeCode_CheckedChanged);
            //
            // checkGeminiCli
            //
            this.checkGeminiCli.AutoSize = true;
            this.checkGeminiCli.Location = new System.Drawing.Point(10, 74);
            this.checkGeminiCli.Name = "checkGeminiCli";
            this.checkGeminiCli.Size = new System.Drawing.Size(83, 19);
            this.checkGeminiCli.TabIndex = 2;
            this.checkGeminiCli.Text = "&Gemini CLI";
            this.checkGeminiCli.CheckedChanged += new System.EventHandler(this.checkGeminiCli_CheckedChanged);
            //
            // checkVSCode
            //
            this.checkVSCode.AutoSize = true;
            this.checkVSCode.Location = new System.Drawing.Point(10, 99);
            this.checkVSCode.Name = "checkVSCode";
            this.checkVSCode.Size = new System.Drawing.Size(118, 19);
            this.checkVSCode.TabIndex = 3;
            this.checkVSCode.Text = "&VS Code (Copilot)";
            this.checkVSCode.CheckedChanged += new System.EventHandler(this.checkVSCode_CheckedChanged);
            //
            // checkCursor
            //
            this.checkCursor.AutoSize = true;
            this.checkCursor.Location = new System.Drawing.Point(10, 124);
            this.checkCursor.Name = "checkCursor";
            this.checkCursor.Size = new System.Drawing.Size(62, 19);
            this.checkCursor.TabIndex = 4;
            this.checkCursor.Text = "C&ursor";
            this.checkCursor.CheckedChanged += new System.EventHandler(this.checkCursor_CheckedChanged);
            //
            // labelSetupStatus
            //
            this.labelSetupStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelSetupStatus.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelSetupStatus.Location = new System.Drawing.Point(8, 157);
            this.labelSetupStatus.Name = "labelSetupStatus";
            this.labelSetupStatus.Size = new System.Drawing.Size(440, 30);
            this.labelSetupStatus.TabIndex = 5;
            //
            // groupBoxSetup
            //
            this.groupBoxSetup.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxSetup.Controls.Add(this.checkClaudeDesktop);
            this.groupBoxSetup.Controls.Add(this.checkClaudeCode);
            this.groupBoxSetup.Controls.Add(this.checkGeminiCli);
            this.groupBoxSetup.Controls.Add(this.checkVSCode);
            this.groupBoxSetup.Controls.Add(this.checkCursor);
            this.groupBoxSetup.Controls.Add(this.labelSetupStatus);
            this.groupBoxSetup.Location = new System.Drawing.Point(13, 141);
            this.groupBoxSetup.Name = "groupBoxSetup";
            this.groupBoxSetup.Size = new System.Drawing.Size(455, 200);
            this.groupBoxSetup.TabIndex = 5;
            this.groupBoxSetup.TabStop = false;
            this.groupBoxSetup.Text = "Register Skyline MCP server with:";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonClose;
            this.ClientSize = new System.Drawing.Size(480, 357);
            this.Controls.Add(this.groupBoxSetup);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.labelDocument);
            this.Controls.Add(this.buttonSetup);
            this.Controls.Add(this.buttonClose);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(496, 184);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "AI Connector";
            this.groupBoxSetup.ResumeLayout(false);
            this.groupBoxSetup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Label labelVersion;
        private System.Windows.Forms.Label labelDocument;
        private System.Windows.Forms.Button buttonSetup;
        private System.Windows.Forms.Button buttonClose;
        private System.Windows.Forms.CheckBox checkClaudeDesktop;
        private System.Windows.Forms.CheckBox checkClaudeCode;
        private System.Windows.Forms.CheckBox checkGeminiCli;
        private System.Windows.Forms.CheckBox checkVSCode;
        private System.Windows.Forms.CheckBox checkCursor;
        private System.Windows.Forms.Label labelSetupStatus;
        private System.Windows.Forms.GroupBox groupBoxSetup;
    }
}
