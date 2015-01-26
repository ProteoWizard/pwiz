//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Surendra Dasari
//
// Copyright 2015 Vanderbilt University
//
// Contributor(s):
//

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
            this.dissociationTypeComboBox = new System.Windows.Forms.ComboBox();
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
            this.FragmentMZToleranceLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.FragmentMZToleranceLabel.AutoSize = true;
            this.FragmentMZToleranceLabel.Location = new System.Drawing.Point(255, 12);
            this.FragmentMZToleranceLabel.Name = "FragmentMZToleranceLabel";
            this.FragmentMZToleranceLabel.Size = new System.Drawing.Size(178, 13);
            this.FragmentMZToleranceLabel.TabIndex = 3;
            this.FragmentMZToleranceLabel.Text = "Fragment m/z tolerance (in Daltons):";
            // 
            // FragmentationTypeLabel
            // 
            this.FragmentationTypeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.FragmentationTypeLabel.AutoSize = true;
            this.FragmentationTypeLabel.Location = new System.Drawing.Point(34, 12);
            this.FragmentationTypeLabel.Name = "FragmentationTypeLabel";
            this.FragmentationTypeLabel.Size = new System.Drawing.Size(100, 13);
            this.FragmentationTypeLabel.TabIndex = 4;
            this.FragmentationTypeLabel.Text = "Fragmentation type:";
            // 
            // FragmentMZToleranceTextBox
            // 
            this.FragmentMZToleranceTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.FragmentMZToleranceTextBox.Location = new System.Drawing.Point(436, 9);
            this.FragmentMZToleranceTextBox.Name = "FragmentMZToleranceTextBox";
            this.FragmentMZToleranceTextBox.Size = new System.Drawing.Size(47, 20);
            this.FragmentMZToleranceTextBox.TabIndex = 6;
            this.FragmentMZToleranceTextBox.Text = "0.5";
            // 
            // dissociationTypeComboBox
            // 
            this.dissociationTypeComboBox.AllowDrop = true;
            this.dissociationTypeComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.dissociationTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.dissociationTypeComboBox.FormattingEnabled = true;
            this.dissociationTypeComboBox.Items.AddRange(new object[] {
            "Auto",
            "Trap CID",
            "Beam CID (HCD)",
            "ETD/ECD"});
            this.dissociationTypeComboBox.Location = new System.Drawing.Point(144, 9);
            this.dissociationTypeComboBox.Name = "dissociationTypeComboBox";
            this.dissociationTypeComboBox.Size = new System.Drawing.Size(100, 21);
            this.dissociationTypeComboBox.TabIndex = 7;
            // 
            // btnAttestPTMs
            // 
            this.btnAttestPTMs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAttestPTMs.Location = new System.Drawing.Point(493, 9);
            this.btnAttestPTMs.Name = "btnAttestPTMs";
            this.btnAttestPTMs.Size = new System.Drawing.Size(62, 22);
            this.btnAttestPTMs.TabIndex = 8;
            this.btnAttestPTMs.Text = "Start";
            this.btnAttestPTMs.UseVisualStyleBackColor = true;
            this.btnAttestPTMs.Click += new System.EventHandler(this.ExecuteAttestButton_Click);
            // 
            // btnCancelAttestaion
            // 
            this.btnCancelAttestaion.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancelAttestaion.Enabled = false;
            this.btnCancelAttestaion.Location = new System.Drawing.Point(561, 9);
            this.btnCancelAttestaion.Name = "btnCancelAttestaion";
            this.btnCancelAttestaion.Size = new System.Drawing.Size(62, 22);
            this.btnCancelAttestaion.TabIndex = 9;
            this.btnCancelAttestaion.Text = "Cancel";
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
            this.tbStatus.Location = new System.Drawing.Point(15, 37);
            this.tbStatus.Multiline = true;
            this.tbStatus.Name = "tbStatus";
            this.tbStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbStatus.Size = new System.Drawing.Size(608, 305);
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
            this.Controls.Add(this.dissociationTypeComboBox);
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
        private System.Windows.Forms.ComboBox dissociationTypeComboBox;
        private System.Windows.Forms.Button btnAttestPTMs;
        private System.Windows.Forms.Button btnCancelAttestaion;
        private System.Windows.Forms.TextBox tbStatus;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
        private System.Windows.Forms.ToolStripStatusLabel lblStatusToolStrip;
    }
}