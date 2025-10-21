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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

namespace IDPicker.Forms
{
    partial class EmbedGeneMetadataWarningForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose (bool disposing)
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
        private void InitializeComponent ()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EmbedGeneMetadataWarningForm));
            this.label1 = new System.Windows.Forms.Label();
            this.skipButton = new System.Windows.Forms.Button();
            this.doNotShowCheckBox = new System.Windows.Forms.CheckBox();
            this.embedButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(8, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(407, 120);
            this.label1.TabIndex = 0;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // skipButton
            // 
            this.skipButton.DialogResult = System.Windows.Forms.DialogResult.Ignore;
            this.skipButton.Location = new System.Drawing.Point(337, 154);
            this.skipButton.Name = "skipButton";
            this.skipButton.Size = new System.Drawing.Size(75, 23);
            this.skipButton.TabIndex = 1;
            this.skipButton.Text = "Skip";
            this.skipButton.UseVisualStyleBackColor = true;
            // 
            // doNotShowCheckBox
            // 
            this.doNotShowCheckBox.AutoSize = true;
            this.doNotShowCheckBox.Location = new System.Drawing.Point(12, 158);
            this.doNotShowCheckBox.Name = "doNotShowCheckBox";
            this.doNotShowCheckBox.Size = new System.Drawing.Size(136, 17);
            this.doNotShowCheckBox.TabIndex = 2;
            this.doNotShowCheckBox.Text = "Don\'t ask me this again";
            this.doNotShowCheckBox.UseVisualStyleBackColor = true;
            // 
            // embedButton
            // 
            this.embedButton.DialogResult = System.Windows.Forms.DialogResult.Yes;
            this.embedButton.Location = new System.Drawing.Point(256, 154);
            this.embedButton.Name = "embedButton";
            this.embedButton.Size = new System.Drawing.Size(75, 23);
            this.embedButton.TabIndex = 0;
            this.embedButton.Text = "Embed";
            this.embedButton.UseVisualStyleBackColor = true;
            // 
            // EmbedGeneMetadataWarningForm
            // 
            this.AcceptButton = this.embedButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.skipButton;
            this.ClientSize = new System.Drawing.Size(424, 189);
            this.ControlBox = false;
            this.Controls.Add(this.embedButton);
            this.Controls.Add(this.doNotShowCheckBox);
            this.Controls.Add(this.skipButton);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "EmbedGeneMetadataWarningForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Enable Gene Features?";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button skipButton;
        private System.Windows.Forms.CheckBox doNotShowCheckBox;
        private System.Windows.Forms.Button embedButton;
    }
}