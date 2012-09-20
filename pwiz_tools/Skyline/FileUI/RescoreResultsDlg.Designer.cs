namespace pwiz.Skyline.FileUI
{
    partial class RescoreResultsDlg
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
            this.btnRescoreAs = new System.Windows.Forms.Button();
            this.btnRescore = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelMessage = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnRescoreAs
            // 
            this.btnRescoreAs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRescoreAs.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnRescoreAs.Location = new System.Drawing.Point(182, 130);
            this.btnRescoreAs.Name = "btnRescoreAs";
            this.btnRescoreAs.Size = new System.Drawing.Size(139, 23);
            this.btnRescoreAs.TabIndex = 2;
            this.btnRescoreAs.Text = "Re-score and save &as...";
            this.btnRescoreAs.UseVisualStyleBackColor = true;
            this.btnRescoreAs.Click += new System.EventHandler(this.btnRescoreAs_Click);
            // 
            // btnRescore
            // 
            this.btnRescore.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRescore.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnRescore.Location = new System.Drawing.Point(73, 130);
            this.btnRescore.Name = "btnRescore";
            this.btnRescore.Size = new System.Drawing.Size(103, 23);
            this.btnRescore.TabIndex = 1;
            this.btnRescore.Text = "Re-score in &place";
            this.btnRescore.UseVisualStyleBackColor = true;
            this.btnRescore.Click += new System.EventHandler(this.btnRescore_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(327, 130);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // labelMessage
            // 
            this.labelMessage.AutoSize = true;
            this.labelMessage.Location = new System.Drawing.Point(24, 21);
            this.labelMessage.MaximumSize = new System.Drawing.Size(362, 0);
            this.labelMessage.Name = "labelMessage";
            this.labelMessage.Size = new System.Drawing.Size(49, 13);
            this.labelMessage.TabIndex = 0;
            this.labelMessage.Text = "message\r\n";
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.SystemColors.Window;
            this.panel1.Controls.Add(this.labelMessage);
            this.panel1.Location = new System.Drawing.Point(-2, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(418, 119);
            this.panel1.TabIndex = 0;
            // 
            // RescoreResultsDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(414, 165);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.btnRescoreAs);
            this.Controls.Add(this.btnRescore);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RescoreResultsDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Re-score Results";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnRescoreAs;
        private System.Windows.Forms.Button btnRescore;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label labelMessage;
        private System.Windows.Forms.Panel panel1;
    }
}