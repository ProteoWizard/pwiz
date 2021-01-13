namespace SkylineBatch
{
    partial class RVersionControl
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
            this.labelMessage = new System.Windows.Forms.Label();
            this.comboRVersions = new System.Windows.Forms.ComboBox();
            this.labelTitle = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelMessage
            // 
            this.labelMessage.AutoSize = true;
            this.labelMessage.Location = new System.Drawing.Point(-3, 22);
            this.labelMessage.Name = "labelMessage";
            this.labelMessage.Size = new System.Drawing.Size(52, 13);
            this.labelMessage.TabIndex = 0;
            this.labelMessage.Text = "message:";
            // 
            // comboRVersions
            // 
            this.comboRVersions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRVersions.FormattingEnabled = true;
            this.comboRVersions.Location = new System.Drawing.Point(0, 38);
            this.comboRVersions.Name = "comboRVersions";
            this.comboRVersions.Size = new System.Drawing.Size(123, 21);
            this.comboRVersions.TabIndex = 1;
            this.comboRVersions.SelectedIndexChanged += new System.EventHandler(this.comboRVersions_SelectedIndexChanged);
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Location = new System.Drawing.Point(-3, 0);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(27, 13);
            this.labelTitle.TabIndex = 2;
            this.labelTitle.Text = "Title";
            // 
            // RVersionControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.labelTitle);
            this.Controls.Add(this.comboRVersions);
            this.Controls.Add(this.labelMessage);
            this.Name = "RVersionControl";
            this.Size = new System.Drawing.Size(277, 71);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelMessage;
        private System.Windows.Forms.ComboBox comboRVersions;
        private System.Windows.Forms.Label labelTitle;
    }
}
