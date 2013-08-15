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
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(178, 194);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 8;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(259, 194);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // instructionLabel
            // 
            this.instructionLabel.AutoSize = true;
            this.instructionLabel.Location = new System.Drawing.Point(9, 6);
            this.instructionLabel.Name = "instructionLabel";
            this.instructionLabel.Size = new System.Drawing.Size(306, 39);
            this.instructionLabel.TabIndex = 0;
            this.instructionLabel.Text = "Once your project has been created, and you have registered a\r\nuser account, plea" +
    "se enter the email and password for your\r\nuser account below.";
            // 
            // InputPanel
            // 
            this.InputPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
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
            this.InputPanel.Location = new System.Drawing.Point(3, 65);
            this.InputPanel.Name = "InputPanel";
            this.InputPanel.Size = new System.Drawing.Size(337, 220);
            this.InputPanel.TabIndex = 10;
            // 
            // lblProjectInfo
            // 
            this.lblProjectInfo.AutoSize = true;
            this.lblProjectInfo.Location = new System.Drawing.Point(13, 10);
            this.lblProjectInfo.Name = "lblProjectInfo";
            this.lblProjectInfo.Size = new System.Drawing.Size(188, 13);
            this.lblProjectInfo.TabIndex = 0;
            this.lblProjectInfo.Text = "&URL (e.g. https://panoramaweb.org/):";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(229, 151);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(50, 13);
            this.label2.TabIndex = 7;
            this.label2.Text = "(optional)";
            // 
            // textServerURL
            // 
            this.textServerURL.Location = new System.Drawing.Point(16, 26);
            this.textServerURL.Name = "textServerURL";
            this.textServerURL.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textServerURL.Size = new System.Drawing.Size(303, 20);
            this.textServerURL.TabIndex = 1;
            // 
            // textPassword
            // 
            this.textPassword.Location = new System.Drawing.Point(16, 148);
            this.textPassword.Name = "textPassword";
            this.textPassword.PasswordChar = '*';
            this.textPassword.Size = new System.Drawing.Size(207, 20);
            this.textPassword.TabIndex = 6;
            this.textPassword.UseSystemPasswordChar = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(229, 90);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(50, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "(optional)";
            // 
            // lblPassword
            // 
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new System.Drawing.Point(13, 128);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(56, 13);
            this.lblPassword.TabIndex = 5;
            this.lblPassword.Text = "&Password:";
            // 
            // lblUsername
            // 
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new System.Drawing.Point(13, 69);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(35, 13);
            this.lblUsername.TabIndex = 2;
            this.lblUsername.Text = "&Email:";
            // 
            // textUsername
            // 
            this.textUsername.Location = new System.Drawing.Point(16, 87);
            this.textUsername.Name = "textUsername";
            this.textUsername.Size = new System.Drawing.Size(207, 20);
            this.textUsername.TabIndex = 3;
            // 
            // InstructionPanel
            // 
            this.InstructionPanel.AutoSize = true;
            this.InstructionPanel.Controls.Add(this.instructionLabel);
            this.InstructionPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.InstructionPanel.Location = new System.Drawing.Point(3, 3);
            this.InstructionPanel.Name = "InstructionPanel";
            this.InstructionPanel.Size = new System.Drawing.Size(337, 56);
            this.InstructionPanel.TabIndex = 10;
            this.InstructionPanel.Visible = false;
            // 
            // ComponentOrganizer
            // 
            this.ComponentOrganizer.AutoSize = true;
            this.ComponentOrganizer.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ComponentOrganizer.ColumnCount = 1;
            this.ComponentOrganizer.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.ComponentOrganizer.Controls.Add(this.InstructionPanel, 0, 0);
            this.ComponentOrganizer.Controls.Add(this.InputPanel, 0, 1);
            this.ComponentOrganizer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ComponentOrganizer.Location = new System.Drawing.Point(0, 0);
            this.ComponentOrganizer.Name = "ComponentOrganizer";
            this.ComponentOrganizer.RowCount = 2;
            this.ComponentOrganizer.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.ComponentOrganizer.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 226F));
            this.ComponentOrganizer.Size = new System.Drawing.Size(343, 288);
            this.ComponentOrganizer.TabIndex = 12;
            // 
            // EditServerDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(343, 288);
            this.Controls.Add(this.ComponentOrganizer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditServerDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Server";
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