namespace pwiz.Topograph.ui.Forms
{
    partial class TopographForm
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
            this.viewIsotopeDistributionGraphToolsStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.halfLivesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorEnrichmentsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resultsPerGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resultsByReplicateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.alignmentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusBarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.isotopeLabelsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutTopographToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debuggingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusBar = new pwiz.Topograph.ui.Controls.StatusBar();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dockPanel
            // 
            this.dockPanel.ActiveAutoHideContent = null;
            this.dockPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dockPanel.Location = new System.Drawing.Point(0, 24);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Size = new System.Drawing.Size(984, 518);
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
            this.fileToolStripMenuItem.DropDownOpening += new System.EventHandler(this.FileToolStripMenuItemOnDropDownOpening);
            // 
            // newWorkspaceToolStripMenuItem
            // 
            this.newWorkspaceToolStripMenuItem.Name = "newWorkspaceToolStripMenuItem";
            this.newWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.newWorkspaceToolStripMenuItem.Text = "New Workspace...";
            this.newWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.NewWorkspaceToolStripMenuItemOnClick);
            // 
            // openWorkspaceToolStripMenuItem
            // 
            this.openWorkspaceToolStripMenuItem.Name = "openWorkspaceToolStripMenuItem";
            this.openWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.openWorkspaceToolStripMenuItem.Text = "Open Workspace...";
            this.openWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.OpenWorkspaceToolStripMenuItemOnClick);
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
            this.newOnlineWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.NewOnlineWorkspaceToolStripMenuItemOnClick);
            // 
            // openOnlineWorkspaceToolStripMenuItem
            // 
            this.openOnlineWorkspaceToolStripMenuItem.Name = "openOnlineWorkspaceToolStripMenuItem";
            this.openOnlineWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.openOnlineWorkspaceToolStripMenuItem.Text = "Connect to Online Workspace...";
            this.openOnlineWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.OpenOnlineWorkspaceToolStripMenuItemOnClick);
            // 
            // outputWorkspaceSQLToolStripMenuItem
            // 
            this.outputWorkspaceSQLToolStripMenuItem.Enabled = false;
            this.outputWorkspaceSQLToolStripMenuItem.Name = "outputWorkspaceSQLToolStripMenuItem";
            this.outputWorkspaceSQLToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.outputWorkspaceSQLToolStripMenuItem.Text = "Export Workspace SQL...";
            this.outputWorkspaceSQLToolStripMenuItem.Click += new System.EventHandler(this.OutputWorkspaceSqlToolStripMenuItemOnClick);
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
            this.saveWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.SaveWorkspaceToolStripMenuItemOnClick);
            // 
            // closeWorkspaceToolStripMenuItem
            // 
            this.closeWorkspaceToolStripMenuItem.Enabled = false;
            this.closeWorkspaceToolStripMenuItem.Name = "closeWorkspaceToolStripMenuItem";
            this.closeWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.closeWorkspaceToolStripMenuItem.Text = "Close Workspace";
            this.closeWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.CloseWorkspaceToolStripMenuItemOnClick);
            // 
            // addSearchResultsToolStripMenuItem
            // 
            this.addSearchResultsToolStripMenuItem.Enabled = false;
            this.addSearchResultsToolStripMenuItem.Name = "addSearchResultsToolStripMenuItem";
            this.addSearchResultsToolStripMenuItem.Size = new System.Drawing.Size(241, 22);
            this.addSearchResultsToolStripMenuItem.Text = "Add Search Results...";
            this.addSearchResultsToolStripMenuItem.Click += new System.EventHandler(this.AddSearchResultsToolStripMenuItemOnClick);
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
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.ExitToolStripMenuItemOnClick);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.dashboardToolStripMenuItem,
            this.peptidesToolStripMenuItem,
            this.dataFilesToolStripMenuItem,
            this.peptideAnalysesToolStripMenuItem,
            this.viewIsotopeDistributionGraphToolsStripMenuItem,
            this.halfLivesToolStripMenuItem,
            this.precursorEnrichmentsToolStripMenuItem,
            this.resultsPerGroupToolStripMenuItem,
            this.resultsByReplicateToolStripMenuItem,
            this.alignmentToolStripMenuItem,
            this.statusBarToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // dashboardToolStripMenuItem
            // 
            this.dashboardToolStripMenuItem.Name = "dashboardToolStripMenuItem";
            this.dashboardToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.dashboardToolStripMenuItem.Text = "Dashboard";
            this.dashboardToolStripMenuItem.Click += new System.EventHandler(this.DashboardToolStripMenuItemOnClick);
            // 
            // peptidesToolStripMenuItem
            // 
            this.peptidesToolStripMenuItem.Enabled = false;
            this.peptidesToolStripMenuItem.Name = "peptidesToolStripMenuItem";
            this.peptidesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.peptidesToolStripMenuItem.Text = "Peptides";
            this.peptidesToolStripMenuItem.Click += new System.EventHandler(this.PeptidesToolStripMenuItemOnClick);
            // 
            // dataFilesToolStripMenuItem
            // 
            this.dataFilesToolStripMenuItem.Enabled = false;
            this.dataFilesToolStripMenuItem.Name = "dataFilesToolStripMenuItem";
            this.dataFilesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.dataFilesToolStripMenuItem.Text = "Data Files";
            this.dataFilesToolStripMenuItem.Click += new System.EventHandler(this.DataFilesToolStripMenuItemOnClick);
            // 
            // peptideAnalysesToolStripMenuItem
            // 
            this.peptideAnalysesToolStripMenuItem.Enabled = false;
            this.peptideAnalysesToolStripMenuItem.Name = "peptideAnalysesToolStripMenuItem";
            this.peptideAnalysesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.peptideAnalysesToolStripMenuItem.Text = "Peptide Analyses";
            this.peptideAnalysesToolStripMenuItem.Click += new System.EventHandler(this.PeptideAnalysesToolStripMenuItemOnClick);
            // 
            // viewIsotopeDistributionGraphToolsStripMenuItem
            // 
            this.viewIsotopeDistributionGraphToolsStripMenuItem.Enabled = false;
            this.viewIsotopeDistributionGraphToolsStripMenuItem.Name = "viewIsotopeDistributionGraphToolsStripMenuItem";
            this.viewIsotopeDistributionGraphToolsStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.viewIsotopeDistributionGraphToolsStripMenuItem.Text = "Isotope Distribution Graph";
            this.viewIsotopeDistributionGraphToolsStripMenuItem.Click += new System.EventHandler(this.MercuryToolStripMenuItemOnClick);
            // 
            // halfLivesToolStripMenuItem
            // 
            this.halfLivesToolStripMenuItem.Enabled = false;
            this.halfLivesToolStripMenuItem.Name = "halfLivesToolStripMenuItem";
            this.halfLivesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.halfLivesToolStripMenuItem.Text = "Half Lives";
            this.halfLivesToolStripMenuItem.Click += new System.EventHandler(this.HalfLivesToolStripMenuItemOnClick);
            // 
            // precursorEnrichmentsToolStripMenuItem
            // 
            this.precursorEnrichmentsToolStripMenuItem.Enabled = false;
            this.precursorEnrichmentsToolStripMenuItem.Name = "precursorEnrichmentsToolStripMenuItem";
            this.precursorEnrichmentsToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.precursorEnrichmentsToolStripMenuItem.Text = "Precursor Enrichments";
            this.precursorEnrichmentsToolStripMenuItem.Click += new System.EventHandler(this.PrecursorEnrichmentsToolStripMenuItemOnClick);
            // 
            // resultsPerGroupToolStripMenuItem
            // 
            this.resultsPerGroupToolStripMenuItem.Enabled = false;
            this.resultsPerGroupToolStripMenuItem.Name = "resultsPerGroupToolStripMenuItem";
            this.resultsPerGroupToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.resultsPerGroupToolStripMenuItem.Text = "Results By Cohort";
            this.resultsPerGroupToolStripMenuItem.Click += new System.EventHandler(this.ResultsPerGroupToolStripMenuItemOnClick);
            // 
            // resultsByReplicateToolStripMenuItem
            // 
            this.resultsByReplicateToolStripMenuItem.Enabled = false;
            this.resultsByReplicateToolStripMenuItem.Name = "resultsByReplicateToolStripMenuItem";
            this.resultsByReplicateToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.resultsByReplicateToolStripMenuItem.Text = "Results By Replicate";
            this.resultsByReplicateToolStripMenuItem.Click += new System.EventHandler(this.ResultsByReplicateToolStripMenuItemOnClick);
            // 
            // alignmentToolStripMenuItem
            // 
            this.alignmentToolStripMenuItem.Enabled = false;
            this.alignmentToolStripMenuItem.Name = "alignmentToolStripMenuItem";
            this.alignmentToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.alignmentToolStripMenuItem.Text = "Alignment";
            this.alignmentToolStripMenuItem.Click += new System.EventHandler(this.AlignmentToolStripMenuItemOnClick);
            // 
            // statusBarToolStripMenuItem
            // 
            this.statusBarToolStripMenuItem.Name = "statusBarToolStripMenuItem";
            this.statusBarToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.statusBarToolStripMenuItem.Text = "Status Bar";
            this.statusBarToolStripMenuItem.Click += new System.EventHandler(this.StatusBarToolStripMenuItemClick);
            // 
            // settingsToolStripMenuItem
            // 
            this.settingsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.isotopeLabelsToolStripMenuItem,
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
            // isotopeLabelsToolStripMenuItem
            // 
            this.isotopeLabelsToolStripMenuItem.Enabled = false;
            this.isotopeLabelsToolStripMenuItem.Name = "isotopeLabelsToolStripMenuItem";
            this.isotopeLabelsToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.isotopeLabelsToolStripMenuItem.Text = "Tracers...";
            this.isotopeLabelsToolStripMenuItem.Click += new System.EventHandler(this.IsotopeLabelsToolStripMenuItemOnClick);
            // 
            // modificationsToolStripMenuItem
            // 
            this.modificationsToolStripMenuItem.Enabled = false;
            this.modificationsToolStripMenuItem.Name = "modificationsToolStripMenuItem";
            this.modificationsToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.modificationsToolStripMenuItem.Text = "Modifications...";
            this.modificationsToolStripMenuItem.Click += new System.EventHandler(this.ModificationsToolStripMenuItemOnClick);
            // 
            // updateProteinNamesToolStripMenuItem
            // 
            this.updateProteinNamesToolStripMenuItem.Enabled = false;
            this.updateProteinNamesToolStripMenuItem.Name = "updateProteinNamesToolStripMenuItem";
            this.updateProteinNamesToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.updateProteinNamesToolStripMenuItem.Text = "Update Protein Names...";
            this.updateProteinNamesToolStripMenuItem.Click += new System.EventHandler(this.UpdateProteinNamesToolStripMenuItemOnClick);
            // 
            // machineSettingsToolStripMenuItem
            // 
            this.machineSettingsToolStripMenuItem.Enabled = false;
            this.machineSettingsToolStripMenuItem.Name = "machineSettingsToolStripMenuItem";
            this.machineSettingsToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.machineSettingsToolStripMenuItem.Text = "Miscellaneous...";
            this.machineSettingsToolStripMenuItem.Click += new System.EventHandler(this.MachineSettingsToolStripMenuItemOnClick);
            // 
            // dataDirectoryToolStripMenuItem
            // 
            this.dataDirectoryToolStripMenuItem.Enabled = false;
            this.dataDirectoryToolStripMenuItem.Name = "dataDirectoryToolStripMenuItem";
            this.dataDirectoryToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.dataDirectoryToolStripMenuItem.Text = "Data Directory...";
            this.dataDirectoryToolStripMenuItem.Click += new System.EventHandler(this.DataDirectoryToolStripMenuItemOnClick);
            // 
            // displayToolStripMenuItem
            // 
            this.displayToolStripMenuItem.Name = "displayToolStripMenuItem";
            this.displayToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.displayToolStripMenuItem.Text = "Display...";
            this.displayToolStripMenuItem.Click += new System.EventHandler(this.DisplayToolStripMenuItemOnClick);
            // 
            // acceptanceCriteriaToolStripMenuItem
            // 
            this.acceptanceCriteriaToolStripMenuItem.Enabled = false;
            this.acceptanceCriteriaToolStripMenuItem.Name = "acceptanceCriteriaToolStripMenuItem";
            this.acceptanceCriteriaToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.acceptanceCriteriaToolStripMenuItem.Text = "Acceptance Criteria...";
            this.acceptanceCriteriaToolStripMenuItem.Click += new System.EventHandler(this.AcceptanceCriteriaToolStripMenuItemOnClick);
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
            this.runningJobsToolStripMenuItem.Click += new System.EventHandler(this.StatusToolStripMenuItemOnClick);
            // 
            // databaseLocksToolStripMenuItem
            // 
            this.databaseLocksToolStripMenuItem.Enabled = false;
            this.databaseLocksToolStripMenuItem.Name = "databaseLocksToolStripMenuItem";
            this.databaseLocksToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.databaseLocksToolStripMenuItem.Text = "Database Locks";
            this.databaseLocksToolStripMenuItem.Click += new System.EventHandler(this.LocksToolStripMenuItemOnClick);
            // 
            // errorsToolStripMenuItem1
            // 
            this.errorsToolStripMenuItem1.Name = "errorsToolStripMenuItem1";
            this.errorsToolStripMenuItem1.Size = new System.Drawing.Size(214, 22);
            this.errorsToolStripMenuItem1.Text = "Errors";
            this.errorsToolStripMenuItem1.Click += new System.EventHandler(this.ErrorsToolStripMenuItemOnClick);
            // 
            // recalculateResultsToolStripMenuItem
            // 
            this.recalculateResultsToolStripMenuItem.Enabled = false;
            this.recalculateResultsToolStripMenuItem.Name = "recalculateResultsToolStripMenuItem";
            this.recalculateResultsToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.recalculateResultsToolStripMenuItem.Text = "Recalculate Results...";
            this.recalculateResultsToolStripMenuItem.Click += new System.EventHandler(this.RecalculateResultsToolStripMenuItemOnClick);
            // 
            // databaseSizeToolStripMenuItem
            // 
            this.databaseSizeToolStripMenuItem.Enabled = false;
            this.databaseSizeToolStripMenuItem.Name = "databaseSizeToolStripMenuItem";
            this.databaseSizeToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.databaseSizeToolStripMenuItem.Text = "Database Size...";
            this.databaseSizeToolStripMenuItem.Click += new System.EventHandler(this.DatabaseSizeToolStripMenuItemOnClick);
            // 
            // precursorPoolSimulatorToolStripMenuItem
            // 
            this.precursorPoolSimulatorToolStripMenuItem.Enabled = false;
            this.precursorPoolSimulatorToolStripMenuItem.Name = "precursorPoolSimulatorToolStripMenuItem";
            this.precursorPoolSimulatorToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.precursorPoolSimulatorToolStripMenuItem.Text = "Precursor Pool Simulator...";
            this.precursorPoolSimulatorToolStripMenuItem.Click += new System.EventHandler(this.PrecursorPoolSimulatorToolStripMenuItemOnClick);
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
            this.aboutTopographToolStripMenuItem.Click += new System.EventHandler(this.AboutTopographToolStripMenuItemOnClick);
            // 
            // debuggingToolStripMenuItem
            // 
            this.debuggingToolStripMenuItem.Name = "debuggingToolStripMenuItem";
            this.debuggingToolStripMenuItem.Size = new System.Drawing.Size(32, 19);
            this.debuggingToolStripMenuItem.Text = "Debugging";
            // 
            // statusBar
            // 
            this.statusBar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.statusBar.Location = new System.Drawing.Point(0, 542);
            this.statusBar.Name = "statusBar";
            this.statusBar.Size = new System.Drawing.Size(984, 22);
            this.statusBar.TabIndex = 8;
            // 
            // TopographForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 564);
            this.Controls.Add(this.dockPanel);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.statusBar);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "TopographForm";
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
        private System.Windows.Forms.ToolStripMenuItem isotopeLabelsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptidesToolStripMenuItem;
        private DigitalRune.Windows.Docking.DockPanel dockPanel;
        private System.Windows.Forms.ToolStripMenuItem dataFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideAnalysesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem modificationsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveWorkspaceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updateProteinNamesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem machineSettingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewIsotopeDistributionGraphToolsStripMenuItem;
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
        private System.Windows.Forms.ToolStripMenuItem statusBarToolStripMenuItem;
        private Controls.StatusBar statusBar;
    }
}