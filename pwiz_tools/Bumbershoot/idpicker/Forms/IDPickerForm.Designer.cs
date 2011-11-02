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
            this.dockPanel = new DigitalRune.Windows.Docking.DockPanel();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.breadCrumbPanel = new System.Windows.Forms.Panel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuRoot = new System.Windows.Forms.ToolStripMenuItem();
            this.importToToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportCurrentViewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toHTMLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.layoutToolStripMenuRoot = new System.Windows.Forms.ToolStripMenuItem();
            this.dataFiltersToolStripMenuRoot = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dockPanel
            // 
            this.dockPanel.ActiveAutoHideContent = null;
            this.dockPanel.Anchor = ((System.Windows.Forms.AnchorStyles) ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.dockPanel.DefaultFloatingWindowSize = new System.Drawing.Size(300, 300);
            this.dockPanel.DockLeftPortion = 0.5;
            this.dockPanel.DockRightPortion = 0.5;
            this.dockPanel.DockTopPortion = 0.5;
            this.dockPanel.Location = new System.Drawing.Point(0, 48);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Size = new System.Drawing.Size(584, 404);
            this.dockPanel.TabIndex = 0;
            // 
            // statusStrip
            // 
            this.statusStrip.BackColor = System.Drawing.SystemColors.Control;
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel,
            this.toolStripProgressBar});
            this.statusStrip.Location = new System.Drawing.Point(0, 452);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(584, 22);
            this.statusStrip.TabIndex = 4;
            this.statusStrip.Text = "statusStrip1";
            // 
            // toolStripStatusLabel
            // 
            this.toolStripStatusLabel.Name = "toolStripStatusLabel";
            this.toolStripStatusLabel.Size = new System.Drawing.Size(569, 17);
            this.toolStripStatusLabel.Spring = true;
            this.toolStripStatusLabel.Text = "Ready";
            this.toolStripStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.toolStripStatusLabel.TextDirection = System.Windows.Forms.ToolStripTextDirection.Horizontal;
            // 
            // toolStripProgressBar
            // 
            this.toolStripProgressBar.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripProgressBar.Name = "toolStripProgressBar";
            this.toolStripProgressBar.Size = new System.Drawing.Size(200, 16);
            this.toolStripProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.toolStripProgressBar.Visible = false;
            // 
            // breadCrumbPanel
            // 
            this.breadCrumbPanel.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.breadCrumbPanel.Location = new System.Drawing.Point(0, 22);
            this.breadCrumbPanel.Name = "breadCrumbPanel";
            this.breadCrumbPanel.Size = new System.Drawing.Size(584, 26);
            this.breadCrumbPanel.TabIndex = 5;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuRoot,
            this.toolsToolStripMenuItem,
            this.layoutToolStripMenuRoot,
            this.dataFiltersToolStripMenuRoot});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(584, 24);
            this.menuStrip1.TabIndex = 6;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuRoot
            // 
            this.fileToolStripMenuRoot.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importToToolStripMenuItem,
            this.openToolStripMenuItem,
            this.toolStripSeparator1,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuRoot.Name = "fileToolStripMenuRoot";
            this.fileToolStripMenuRoot.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuRoot.Text = "File";
            // 
            // importToToolStripMenuItem
            // 
            this.importToToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem,
            this.importToolStripMenuItem});
            this.importToToolStripMenuItem.Name = "importToToolStripMenuItem";
            this.importToToolStripMenuItem.Size = new System.Drawing.Size(138, 22);
            this.importToToolStripMenuItem.Text = "Import files";
            // 
            // newToolStripMenuItem
            // 
            this.newToolStripMenuItem.Name = "newToolStripMenuItem";
            this.newToolStripMenuItem.Size = new System.Drawing.Size(170, 22);
            this.newToolStripMenuItem.Text = "to New Session";
            this.newToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // importToolStripMenuItem
            // 
            this.importToolStripMenuItem.Name = "importToolStripMenuItem";
            this.importToolStripMenuItem.Size = new System.Drawing.Size(170, 22);
            this.importToolStripMenuItem.Text = "to Current Session";
            this.importToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(138, 22);
            this.openToolStripMenuItem.Text = "Open idpDB";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(135, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(138, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.optionsToolStripMenuItem,
            this.exportCurrentViewToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(48, 20);
            this.toolsToolStripMenuItem.Text = "Tools";
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
            this.optionsToolStripMenuItem.Text = "Options";
            this.optionsToolStripMenuItem.Click += new System.EventHandler(this.optionsToolStripMenuItem_Click);
            // 
            // exportCurrentViewToolStripMenuItem
            // 
            this.exportCurrentViewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toHTMLToolStripMenuItem,
            this.toExcelToolStripMenuItem});
            this.exportCurrentViewToolStripMenuItem.Name = "exportCurrentViewToolStripMenuItem";
            this.exportCurrentViewToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
            this.exportCurrentViewToolStripMenuItem.Text = "Export Current View";
            // 
            // toHTMLToolStripMenuItem
            // 
            this.toHTMLToolStripMenuItem.Name = "toHTMLToolStripMenuItem";
            this.toHTMLToolStripMenuItem.Size = new System.Drawing.Size(121, 22);
            this.toHTMLToolStripMenuItem.Text = "to HTML";
            this.toHTMLToolStripMenuItem.Click += new System.EventHandler(this.toHTMLToolStripMenuItem_Click);
            // 
            // toExcelToolStripMenuItem
            // 
            this.toExcelToolStripMenuItem.Name = "toExcelToolStripMenuItem";
            this.toExcelToolStripMenuItem.Size = new System.Drawing.Size(121, 22);
            this.toExcelToolStripMenuItem.Text = "to Excel";
            this.toExcelToolStripMenuItem.Click += new System.EventHandler(this.toExcelToolStripMenuItem_Click);
            // 
            // layoutToolStripMenuRoot
            // 
            this.layoutToolStripMenuRoot.Name = "layoutToolStripMenuRoot";
            this.layoutToolStripMenuRoot.Size = new System.Drawing.Size(55, 20);
            this.layoutToolStripMenuRoot.Text = "Layout";
            this.layoutToolStripMenuRoot.DropDownOpening += new System.EventHandler(this.layoutButton_Click);
            // 
            // dataFiltersToolStripMenuRoot
            // 
            this.dataFiltersToolStripMenuRoot.Name = "dataFiltersToolStripMenuRoot";
            this.dataFiltersToolStripMenuRoot.Size = new System.Drawing.Size(77, 20);
            this.dataFiltersToolStripMenuRoot.Text = "Data Filters";
            this.dataFiltersToolStripMenuRoot.Click += new System.EventHandler(this.dataFilterButton_Click);
            // 
            // IDPickerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.ClientSize = new System.Drawing.Size(584, 474);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.dockPanel);
            this.Controls.Add(this.breadCrumbPanel);
            this.Name = "IDPickerForm";
            this.Text = "IDPicker";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.IDPickerForm_FormClosing);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DigitalRune.Windows.Docking.DockPanel dockPanel;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel;
        private System.Windows.Forms.ToolStripProgressBar toolStripProgressBar;
        private System.Windows.Forms.Panel breadCrumbPanel;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuRoot;
        private System.Windows.Forms.ToolStripMenuItem layoutToolStripMenuRoot;
        private System.Windows.Forms.ToolStripMenuItem dataFiltersToolStripMenuRoot;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importToToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportCurrentViewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toHTMLToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toExcelToolStripMenuItem;

    }
}

