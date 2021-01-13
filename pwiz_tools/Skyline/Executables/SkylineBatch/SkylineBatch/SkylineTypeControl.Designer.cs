namespace SkylineBatch
{
    partial class SkylineTypeControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.textSkylineInstallationPath = new System.Windows.Forms.TextBox();
            this.radioButtonSkylineDaily = new System.Windows.Forms.RadioButton();
            this.radioButtonSpecifySkylinePath = new System.Windows.Forms.RadioButton();
            this.radioButtonSkyline = new System.Windows.Forms.RadioButton();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textSkylineInstallationPath
            // 
            this.textSkylineInstallationPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textSkylineInstallationPath.Enabled = false;
            this.textSkylineInstallationPath.Location = new System.Drawing.Point(18, 64);
            this.textSkylineInstallationPath.Margin = new System.Windows.Forms.Padding(2);
            this.textSkylineInstallationPath.Name = "textSkylineInstallationPath";
            this.textSkylineInstallationPath.Size = new System.Drawing.Size(310, 20);
            this.textSkylineInstallationPath.TabIndex = 24;
            this.textSkylineInstallationPath.Text = "C:\\Program Files\\Skyline";
            // 
            // radioButtonSkylineDaily
            // 
            this.radioButtonSkylineDaily.AutoSize = true;
            this.radioButtonSkylineDaily.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
            this.radioButtonSkylineDaily.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.radioButtonSkylineDaily.Location = new System.Drawing.Point(0, 22);
            this.radioButtonSkylineDaily.Margin = new System.Windows.Forms.Padding(2);
            this.radioButtonSkylineDaily.Name = "radioButtonSkylineDaily";
            this.radioButtonSkylineDaily.Size = new System.Drawing.Size(105, 17);
            this.radioButtonSkylineDaily.TabIndex = 28;
            this.radioButtonSkylineDaily.TabStop = true;
            this.radioButtonSkylineDaily.Text = "Use Skyline-&daily";
            this.radioButtonSkylineDaily.UseVisualStyleBackColor = true;
            // 
            // radioButtonSpecifySkylinePath
            // 
            this.radioButtonSpecifySkylinePath.AutoSize = true;
            this.radioButtonSpecifySkylinePath.CheckAlign = System.Drawing.ContentAlignment.BottomLeft;
            this.radioButtonSpecifySkylinePath.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.radioButtonSpecifySkylinePath.Location = new System.Drawing.Point(0, 43);
            this.radioButtonSpecifySkylinePath.Margin = new System.Windows.Forms.Padding(2);
            this.radioButtonSpecifySkylinePath.Name = "radioButtonSpecifySkylinePath";
            this.radioButtonSpecifySkylinePath.Size = new System.Drawing.Size(192, 17);
            this.radioButtonSpecifySkylinePath.TabIndex = 26;
            this.radioButtonSpecifySkylinePath.TabStop = true;
            this.radioButtonSpecifySkylinePath.Text = "&Specify Skyline installation directory";
            this.radioButtonSpecifySkylinePath.UseVisualStyleBackColor = true;
            this.radioButtonSpecifySkylinePath.CheckedChanged += new System.EventHandler(this.RadioButtonChanged);
            // 
            // radioButtonSkyline
            // 
            this.radioButtonSkyline.AutoSize = true;
            this.radioButtonSkyline.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
            this.radioButtonSkyline.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.radioButtonSkyline.Location = new System.Drawing.Point(0, 1);
            this.radioButtonSkyline.Margin = new System.Windows.Forms.Padding(2);
            this.radioButtonSkyline.Name = "radioButtonSkyline";
            this.radioButtonSkyline.Size = new System.Drawing.Size(81, 17);
            this.radioButtonSkyline.TabIndex = 25;
            this.radioButtonSkyline.TabStop = true;
            this.radioButtonSkyline.Text = "&Use Skyline";
            this.radioButtonSkyline.UseVisualStyleBackColor = true;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowse.Enabled = false;
            this.btnBrowse.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnBrowse.Location = new System.Drawing.Point(332, 62);
            this.btnBrowse.Margin = new System.Windows.Forms.Padding(2);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(32, 23);
            this.btnBrowse.TabIndex = 27;
            this.btnBrowse.Text = "...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // SkylineTypeControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.textSkylineInstallationPath);
            this.Controls.Add(this.radioButtonSkylineDaily);
            this.Controls.Add(this.radioButtonSpecifySkylinePath);
            this.Controls.Add(this.radioButtonSkyline);
            this.Controls.Add(this.btnBrowse);
            this.Name = "SkylineTypeControl";
            this.Size = new System.Drawing.Size(364, 91);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textSkylineInstallationPath;
        private System.Windows.Forms.RadioButton radioButtonSkylineDaily;
        private System.Windows.Forms.RadioButton radioButtonSpecifySkylinePath;
        private System.Windows.Forms.RadioButton radioButtonSkyline;
        private System.Windows.Forms.Button btnBrowse;
    }
}
