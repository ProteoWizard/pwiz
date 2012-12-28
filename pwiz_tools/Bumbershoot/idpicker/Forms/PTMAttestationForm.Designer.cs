namespace IDPicker.Forms
{
    partial class PTMAttestationForm
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
            this.FragmentMZToleranceLabel = new System.Windows.Forms.Label();
            this.FragmentationTypeLabel = new System.Windows.Forms.Label();
            this.FragmentMZToleranceTextBox = new System.Windows.Forms.TextBox();
            this.listBoxDissociationType = new System.Windows.Forms.ListBox();
            this.btnAttestPTMs = new System.Windows.Forms.Button();
            this.btnCancelAttestaion = new System.Windows.Forms.Button();
            this.tbStatus = new System.Windows.Forms.TextBox();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.progressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.lblStatusToolStrip = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // FragmentMZToleranceLabel
            // 
            this.FragmentMZToleranceLabel.AutoSize = true;
            this.FragmentMZToleranceLabel.Location = new System.Drawing.Point(224, 21);
            this.FragmentMZToleranceLabel.Name = "FragmentMZToleranceLabel";
            this.FragmentMZToleranceLabel.Size = new System.Drawing.Size(132, 13);
            this.FragmentMZToleranceLabel.TabIndex = 3;
            this.FragmentMZToleranceLabel.Text = "Fragment M/Z Tol (in Dal.)";
            // 
            // FragmentationTypeLabel
            // 
            this.FragmentationTypeLabel.AutoSize = true;
            this.FragmentationTypeLabel.Location = new System.Drawing.Point(13, 18);
            this.FragmentationTypeLabel.Name = "FragmentationTypeLabel";
            this.FragmentationTypeLabel.Size = new System.Drawing.Size(104, 13);
            this.FragmentationTypeLabel.TabIndex = 4;
            this.FragmentationTypeLabel.Text = "Fragmentation Type:";
            // 
            // FragmentMZToleranceTextBox
            // 
            this.FragmentMZToleranceTextBox.Location = new System.Drawing.Point(368, 18);
            this.FragmentMZToleranceTextBox.Name = "FragmentMZToleranceTextBox";
            this.FragmentMZToleranceTextBox.Size = new System.Drawing.Size(47, 20);
            this.FragmentMZToleranceTextBox.TabIndex = 6;
            this.FragmentMZToleranceTextBox.Text = "0.5";
            // 
            // listBoxDissociationType
            // 
            this.listBoxDissociationType.AllowDrop = true;
            this.listBoxDissociationType.FormattingEnabled = true;
            this.listBoxDissociationType.Items.AddRange(new object[] {
            "CID/CAD",
            "ETD/ECD",
            "HCD"});
            this.listBoxDissociationType.Location = new System.Drawing.Point(134, 12);
            this.listBoxDissociationType.Name = "listBoxDissociationType";
            this.listBoxDissociationType.Size = new System.Drawing.Size(73, 30);
            this.listBoxDissociationType.TabIndex = 7;
            // 
            // btnAttestPTMs
            // 
            this.btnAttestPTMs.Location = new System.Drawing.Point(11, 60);
            this.btnAttestPTMs.Name = "btnAttestPTMs";
            this.btnAttestPTMs.Size = new System.Drawing.Size(116, 41);
            this.btnAttestPTMs.TabIndex = 8;
            this.btnAttestPTMs.Text = "Attest PTMs";
            this.btnAttestPTMs.UseVisualStyleBackColor = true;
            this.btnAttestPTMs.Click += new System.EventHandler(this.ExecuteAttestButton_Click);
            // 
            // btnCancelAttestaion
            // 
            this.btnCancelAttestaion.Enabled = false;
            this.btnCancelAttestaion.Location = new System.Drawing.Point(154, 60);
            this.btnCancelAttestaion.Name = "btnCancelAttestaion";
            this.btnCancelAttestaion.Size = new System.Drawing.Size(116, 41);
            this.btnCancelAttestaion.TabIndex = 9;
            this.btnCancelAttestaion.Text = "Stop Attestation";
            this.btnCancelAttestaion.UseVisualStyleBackColor = true;
            this.btnCancelAttestaion.Click += new System.EventHandler(this.btnCancelAttestaion_Click);
            // 
            // tbStatus
            // 
            this.tbStatus.AcceptsReturn = true;
            this.tbStatus.AcceptsTab = true;
            this.tbStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbStatus.BackColor = System.Drawing.SystemColors.Window;
            this.tbStatus.Location = new System.Drawing.Point(15, 120);
            this.tbStatus.Multiline = true;
            this.tbStatus.Name = "tbStatus";
            this.tbStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbStatus.Size = new System.Drawing.Size(608, 222);
            this.tbStatus.TabIndex = 10;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.progressBar,
            this.lblStatusToolStrip});
            this.statusStrip1.Location = new System.Drawing.Point(0, 352);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(635, 22);
            this.statusStrip1.TabIndex = 11;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // progressBar
            // 
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(100, 16);
            // 
            // lblStatusToolStrip
            // 
            this.lblStatusToolStrip.Name = "lblStatusToolStrip";
            this.lblStatusToolStrip.Size = new System.Drawing.Size(39, 17);
            this.lblStatusToolStrip.Text = "Ready";
            // 
            // PTMAttestationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(635, 374);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.tbStatus);
            this.Controls.Add(this.btnCancelAttestaion);
            this.Controls.Add(this.btnAttestPTMs);
            this.Controls.Add(this.listBoxDissociationType);
            this.Controls.Add(this.FragmentMZToleranceTextBox);
            this.Controls.Add(this.FragmentationTypeLabel);
            this.Controls.Add(this.FragmentMZToleranceLabel);
            this.Name = "PTMAttestationForm";
            this.TabText = "PTM Attestation";
            this.Text = "PTM Attestation";
            this.Load += new System.EventHandler(this.PTMAttestationForm_Load);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label FragmentMZToleranceLabel;
        private System.Windows.Forms.Label FragmentationTypeLabel;
        private System.Windows.Forms.TextBox FragmentMZToleranceTextBox;
        private System.Windows.Forms.ListBox listBoxDissociationType;
        private System.Windows.Forms.Button btnAttestPTMs;
        private System.Windows.Forms.Button btnCancelAttestaion;
        private System.Windows.Forms.TextBox tbStatus;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
        private System.Windows.Forms.ToolStripStatusLabel lblStatusToolStrip;
    }
}