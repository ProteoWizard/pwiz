//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

namespace IDPicker
{
    partial class IDPickerForm
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
            this.components = new System.ComponentModel.Container();
            this.dockPanel = new DigitalRune.Windows.Docking.DockPanel();
            this.layoutMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.layoutButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // dockPanel
            // 
            this.dockPanel.ActiveAutoHideContent = null;
            this.dockPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.dockPanel.DefaultFloatingWindowSize = new System.Drawing.Size(300, 300);
            this.dockPanel.DockLeftPortion = 0.5;
            this.dockPanel.DockRightPortion = 0.5;
            this.dockPanel.DockTopPortion = 0.5;
            this.dockPanel.Location = new System.Drawing.Point(0, 26);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Size = new System.Drawing.Size(584, 448);
            this.dockPanel.TabIndex = 0;
            // 
            // layoutMenuStrip
            // 
            this.layoutMenuStrip.Name = "layoutMenuStrip";
            this.layoutMenuStrip.Size = new System.Drawing.Size(61, 4);
            // 
            // layoutButton
            // 
            this.layoutButton.Location = new System.Drawing.Point(1, 2);
            this.layoutButton.Name = "layoutButton";
            this.layoutButton.Size = new System.Drawing.Size(48, 23);
            this.layoutButton.TabIndex = 2;
            this.layoutButton.Text = "Layout";
            this.layoutButton.UseVisualStyleBackColor = true;
            this.layoutButton.Click += new System.EventHandler(this.layoutButton_Click);
            // 
            // IDPickerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 474);
            this.Controls.Add(this.dockPanel);
            this.Controls.Add(this.layoutButton);
            this.Name = "IDPickerForm";
            this.Text = "IDPickerForm";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.IDPickerForm_FormClosing);
            this.ResumeLayout(false);

        }

        #endregion

        private DigitalRune.Windows.Docking.DockPanel dockPanel;
        private System.Windows.Forms.ContextMenuStrip layoutMenuStrip;
        private System.Windows.Forms.Button layoutButton;

    }
}

