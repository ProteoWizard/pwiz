namespace SettingsCleaner
{
    partial class SettingsCleaner
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsCleaner));
            this.label1 = new System.Windows.Forms.Label();
            this.DisclaimerBox = new System.Windows.Forms.TextBox();
            this.ActionButton = new System.Windows.Forms.Button();
            this.AcceptBox = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(169, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Restore Database to Default";
            // 
            // DisclaimerBox
            // 
            this.DisclaimerBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.DisclaimerBox.Location = new System.Drawing.Point(12, 25);
            this.DisclaimerBox.Multiline = true;
            this.DisclaimerBox.Name = "DisclaimerBox";
            this.DisclaimerBox.Size = new System.Drawing.Size(434, 100);
            this.DisclaimerBox.TabIndex = 1;
            this.DisclaimerBox.Text = resources.GetString("DisclaimerBox.Text");
            this.DisclaimerBox.Enter += new System.EventHandler(this.DisclaimerBox_Enter);
            // 
            // ActionButton
            // 
            this.ActionButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ActionButton.Location = new System.Drawing.Point(371, 131);
            this.ActionButton.Name = "ActionButton";
            this.ActionButton.Size = new System.Drawing.Size(75, 23);
            this.ActionButton.TabIndex = 2;
            this.ActionButton.Text = "Exit";
            this.ActionButton.UseVisualStyleBackColor = true;
            this.ActionButton.Click += new System.EventHandler(this.ActionButton_Click);
            // 
            // AcceptBox
            // 
            this.AcceptBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.AcceptBox.AutoSize = true;
            this.AcceptBox.Location = new System.Drawing.Point(12, 135);
            this.AcceptBox.Name = "AcceptBox";
            this.AcceptBox.Size = new System.Drawing.Size(303, 17);
            this.AcceptBox.TabIndex = 3;
            this.AcceptBox.Text = "I have read the warning and still wish to reset the database";
            this.AcceptBox.UseVisualStyleBackColor = true;
            this.AcceptBox.CheckedChanged += new System.EventHandler(this.AcceptBox_CheckedChanged);
            // 
            // SettingsCleaner
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(458, 166);
            this.Controls.Add(this.AcceptBox);
            this.Controls.Add(this.ActionButton);
            this.Controls.Add(this.DisclaimerBox);
            this.Controls.Add(this.label1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "SettingsCleaner";
            this.Text = "Settings Cleaner";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox DisclaimerBox;
        private System.Windows.Forms.Button ActionButton;
        private System.Windows.Forms.CheckBox AcceptBox;
    }
}

