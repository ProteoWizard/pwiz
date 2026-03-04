/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace pwiz.Skyline.Alerts
{
    partial class ScreenCapturePermissionDlg
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
            this.labelMessage = new System.Windows.Forms.Label();
            this.cbDoNotAskAgain = new System.Windows.Forms.CheckBox();
            this.btnAllow = new System.Windows.Forms.Button();
            this.btnDeny = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // labelMessage
            //
            this.labelMessage.Location = new System.Drawing.Point(12, 20);
            this.labelMessage.Name = "labelMessage";
            this.labelMessage.Size = new System.Drawing.Size(360, 40);
            this.labelMessage.TabIndex = 0;
            this.labelMessage.Text = AlertsResources.ScreenCapturePermissionDlg_Message;
            //
            // cbDoNotAskAgain
            //
            this.cbDoNotAskAgain.AutoSize = true;
            this.cbDoNotAskAgain.Location = new System.Drawing.Point(15, 72);
            this.cbDoNotAskAgain.Name = "cbDoNotAskAgain";
            this.cbDoNotAskAgain.Size = new System.Drawing.Size(134, 17);
            this.cbDoNotAskAgain.TabIndex = 1;
            this.cbDoNotAskAgain.Text = AlertsResources.ScreenCapturePermissionDlg_DoNotAskAgain;
            this.cbDoNotAskAgain.UseVisualStyleBackColor = true;
            //
            // btnAllow
            //
            this.btnAllow.Location = new System.Drawing.Point(216, 100);
            this.btnAllow.Name = "btnAllow";
            this.btnAllow.Size = new System.Drawing.Size(75, 23);
            this.btnAllow.TabIndex = 2;
            this.btnAllow.Text = AlertsResources.ScreenCapturePermissionDlg_Allow;
            this.btnAllow.UseVisualStyleBackColor = true;
            this.btnAllow.Click += new System.EventHandler(this.btnAllow_Click);
            //
            // btnDeny
            //
            this.btnDeny.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnDeny.Location = new System.Drawing.Point(297, 100);
            this.btnDeny.Name = "btnDeny";
            this.btnDeny.Size = new System.Drawing.Size(75, 23);
            this.btnDeny.TabIndex = 3;
            this.btnDeny.Text = AlertsResources.ScreenCapturePermissionDlg_Deny;
            this.btnDeny.UseVisualStyleBackColor = true;
            this.btnDeny.Click += new System.EventHandler(this.btnDeny_Click);
            //
            // ScreenCapturePermissionDlg
            //
            this.AcceptButton = this.btnAllow;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnDeny;
            this.ClientSize = new System.Drawing.Size(384, 136);
            this.Controls.Add(this.btnDeny);
            this.Controls.Add(this.btnAllow);
            this.Controls.Add(this.cbDoNotAskAgain);
            this.Controls.Add(this.labelMessage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ScreenCapturePermissionDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = AlertsResources.ScreenCapturePermissionDlg_Title;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label labelMessage;
        private System.Windows.Forms.CheckBox cbDoNotAskAgain;
        private System.Windows.Forms.Button btnAllow;
        private System.Windows.Forms.Button btnDeny;
    }
}
