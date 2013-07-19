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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(IDPickerForm));
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
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.exportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportAllViewsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toHTMLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toExcelSelectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.subsetFASTAToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toQuasitelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.spectralLibraryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.embedSpectraToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadGeneMetadataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.layoutToolStripMenuRoot = new System.Windows.Forms.ToolStripMenuItem();
            this.dataFiltersToolStripMenuRoot = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tutorialsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.glossaryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dataImportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.proteinViewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.commandLineHelpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.checkForUpdatesAutomaticallyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.checkForUpdatesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.developerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLogToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dockPanel = new DigitalRune.Windows.Docking.DockPanel();
            this.statusStrip.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
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
            this.breadCrumbPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.breadCrumbPanel.Location = new System.Drawing.Point(0, 22);
            this.breadCrumbPanel.Name = "breadCrumbPanel";
            this.breadCrumbPanel.Size = new System.Drawing.Size(584, 26);
            this.breadCrumbPanel.TabIndex = 5;
            // 
            // menuStrip1
            // 
            this.menuStrip1.BackColor = System.Drawing.SystemColors.MenuBar;
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuRoot,
            this.toolsToolStripMenuItem,
            this.layoutToolStripMenuRoot,
            this.dataFiltersToolStripMenuRoot,
            this.helpToolStripMenuItem,
            this.developerToolStripMenuItem});
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
            this.toolStripSeparator4,
            this.exportToolStripMenuItem,
            this.embedSpectraToolStripMenuItem,
            this.toolStripSeparator1,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuRoot.Name = "fileToolStripMenuRoot";
            this.fileToolStripMenuRoot.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuRoot.Text = "File";
            this.fileToolStripMenuRoot.DropDownOpening += new System.EventHandler(this.fileToolStripMenuRoot_DropDownOpening);
            // 
            // importToToolStripMenuItem
            // 
            this.importToToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem,
            this.importToolStripMenuItem});
            this.importToToolStripMenuItem.Name = "importToToolStripMenuItem";
            this.importToToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
            this.importToToolStripMenuItem.Text = "Import files";
            this.importToToolStripMenuItem.Click += new System.EventHandler(this.importToToolStripMenuItem_Click);
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
            this.openToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
            this.openToolStripMenuItem.Text = "Open idpDB...";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(158, 6);
            // 
            // exportToolStripMenuItem
            // 
            this.exportToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportAllViewsToolStripMenuItem,
            this.subsetFASTAToolStripMenuItem,
            this.toQuasitelToolStripMenuItem,
            this.spectralLibraryToolStripMenuItem});
            this.exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            this.exportToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
            this.exportToolStripMenuItem.Text = "Export";
            // 
            // exportAllViewsToolStripMenuItem
            // 
            this.exportAllViewsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toHTMLToolStripMenuItem,
            this.toExcelToolStripMenuItem,
            this.toExcelSelectToolStripMenuItem});
            this.exportAllViewsToolStripMenuItem.Name = "exportAllViewsToolStripMenuItem";
            this.exportAllViewsToolStripMenuItem.Size = new System.Drawing.Size(156, 22);
            this.exportAllViewsToolStripMenuItem.Text = "All Views";
            // 
            // toHTMLToolStripMenuItem
            // 
            this.toHTMLToolStripMenuItem.Name = "toHTMLToolStripMenuItem";
            this.toHTMLToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.toHTMLToolStripMenuItem.Text = "to HTML";
            this.toHTMLToolStripMenuItem.Click += new System.EventHandler(this.toHTMLToolStripMenuItem_Click);
            // 
            // toExcelToolStripMenuItem
            // 
            this.toExcelToolStripMenuItem.Name = "toExcelToolStripMenuItem";
            this.toExcelToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.toExcelToolStripMenuItem.Text = "to Excel (all cells)";
            this.toExcelToolStripMenuItem.Click += new System.EventHandler(this.toExcelToolStripMenuItem_Click);
            // 
            // toExcelSelectToolStripMenuItem
            // 
            this.toExcelSelectToolStripMenuItem.Name = "toExcelSelectToolStripMenuItem";
            this.toExcelSelectToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.toExcelSelectToolStripMenuItem.Text = "to Excel (selected cells)";
            this.toExcelSelectToolStripMenuItem.Click += new System.EventHandler(this.toExcelToolStripMenuItem_Click);
            // 
            // subsetFASTAToolStripMenuItem
            // 
            this.subsetFASTAToolStripMenuItem.Name = "subsetFASTAToolStripMenuItem";
            this.subsetFASTAToolStripMenuItem.Size = new System.Drawing.Size(156, 22);
            this.subsetFASTAToolStripMenuItem.Text = "Subset FASTA...";
            this.subsetFASTAToolStripMenuItem.Click += new System.EventHandler(this.exportSubsetFASTAToolStripMenuItem_Click);
            // 
            // toQuasitelToolStripMenuItem
            // 
            this.toQuasitelToolStripMenuItem.Name = "toQuasitelToolStripMenuItem";
            this.toQuasitelToolStripMenuItem.Size = new System.Drawing.Size(156, 22);
            this.toQuasitelToolStripMenuItem.Text = "To Quasitel...";
            this.toQuasitelToolStripMenuItem.Click += new System.EventHandler(this.toQuasitelToolStripMenuItem_Click);
            // 
            // spectralLibraryToolStripMenuItem
            // 
            this.spectralLibraryToolStripMenuItem.Name = "spectralLibraryToolStripMenuItem";
            this.spectralLibraryToolStripMenuItem.Size = new System.Drawing.Size(156, 22);
            this.spectralLibraryToolStripMenuItem.Text = "Spectral Library";
            this.spectralLibraryToolStripMenuItem.Click += new System.EventHandler(this.spectralLibraryToolStripMenuItem_Click);
            // 
            // embedSpectraToolStripMenuItem
            // 
            this.embedSpectraToolStripMenuItem.Name = "embedSpectraToolStripMenuItem";
            this.embedSpectraToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
            this.embedSpectraToolStripMenuItem.Text = "Embed spectra...";
            this.embedSpectraToolStripMenuItem.Click += new System.EventHandler(this.embedSpectraToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(158, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.optionsToolStripMenuItem,
            this.loadGeneMetadataToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(48, 20);
            this.toolsToolStripMenuItem.Text = "Tools";
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(183, 22);
            this.optionsToolStripMenuItem.Text = "Options...";
            this.optionsToolStripMenuItem.Click += new System.EventHandler(this.optionsToolStripMenuItem_Click);
            // 
            // loadGeneMetadataToolStripMenuItem
            // 
            this.loadGeneMetadataToolStripMenuItem.Name = "loadGeneMetadataToolStripMenuItem";
            this.loadGeneMetadataToolStripMenuItem.Size = new System.Drawing.Size(183, 22);
            this.loadGeneMetadataToolStripMenuItem.Text = "Load Gene Metadata";
            this.loadGeneMetadataToolStripMenuItem.Click += new System.EventHandler(this.loadGeneMetadataToolStripMenuItem_Click);
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
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tutorialsToolStripMenuItem,
            this.commandLineHelpMenuItem,
            this.toolStripSeparator2,
            this.checkForUpdatesAutomaticallyToolStripMenuItem,
            this.checkForUpdatesToolStripMenuItem,
            this.toolStripSeparator3,
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // tutorialsToolStripMenuItem
            // 
            this.tutorialsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.glossaryToolStripMenuItem,
            this.dataImportToolStripMenuItem,
            this.proteinViewToolStripMenuItem});
            this.tutorialsToolStripMenuItem.Name = "tutorialsToolStripMenuItem";
            this.tutorialsToolStripMenuItem.Size = new System.Drawing.Size(248, 22);
            this.tutorialsToolStripMenuItem.Text = "Tutorials";
            // 
            // glossaryToolStripMenuItem
            // 
            this.glossaryToolStripMenuItem.Name = "glossaryToolStripMenuItem";
            this.glossaryToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
            this.glossaryToolStripMenuItem.Text = "Glossary";
            this.glossaryToolStripMenuItem.Click += new System.EventHandler(this.glossaryToolStripMenuItem_Click);
            // 
            // dataImportToolStripMenuItem
            // 
            this.dataImportToolStripMenuItem.Name = "dataImportToolStripMenuItem";
            this.dataImportToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
            this.dataImportToolStripMenuItem.Text = "Data Import";
            this.dataImportToolStripMenuItem.Click += new System.EventHandler(this.dataImportToolStripMenuItem_Click);
            // 
            // proteinViewToolStripMenuItem
            // 
            this.proteinViewToolStripMenuItem.Name = "proteinViewToolStripMenuItem";
            this.proteinViewToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
            this.proteinViewToolStripMenuItem.Text = "Protein View";
            this.proteinViewToolStripMenuItem.Click += new System.EventHandler(this.proteinViewToolStripMenuItem_Click);
            // 
            // commandLineHelpMenuItem
            // 
            this.commandLineHelpMenuItem.Name = "commandLineHelpMenuItem";
            this.commandLineHelpMenuItem.Size = new System.Drawing.Size(248, 22);
            this.commandLineHelpMenuItem.Text = "Command-line Help";
            this.commandLineHelpMenuItem.Click += new System.EventHandler(this.showCommandLineHelp);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(245, 6);
            // 
            // checkForUpdatesAutomaticallyToolStripMenuItem
            // 
            this.checkForUpdatesAutomaticallyToolStripMenuItem.CheckOnClick = true;
            this.checkForUpdatesAutomaticallyToolStripMenuItem.Name = "checkForUpdatesAutomaticallyToolStripMenuItem";
            this.checkForUpdatesAutomaticallyToolStripMenuItem.Size = new System.Drawing.Size(248, 22);
            this.checkForUpdatesAutomaticallyToolStripMenuItem.Text = "Check for Updates Automatically";
            this.checkForUpdatesAutomaticallyToolStripMenuItem.CheckedChanged += new System.EventHandler(this.checkForUpdatesAutomaticallyToolStripMenuItem_CheckedChanged);
            // 
            // checkForUpdatesToolStripMenuItem
            // 
            this.checkForUpdatesToolStripMenuItem.Name = "checkForUpdatesToolStripMenuItem";
            this.checkForUpdatesToolStripMenuItem.Size = new System.Drawing.Size(248, 22);
            this.checkForUpdatesToolStripMenuItem.Text = "Check for Updates Now";
            this.checkForUpdatesToolStripMenuItem.Click += new System.EventHandler(this.checkForUpdatesToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(245, 6);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(248, 22);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // developerToolStripMenuItem
            // 
            this.developerToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showLogToolStripMenuItem});
            this.developerToolStripMenuItem.Name = "developerToolStripMenuItem";
            this.developerToolStripMenuItem.Size = new System.Drawing.Size(72, 20);
            this.developerToolStripMenuItem.Text = "Developer";
            // 
            // showLogToolStripMenuItem
            // 
            this.showLogToolStripMenuItem.Name = "showLogToolStripMenuItem";
            this.showLogToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.showLogToolStripMenuItem.Text = "Show Log";
            this.showLogToolStripMenuItem.Click += new System.EventHandler(this.showLogToolStripMenuItem_Click);
            // 
            // dockPanel
            // 
            this.dockPanel.ActiveAutoHideContent = null;
            this.dockPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dockPanel.DockLeftPortion = 0.5D;
            this.dockPanel.DockRightPortion = 0.5D;
            this.dockPanel.DockTopPortion = 0.5D;
            this.dockPanel.Location = new System.Drawing.Point(0, 48);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Size = new System.Drawing.Size(584, 404);
            this.dockPanel.TabIndex = 0;
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
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
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
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem checkForUpdatesAutomaticallyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem checkForUpdatesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem embedSpectraToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem exportAllViewsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toHTMLToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toExcelToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toExcelSelectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem subsetFASTAToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toQuasitelToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem commandLineHelpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tutorialsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem glossaryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem dataImportToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem proteinViewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem developerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLogToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem spectralLibraryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadGeneMetadataToolStripMenuItem;

    }
}

