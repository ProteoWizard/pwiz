
using System;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline
{
    partial class SkylineWindow
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SkylineWindow));
            this.selectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.synchronizeSummaryZoomingContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicatesRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.averageReplicatesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleReplicateRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bestReplicateRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator24 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator25 = new System.Windows.Forms.ToolStripSeparator();
            this.peptideOrderContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderDocumentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderAreaContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderMassErrorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderDocumentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderAcqTimeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentScopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.proteinScopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideCvsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator28 = new System.Windows.Forms.ToolStripSeparator();
            this.groupReplicatesByContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupByReplicateContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel1 = new System.Windows.Forms.Panel();
            this.dockPanel = new DigitalRune.Windows.Docking.DockPanel();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusGeneral = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusProgress = new System.Windows.Forms.ToolStripProgressBar();
            this.buttonShowAllChromatograms = new System.Windows.Forms.ToolStripSplitButton();
            this.statusSequences = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusPeptides = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusPrecursors = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusIons = new System.Windows.Forms.ToolStripStatusLabel();
            this.mainToolStrip = new System.Windows.Forms.ToolStrip();
            this.newToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.openToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.saveToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.publishToolbarButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator20 = new System.Windows.Forms.ToolStripSeparator();
            this.cutToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.copyToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.pasteToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator21 = new System.Windows.Forms.ToolStripSeparator();
            this.undoToolBarButton = new System.Windows.Forms.ToolStripSplitButton();
            this.redoToolBarButton = new System.Windows.Forms.ToolStripSplitButton();
            this.toolStripSeparatorSelectUI = new System.Windows.Forms.ToolStripSeparator();
            this.modeUIToolBarDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.menuMain = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startPageMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openPanoramaMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openContainingFolderMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator53 = new System.Windows.Forms.ToolStripSeparator();
            this.saveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.shareDocumentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.publishMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ardiaPublishMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.searchStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runPeptideSearchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.encyclopeDiaSearchMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFeatureDetectionMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peakBoundariesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator51 = new System.Windows.Forms.ToolStripSeparator();
            this.importPeptideSearchMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator52 = new System.Windows.Forms.ToolStripSeparator();
            this.importFASTAMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importAssayLibraryMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importMassListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importDocumentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importAnnotationsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportTransitionListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportIsolationListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportMethodMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator49 = new System.Windows.Forms.ToolStripSeparator();
            this.exportReportMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator50 = new System.Windows.Forms.ToolStripSeparator();
            this.exportSpectralLibraryMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.chromatogramsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mProphetFeaturesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportAnnotationsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mruBeforeToolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.mruAfterToolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refineToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorSettings = new System.Windows.Forms.ToolStripSeparator();
            this.saveCurrentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editSettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator31 = new System.Windows.Forms.ToolStripSeparator();
            this.shareSettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importSettingsMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.peptideSettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transitionSettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentSettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator37 = new System.Windows.Forms.ToolStripSeparator();
            this.integrateAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.placeholderToolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorTools = new System.Windows.Forms.ToolStripSeparator();
            this.updatesToolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStoreMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.configureToolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.searchToolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator46 = new System.Windows.Forms.ToolStripSeparator();
            this.immediateWindowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator47 = new System.Windows.Forms.ToolStripSeparator();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.homeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.videosMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.webinarsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tutorialsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reportsHelpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.commandLineHelpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.keyboardShortcutsHelpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.otherDocsHelpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.supportMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.issuesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.submitErrorReportMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.crashSkylineMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.checkForUpdatesSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.checkForUpdatesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator29 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.eSPFeaturesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel1.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.mainToolStrip.SuspendLayout();
            this.menuMain.SuspendLayout();
            this.SuspendLayout();
            // contextMenuTreeNode and tree node items moved to TreeNodeContextMenu
            //
            // selectionContextMenuItem
            // 
            this.selectionContextMenuItem.CheckOnClick = true;
            this.selectionContextMenuItem.Name = "selectionContextMenuItem";
            resources.ApplyResources(this.selectionContextMenuItem, "selectionContextMenuItem");
            this.selectionContextMenuItem.Click += new System.EventHandler(this.selectionContextMenuItem_Click);
            // 
            // synchronizeSummaryZoomingContextMenuItem
            // 
            this.synchronizeSummaryZoomingContextMenuItem.CheckOnClick = true;
            this.synchronizeSummaryZoomingContextMenuItem.Name = "synchronizeSummaryZoomingContextMenuItem";
            resources.ApplyResources(this.synchronizeSummaryZoomingContextMenuItem, "synchronizeSummaryZoomingContextMenuItem");
            this.synchronizeSummaryZoomingContextMenuItem.Click += new System.EventHandler(this.synchronizeSummaryZoomingContextMenuItem_Click);
            //
            // replicatesRTContextMenuItem
            // 
            this.replicatesRTContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.averageReplicatesContextMenuItem,
            this.singleReplicateRTContextMenuItem,
            this.bestReplicateRTContextMenuItem});
            this.replicatesRTContextMenuItem.Name = "replicatesRTContextMenuItem";
            resources.ApplyResources(this.replicatesRTContextMenuItem, "replicatesRTContextMenuItem");
            this.replicatesRTContextMenuItem.DropDownOpening += new System.EventHandler(this.replicatesRTContextMenuItem_DropDownOpening);
            // 
            // averageReplicatesContextMenuItem
            // 
            this.averageReplicatesContextMenuItem.Name = "averageReplicatesContextMenuItem";
            resources.ApplyResources(this.averageReplicatesContextMenuItem, "averageReplicatesContextMenuItem");
            this.averageReplicatesContextMenuItem.Click += new System.EventHandler(this.averageReplicatesContextMenuItem_Click);
            // 
            // singleReplicateRTContextMenuItem
            // 
            this.singleReplicateRTContextMenuItem.Name = "singleReplicateRTContextMenuItem";
            resources.ApplyResources(this.singleReplicateRTContextMenuItem, "singleReplicateRTContextMenuItem");
            this.singleReplicateRTContextMenuItem.Click += new System.EventHandler(this.singleReplicateRTContextMenuItem_Click);
            // 
            // bestReplicateRTContextMenuItem
            // 
            this.bestReplicateRTContextMenuItem.Name = "bestReplicateRTContextMenuItem";
            resources.ApplyResources(this.bestReplicateRTContextMenuItem, "bestReplicateRTContextMenuItem");
            this.bestReplicateRTContextMenuItem.Click += new System.EventHandler(this.bestReplicateRTContextMenuItem_Click);
            //
            // toolStripSeparator24
            //
            this.toolStripSeparator24.Name = "toolStripSeparator24";
            resources.ApplyResources(this.toolStripSeparator24, "toolStripSeparator24");
            //
            // toolStripSeparator25
            //
            this.toolStripSeparator25.Name = "toolStripSeparator25";
            resources.ApplyResources(this.toolStripSeparator25, "toolStripSeparator25");
            // contextMenuPeakAreas and peak area items moved to PeakAreasContextMenu
            //
            // peptideOrderContextMenuItem
            // 
            this.peptideOrderContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.peptideOrderDocumentContextMenuItem,
            this.peptideOrderRTContextMenuItem,
            this.peptideOrderAreaContextMenuItem,
            this.peptideOrderMassErrorContextMenuItem});
            this.peptideOrderContextMenuItem.Name = "peptideOrderContextMenuItem";
            resources.ApplyResources(this.peptideOrderContextMenuItem, "peptideOrderContextMenuItem");
            this.peptideOrderContextMenuItem.DropDownOpening += new System.EventHandler(this.peptideOrderContextMenuItem_DropDownOpening);
            // 
            // peptideOrderDocumentContextMenuItem
            // 
            this.peptideOrderDocumentContextMenuItem.Name = "peptideOrderDocumentContextMenuItem";
            resources.ApplyResources(this.peptideOrderDocumentContextMenuItem, "peptideOrderDocumentContextMenuItem");
            this.peptideOrderDocumentContextMenuItem.Click += new System.EventHandler(this.peptideOrderDocumentContextMenuItem_Click);
            // 
            // peptideOrderRTContextMenuItem
            // 
            this.peptideOrderRTContextMenuItem.Name = "peptideOrderRTContextMenuItem";
            resources.ApplyResources(this.peptideOrderRTContextMenuItem, "peptideOrderRTContextMenuItem");
            this.peptideOrderRTContextMenuItem.Click += new System.EventHandler(this.peptideOrderRTContextMenuItem_Click);
            // 
            // peptideOrderAreaContextMenuItem
            // 
            this.peptideOrderAreaContextMenuItem.Name = "peptideOrderAreaContextMenuItem";
            resources.ApplyResources(this.peptideOrderAreaContextMenuItem, "peptideOrderAreaContextMenuItem");
            this.peptideOrderAreaContextMenuItem.Click += new System.EventHandler(this.peptideOrderAreaContextMenuItem_Click);
            // 
            // peptideOrderMassErrorContextMenuItem
            // 
            this.peptideOrderMassErrorContextMenuItem.Name = "peptideOrderMassErrorContextMenuItem";
            resources.ApplyResources(this.peptideOrderMassErrorContextMenuItem, "peptideOrderMassErrorContextMenuItem");
            this.peptideOrderMassErrorContextMenuItem.Click += new System.EventHandler(this.peptideOrderMassErrorContextMenuItem_Click);
            // 
            // replicateOrderContextMenuItem
            // 
            this.replicateOrderContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.replicateOrderDocumentContextMenuItem,
            this.replicateOrderAcqTimeContextMenuItem});
            this.replicateOrderContextMenuItem.Name = "replicateOrderContextMenuItem";
            resources.ApplyResources(this.replicateOrderContextMenuItem, "replicateOrderContextMenuItem");
            // 
            // replicateOrderDocumentContextMenuItem
            // 
            this.replicateOrderDocumentContextMenuItem.Name = "replicateOrderDocumentContextMenuItem";
            resources.ApplyResources(this.replicateOrderDocumentContextMenuItem, "replicateOrderDocumentContextMenuItem");
            this.replicateOrderDocumentContextMenuItem.Click += new System.EventHandler(this.replicateOrderDocumentContextMenuItem_Click);
            // 
            // replicateOrderAcqTimeContextMenuItem
            // 
            this.replicateOrderAcqTimeContextMenuItem.Name = "replicateOrderAcqTimeContextMenuItem";
            resources.ApplyResources(this.replicateOrderAcqTimeContextMenuItem, "replicateOrderAcqTimeContextMenuItem");
            this.replicateOrderAcqTimeContextMenuItem.Click += new System.EventHandler(this.replicateOrderAcqTimeContextMenuItem_Click);
            //
            // scopeContextMenuItem
            // 
            this.scopeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.documentScopeContextMenuItem,
            this.proteinScopeContextMenuItem});
            this.scopeContextMenuItem.Name = "scopeContextMenuItem";
            resources.ApplyResources(this.scopeContextMenuItem, "scopeContextMenuItem");
            this.scopeContextMenuItem.DropDownOpening += new System.EventHandler(this.scopeContextMenuItem_DropDownOpening);
            // 
            // documentScopeContextMenuItem
            // 
            this.documentScopeContextMenuItem.Name = "documentScopeContextMenuItem";
            resources.ApplyResources(this.documentScopeContextMenuItem, "documentScopeContextMenuItem");
            this.documentScopeContextMenuItem.Click += new System.EventHandler(this.documentScopeContextMenuItem_Click);
            // 
            // proteinScopeContextMenuItem
            // 
            this.proteinScopeContextMenuItem.Name = "proteinScopeContextMenuItem";
            resources.ApplyResources(this.proteinScopeContextMenuItem, "proteinScopeContextMenuItem");
            this.proteinScopeContextMenuItem.Click += new System.EventHandler(this.proteinScopeContextMenuItem_Click);
            //
            // peptideCvsContextMenuItem
            // 
            this.peptideCvsContextMenuItem.CheckOnClick = true;
            this.peptideCvsContextMenuItem.Name = "peptideCvsContextMenuItem";
            resources.ApplyResources(this.peptideCvsContextMenuItem, "peptideCvsContextMenuItem");
            this.peptideCvsContextMenuItem.Click += new System.EventHandler(this.peptideCvsContextMenuItem_Click);
            // 
            // toolStripSeparator28
            // 
            this.toolStripSeparator28.Name = "toolStripSeparator28";
            resources.ApplyResources(this.toolStripSeparator28, "toolStripSeparator28");
            //
            // groupReplicatesByContextMenuItem
            // 
            this.groupReplicatesByContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.groupByReplicateContextMenuItem});
            this.groupReplicatesByContextMenuItem.Name = "groupReplicatesByContextMenuItem";
            resources.ApplyResources(this.groupReplicatesByContextMenuItem, "groupReplicatesByContextMenuItem");
            // 
            // groupByReplicateContextMenuItem
            // 
            this.groupByReplicateContextMenuItem.Name = "groupByReplicateContextMenuItem";
            resources.ApplyResources(this.groupByReplicateContextMenuItem, "groupByReplicateContextMenuItem");
            this.groupByReplicateContextMenuItem.Click += new System.EventHandler(this.groupByReplicateContextMenuItem_Click);
            //
            // panel1
            // 
            this.panel1.Controls.Add(this.dockPanel);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // dockPanel
            // 
            this.dockPanel.ActiveAutoHideContent = null;
            resources.ApplyResources(this.dockPanel, "dockPanel");
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.ActiveDocumentChanged += new System.EventHandler(this.dockPanel_ActiveDocumentChanged);
            // 
            // statusStrip
            // 
            resources.ApplyResources(this.statusStrip, "statusStrip");
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusGeneral,
            this.statusProgress,
            this.buttonShowAllChromatograms,
            this.statusSequences,
            this.statusPeptides,
            this.statusPrecursors,
            this.statusIons});
            this.statusStrip.Name = "statusStrip";
            // 
            // statusGeneral
            // 
            this.statusGeneral.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusGeneral.Name = "statusGeneral";
            resources.ApplyResources(this.statusGeneral, "statusGeneral");
            this.statusGeneral.Spring = true;
            // 
            // statusProgress
            // 
            this.statusProgress.Name = "statusProgress";
            resources.ApplyResources(this.statusProgress, "statusProgress");
            // 
            // buttonShowAllChromatograms
            // 
            this.buttonShowAllChromatograms.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.buttonShowAllChromatograms.DropDownButtonWidth = 0;
            this.buttonShowAllChromatograms.Image = global::pwiz.Skyline.Properties.Resources.AllIonsStatusButton;
            resources.ApplyResources(this.buttonShowAllChromatograms, "buttonShowAllChromatograms");
            this.buttonShowAllChromatograms.Name = "buttonShowAllChromatograms";
            this.buttonShowAllChromatograms.ButtonClick += new System.EventHandler(this.buttonShowAllChromatograms_ButtonClick);
            // 
            // statusSequences
            // 
            this.statusSequences.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusSequences.Name = "statusSequences";
            resources.ApplyResources(this.statusSequences, "statusSequences");
            // 
            // statusPeptides
            // 
            this.statusPeptides.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusPeptides.Name = "statusPeptides";
            resources.ApplyResources(this.statusPeptides, "statusPeptides");
            // 
            // statusPrecursors
            // 
            this.statusPrecursors.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusPrecursors.Name = "statusPrecursors";
            resources.ApplyResources(this.statusPrecursors, "statusPrecursors");
            // 
            // statusIons
            // 
            this.statusIons.Name = "statusIons";
            resources.ApplyResources(this.statusIons, "statusIons");
            // 
            // mainToolStrip
            // 
            this.mainToolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.mainToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolBarButton,
            this.openToolBarButton,
            this.saveToolBarButton,
            this.publishToolbarButton,
            this.toolStripSeparator20,
            this.cutToolBarButton,
            this.copyToolBarButton,
            this.pasteToolBarButton,
            this.toolStripSeparator21,
            this.undoToolBarButton,
            this.redoToolBarButton,
            this.toolStripSeparatorSelectUI,
            this.modeUIToolBarDropDownButton});
            resources.ApplyResources(this.mainToolStrip, "mainToolStrip");
            this.mainToolStrip.Name = "mainToolStrip";
            // 
            // newToolBarButton
            // 
            this.newToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.newToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.NewDocument;
            resources.ApplyResources(this.newToolBarButton, "newToolBarButton");
            this.newToolBarButton.Name = "newToolBarButton";
            this.newToolBarButton.Click += new System.EventHandler(this.newMenuItem_Click);
            // 
            // openToolBarButton
            // 
            this.openToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.openToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.OpenFolder;
            resources.ApplyResources(this.openToolBarButton, "openToolBarButton");
            this.openToolBarButton.Name = "openToolBarButton";
            this.openToolBarButton.Click += new System.EventHandler(this.openMenuItem_Click);
            // 
            // saveToolBarButton
            // 
            this.saveToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.saveToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Save;
            resources.ApplyResources(this.saveToolBarButton, "saveToolBarButton");
            this.saveToolBarButton.Name = "saveToolBarButton";
            this.saveToolBarButton.Click += new System.EventHandler(this.saveMenuItem_Click);
            // 
            // publishToolbarButton
            // 
            this.publishToolbarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.publishToolbarButton.Image = global::pwiz.Skyline.Properties.Resources.PanoramaPublish;
            resources.ApplyResources(this.publishToolbarButton, "publishToolbarButton");
            this.publishToolbarButton.Name = "publishToolbarButton";
            this.publishToolbarButton.Click += new System.EventHandler(this.publishMenuItem_Click);
            // 
            // toolStripSeparator20
            // 
            this.toolStripSeparator20.Name = "toolStripSeparator20";
            resources.ApplyResources(this.toolStripSeparator20, "toolStripSeparator20");
            // 
            // cutToolBarButton
            // 
            this.cutToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.cutToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Cut;
            resources.ApplyResources(this.cutToolBarButton, "cutToolBarButton");
            this.cutToolBarButton.Name = "cutToolBarButton";
            this.cutToolBarButton.Click += new System.EventHandler(this.cutMenuItem_Click);
            // 
            // copyToolBarButton
            // 
            this.copyToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.copyToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Copy;
            resources.ApplyResources(this.copyToolBarButton, "copyToolBarButton");
            this.copyToolBarButton.Name = "copyToolBarButton";
            this.copyToolBarButton.Click += new System.EventHandler(this.copyMenuItem_Click);
            // 
            // pasteToolBarButton
            // 
            this.pasteToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.pasteToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Paste;
            resources.ApplyResources(this.pasteToolBarButton, "pasteToolBarButton");
            this.pasteToolBarButton.Name = "pasteToolBarButton";
            this.pasteToolBarButton.Click += new System.EventHandler(this.pasteMenuItem_Click);
            // 
            // toolStripSeparator21
            // 
            this.toolStripSeparator21.Name = "toolStripSeparator21";
            resources.ApplyResources(this.toolStripSeparator21, "toolStripSeparator21");
            // 
            // undoToolBarButton
            // 
            this.undoToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.undoToolBarButton, "undoToolBarButton");
            this.undoToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Edit_Undo;
            this.undoToolBarButton.Name = "undoToolBarButton";
            // 
            // redoToolBarButton
            // 
            this.redoToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.redoToolBarButton, "redoToolBarButton");
            this.redoToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Edit_Redo;
            this.redoToolBarButton.Name = "redoToolBarButton";
            // 
            // toolStripSeparatorSelectUI
            // 
            this.toolStripSeparatorSelectUI.Name = "toolStripSeparatorSelectUI";
            resources.ApplyResources(this.toolStripSeparatorSelectUI, "toolStripSeparatorSelectUI");
            // 
            // modeUIToolBarDropDownButton
            // 
            this.modeUIToolBarDropDownButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.modeUIToolBarDropDownButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.modeUIToolBarDropDownButton.Image = global::pwiz.Skyline.Properties.Resources.UIModeProteomic;
            resources.ApplyResources(this.modeUIToolBarDropDownButton, "modeUIToolBarDropDownButton");
            this.modeUIToolBarDropDownButton.Name = "modeUIToolBarDropDownButton";
            // 
            // menuMain
            // 
            this.menuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.refineToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.settingsToolStripMenuItem,
            this.toolsMenu,
            this.helpToolStripMenuItem});
            resources.ApplyResources(this.menuMain, "menuMain");
            this.menuMain.Name = "menuMain";
            this.menuMain.ShowItemToolTips = true;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startPageMenuItem,
            this.newMenuItem,
            this.openMenuItem,
            this.openPanoramaMenuItem,
            this.openContainingFolderMenuItem,
            this.toolStripSeparator53,
            this.saveMenuItem,
            this.saveAsMenuItem,
            this.shareDocumentMenuItem,
            this.publishMenuItem,
            this.ardiaPublishMenuItem,
            this.toolStripSeparator2,
            this.searchStripMenuItem,
            this.importToolStripMenuItem,
            this.exportToolStripMenuItem,
            this.mruBeforeToolStripSeparator,
            this.mruAfterToolStripSeparator,
            this.exitMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            resources.ApplyResources(this.fileToolStripMenuItem, "fileToolStripMenuItem");
            this.fileToolStripMenuItem.DropDownOpening += new System.EventHandler(this.fileMenu_DropDownOpening);
            // 
            // startPageMenuItem
            // 
            this.startPageMenuItem.Image = global::pwiz.Skyline.Properties.Resources.HomeIcon1;
            resources.ApplyResources(this.startPageMenuItem, "startPageMenuItem");
            this.startPageMenuItem.Name = "startPageMenuItem";
            this.startPageMenuItem.Click += new System.EventHandler(this.startPageMenuItem_Click);
            // 
            // newMenuItem
            // 
            this.newMenuItem.Image = global::pwiz.Skyline.Properties.Resources.NewDocument;
            resources.ApplyResources(this.newMenuItem, "newMenuItem");
            this.newMenuItem.Name = "newMenuItem";
            this.newMenuItem.Click += new System.EventHandler(this.newMenuItem_Click);
            // 
            // openMenuItem
            // 
            this.openMenuItem.Image = global::pwiz.Skyline.Properties.Resources.OpenFolder;
            resources.ApplyResources(this.openMenuItem, "openMenuItem");
            this.openMenuItem.Name = "openMenuItem";
            this.openMenuItem.Click += new System.EventHandler(this.openMenuItem_Click);
            // 
            // openPanoramaMenuItem
            // 
            this.openPanoramaMenuItem.Image = global::pwiz.Skyline.Properties.Resources.PanoramaDownload;
            resources.ApplyResources(this.openPanoramaMenuItem, "openPanoramaMenuItem");
            this.openPanoramaMenuItem.Name = "openPanoramaMenuItem";
            this.openPanoramaMenuItem.Click += new System.EventHandler(this.openPanorama_Click);
            // 
            // openContainingFolderMenuItem
            // 
            this.openContainingFolderMenuItem.Name = "openContainingFolderMenuItem";
            resources.ApplyResources(this.openContainingFolderMenuItem, "openContainingFolderMenuItem");
            this.openContainingFolderMenuItem.Click += new System.EventHandler(this.openContainingFolderMenuItem_Click);
            // 
            // toolStripSeparator53
            // 
            this.toolStripSeparator53.Name = "toolStripSeparator53";
            resources.ApplyResources(this.toolStripSeparator53, "toolStripSeparator53");
            // 
            // saveMenuItem
            // 
            this.saveMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Save;
            resources.ApplyResources(this.saveMenuItem, "saveMenuItem");
            this.saveMenuItem.Name = "saveMenuItem";
            this.saveMenuItem.Click += new System.EventHandler(this.saveMenuItem_Click);
            // 
            // saveAsMenuItem
            // 
            this.saveAsMenuItem.Name = "saveAsMenuItem";
            resources.ApplyResources(this.saveAsMenuItem, "saveAsMenuItem");
            this.saveAsMenuItem.Click += new System.EventHandler(this.saveAsMenuItem_Click);
            // 
            // shareDocumentMenuItem
            // 
            this.shareDocumentMenuItem.Name = "shareDocumentMenuItem";
            resources.ApplyResources(this.shareDocumentMenuItem, "shareDocumentMenuItem");
            this.shareDocumentMenuItem.Click += new System.EventHandler(this.shareDocumentMenuItem_Click);
            // 
            // publishMenuItem
            // 
            this.publishMenuItem.Image = global::pwiz.Skyline.Properties.Resources.PanoramaPublish;
            resources.ApplyResources(this.publishMenuItem, "publishMenuItem");
            this.publishMenuItem.Name = "publishMenuItem";
            this.publishMenuItem.Click += new System.EventHandler(this.publishMenuItem_Click);
            // 
            // ardiaPublishMenuItem
            // 
            this.ardiaPublishMenuItem.Image = global::pwiz.Skyline.Properties.Resources.ArdiaIcon;
            resources.ApplyResources(this.ardiaPublishMenuItem, "ardiaPublishMenuItem");
            this.ardiaPublishMenuItem.Name = "ardiaPublishMenuItem";
            this.ardiaPublishMenuItem.Click += new System.EventHandler(this.ardiaPublishMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(this.toolStripSeparator2, "toolStripSeparator2");
            // 
            // searchStripMenuItem
            // 
            this.searchStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.runPeptideSearchToolStripMenuItem,
            this.encyclopeDiaSearchMenuItem,
            this.importFeatureDetectionMenuItem});
            this.searchStripMenuItem.Name = "searchStripMenuItem";
            resources.ApplyResources(this.searchStripMenuItem, "searchStripMenuItem");
            // 
            // runPeptideSearchToolStripMenuItem
            // 
            this.runPeptideSearchToolStripMenuItem.Name = "runPeptideSearchToolStripMenuItem";
            resources.ApplyResources(this.runPeptideSearchToolStripMenuItem, "runPeptideSearchToolStripMenuItem");
            this.modeUIHandler.SetUIMode(this.runPeptideSearchToolStripMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.runPeptideSearchToolStripMenuItem.Click += new System.EventHandler(this.runPeptideSearchToolStripMenuItem_Click);
            // 
            // encyclopeDiaSearchMenuItem
            // 
            this.encyclopeDiaSearchMenuItem.Name = "encyclopeDiaSearchMenuItem";
            resources.ApplyResources(this.encyclopeDiaSearchMenuItem, "encyclopeDiaSearchMenuItem");
            this.modeUIHandler.SetUIMode(this.encyclopeDiaSearchMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.encyclopeDiaSearchMenuItem.Click += new System.EventHandler(this.encyclopeDiaSearchMenuItem_Click);
            // 
            // importFeatureDetectionMenuItem
            // 
            this.importFeatureDetectionMenuItem.Name = "importFeatureDetectionMenuItem";
            resources.ApplyResources(this.importFeatureDetectionMenuItem, "importFeatureDetectionMenuItem");
            this.importFeatureDetectionMenuItem.Click += new System.EventHandler(this.importFeatureDetectionMenuItem_Click);
            // 
            // importToolStripMenuItem
            // 
            this.importToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importResultsMenuItem,
            this.peakBoundariesToolStripMenuItem,
            this.toolStripSeparator51,
            this.importPeptideSearchMenuItem,
            this.toolStripSeparator52,
            this.importFASTAMenuItem,
            this.importAssayLibraryMenuItem,
            this.importMassListMenuItem,
            this.importDocumentMenuItem,
            this.importAnnotationsMenuItem});
            this.importToolStripMenuItem.Name = "importToolStripMenuItem";
            resources.ApplyResources(this.importToolStripMenuItem, "importToolStripMenuItem");
            // 
            // importResultsMenuItem
            // 
            this.importResultsMenuItem.Name = "importResultsMenuItem";
            resources.ApplyResources(this.importResultsMenuItem, "importResultsMenuItem");
            this.importResultsMenuItem.Click += new System.EventHandler(this.importResultsMenuItem_Click);
            // 
            // peakBoundariesToolStripMenuItem
            // 
            this.peakBoundariesToolStripMenuItem.Name = "peakBoundariesToolStripMenuItem";
            resources.ApplyResources(this.peakBoundariesToolStripMenuItem, "peakBoundariesToolStripMenuItem");
            this.peakBoundariesToolStripMenuItem.Click += new System.EventHandler(this.peakBoundariesToolStripMenuItem_Click);
            // 
            // toolStripSeparator51
            // 
            this.toolStripSeparator51.Name = "toolStripSeparator51";
            resources.ApplyResources(this.toolStripSeparator51, "toolStripSeparator51");
            // 
            // importPeptideSearchMenuItem
            // 
            this.importPeptideSearchMenuItem.Name = "importPeptideSearchMenuItem";
            resources.ApplyResources(this.importPeptideSearchMenuItem, "importPeptideSearchMenuItem");
            this.modeUIHandler.SetUIMode(this.importPeptideSearchMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.importPeptideSearchMenuItem.Click += new System.EventHandler(this.importPeptideSearchMenuItem_Click);
            // 
            // toolStripSeparator52
            // 
            this.toolStripSeparator52.Name = "toolStripSeparator52";
            resources.ApplyResources(this.toolStripSeparator52, "toolStripSeparator52");
            // 
            // importFASTAMenuItem
            // 
            this.importFASTAMenuItem.Name = "importFASTAMenuItem";
            resources.ApplyResources(this.importFASTAMenuItem, "importFASTAMenuItem");
            this.modeUIHandler.SetUIMode(this.importFASTAMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.importFASTAMenuItem.Click += new System.EventHandler(this.importFASTAMenuItem_Click);
            // 
            // importAssayLibraryMenuItem
            // 
            this.importAssayLibraryMenuItem.Name = "importAssayLibraryMenuItem";
            resources.ApplyResources(this.importAssayLibraryMenuItem, "importAssayLibraryMenuItem");
            this.importAssayLibraryMenuItem.Click += new System.EventHandler(this.importAssayLibraryMenuItem_Click);
            // 
            // importMassListMenuItem
            // 
            this.importMassListMenuItem.Name = "importMassListMenuItem";
            resources.ApplyResources(this.importMassListMenuItem, "importMassListMenuItem");
            this.importMassListMenuItem.Click += new System.EventHandler(this.importMassListMenuItem_Click);
            // 
            // importDocumentMenuItem
            // 
            this.importDocumentMenuItem.Name = "importDocumentMenuItem";
            resources.ApplyResources(this.importDocumentMenuItem, "importDocumentMenuItem");
            this.importDocumentMenuItem.Click += new System.EventHandler(this.importDocumentMenuItem_Click);
            // 
            // importAnnotationsMenuItem
            // 
            this.importAnnotationsMenuItem.Name = "importAnnotationsMenuItem";
            resources.ApplyResources(this.importAnnotationsMenuItem, "importAnnotationsMenuItem");
            this.importAnnotationsMenuItem.Click += new System.EventHandler(this.importAnnotationsMenuItem_Click);
            // 
            // exportToolStripMenuItem
            // 
            this.exportToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportTransitionListMenuItem,
            this.exportIsolationListMenuItem,
            this.exportMethodMenuItem,
            this.toolStripSeparator49,
            this.exportReportMenuItem,
            this.toolStripSeparator50,
            this.exportSpectralLibraryMenuItem,
            this.chromatogramsToolStripMenuItem,
            this.mProphetFeaturesMenuItem,
            this.exportAnnotationsMenuItem});
            this.exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            resources.ApplyResources(this.exportToolStripMenuItem, "exportToolStripMenuItem");
            // 
            // exportTransitionListMenuItem
            // 
            this.exportTransitionListMenuItem.Name = "exportTransitionListMenuItem";
            resources.ApplyResources(this.exportTransitionListMenuItem, "exportTransitionListMenuItem");
            this.exportTransitionListMenuItem.Click += new System.EventHandler(this.exportTransitionListMenuItem_Click);
            // 
            // exportIsolationListMenuItem
            // 
            this.exportIsolationListMenuItem.Name = "exportIsolationListMenuItem";
            resources.ApplyResources(this.exportIsolationListMenuItem, "exportIsolationListMenuItem");
            this.exportIsolationListMenuItem.Click += new System.EventHandler(this.exportIsolationListMenuItem_Click);
            // 
            // exportMethodMenuItem
            // 
            this.exportMethodMenuItem.Name = "exportMethodMenuItem";
            resources.ApplyResources(this.exportMethodMenuItem, "exportMethodMenuItem");
            this.exportMethodMenuItem.Click += new System.EventHandler(this.exportMethodMenuItem_Click);
            // 
            // toolStripSeparator49
            // 
            this.toolStripSeparator49.Name = "toolStripSeparator49";
            resources.ApplyResources(this.toolStripSeparator49, "toolStripSeparator49");
            // 
            // exportReportMenuItem
            // 
            this.exportReportMenuItem.Name = "exportReportMenuItem";
            resources.ApplyResources(this.exportReportMenuItem, "exportReportMenuItem");
            this.exportReportMenuItem.Click += new System.EventHandler(this.exportReportMenuItem_Click);
            // 
            // toolStripSeparator50
            // 
            this.toolStripSeparator50.Name = "toolStripSeparator50";
            resources.ApplyResources(this.toolStripSeparator50, "toolStripSeparator50");
            // 
            // exportSpectralLibraryMenuItem
            // 
            this.exportSpectralLibraryMenuItem.Name = "exportSpectralLibraryMenuItem";
            resources.ApplyResources(this.exportSpectralLibraryMenuItem, "exportSpectralLibraryMenuItem");
            this.exportSpectralLibraryMenuItem.Click += new System.EventHandler(this.exportSpectralLibraryMenuItem_Click);
            // 
            // chromatogramsToolStripMenuItem
            // 
            this.chromatogramsToolStripMenuItem.Name = "chromatogramsToolStripMenuItem";
            resources.ApplyResources(this.chromatogramsToolStripMenuItem, "chromatogramsToolStripMenuItem");
            this.chromatogramsToolStripMenuItem.Click += new System.EventHandler(this.chromatogramsToolStripMenuItem_Click);
            // 
            // mProphetFeaturesMenuItem
            // 
            this.mProphetFeaturesMenuItem.Name = "mProphetFeaturesMenuItem";
            resources.ApplyResources(this.mProphetFeaturesMenuItem, "mProphetFeaturesMenuItem");
            this.mProphetFeaturesMenuItem.Click += new System.EventHandler(this.mProphetFeaturesMenuItem_Click);
            // 
            // exportAnnotationsMenuItem
            // 
            this.exportAnnotationsMenuItem.Name = "exportAnnotationsMenuItem";
            resources.ApplyResources(this.exportAnnotationsMenuItem, "exportAnnotationsMenuItem");
            this.exportAnnotationsMenuItem.Click += new System.EventHandler(this.exportAnnotationsMenuItem_Click);
            // 
            // mruBeforeToolStripSeparator
            // 
            this.mruBeforeToolStripSeparator.Name = "mruBeforeToolStripSeparator";
            resources.ApplyResources(this.mruBeforeToolStripSeparator, "mruBeforeToolStripSeparator");
            // 
            // mruAfterToolStripSeparator
            // 
            this.mruAfterToolStripSeparator.Name = "mruAfterToolStripSeparator";
            resources.ApplyResources(this.mruAfterToolStripSeparator, "mruAfterToolStripSeparator");
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Name = "exitMenuItem";
            resources.ApplyResources(this.exitMenuItem, "exitMenuItem");
            this.exitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            resources.ApplyResources(this.editToolStripMenuItem, "editToolStripMenuItem");
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.DropDownOpening += new System.EventHandler(this.editToolStripMenuItem_DropDownOpening);
            // 
            // refineToolStripMenuItem
            // 
            this.refineToolStripMenuItem.Name = "refineToolStripMenuItem";
            resources.ApplyResources(this.refineToolStripMenuItem, "refineToolStripMenuItem");
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            resources.ApplyResources(this.viewToolStripMenuItem, "viewToolStripMenuItem");
            this.viewToolStripMenuItem.DropDownOpening += new System.EventHandler(this.viewToolStripMenuItem_DropDownOpening);
            // 
            // settingsToolStripMenuItem
            // 
            this.settingsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripSeparatorSettings,
            this.saveCurrentMenuItem,
            this.editSettingsMenuItem,
            this.toolStripSeparator31,
            this.shareSettingsMenuItem,
            this.importSettingsMenuItem1,
            this.toolStripSeparator3,
            this.peptideSettingsMenuItem,
            this.transitionSettingsMenuItem,
            this.documentSettingsMenuItem,
            this.toolStripSeparator37,
            this.integrateAllMenuItem});
            this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            resources.ApplyResources(this.settingsToolStripMenuItem, "settingsToolStripMenuItem");
            this.settingsToolStripMenuItem.DropDownOpening += new System.EventHandler(this.settingsMenu_DropDownOpening);
            // 
            // toolStripSeparatorSettings
            // 
            this.toolStripSeparatorSettings.Name = "toolStripSeparatorSettings";
            resources.ApplyResources(this.toolStripSeparatorSettings, "toolStripSeparatorSettings");
            // 
            // saveCurrentMenuItem
            // 
            this.saveCurrentMenuItem.Name = "saveCurrentMenuItem";
            resources.ApplyResources(this.saveCurrentMenuItem, "saveCurrentMenuItem");
            this.saveCurrentMenuItem.Click += new System.EventHandler(this.saveCurrentMenuItem_Click);
            // 
            // editSettingsMenuItem
            // 
            this.editSettingsMenuItem.Name = "editSettingsMenuItem";
            resources.ApplyResources(this.editSettingsMenuItem, "editSettingsMenuItem");
            this.editSettingsMenuItem.Click += new System.EventHandler(this.editSettingsMenuItem_Click);
            // 
            // toolStripSeparator31
            // 
            this.toolStripSeparator31.Name = "toolStripSeparator31";
            resources.ApplyResources(this.toolStripSeparator31, "toolStripSeparator31");
            // 
            // shareSettingsMenuItem
            // 
            this.shareSettingsMenuItem.Name = "shareSettingsMenuItem";
            resources.ApplyResources(this.shareSettingsMenuItem, "shareSettingsMenuItem");
            this.shareSettingsMenuItem.Click += new System.EventHandler(this.shareSettingsMenuItem_Click);
            // 
            // importSettingsMenuItem1
            // 
            this.importSettingsMenuItem1.Name = "importSettingsMenuItem1";
            resources.ApplyResources(this.importSettingsMenuItem1, "importSettingsMenuItem1");
            this.importSettingsMenuItem1.Click += new System.EventHandler(this.importSettingsMenuItem1_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            resources.ApplyResources(this.toolStripSeparator3, "toolStripSeparator3");
            // 
            // peptideSettingsMenuItem
            // 
            this.peptideSettingsMenuItem.Name = "peptideSettingsMenuItem";
            resources.ApplyResources(this.peptideSettingsMenuItem, "peptideSettingsMenuItem");
            this.peptideSettingsMenuItem.Click += new System.EventHandler(this.peptideSettingsMenuItem_Click);
            // 
            // transitionSettingsMenuItem
            // 
            this.transitionSettingsMenuItem.Name = "transitionSettingsMenuItem";
            resources.ApplyResources(this.transitionSettingsMenuItem, "transitionSettingsMenuItem");
            this.transitionSettingsMenuItem.Click += new System.EventHandler(this.transitionSettingsMenuItem_Click);
            // 
            // documentSettingsMenuItem
            // 
            this.documentSettingsMenuItem.Name = "documentSettingsMenuItem";
            resources.ApplyResources(this.documentSettingsMenuItem, "documentSettingsMenuItem");
            this.documentSettingsMenuItem.Click += new System.EventHandler(this.documentSettingsMenuItem_Click);
            // 
            // toolStripSeparator37
            // 
            this.toolStripSeparator37.Name = "toolStripSeparator37";
            resources.ApplyResources(this.toolStripSeparator37, "toolStripSeparator37");
            // 
            // integrateAllMenuItem
            // 
            this.integrateAllMenuItem.Name = "integrateAllMenuItem";
            resources.ApplyResources(this.integrateAllMenuItem, "integrateAllMenuItem");
            this.integrateAllMenuItem.Click += new System.EventHandler(this.integrateAllMenuItem_Click);
            // 
            // toolsMenu
            // 
            this.toolsMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.placeholderToolsMenuItem,
            this.toolStripSeparatorTools,
            this.updatesToolsMenuItem,
            this.toolStoreMenuItem,
            this.configureToolsMenuItem,
            this.searchToolsMenuItem,
            this.toolStripSeparator46,
            this.immediateWindowToolStripMenuItem,
            this.toolStripSeparator47,
            this.optionsToolStripMenuItem});
            this.toolsMenu.Name = "toolsMenu";
            resources.ApplyResources(this.toolsMenu, "toolsMenu");
            this.toolsMenu.DropDownOpening += new System.EventHandler(this.toolsMenu_DropDownOpening);
            // 
            // placeholderToolsMenuItem
            // 
            this.placeholderToolsMenuItem.Name = "placeholderToolsMenuItem";
            resources.ApplyResources(this.placeholderToolsMenuItem, "placeholderToolsMenuItem");
            // 
            // toolStripSeparatorTools
            // 
            this.toolStripSeparatorTools.Name = "toolStripSeparatorTools";
            resources.ApplyResources(this.toolStripSeparatorTools, "toolStripSeparatorTools");
            // 
            // updatesToolsMenuItem
            // 
            resources.ApplyResources(this.updatesToolsMenuItem, "updatesToolsMenuItem");
            this.updatesToolsMenuItem.Name = "updatesToolsMenuItem";
            this.updatesToolsMenuItem.Click += new System.EventHandler(this.updatesToolsMenuItem_Click);
            // 
            // toolStoreMenuItem
            // 
            this.toolStoreMenuItem.Name = "toolStoreMenuItem";
            resources.ApplyResources(this.toolStoreMenuItem, "toolStoreMenuItem");
            this.toolStoreMenuItem.Click += new System.EventHandler(this.toolStoreMenuItem_Click);
            // 
            // configureToolsMenuItem
            // 
            this.configureToolsMenuItem.Name = "configureToolsMenuItem";
            resources.ApplyResources(this.configureToolsMenuItem, "configureToolsMenuItem");
            this.configureToolsMenuItem.Click += new System.EventHandler(this.configureToolsMenuItem_Click);
            // 
            // searchToolsMenuItem
            // 
            this.searchToolsMenuItem.Name = "searchToolsMenuItem";
            resources.ApplyResources(this.searchToolsMenuItem, "searchToolsMenuItem");
            this.searchToolsMenuItem.Click += new System.EventHandler(this.searchToolsMenuItem_Click);
            // 
            // toolStripSeparator46
            // 
            this.toolStripSeparator46.Name = "toolStripSeparator46";
            resources.ApplyResources(this.toolStripSeparator46, "toolStripSeparator46");
            // 
            // immediateWindowToolStripMenuItem
            // 
            this.immediateWindowToolStripMenuItem.Name = "immediateWindowToolStripMenuItem";
            resources.ApplyResources(this.immediateWindowToolStripMenuItem, "immediateWindowToolStripMenuItem");
            this.immediateWindowToolStripMenuItem.Click += new System.EventHandler(this.immediateWindowToolStripMenuItem_Click);
            // 
            // toolStripSeparator47
            // 
            this.toolStripSeparator47.Name = "toolStripSeparator47";
            resources.ApplyResources(this.toolStripSeparator47, "toolStripSeparator47");
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            resources.ApplyResources(this.optionsToolStripMenuItem, "optionsToolStripMenuItem");
            this.optionsToolStripMenuItem.Click += new System.EventHandler(this.optionsToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.homeMenuItem,
            this.videosMenuItem,
            this.webinarsMenuItem,
            this.tutorialsMenuItem,
            this.documentationToolStripMenuItem,
            this.supportMenuItem,
            this.issuesMenuItem,
            this.submitErrorReportMenuItem,
            this.crashSkylineMenuItem,
            this.checkForUpdatesSeparator,
            this.checkForUpdatesMenuItem,
            this.toolStripSeparator29,
            this.aboutMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            resources.ApplyResources(this.helpToolStripMenuItem, "helpToolStripMenuItem");
            this.helpToolStripMenuItem.DropDownOpening += new System.EventHandler(this.helpToolStripMenuItem_DropDownOpening);
            // 
            // homeMenuItem
            // 
            this.homeMenuItem.Name = "homeMenuItem";
            resources.ApplyResources(this.homeMenuItem, "homeMenuItem");
            this.homeMenuItem.Click += new System.EventHandler(this.homeMenuItem_Click);
            // 
            // videosMenuItem
            // 
            this.videosMenuItem.Name = "videosMenuItem";
            resources.ApplyResources(this.videosMenuItem, "videosMenuItem");
            this.videosMenuItem.Click += new System.EventHandler(this.videosMenuItem_Click);
            // 
            // webinarsMenuItem
            // 
            this.webinarsMenuItem.Name = "webinarsMenuItem";
            resources.ApplyResources(this.webinarsMenuItem, "webinarsMenuItem");
            this.webinarsMenuItem.Click += new System.EventHandler(this.webinarsMenuItem_Click);
            // 
            // tutorialsMenuItem
            // 
            this.tutorialsMenuItem.Name = "tutorialsMenuItem";
            resources.ApplyResources(this.tutorialsMenuItem, "tutorialsMenuItem");
            this.tutorialsMenuItem.Click += new System.EventHandler(this.tutorialsMenuItem_Click);
            // 
            // documentationToolStripMenuItem
            // 
            this.documentationToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.reportsHelpMenuItem,
            this.commandLineHelpMenuItem,
            this.keyboardShortcutsHelpMenuItem,
            this.otherDocsHelpMenuItem});
            this.documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            resources.ApplyResources(this.documentationToolStripMenuItem, "documentationToolStripMenuItem");
            // 
            // reportsHelpMenuItem
            // 
            this.reportsHelpMenuItem.Name = "reportsHelpMenuItem";
            resources.ApplyResources(this.reportsHelpMenuItem, "reportsHelpMenuItem");
            this.reportsHelpMenuItem.Click += new System.EventHandler(this.reportsHelpMenuItem_Click);
            // 
            // commandLineHelpMenuItem
            // 
            this.commandLineHelpMenuItem.Name = "commandLineHelpMenuItem";
            resources.ApplyResources(this.commandLineHelpMenuItem, "commandLineHelpMenuItem");
            this.commandLineHelpMenuItem.Click += new System.EventHandler(this.commandLineHelpMenuItem_Click);
            // 
            // keyboardShortcutsHelpMenuItem
            // 
            this.keyboardShortcutsHelpMenuItem.Name = "keyboardShortcutsHelpMenuItem";
            resources.ApplyResources(this.keyboardShortcutsHelpMenuItem, "keyboardShortcutsHelpMenuItem");
            this.keyboardShortcutsHelpMenuItem.Click += new System.EventHandler(this.keyboardShortcutsHelpMenuItem_Click);
            // 
            // otherDocsHelpMenuItem
            // 
            this.otherDocsHelpMenuItem.Name = "otherDocsHelpMenuItem";
            resources.ApplyResources(this.otherDocsHelpMenuItem, "otherDocsHelpMenuItem");
            this.otherDocsHelpMenuItem.Click += new System.EventHandler(this.otherDocsHelpMenuItem_Click);
            // 
            // supportMenuItem
            // 
            this.supportMenuItem.Name = "supportMenuItem";
            resources.ApplyResources(this.supportMenuItem, "supportMenuItem");
            this.supportMenuItem.Click += new System.EventHandler(this.supportMenuItem_Click);
            // 
            // issuesMenuItem
            // 
            this.issuesMenuItem.Name = "issuesMenuItem";
            resources.ApplyResources(this.issuesMenuItem, "issuesMenuItem");
            this.issuesMenuItem.Click += new System.EventHandler(this.issuesMenuItem_Click);
            // 
            // submitErrorReportMenuItem
            // 
            this.submitErrorReportMenuItem.Name = "submitErrorReportMenuItem";
            resources.ApplyResources(this.submitErrorReportMenuItem, "submitErrorReportMenuItem");
            this.submitErrorReportMenuItem.Click += new System.EventHandler(this.submitErrorReportMenuItem_Click);
            // 
            // crashSkylineMenuItem
            // 
            this.crashSkylineMenuItem.Name = "crashSkylineMenuItem";
            resources.ApplyResources(this.crashSkylineMenuItem, "crashSkylineMenuItem");
            this.crashSkylineMenuItem.Click += new System.EventHandler(this.crashSkylineMenuItem_Click);
            // 
            // checkForUpdatesSeparator
            // 
            this.checkForUpdatesSeparator.Name = "checkForUpdatesSeparator";
            resources.ApplyResources(this.checkForUpdatesSeparator, "checkForUpdatesSeparator");
            // 
            // checkForUpdatesMenuItem
            // 
            this.checkForUpdatesMenuItem.Name = "checkForUpdatesMenuItem";
            resources.ApplyResources(this.checkForUpdatesMenuItem, "checkForUpdatesMenuItem");
            this.checkForUpdatesMenuItem.Click += new System.EventHandler(this.checkForUpdatesMenuItem_Click);
            // 
            // toolStripSeparator29
            // 
            this.toolStripSeparator29.Name = "toolStripSeparator29";
            resources.ApplyResources(this.toolStripSeparator29, "toolStripSeparator29");
            // 
            // aboutMenuItem
            // 
            this.aboutMenuItem.Name = "aboutMenuItem";
            resources.ApplyResources(this.aboutMenuItem, "aboutMenuItem");
            this.aboutMenuItem.Click += new System.EventHandler(this.aboutMenuItem_Click);
            // 
            // eSPFeaturesMenuItem
            // 
            this.eSPFeaturesMenuItem.Name = "eSPFeaturesMenuItem";
            resources.ApplyResources(this.eSPFeaturesMenuItem, "eSPFeaturesMenuItem");
            this.modeUIHandler.SetUIMode(this.eSPFeaturesMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.eSPFeaturesMenuItem.Click += new System.EventHandler(this.espFeaturesMenuItem_Click);
            //
            // SkylineWindow
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.mainToolStrip);
            this.Controls.Add(this.menuMain);
            this.Icon = global::pwiz.Skyline.Properties.Resources.Skyline;
            this.MainMenuStrip = this.menuMain;
            this.Name = "SkylineWindow";
            this.Activated += new System.EventHandler(this.SkylineWindow_Activated);
            this.Move += new System.EventHandler(this.SkylineWindow_Move);
            this.Resize += new System.EventHandler(this.SkylineWindow_Resize);
            this.panel1.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.mainToolStrip.ResumeLayout(false);
            this.mainToolStrip.PerformLayout();
            this.menuMain.ResumeLayout(false);
            this.menuMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutMenuItem;
        private System.Windows.Forms.MenuStrip menuMain;
        private System.Windows.Forms.ToolStripSeparator mruAfterToolStripSeparator;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorSettings;
        private System.Windows.Forms.ToolStripMenuItem saveCurrentMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editSettingsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem peptideSettingsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transitionSettingsMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusSequences;
        private System.Windows.Forms.ToolStripStatusLabel statusPrecursors;
        private System.Windows.Forms.ToolStripStatusLabel statusIons;
        private System.Windows.Forms.ToolStripStatusLabel statusGeneral;
        private System.Windows.Forms.ToolStripMenuItem exportToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFASTAMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importResultsMenuItem;
        private System.Windows.Forms.ToolStripSeparator mruBeforeToolStripSeparator;
        private System.Windows.Forms.ToolStripProgressBar statusProgress;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importMassListMenuItem;
        private DigitalRune.Windows.Docking.DockPanel dockPanel;
        private System.Windows.Forms.ToolStrip mainToolStrip;        
        private System.Windows.Forms.ToolStripButton newToolBarButton;        
        private System.Windows.Forms.ToolStripSplitButton undoToolBarButton;        
        private System.Windows.Forms.ToolStripSplitButton redoToolBarButton;        
        private System.Windows.Forms.ToolStripButton openToolBarButton;        
        private System.Windows.Forms.ToolStripButton saveToolBarButton;        
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator20;        
        private System.Windows.Forms.ToolStripButton cutToolBarButton;        
        private System.Windows.Forms.ToolStripButton copyToolBarButton;        
        private System.Windows.Forms.ToolStripButton pasteToolBarButton;        
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator21;        
        internal System.Windows.Forms.ToolStripSeparator toolStripSeparator24;
        internal System.Windows.Forms.ToolStripSeparator toolStripSeparator25;
        private System.Windows.Forms.ToolStripMenuItem exportTransitionListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportReportMenuItem;
        // timeGraphContextMenuItem, regressionContextMenuItem, replicateComparisonContextMenuItem, schedulingContextMenuItem moved to RetentionTimesContextMenu
        private System.Windows.Forms.ToolStripMenuItem supportMenuItem;
        private System.Windows.Forms.ToolStripMenuItem issuesMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator29;
        private System.Windows.Forms.ToolStripMenuItem homeMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator31;
        private System.Windows.Forms.ToolStripMenuItem shareSettingsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importSettingsMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem exportMethodMenuItem;
        // contextMenuPeakAreas and peak area items moved to PeakAreasContextMenu
        private System.Windows.Forms.ToolStripMenuItem peptideOrderContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderDocumentContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderAreaContextMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem peptideCvsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentSettingsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refineToolStripMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem selectionContextMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem synchronizeSummaryZoomingContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem shareDocumentMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator37;
        private System.Windows.Forms.ToolStripMenuItem integrateAllMenuItem;
        internal System.Windows.Forms.ToolStripSeparator toolStripSeparator28;
        // timePeptideComparisonContextMenuItem, rtValueMenuItem, RT value items, timePropsContextMenuItem, toolStripSeparator38 moved to RetentionTimesContextMenu
        private System.Windows.Forms.ToolStripStatusLabel statusPeptides;
        private System.Windows.Forms.ToolStripMenuItem replicatesRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem averageReplicatesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleReplicateRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bestReplicateRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem videosMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tutorialsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importDocumentMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateOrderContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateOrderDocumentContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateOrderAcqTimeContextMenuItem;
        // showRTLegendContextMenuItem moved to RetentionTimesContextMenu
        private System.Windows.Forms.ToolStripMenuItem scopeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentScopeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem proteinScopeContextMenuItem;
        // chooseCalculatorContextMenuItem and sub-items moved to RetentionTimesContextMenu
        private System.Windows.Forms.ToolStripMenuItem eSPFeaturesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportIsolationListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsMenu;
        private System.Windows.Forms.ToolStripMenuItem placeholderToolsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem configureToolsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator47;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator46;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem publishMenuItem;
        private System.Windows.Forms.ToolStripMenuItem immediateWindowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importPeptideSearchMenuItem;
        private System.Windows.Forms.ToolStripMenuItem groupReplicatesByContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem groupByReplicateContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mProphetFeaturesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peakBoundariesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSplitButton buttonShowAllChromatograms;
        private System.Windows.Forms.ToolStripMenuItem chromatogramsToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton publishToolbarButton;
        private System.Windows.Forms.ToolStripMenuItem updatesToolsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator49;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator50;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator51;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator52;
        private System.Windows.Forms.ToolStripMenuItem toolStoreMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorTools;
        private System.Windows.Forms.ToolStripMenuItem startPageMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openContainingFolderMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator53;
        // timePlotContextMenuItem, timeCorrelation/Residuals, timePoints/Targets/Standards/Decoys moved to RetentionTimesContextMenu
        private System.Windows.Forms.ToolStripMenuItem peptideOrderMassErrorContextMenuItem;
        // scoreToRunToolStripMenuItem, runToRunToolStripMenuItem moved to RetentionTimesContextMenu
        private System.Windows.Forms.ToolStripSeparator checkForUpdatesSeparator;
        private System.Windows.Forms.ToolStripMenuItem checkForUpdatesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSpectralLibraryMenuItem;
        // setRegressionMethodContextMenuItem, linearRegression, kernelDensity, loess moved to RetentionTimesContextMenu
        private System.Windows.Forms.ToolStripMenuItem importAssayLibraryMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAnnotationsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importAnnotationsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorSelectUI;
        private System.Windows.Forms.ToolStripDropDownButton modeUIToolBarDropDownButton;
        // targetsAt1FDRToolStripMenuItem moved to RetentionTimesContextMenu
        private System.Windows.Forms.ToolStripMenuItem webinarsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reportsHelpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem commandLineHelpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem keyboardShortcutsHelpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem otherDocsHelpMenuItem;
        // logRegressionContextMenuItem moved to RetentionTimesContextMenu

        private System.Windows.Forms.ToolStripMenuItem submitErrorReportMenuItem;
        private System.Windows.Forms.ToolStripMenuItem crashSkylineMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openPanoramaMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runPeptideSearchToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem encyclopeDiaSearchMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFeatureDetectionMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ardiaPublishMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchToolsMenuItem;
    }
}

