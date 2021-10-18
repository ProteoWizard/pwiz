namespace SkylineBatch
{
    partial class RemoteFileControl
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
            this.label2 = new System.Windows.Forms.Label();
            this.textRelativePath = new System.Windows.Forms.TextBox();
            this.comboRemoteFileSource = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label2.Location = new System.Drawing.Point(-6, 96);
            this.label2.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(144, 25);
            this.label2.TabIndex = 25;
            this.label2.Text = "Relative path:";
            // 
            // textRelativePath
            // 
            this.textRelativePath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textRelativePath.Location = new System.Drawing.Point(0, 127);
            this.textRelativePath.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.textRelativePath.Name = "textRelativePath";
            this.textRelativePath.Size = new System.Drawing.Size(758, 31);
            this.textRelativePath.TabIndex = 24;
            // 
            // comboRemoteFileSource
            // 
            this.comboRemoteFileSource.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboRemoteFileSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRemoteFileSource.FormattingEnabled = true;
            this.comboRemoteFileSource.Location = new System.Drawing.Point(0, 29);
            this.comboRemoteFileSource.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.comboRemoteFileSource.Name = "comboRemoteFileSource";
            this.comboRemoteFileSource.Size = new System.Drawing.Size(758, 33);
            this.comboRemoteFileSource.TabIndex = 23;
            this.comboRemoteFileSource.SelectedIndexChanged += new System.EventHandler(this.comboRemoteFileSource_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label1.Location = new System.Drawing.Point(-6, -2);
            this.label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(163, 25);
            this.label1.TabIndex = 22;
            this.label1.Text = "Remote source:";
            // 
            // RemoteFileControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textRelativePath);
            this.Controls.Add(this.comboRemoteFileSource);
            this.Controls.Add(this.label1);
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.Name = "RemoteFileControl";
            this.Size = new System.Drawing.Size(764, 171);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textRelativePath;
        private System.Windows.Forms.ComboBox comboRemoteFileSource;
        private System.Windows.Forms.Label label1;
    }
}
