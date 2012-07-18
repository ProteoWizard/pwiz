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
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

namespace IDPicker.Forms
{
    partial class FragmentationStatisticsForm
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
            this.exportButton = new System.Windows.Forms.Button();
            this.exportMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.clipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.spectrumFiltersTextBox = new System.Windows.Forms.TextBox();
            this.lockZoomCheckBox = new System.Windows.Forms.CheckBox();
            this.refreshButton = new System.Windows.Forms.Button();
            this.fragmentToleranceUnitsComboBox = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.fragmentToleranceTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.dockPanel = new DigitalRune.Windows.Docking.DockPanel();
            this.refreshDataLabel = new System.Windows.Forms.LinkLabel();
            this.exportMenu.SuspendLayout();
            this.dockPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Enabled = false;
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.Location = new System.Drawing.Point(501, 2);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(30, 23);
            this.exportButton.TabIndex = 4;
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // exportMenu
            // 
            this.exportMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clipboardToolStripMenuItem,
            this.fileToolStripMenuItem,
            this.showInExcelToolStripMenuItem});
            this.exportMenu.Name = "contextMenuStrip1";
            this.exportMenu.Size = new System.Drawing.Size(172, 70);
            // 
            // clipboardToolStripMenuItem
            // 
            this.clipboardToolStripMenuItem.Name = "clipboardToolStripMenuItem";
            this.clipboardToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.clipboardToolStripMenuItem.Text = "Copy to Clipboard";
            this.clipboardToolStripMenuItem.Click += new System.EventHandler(this.clipboardToolStripMenuItem_Click);
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.fileToolStripMenuItem.Text = "Export to File";
            this.fileToolStripMenuItem.Click += new System.EventHandler(this.fileToolStripMenuItem_Click);
            // 
            // showInExcelToolStripMenuItem
            // 
            this.showInExcelToolStripMenuItem.Name = "showInExcelToolStripMenuItem";
            this.showInExcelToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.showInExcelToolStripMenuItem.Text = "Show in Excel";
            this.showInExcelToolStripMenuItem.Click += new System.EventHandler(this.showInExcelToolStripMenuItem_Click);
            // 
            // spectrumFiltersTextBox
            // 
            this.spectrumFiltersTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.spectrumFiltersTextBox.Location = new System.Drawing.Point(44, 4);
            this.spectrumFiltersTextBox.Name = "spectrumFiltersTextBox";
            this.spectrumFiltersTextBox.Size = new System.Drawing.Size(161, 20);
            this.spectrumFiltersTextBox.TabIndex = 6;
            this.spectrumFiltersTextBox.Text = "threshold count 50 most-intense;";
            this.spectrumFiltersTextBox.TextChanged += new System.EventHandler(this.spectrumFiltersTextBox_TextChanged);
            this.spectrumFiltersTextBox.Leave += new System.EventHandler(this.spectrumFiltersTextBox_Leave);
            // 
            // lockZoomCheckBox
            // 
            this.lockZoomCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lockZoomCheckBox.Appearance = System.Windows.Forms.Appearance.Button;
            this.lockZoomCheckBox.AutoSize = true;
            this.lockZoomCheckBox.Location = new System.Drawing.Point(392, 2);
            this.lockZoomCheckBox.Name = "lockZoomCheckBox";
            this.lockZoomCheckBox.Size = new System.Drawing.Size(71, 23);
            this.lockZoomCheckBox.TabIndex = 7;
            this.lockZoomCheckBox.Text = "Lock Zoom";
            this.lockZoomCheckBox.UseVisualStyleBackColor = true;
            // 
            // refreshButton
            // 
            this.refreshButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.refreshButton.Location = new System.Drawing.Point(467, 2);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(30, 23);
            this.refreshButton.TabIndex = 9;
            this.refreshButton.UseVisualStyleBackColor = true;
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // fragmentToleranceUnitsComboBox
            // 
            this.fragmentToleranceUnitsComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.fragmentToleranceUnitsComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.fragmentToleranceUnitsComboBox.FormattingEnabled = true;
            this.fragmentToleranceUnitsComboBox.Items.AddRange(new object[] {
            "m/z",
            "ppm"});
            this.fragmentToleranceUnitsComboBox.Location = new System.Drawing.Point(339, 3);
            this.fragmentToleranceUnitsComboBox.Name = "fragmentToleranceUnitsComboBox";
            this.fragmentToleranceUnitsComboBox.Size = new System.Drawing.Size(47, 21);
            this.fragmentToleranceUnitsComboBox.TabIndex = 22;
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(212, 7);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(87, 13);
            this.label4.TabIndex = 21;
            this.label4.Text = "Match tolerance:";
            // 
            // fragmentToleranceTextBox
            // 
            this.fragmentToleranceTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.fragmentToleranceTextBox.Location = new System.Drawing.Point(300, 4);
            this.fragmentToleranceTextBox.Name = "fragmentToleranceTextBox";
            this.fragmentToleranceTextBox.Size = new System.Drawing.Size(41, 20);
            this.fragmentToleranceTextBox.TabIndex = 20;
            this.fragmentToleranceTextBox.Text = "0.5";
            this.fragmentToleranceTextBox.TextChanged += new System.EventHandler(this.spectrumFiltersTextBox_TextChanged);
            this.fragmentToleranceTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.fragmentToleranceTextBox_KeyDown);
            this.fragmentToleranceTextBox.Leave += new System.EventHandler(this.spectrumFiltersTextBox_Leave);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(4, 7);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(37, 13);
            this.label1.TabIndex = 23;
            this.label1.Text = "Filters:";
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
            this.dockPanel.Location = new System.Drawing.Point(-2, 27);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Size = new System.Drawing.Size(547, 369);
            this.dockPanel.TabIndex = 24;
            // 
            // refreshDataLabel
            // 
            this.refreshDataLabel.AutoSize = true;
            this.refreshDataLabel.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.refreshDataLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.refreshDataLabel.LinkArea = new System.Windows.Forms.LinkArea(0, 10);
            this.refreshDataLabel.LinkColor = System.Drawing.Color.Blue;
            this.refreshDataLabel.Location = new System.Drawing.Point(100, 145);
            this.refreshDataLabel.Name = "refreshDataLabel";
            this.refreshDataLabel.Size = new System.Drawing.Size(347, 78);
            this.refreshDataLabel.TabIndex = 9;
            this.refreshDataLabel.TabStop = true;
            this.refreshDataLabel.Text = "Click here or press the refresh button\r\nto load statistics based on the current f" +
    "ilters.\r\n\r\nIf you change filters, you have to refresh again.";
            this.refreshDataLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.refreshDataLabel.UseCompatibleTextRendering = true;
            this.refreshDataLabel.VisitedLinkColor = System.Drawing.Color.Blue;
            // 
            // FragmentationStatisticsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(544, 395);
            this.Controls.Add(this.dockPanel);
            this.Controls.Add(this.fragmentToleranceUnitsComboBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.fragmentToleranceTextBox);
            this.Controls.Add(this.refreshButton);
            this.Controls.Add(this.lockZoomCheckBox);
            this.Controls.Add(this.spectrumFiltersTextBox);
            this.Controls.Add(this.exportButton);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas)(((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right) 
            | DigitalRune.Windows.Docking.DockAreas.Top) 
            | DigitalRune.Windows.Docking.DockAreas.Bottom) 
            | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.MinimumSize = new System.Drawing.Size(560, 100);
            this.Name = "FragmentationStatisticsForm";
            this.TabText = "FragmentationStatisticsForm";
            this.Text = "FragmentationStatisticsForm";
            this.exportMenu.ResumeLayout(false);
            this.dockPanel.ResumeLayout(false);
            this.dockPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private System.Windows.Forms.ToolStripMenuItem clipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
        private System.Windows.Forms.TextBox spectrumFiltersTextBox;
        private System.Windows.Forms.CheckBox lockZoomCheckBox;
        private System.Windows.Forms.Button refreshButton;
        public System.Windows.Forms.ComboBox fragmentToleranceUnitsComboBox;
        private System.Windows.Forms.Label label4;
        public System.Windows.Forms.TextBox fragmentToleranceTextBox;
        private System.Windows.Forms.Label label1;
        private DigitalRune.Windows.Docking.DockPanel dockPanel;
        private System.Windows.Forms.LinkLabel refreshDataLabel;

    }
}