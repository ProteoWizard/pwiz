namespace pwiz.Topograph.ui.Forms
{
    partial class TurnoverForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TurnoverForm));
            this.dockPanel = new DigitalRune.Windows.Docking.DockPanel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newWorkspaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openWorkspaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.newOnlineWorkspaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openOnlineWorkspaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.outputWorkspaceSQLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.saveWorkspaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.closeWorkspaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addSearchResultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mruBeforeToolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.mruAfterToolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dashboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptidesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dataFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideAnalysesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.queryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mercuryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.halfLivesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorEnrichmentsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resultsPerGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resultsByReplicateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.alignmentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.enrichmentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.modificationsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.updateProteinNamesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.machineSettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dataDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.displayToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.acceptanceCriteriaToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debuggingToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.runningJobsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.databaseLocksToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.errorsToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.recalculateResultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.databaseSizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorPoolSimulatorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debuggingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutTopographToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dockPanel
            // 
            this.dockPanel.ActiveAutoHideContent = null;
            this.dockPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dockPanel.Location = new System.Drawing.Point(0, 24);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Size = new System.Drawing.Size(984, 540);
            this.dockPanel.TabIndex = 0;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.settingsToolStripMenuItem,
            this.debuggingToolStripMenuItem1,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(984, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newWorkspaceToolStripMenuItem,
            this.openWorkspaceToolStripMenuItem,
            this.toolStripSeparator2,
            this.newOnlineWorkspaceToolStripMenuItem,
            this.openOnlineWorkspaceToolStripMenuItem,
            this.outputWorkspaceSQLToolStripMenuItem,
            this.toolStripSeparator3,
            this.saveWorkspaceToolStripMenuItem,
            this.closeWorkspaceToolStripMenuItem,
            this.addSearchResultsToolStripMenuItem,
            this.mruBeforeToolStripSeparator,
            this.mruAfterToolStripSeparator,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            this.fileToolStripMenuItem.DropDownOpening += new System.EventHandler(this.fileToolStripMenuItem_DropDownOpening);
            // 
            // newWorkspaceToolStripMenuItem
            // 
            this.newWorkspaceToolStripMenuItem.Name = "newWorkspaceToolStripMenuItem";
            this.newWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.newWorkspaceToolStripMenuItem.Text = "New Workspace...";
            this.newWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.newWorkspaceToolStripMenuItem_Click);
            // 
            // openWorkspaceToolStripMenuItem
            // 
            this.openWorkspaceToolStripMenuItem.Name = "openWorkspaceToolStripMenuItem";
            this.openWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.openWorkspaceToolStripMenuItem.Text = "Open Workspace...";
            this.openWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.openWorkspaceToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(238, 6);
            // 
            // newOnlineWorkspaceToolStripMenuItem
            // 
            this.newOnlineWorkspaceToolStripMenuItem.Name = "newOnlineWorkspaceToolStripMenuItem";
            this.newOnlineWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.newOnlineWorkspaceToolStripMenuItem.Text = "New Online Workspace...";
            this.newOnlineWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.newOnlineWorkspaceToolStripMenuItem_Click);
            // 
            // openOnlineWorkspaceToolStripMenuItem
            // 
            this.openOnlineWorkspaceToolStripMenuItem.Name = "openOnlineWorkspaceToolStripMenuItem";
            this.openOnlineWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.openOnlineWorkspaceToolStripMenuItem.Text = "Connect to Online Workspace...";
            this.openOnlineWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.openOnlineWorkspaceToolStripMenuItem_Click);
            // 
            // outputWorkspaceSQLToolStripMenuItem
            // 
            this.outputWorkspaceSQLToolStripMenuItem.Enabled = false;
            this.outputWorkspaceSQLToolStripMenuItem.Name = "outputWorkspaceSQLToolStripMenuItem";
            this.outputWorkspaceSQLToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.outputWorkspaceSQLToolStripMenuItem.Text = "Export Workspace SQL...";
            this.outputWorkspaceSQLToolStripMenuItem.Click += new System.EventHandler(this.outputWorkspaceSQLToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(238, 6);
            // 
            // saveWorkspaceToolStripMenuItem
            // 
            this.saveWorkspaceToolStripMenuItem.Enabled = false;
            this.saveWorkspaceToolStripMenuItem.Name = "saveWorkspaceToolStripMenuItem";
            this.saveWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.saveWorkspaceToolStripMenuItem.Text = "Save Workspace";
            this.saveWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.saveWorkspaceToolStripMenuItem_Click);
            // 
            // closeWorkspaceToolStripMenuItem
            // 
            this.closeWorkspaceToolStripMenuItem.Enabled = false;
            this.closeWorkspaceToolStripMenuItem.Name = "closeWorkspaceToolStripMenuItem";
            this.closeWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.closeWorkspaceToolStripMenuItem.Text = "Close Workspace";
            this.closeWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.closeWorkspaceToolStripMenuItem_Click);
            // 
            // addSearchResultsToolStripMenuItem
            // 
            this.addSearchResultsToolStripMenuItem.Enabled = false;
            this.addSearchResultsToolStripMenuItem.Name = "addSearchResultsToolStripMenuItem";
            this.addSearchResultsToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.addSearchResultsToolStripMenuItem.Text = "Add Search Results...";
            this.addSearchResultsToolStripMenuItem.Click += new System.EventHandler(this.addSearchResultsToolStripMenuItem_Click);
            // 
            // mruBeforeToolStripSeparator
            // 
            this.mruBeforeToolStripSeparator.Name = "mruBeforeToolStripSeparator";
            this.mruBeforeToolStripSeparator.Size = new System.Drawing.Size(238, 6);
            // 
            // mruAfterToolStripSeparator
            // 
            this.mruAfterToolStripSeparator.Name = "mruAfterToolStripSeparator";
            this.mruAfterToolStripSeparator.Size = new System.Drawing.Size(238, 6);
            this.mruAfterToolStripSeparator.Visible = false;
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.dashboardToolStripMenuItem,
            this.peptidesToolStripMenuItem,
            this.dataFilesToolStripMenuItem,
            this.peptideAnalysesToolStripMenuItem,
            this.queryToolStripMenuItem,
            this.mercuryToolStripMenuItem,
            this.halfLivesToolStripMenuItem,
            this.precursorEnrichmentsToolStripMenuItem,
            this.resultsPerGroupToolStripMenuItem,
            this.resultsByReplicateToolStripMenuItem,
            this.alignmentToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // dashboardToolStripMenuItem
            // 
            this.dashboardToolStripMenuItem.Name = "dashboardToolStripMenuItem";
            this.dashboardToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.dashboardToolStripMenuItem.Text = "Dashboard";
            this.dashboardToolStripMenuItem.Click += new System.EventHandler(this.dashboardToolStripMenuItem_Click);
            // 
            // peptidesToolStripMenuItem
            // 
            this.peptidesToolStripMenuItem.Enabled = false;
            this.peptidesToolStripMenuItem.Name = "peptidesToolStripMenuItem";
            this.peptidesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.peptidesToolStripMenuItem.Text = "Peptides";
            this.peptidesToolStripMenuItem.Click += new System.EventHandler(this.peptidesToolStripMenuItem_Click);
            // 
            // dataFilesToolStripMenuItem
            // 
            this.dataFilesToolStripMenuItem.Enabled = false;
            this.dataFilesToolStripMenuItem.Name = "dataFilesToolStripMenuItem";
            this.dataFilesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.dataFilesToolStripMenuItem.Text = "Data Files";
            this.dataFilesToolStripMenuItem.Click += new System.EventHandler(this.dataFilesToolStripMenuItem_Click);
            // 
            // peptideAnalysesToolStripMenuItem
            // 
            this.peptideAnalysesToolStripMenuItem.Enabled = false;
            this.peptideAnalysesToolStripMenuItem.Name = "peptideAnalysesToolStripMenuItem";
            this.peptideAnalysesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.peptideAnalysesToolStripMenuItem.Text = "Peptide Analyses";
            this.peptideAnalysesToolStripMenuItem.Click += new System.EventHandler(this.peptideAnalysesToolStripMenuItem_Click);
            // 
            // queryToolStripMenuItem
            // 
            this.queryToolStripMenuItem.Enabled = false;
            this.queryToolStripMenuItem.Name = "queryToolStripMenuItem";
            this.queryToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.queryToolStripMenuItem.Text = "Queries";
            this.queryToolStripMenuItem.Click += new System.EventHandler(this.queriesToolStripMenuItem_Click);
            // 
            // mercuryToolStripMenuItem
            // 
            this.mercuryToolStripMenuItem.Enabled = false;
            this.mercuryToolStripMenuItem.Name = "mercuryToolStripMenuItem";
            this.mercuryToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.mercuryToolStripMenuItem.Text = "Isotope Distribution Graph";
            this.mercuryToolStripMenuItem.Click += new System.EventHandler(this.mercuryToolStripMenuItem_Click);
            // 
            // halfLivesToolStripMenuItem
            // 
            this.halfLivesToolStripMenuItem.Enabled = false;
            this.halfLivesToolStripMenuItem.Name = "halfLivesToolStripMenuItem";
            this.halfLivesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.halfLivesToolStripMenuItem.Text = "Half Lives";
            this.halfLivesToolStripMenuItem.Click += new System.EventHandler(this.halfLivesToolStripMenuItem_Click);
            // 
            // precursorEnrichmentsToolStripMenuItem
            // 
            this.precursorEnrichmentsToolStripMenuItem.Enabled = false;
            this.precursorEnrichmentsToolStripMenuItem.Name = "precursorEnrichmentsToolStripMenuItem";
            this.precursorEnrichmentsToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.precursorEnrichmentsToolStripMenuItem.Text = "Precursor Enrichments";
            this.precursorEnrichmentsToolStripMenuItem.Click += new System.EventHandler(this.precursorEnrichmentsToolStripMenuItem_Click);
            // 
            // resultsPerGroupToolStripMenuItem
            // 
            this.resultsPerGroupToolStripMenuItem.Enabled = false;
            this.resultsPerGroupToolStripMenuItem.Name = "resultsPerGroupToolStripMenuItem";
            this.resultsPerGroupToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.resultsPerGroupToolStripMenuItem.Text = "Results By Cohort";
            this.resultsPerGroupToolStripMenuItem.Click += new System.EventHandler(this.resultsPerGroupToolStripMenuItem_Click);
            // 
            // resultsByReplicateToolStripMenuItem
            // 
            this.resultsByReplicateToolStripMenuItem.Enabled = false;
            this.resultsByReplicateToolStripMenuItem.Name = "resultsByReplicateToolStripMenuItem";
            this.resultsByReplicateToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.resultsByReplicateToolStripMenuItem.Text = "Results By Replicate";
            this.resultsByReplicateToolStripMenuItem.Click += new System.EventHandler(this.resultsByReplicateToolStripMenuItem_Click);
            // 
            // alignmentToolStripMenuItem
            // 
            this.alignmentToolStripMenuItem.Enabled = false;
            this.alignmentToolStripMenuItem.Name = "alignmentToolStripMenuItem";
            this.alignmentToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.alignmentToolStripMenuItem.Text = "Alignment";
            this.alignmentToolStripMenuItem.Click += new System.EventHandler(this.alignmentToolStripMenuItem_Click);
            // 
            // settingsToolStripMenuItem
            // 
            this.settingsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.enrichmentToolStripMenuItem,
            this.modificationsToolStripMenuItem,
            this.updateProteinNamesToolStripMenuItem,
            this.machineSettingsToolStripMenuItem,
            this.dataDirectoryToolStripMenuItem,
            this.displayToolStripMenuItem,
            this.acceptanceCriteriaToolStripMenuItem});
            this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            this.settingsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.settingsToolStripMenuItem.Text = "Settings";
            // 
            // enrichmentToolStripMenuItem
            // 
            this.enrichmentToolStripMenuItem.Enabled = false;
            this.enrichmentToolStripMenuItem.Name = "enrichmentToolStripMenuItem";
            this.enrichmentToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.enrichmentToolStripMenuItem.Text = "Tracers...";
            this.enrichmentToolStripMenuItem.Click += new System.EventHandler(this.enrichmentToolStripMenuItem_Click);
            // 
            // modificationsToolStripMenuItem
            // 
            this.modificationsToolStripMenuItem.Enabled = false;
            this.modificationsToolStripMenuItem.Name = "modificationsToolStripMenuItem";
            this.modificationsToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.modificationsToolStripMenuItem.Text = "Modifications...";
            this.modificationsToolStripMenuItem.Click += new System.EventHandler(this.modificationsToolStripMenuItem_Click);
            // 
            // updateProteinNamesToolStripMenuItem
            // 
            this.updateProteinNamesToolStripMenuItem.Enabled = false;
            this.updateProteinNamesToolStripMenuItem.Name = "updateProteinNamesToolStripMenuItem";
            this.updateProteinNamesToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.updateProteinNamesToolStripMenuItem.Text = "Update Protein Names...";
            this.updateProteinNamesToolStripMenuItem.Click += new System.EventHandler(this.updateProteinNamesToolStripMenuItem_Click);
            // 
            // machineSettingsToolStripMenuItem
            // 
            this.machineSettingsToolStripMenuItem.Enabled = false;
            this.machineSettingsToolStripMenuItem.Name = "machineSettingsToolStripMenuItem";
            this.machineSettingsToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.machineSettingsToolStripMenuItem.Text = "Miscellaneous...";
            this.machineSettingsToolStripMenuItem.Click += new System.EventHandler(this.machineSettingsToolStripMenuItem_Click);
            // 
            // dataDirectoryToolStripMenuItem
            // 
            this.dataDirectoryToolStripMenuItem.Enabled = false;
            this.dataDirectoryToolStripMenuItem.Name = "dataDirectoryToolStripMenuItem";
            this.dataDirectoryToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.dataDirectoryToolStripMenuItem.Text = "Data Directory...";
            this.dataDirectoryToolStripMenuItem.Click += new System.EventHandler(this.dataDirectoryToolStripMenuItem_Click);
            // 
            // displayToolStripMenuItem
            // 
            this.displayToolStripMenuItem.Name = "displayToolStripMenuItem";
            this.displayToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.displayToolStripMenuItem.Text = "Display...";
            this.displayToolStripMenuItem.Click += new System.EventHandler(this.displayToolStripMenuItem_Click);
            // 
            // acceptanceCriteriaToolStripMenuItem
            // 
            this.acceptanceCriteriaToolStripMenuItem.Enabled = false;
            this.acceptanceCriteriaToolStripMenuItem.Name = "acceptanceCriteriaToolStripMenuItem";
            this.acceptanceCriteriaToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.acceptanceCriteriaToolStripMenuItem.Text = "Acceptance Criteria...";
            this.acceptanceCriteriaToolStripMenuItem.Click += new System.EventHandler(this.acceptanceCriteriaToolStripMenuItem_Click);
            // 
            // debuggingToolStripMenuItem1
            // 
            this.debuggingToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.runningJobsToolStripMenuItem,
            this.databaseLocksToolStripMenuItem,
            this.errorsToolStripMenuItem1,
            this.recalculateResultsToolStripMenuItem,
            this.databaseSizeToolStripMenuItem,
            this.precursorPoolSimulatorToolStripMenuItem});
            this.debuggingToolStripMenuItem1.Name = "debuggingToolStripMenuItem1";
            this.debuggingToolStripMenuItem1.Size = new System.Drawing.Size(107, 20);
            this.debuggingToolStripMenuItem1.Text = "Troubleshooting";
            // 
            // runningJobsToolStripMenuItem
            // 
            this.runningJobsToolStripMenuItem.Enabled = false;
            this.runningJobsToolStripMenuItem.Name = "runningJobsToolStripMenuItem";
            this.runningJobsToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.runningJobsToolStripMenuItem.Text = "Running Jobs";
            this.runningJobsToolStripMenuItem.Click += new System.EventHandler(this.statusToolStripMenuItem_Click);
            // 
            // databaseLocksToolStripMenuItem
            // 
            this.databaseLocksToolStripMenuItem.Enabled = false;
            this.databaseLocksToolStripMenuItem.Name = "databaseLocksToolStripMenuItem";
            this.databaseLocksToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.databaseLocksToolStripMenuItem.Text = "Database Locks";
            this.databaseLocksToolStripMenuItem.Click += new System.EventHandler(this.locksToolStripMenuItem_Click);
            // 
            // errorsToolStripMenuItem1
            // 
            this.errorsToolStripMenuItem1.Name = "errorsToolStripMenuItem1";
            this.errorsToolStripMenuItem1.Size = new System.Drawing.Size(214, 22);
            this.errorsToolStripMenuItem1.Text = "Errors";
            this.errorsToolStripMenuItem1.Click += new System.EventHandler(this.errorsToolStripMenuItem_Click);
            // 
            // recalculateResultsToolStripMenuItem
            // 
            this.recalculateResultsToolStripMenuItem.Enabled = false;
            this.recalculateResultsToolStripMenuItem.Name = "recalculateResultsToolStripMenuItem";
            this.recalculateResultsToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.recalculateResultsToolStripMenuItem.Text = "Recalculate Results...";
            this.recalculateResultsToolStripMenuItem.Click += new System.EventHandler(this.recalculateResultsToolStripMenuItem_Click);
            // 
            // databaseSizeToolStripMenuItem
            // 
            this.databaseSizeToolStripMenuItem.Enabled = false;
            this.databaseSizeToolStripMenuItem.Name = "databaseSizeToolStripMenuItem";
            this.databaseSizeToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.databaseSizeToolStripMenuItem.Text = "Database Size...";
            this.databaseSizeToolStripMenuItem.Click += new System.EventHandler(this.databaseSizeToolStripMenuItem_Click);
            // 
            // precursorPoolSimulatorToolStripMenuItem
            // 
            this.precursorPoolSimulatorToolStripMenuItem.Enabled = false;
            this.precursorPoolSimulatorToolStripMenuItem.Name = "precursorPoolSimulatorToolStripMenuItem";
            this.precursorPoolSimulatorToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.precursorPoolSimulatorToolStripMenuItem.Text = "Precursor Pool Simulator...";
            this.precursorPoolSimulatorToolStripMenuItem.Click += new System.EventHandler(this.precursorPoolSimulatorToolStripMenuItem_Click);
            // 
            // debuggingToolStripMenuItem
            // 
            this.debuggingToolStripMenuItem.Name = "debuggingToolStripMenuItem";
            this.debuggingToolStripMenuItem.Size = new System.Drawing.Size(32, 19);
            this.debuggingToolStripMenuItem.Text = "Debugging";
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutTopographToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // aboutTopographToolStripMenuItem
            // 
            this.aboutTopographToolStripMenuItem.Name = "aboutTopographToolStripMenuItem";
            this.aboutTopographToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
            this.aboutTopographToolStripMenuItem.Text = "About Topograph...";
            this.aboutTopographToolStripMenuItem.Click += new System.EventHandler(this.aboutTopographToolStripMenuItem_Click);
            // 
            // TurnoverForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 564);
            this.Controls.Add(this.dockPanel);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "TurnoverForm";
            this.Text = "Topograph";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newWorkspaceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openWorkspaceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem closeWorkspaceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSearchResultsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem enrichmentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptidesToolStripMenuItem;
        private DigitalRune.Windows.Docking.DockPanel dockPanel;
        private System.Windows.Forms.ToolStripMenuItem dataFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideAnalysesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem modificationsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveWorkspaceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updateProteinNamesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem queryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem machineSettingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mercuryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem halfLivesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem newOnlineWorkspaceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openOnlineWorkspaceToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem dataDirectoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem outputWorkspaceSQLToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem debuggingToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem runningJobsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem debuggingToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem databaseLocksToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem errorsToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem resultsPerGroupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem precursorEnrichmentsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recalculateResultsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem displayToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator mruBeforeToolStripSeparator;
        private System.Windows.Forms.ToolStripSeparator mruAfterToolStripSeparator;
        private System.Windows.Forms.ToolStripMenuItem resultsByReplicateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem alignmentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem databaseSizeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem acceptanceCriteriaToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem precursorPoolSimulatorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem dashboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutTopographToolStripMenuItem;
    }
}