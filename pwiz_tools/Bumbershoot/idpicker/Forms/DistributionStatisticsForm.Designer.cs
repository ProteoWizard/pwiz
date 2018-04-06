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
    partial class DistributionStatisticsForm
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
            this.dockPanel = new DigitalRune.Windows.Docking.DockPanel();
            this.refreshButton = new System.Windows.Forms.Button();
            this.refreshDataLabel = new System.Windows.Forms.LinkLabel();
            this.dockPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // dockPanel
            // 
            this.dockPanel.ActiveAutoHideContent = null;
            this.dockPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dockPanel.Controls.Add(this.refreshDataLabel);
            this.dockPanel.DockLeftPortion = 0.5D;
            this.dockPanel.DockRightPortion = 0.5D;
            this.dockPanel.DockTopPortion = 0.5D;
            this.dockPanel.Location = new System.Drawing.Point(0, 27);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Size = new System.Drawing.Size(556, 275);
            this.dockPanel.TabIndex = 26;
            // 
            // refreshButton
            // 
            this.refreshButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.refreshButton.Location = new System.Drawing.Point(524, 2);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(30, 23);
            this.refreshButton.TabIndex = 25;
            this.refreshButton.UseVisualStyleBackColor = true;
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // refreshDataLabel
            // 
            this.refreshDataLabel.AutoSize = true;
            this.refreshDataLabel.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.refreshDataLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.refreshDataLabel.LinkArea = new System.Windows.Forms.LinkArea(0, 10);
            this.refreshDataLabel.LinkColor = System.Drawing.Color.Blue;
            this.refreshDataLabel.Location = new System.Drawing.Point(89, 85);
            this.refreshDataLabel.Name = "refreshDataLabel";
            this.refreshDataLabel.Size = new System.Drawing.Size(347, 78);
            this.refreshDataLabel.TabIndex = 8;
            this.refreshDataLabel.TabStop = true;
            this.refreshDataLabel.Text = "Click here or press the refresh button\r\nto load statistics based on the current f" +
    "ilters.\r\n\r\nIf you change filters, you have to refresh again.";
            this.refreshDataLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.refreshDataLabel.UseCompatibleTextRendering = true;
            this.refreshDataLabel.VisitedLinkColor = System.Drawing.Color.Blue;
            // 
            // DistributionStatisticsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(556, 302);
            this.Controls.Add(this.dockPanel);
            this.Controls.Add(this.refreshButton);
            this.MinimumSize = new System.Drawing.Size(280, 100);
            this.Name = "DistributionStatisticsForm";
            this.TabText = "DistributionStatisticsForm";
            this.Text = "DistributionStatisticsForm";
            this.dockPanel.ResumeLayout(false);
            this.dockPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private DigitalRune.Windows.Docking.DockPanel dockPanel;
        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.LinkLabel refreshDataLabel;
    }
}