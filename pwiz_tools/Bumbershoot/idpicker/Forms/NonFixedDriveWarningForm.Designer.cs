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
    partial class NonFixedDriveWarningForm
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
            this.label1 = new System.Windows.Forms.Label();
            this.ignoreButton = new System.Windows.Forms.Button();
            this.doNotShowCheckBox = new System.Windows.Forms.CheckBox();
            this.copyButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.label1.Location = new System.Drawing.Point(8, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(380, 100);
            this.label1.TabIndex = 0;
            this.label1.Text = "IDPicker does not perform well on non-fixed drives,\r\nsuch as network, optical, an" +
                "d slow thumb drives.\r\n\r\nIt is recommended that you copy the idpDB to a local\r\ndr" +
                "ive and open it from there.";
            // 
            // ignoreButton
            // 
            this.ignoreButton.DialogResult = System.Windows.Forms.DialogResult.Ignore;
            this.ignoreButton.Location = new System.Drawing.Point(311, 154);
            this.ignoreButton.Name = "ignoreButton";
            this.ignoreButton.Size = new System.Drawing.Size(75, 23);
            this.ignoreButton.TabIndex = 1;
            this.ignoreButton.Text = "Ignore";
            this.ignoreButton.UseVisualStyleBackColor = true;
            // 
            // doNotShowCheckBox
            // 
            this.doNotShowCheckBox.AutoSize = true;
            this.doNotShowCheckBox.Location = new System.Drawing.Point(12, 158);
            this.doNotShowCheckBox.Name = "doNotShowCheckBox";
            this.doNotShowCheckBox.Size = new System.Drawing.Size(183, 17);
            this.doNotShowCheckBox.TabIndex = 2;
            this.doNotShowCheckBox.Text = "Don\'t show this warning next time";
            this.doNotShowCheckBox.UseVisualStyleBackColor = true;
            // 
            // copyButton
            // 
            this.copyButton.DialogResult = System.Windows.Forms.DialogResult.Yes;
            this.copyButton.Location = new System.Drawing.Point(230, 154);
            this.copyButton.Name = "copyButton";
            this.copyButton.Size = new System.Drawing.Size(75, 23);
            this.copyButton.TabIndex = 0;
            this.copyButton.Text = "Copy Local";
            this.copyButton.UseVisualStyleBackColor = true;
            // 
            // NonFixedDriveWarningForm
            // 
            this.AcceptButton = this.copyButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.ignoreButton;
            this.ClientSize = new System.Drawing.Size(398, 189);
            this.ControlBox = false;
            this.Controls.Add(this.copyButton);
            this.Controls.Add(this.doNotShowCheckBox);
            this.Controls.Add(this.ignoreButton);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "NonFixedDriveWarningForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Non-Fixed Drive Warning";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button ignoreButton;
        private System.Windows.Forms.CheckBox doNotShowCheckBox;
        private System.Windows.Forms.Button copyButton;
    }
}