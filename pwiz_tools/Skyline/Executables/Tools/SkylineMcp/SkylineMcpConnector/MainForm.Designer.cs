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
            this.labelPipe = new System.Windows.Forms.Label();
            this.labelMcpHeader = new System.Windows.Forms.Label();
            this.labelMcpStatus = new System.Windows.Forms.Label();
            this.labelMcpInstructions = new System.Windows.Forms.Label();
            this.textMcpCommand = new System.Windows.Forms.TextBox();
            this.buttonCopyCommand = new System.Windows.Forms.Button();
            this.buttonDisconnect = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // labelStatus
            //
            this.labelStatus.AutoSize = true;
            this.labelStatus.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelStatus.Location = new System.Drawing.Point(20, 20);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(150, 21);
            this.labelStatus.TabIndex = 0;
            this.labelStatus.Text = "Connecting...";
            //
            // labelVersion
            //
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(20, 55);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(50, 15);
            this.labelVersion.TabIndex = 1;
            this.labelVersion.Text = "";
            //
            // labelDocument
            //
            this.labelDocument.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelDocument.AutoEllipsis = true;
            this.labelDocument.Location = new System.Drawing.Point(20, 80);
            this.labelDocument.Name = "labelDocument";
            this.labelDocument.Size = new System.Drawing.Size(440, 15);
            this.labelDocument.TabIndex = 2;
            this.labelDocument.Text = "";
            //
            // labelPipe
            //
            this.labelPipe.AutoSize = true;
            this.labelPipe.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelPipe.Location = new System.Drawing.Point(20, 105);
            this.labelPipe.Name = "labelPipe";
            this.labelPipe.Size = new System.Drawing.Size(50, 15);
            this.labelPipe.TabIndex = 3;
            this.labelPipe.Text = "";
            //
            // labelMcpHeader
            //
            this.labelMcpHeader.AutoSize = true;
            this.labelMcpHeader.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelMcpHeader.Location = new System.Drawing.Point(20, 140);
            this.labelMcpHeader.Name = "labelMcpHeader";
            this.labelMcpHeader.Size = new System.Drawing.Size(120, 19);
            this.labelMcpHeader.TabIndex = 4;
            this.labelMcpHeader.Text = "Claude Code Setup";
            //
            // labelMcpStatus
            //
            this.labelMcpStatus.AutoSize = true;
            this.labelMcpStatus.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelMcpStatus.Location = new System.Drawing.Point(20, 165);
            this.labelMcpStatus.Name = "labelMcpStatus";
            this.labelMcpStatus.Size = new System.Drawing.Size(100, 15);
            this.labelMcpStatus.TabIndex = 5;
            this.labelMcpStatus.Text = "";
            //
            // labelMcpInstructions
            //
            this.labelMcpInstructions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelMcpInstructions.Location = new System.Drawing.Point(20, 190);
            this.labelMcpInstructions.Name = "labelMcpInstructions";
            this.labelMcpInstructions.Size = new System.Drawing.Size(440, 30);
            this.labelMcpInstructions.TabIndex = 6;
            this.labelMcpInstructions.Text = "Run this command in your terminal to register the MCP server, then restart Claude Code:";
            //
            // textMcpCommand
            //
            this.textMcpCommand.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textMcpCommand.BackColor = System.Drawing.SystemColors.Window;
            this.textMcpCommand.Font = new System.Drawing.Font("Consolas", 9F);
            this.textMcpCommand.Location = new System.Drawing.Point(20, 225);
            this.textMcpCommand.Name = "textMcpCommand";
            this.textMcpCommand.ReadOnly = true;
            this.textMcpCommand.Size = new System.Drawing.Size(370, 22);
            this.textMcpCommand.TabIndex = 7;
            //
            // buttonCopyCommand
            //
            this.buttonCopyCommand.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCopyCommand.Location = new System.Drawing.Point(400, 224);
            this.buttonCopyCommand.Name = "buttonCopyCommand";
            this.buttonCopyCommand.Size = new System.Drawing.Size(60, 24);
            this.buttonCopyCommand.TabIndex = 8;
            this.buttonCopyCommand.Text = "Copy";
            this.buttonCopyCommand.Click += new System.EventHandler(this.buttonCopyCommand_Click);
            //
            // buttonDisconnect
            //
            this.buttonDisconnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDisconnect.Location = new System.Drawing.Point(370, 270);
            this.buttonDisconnect.Name = "buttonDisconnect";
            this.buttonDisconnect.Size = new System.Drawing.Size(90, 30);
            this.buttonDisconnect.TabIndex = 9;
            this.buttonDisconnect.Text = "Disconnect";
            this.buttonDisconnect.Click += new System.EventHandler(this.buttonDisconnect_Click);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(480, 315);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.labelDocument);
            this.Controls.Add(this.labelPipe);
            this.Controls.Add(this.labelMcpHeader);
            this.Controls.Add(this.labelMcpStatus);
            this.Controls.Add(this.labelMcpInstructions);
            this.Controls.Add(this.textMcpCommand);
            this.Controls.Add(this.buttonCopyCommand);
            this.Controls.Add(this.buttonDisconnect);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Skyline MCP Connector";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Label labelVersion;
        private System.Windows.Forms.Label labelDocument;
        private System.Windows.Forms.Label labelPipe;
        private System.Windows.Forms.Label labelMcpHeader;
        private System.Windows.Forms.Label labelMcpStatus;
        private System.Windows.Forms.Label labelMcpInstructions;
        private System.Windows.Forms.TextBox textMcpCommand;
        private System.Windows.Forms.Button buttonCopyCommand;
        private System.Windows.Forms.Button buttonDisconnect;
    }
}
