//
// $Id: BreadCrumbControl.Designer.cs 55 2011-04-28 15:57:33Z chambm $
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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

namespace CustomDataSourceDialog
{
    partial class BreadCrumbControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BreadCrumbControl));
            this.BreadCrumbTrail = new System.Windows.Forms.ToolStrip();
            this.BackPanel = new System.Windows.Forms.Panel();
            this.RightPanel = new System.Windows.Forms.Panel();
            this.RightToolStrip = new System.Windows.Forms.ToolStrip();
            this.RefreshButton = new System.Windows.Forms.ToolStripButton();
            this.HistoryButton = new System.Windows.Forms.ToolStripButton();
            this.LeftPanel = new System.Windows.Forms.Panel();
            this.BackPanel.SuspendLayout();
            this.RightPanel.SuspendLayout();
            this.RightToolStrip.SuspendLayout();
            this.LeftPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // BreadCrumbTrail
            // 
            this.BreadCrumbTrail.CanOverflow = false;
            this.BreadCrumbTrail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.BreadCrumbTrail.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.BreadCrumbTrail.Location = new System.Drawing.Point(0, 0);
            this.BreadCrumbTrail.Name = "BreadCrumbTrail";
            this.BreadCrumbTrail.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.BreadCrumbTrail.Size = new System.Drawing.Size(373, 21);
            this.BreadCrumbTrail.TabIndex = 1;
            this.BreadCrumbTrail.Text = "toolStrip1";
            this.BreadCrumbTrail.MouseClick += new System.Windows.Forms.MouseEventHandler(this.BreadCrumbTrail_MouseClick);
            // 
            // BackPanel
            // 
            this.BackPanel.Controls.Add(this.RightPanel);
            this.BackPanel.Controls.Add(this.LeftPanel);
            this.BackPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.BackPanel.Location = new System.Drawing.Point(0, 0);
            this.BackPanel.Name = "BackPanel";
            this.BackPanel.Size = new System.Drawing.Size(419, 21);
            this.BackPanel.TabIndex = 2;
            // 
            // RightPanel
            // 
            this.RightPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.RightPanel.Controls.Add(this.RightToolStrip);
            this.RightPanel.Location = new System.Drawing.Point(372, 0);
            this.RightPanel.Name = "RightPanel";
            this.RightPanel.Size = new System.Drawing.Size(47, 21);
            this.RightPanel.TabIndex = 3;
            // 
            // RightToolStrip
            // 
            this.RightToolStrip.CanOverflow = false;
            this.RightToolStrip.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RightToolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.RightToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.RefreshButton,
            this.HistoryButton});
            this.RightToolStrip.Location = new System.Drawing.Point(0, 0);
            this.RightToolStrip.Name = "RightToolStrip";
            this.RightToolStrip.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.RightToolStrip.Size = new System.Drawing.Size(47, 21);
            this.RightToolStrip.TabIndex = 2;
            this.RightToolStrip.Text = "toolStrip1";
            // 
            // RefreshButton
            // 
            this.RefreshButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.RefreshButton.Image = ((System.Drawing.Image)(resources.GetObject("RefreshButton.Image")));
            this.RefreshButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.RefreshButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.RefreshButton.Name = "RefreshButton";
            this.RefreshButton.Size = new System.Drawing.Size(23, 18);
            this.RefreshButton.Text = "Refresh";
            this.RefreshButton.Click += new System.EventHandler(this.RefreshButton_Click);
            // 
            // HistoryButton
            // 
            this.HistoryButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.HistoryButton.Image = ((System.Drawing.Image)(resources.GetObject("HistoryButton.Image")));
            this.HistoryButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.HistoryButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.HistoryButton.Name = "HistoryButton";
            this.HistoryButton.Size = new System.Drawing.Size(23, 18);
            this.HistoryButton.Text = "History";
            this.HistoryButton.Visible = false;
            this.HistoryButton.Click += new System.EventHandler(this.CreateManualFileEntryControl);
            // 
            // LeftPanel
            // 
            this.LeftPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.LeftPanel.Controls.Add(this.BreadCrumbTrail);
            this.LeftPanel.Location = new System.Drawing.Point(0, 0);
            this.LeftPanel.Name = "LeftPanel";
            this.LeftPanel.Size = new System.Drawing.Size(373, 21);
            this.LeftPanel.TabIndex = 2;
            // 
            // BreadCrumbControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.BackPanel);
            this.Name = "BreadCrumbControl";
            this.Size = new System.Drawing.Size(419, 21);
            this.BackPanel.ResumeLayout(false);
            this.RightPanel.ResumeLayout(false);
            this.RightPanel.PerformLayout();
            this.RightToolStrip.ResumeLayout(false);
            this.RightToolStrip.PerformLayout();
            this.LeftPanel.ResumeLayout(false);
            this.LeftPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ToolStrip BreadCrumbTrail;
        private System.Windows.Forms.Panel BackPanel;
        private System.Windows.Forms.Panel RightPanel;
        private System.Windows.Forms.Panel LeftPanel;
        private System.Windows.Forms.ToolStrip RightToolStrip;
        private System.Windows.Forms.ToolStripButton RefreshButton;
        private System.Windows.Forms.ToolStripButton HistoryButton;
    }
}
