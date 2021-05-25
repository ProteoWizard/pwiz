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
    partial class NewVersionForm
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
            this.textTemplate = new System.Windows.Forms.Label();
            this.changelogTextBox = new System.Windows.Forms.TextBox();
            this.yesButton = new System.Windows.Forms.Button();
            this.noButton = new System.Windows.Forms.Button();
            this.showChangeLogCheckbox = new System.Windows.Forms.CheckBox();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // textTemplate
            // 
            this.textTemplate.AutoSize = true;
            this.textTemplate.Location = new System.Drawing.Point(12, 9);
            this.textTemplate.Name = "textTemplate";
            this.textTemplate.Size = new System.Drawing.Size(200, 65);
            this.textTemplate.TabIndex = 0;
            this.textTemplate.Text = "There is a newer version of {0} available.\r\nYou are using version {1}.\r\nThe lates" +
    "t version is {2}.\r\n\r\nDownload it now?";
            // 
            // changelogTextBox
            // 
            this.changelogTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.changelogTextBox.BackColor = System.Drawing.SystemColors.Window;
            this.changelogTextBox.Location = new System.Drawing.Point(12, 3);
            this.changelogTextBox.Multiline = true;
            this.changelogTextBox.Name = "changelogTextBox";
            this.changelogTextBox.ReadOnly = true;
            this.changelogTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.changelogTextBox.Size = new System.Drawing.Size(600, 231);
            this.changelogTextBox.TabIndex = 1;
            // 
            // yesButton
            // 
            this.yesButton.DialogResult = System.Windows.Forms.DialogResult.Yes;
            this.yesButton.Location = new System.Drawing.Point(12, 87);
            this.yesButton.Name = "yesButton";
            this.yesButton.Size = new System.Drawing.Size(75, 23);
            this.yesButton.TabIndex = 2;
            this.yesButton.Text = "Yes";
            this.yesButton.UseVisualStyleBackColor = true;
            // 
            // noButton
            // 
            this.noButton.DialogResult = System.Windows.Forms.DialogResult.No;
            this.noButton.Location = new System.Drawing.Point(93, 87);
            this.noButton.Name = "noButton";
            this.noButton.Size = new System.Drawing.Size(75, 23);
            this.noButton.TabIndex = 3;
            this.noButton.Text = "No";
            this.noButton.UseVisualStyleBackColor = true;
            // 
            // showChangeLogCheckbox
            // 
            this.showChangeLogCheckbox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.showChangeLogCheckbox.Appearance = System.Windows.Forms.Appearance.Button;
            this.showChangeLogCheckbox.AutoSize = true;
            this.showChangeLogCheckbox.Checked = true;
            this.showChangeLogCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showChangeLogCheckbox.Location = new System.Drawing.Point(507, 87);
            this.showChangeLogCheckbox.Name = "showChangeLogCheckbox";
            this.showChangeLogCheckbox.Size = new System.Drawing.Size(105, 23);
            this.showChangeLogCheckbox.TabIndex = 4;
            this.showChangeLogCheckbox.Text = "Show Change Log";
            this.showChangeLogCheckbox.UseVisualStyleBackColor = true;
            this.showChangeLogCheckbox.Visible = false;
            this.showChangeLogCheckbox.CheckedChanged += new System.EventHandler(this.showChangeLogCheckbox_CheckedChanged);
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer.IsSplitterFixed = true;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.showChangeLogCheckbox);
            this.splitContainer.Panel1.Controls.Add(this.textTemplate);
            this.splitContainer.Panel1.Controls.Add(this.yesButton);
            this.splitContainer.Panel1.Controls.Add(this.noButton);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.changelogTextBox);
            this.splitContainer.Panel2MinSize = 0;
            this.splitContainer.Size = new System.Drawing.Size(624, 362);
            this.splitContainer.SplitterDistance = 115;
            this.splitContainer.SplitterWidth = 1;
            this.splitContainer.TabIndex = 5;
            // 
            // NewVersionForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(624, 362);
            this.Controls.Add(this.splitContainer);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(312, 151);
            this.Name = "NewVersionForm";
            this.ShowIcon = false;
            this.Text = "Newer Version Available";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label textTemplate;
        private System.Windows.Forms.TextBox changelogTextBox;
        private System.Windows.Forms.Button yesButton;
        private System.Windows.Forms.Button noButton;
        private System.Windows.Forms.CheckBox showChangeLogCheckbox;
        private System.Windows.Forms.SplitContainer splitContainer;
    }
}