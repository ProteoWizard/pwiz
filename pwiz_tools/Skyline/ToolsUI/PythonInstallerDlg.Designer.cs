namespace pwiz.Skyline.ToolsUI
{
    partial class PythonInstallerDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonInstallerDlg));
            this.labelDescription = new System.Windows.Forms.Label();
            this.textBoxPackages = new System.Windows.Forms.TextBox();
            this.btnInstall = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelDescription
            // 
            this.labelDescription.Location = new System.Drawing.Point(12, 9);
            this.labelDescription.Name = "labelDescription";
            this.labelDescription.Size = new System.Drawing.Size(454, 93);
            this.labelDescription.TabIndex = 0;
            this.labelDescription.Text = resources.GetString("labelDescription.Text");
            // 
            // textBoxPackages
            // 
            this.textBoxPackages.BackColor = System.Drawing.SystemColors.Control;
            this.textBoxPackages.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxPackages.Location = new System.Drawing.Point(16, 105);
            this.textBoxPackages.Multiline = true;
            this.textBoxPackages.Name = "textBoxPackages";
            this.textBoxPackages.Size = new System.Drawing.Size(450, 364);
            this.textBoxPackages.TabIndex = 1;
            this.textBoxPackages.Text = "- package1\r\n- package2 1.2.3\r\n- package3";
            // 
            // btnInstall
            // 
            this.btnInstall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnInstall.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnInstall.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnInstall.Location = new System.Drawing.Point(233, 495);
            this.btnInstall.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnInstall.Name = "btnInstall";
            this.btnInstall.Size = new System.Drawing.Size(112, 35);
            this.btnInstall.TabIndex = 3;
            this.btnInstall.Text = "Install";
            this.btnInstall.UseVisualStyleBackColor = true;
            this.btnInstall.Click += new System.EventHandler(this.btnInstall_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(353, 495);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(112, 35);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // PythonInstallerDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(478, 544);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnInstall);
            this.Controls.Add(this.textBoxPackages);
            this.Controls.Add(this.labelDescription);
            this.Name = "PythonInstallerDlg";
            this.Text = "PythonInstallerDlg";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelDescription;
        private System.Windows.Forms.TextBox textBoxPackages;
        private System.Windows.Forms.Button btnInstall;
        private System.Windows.Forms.Button btnCancel;
    }
}