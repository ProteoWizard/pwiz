
using System;
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
            this.contextMenuTreeNode = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.cutContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.pickChildrenContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removePeakContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.modifyPeptideContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.editNoteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorRatios = new System.Windows.Forms.ToolStripSeparator();
            this.ratiosContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.placeholderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicatesTreeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleReplicateTreeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bestReplicateTreeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuSpectrum = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.aionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.xionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.yionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.zionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorIonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.chargesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge1ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge2ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge3ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge4ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            this.ranksContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ionMzValuesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.observedMzValuesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicatesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator13 = new System.Windows.Forms.ToolStripSeparator();
            this.lockYaxisContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator14 = new System.Windows.Forms.ToolStripSeparator();
            this.spectrumPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator15 = new System.Windows.Forms.ToolStripSeparator();
            this.zoomSpectrumContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator27 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuChromatogram = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.removePeakGraphMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removePeaksGraphMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removePeaksGraphSubMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator33 = new System.Windows.Forms.ToolStripSeparator();
            this.legendChromContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peakBoundariesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.retentionTimesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bestRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.thresholdRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.noneRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.retentionTimePredContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideIDTimesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.alignedPeptideIDTimesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator16 = new System.Windows.Forms.ToolStripSeparator();
            this.transitionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorsTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.productsTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.totalTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transformChromContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transformChromNoneContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.secondDerivativeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.firstDerivativeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.smoothSGChromContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator17 = new System.Windows.Forms.ToolStripSeparator();
            this.autoZoomContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoZoomNoneContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoZoomBestPeakContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoZoomRTWindowContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoZoomBothContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lockYChromContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.synchronizeZoomingContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator18 = new System.Windows.Forms.ToolStripSeparator();
            this.chromPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator19 = new System.Windows.Forms.ToolStripSeparator();
            this.zoomChromContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator26 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuRetentionTimes = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.timeGraphContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timePeptideComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.linearRegressionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.schedulingContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideRTValueMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fwhmRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fwbRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showRTLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.alignRTToSelectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refineRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.predictionRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicatesRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.averageReplicatesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleReplicateRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bestReplicateRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setRTThresholdContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator22 = new System.Windows.Forms.ToolStripSeparator();
            this.createRTRegressionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.chooseCalculatorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.placeholderToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorCalculators = new System.Windows.Forms.ToolStripSeparator();
            this.addCalculatorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.updateCalculatorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator23 = new System.Windows.Forms.ToolStripSeparator();
            this.removeRTOutliersContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator24 = new System.Windows.Forms.ToolStripSeparator();
            this.timePropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator38 = new System.Windows.Forms.ToolStripSeparator();
            this.zoomOutRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator25 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuPeakAreas = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.areaGraphContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaReplicateComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaPeptideComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderDocumentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderAreaContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderDocumentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderAcqTimeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaNormalizeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaNormalizeMaximumContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaNormalizeTotalContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator40 = new System.Windows.Forms.ToolStripSeparator();
            this.areaNormalizeNoneContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentScopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.proteinScopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showPeakAreaLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLibraryPeakAreaContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showDotProductToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideLogScaleContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideCvsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator28 = new System.Windows.Forms.ToolStripSeparator();
            this.areaPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel1 = new System.Windows.Forms.Panel();
            this.dockPanel = new DigitalRune.Windows.Docking.DockPanel();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusGeneral = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusProgress = new System.Windows.Forms.ToolStripProgressBar();
            this.statusSequences = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusPeptides = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusPrecursors = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusIons = new System.Windows.Forms.ToolStripStatusLabel();
            this.mainToolStrip = new System.Windows.Forms.ToolStrip();
            this.newToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.openToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.saveToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator20 = new System.Windows.Forms.ToolStripSeparator();
            this.cutToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.copyToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.pasteToolBarButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator21 = new System.Windows.Forms.ToolStripSeparator();
            this.undoToolBarButton = new System.Windows.Forms.ToolStripSplitButton();
            this.redoToolBarButton = new System.Windows.Forms.ToolStripSplitButton();
            this.menuMain = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.shareDocumentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.importToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFASTAMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importMassListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importDocumentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportTransitionListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportIsolationListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportMethodMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportReportMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.eSPFeaturesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mruBeforeToolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.mruAfterToolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.undoMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.redoMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator34 = new System.Windows.Forms.ToolStripSeparator();
            this.cutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.findPeptideMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.findNextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
            this.editNoteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator42 = new System.Windows.Forms.ToolStripSeparator();
            this.insertToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.insertFASTAMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.insertProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.insertPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.insertTransitionListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refineToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeEmptyProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeDuplicatePeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeRepeatedPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeMissingResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator45 = new System.Windows.Forms.ToolStripSeparator();
            this.sortProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator43 = new System.Windows.Forms.ToolStripSeparator();
            this.acceptPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.generateDecoysMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator35 = new System.Windows.Forms.ToolStripSeparator();
            this.refineAdvancedMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.expandAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandPrecursorsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.collapseAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.collapseProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.collapsePeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.collapsePrecursorsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.modifyPeptideMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.manageUniquePeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator30 = new System.Windows.Forms.ToolStripSeparator();
            this.manageResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.textZoomToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.defaultTextToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.largeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extraLargeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator41 = new System.Windows.Forms.ToolStripSeparator();
            this.spectralLibrariesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator32 = new System.Windows.Forms.ToolStripSeparator();
            this.arrangeGraphsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arrangeTiledMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arrangedTabbedMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupedMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator39 = new System.Windows.Forms.ToolStripSeparator();
            this.graphsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ionTypesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.xMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.yMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.zMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorIonMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.chargesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge1MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge2MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge3MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge4MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ranksMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator9 = new System.Windows.Forms.ToolStripSeparator();
            this.chromatogramsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showChromMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorReplicates = new System.Windows.Forms.ToolStripSeparator();
            this.previousReplicateMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.nextReplicateMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator44 = new System.Windows.Forms.ToolStripSeparator();
            this.closeAllChromatogramsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transitionsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allTranMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorsTranMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.productsTranMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleTranMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.totalTranMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transformChromMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transformChromNoneMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.secondDerivativeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.firstDerivativeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.smoothSGChromMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoZoomMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoZoomNoneMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoZoomBestPeakMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoZoomRTWindowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoZoomBothMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator10 = new System.Windows.Forms.ToolStripSeparator();
            this.retentionTimesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateComparisonMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timePeptideComparisonMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.linearRegressionMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.schedulingMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.retentionTimeAlignmentsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peakAreasMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaReplicateComparisonMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaPeptideComparisonMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resultsGridMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator36 = new System.Windows.Forms.ToolStripSeparator();
            this.toolBarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.annotationsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator37 = new System.Windows.Forms.ToolStripSeparator();
            this.integrateAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.placeholderToolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator46 = new System.Windows.Forms.ToolStripSeparator();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator47 = new System.Windows.Forms.ToolStripSeparator();
            this.configureToolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.homeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.videosMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tutorialsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.supportMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.issuesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator29 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuTreeNode.SuspendLayout();
            this.contextMenuSpectrum.SuspendLayout();
            this.contextMenuChromatogram.SuspendLayout();
            this.contextMenuRetentionTimes.SuspendLayout();
            this.contextMenuPeakAreas.SuspendLayout();
            this.panel1.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.mainToolStrip.SuspendLayout();
            this.menuMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuTreeNode
            // 
            this.contextMenuTreeNode.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cutContextMenuItem,
            this.copyContextMenuItem,
            this.pasteContextMenuItem,
            this.deleteContextMenuItem,
            this.toolStripSeparator1,
            this.pickChildrenContextMenuItem,
            this.removePeakContextMenuItem,
            this.modifyPeptideContextMenuItem,
            this.toolStripSeparator7,
            this.editNoteContextMenuItem,
            this.toolStripSeparatorRatios,
            this.ratiosContextMenuItem,
            this.replicatesTreeContextMenuItem});
            this.contextMenuTreeNode.Name = "contextMenuTreeNode";
            this.contextMenuTreeNode.Size = new System.Drawing.Size(146, 242);
            this.contextMenuTreeNode.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuTreeNode_Opening);
            // 
            // cutContextMenuItem
            // 
            this.cutContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Cut;
            this.cutContextMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.cutContextMenuItem.Name = "cutContextMenuItem";
            this.cutContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.cutContextMenuItem.Text = "Cut";
            this.cutContextMenuItem.Click += new System.EventHandler(this.cutMenuItem_Click);
            // 
            // copyContextMenuItem
            // 
            this.copyContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Copy;
            this.copyContextMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.copyContextMenuItem.Name = "copyContextMenuItem";
            this.copyContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.copyContextMenuItem.Text = "Copy";
            this.copyContextMenuItem.Click += new System.EventHandler(this.copyMenuItem_Click);
            // 
            // pasteContextMenuItem
            // 
            this.pasteContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Paste;
            this.pasteContextMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.pasteContextMenuItem.Name = "pasteContextMenuItem";
            this.pasteContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.pasteContextMenuItem.Text = "Paste";
            this.pasteContextMenuItem.Click += new System.EventHandler(this.pasteMenuItem_Click);
            // 
            // deleteContextMenuItem
            // 
            this.deleteContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            this.deleteContextMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.deleteContextMenuItem.Name = "deleteContextMenuItem";
            this.deleteContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.deleteContextMenuItem.Text = "Delete";
            this.deleteContextMenuItem.Click += new System.EventHandler(this.deleteMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(142, 6);
            // 
            // pickChildrenContextMenuItem
            // 
            this.pickChildrenContextMenuItem.Name = "pickChildrenContextMenuItem";
            this.pickChildrenContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.pickChildrenContextMenuItem.Text = "Pick Children";
            this.pickChildrenContextMenuItem.Click += new System.EventHandler(this.pickChildrenContextMenuItem_Click);
            // 
            // removePeakContextMenuItem
            // 
            this.removePeakContextMenuItem.Name = "removePeakContextMenuItem";
            this.removePeakContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.removePeakContextMenuItem.Text = "Remove Peak";
            this.removePeakContextMenuItem.Click += new System.EventHandler(this.removePeakContextMenuItem_Click);
            // 
            // modifyPeptideContextMenuItem
            // 
            this.modifyPeptideContextMenuItem.Name = "modifyPeptideContextMenuItem";
            this.modifyPeptideContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.modifyPeptideContextMenuItem.Text = "Modify...";
            this.modifyPeptideContextMenuItem.Visible = false;
            this.modifyPeptideContextMenuItem.Click += new System.EventHandler(this.modifyPeptideMenuItem_Click);
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(142, 6);
            // 
            // editNoteContextMenuItem
            // 
            this.editNoteContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Comment;
            this.editNoteContextMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.editNoteContextMenuItem.Name = "editNoteContextMenuItem";
            this.editNoteContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.editNoteContextMenuItem.Text = "Edit Note";
            this.editNoteContextMenuItem.Click += new System.EventHandler(this.editNoteMenuItem_Click);
            // 
            // toolStripSeparatorRatios
            // 
            this.toolStripSeparatorRatios.Name = "toolStripSeparatorRatios";
            this.toolStripSeparatorRatios.Size = new System.Drawing.Size(142, 6);
            // 
            // ratiosContextMenuItem
            // 
            this.ratiosContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.placeholderToolStripMenuItem});
            this.ratiosContextMenuItem.Name = "ratiosContextMenuItem";
            this.ratiosContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.ratiosContextMenuItem.Text = "Ratios To";
            this.ratiosContextMenuItem.DropDownOpening += new System.EventHandler(this.ratiosContextMenuItem_DropDownOpening);
            // 
            // placeholderToolStripMenuItem
            // 
            this.placeholderToolStripMenuItem.Name = "placeholderToolStripMenuItem";
            this.placeholderToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.placeholderToolStripMenuItem.Text = "<placeholder>";
            // 
            // replicatesTreeContextMenuItem
            // 
            this.replicatesTreeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.singleReplicateTreeContextMenuItem,
            this.bestReplicateTreeContextMenuItem});
            this.replicatesTreeContextMenuItem.Name = "replicatesTreeContextMenuItem";
            this.replicatesTreeContextMenuItem.Size = new System.Drawing.Size(145, 22);
            this.replicatesTreeContextMenuItem.Text = "Replicates";
            this.replicatesTreeContextMenuItem.DropDownOpening += new System.EventHandler(this.replicatesTreeContextMenuItem_DropDownOpening);
            // 
            // singleReplicateTreeContextMenuItem
            // 
            this.singleReplicateTreeContextMenuItem.Name = "singleReplicateTreeContextMenuItem";
            this.singleReplicateTreeContextMenuItem.Size = new System.Drawing.Size(106, 22);
            this.singleReplicateTreeContextMenuItem.Text = "Single";
            this.singleReplicateTreeContextMenuItem.Click += new System.EventHandler(this.singleReplicateTreeContextMenuItem_Click);
            // 
            // bestReplicateTreeContextMenuItem
            // 
            this.bestReplicateTreeContextMenuItem.Name = "bestReplicateTreeContextMenuItem";
            this.bestReplicateTreeContextMenuItem.Size = new System.Drawing.Size(106, 22);
            this.bestReplicateTreeContextMenuItem.Text = "Best";
            this.bestReplicateTreeContextMenuItem.Click += new System.EventHandler(this.bestReplicateTreeContextMenuItem_Click);
            // 
            // contextMenuSpectrum
            // 
            this.contextMenuSpectrum.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aionsContextMenuItem,
            this.bionsContextMenuItem,
            this.cionsContextMenuItem,
            this.xionsContextMenuItem,
            this.yionsContextMenuItem,
            this.zionsContextMenuItem,
            this.precursorIonContextMenuItem,
            this.toolStripSeparator11,
            this.chargesContextMenuItem,
            this.toolStripSeparator12,
            this.ranksContextMenuItem,
            this.ionMzValuesContextMenuItem,
            this.observedMzValuesContextMenuItem,
            this.duplicatesContextMenuItem,
            this.toolStripSeparator13,
            this.lockYaxisContextMenuItem,
            this.toolStripSeparator14,
            this.spectrumPropsContextMenuItem,
            this.toolStripSeparator15,
            this.zoomSpectrumContextMenuItem,
            this.toolStripSeparator27});
            this.contextMenuSpectrum.Name = "contextMenuSpectrum";
            this.contextMenuSpectrum.Size = new System.Drawing.Size(186, 370);
            // 
            // aionsContextMenuItem
            // 
            this.aionsContextMenuItem.CheckOnClick = true;
            this.aionsContextMenuItem.Name = "aionsContextMenuItem";
            this.aionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.aionsContextMenuItem.Text = "A-ions";
            this.aionsContextMenuItem.Click += new System.EventHandler(this.aMenuItem_Click);
            // 
            // bionsContextMenuItem
            // 
            this.bionsContextMenuItem.CheckOnClick = true;
            this.bionsContextMenuItem.Name = "bionsContextMenuItem";
            this.bionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.bionsContextMenuItem.Text = "B-ions";
            this.bionsContextMenuItem.Click += new System.EventHandler(this.bMenuItem_Click);
            // 
            // cionsContextMenuItem
            // 
            this.cionsContextMenuItem.CheckOnClick = true;
            this.cionsContextMenuItem.Name = "cionsContextMenuItem";
            this.cionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.cionsContextMenuItem.Text = "C-ions";
            this.cionsContextMenuItem.Click += new System.EventHandler(this.cMenuItem_Click);
            // 
            // xionsContextMenuItem
            // 
            this.xionsContextMenuItem.CheckOnClick = true;
            this.xionsContextMenuItem.Name = "xionsContextMenuItem";
            this.xionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.xionsContextMenuItem.Text = "X-ions";
            this.xionsContextMenuItem.Click += new System.EventHandler(this.xMenuItem_Click);
            // 
            // yionsContextMenuItem
            // 
            this.yionsContextMenuItem.CheckOnClick = true;
            this.yionsContextMenuItem.Name = "yionsContextMenuItem";
            this.yionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.yionsContextMenuItem.Text = "Y-ions";
            this.yionsContextMenuItem.Click += new System.EventHandler(this.yMenuItem_Click);
            // 
            // zionsContextMenuItem
            // 
            this.zionsContextMenuItem.CheckOnClick = true;
            this.zionsContextMenuItem.Name = "zionsContextMenuItem";
            this.zionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.zionsContextMenuItem.Text = "Z-ions";
            this.zionsContextMenuItem.Click += new System.EventHandler(this.zMenuItem_Click);
            // 
            // precursorIonContextMenuItem
            // 
            this.precursorIonContextMenuItem.Name = "precursorIonContextMenuItem";
            this.precursorIonContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.precursorIonContextMenuItem.Text = "Precursor";
            this.precursorIonContextMenuItem.Click += new System.EventHandler(this.precursorIonMenuItem_Click);
            // 
            // toolStripSeparator11
            // 
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            this.toolStripSeparator11.Size = new System.Drawing.Size(182, 6);
            // 
            // chargesContextMenuItem
            // 
            this.chargesContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.charge1ContextMenuItem,
            this.charge2ContextMenuItem,
            this.charge3ContextMenuItem,
            this.charge4ContextMenuItem});
            this.chargesContextMenuItem.Name = "chargesContextMenuItem";
            this.chargesContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.chargesContextMenuItem.Text = "Charges";
            this.chargesContextMenuItem.DropDownOpening += new System.EventHandler(this.chargesMenuItem_DropDownOpening);
            // 
            // charge1ContextMenuItem
            // 
            this.charge1ContextMenuItem.Name = "charge1ContextMenuItem";
            this.charge1ContextMenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge1ContextMenuItem.Text = "1";
            this.charge1ContextMenuItem.Click += new System.EventHandler(this.charge1MenuItem_Click);
            // 
            // charge2ContextMenuItem
            // 
            this.charge2ContextMenuItem.Name = "charge2ContextMenuItem";
            this.charge2ContextMenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge2ContextMenuItem.Text = "2";
            this.charge2ContextMenuItem.Click += new System.EventHandler(this.charge2MenuItem_Click);
            // 
            // charge3ContextMenuItem
            // 
            this.charge3ContextMenuItem.Name = "charge3ContextMenuItem";
            this.charge3ContextMenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge3ContextMenuItem.Text = "3";
            this.charge3ContextMenuItem.Click += new System.EventHandler(this.charge3MenuItem_Click);
            // 
            // charge4ContextMenuItem
            // 
            this.charge4ContextMenuItem.Name = "charge4ContextMenuItem";
            this.charge4ContextMenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge4ContextMenuItem.Text = "4";
            this.charge4ContextMenuItem.Click += new System.EventHandler(this.charge4MenuItem_Click);
            // 
            // toolStripSeparator12
            // 
            this.toolStripSeparator12.Name = "toolStripSeparator12";
            this.toolStripSeparator12.Size = new System.Drawing.Size(182, 6);
            // 
            // ranksContextMenuItem
            // 
            this.ranksContextMenuItem.CheckOnClick = true;
            this.ranksContextMenuItem.Name = "ranksContextMenuItem";
            this.ranksContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.ranksContextMenuItem.Text = "Ranks";
            this.ranksContextMenuItem.Click += new System.EventHandler(this.ranksMenuItem_Click);
            // 
            // ionMzValuesContextMenuItem
            // 
            this.ionMzValuesContextMenuItem.CheckOnClick = true;
            this.ionMzValuesContextMenuItem.Name = "ionMzValuesContextMenuItem";
            this.ionMzValuesContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.ionMzValuesContextMenuItem.Text = "Ion m/z Values";
            this.ionMzValuesContextMenuItem.Click += new System.EventHandler(this.ionMzValuesContextMenuItem_Click);
            // 
            // observedMzValuesContextMenuItem
            // 
            this.observedMzValuesContextMenuItem.CheckOnClick = true;
            this.observedMzValuesContextMenuItem.Name = "observedMzValuesContextMenuItem";
            this.observedMzValuesContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.observedMzValuesContextMenuItem.Text = "Observed m/z Values";
            this.observedMzValuesContextMenuItem.Click += new System.EventHandler(this.observedMzValuesContextMenuItem_Click);
            // 
            // duplicatesContextMenuItem
            // 
            this.duplicatesContextMenuItem.CheckOnClick = true;
            this.duplicatesContextMenuItem.Name = "duplicatesContextMenuItem";
            this.duplicatesContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.duplicatesContextMenuItem.Text = "Duplicate Ions";
            this.duplicatesContextMenuItem.Click += new System.EventHandler(this.duplicatesContextMenuItem_Click);
            // 
            // toolStripSeparator13
            // 
            this.toolStripSeparator13.Name = "toolStripSeparator13";
            this.toolStripSeparator13.Size = new System.Drawing.Size(182, 6);
            // 
            // lockYaxisContextMenuItem
            // 
            this.lockYaxisContextMenuItem.CheckOnClick = true;
            this.lockYaxisContextMenuItem.Name = "lockYaxisContextMenuItem";
            this.lockYaxisContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.lockYaxisContextMenuItem.Text = "Auto-scale Y-axis";
            this.lockYaxisContextMenuItem.Click += new System.EventHandler(this.lockYaxisContextMenuItem_Click);
            // 
            // toolStripSeparator14
            // 
            this.toolStripSeparator14.Name = "toolStripSeparator14";
            this.toolStripSeparator14.Size = new System.Drawing.Size(182, 6);
            // 
            // spectrumPropsContextMenuItem
            // 
            this.spectrumPropsContextMenuItem.Name = "spectrumPropsContextMenuItem";
            this.spectrumPropsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.spectrumPropsContextMenuItem.Text = "Properties...";
            this.spectrumPropsContextMenuItem.Click += new System.EventHandler(this.spectrumPropsContextMenuItem_Click);
            // 
            // toolStripSeparator15
            // 
            this.toolStripSeparator15.Name = "toolStripSeparator15";
            this.toolStripSeparator15.Size = new System.Drawing.Size(182, 6);
            // 
            // zoomSpectrumContextMenuItem
            // 
            this.zoomSpectrumContextMenuItem.Name = "zoomSpectrumContextMenuItem";
            this.zoomSpectrumContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.zoomSpectrumContextMenuItem.Text = "Zoom Out";
            this.zoomSpectrumContextMenuItem.Click += new System.EventHandler(this.zoomSpectrumContextMenuItem_Click);
            // 
            // toolStripSeparator27
            // 
            this.toolStripSeparator27.Name = "toolStripSeparator27";
            this.toolStripSeparator27.Size = new System.Drawing.Size(182, 6);
            // 
            // contextMenuChromatogram
            // 
            this.contextMenuChromatogram.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.removePeakGraphMenuItem,
            this.removePeaksGraphMenuItem,
            this.toolStripSeparator33,
            this.legendChromContextMenuItem,
            this.peakBoundariesContextMenuItem,
            this.retentionTimesContextMenuItem,
            this.retentionTimePredContextMenuItem,
            this.peptideIDTimesContextMenuItem,
            this.alignedPeptideIDTimesToolStripMenuItem,
            this.toolStripSeparator16,
            this.transitionsContextMenuItem,
            this.transformChromContextMenuItem,
            this.toolStripSeparator17,
            this.autoZoomContextMenuItem,
            this.lockYChromContextMenuItem,
            this.synchronizeZoomingContextMenuItem,
            this.toolStripSeparator18,
            this.chromPropsContextMenuItem,
            this.toolStripSeparator19,
            this.zoomChromContextMenuItem,
            this.toolStripSeparator26});
            this.contextMenuChromatogram.Name = "contextMenuChromatogram";
            this.contextMenuChromatogram.Size = new System.Drawing.Size(213, 370);
            // 
            // removePeakGraphMenuItem
            // 
            this.removePeakGraphMenuItem.Name = "removePeakGraphMenuItem";
            this.removePeakGraphMenuItem.Size = new System.Drawing.Size(212, 22);
            this.removePeakGraphMenuItem.Text = "Remove Peak";
            this.removePeakGraphMenuItem.Click += new System.EventHandler(this.removePeakContextMenuItem_Click);
            // 
            // removePeaksGraphMenuItem
            // 
            this.removePeaksGraphMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.removePeaksGraphSubMenuItem});
            this.removePeaksGraphMenuItem.Name = "removePeaksGraphMenuItem";
            this.removePeaksGraphMenuItem.Size = new System.Drawing.Size(212, 22);
            this.removePeaksGraphMenuItem.Text = "Remove Peak";
            this.removePeaksGraphMenuItem.DropDownOpening += new System.EventHandler(this.removePeaksGraphMenuItem_DropDownOpening);
            // 
            // removePeaksGraphSubMenuItem
            // 
            this.removePeaksGraphSubMenuItem.Name = "removePeaksGraphSubMenuItem";
            this.removePeaksGraphSubMenuItem.Size = new System.Drawing.Size(152, 22);
            this.removePeaksGraphSubMenuItem.Text = "<placeholder>";
            // 
            // toolStripSeparator33
            // 
            this.toolStripSeparator33.Name = "toolStripSeparator33";
            this.toolStripSeparator33.Size = new System.Drawing.Size(209, 6);
            // 
            // legendChromContextMenuItem
            // 
            this.legendChromContextMenuItem.CheckOnClick = true;
            this.legendChromContextMenuItem.Name = "legendChromContextMenuItem";
            this.legendChromContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.legendChromContextMenuItem.Text = "Legend";
            this.legendChromContextMenuItem.Click += new System.EventHandler(this.legendChromContextMenuItem_Click);
            // 
            // peakBoundariesContextMenuItem
            // 
            this.peakBoundariesContextMenuItem.CheckOnClick = true;
            this.peakBoundariesContextMenuItem.Name = "peakBoundariesContextMenuItem";
            this.peakBoundariesContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.peakBoundariesContextMenuItem.Text = "Peak Boundaries";
            this.peakBoundariesContextMenuItem.Click += new System.EventHandler(this.peakBoundariesContextMenuItem_Click);
            // 
            // retentionTimesContextMenuItem
            // 
            this.retentionTimesContextMenuItem.CheckOnClick = true;
            this.retentionTimesContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allRTContextMenuItem,
            this.bestRTContextMenuItem,
            this.thresholdRTContextMenuItem,
            this.noneRTContextMenuItem});
            this.retentionTimesContextMenuItem.Name = "retentionTimesContextMenuItem";
            this.retentionTimesContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.retentionTimesContextMenuItem.Text = "Retention Times";
            this.retentionTimesContextMenuItem.DropDownOpening += new System.EventHandler(this.retentionTimesContextMenuItem_DropDownOpening);
            // 
            // allRTContextMenuItem
            // 
            this.allRTContextMenuItem.Name = "allRTContextMenuItem";
            this.allRTContextMenuItem.Size = new System.Drawing.Size(173, 22);
            this.allRTContextMenuItem.Text = "All";
            this.allRTContextMenuItem.Click += new System.EventHandler(this.allRTContextMenuItem_Click);
            // 
            // bestRTContextMenuItem
            // 
            this.bestRTContextMenuItem.Name = "bestRTContextMenuItem";
            this.bestRTContextMenuItem.Size = new System.Drawing.Size(173, 22);
            this.bestRTContextMenuItem.Text = "Best Peak";
            this.bestRTContextMenuItem.Click += new System.EventHandler(this.bestRTContextMenuItem_Click);
            // 
            // thresholdRTContextMenuItem
            // 
            this.thresholdRTContextMenuItem.Name = "thresholdRTContextMenuItem";
            this.thresholdRTContextMenuItem.Size = new System.Drawing.Size(173, 22);
            this.thresholdRTContextMenuItem.Text = "Above Threshold...";
            this.thresholdRTContextMenuItem.Click += new System.EventHandler(this.thresholdRTContextMenuItem_Click);
            // 
            // noneRTContextMenuItem
            // 
            this.noneRTContextMenuItem.Name = "noneRTContextMenuItem";
            this.noneRTContextMenuItem.Size = new System.Drawing.Size(173, 22);
            this.noneRTContextMenuItem.Text = "None";
            this.noneRTContextMenuItem.Click += new System.EventHandler(this.noneRTContextMenuItem_Click);
            // 
            // retentionTimePredContextMenuItem
            // 
            this.retentionTimePredContextMenuItem.CheckOnClick = true;
            this.retentionTimePredContextMenuItem.Name = "retentionTimePredContextMenuItem";
            this.retentionTimePredContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.retentionTimePredContextMenuItem.Text = "Retention Time Prediction";
            this.retentionTimePredContextMenuItem.Click += new System.EventHandler(this.retentionTimePredContextMenuItem_Click);
            // 
            // peptideIDTimesContextMenuItem
            // 
            this.peptideIDTimesContextMenuItem.CheckOnClick = true;
            this.peptideIDTimesContextMenuItem.Name = "peptideIDTimesContextMenuItem";
            this.peptideIDTimesContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.peptideIDTimesContextMenuItem.Text = "Peptide ID Times";
            this.peptideIDTimesContextMenuItem.Click += new System.EventHandler(this.peptideIDTimesContextMenuItem_Click);
            // 
            // alignedPeptideIDTimesToolStripMenuItem
            // 
            this.alignedPeptideIDTimesToolStripMenuItem.CheckOnClick = true;
            this.alignedPeptideIDTimesToolStripMenuItem.Name = "alignedPeptideIDTimesToolStripMenuItem";
            this.alignedPeptideIDTimesToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.alignedPeptideIDTimesToolStripMenuItem.Text = "Aligned Peptide ID Times";
            this.alignedPeptideIDTimesToolStripMenuItem.Click += new System.EventHandler(this.alignedPeptideIDTimesToolStripMenuItem_Click);
            // 
            // toolStripSeparator16
            // 
            this.toolStripSeparator16.Name = "toolStripSeparator16";
            this.toolStripSeparator16.Size = new System.Drawing.Size(209, 6);
            // 
            // transitionsContextMenuItem
            // 
            this.transitionsContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allTranContextMenuItem,
            this.precursorsTranContextMenuItem,
            this.productsTranContextMenuItem,
            this.singleTranContextMenuItem,
            this.totalTranContextMenuItem});
            this.transitionsContextMenuItem.Name = "transitionsContextMenuItem";
            this.transitionsContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.transitionsContextMenuItem.Text = "Transitions";
            this.transitionsContextMenuItem.DropDownOpening += new System.EventHandler(this.transitionsMenuItem_DropDownOpening);
            // 
            // allTranContextMenuItem
            // 
            this.allTranContextMenuItem.Name = "allTranContextMenuItem";
            this.allTranContextMenuItem.Size = new System.Drawing.Size(129, 22);
            this.allTranContextMenuItem.Text = "All";
            this.allTranContextMenuItem.Click += new System.EventHandler(this.allTranMenuItem_Click);
            // 
            // precursorsTranContextMenuItem
            // 
            this.precursorsTranContextMenuItem.Name = "precursorsTranContextMenuItem";
            this.precursorsTranContextMenuItem.Size = new System.Drawing.Size(129, 22);
            this.precursorsTranContextMenuItem.Text = "Precursors";
            this.precursorsTranContextMenuItem.Click += new System.EventHandler(this.precursorsTranMenuItem_Click);
            // 
            // productsTranContextMenuItem
            // 
            this.productsTranContextMenuItem.Name = "productsTranContextMenuItem";
            this.productsTranContextMenuItem.Size = new System.Drawing.Size(129, 22);
            this.productsTranContextMenuItem.Text = "Products";
            this.productsTranContextMenuItem.Click += new System.EventHandler(this.productsTranMenuItem_Click);
            // 
            // singleTranContextMenuItem
            // 
            this.singleTranContextMenuItem.Name = "singleTranContextMenuItem";
            this.singleTranContextMenuItem.Size = new System.Drawing.Size(129, 22);
            this.singleTranContextMenuItem.Text = "Single";
            this.singleTranContextMenuItem.Click += new System.EventHandler(this.singleTranMenuItem_Click);
            // 
            // totalTranContextMenuItem
            // 
            this.totalTranContextMenuItem.Name = "totalTranContextMenuItem";
            this.totalTranContextMenuItem.Size = new System.Drawing.Size(129, 22);
            this.totalTranContextMenuItem.Text = "Total";
            this.totalTranContextMenuItem.Click += new System.EventHandler(this.totalTranMenuItem_Click);
            // 
            // transformChromContextMenuItem
            // 
            this.transformChromContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.transformChromNoneContextMenuItem,
            this.secondDerivativeContextMenuItem,
            this.firstDerivativeContextMenuItem,
            this.smoothSGChromContextMenuItem});
            this.transformChromContextMenuItem.Name = "transformChromContextMenuItem";
            this.transformChromContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.transformChromContextMenuItem.Text = "Transform";
            this.transformChromContextMenuItem.DropDownOpening += new System.EventHandler(this.transformChromMenuItem_DropDownOpening);
            // 
            // transformChromNoneContextMenuItem
            // 
            this.transformChromNoneContextMenuItem.Name = "transformChromNoneContextMenuItem";
            this.transformChromNoneContextMenuItem.Size = new System.Drawing.Size(213, 22);
            this.transformChromNoneContextMenuItem.Text = "None";
            this.transformChromNoneContextMenuItem.Click += new System.EventHandler(this.transformChromNoneMenuItem_Click);
            // 
            // secondDerivativeContextMenuItem
            // 
            this.secondDerivativeContextMenuItem.Name = "secondDerivativeContextMenuItem";
            this.secondDerivativeContextMenuItem.Size = new System.Drawing.Size(213, 22);
            this.secondDerivativeContextMenuItem.Text = "Second Derivative";
            this.secondDerivativeContextMenuItem.Click += new System.EventHandler(this.secondDerivativeMenuItem_Click);
            // 
            // firstDerivativeContextMenuItem
            // 
            this.firstDerivativeContextMenuItem.Name = "firstDerivativeContextMenuItem";
            this.firstDerivativeContextMenuItem.Size = new System.Drawing.Size(213, 22);
            this.firstDerivativeContextMenuItem.Text = "First Derivative";
            this.firstDerivativeContextMenuItem.Click += new System.EventHandler(this.firstDerivativeMenuItem_Click);
            // 
            // smoothSGChromContextMenuItem
            // 
            this.smoothSGChromContextMenuItem.Name = "smoothSGChromContextMenuItem";
            this.smoothSGChromContextMenuItem.Size = new System.Drawing.Size(213, 22);
            this.smoothSGChromContextMenuItem.Text = "Savitzky-Golay Smoothing";
            this.smoothSGChromContextMenuItem.Click += new System.EventHandler(this.smoothSGChromMenuItem_Click);
            // 
            // toolStripSeparator17
            // 
            this.toolStripSeparator17.Name = "toolStripSeparator17";
            this.toolStripSeparator17.Size = new System.Drawing.Size(209, 6);
            // 
            // autoZoomContextMenuItem
            // 
            this.autoZoomContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.autoZoomNoneContextMenuItem,
            this.autoZoomBestPeakContextMenuItem,
            this.autoZoomRTWindowContextMenuItem,
            this.autoZoomBothContextMenuItem});
            this.autoZoomContextMenuItem.Name = "autoZoomContextMenuItem";
            this.autoZoomContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.autoZoomContextMenuItem.Text = "Auto-zoom X-axis";
            this.autoZoomContextMenuItem.DropDownOpening += new System.EventHandler(this.autozoomMenuItem_DropDownOpening);
            // 
            // autoZoomNoneContextMenuItem
            // 
            this.autoZoomNoneContextMenuItem.Name = "autoZoomNoneContextMenuItem";
            this.autoZoomNoneContextMenuItem.Size = new System.Drawing.Size(202, 22);
            this.autoZoomNoneContextMenuItem.Text = "None";
            this.autoZoomNoneContextMenuItem.Click += new System.EventHandler(this.autoZoomNoneMenuItem_Click);
            // 
            // autoZoomBestPeakContextMenuItem
            // 
            this.autoZoomBestPeakContextMenuItem.Name = "autoZoomBestPeakContextMenuItem";
            this.autoZoomBestPeakContextMenuItem.Size = new System.Drawing.Size(202, 22);
            this.autoZoomBestPeakContextMenuItem.Text = "Best Peak";
            this.autoZoomBestPeakContextMenuItem.Click += new System.EventHandler(this.autoZoomBestPeakMenuItem_Click);
            // 
            // autoZoomRTWindowContextMenuItem
            // 
            this.autoZoomRTWindowContextMenuItem.Name = "autoZoomRTWindowContextMenuItem";
            this.autoZoomRTWindowContextMenuItem.Size = new System.Drawing.Size(202, 22);
            this.autoZoomRTWindowContextMenuItem.Text = "Retention Time Window";
            this.autoZoomRTWindowContextMenuItem.Click += new System.EventHandler(this.autoZoomRTWindowMenuItem_Click);
            // 
            // autoZoomBothContextMenuItem
            // 
            this.autoZoomBothContextMenuItem.Name = "autoZoomBothContextMenuItem";
            this.autoZoomBothContextMenuItem.Size = new System.Drawing.Size(202, 22);
            this.autoZoomBothContextMenuItem.Text = "Both";
            this.autoZoomBothContextMenuItem.Click += new System.EventHandler(this.autoZoomBothMenuItem_Click);
            // 
            // lockYChromContextMenuItem
            // 
            this.lockYChromContextMenuItem.CheckOnClick = true;
            this.lockYChromContextMenuItem.Name = "lockYChromContextMenuItem";
            this.lockYChromContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.lockYChromContextMenuItem.Text = "Auto-scale Y-axis";
            this.lockYChromContextMenuItem.Click += new System.EventHandler(this.lockYChromContextMenuItem_Click);
            // 
            // synchronizeZoomingContextMenuItem
            // 
            this.synchronizeZoomingContextMenuItem.CheckOnClick = true;
            this.synchronizeZoomingContextMenuItem.Name = "synchronizeZoomingContextMenuItem";
            this.synchronizeZoomingContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.synchronizeZoomingContextMenuItem.Text = "Synchronize Zooming";
            this.synchronizeZoomingContextMenuItem.Click += new System.EventHandler(this.synchronizeZoomingContextMenuItem_Click);
            // 
            // toolStripSeparator18
            // 
            this.toolStripSeparator18.Name = "toolStripSeparator18";
            this.toolStripSeparator18.Size = new System.Drawing.Size(209, 6);
            // 
            // chromPropsContextMenuItem
            // 
            this.chromPropsContextMenuItem.Name = "chromPropsContextMenuItem";
            this.chromPropsContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.chromPropsContextMenuItem.Text = "Properties...";
            this.chromPropsContextMenuItem.Click += new System.EventHandler(this.chromPropsContextMenuItem_Click);
            // 
            // toolStripSeparator19
            // 
            this.toolStripSeparator19.Name = "toolStripSeparator19";
            this.toolStripSeparator19.Size = new System.Drawing.Size(209, 6);
            // 
            // zoomChromContextMenuItem
            // 
            this.zoomChromContextMenuItem.Name = "zoomChromContextMenuItem";
            this.zoomChromContextMenuItem.Size = new System.Drawing.Size(212, 22);
            this.zoomChromContextMenuItem.Text = "Zoom Out";
            // 
            // toolStripSeparator26
            // 
            this.toolStripSeparator26.Name = "toolStripSeparator26";
            this.toolStripSeparator26.Size = new System.Drawing.Size(209, 6);
            // 
            // contextMenuRetentionTimes
            // 
            this.contextMenuRetentionTimes.AllowMerge = false;
            this.contextMenuRetentionTimes.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.timeGraphContextMenuItem,
            this.peptideRTValueMenuItem,
            this.showRTLegendContextMenuItem,
            this.selectionContextMenuItem,
            this.alignRTToSelectionContextMenuItem,
            this.refineRTContextMenuItem,
            this.predictionRTContextMenuItem,
            this.replicatesRTContextMenuItem,
            this.setRTThresholdContextMenuItem,
            this.toolStripSeparator22,
            this.createRTRegressionContextMenuItem,
            this.chooseCalculatorContextMenuItem,
            this.toolStripSeparator23,
            this.removeRTOutliersContextMenuItem,
            this.removeRTContextMenuItem,
            this.toolStripSeparator24,
            this.timePropsContextMenuItem,
            this.toolStripSeparator38,
            this.zoomOutRTContextMenuItem,
            this.toolStripSeparator25});
            this.contextMenuRetentionTimes.Name = "contextMenuRetentionTimes";
            this.contextMenuRetentionTimes.Size = new System.Drawing.Size(178, 364);
            // 
            // timeGraphContextMenuItem
            // 
            this.timeGraphContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.replicateComparisonContextMenuItem,
            this.timePeptideComparisonContextMenuItem,
            this.linearRegressionContextMenuItem,
            this.schedulingContextMenuItem});
            this.timeGraphContextMenuItem.Name = "timeGraphContextMenuItem";
            this.timeGraphContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.timeGraphContextMenuItem.Text = "Graph";
            this.timeGraphContextMenuItem.DropDownOpening += new System.EventHandler(this.timeGraphMenuItem_DropDownOpening);
            // 
            // replicateComparisonContextMenuItem
            // 
            this.replicateComparisonContextMenuItem.CheckOnClick = true;
            this.replicateComparisonContextMenuItem.Name = "replicateComparisonContextMenuItem";
            this.replicateComparisonContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.replicateComparisonContextMenuItem.Text = "Replicate Comparison";
            this.replicateComparisonContextMenuItem.Click += new System.EventHandler(this.replicateComparisonMenuItem_Click);
            // 
            // timePeptideComparisonContextMenuItem
            // 
            this.timePeptideComparisonContextMenuItem.Name = "timePeptideComparisonContextMenuItem";
            this.timePeptideComparisonContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.timePeptideComparisonContextMenuItem.Text = "Peptide Comparison";
            this.timePeptideComparisonContextMenuItem.Click += new System.EventHandler(this.timePeptideComparisonMenuItem_Click);
            // 
            // linearRegressionContextMenuItem
            // 
            this.linearRegressionContextMenuItem.CheckOnClick = true;
            this.linearRegressionContextMenuItem.Name = "linearRegressionContextMenuItem";
            this.linearRegressionContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.linearRegressionContextMenuItem.Text = "Linear Regression";
            this.linearRegressionContextMenuItem.Click += new System.EventHandler(this.linearRegressionMenuItem_Click);
            // 
            // schedulingContextMenuItem
            // 
            this.schedulingContextMenuItem.Name = "schedulingContextMenuItem";
            this.schedulingContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.schedulingContextMenuItem.Text = "Scheduling";
            this.schedulingContextMenuItem.Click += new System.EventHandler(this.schedulingMenuItem_Click);
            // 
            // peptideRTValueMenuItem
            // 
            this.peptideRTValueMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allRTValueContextMenuItem,
            this.timeRTValueContextMenuItem,
            this.fwhmRTValueContextMenuItem,
            this.fwbRTValueContextMenuItem});
            this.peptideRTValueMenuItem.Name = "peptideRTValueMenuItem";
            this.peptideRTValueMenuItem.Size = new System.Drawing.Size(177, 22);
            this.peptideRTValueMenuItem.Text = "Value";
            this.peptideRTValueMenuItem.DropDownOpening += new System.EventHandler(this.peptideRTValueMenuItem_DropDownOpening);
            // 
            // allRTValueContextMenuItem
            // 
            this.allRTValueContextMenuItem.Name = "allRTValueContextMenuItem";
            this.allRTValueContextMenuItem.Size = new System.Drawing.Size(155, 22);
            this.allRTValueContextMenuItem.Text = "All";
            this.allRTValueContextMenuItem.Click += new System.EventHandler(this.allRTValueContextMenuItem_Click);
            // 
            // timeRTValueContextMenuItem
            // 
            this.timeRTValueContextMenuItem.Name = "timeRTValueContextMenuItem";
            this.timeRTValueContextMenuItem.Size = new System.Drawing.Size(155, 22);
            this.timeRTValueContextMenuItem.Text = "Retention Time";
            this.timeRTValueContextMenuItem.Click += new System.EventHandler(this.timeRTValueContextMenuItem_Click);
            // 
            // fwhmRTValueContextMenuItem
            // 
            this.fwhmRTValueContextMenuItem.Name = "fwhmRTValueContextMenuItem";
            this.fwhmRTValueContextMenuItem.Size = new System.Drawing.Size(155, 22);
            this.fwhmRTValueContextMenuItem.Text = "FWHM";
            this.fwhmRTValueContextMenuItem.Click += new System.EventHandler(this.fwhmRTValueContextMenuItem_Click);
            // 
            // fwbRTValueContextMenuItem
            // 
            this.fwbRTValueContextMenuItem.Name = "fwbRTValueContextMenuItem";
            this.fwbRTValueContextMenuItem.Size = new System.Drawing.Size(155, 22);
            this.fwbRTValueContextMenuItem.Text = "FWB";
            this.fwbRTValueContextMenuItem.Click += new System.EventHandler(this.fwbRTValueContextMenuItem_Click);
            // 
            // showRTLegendContextMenuItem
            // 
            this.showRTLegendContextMenuItem.Name = "showRTLegendContextMenuItem";
            this.showRTLegendContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.showRTLegendContextMenuItem.Text = "Legend";
            this.showRTLegendContextMenuItem.Click += new System.EventHandler(this.showRTLegendContextMenuItem_Click);
            // 
            // selectionContextMenuItem
            // 
            this.selectionContextMenuItem.CheckOnClick = true;
            this.selectionContextMenuItem.Name = "selectionContextMenuItem";
            this.selectionContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.selectionContextMenuItem.Text = "Selection";
            this.selectionContextMenuItem.Click += new System.EventHandler(this.selectionContextMenuItem_Click);
            // 
            // alignRTToSelectionContextMenuItem
            // 
            this.alignRTToSelectionContextMenuItem.CheckOnClick = true;
            this.alignRTToSelectionContextMenuItem.Name = "alignRTToSelectionContextMenuItem";
            this.alignRTToSelectionContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.alignRTToSelectionContextMenuItem.Text = "Align Times To {0}";
            this.alignRTToSelectionContextMenuItem.Click += new System.EventHandler(this.alignRTToSelectionContextMenuItem_Click);
            // 
            // refineRTContextMenuItem
            // 
            this.refineRTContextMenuItem.CheckOnClick = true;
            this.refineRTContextMenuItem.Name = "refineRTContextMenuItem";
            this.refineRTContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.refineRTContextMenuItem.Text = "Refine";
            this.refineRTContextMenuItem.Click += new System.EventHandler(this.refineRTContextMenuItem_Click);
            // 
            // predictionRTContextMenuItem
            // 
            this.predictionRTContextMenuItem.CheckOnClick = true;
            this.predictionRTContextMenuItem.Name = "predictionRTContextMenuItem";
            this.predictionRTContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.predictionRTContextMenuItem.Text = "Prediction";
            this.predictionRTContextMenuItem.Click += new System.EventHandler(this.predictionRTContextMenuItem_Click);
            // 
            // replicatesRTContextMenuItem
            // 
            this.replicatesRTContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.averageReplicatesContextMenuItem,
            this.singleReplicateRTContextMenuItem,
            this.bestReplicateRTContextMenuItem});
            this.replicatesRTContextMenuItem.Name = "replicatesRTContextMenuItem";
            this.replicatesRTContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.replicatesRTContextMenuItem.Text = "Replicates";
            this.replicatesRTContextMenuItem.DropDownOpening += new System.EventHandler(this.replicatesRTContextMenuItem_DropDownOpening);
            // 
            // averageReplicatesContextMenuItem
            // 
            this.averageReplicatesContextMenuItem.Name = "averageReplicatesContextMenuItem";
            this.averageReplicatesContextMenuItem.Size = new System.Drawing.Size(106, 22);
            this.averageReplicatesContextMenuItem.Text = "All";
            this.averageReplicatesContextMenuItem.Click += new System.EventHandler(this.averageReplicatesContextMenuItem_Click);
            // 
            // singleReplicateRTContextMenuItem
            // 
            this.singleReplicateRTContextMenuItem.Name = "singleReplicateRTContextMenuItem";
            this.singleReplicateRTContextMenuItem.Size = new System.Drawing.Size(106, 22);
            this.singleReplicateRTContextMenuItem.Text = "Single";
            this.singleReplicateRTContextMenuItem.Click += new System.EventHandler(this.singleReplicateRTContextMenuItem_Click);
            // 
            // bestReplicateRTContextMenuItem
            // 
            this.bestReplicateRTContextMenuItem.Name = "bestReplicateRTContextMenuItem";
            this.bestReplicateRTContextMenuItem.Size = new System.Drawing.Size(106, 22);
            this.bestReplicateRTContextMenuItem.Text = "Best";
            this.bestReplicateRTContextMenuItem.Click += new System.EventHandler(this.bestReplicateRTContextMenuItem_Click);
            // 
            // setRTThresholdContextMenuItem
            // 
            this.setRTThresholdContextMenuItem.Name = "setRTThresholdContextMenuItem";
            this.setRTThresholdContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.setRTThresholdContextMenuItem.Text = "Set Threshold...";
            this.setRTThresholdContextMenuItem.Click += new System.EventHandler(this.setRTThresholdContextMenuItem_Click);
            // 
            // toolStripSeparator22
            // 
            this.toolStripSeparator22.Name = "toolStripSeparator22";
            this.toolStripSeparator22.Size = new System.Drawing.Size(174, 6);
            // 
            // createRTRegressionContextMenuItem
            // 
            this.createRTRegressionContextMenuItem.Name = "createRTRegressionContextMenuItem";
            this.createRTRegressionContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.createRTRegressionContextMenuItem.Text = "Create Regression...";
            this.createRTRegressionContextMenuItem.Click += new System.EventHandler(this.createRTRegressionContextMenuItem_Click);
            // 
            // chooseCalculatorContextMenuItem
            // 
            this.chooseCalculatorContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.placeholderToolStripMenuItem1,
            this.toolStripSeparatorCalculators,
            this.addCalculatorContextMenuItem,
            this.updateCalculatorContextMenuItem});
            this.chooseCalculatorContextMenuItem.Name = "chooseCalculatorContextMenuItem";
            this.chooseCalculatorContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.chooseCalculatorContextMenuItem.Text = "Calculator";
            this.chooseCalculatorContextMenuItem.DropDownOpening += new System.EventHandler(this.chooseCalculatorContextMenuItem_DropDownOpening);
            // 
            // placeholderToolStripMenuItem1
            // 
            this.placeholderToolStripMenuItem1.Name = "placeholderToolStripMenuItem1";
            this.placeholderToolStripMenuItem1.Size = new System.Drawing.Size(152, 22);
            this.placeholderToolStripMenuItem1.Text = "<placeholder>";
            // 
            // toolStripSeparatorCalculators
            // 
            this.toolStripSeparatorCalculators.Name = "toolStripSeparatorCalculators";
            this.toolStripSeparatorCalculators.Size = new System.Drawing.Size(149, 6);
            // 
            // addCalculatorContextMenuItem
            // 
            this.addCalculatorContextMenuItem.Name = "addCalculatorContextMenuItem";
            this.addCalculatorContextMenuItem.Size = new System.Drawing.Size(152, 22);
            this.addCalculatorContextMenuItem.Text = "Add...";
            this.addCalculatorContextMenuItem.Click += new System.EventHandler(this.addCalculatorContextMenuItem_Click);
            // 
            // updateCalculatorContextMenuItem
            // 
            this.updateCalculatorContextMenuItem.Name = "updateCalculatorContextMenuItem";
            this.updateCalculatorContextMenuItem.Size = new System.Drawing.Size(152, 22);
            this.updateCalculatorContextMenuItem.Text = "Edit Current...";
            this.updateCalculatorContextMenuItem.Click += new System.EventHandler(this.updateCalculatorContextMenuItem_Click);
            // 
            // toolStripSeparator23
            // 
            this.toolStripSeparator23.Name = "toolStripSeparator23";
            this.toolStripSeparator23.Size = new System.Drawing.Size(174, 6);
            // 
            // removeRTOutliersContextMenuItem
            // 
            this.removeRTOutliersContextMenuItem.Name = "removeRTOutliersContextMenuItem";
            this.removeRTOutliersContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.removeRTOutliersContextMenuItem.Text = "Remove Outliers";
            this.removeRTOutliersContextMenuItem.Click += new System.EventHandler(this.removeRTOutliersContextMenuItem_Click);
            // 
            // removeRTContextMenuItem
            // 
            this.removeRTContextMenuItem.Name = "removeRTContextMenuItem";
            this.removeRTContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.removeRTContextMenuItem.Text = "Remove";
            this.removeRTContextMenuItem.Click += new System.EventHandler(this.removeRTContextMenuItem_Click);
            // 
            // toolStripSeparator24
            // 
            this.toolStripSeparator24.Name = "toolStripSeparator24";
            this.toolStripSeparator24.Size = new System.Drawing.Size(174, 6);
            // 
            // timePropsContextMenuItem
            // 
            this.timePropsContextMenuItem.Name = "timePropsContextMenuItem";
            this.timePropsContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.timePropsContextMenuItem.Text = "Properties...";
            this.timePropsContextMenuItem.Click += new System.EventHandler(this.timePropsContextMenuItem_Click);
            // 
            // toolStripSeparator38
            // 
            this.toolStripSeparator38.Name = "toolStripSeparator38";
            this.toolStripSeparator38.Size = new System.Drawing.Size(174, 6);
            // 
            // zoomOutRTContextMenuItem
            // 
            this.zoomOutRTContextMenuItem.Name = "zoomOutRTContextMenuItem";
            this.zoomOutRTContextMenuItem.Size = new System.Drawing.Size(177, 22);
            this.zoomOutRTContextMenuItem.Text = "Zoom Out";
            // 
            // toolStripSeparator25
            // 
            this.toolStripSeparator25.Name = "toolStripSeparator25";
            this.toolStripSeparator25.Size = new System.Drawing.Size(174, 6);
            // 
            // contextMenuPeakAreas
            // 
            this.contextMenuPeakAreas.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaGraphContextMenuItem,
            this.peptideOrderContextMenuItem,
            this.replicateOrderContextMenuItem,
            this.areaNormalizeContextMenuItem,
            this.scopeContextMenuItem,
            this.showPeakAreaLegendContextMenuItem,
            this.showLibraryPeakAreaContextMenuItem,
            this.showDotProductToolStripMenuItem,
            this.peptideLogScaleContextMenuItem,
            this.peptideCvsContextMenuItem,
            this.toolStripSeparator28,
            this.areaPropsContextMenuItem});
            this.contextMenuPeakAreas.Name = "contextMenuStrip1";
            this.contextMenuPeakAreas.Size = new System.Drawing.Size(171, 252);
            // 
            // areaGraphContextMenuItem
            // 
            this.areaGraphContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaReplicateComparisonContextMenuItem,
            this.areaPeptideComparisonContextMenuItem});
            this.areaGraphContextMenuItem.Name = "areaGraphContextMenuItem";
            this.areaGraphContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.areaGraphContextMenuItem.Text = "Graph";
            this.areaGraphContextMenuItem.DropDownOpening += new System.EventHandler(this.areaGraphMenuItem_DropDownOpening);
            // 
            // areaReplicateComparisonContextMenuItem
            // 
            this.areaReplicateComparisonContextMenuItem.Name = "areaReplicateComparisonContextMenuItem";
            this.areaReplicateComparisonContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.areaReplicateComparisonContextMenuItem.Text = "Replicate Comparison";
            this.areaReplicateComparisonContextMenuItem.Click += new System.EventHandler(this.areaReplicateComparisonMenuItem_Click);
            // 
            // areaPeptideComparisonContextMenuItem
            // 
            this.areaPeptideComparisonContextMenuItem.Name = "areaPeptideComparisonContextMenuItem";
            this.areaPeptideComparisonContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.areaPeptideComparisonContextMenuItem.Text = "Peptide Comparison";
            this.areaPeptideComparisonContextMenuItem.Click += new System.EventHandler(this.areaPeptideComparisonMenuItem_Click);
            // 
            // peptideOrderContextMenuItem
            // 
            this.peptideOrderContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.peptideOrderDocumentContextMenuItem,
            this.peptideOrderRTContextMenuItem,
            this.peptideOrderAreaContextMenuItem});
            this.peptideOrderContextMenuItem.Name = "peptideOrderContextMenuItem";
            this.peptideOrderContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.peptideOrderContextMenuItem.Text = "Order";
            this.peptideOrderContextMenuItem.DropDownOpening += new System.EventHandler(this.peptideOrderContextMenuItem_DropDownOpening);
            // 
            // peptideOrderDocumentContextMenuItem
            // 
            this.peptideOrderDocumentContextMenuItem.Name = "peptideOrderDocumentContextMenuItem";
            this.peptideOrderDocumentContextMenuItem.Size = new System.Drawing.Size(155, 22);
            this.peptideOrderDocumentContextMenuItem.Text = "Document";
            this.peptideOrderDocumentContextMenuItem.Click += new System.EventHandler(this.peptideOrderDocumentContextMenuItem_Click);
            // 
            // peptideOrderRTContextMenuItem
            // 
            this.peptideOrderRTContextMenuItem.Name = "peptideOrderRTContextMenuItem";
            this.peptideOrderRTContextMenuItem.Size = new System.Drawing.Size(155, 22);
            this.peptideOrderRTContextMenuItem.Text = "Retention Time";
            this.peptideOrderRTContextMenuItem.Click += new System.EventHandler(this.peptideOrderRTContextMenuItem_Click);
            // 
            // peptideOrderAreaContextMenuItem
            // 
            this.peptideOrderAreaContextMenuItem.Name = "peptideOrderAreaContextMenuItem";
            this.peptideOrderAreaContextMenuItem.Size = new System.Drawing.Size(155, 22);
            this.peptideOrderAreaContextMenuItem.Text = "Peak Area";
            this.peptideOrderAreaContextMenuItem.Click += new System.EventHandler(this.peptideOrderAreaContextMenuItem_Click);
            // 
            // replicateOrderContextMenuItem
            // 
            this.replicateOrderContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.replicateOrderDocumentContextMenuItem,
            this.replicateOrderAcqTimeContextMenuItem});
            this.replicateOrderContextMenuItem.Name = "replicateOrderContextMenuItem";
            this.replicateOrderContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.replicateOrderContextMenuItem.Text = "Order";
            this.replicateOrderContextMenuItem.DropDownOpening += new System.EventHandler(this.replicateOrderContextMenuItem_DropDownOpening);
            // 
            // replicateOrderDocumentContextMenuItem
            // 
            this.replicateOrderDocumentContextMenuItem.Name = "replicateOrderDocumentContextMenuItem";
            this.replicateOrderDocumentContextMenuItem.Size = new System.Drawing.Size(152, 22);
            this.replicateOrderDocumentContextMenuItem.Text = "Document";
            this.replicateOrderDocumentContextMenuItem.Click += new System.EventHandler(this.replicateOrderDocumentContextMenuItem_Click);
            // 
            // replicateOrderAcqTimeContextMenuItem
            // 
            this.replicateOrderAcqTimeContextMenuItem.Name = "replicateOrderAcqTimeContextMenuItem";
            this.replicateOrderAcqTimeContextMenuItem.Size = new System.Drawing.Size(152, 22);
            this.replicateOrderAcqTimeContextMenuItem.Text = "Acquired Time";
            this.replicateOrderAcqTimeContextMenuItem.Click += new System.EventHandler(this.replicateOrderAcqTimeContextMenuItem_Click);
            // 
            // areaNormalizeContextMenuItem
            // 
            this.areaNormalizeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaNormalizeMaximumContextMenuItem,
            this.areaNormalizeTotalContextMenuItem,
            this.toolStripSeparator40,
            this.areaNormalizeNoneContextMenuItem});
            this.areaNormalizeContextMenuItem.Name = "areaNormalizeContextMenuItem";
            this.areaNormalizeContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.areaNormalizeContextMenuItem.Text = "Normalized To";
            this.areaNormalizeContextMenuItem.DropDownOpening += new System.EventHandler(this.areaNormalizeContextMenuItem_DropDownOpening);
            // 
            // areaNormalizeMaximumContextMenuItem
            // 
            this.areaNormalizeMaximumContextMenuItem.Name = "areaNormalizeMaximumContextMenuItem";
            this.areaNormalizeMaximumContextMenuItem.Size = new System.Drawing.Size(128, 22);
            this.areaNormalizeMaximumContextMenuItem.Text = "Maximum";
            this.areaNormalizeMaximumContextMenuItem.Click += new System.EventHandler(this.areaNormalizeMaximumContextMenuItem_Click);
            // 
            // areaNormalizeTotalContextMenuItem
            // 
            this.areaNormalizeTotalContextMenuItem.Name = "areaNormalizeTotalContextMenuItem";
            this.areaNormalizeTotalContextMenuItem.Size = new System.Drawing.Size(128, 22);
            this.areaNormalizeTotalContextMenuItem.Text = "Total";
            this.areaNormalizeTotalContextMenuItem.Click += new System.EventHandler(this.areaNormalizeTotalContextMenuItem_Click);
            // 
            // toolStripSeparator40
            // 
            this.toolStripSeparator40.Name = "toolStripSeparator40";
            this.toolStripSeparator40.Size = new System.Drawing.Size(125, 6);
            // 
            // areaNormalizeNoneContextMenuItem
            // 
            this.areaNormalizeNoneContextMenuItem.Name = "areaNormalizeNoneContextMenuItem";
            this.areaNormalizeNoneContextMenuItem.Size = new System.Drawing.Size(128, 22);
            this.areaNormalizeNoneContextMenuItem.Text = "None";
            this.areaNormalizeNoneContextMenuItem.Click += new System.EventHandler(this.areaNormalizeNoneContextMenuItem_Click);
            // 
            // scopeContextMenuItem
            // 
            this.scopeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.documentScopeContextMenuItem,
            this.proteinScopeContextMenuItem});
            this.scopeContextMenuItem.Name = "scopeContextMenuItem";
            this.scopeContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.scopeContextMenuItem.Text = "Scope";
            this.scopeContextMenuItem.DropDownOpening += new System.EventHandler(this.scopeContextMenuItem_DropDownOpening);
            // 
            // documentScopeContextMenuItem
            // 
            this.documentScopeContextMenuItem.Name = "documentScopeContextMenuItem";
            this.documentScopeContextMenuItem.Size = new System.Drawing.Size(130, 22);
            this.documentScopeContextMenuItem.Text = "Document";
            this.documentScopeContextMenuItem.Click += new System.EventHandler(this.documentScopeContextMenuItem_Click);
            // 
            // proteinScopeContextMenuItem
            // 
            this.proteinScopeContextMenuItem.Name = "proteinScopeContextMenuItem";
            this.proteinScopeContextMenuItem.Size = new System.Drawing.Size(130, 22);
            this.proteinScopeContextMenuItem.Text = "Protein";
            this.proteinScopeContextMenuItem.Click += new System.EventHandler(this.proteinScopeContextMenuItem_Click);
            // 
            // showPeakAreaLegendContextMenuItem
            // 
            this.showPeakAreaLegendContextMenuItem.Name = "showPeakAreaLegendContextMenuItem";
            this.showPeakAreaLegendContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.showPeakAreaLegendContextMenuItem.Text = "Legend";
            this.showPeakAreaLegendContextMenuItem.Click += new System.EventHandler(this.showPeakAreaLegendContextMenuItem_Click);
            // 
            // showLibraryPeakAreaContextMenuItem
            // 
            this.showLibraryPeakAreaContextMenuItem.CheckOnClick = true;
            this.showLibraryPeakAreaContextMenuItem.Name = "showLibraryPeakAreaContextMenuItem";
            this.showLibraryPeakAreaContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.showLibraryPeakAreaContextMenuItem.Text = "Show Library";
            this.showLibraryPeakAreaContextMenuItem.Click += new System.EventHandler(this.showLibraryPeakAreaContextMenuItem_Click);
            // 
            // showDotProductToolStripMenuItem
            // 
            this.showDotProductToolStripMenuItem.Name = "showDotProductToolStripMenuItem";
            this.showDotProductToolStripMenuItem.Size = new System.Drawing.Size(170, 22);
            this.showDotProductToolStripMenuItem.Text = "Show Dot Product";
            this.showDotProductToolStripMenuItem.Click += new System.EventHandler(this.showDotProductToolStripMenuItem_Click);
            // 
            // peptideLogScaleContextMenuItem
            // 
            this.peptideLogScaleContextMenuItem.CheckOnClick = true;
            this.peptideLogScaleContextMenuItem.Name = "peptideLogScaleContextMenuItem";
            this.peptideLogScaleContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.peptideLogScaleContextMenuItem.Text = "Log Scale";
            this.peptideLogScaleContextMenuItem.Click += new System.EventHandler(this.peptideLogScaleContextMenuItem_Click);
            // 
            // peptideCvsContextMenuItem
            // 
            this.peptideCvsContextMenuItem.CheckOnClick = true;
            this.peptideCvsContextMenuItem.Name = "peptideCvsContextMenuItem";
            this.peptideCvsContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.peptideCvsContextMenuItem.Text = "CV Values";
            this.peptideCvsContextMenuItem.Click += new System.EventHandler(this.peptideCvsContextMenuItem_Click);
            // 
            // toolStripSeparator28
            // 
            this.toolStripSeparator28.Name = "toolStripSeparator28";
            this.toolStripSeparator28.Size = new System.Drawing.Size(167, 6);
            // 
            // areaPropsContextMenuItem
            // 
            this.areaPropsContextMenuItem.Name = "areaPropsContextMenuItem";
            this.areaPropsContextMenuItem.Size = new System.Drawing.Size(170, 22);
            this.areaPropsContextMenuItem.Text = "Properties...";
            this.areaPropsContextMenuItem.Click += new System.EventHandler(this.areaPropsContextMenuItem_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.dockPanel);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 49);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(734, 443);
            this.panel1.TabIndex = 0;
            // 
            // dockPanel
            // 
            this.dockPanel.ActiveAutoHideContent = null;
            this.dockPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dockPanel.Location = new System.Drawing.Point(-1, 0);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Size = new System.Drawing.Size(736, 444);
            this.dockPanel.TabIndex = 0;
            this.dockPanel.ActiveDocumentChanged += new System.EventHandler(this.dockPanel_ActiveDocumentChanged);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusGeneral,
            this.statusProgress,
            this.statusSequences,
            this.statusPeptides,
            this.statusPrecursors,
            this.statusIons});
            this.statusStrip.Location = new System.Drawing.Point(0, 492);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(734, 22);
            this.statusStrip.TabIndex = 3;
            // 
            // statusGeneral
            // 
            this.statusGeneral.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusGeneral.Name = "statusGeneral";
            this.statusGeneral.Size = new System.Drawing.Size(379, 17);
            this.statusGeneral.Spring = true;
            this.statusGeneral.Text = "Ready";
            this.statusGeneral.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // statusProgress
            // 
            this.statusProgress.Name = "statusProgress";
            this.statusProgress.Size = new System.Drawing.Size(100, 16);
            this.statusProgress.Visible = false;
            // 
            // statusSequences
            // 
            this.statusSequences.AutoSize = false;
            this.statusSequences.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusSequences.Name = "statusSequences";
            this.statusSequences.Size = new System.Drawing.Size(75, 17);
            this.statusSequences.Text = "0 prot";
            this.statusSequences.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // statusPeptides
            // 
            this.statusPeptides.AutoSize = false;
            this.statusPeptides.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusPeptides.Name = "statusPeptides";
            this.statusPeptides.Size = new System.Drawing.Size(85, 17);
            this.statusPeptides.Text = "0 pep";
            this.statusPeptides.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // statusPrecursors
            // 
            this.statusPrecursors.AutoSize = false;
            this.statusPrecursors.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusPrecursors.Name = "statusPrecursors";
            this.statusPrecursors.Size = new System.Drawing.Size(85, 17);
            this.statusPrecursors.Text = "0 prec";
            this.statusPrecursors.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // statusIons
            // 
            this.statusIons.AutoSize = false;
            this.statusIons.Name = "statusIons";
            this.statusIons.Size = new System.Drawing.Size(95, 17);
            this.statusIons.Text = "0 tran";
            this.statusIons.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // mainToolStrip
            // 
            this.mainToolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.mainToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolBarButton,
            this.openToolBarButton,
            this.saveToolBarButton,
            this.toolStripSeparator20,
            this.cutToolBarButton,
            this.copyToolBarButton,
            this.pasteToolBarButton,
            this.toolStripSeparator21,
            this.undoToolBarButton,
            this.redoToolBarButton});
            this.mainToolStrip.Location = new System.Drawing.Point(0, 24);
            this.mainToolStrip.Name = "mainToolStrip";
            this.mainToolStrip.Size = new System.Drawing.Size(734, 25);
            this.mainToolStrip.TabIndex = 5;
            this.mainToolStrip.Text = "toolStrip1";
            // 
            // newToolBarButton
            // 
            this.newToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.newToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.NewDocument;
            this.newToolBarButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.newToolBarButton.Name = "newToolBarButton";
            this.newToolBarButton.Size = new System.Drawing.Size(23, 22);
            this.newToolBarButton.Text = "New Document";
            this.newToolBarButton.Click += new System.EventHandler(this.newMenuItem_Click);
            // 
            // openToolBarButton
            // 
            this.openToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.openToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.OpenFolder;
            this.openToolBarButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.openToolBarButton.Name = "openToolBarButton";
            this.openToolBarButton.Size = new System.Drawing.Size(23, 22);
            this.openToolBarButton.Text = "Open";
            this.openToolBarButton.Click += new System.EventHandler(this.openMenuItem_Click);
            // 
            // saveToolBarButton
            // 
            this.saveToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.saveToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Save;
            this.saveToolBarButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.saveToolBarButton.Name = "saveToolBarButton";
            this.saveToolBarButton.Size = new System.Drawing.Size(23, 22);
            this.saveToolBarButton.Text = "Save";
            this.saveToolBarButton.Click += new System.EventHandler(this.saveMenuItem_Click);
            // 
            // toolStripSeparator20
            // 
            this.toolStripSeparator20.Name = "toolStripSeparator20";
            this.toolStripSeparator20.Size = new System.Drawing.Size(6, 25);
            // 
            // cutToolBarButton
            // 
            this.cutToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.cutToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Cut;
            this.cutToolBarButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.cutToolBarButton.Name = "cutToolBarButton";
            this.cutToolBarButton.Size = new System.Drawing.Size(23, 22);
            this.cutToolBarButton.Text = "Cut";
            this.cutToolBarButton.Click += new System.EventHandler(this.cutMenuItem_Click);
            // 
            // copyToolBarButton
            // 
            this.copyToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.copyToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Copy;
            this.copyToolBarButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.copyToolBarButton.Name = "copyToolBarButton";
            this.copyToolBarButton.Size = new System.Drawing.Size(23, 22);
            this.copyToolBarButton.Text = "Copy";
            this.copyToolBarButton.Click += new System.EventHandler(this.copyMenuItem_Click);
            // 
            // pasteToolBarButton
            // 
            this.pasteToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.pasteToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Paste;
            this.pasteToolBarButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.pasteToolBarButton.Name = "pasteToolBarButton";
            this.pasteToolBarButton.Size = new System.Drawing.Size(23, 22);
            this.pasteToolBarButton.Text = "Paste";
            this.pasteToolBarButton.Click += new System.EventHandler(this.pasteMenuItem_Click);
            // 
            // toolStripSeparator21
            // 
            this.toolStripSeparator21.Name = "toolStripSeparator21";
            this.toolStripSeparator21.Size = new System.Drawing.Size(6, 25);
            // 
            // undoToolBarButton
            // 
            this.undoToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.undoToolBarButton.Enabled = false;
            this.undoToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Edit_Undo;
            this.undoToolBarButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.undoToolBarButton.Name = "undoToolBarButton";
            this.undoToolBarButton.Size = new System.Drawing.Size(32, 22);
            this.undoToolBarButton.Text = "Undo";
            // 
            // redoToolBarButton
            // 
            this.redoToolBarButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.redoToolBarButton.Enabled = false;
            this.redoToolBarButton.Image = global::pwiz.Skyline.Properties.Resources.Edit_Redo;
            this.redoToolBarButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.redoToolBarButton.Name = "redoToolBarButton";
            this.redoToolBarButton.Size = new System.Drawing.Size(32, 22);
            this.redoToolBarButton.Text = "Redo";
            // 
            // menuMain
            // 
            this.menuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.settingsToolStripMenuItem,
            this.toolsMenu,
            this.helpToolStripMenuItem});
            this.menuMain.Location = new System.Drawing.Point(0, 0);
            this.menuMain.Name = "menuMain";
            this.menuMain.Size = new System.Drawing.Size(734, 24);
            this.menuMain.TabIndex = 1;
            this.menuMain.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newMenuItem,
            this.openMenuItem,
            this.saveMenuItem,
            this.saveAsMenuItem,
            this.shareDocumentMenuItem,
            this.toolStripSeparator2,
            this.importToolStripMenuItem,
            this.exportToolStripMenuItem,
            this.mruBeforeToolStripSeparator,
            this.mruAfterToolStripSeparator,
            this.exitMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";
            this.fileToolStripMenuItem.DropDownOpening += new System.EventHandler(this.fileMenu_DropDownOpening);
            // 
            // newMenuItem
            // 
            this.newMenuItem.Image = global::pwiz.Skyline.Properties.Resources.NewDocument;
            this.newMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.newMenuItem.Name = "newMenuItem";
            this.newMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this.newMenuItem.Size = new System.Drawing.Size(155, 22);
            this.newMenuItem.Text = "&New";
            this.newMenuItem.Click += new System.EventHandler(this.newMenuItem_Click);
            // 
            // openMenuItem
            // 
            this.openMenuItem.Image = global::pwiz.Skyline.Properties.Resources.OpenFolder;
            this.openMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.openMenuItem.Name = "openMenuItem";
            this.openMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openMenuItem.Size = new System.Drawing.Size(155, 22);
            this.openMenuItem.Text = "&Open...";
            this.openMenuItem.Click += new System.EventHandler(this.openMenuItem_Click);
            // 
            // saveMenuItem
            // 
            this.saveMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Save;
            this.saveMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.saveMenuItem.Name = "saveMenuItem";
            this.saveMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveMenuItem.Size = new System.Drawing.Size(155, 22);
            this.saveMenuItem.Text = "&Save";
            this.saveMenuItem.Click += new System.EventHandler(this.saveMenuItem_Click);
            // 
            // saveAsMenuItem
            // 
            this.saveAsMenuItem.Name = "saveAsMenuItem";
            this.saveAsMenuItem.Size = new System.Drawing.Size(155, 22);
            this.saveAsMenuItem.Text = "Save &As...";
            this.saveAsMenuItem.Click += new System.EventHandler(this.saveAsMenuItem_Click);
            // 
            // shareDocumentMenuItem
            // 
            this.shareDocumentMenuItem.Name = "shareDocumentMenuItem";
            this.shareDocumentMenuItem.Size = new System.Drawing.Size(155, 22);
            this.shareDocumentMenuItem.Text = "S&hare...";
            this.shareDocumentMenuItem.Click += new System.EventHandler(this.shareDocumentMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(152, 6);
            // 
            // importToolStripMenuItem
            // 
            this.importToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importResultsMenuItem,
            this.importFASTAMenuItem,
            this.importMassListMenuItem,
            this.importDocumentMenuItem});
            this.importToolStripMenuItem.Name = "importToolStripMenuItem";
            this.importToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.importToolStripMenuItem.Text = "&Import";
            // 
            // importResultsMenuItem
            // 
            this.importResultsMenuItem.Name = "importResultsMenuItem";
            this.importResultsMenuItem.Size = new System.Drawing.Size(157, 22);
            this.importResultsMenuItem.Text = "&Results...";
            this.importResultsMenuItem.Click += new System.EventHandler(this.importResultsMenuItem_Click);
            // 
            // importFASTAMenuItem
            // 
            this.importFASTAMenuItem.Name = "importFASTAMenuItem";
            this.importFASTAMenuItem.Size = new System.Drawing.Size(157, 22);
            this.importFASTAMenuItem.Text = "&FASTA...";
            this.importFASTAMenuItem.Click += new System.EventHandler(this.importFASTAMenuItem_Click);
            // 
            // importMassListMenuItem
            // 
            this.importMassListMenuItem.Name = "importMassListMenuItem";
            this.importMassListMenuItem.Size = new System.Drawing.Size(157, 22);
            this.importMassListMenuItem.Text = "&Transition List...";
            this.importMassListMenuItem.Click += new System.EventHandler(this.importMassListMenuItem_Click);
            // 
            // importDocumentMenuItem
            // 
            this.importDocumentMenuItem.Name = "importDocumentMenuItem";
            this.importDocumentMenuItem.Size = new System.Drawing.Size(157, 22);
            this.importDocumentMenuItem.Text = "&Document...";
            this.importDocumentMenuItem.Click += new System.EventHandler(this.importDocumentMenuItem_Click);
            // 
            // exportToolStripMenuItem
            // 
            this.exportToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportTransitionListMenuItem,
            this.exportIsolationListMenuItem,
            this.exportMethodMenuItem,
            this.exportReportMenuItem,
            this.eSPFeaturesMenuItem});
            this.exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            this.exportToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.exportToolStripMenuItem.Text = "&Export";
            // 
            // exportTransitionListMenuItem
            // 
            this.exportTransitionListMenuItem.Name = "exportTransitionListMenuItem";
            this.exportTransitionListMenuItem.Size = new System.Drawing.Size(157, 22);
            this.exportTransitionListMenuItem.Text = "&Transition List...";
            this.exportTransitionListMenuItem.Click += new System.EventHandler(this.exportTransitionListMenuItem_Click);
            // 
            // exportIsolationListMenuItem
            // 
            this.exportIsolationListMenuItem.Name = "exportIsolationListMenuItem";
            this.exportIsolationListMenuItem.Size = new System.Drawing.Size(157, 22);
            this.exportIsolationListMenuItem.Text = "&Isolation List...";
            this.exportIsolationListMenuItem.Click += new System.EventHandler(this.exportIsolationListMenuItem_Click);
            // 
            // exportMethodMenuItem
            // 
            this.exportMethodMenuItem.Name = "exportMethodMenuItem";
            this.exportMethodMenuItem.Size = new System.Drawing.Size(157, 22);
            this.exportMethodMenuItem.Text = "&Method...";
            this.exportMethodMenuItem.Click += new System.EventHandler(this.exportMethodMenuItem_Click);
            // 
            // exportReportMenuItem
            // 
            this.exportReportMenuItem.Name = "exportReportMenuItem";
            this.exportReportMenuItem.Size = new System.Drawing.Size(157, 22);
            this.exportReportMenuItem.Text = "&Report...";
            this.exportReportMenuItem.Click += new System.EventHandler(this.exportReportMenuItem_Click);
            // 
            // eSPFeaturesMenuItem
            // 
            this.eSPFeaturesMenuItem.Name = "eSPFeaturesMenuItem";
            this.eSPFeaturesMenuItem.Size = new System.Drawing.Size(157, 22);
            this.eSPFeaturesMenuItem.Text = "&ESP Features...";
            this.eSPFeaturesMenuItem.Visible = false;
            this.eSPFeaturesMenuItem.Click += new System.EventHandler(this.espFeaturesMenuItem_Click);
            // 
            // mruBeforeToolStripSeparator
            // 
            this.mruBeforeToolStripSeparator.Name = "mruBeforeToolStripSeparator";
            this.mruBeforeToolStripSeparator.Size = new System.Drawing.Size(152, 6);
            // 
            // mruAfterToolStripSeparator
            // 
            this.mruAfterToolStripSeparator.Name = "mruAfterToolStripSeparator";
            this.mruAfterToolStripSeparator.Size = new System.Drawing.Size(152, 6);
            this.mruAfterToolStripSeparator.Visible = false;
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new System.Drawing.Size(155, 22);
            this.exitMenuItem.Text = "E&xit";
            this.exitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.undoMenuItem,
            this.redoMenuItem,
            this.toolStripSeparator34,
            this.cutMenuItem,
            this.copyMenuItem,
            this.pasteMenuItem,
            this.deleteMenuItem,
            this.selectAllMenuItem,
            this.toolStripSeparator4,
            this.findPeptideMenuItem,
            this.findNextMenuItem,
            this.toolStripSeparator8,
            this.editNoteToolStripMenuItem,
            this.toolStripSeparator42,
            this.insertToolStripMenuItem,
            this.refineToolStripMenuItem,
            this.toolStripSeparator6,
            this.expandAllToolStripMenuItem,
            this.collapseAllToolStripMenuItem,
            this.toolStripSeparator5,
            this.modifyPeptideMenuItem,
            this.manageUniquePeptidesMenuItem,
            this.toolStripSeparator30,
            this.manageResultsMenuItem});
            this.editToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editToolStripMenuItem.Text = "&Edit";
            // 
            // undoMenuItem
            // 
            this.undoMenuItem.Enabled = false;
            this.undoMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Edit_Undo;
            this.undoMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.undoMenuItem.Name = "undoMenuItem";
            this.undoMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z)));
            this.undoMenuItem.Size = new System.Drawing.Size(207, 22);
            this.undoMenuItem.Text = "&Undo";
            // 
            // redoMenuItem
            // 
            this.redoMenuItem.Enabled = false;
            this.redoMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Edit_Redo;
            this.redoMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.redoMenuItem.Name = "redoMenuItem";
            this.redoMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y)));
            this.redoMenuItem.Size = new System.Drawing.Size(207, 22);
            this.redoMenuItem.Text = "&Redo";
            // 
            // toolStripSeparator34
            // 
            this.toolStripSeparator34.Name = "toolStripSeparator34";
            this.toolStripSeparator34.Size = new System.Drawing.Size(204, 6);
            // 
            // cutMenuItem
            // 
            this.cutMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Cut;
            this.cutMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.cutMenuItem.Name = "cutMenuItem";
            this.cutMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.X)));
            this.cutMenuItem.Size = new System.Drawing.Size(207, 22);
            this.cutMenuItem.Text = "Cu&t";
            this.cutMenuItem.Click += new System.EventHandler(this.cutMenuItem_Click);
            // 
            // copyMenuItem
            // 
            this.copyMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Copy;
            this.copyMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.copyMenuItem.Name = "copyMenuItem";
            this.copyMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.copyMenuItem.Size = new System.Drawing.Size(207, 22);
            this.copyMenuItem.Text = "&Copy";
            this.copyMenuItem.Click += new System.EventHandler(this.copyMenuItem_Click);
            // 
            // pasteMenuItem
            // 
            this.pasteMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Paste;
            this.pasteMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.pasteMenuItem.Name = "pasteMenuItem";
            this.pasteMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V)));
            this.pasteMenuItem.Size = new System.Drawing.Size(207, 22);
            this.pasteMenuItem.Text = "&Paste";
            this.pasteMenuItem.Click += new System.EventHandler(this.pasteMenuItem_Click);
            // 
            // deleteMenuItem
            // 
            this.deleteMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            this.deleteMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.deleteMenuItem.Name = "deleteMenuItem";
            this.deleteMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Delete;
            this.deleteMenuItem.Size = new System.Drawing.Size(207, 22);
            this.deleteMenuItem.Text = "&Delete";
            this.deleteMenuItem.Click += new System.EventHandler(this.deleteMenuItem_Click);
            // 
            // selectAllMenuItem
            // 
            this.selectAllMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.selectAllMenuItem.Name = "selectAllMenuItem";
            this.selectAllMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A)));
            this.selectAllMenuItem.Size = new System.Drawing.Size(207, 22);
            this.selectAllMenuItem.Text = "Select &All";
            this.selectAllMenuItem.Click += new System.EventHandler(this.selectAllMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(204, 6);
            // 
            // findPeptideMenuItem
            // 
            this.findPeptideMenuItem.Name = "findPeptideMenuItem";
            this.findPeptideMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F)));
            this.findPeptideMenuItem.Size = new System.Drawing.Size(207, 22);
            this.findPeptideMenuItem.Text = "&Find...";
            this.findPeptideMenuItem.Click += new System.EventHandler(this.findMenuItem_Click);
            // 
            // findNextMenuItem
            // 
            this.findNextMenuItem.Name = "findNextMenuItem";
            this.findNextMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.findNextMenuItem.Size = new System.Drawing.Size(207, 22);
            this.findNextMenuItem.Text = "Find Ne&xt";
            this.findNextMenuItem.Click += new System.EventHandler(this.findNextMenuItem_Click);
            // 
            // toolStripSeparator8
            // 
            this.toolStripSeparator8.Name = "toolStripSeparator8";
            this.toolStripSeparator8.Size = new System.Drawing.Size(204, 6);
            // 
            // editNoteToolStripMenuItem
            // 
            this.editNoteToolStripMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Comment;
            this.editNoteToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.editNoteToolStripMenuItem.Name = "editNoteToolStripMenuItem";
            this.editNoteToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F2)));
            this.editNoteToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.editNoteToolStripMenuItem.Text = "Edit &Note";
            this.editNoteToolStripMenuItem.Click += new System.EventHandler(this.editNoteMenuItem_Click);
            // 
            // toolStripSeparator42
            // 
            this.toolStripSeparator42.Name = "toolStripSeparator42";
            this.toolStripSeparator42.Size = new System.Drawing.Size(204, 6);
            // 
            // insertToolStripMenuItem
            // 
            this.insertToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.insertFASTAMenuItem,
            this.insertProteinsMenuItem,
            this.insertPeptidesMenuItem,
            this.insertTransitionListMenuItem});
            this.insertToolStripMenuItem.Name = "insertToolStripMenuItem";
            this.insertToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.insertToolStripMenuItem.Text = "&Insert";
            // 
            // insertFASTAMenuItem
            // 
            this.insertFASTAMenuItem.Name = "insertFASTAMenuItem";
            this.insertFASTAMenuItem.Size = new System.Drawing.Size(157, 22);
            this.insertFASTAMenuItem.Text = "&FASTA...";
            this.insertFASTAMenuItem.Click += new System.EventHandler(this.insertFASTAToolStripMenuItem_Click);
            // 
            // insertProteinsMenuItem
            // 
            this.insertProteinsMenuItem.Name = "insertProteinsMenuItem";
            this.insertProteinsMenuItem.Size = new System.Drawing.Size(157, 22);
            this.insertProteinsMenuItem.Text = "Pr&oteins...";
            this.insertProteinsMenuItem.Click += new System.EventHandler(this.insertProteinsToolStripMenuItem_Click);
            // 
            // insertPeptidesMenuItem
            // 
            this.insertPeptidesMenuItem.Name = "insertPeptidesMenuItem";
            this.insertPeptidesMenuItem.Size = new System.Drawing.Size(157, 22);
            this.insertPeptidesMenuItem.Text = "&Peptides...";
            this.insertPeptidesMenuItem.Click += new System.EventHandler(this.insertPeptidesToolStripMenuItem_Click);
            // 
            // insertTransitionListMenuItem
            // 
            this.insertTransitionListMenuItem.Name = "insertTransitionListMenuItem";
            this.insertTransitionListMenuItem.Size = new System.Drawing.Size(157, 22);
            this.insertTransitionListMenuItem.Text = "&Transition List...";
            this.insertTransitionListMenuItem.Click += new System.EventHandler(this.insertTransitionListMenuItem_Click);
            // 
            // refineToolStripMenuItem
            // 
            this.refineToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.removeEmptyProteinsMenuItem,
            this.removeDuplicatePeptidesMenuItem,
            this.removeRepeatedPeptidesMenuItem,
            this.removeMissingResultsMenuItem,
            this.toolStripSeparator45,
            this.sortProteinsMenuItem,
            this.toolStripSeparator43,
            this.acceptPeptidesMenuItem,
            this.generateDecoysMenuItem,
            this.toolStripSeparator35,
            this.refineAdvancedMenuItem});
            this.refineToolStripMenuItem.Name = "refineToolStripMenuItem";
            this.refineToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.refineToolStripMenuItem.Text = "R&efine";
            // 
            // removeEmptyProteinsMenuItem
            // 
            this.removeEmptyProteinsMenuItem.Name = "removeEmptyProteinsMenuItem";
            this.removeEmptyProteinsMenuItem.Size = new System.Drawing.Size(218, 22);
            this.removeEmptyProteinsMenuItem.Text = "Remove &Empty Proteins";
            this.removeEmptyProteinsMenuItem.Click += new System.EventHandler(this.removeEmptyProteinsMenuItem_Click);
            // 
            // removeDuplicatePeptidesMenuItem
            // 
            this.removeDuplicatePeptidesMenuItem.Name = "removeDuplicatePeptidesMenuItem";
            this.removeDuplicatePeptidesMenuItem.Size = new System.Drawing.Size(218, 22);
            this.removeDuplicatePeptidesMenuItem.Text = "Remove &Duplicate Peptides";
            this.removeDuplicatePeptidesMenuItem.Click += new System.EventHandler(this.removeDuplicatePeptidesMenuItem_Click);
            // 
            // removeRepeatedPeptidesMenuItem
            // 
            this.removeRepeatedPeptidesMenuItem.Name = "removeRepeatedPeptidesMenuItem";
            this.removeRepeatedPeptidesMenuItem.Size = new System.Drawing.Size(218, 22);
            this.removeRepeatedPeptidesMenuItem.Text = "&Remove Repeated Peptides";
            this.removeRepeatedPeptidesMenuItem.Click += new System.EventHandler(this.removeRepeatedPeptidesMenuItem_Click);
            // 
            // removeMissingResultsMenuItem
            // 
            this.removeMissingResultsMenuItem.Name = "removeMissingResultsMenuItem";
            this.removeMissingResultsMenuItem.Size = new System.Drawing.Size(218, 22);
            this.removeMissingResultsMenuItem.Text = "Remove &Missing Results";
            this.removeMissingResultsMenuItem.Click += new System.EventHandler(this.removeMissingResultsMenuItem_Click);
            // 
            // toolStripSeparator45
            // 
            this.toolStripSeparator45.Name = "toolStripSeparator45";
            this.toolStripSeparator45.Size = new System.Drawing.Size(215, 6);
            // 
            // sortProteinsMenuItem
            // 
            this.sortProteinsMenuItem.Name = "sortProteinsMenuItem";
            this.sortProteinsMenuItem.Size = new System.Drawing.Size(218, 22);
            this.sortProteinsMenuItem.Text = "&Sort Proteins by Name";
            this.sortProteinsMenuItem.Click += new System.EventHandler(this.sortProteinsMenuItem_Click);
            // 
            // toolStripSeparator43
            // 
            this.toolStripSeparator43.Name = "toolStripSeparator43";
            this.toolStripSeparator43.Size = new System.Drawing.Size(215, 6);
            // 
            // acceptPeptidesMenuItem
            // 
            this.acceptPeptidesMenuItem.Name = "acceptPeptidesMenuItem";
            this.acceptPeptidesMenuItem.Size = new System.Drawing.Size(218, 22);
            this.acceptPeptidesMenuItem.Text = "A&ccept Peptides...";
            this.acceptPeptidesMenuItem.Click += new System.EventHandler(this.acceptPeptidesMenuItem_Click);
            // 
            // generateDecoysMenuItem
            // 
            this.generateDecoysMenuItem.Name = "generateDecoysMenuItem";
            this.generateDecoysMenuItem.RightToLeftAutoMirrorImage = true;
            this.generateDecoysMenuItem.Size = new System.Drawing.Size(218, 22);
            this.generateDecoysMenuItem.Text = "Add &Decoy Peptides...";
            this.generateDecoysMenuItem.Click += new System.EventHandler(this.generateDecoysMenuItem_Click);
            // 
            // toolStripSeparator35
            // 
            this.toolStripSeparator35.Name = "toolStripSeparator35";
            this.toolStripSeparator35.Size = new System.Drawing.Size(215, 6);
            // 
            // refineAdvancedMenuItem
            // 
            this.refineAdvancedMenuItem.Name = "refineAdvancedMenuItem";
            this.refineAdvancedMenuItem.Size = new System.Drawing.Size(218, 22);
            this.refineAdvancedMenuItem.Text = "&Advanced...";
            this.refineAdvancedMenuItem.Click += new System.EventHandler(this.refineMenuItem_Click);
            // 
            // toolStripSeparator6
            // 
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new System.Drawing.Size(204, 6);
            // 
            // expandAllToolStripMenuItem
            // 
            this.expandAllToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.expandProteinsMenuItem,
            this.expandPeptidesMenuItem,
            this.expandPrecursorsMenuItem});
            this.expandAllToolStripMenuItem.Name = "expandAllToolStripMenuItem";
            this.expandAllToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.expandAllToolStripMenuItem.Text = "E&xpand All";
            // 
            // expandProteinsMenuItem
            // 
            this.expandProteinsMenuItem.Name = "expandProteinsMenuItem";
            this.expandProteinsMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.E)));
            this.expandProteinsMenuItem.Size = new System.Drawing.Size(174, 22);
            this.expandProteinsMenuItem.Text = "&Proteins";
            this.expandProteinsMenuItem.Click += new System.EventHandler(this.expandProteinsMenuItem_Click);
            // 
            // expandPeptidesMenuItem
            // 
            this.expandPeptidesMenuItem.Name = "expandPeptidesMenuItem";
            this.expandPeptidesMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D)));
            this.expandPeptidesMenuItem.Size = new System.Drawing.Size(174, 22);
            this.expandPeptidesMenuItem.Text = "P&eptides";
            this.expandPeptidesMenuItem.Click += new System.EventHandler(this.expandPeptidesMenuItem_Click);
            // 
            // expandPrecursorsMenuItem
            // 
            this.expandPrecursorsMenuItem.Name = "expandPrecursorsMenuItem";
            this.expandPrecursorsMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.W)));
            this.expandPrecursorsMenuItem.Size = new System.Drawing.Size(174, 22);
            this.expandPrecursorsMenuItem.Text = "P&recursors";
            this.expandPrecursorsMenuItem.Click += new System.EventHandler(this.expandPrecursorsMenuItem_Click);
            // 
            // collapseAllToolStripMenuItem
            // 
            this.collapseAllToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.collapseProteinsMenuItem,
            this.collapsePeptidesMenuItem,
            this.collapsePrecursorsMenuItem});
            this.collapseAllToolStripMenuItem.Name = "collapseAllToolStripMenuItem";
            this.collapseAllToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.collapseAllToolStripMenuItem.Text = "C&ollapse All";
            // 
            // collapseProteinsMenuItem
            // 
            this.collapseProteinsMenuItem.Name = "collapseProteinsMenuItem";
            this.collapseProteinsMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.E)));
            this.collapseProteinsMenuItem.Size = new System.Drawing.Size(206, 22);
            this.collapseProteinsMenuItem.Text = "&Proteins";
            this.collapseProteinsMenuItem.Click += new System.EventHandler(this.collapseProteinsMenuItem_Click);
            // 
            // collapsePeptidesMenuItem
            // 
            this.collapsePeptidesMenuItem.Name = "collapsePeptidesMenuItem";
            this.collapsePeptidesMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D)));
            this.collapsePeptidesMenuItem.Size = new System.Drawing.Size(206, 22);
            this.collapsePeptidesMenuItem.Text = "P&eptides";
            this.collapsePeptidesMenuItem.Click += new System.EventHandler(this.collapsePeptidesMenuItem_Click);
            // 
            // collapsePrecursorsMenuItem
            // 
            this.collapsePrecursorsMenuItem.Name = "collapsePrecursorsMenuItem";
            this.collapsePrecursorsMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.W)));
            this.collapsePrecursorsMenuItem.Size = new System.Drawing.Size(206, 22);
            this.collapsePrecursorsMenuItem.Text = "P&recursors";
            this.collapsePrecursorsMenuItem.Click += new System.EventHandler(this.collapsePrecursorsMenuItem_Click);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(204, 6);
            // 
            // modifyPeptideMenuItem
            // 
            this.modifyPeptideMenuItem.Name = "modifyPeptideMenuItem";
            this.modifyPeptideMenuItem.Size = new System.Drawing.Size(207, 22);
            this.modifyPeptideMenuItem.Text = "&Modify Peptide...";
            this.modifyPeptideMenuItem.Click += new System.EventHandler(this.modifyPeptideMenuItem_Click);
            // 
            // manageUniquePeptidesMenuItem
            // 
            this.manageUniquePeptidesMenuItem.Name = "manageUniquePeptidesMenuItem";
            this.manageUniquePeptidesMenuItem.Size = new System.Drawing.Size(207, 22);
            this.manageUniquePeptidesMenuItem.Text = "Uni&que Peptides...";
            this.manageUniquePeptidesMenuItem.Click += new System.EventHandler(this.manageUniquePeptidesMenuItem_Click);
            // 
            // toolStripSeparator30
            // 
            this.toolStripSeparator30.Name = "toolStripSeparator30";
            this.toolStripSeparator30.Size = new System.Drawing.Size(204, 6);
            // 
            // manageResultsMenuItem
            // 
            this.manageResultsMenuItem.Name = "manageResultsMenuItem";
            this.manageResultsMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R)));
            this.manageResultsMenuItem.Size = new System.Drawing.Size(207, 22);
            this.manageResultsMenuItem.Text = "Manage Re&sults...";
            this.manageResultsMenuItem.Click += new System.EventHandler(this.manageResultsMenuItem_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.peptidesMenuItem,
            this.textZoomToolStripMenuItem,
            this.toolStripSeparator41,
            this.spectralLibrariesToolStripMenuItem,
            this.toolStripSeparator32,
            this.arrangeGraphsToolStripMenuItem,
            this.toolStripSeparator39,
            this.graphsToolStripMenuItem,
            this.ionTypesMenuItem,
            this.chargesMenuItem,
            this.ranksMenuItem,
            this.toolStripSeparator9,
            this.chromatogramsMenuItem,
            this.transitionsMenuItem,
            this.transformChromMenuItem,
            this.autoZoomMenuItem,
            this.toolStripSeparator10,
            this.retentionTimesMenuItem,
            this.peakAreasMenuItem,
            this.resultsGridMenuItem,
            this.toolStripSeparator36,
            this.toolBarToolStripMenuItem,
            this.statusToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "&View";
            this.viewToolStripMenuItem.DropDownOpening += new System.EventHandler(this.viewToolStripMenuItem_DropDownOpening);
            // 
            // peptidesMenuItem
            // 
            this.peptidesMenuItem.Name = "peptidesMenuItem";
            this.peptidesMenuItem.Size = new System.Drawing.Size(191, 22);
            this.peptidesMenuItem.Text = "Targ&ets";
            this.peptidesMenuItem.Click += new System.EventHandler(this.peptidesMenuItem_Click);
            // 
            // textZoomToolStripMenuItem
            // 
            this.textZoomToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.defaultTextToolStripMenuItem,
            this.largeToolStripMenuItem,
            this.extraLargeToolStripMenuItem});
            this.textZoomToolStripMenuItem.Name = "textZoomToolStripMenuItem";
            this.textZoomToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.textZoomToolStripMenuItem.Text = "Text &Zoom";
            // 
            // defaultTextToolStripMenuItem
            // 
            this.defaultTextToolStripMenuItem.Name = "defaultTextToolStripMenuItem";
            this.defaultTextToolStripMenuItem.Size = new System.Drawing.Size(131, 22);
            this.defaultTextToolStripMenuItem.Text = "Default";
            this.defaultTextToolStripMenuItem.Click += new System.EventHandler(this.defaultToolStripMenuItem_Click);
            // 
            // largeToolStripMenuItem
            // 
            this.largeToolStripMenuItem.Name = "largeToolStripMenuItem";
            this.largeToolStripMenuItem.Size = new System.Drawing.Size(131, 22);
            this.largeToolStripMenuItem.Text = "Large";
            this.largeToolStripMenuItem.Click += new System.EventHandler(this.largeToolStripMenuItem_Click);
            // 
            // extraLargeToolStripMenuItem
            // 
            this.extraLargeToolStripMenuItem.Name = "extraLargeToolStripMenuItem";
            this.extraLargeToolStripMenuItem.Size = new System.Drawing.Size(131, 22);
            this.extraLargeToolStripMenuItem.Text = "Extra Large";
            this.extraLargeToolStripMenuItem.Click += new System.EventHandler(this.extraLargeToolStripMenuItem_Click);
            // 
            // toolStripSeparator41
            // 
            this.toolStripSeparator41.Name = "toolStripSeparator41";
            this.toolStripSeparator41.Size = new System.Drawing.Size(188, 6);
            // 
            // spectralLibrariesToolStripMenuItem
            // 
            this.spectralLibrariesToolStripMenuItem.Name = "spectralLibrariesToolStripMenuItem";
            this.spectralLibrariesToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.spectralLibrariesToolStripMenuItem.Text = "Spectral &Libraries";
            this.spectralLibrariesToolStripMenuItem.Click += new System.EventHandler(this.spectralLibrariesToolStripMenuItem_Click);
            // 
            // toolStripSeparator32
            // 
            this.toolStripSeparator32.Name = "toolStripSeparator32";
            this.toolStripSeparator32.Size = new System.Drawing.Size(188, 6);
            // 
            // arrangeGraphsToolStripMenuItem
            // 
            this.arrangeGraphsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.arrangeTiledMenuItem,
            this.arrangedTabbedMenuItem,
            this.groupedMenuItem});
            this.arrangeGraphsToolStripMenuItem.Name = "arrangeGraphsToolStripMenuItem";
            this.arrangeGraphsToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.arrangeGraphsToolStripMenuItem.Text = "&Arrange Graphs";
            // 
            // arrangeTiledMenuItem
            // 
            this.arrangeTiledMenuItem.Name = "arrangeTiledMenuItem";
            this.arrangeTiledMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.T)));
            this.arrangeTiledMenuItem.Size = new System.Drawing.Size(187, 22);
            this.arrangeTiledMenuItem.Text = "&Tiled";
            this.arrangeTiledMenuItem.Click += new System.EventHandler(this.arrangeTiledMenuItem_Click);
            // 
            // arrangedTabbedMenuItem
            // 
            this.arrangedTabbedMenuItem.Name = "arrangedTabbedMenuItem";
            this.arrangedTabbedMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.T)));
            this.arrangedTabbedMenuItem.Size = new System.Drawing.Size(187, 22);
            this.arrangedTabbedMenuItem.Text = "T&abbed";
            this.arrangedTabbedMenuItem.Click += new System.EventHandler(this.arrangeTabbedMenuItem_Click);
            // 
            // groupedMenuItem
            // 
            this.groupedMenuItem.Name = "groupedMenuItem";
            this.groupedMenuItem.Size = new System.Drawing.Size(187, 22);
            this.groupedMenuItem.Text = "&Grouped...";
            this.groupedMenuItem.Click += new System.EventHandler(this.arrangeGroupedMenuItem_Click);
            // 
            // toolStripSeparator39
            // 
            this.toolStripSeparator39.Name = "toolStripSeparator39";
            this.toolStripSeparator39.Size = new System.Drawing.Size(188, 6);
            // 
            // graphsToolStripMenuItem
            // 
            this.graphsToolStripMenuItem.Enabled = false;
            this.graphsToolStripMenuItem.Name = "graphsToolStripMenuItem";
            this.graphsToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.D1)));
            this.graphsToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.graphsToolStripMenuItem.Text = "&MS/MS Spectra";
            this.graphsToolStripMenuItem.Click += new System.EventHandler(this.graphsToolStripMenuItem_Click);
            // 
            // ionTypesMenuItem
            // 
            this.ionTypesMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aMenuItem,
            this.bMenuItem,
            this.cMenuItem,
            this.xMenuItem,
            this.yMenuItem,
            this.zMenuItem,
            this.precursorIonMenuItem});
            this.ionTypesMenuItem.Enabled = false;
            this.ionTypesMenuItem.Name = "ionTypesMenuItem";
            this.ionTypesMenuItem.Size = new System.Drawing.Size(191, 22);
            this.ionTypesMenuItem.Text = "&Ion Types";
            this.ionTypesMenuItem.DropDownOpening += new System.EventHandler(this.ionTypesMenuItem_DropDownOpening);
            // 
            // aMenuItem
            // 
            this.aMenuItem.CheckOnClick = true;
            this.aMenuItem.Name = "aMenuItem";
            this.aMenuItem.Size = new System.Drawing.Size(124, 22);
            this.aMenuItem.Text = "&A";
            this.aMenuItem.Click += new System.EventHandler(this.aMenuItem_Click);
            // 
            // bMenuItem
            // 
            this.bMenuItem.CheckOnClick = true;
            this.bMenuItem.Name = "bMenuItem";
            this.bMenuItem.Size = new System.Drawing.Size(124, 22);
            this.bMenuItem.Text = "&B";
            this.bMenuItem.Click += new System.EventHandler(this.bMenuItem_Click);
            // 
            // cMenuItem
            // 
            this.cMenuItem.CheckOnClick = true;
            this.cMenuItem.Name = "cMenuItem";
            this.cMenuItem.Size = new System.Drawing.Size(124, 22);
            this.cMenuItem.Text = "&C";
            this.cMenuItem.Click += new System.EventHandler(this.cMenuItem_Click);
            // 
            // xMenuItem
            // 
            this.xMenuItem.CheckOnClick = true;
            this.xMenuItem.Name = "xMenuItem";
            this.xMenuItem.Size = new System.Drawing.Size(124, 22);
            this.xMenuItem.Text = "&X";
            this.xMenuItem.Click += new System.EventHandler(this.xMenuItem_Click);
            // 
            // yMenuItem
            // 
            this.yMenuItem.Checked = true;
            this.yMenuItem.CheckOnClick = true;
            this.yMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.yMenuItem.Name = "yMenuItem";
            this.yMenuItem.Size = new System.Drawing.Size(124, 22);
            this.yMenuItem.Text = "&Y";
            this.yMenuItem.Click += new System.EventHandler(this.yMenuItem_Click);
            // 
            // zMenuItem
            // 
            this.zMenuItem.CheckOnClick = true;
            this.zMenuItem.Name = "zMenuItem";
            this.zMenuItem.Size = new System.Drawing.Size(124, 22);
            this.zMenuItem.Text = "&Z";
            this.zMenuItem.Click += new System.EventHandler(this.zMenuItem_Click);
            // 
            // precursorIonMenuItem
            // 
            this.precursorIonMenuItem.Name = "precursorIonMenuItem";
            this.precursorIonMenuItem.Size = new System.Drawing.Size(124, 22);
            this.precursorIonMenuItem.Text = "&Precursor";
            this.precursorIonMenuItem.Click += new System.EventHandler(this.precursorIonMenuItem_Click);
            // 
            // chargesMenuItem
            // 
            this.chargesMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.charge1MenuItem,
            this.charge2MenuItem,
            this.charge3MenuItem,
            this.charge4MenuItem});
            this.chargesMenuItem.Enabled = false;
            this.chargesMenuItem.Name = "chargesMenuItem";
            this.chargesMenuItem.Size = new System.Drawing.Size(191, 22);
            this.chargesMenuItem.Text = "&Charges";
            this.chargesMenuItem.DropDownOpening += new System.EventHandler(this.chargesMenuItem_DropDownOpening);
            // 
            // charge1MenuItem
            // 
            this.charge1MenuItem.Checked = true;
            this.charge1MenuItem.CheckOnClick = true;
            this.charge1MenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.charge1MenuItem.Name = "charge1MenuItem";
            this.charge1MenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge1MenuItem.Text = "&1";
            this.charge1MenuItem.Click += new System.EventHandler(this.charge1MenuItem_Click);
            // 
            // charge2MenuItem
            // 
            this.charge2MenuItem.CheckOnClick = true;
            this.charge2MenuItem.Name = "charge2MenuItem";
            this.charge2MenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge2MenuItem.Text = "&2";
            this.charge2MenuItem.Click += new System.EventHandler(this.charge2MenuItem_Click);
            // 
            // charge3MenuItem
            // 
            this.charge3MenuItem.Name = "charge3MenuItem";
            this.charge3MenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge3MenuItem.Text = "&3";
            this.charge3MenuItem.Click += new System.EventHandler(this.charge3MenuItem_Click);
            // 
            // charge4MenuItem
            // 
            this.charge4MenuItem.Name = "charge4MenuItem";
            this.charge4MenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge4MenuItem.Text = "&4";
            this.charge4MenuItem.Click += new System.EventHandler(this.charge4MenuItem_Click);
            // 
            // ranksMenuItem
            // 
            this.ranksMenuItem.Checked = true;
            this.ranksMenuItem.CheckOnClick = true;
            this.ranksMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ranksMenuItem.Enabled = false;
            this.ranksMenuItem.Name = "ranksMenuItem";
            this.ranksMenuItem.Size = new System.Drawing.Size(191, 22);
            this.ranksMenuItem.Text = "&Ranks";
            this.ranksMenuItem.Click += new System.EventHandler(this.ranksMenuItem_Click);
            // 
            // toolStripSeparator9
            // 
            this.toolStripSeparator9.Name = "toolStripSeparator9";
            this.toolStripSeparator9.Size = new System.Drawing.Size(188, 6);
            // 
            // chromatogramsMenuItem
            // 
            this.chromatogramsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showChromMenuItem,
            this.toolStripSeparatorReplicates,
            this.previousReplicateMenuItem,
            this.nextReplicateMenuItem,
            this.toolStripSeparator44,
            this.closeAllChromatogramsMenuItem});
            this.chromatogramsMenuItem.Enabled = false;
            this.chromatogramsMenuItem.Name = "chromatogramsMenuItem";
            this.chromatogramsMenuItem.Size = new System.Drawing.Size(191, 22);
            this.chromatogramsMenuItem.Text = "Chr&omatograms";
            this.chromatogramsMenuItem.DropDownOpening += new System.EventHandler(this.chromatogramsMenuItem_DropDownOpening);
            // 
            // showChromMenuItem
            // 
            this.showChromMenuItem.Name = "showChromMenuItem";
            this.showChromMenuItem.Size = new System.Drawing.Size(219, 22);
            this.showChromMenuItem.Text = "<placeholder>";
            // 
            // toolStripSeparatorReplicates
            // 
            this.toolStripSeparatorReplicates.Name = "toolStripSeparatorReplicates";
            this.toolStripSeparatorReplicates.Size = new System.Drawing.Size(216, 6);
            // 
            // previousReplicateMenuItem
            // 
            this.previousReplicateMenuItem.Name = "previousReplicateMenuItem";
            this.previousReplicateMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Up)));
            this.previousReplicateMenuItem.Size = new System.Drawing.Size(219, 22);
            this.previousReplicateMenuItem.Text = "Pre&vious Replicate";
            this.previousReplicateMenuItem.Click += new System.EventHandler(this.previousReplicateMenuItem_Click);
            // 
            // nextReplicateMenuItem
            // 
            this.nextReplicateMenuItem.Name = "nextReplicateMenuItem";
            this.nextReplicateMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Down)));
            this.nextReplicateMenuItem.Size = new System.Drawing.Size(219, 22);
            this.nextReplicateMenuItem.Text = "Ne&xt Replicate";
            this.nextReplicateMenuItem.Click += new System.EventHandler(this.nextReplicateMenuItem_Click);
            // 
            // toolStripSeparator44
            // 
            this.toolStripSeparator44.Name = "toolStripSeparator44";
            this.toolStripSeparator44.Size = new System.Drawing.Size(216, 6);
            // 
            // closeAllChromatogramsMenuItem
            // 
            this.closeAllChromatogramsMenuItem.Name = "closeAllChromatogramsMenuItem";
            this.closeAllChromatogramsMenuItem.Size = new System.Drawing.Size(219, 22);
            this.closeAllChromatogramsMenuItem.Text = "Close &All";
            this.closeAllChromatogramsMenuItem.Click += new System.EventHandler(this.closeAllChromatogramsMenuItem_Click);
            // 
            // transitionsMenuItem
            // 
            this.transitionsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allTranMenuItem,
            this.precursorsTranMenuItem,
            this.productsTranMenuItem,
            this.singleTranMenuItem,
            this.totalTranMenuItem});
            this.transitionsMenuItem.Enabled = false;
            this.transitionsMenuItem.Name = "transitionsMenuItem";
            this.transitionsMenuItem.Size = new System.Drawing.Size(191, 22);
            this.transitionsMenuItem.Text = "Tra&nsitions";
            this.transitionsMenuItem.DropDownOpening += new System.EventHandler(this.transitionsMenuItem_DropDownOpening);
            // 
            // allTranMenuItem
            // 
            this.allTranMenuItem.Name = "allTranMenuItem";
            this.allTranMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F10)));
            this.allTranMenuItem.Size = new System.Drawing.Size(196, 22);
            this.allTranMenuItem.Text = "&All";
            this.allTranMenuItem.Click += new System.EventHandler(this.allTranMenuItem_Click);
            // 
            // precursorsTranMenuItem
            // 
            this.precursorsTranMenuItem.Name = "precursorsTranMenuItem";
            this.precursorsTranMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F10)));
            this.precursorsTranMenuItem.Size = new System.Drawing.Size(196, 22);
            this.precursorsTranMenuItem.Text = "&Precursors";
            this.precursorsTranMenuItem.Click += new System.EventHandler(this.precursorsTranMenuItem_Click);
            // 
            // productsTranMenuItem
            // 
            this.productsTranMenuItem.Name = "productsTranMenuItem";
            this.productsTranMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Alt) 
            | System.Windows.Forms.Keys.F10)));
            this.productsTranMenuItem.Size = new System.Drawing.Size(196, 22);
            this.productsTranMenuItem.Text = "Pr&oducts";
            this.productsTranMenuItem.Click += new System.EventHandler(this.productsTranMenuItem_Click);
            // 
            // singleTranMenuItem
            // 
            this.singleTranMenuItem.Name = "singleTranMenuItem";
            this.singleTranMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F10;
            this.singleTranMenuItem.Size = new System.Drawing.Size(196, 22);
            this.singleTranMenuItem.Text = "&Single";
            this.singleTranMenuItem.Click += new System.EventHandler(this.singleTranMenuItem_Click);
            // 
            // totalTranMenuItem
            // 
            this.totalTranMenuItem.Name = "totalTranMenuItem";
            this.totalTranMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F10)));
            this.totalTranMenuItem.Size = new System.Drawing.Size(196, 22);
            this.totalTranMenuItem.Text = "&Total";
            this.totalTranMenuItem.Click += new System.EventHandler(this.totalTranMenuItem_Click);
            // 
            // transformChromMenuItem
            // 
            this.transformChromMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.transformChromNoneMenuItem,
            this.secondDerivativeMenuItem,
            this.firstDerivativeMenuItem,
            this.smoothSGChromMenuItem});
            this.transformChromMenuItem.Enabled = false;
            this.transformChromMenuItem.Name = "transformChromMenuItem";
            this.transformChromMenuItem.Size = new System.Drawing.Size(191, 22);
            this.transformChromMenuItem.Text = "Trans&form";
            this.transformChromMenuItem.DropDownOpening += new System.EventHandler(this.transformChromMenuItem_DropDownOpening);
            // 
            // transformChromNoneMenuItem
            // 
            this.transformChromNoneMenuItem.Name = "transformChromNoneMenuItem";
            this.transformChromNoneMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F12)));
            this.transformChromNoneMenuItem.Size = new System.Drawing.Size(297, 22);
            this.transformChromNoneMenuItem.Text = "&None";
            this.transformChromNoneMenuItem.Click += new System.EventHandler(this.transformChromNoneMenuItem_Click);
            // 
            // secondDerivativeMenuItem
            // 
            this.secondDerivativeMenuItem.Name = "secondDerivativeMenuItem";
            this.secondDerivativeMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F12;
            this.secondDerivativeMenuItem.Size = new System.Drawing.Size(297, 22);
            this.secondDerivativeMenuItem.Text = "&Second Derivative";
            this.secondDerivativeMenuItem.Click += new System.EventHandler(this.secondDerivativeMenuItem_Click);
            // 
            // firstDerivativeMenuItem
            // 
            this.firstDerivativeMenuItem.Name = "firstDerivativeMenuItem";
            this.firstDerivativeMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F12)));
            this.firstDerivativeMenuItem.Size = new System.Drawing.Size(297, 22);
            this.firstDerivativeMenuItem.Text = "&First Derivative";
            this.firstDerivativeMenuItem.Click += new System.EventHandler(this.firstDerivativeMenuItem_Click);
            // 
            // smoothSGChromMenuItem
            // 
            this.smoothSGChromMenuItem.Name = "smoothSGChromMenuItem";
            this.smoothSGChromMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.F12)));
            this.smoothSGChromMenuItem.Size = new System.Drawing.Size(297, 22);
            this.smoothSGChromMenuItem.Text = "Savitzky-&Golay Smoothing";
            this.smoothSGChromMenuItem.Click += new System.EventHandler(this.smoothSGChromMenuItem_Click);
            // 
            // autoZoomMenuItem
            // 
            this.autoZoomMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.autoZoomNoneMenuItem,
            this.autoZoomBestPeakMenuItem,
            this.autoZoomRTWindowMenuItem,
            this.autoZoomBothMenuItem});
            this.autoZoomMenuItem.Enabled = false;
            this.autoZoomMenuItem.Name = "autoZoomMenuItem";
            this.autoZoomMenuItem.Size = new System.Drawing.Size(191, 22);
            this.autoZoomMenuItem.Text = "Auto-&Zoom";
            this.autoZoomMenuItem.DropDownOpening += new System.EventHandler(this.autozoomMenuItem_DropDownOpening);
            // 
            // autoZoomNoneMenuItem
            // 
            this.autoZoomNoneMenuItem.Name = "autoZoomNoneMenuItem";
            this.autoZoomNoneMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F11)));
            this.autoZoomNoneMenuItem.Size = new System.Drawing.Size(254, 22);
            this.autoZoomNoneMenuItem.Text = "&None";
            this.autoZoomNoneMenuItem.Click += new System.EventHandler(this.autoZoomNoneMenuItem_Click);
            // 
            // autoZoomBestPeakMenuItem
            // 
            this.autoZoomBestPeakMenuItem.Name = "autoZoomBestPeakMenuItem";
            this.autoZoomBestPeakMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F11;
            this.autoZoomBestPeakMenuItem.Size = new System.Drawing.Size(254, 22);
            this.autoZoomBestPeakMenuItem.Text = "&Best Peak";
            this.autoZoomBestPeakMenuItem.Click += new System.EventHandler(this.autoZoomBestPeakMenuItem_Click);
            // 
            // autoZoomRTWindowMenuItem
            // 
            this.autoZoomRTWindowMenuItem.Name = "autoZoomRTWindowMenuItem";
            this.autoZoomRTWindowMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F11)));
            this.autoZoomRTWindowMenuItem.Size = new System.Drawing.Size(254, 22);
            this.autoZoomRTWindowMenuItem.Text = "&Retention Time Window";
            this.autoZoomRTWindowMenuItem.Click += new System.EventHandler(this.autoZoomRTWindowMenuItem_Click);
            // 
            // autoZoomBothMenuItem
            // 
            this.autoZoomBothMenuItem.Name = "autoZoomBothMenuItem";
            this.autoZoomBothMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.F11)));
            this.autoZoomBothMenuItem.Size = new System.Drawing.Size(254, 22);
            this.autoZoomBothMenuItem.Text = "B&oth";
            this.autoZoomBothMenuItem.Click += new System.EventHandler(this.autoZoomBothMenuItem_Click);
            // 
            // toolStripSeparator10
            // 
            this.toolStripSeparator10.Name = "toolStripSeparator10";
            this.toolStripSeparator10.Size = new System.Drawing.Size(188, 6);
            // 
            // retentionTimesMenuItem
            // 
            this.retentionTimesMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.replicateComparisonMenuItem,
            this.timePeptideComparisonMenuItem,
            this.linearRegressionMenuItem,
            this.schedulingMenuItem,
            this.retentionTimeAlignmentsToolStripMenuItem});
            this.retentionTimesMenuItem.Enabled = false;
            this.retentionTimesMenuItem.Name = "retentionTimesMenuItem";
            this.retentionTimesMenuItem.Size = new System.Drawing.Size(191, 22);
            this.retentionTimesMenuItem.Text = "Retention &Times";
            this.retentionTimesMenuItem.DropDownOpening += new System.EventHandler(this.timeGraphMenuItem_DropDownOpening);
            // 
            // replicateComparisonMenuItem
            // 
            this.replicateComparisonMenuItem.Name = "replicateComparisonMenuItem";
            this.replicateComparisonMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F8;
            this.replicateComparisonMenuItem.Size = new System.Drawing.Size(228, 22);
            this.replicateComparisonMenuItem.Text = "&Replicate Comparison";
            this.replicateComparisonMenuItem.Click += new System.EventHandler(this.replicateComparisonMenuItem_Click);
            // 
            // timePeptideComparisonMenuItem
            // 
            this.timePeptideComparisonMenuItem.Name = "timePeptideComparisonMenuItem";
            this.timePeptideComparisonMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F8)));
            this.timePeptideComparisonMenuItem.Size = new System.Drawing.Size(228, 22);
            this.timePeptideComparisonMenuItem.Text = "&Peptide Comparison";
            this.timePeptideComparisonMenuItem.Click += new System.EventHandler(this.timePeptideComparisonMenuItem_Click);
            // 
            // linearRegressionMenuItem
            // 
            this.linearRegressionMenuItem.Name = "linearRegressionMenuItem";
            this.linearRegressionMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F8)));
            this.linearRegressionMenuItem.Size = new System.Drawing.Size(228, 22);
            this.linearRegressionMenuItem.Text = "&Linear Regression";
            this.linearRegressionMenuItem.Click += new System.EventHandler(this.linearRegressionMenuItem_Click);
            // 
            // schedulingMenuItem
            // 
            this.schedulingMenuItem.Name = "schedulingMenuItem";
            this.schedulingMenuItem.Size = new System.Drawing.Size(228, 22);
            this.schedulingMenuItem.Text = "&Scheduling";
            this.schedulingMenuItem.Click += new System.EventHandler(this.schedulingMenuItem_Click);
            // 
            // retentionTimeAlignmentsToolStripMenuItem
            // 
            this.retentionTimeAlignmentsToolStripMenuItem.Name = "retentionTimeAlignmentsToolStripMenuItem";
            this.retentionTimeAlignmentsToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.retentionTimeAlignmentsToolStripMenuItem.Text = "Ali&gnment";
            this.retentionTimeAlignmentsToolStripMenuItem.Click += new System.EventHandler(this.retentionTimeAlignmentToolStripMenuItem_Click);
            // 
            // peakAreasMenuItem
            // 
            this.peakAreasMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaReplicateComparisonMenuItem,
            this.areaPeptideComparisonMenuItem});
            this.peakAreasMenuItem.Enabled = false;
            this.peakAreasMenuItem.Name = "peakAreasMenuItem";
            this.peakAreasMenuItem.Size = new System.Drawing.Size(191, 22);
            this.peakAreasMenuItem.Text = "Pea&k Areas";
            this.peakAreasMenuItem.DropDownOpening += new System.EventHandler(this.areaGraphMenuItem_DropDownOpening);
            // 
            // areaReplicateComparisonMenuItem
            // 
            this.areaReplicateComparisonMenuItem.Name = "areaReplicateComparisonMenuItem";
            this.areaReplicateComparisonMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F7;
            this.areaReplicateComparisonMenuItem.Size = new System.Drawing.Size(228, 22);
            this.areaReplicateComparisonMenuItem.Text = "&Replicate Comparison";
            this.areaReplicateComparisonMenuItem.Click += new System.EventHandler(this.areaReplicateComparisonMenuItem_Click);
            // 
            // areaPeptideComparisonMenuItem
            // 
            this.areaPeptideComparisonMenuItem.Name = "areaPeptideComparisonMenuItem";
            this.areaPeptideComparisonMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F7)));
            this.areaPeptideComparisonMenuItem.Size = new System.Drawing.Size(228, 22);
            this.areaPeptideComparisonMenuItem.Text = "&Peptide Comparison";
            this.areaPeptideComparisonMenuItem.Click += new System.EventHandler(this.areaPeptideComparisonMenuItem_Click);
            // 
            // resultsGridMenuItem
            // 
            this.resultsGridMenuItem.Enabled = false;
            this.resultsGridMenuItem.Name = "resultsGridMenuItem";
            this.resultsGridMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.D2)));
            this.resultsGridMenuItem.Size = new System.Drawing.Size(191, 22);
            this.resultsGridMenuItem.Text = "Results &Grid";
            this.resultsGridMenuItem.Click += new System.EventHandler(this.resultsGridMenuItem_Click);
            // 
            // toolStripSeparator36
            // 
            this.toolStripSeparator36.Name = "toolStripSeparator36";
            this.toolStripSeparator36.Size = new System.Drawing.Size(188, 6);
            // 
            // toolBarToolStripMenuItem
            // 
            this.toolBarToolStripMenuItem.Checked = true;
            this.toolBarToolStripMenuItem.CheckOnClick = true;
            this.toolBarToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.toolBarToolStripMenuItem.Name = "toolBarToolStripMenuItem";
            this.toolBarToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.toolBarToolStripMenuItem.Text = "Tool &Bar";
            this.toolBarToolStripMenuItem.Click += new System.EventHandler(this.toolBarToolStripMenuItem_Click);
            // 
            // statusToolStripMenuItem
            // 
            this.statusToolStripMenuItem.Checked = true;
            this.statusToolStripMenuItem.CheckOnClick = true;
            this.statusToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.statusToolStripMenuItem.Name = "statusToolStripMenuItem";
            this.statusToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.statusToolStripMenuItem.Text = "&Status Bar";
            this.statusToolStripMenuItem.Click += new System.EventHandler(this.statusToolStripMenuItem_Click);
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
            this.annotationsToolStripMenuItem,
            this.toolStripSeparator37,
            this.integrateAllMenuItem});
            this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            this.settingsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.settingsToolStripMenuItem.Text = "&Settings";
            this.settingsToolStripMenuItem.DropDownOpening += new System.EventHandler(this.settingsMenu_DropDownOpening);
            // 
            // toolStripSeparatorSettings
            // 
            this.toolStripSeparatorSettings.Name = "toolStripSeparatorSettings";
            this.toolStripSeparatorSettings.Size = new System.Drawing.Size(178, 6);
            this.toolStripSeparatorSettings.Visible = false;
            // 
            // saveCurrentMenuItem
            // 
            this.saveCurrentMenuItem.Name = "saveCurrentMenuItem";
            this.saveCurrentMenuItem.Size = new System.Drawing.Size(181, 22);
            this.saveCurrentMenuItem.Text = "&Save Current...";
            this.saveCurrentMenuItem.Click += new System.EventHandler(this.saveCurrentMenuItem_Click);
            // 
            // editSettingsMenuItem
            // 
            this.editSettingsMenuItem.Name = "editSettingsMenuItem";
            this.editSettingsMenuItem.Size = new System.Drawing.Size(181, 22);
            this.editSettingsMenuItem.Text = "&Edit List...";
            this.editSettingsMenuItem.Click += new System.EventHandler(this.editSettingsMenuItem_Click);
            // 
            // toolStripSeparator31
            // 
            this.toolStripSeparator31.Name = "toolStripSeparator31";
            this.toolStripSeparator31.Size = new System.Drawing.Size(178, 6);
            // 
            // shareSettingsMenuItem
            // 
            this.shareSettingsMenuItem.Name = "shareSettingsMenuItem";
            this.shareSettingsMenuItem.Size = new System.Drawing.Size(181, 22);
            this.shareSettingsMenuItem.Text = "Sh&are...";
            this.shareSettingsMenuItem.Click += new System.EventHandler(this.shareSettingsMenuItem_Click);
            // 
            // importSettingsMenuItem1
            // 
            this.importSettingsMenuItem1.Name = "importSettingsMenuItem1";
            this.importSettingsMenuItem1.Size = new System.Drawing.Size(181, 22);
            this.importSettingsMenuItem1.Text = "&Import...";
            this.importSettingsMenuItem1.Click += new System.EventHandler(this.importSettingsMenuItem1_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(178, 6);
            // 
            // peptideSettingsMenuItem
            // 
            this.peptideSettingsMenuItem.Name = "peptideSettingsMenuItem";
            this.peptideSettingsMenuItem.Size = new System.Drawing.Size(181, 22);
            this.peptideSettingsMenuItem.Text = "&Peptide Settings...";
            this.peptideSettingsMenuItem.Click += new System.EventHandler(this.peptideSettingsMenuItem_Click);
            // 
            // transitionSettingsMenuItem
            // 
            this.transitionSettingsMenuItem.Name = "transitionSettingsMenuItem";
            this.transitionSettingsMenuItem.Size = new System.Drawing.Size(181, 22);
            this.transitionSettingsMenuItem.Text = "&Transition Settings...";
            this.transitionSettingsMenuItem.Click += new System.EventHandler(this.transitionSettingsMenuItem_Click);
            // 
            // annotationsToolStripMenuItem
            // 
            this.annotationsToolStripMenuItem.Name = "annotationsToolStripMenuItem";
            this.annotationsToolStripMenuItem.Size = new System.Drawing.Size(181, 22);
            this.annotationsToolStripMenuItem.Text = "&Annotations...";
            this.annotationsToolStripMenuItem.Click += new System.EventHandler(this.annotationsToolStripMenuItem_Click);
            // 
            // toolStripSeparator37
            // 
            this.toolStripSeparator37.Name = "toolStripSeparator37";
            this.toolStripSeparator37.Size = new System.Drawing.Size(178, 6);
            // 
            // integrateAllMenuItem
            // 
            this.integrateAllMenuItem.Name = "integrateAllMenuItem";
            this.integrateAllMenuItem.Size = new System.Drawing.Size(181, 22);
            this.integrateAllMenuItem.Text = "I&ntegrate All";
            this.integrateAllMenuItem.Click += new System.EventHandler(this.integrateAllMenuItem_Click);
            // 
            // toolsMenu
            // 
            this.toolsMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.placeholderToolsMenuItem,
            this.toolStripSeparator46,
            this.optionsToolStripMenuItem,
            this.toolStripSeparator47,
            this.configureToolsMenuItem});
            this.toolsMenu.Name = "toolsMenu";
            this.toolsMenu.Size = new System.Drawing.Size(48, 20);
            this.toolsMenu.Text = "&Tools";
            this.toolsMenu.Visible = false;
            this.toolsMenu.DropDownOpening += new System.EventHandler(this.toolsMenu_DropDownOpening);
            // 
            // placeholderToolsMenuItem
            // 
            this.placeholderToolsMenuItem.Name = "placeholderToolsMenuItem";
            this.placeholderToolsMenuItem.Size = new System.Drawing.Size(152, 22);
            this.placeholderToolsMenuItem.Text = "<placeholder>";
            // 
            // toolStripSeparator46
            // 
            this.toolStripSeparator46.Name = "toolStripSeparator46";
            this.toolStripSeparator46.Size = new System.Drawing.Size(149, 6);
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.optionsToolStripMenuItem.Text = "Options";
            this.optionsToolStripMenuItem.Click += new System.EventHandler(this.optionsToolStripMenuItem_Click);
            // 
            // toolStripSeparator47
            // 
            this.toolStripSeparator47.Name = "toolStripSeparator47";
            this.toolStripSeparator47.Size = new System.Drawing.Size(149, 6);
            // 
            // configureToolsMenuItem
            // 
            this.configureToolsMenuItem.Name = "configureToolsMenuItem";
            this.configureToolsMenuItem.Size = new System.Drawing.Size(152, 22);
            this.configureToolsMenuItem.Text = "&Configure...";
            this.configureToolsMenuItem.Click += new System.EventHandler(this.configureToolsMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.homeMenuItem,
            this.videosMenuItem,
            this.tutorialsMenuItem,
            this.supportMenuItem,
            this.issuesMenuItem,
            this.toolStripSeparator29,
            this.aboutMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "&Help";
            // 
            // homeMenuItem
            // 
            this.homeMenuItem.Name = "homeMenuItem";
            this.homeMenuItem.Size = new System.Drawing.Size(120, 22);
            this.homeMenuItem.Text = "&Home";
            this.homeMenuItem.Click += new System.EventHandler(this.homeMenuItem_Click);
            // 
            // videosMenuItem
            // 
            this.videosMenuItem.Name = "videosMenuItem";
            this.videosMenuItem.Size = new System.Drawing.Size(120, 22);
            this.videosMenuItem.Text = "&Videos";
            this.videosMenuItem.Click += new System.EventHandler(this.videosMenuItem_Click);
            // 
            // tutorialsMenuItem
            // 
            this.tutorialsMenuItem.Name = "tutorialsMenuItem";
            this.tutorialsMenuItem.Size = new System.Drawing.Size(120, 22);
            this.tutorialsMenuItem.Text = "&Tutorials";
            this.tutorialsMenuItem.Click += new System.EventHandler(this.tutorialsMenuItem_Click);
            // 
            // supportMenuItem
            // 
            this.supportMenuItem.Name = "supportMenuItem";
            this.supportMenuItem.Size = new System.Drawing.Size(120, 22);
            this.supportMenuItem.Text = "&Support";
            this.supportMenuItem.Click += new System.EventHandler(this.supportMenuItem_Click);
            // 
            // issuesMenuItem
            // 
            this.issuesMenuItem.Name = "issuesMenuItem";
            this.issuesMenuItem.Size = new System.Drawing.Size(120, 22);
            this.issuesMenuItem.Text = "&Issues";
            this.issuesMenuItem.Click += new System.EventHandler(this.issuesMenuItem_Click);
            // 
            // toolStripSeparator29
            // 
            this.toolStripSeparator29.Name = "toolStripSeparator29";
            this.toolStripSeparator29.Size = new System.Drawing.Size(117, 6);
            // 
            // aboutMenuItem
            // 
            this.aboutMenuItem.Name = "aboutMenuItem";
            this.aboutMenuItem.Size = new System.Drawing.Size(120, 22);
            this.aboutMenuItem.Text = "&About";
            this.aboutMenuItem.Click += new System.EventHandler(this.aboutMenuItem_Click);
            // 
            // SkylineWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(734, 514);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.mainToolStrip);
            this.Controls.Add(this.menuMain);
            this.Icon = global::pwiz.Skyline.Properties.Resources.Skyline;
            this.MainMenuStrip = this.menuMain;
            this.Name = "SkylineWindow";
            this.Text = "Skyline";
            this.Activated += new System.EventHandler(this.SkylineWindow_Activated);
            this.Move += new System.EventHandler(this.SkylineWindow_Move);
            this.Resize += new System.EventHandler(this.SkylineWindow_Resize);
            this.contextMenuTreeNode.ResumeLayout(false);
            this.contextMenuSpectrum.ResumeLayout(false);
            this.contextMenuChromatogram.ResumeLayout(false);
            this.contextMenuRetentionTimes.ResumeLayout(false);
            this.contextMenuPeakAreas.ResumeLayout(false);
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
        private System.Windows.Forms.ToolStripMenuItem cutMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteMenuItem;
        private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorSettings;
        private System.Windows.Forms.ToolStripMenuItem saveCurrentMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editSettingsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem peptideSettingsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transitionSettingsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusSequences;
        private System.Windows.Forms.ToolStripStatusLabel statusPrecursors;
        private System.Windows.Forms.ToolStripStatusLabel statusIons;
        private System.Windows.Forms.ToolStripStatusLabel statusGeneral;
        private System.Windows.Forms.ToolStripMenuItem expandAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandPeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem collapseAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripMenuItem collapseProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem collapsePeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFASTAMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importResultsMenuItem;
        private System.Windows.Forms.ToolStripSeparator mruBeforeToolStripSeparator;
        private System.Windows.Forms.ToolStripProgressBar statusProgress;
        private System.Windows.Forms.ContextMenuStrip contextMenuTreeNode;
        private System.Windows.Forms.ToolStripMenuItem cutContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem pickChildrenContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem editNoteContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator8;
        private System.Windows.Forms.ToolStripMenuItem editNoteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem graphsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator9;
        private System.Windows.Forms.ToolStripMenuItem statusToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator10;
        private System.Windows.Forms.ToolStripMenuItem ionTypesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cMenuItem;
        private System.Windows.Forms.ToolStripMenuItem xMenuItem;
        private System.Windows.Forms.ToolStripMenuItem yMenuItem;
        private System.Windows.Forms.ToolStripMenuItem zMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chargesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge1MenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge2MenuItem;
        private System.Windows.Forms.ToolStripMenuItem ranksMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuSpectrum;
        private System.Windows.Forms.ToolStripMenuItem aionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem xionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem yionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem zionsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
        private System.Windows.Forms.ToolStripMenuItem ranksContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator13;
        private System.Windows.Forms.ToolStripMenuItem zoomSpectrumContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator14;
        private System.Windows.Forms.ToolStripMenuItem duplicatesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem lockYaxisContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator15;
        private System.Windows.Forms.ToolStripMenuItem importMassListMenuItem;
        private DigitalRune.Windows.Docking.DockPanel dockPanel;
        private System.Windows.Forms.ContextMenuStrip contextMenuChromatogram;
        private System.Windows.Forms.ToolStripMenuItem retentionTimesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem retentionTimePredContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator16;
        private System.Windows.Forms.ToolStripMenuItem transitionsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator17;
        private System.Windows.Forms.ToolStripMenuItem lockYChromContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator18;
        private System.Windows.Forms.ToolStripMenuItem zoomChromContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator19;
        private System.Windows.Forms.ToolStripMenuItem chromatogramsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showChromMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoZoomContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoZoomNoneContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoZoomBestPeakContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoZoomRTWindowContextMenuItem;
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
        private System.Windows.Forms.ToolStripMenuItem toolBarToolStripMenuItem;        
        private System.Windows.Forms.ToolStripMenuItem autoZoomBothContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transformChromContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem secondDerivativeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem smoothSGChromContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transformChromNoneContextMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuRetentionTimes;
        private System.Windows.Forms.ToolStripMenuItem refineRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setRTThresholdContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator22;
        private System.Windows.Forms.ToolStripMenuItem createRTRegressionContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator23;
        private System.Windows.Forms.ToolStripMenuItem removeRTOutliersContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeRTContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator24;
        private System.Windows.Forms.ToolStripMenuItem zoomOutRTContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator25;
        private System.Windows.Forms.ToolStripMenuItem predictionRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleTranContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem allTranContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem totalTranContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem firstDerivativeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportTransitionListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportReportMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeGraphContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem linearRegressionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chromPropsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator26;
        private System.Windows.Forms.ToolStripMenuItem spectrumPropsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator27;
        private System.Windows.Forms.ToolStripMenuItem schedulingContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transitionsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleTranMenuItem;
        private System.Windows.Forms.ToolStripMenuItem allTranMenuItem;
        private System.Windows.Forms.ToolStripMenuItem totalTranMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transformChromMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transformChromNoneMenuItem;
        private System.Windows.Forms.ToolStripMenuItem secondDerivativeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem firstDerivativeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem smoothSGChromMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoZoomMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoZoomNoneMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoZoomBestPeakMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoZoomRTWindowMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoZoomBothMenuItem;
        private System.Windows.Forms.ToolStripMenuItem retentionTimesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem linearRegressionMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateComparisonMenuItem;
        private System.Windows.Forms.ToolStripMenuItem schedulingMenuItem;
        private System.Windows.Forms.ToolStripMenuItem supportMenuItem;
        private System.Windows.Forms.ToolStripMenuItem issuesMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator29;
        private System.Windows.Forms.ToolStripMenuItem homeMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator30;
        private System.Windows.Forms.ToolStripMenuItem modifyPeptideContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem modifyPeptideMenuItem;
        private System.Windows.Forms.ToolStripMenuItem findPeptideMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator31;
        private System.Windows.Forms.ToolStripMenuItem manageUniquePeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem shareSettingsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importSettingsMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem insertToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem insertFASTAMenuItem;
        private System.Windows.Forms.ToolStripMenuItem insertPeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem insertProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem insertTransitionListMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator32;
        private System.Windows.Forms.ToolStripMenuItem expandPrecursorsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem collapsePrecursorsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem previousReplicateMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorReplicates;
        private System.Windows.Forms.ToolStripMenuItem nextReplicateMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator34;
        private System.Windows.Forms.ToolStripMenuItem arrangeGraphsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem arrangeTiledMenuItem;
        private System.Windows.Forms.ToolStripMenuItem arrangedTabbedMenuItem;
        private System.Windows.Forms.ToolStripMenuItem groupedMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removePeakContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removePeakGraphMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removePeaksGraphMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removePeaksGraphSubMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator33;
        private System.Windows.Forms.ToolStripMenuItem exportMethodMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem allRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bestRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem thresholdRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem noneRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peakBoundariesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peakAreasMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaReplicateComparisonMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaPeptideComparisonMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuPeakAreas;
        private System.Windows.Forms.ToolStripMenuItem areaGraphContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaReplicateComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaPeptideComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaNormalizeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem resultsGridMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideLogScaleContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderDocumentContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderAreaContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator36;
        private System.Windows.Forms.ToolStripMenuItem peptideCvsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem annotationsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refineToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem legendChromContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem shareDocumentMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator37;
        private System.Windows.Forms.ToolStripMenuItem removeDuplicatePeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeRepeatedPeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeEmptyProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeMissingResultsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refineAdvancedMenuItem;
        private System.Windows.Forms.ToolStripMenuItem precursorIonMenuItem;
        private System.Windows.Forms.ToolStripMenuItem precursorIonContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator35;
        private System.Windows.Forms.ToolStripMenuItem integrateAllMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator28;
        private System.Windows.Forms.ToolStripMenuItem areaPropsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePeptideComparisonMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePeptideComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideRTValueMenuItem;
        private System.Windows.Forms.ToolStripMenuItem allRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fwhmRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fwbRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePropsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator38;
        private System.Windows.Forms.ToolStripMenuItem manageResultsMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel statusPeptides;
        private System.Windows.Forms.ToolStripMenuItem ratiosContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorRatios;
        private System.Windows.Forms.ToolStripMenuItem placeholderToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaNormalizeTotalContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator40;
        private System.Windows.Forms.ToolStripMenuItem areaNormalizeNoneContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem synchronizeZoomingContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicatesRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem averageReplicatesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleReplicateRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bestReplicateRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicatesTreeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleReplicateTreeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bestReplicateTreeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem videosMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tutorialsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectAllMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importDocumentMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ionMzValuesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem observedMzValuesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem spectralLibrariesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator39;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator41;
        private System.Windows.Forms.ToolStripMenuItem textZoomToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem defaultTextToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem largeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extraLargeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem acceptPeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem findNextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator42;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator43;
        private System.Windows.Forms.ToolStripMenuItem areaNormalizeMaximumContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge3MenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge4MenuItem;
        private System.Windows.Forms.ToolStripMenuItem chargesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge1ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge2ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge3ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge4ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLibraryPeakAreaContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateOrderContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateOrderDocumentContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateOrderAcqTimeContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator44;
        private System.Windows.Forms.ToolStripMenuItem closeAllChromatogramsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showDotProductToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showPeakAreaLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showRTLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem scopeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentScopeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem proteinScopeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem precursorsTranContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem productsTranContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem precursorsTranMenuItem;
        private System.Windows.Forms.ToolStripMenuItem productsTranMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator45;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chooseCalculatorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem placeholderToolStripMenuItem1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorCalculators;
        private System.Windows.Forms.ToolStripMenuItem updateCalculatorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addCalculatorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideIDTimesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem generateDecoysMenuItem;
        private System.Windows.Forms.ToolStripMenuItem eSPFeaturesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem alignedPeptideIDTimesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem retentionTimeAlignmentsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportIsolationListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem alignRTToSelectionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsMenu;
        private System.Windows.Forms.ToolStripMenuItem placeholderToolsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem configureToolsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator47;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator46;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
    }
}

