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
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

namespace IDPicker.Controls
{
    partial class ForeBackColorControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent ()
        {
            this.colorDialog = new System.Windows.Forms.ColorDialog();
            this.foregroundColorBox = new System.Windows.Forms.Panel();
            this.backgroundColorBox = new System.Windows.Forms.Panel();
            this.previewBox = new System.Windows.Forms.TextBox();
            this.foregroundCheckBox = new System.Windows.Forms.CheckBox();
            this.backgroundCheckBox = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // foregroundColorBox
            // 
            this.foregroundColorBox.BackColor = System.Drawing.SystemColors.WindowText;
            this.foregroundColorBox.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.foregroundColorBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.foregroundColorBox.Enabled = false;
            this.foregroundColorBox.Location = new System.Drawing.Point(129, 3);
            this.foregroundColorBox.Name = "foregroundColorBox";
            this.foregroundColorBox.Size = new System.Drawing.Size(24, 20);
            this.foregroundColorBox.TabIndex = 6;
            // 
            // backgroundColorBox
            // 
            this.backgroundColorBox.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.backgroundColorBox.BackColor = System.Drawing.SystemColors.Window;
            this.backgroundColorBox.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.backgroundColorBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.backgroundColorBox.Enabled = false;
            this.backgroundColorBox.Location = new System.Drawing.Point(294, 3);
            this.backgroundColorBox.Name = "backgroundColorBox";
            this.backgroundColorBox.Size = new System.Drawing.Size(24, 20);
            this.backgroundColorBox.TabIndex = 7;
            // 
            // previewBox
            // 
            this.previewBox.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.previewBox.BackColor = System.Drawing.SystemColors.Window;
            this.previewBox.Cursor = System.Windows.Forms.Cursors.Default;
            this.previewBox.ForeColor = System.Drawing.SystemColors.WindowText;
            this.previewBox.Location = new System.Drawing.Point(6, 35);
            this.previewBox.Name = "previewBox";
            this.previewBox.ReadOnly = true;
            this.previewBox.Size = new System.Drawing.Size(318, 20);
            this.previewBox.TabIndex = 8;
            this.previewBox.TabStop = false;
            this.previewBox.Text = "Color Preview";
            this.previewBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.previewBox.Enter += new System.EventHandler(this.previewBox_Enter);
            // 
            // foregroundCheckBox
            // 
            this.foregroundCheckBox.AutoSize = true;
            this.foregroundCheckBox.Location = new System.Drawing.Point(11, 6);
            this.foregroundCheckBox.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
            this.foregroundCheckBox.Name = "foregroundCheckBox";
            this.foregroundCheckBox.Size = new System.Drawing.Size(118, 17);
            this.foregroundCheckBox.TabIndex = 9;
            this.foregroundCheckBox.Text = "Custom foreground:";
            this.foregroundCheckBox.UseVisualStyleBackColor = true;
            // 
            // backgroundCheckBox
            // 
            this.backgroundCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.backgroundCheckBox.AutoSize = true;
            this.backgroundCheckBox.Location = new System.Drawing.Point(170, 6);
            this.backgroundCheckBox.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
            this.backgroundCheckBox.Name = "backgroundCheckBox";
            this.backgroundCheckBox.Size = new System.Drawing.Size(124, 17);
            this.backgroundCheckBox.TabIndex = 10;
            this.backgroundCheckBox.Text = "Custom background:";
            this.backgroundCheckBox.UseVisualStyleBackColor = true;
            // 
            // ForeBackColorControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.backgroundCheckBox);
            this.Controls.Add(this.foregroundCheckBox);
            this.Controls.Add(this.previewBox);
            this.Controls.Add(this.backgroundColorBox);
            this.Controls.Add(this.foregroundColorBox);
            this.MinimumSize = new System.Drawing.Size(327, 60);
            this.Name = "ForeBackColorControl";
            this.Size = new System.Drawing.Size(327, 60);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ColorDialog colorDialog;
        private System.Windows.Forms.TextBox previewBox;
        private System.Windows.Forms.Panel foregroundColorBox;
        private System.Windows.Forms.Panel backgroundColorBox;
        private System.Windows.Forms.CheckBox foregroundCheckBox;
        private System.Windows.Forms.CheckBox backgroundCheckBox;
    }
}
