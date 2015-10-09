
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SkylineWindow));
            this.contextMenuTreeNode = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.cutContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.pickChildrenContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addMoleculeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addSmallMoleculePrecursorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addTransitionMoleculeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removePeakContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setStandardTypeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.noStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.normStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.qcStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.irtStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.modifyPeptideContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.editNoteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorRatios = new System.Windows.Forms.ToolStripSeparator();
            this.ratiosContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ratiosToGlobalStandardsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.showLibraryChromatogramsSpectrumContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuChromatogram = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.applyPeakAllGraphMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.applyPeakSubsequentGraphMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removePeakGraphMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator33 = new System.Windows.Forms.ToolStripSeparator();
            this.legendChromContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peakBoundariesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.retentionTimesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bestRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.thresholdRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.noneRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.retentionTimePredContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideIDTimesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.idTimesNoneContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.idTimesMatchingContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.idTimesAlignedContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.idTimesOtherContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator16 = new System.Windows.Forms.ToolStripSeparator();
            this.transitionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorsTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.productsTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.totalTranContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorTran = new System.Windows.Forms.ToolStripSeparator();
            this.basePeakContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ticContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorSplitGraph = new System.Windows.Forms.ToolStripSeparator();
            this.splitGraphContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.timePlotContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeCorrelationContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeResidualsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rtValueMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fwhmRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fwbRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showRTLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.areaNormalizeGlobalContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.menuMain = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startPageMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openContainingFolderMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator53 = new System.Windows.Forms.ToolStripSeparator();
            this.saveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.shareDocumentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.publishMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.importToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peakBoundariesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator51 = new System.Windows.Forms.ToolStripSeparator();
            this.importPeptideSearchMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator52 = new System.Windows.Forms.ToolStripSeparator();
            this.importFASTAMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importMassListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importDocumentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportTransitionListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportIsolationListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportMethodMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator49 = new System.Windows.Forms.ToolStripSeparator();
            this.exportReportMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator50 = new System.Windows.Forms.ToolStripSeparator();
            this.eSPFeaturesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mProphetFeaturesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.chromatogramsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.chorusRequestToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.removeEmptyPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeDuplicatePeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeRepeatedPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeMissingResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator45 = new System.Windows.Forms.ToolStripSeparator();
            this.acceptProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renameProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsByNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsByAccessionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsByPreferredNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsByGeneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator43 = new System.Windows.Forms.ToolStripSeparator();
            this.acceptPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.generateDecoysMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reintegrateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.compareModelsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.setStandardTypeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.noStandardMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.normStandardMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.qcStandardMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.irtStandardMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.modifyPeptideMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.manageUniquePeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator30 = new System.Windows.Forms.ToolStripSeparator();
            this.manageResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showTargetsByNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showTargetsByAccessionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showTargetsByPreferredNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showTargetsByGeneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.textZoomToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.defaultTextToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.largeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extraLargeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator41 = new System.Windows.Forms.ToolStripSeparator();
            this.spectralLibrariesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator32 = new System.Windows.Forms.ToolStripSeparator();
            this.arrangeGraphsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arrangeTiledMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arrangeColumnMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arrangeRowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.toolStripSeparatorTranMain = new System.Windows.Forms.ToolStripSeparator();
            this.basePeakMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ticMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator48 = new System.Windows.Forms.ToolStripSeparator();
            this.splitGraphMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.calibrationCurveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupComparisonsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addGroupComparisonMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editGroupComparisonListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resultsGridMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentGridMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.documentSettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator37 = new System.Windows.Forms.ToolStripSeparator();
            this.integrateAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.placeholderToolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorTools = new System.Windows.Forms.ToolStripSeparator();
            this.updatesToolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStoreMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.configureToolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator46 = new System.Windows.Forms.ToolStripSeparator();
            this.immediateWindowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator47 = new System.Windows.Forms.ToolStripSeparator();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.addMoleculeContextMenuItem,
            this.addSmallMoleculePrecursorContextMenuItem,
            this.addTransitionMoleculeContextMenuItem,
            this.removePeakContextMenuItem,
            this.setStandardTypeContextMenuItem,
            this.modifyPeptideContextMenuItem,
            this.toolStripSeparator7,
            this.editNoteContextMenuItem,
            this.toolStripSeparatorRatios,
            this.ratiosContextMenuItem,
            this.replicatesTreeContextMenuItem});
            this.contextMenuTreeNode.Name = "contextMenuTreeNode";
            resources.ApplyResources(this.contextMenuTreeNode, "contextMenuTreeNode");
            this.contextMenuTreeNode.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuTreeNode_Opening);
            // 
            // cutContextMenuItem
            // 
            this.cutContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Cut;
            resources.ApplyResources(this.cutContextMenuItem, "cutContextMenuItem");
            this.cutContextMenuItem.Name = "cutContextMenuItem";
            this.cutContextMenuItem.Click += new System.EventHandler(this.cutMenuItem_Click);
            // 
            // copyContextMenuItem
            // 
            this.copyContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Copy;
            resources.ApplyResources(this.copyContextMenuItem, "copyContextMenuItem");
            this.copyContextMenuItem.Name = "copyContextMenuItem";
            this.copyContextMenuItem.Click += new System.EventHandler(this.copyMenuItem_Click);
            // 
            // pasteContextMenuItem
            // 
            this.pasteContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Paste;
            resources.ApplyResources(this.pasteContextMenuItem, "pasteContextMenuItem");
            this.pasteContextMenuItem.Name = "pasteContextMenuItem";
            this.pasteContextMenuItem.Click += new System.EventHandler(this.pasteMenuItem_Click);
            // 
            // deleteContextMenuItem
            // 
            this.deleteContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            resources.ApplyResources(this.deleteContextMenuItem, "deleteContextMenuItem");
            this.deleteContextMenuItem.Name = "deleteContextMenuItem";
            this.deleteContextMenuItem.Click += new System.EventHandler(this.deleteMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // pickChildrenContextMenuItem
            // 
            this.pickChildrenContextMenuItem.Name = "pickChildrenContextMenuItem";
            resources.ApplyResources(this.pickChildrenContextMenuItem, "pickChildrenContextMenuItem");
            this.pickChildrenContextMenuItem.Click += new System.EventHandler(this.pickChildrenContextMenuItem_Click);
            // 
            // addMoleculeContextMenuItem
            // 
            this.addMoleculeContextMenuItem.Name = "addMoleculeContextMenuItem";
            resources.ApplyResources(this.addMoleculeContextMenuItem, "addMoleculeContextMenuItem");
            this.addMoleculeContextMenuItem.Click += new System.EventHandler(this.addMoleculeContextMenuItem_Click);
            // 
            // addSmallMoleculePrecursorContextMenuItem
            // 
            this.addSmallMoleculePrecursorContextMenuItem.Name = "addSmallMoleculePrecursorContextMenuItem";
            resources.ApplyResources(this.addSmallMoleculePrecursorContextMenuItem, "addSmallMoleculePrecursorContextMenuItem");
            this.addSmallMoleculePrecursorContextMenuItem.Click += new System.EventHandler(this.addSmallMoleculePrecursorContextMenuItem_Click);
            // 
            // addTransitionMoleculeContextMenuItem
            // 
            this.addTransitionMoleculeContextMenuItem.Name = "addTransitionMoleculeContextMenuItem";
            resources.ApplyResources(this.addTransitionMoleculeContextMenuItem, "addTransitionMoleculeContextMenuItem");
            this.addTransitionMoleculeContextMenuItem.Click += new System.EventHandler(this.addTransitionMoleculeContextMenuItem_Click);
            // 
            // removePeakContextMenuItem
            // 
            this.removePeakContextMenuItem.Name = "removePeakContextMenuItem";
            resources.ApplyResources(this.removePeakContextMenuItem, "removePeakContextMenuItem");
            this.removePeakContextMenuItem.Click += new System.EventHandler(this.removePeakContextMenuItem_Click);
            // 
            // setStandardTypeContextMenuItem
            // 
            this.setStandardTypeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.noStandardContextMenuItem,
            this.normStandardContextMenuItem,
            this.qcStandardContextMenuItem,
            this.irtStandardContextMenuItem});
            this.setStandardTypeContextMenuItem.Name = "setStandardTypeContextMenuItem";
            resources.ApplyResources(this.setStandardTypeContextMenuItem, "setStandardTypeContextMenuItem");
            this.setStandardTypeContextMenuItem.DropDownOpening += new System.EventHandler(this.setStandardTypeContextMenuItem_DropDownOpening);
            // 
            // noStandardContextMenuItem
            // 
            this.noStandardContextMenuItem.Name = "noStandardContextMenuItem";
            resources.ApplyResources(this.noStandardContextMenuItem, "noStandardContextMenuItem");
            this.noStandardContextMenuItem.Click += new System.EventHandler(this.noStandardMenuItem_Click);
            // 
            // normStandardContextMenuItem
            // 
            this.normStandardContextMenuItem.Name = "normStandardContextMenuItem";
            resources.ApplyResources(this.normStandardContextMenuItem, "normStandardContextMenuItem");
            this.normStandardContextMenuItem.Click += new System.EventHandler(this.normStandardMenuItem_Click);
            // 
            // qcStandardContextMenuItem
            // 
            this.qcStandardContextMenuItem.Name = "qcStandardContextMenuItem";
            resources.ApplyResources(this.qcStandardContextMenuItem, "qcStandardContextMenuItem");
            this.qcStandardContextMenuItem.Click += new System.EventHandler(this.qcStandardMenuItem_Click);
            // 
            // irtStandardContextMenuItem
            // 
            this.irtStandardContextMenuItem.Name = "irtStandardContextMenuItem";
            resources.ApplyResources(this.irtStandardContextMenuItem, "irtStandardContextMenuItem");
            this.irtStandardContextMenuItem.Click += new System.EventHandler(this.irtStandardContextMenuItem_Click);
            // 
            // modifyPeptideContextMenuItem
            // 
            this.modifyPeptideContextMenuItem.Name = "modifyPeptideContextMenuItem";
            resources.ApplyResources(this.modifyPeptideContextMenuItem, "modifyPeptideContextMenuItem");
            this.modifyPeptideContextMenuItem.Click += new System.EventHandler(this.modifyPeptideMenuItem_Click);
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            resources.ApplyResources(this.toolStripSeparator7, "toolStripSeparator7");
            // 
            // editNoteContextMenuItem
            // 
            this.editNoteContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Comment;
            resources.ApplyResources(this.editNoteContextMenuItem, "editNoteContextMenuItem");
            this.editNoteContextMenuItem.Name = "editNoteContextMenuItem";
            this.editNoteContextMenuItem.Click += new System.EventHandler(this.editNoteMenuItem_Click);
            // 
            // toolStripSeparatorRatios
            // 
            this.toolStripSeparatorRatios.Name = "toolStripSeparatorRatios";
            resources.ApplyResources(this.toolStripSeparatorRatios, "toolStripSeparatorRatios");
            // 
            // ratiosContextMenuItem
            // 
            this.ratiosContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ratiosToGlobalStandardsMenuItem});
            this.ratiosContextMenuItem.Name = "ratiosContextMenuItem";
            resources.ApplyResources(this.ratiosContextMenuItem, "ratiosContextMenuItem");
            this.ratiosContextMenuItem.DropDownOpening += new System.EventHandler(this.ratiosContextMenuItem_DropDownOpening);
            // 
            // ratiosToGlobalStandardsMenuItem
            // 
            this.ratiosToGlobalStandardsMenuItem.Name = "ratiosToGlobalStandardsMenuItem";
            resources.ApplyResources(this.ratiosToGlobalStandardsMenuItem, "ratiosToGlobalStandardsMenuItem");
            // 
            // replicatesTreeContextMenuItem
            // 
            this.replicatesTreeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.singleReplicateTreeContextMenuItem,
            this.bestReplicateTreeContextMenuItem});
            this.replicatesTreeContextMenuItem.Name = "replicatesTreeContextMenuItem";
            resources.ApplyResources(this.replicatesTreeContextMenuItem, "replicatesTreeContextMenuItem");
            this.replicatesTreeContextMenuItem.DropDownOpening += new System.EventHandler(this.replicatesTreeContextMenuItem_DropDownOpening);
            // 
            // singleReplicateTreeContextMenuItem
            // 
            this.singleReplicateTreeContextMenuItem.Name = "singleReplicateTreeContextMenuItem";
            resources.ApplyResources(this.singleReplicateTreeContextMenuItem, "singleReplicateTreeContextMenuItem");
            this.singleReplicateTreeContextMenuItem.Click += new System.EventHandler(this.singleReplicateTreeContextMenuItem_Click);
            // 
            // bestReplicateTreeContextMenuItem
            // 
            this.bestReplicateTreeContextMenuItem.Name = "bestReplicateTreeContextMenuItem";
            resources.ApplyResources(this.bestReplicateTreeContextMenuItem, "bestReplicateTreeContextMenuItem");
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
            this.toolStripSeparator27,
            this.showLibraryChromatogramsSpectrumContextMenuItem});
            this.contextMenuSpectrum.Name = "contextMenuSpectrum";
            resources.ApplyResources(this.contextMenuSpectrum, "contextMenuSpectrum");
            // 
            // aionsContextMenuItem
            // 
            this.aionsContextMenuItem.CheckOnClick = true;
            this.aionsContextMenuItem.Name = "aionsContextMenuItem";
            resources.ApplyResources(this.aionsContextMenuItem, "aionsContextMenuItem");
            this.aionsContextMenuItem.Click += new System.EventHandler(this.aMenuItem_Click);
            // 
            // bionsContextMenuItem
            // 
            this.bionsContextMenuItem.CheckOnClick = true;
            this.bionsContextMenuItem.Name = "bionsContextMenuItem";
            resources.ApplyResources(this.bionsContextMenuItem, "bionsContextMenuItem");
            this.bionsContextMenuItem.Click += new System.EventHandler(this.bMenuItem_Click);
            // 
            // cionsContextMenuItem
            // 
            this.cionsContextMenuItem.CheckOnClick = true;
            this.cionsContextMenuItem.Name = "cionsContextMenuItem";
            resources.ApplyResources(this.cionsContextMenuItem, "cionsContextMenuItem");
            this.cionsContextMenuItem.Click += new System.EventHandler(this.cMenuItem_Click);
            // 
            // xionsContextMenuItem
            // 
            this.xionsContextMenuItem.CheckOnClick = true;
            this.xionsContextMenuItem.Name = "xionsContextMenuItem";
            resources.ApplyResources(this.xionsContextMenuItem, "xionsContextMenuItem");
            this.xionsContextMenuItem.Click += new System.EventHandler(this.xMenuItem_Click);
            // 
            // yionsContextMenuItem
            // 
            this.yionsContextMenuItem.CheckOnClick = true;
            this.yionsContextMenuItem.Name = "yionsContextMenuItem";
            resources.ApplyResources(this.yionsContextMenuItem, "yionsContextMenuItem");
            this.yionsContextMenuItem.Click += new System.EventHandler(this.yMenuItem_Click);
            // 
            // zionsContextMenuItem
            // 
            this.zionsContextMenuItem.CheckOnClick = true;
            this.zionsContextMenuItem.Name = "zionsContextMenuItem";
            resources.ApplyResources(this.zionsContextMenuItem, "zionsContextMenuItem");
            this.zionsContextMenuItem.Click += new System.EventHandler(this.zMenuItem_Click);
            // 
            // precursorIonContextMenuItem
            // 
            this.precursorIonContextMenuItem.Name = "precursorIonContextMenuItem";
            resources.ApplyResources(this.precursorIonContextMenuItem, "precursorIonContextMenuItem");
            this.precursorIonContextMenuItem.Click += new System.EventHandler(this.precursorIonMenuItem_Click);
            // 
            // toolStripSeparator11
            // 
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            resources.ApplyResources(this.toolStripSeparator11, "toolStripSeparator11");
            // 
            // chargesContextMenuItem
            // 
            this.chargesContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.charge1ContextMenuItem,
            this.charge2ContextMenuItem,
            this.charge3ContextMenuItem,
            this.charge4ContextMenuItem});
            this.chargesContextMenuItem.Name = "chargesContextMenuItem";
            resources.ApplyResources(this.chargesContextMenuItem, "chargesContextMenuItem");
            this.chargesContextMenuItem.DropDownOpening += new System.EventHandler(this.chargesMenuItem_DropDownOpening);
            // 
            // charge1ContextMenuItem
            // 
            this.charge1ContextMenuItem.Name = "charge1ContextMenuItem";
            resources.ApplyResources(this.charge1ContextMenuItem, "charge1ContextMenuItem");
            this.charge1ContextMenuItem.Click += new System.EventHandler(this.charge1MenuItem_Click);
            // 
            // charge2ContextMenuItem
            // 
            this.charge2ContextMenuItem.Name = "charge2ContextMenuItem";
            resources.ApplyResources(this.charge2ContextMenuItem, "charge2ContextMenuItem");
            this.charge2ContextMenuItem.Click += new System.EventHandler(this.charge2MenuItem_Click);
            // 
            // charge3ContextMenuItem
            // 
            this.charge3ContextMenuItem.Name = "charge3ContextMenuItem";
            resources.ApplyResources(this.charge3ContextMenuItem, "charge3ContextMenuItem");
            this.charge3ContextMenuItem.Click += new System.EventHandler(this.charge3MenuItem_Click);
            // 
            // charge4ContextMenuItem
            // 
            this.charge4ContextMenuItem.Name = "charge4ContextMenuItem";
            resources.ApplyResources(this.charge4ContextMenuItem, "charge4ContextMenuItem");
            this.charge4ContextMenuItem.Click += new System.EventHandler(this.charge4MenuItem_Click);
            // 
            // toolStripSeparator12
            // 
            this.toolStripSeparator12.Name = "toolStripSeparator12";
            resources.ApplyResources(this.toolStripSeparator12, "toolStripSeparator12");
            // 
            // ranksContextMenuItem
            // 
            this.ranksContextMenuItem.CheckOnClick = true;
            this.ranksContextMenuItem.Name = "ranksContextMenuItem";
            resources.ApplyResources(this.ranksContextMenuItem, "ranksContextMenuItem");
            this.ranksContextMenuItem.Click += new System.EventHandler(this.ranksMenuItem_Click);
            // 
            // ionMzValuesContextMenuItem
            // 
            this.ionMzValuesContextMenuItem.CheckOnClick = true;
            this.ionMzValuesContextMenuItem.Name = "ionMzValuesContextMenuItem";
            resources.ApplyResources(this.ionMzValuesContextMenuItem, "ionMzValuesContextMenuItem");
            this.ionMzValuesContextMenuItem.Click += new System.EventHandler(this.ionMzValuesContextMenuItem_Click);
            // 
            // observedMzValuesContextMenuItem
            // 
            this.observedMzValuesContextMenuItem.CheckOnClick = true;
            this.observedMzValuesContextMenuItem.Name = "observedMzValuesContextMenuItem";
            resources.ApplyResources(this.observedMzValuesContextMenuItem, "observedMzValuesContextMenuItem");
            this.observedMzValuesContextMenuItem.Click += new System.EventHandler(this.observedMzValuesContextMenuItem_Click);
            // 
            // duplicatesContextMenuItem
            // 
            this.duplicatesContextMenuItem.CheckOnClick = true;
            this.duplicatesContextMenuItem.Name = "duplicatesContextMenuItem";
            resources.ApplyResources(this.duplicatesContextMenuItem, "duplicatesContextMenuItem");
            this.duplicatesContextMenuItem.Click += new System.EventHandler(this.duplicatesContextMenuItem_Click);
            // 
            // toolStripSeparator13
            // 
            this.toolStripSeparator13.Name = "toolStripSeparator13";
            resources.ApplyResources(this.toolStripSeparator13, "toolStripSeparator13");
            // 
            // lockYaxisContextMenuItem
            // 
            this.lockYaxisContextMenuItem.CheckOnClick = true;
            this.lockYaxisContextMenuItem.Name = "lockYaxisContextMenuItem";
            resources.ApplyResources(this.lockYaxisContextMenuItem, "lockYaxisContextMenuItem");
            this.lockYaxisContextMenuItem.Click += new System.EventHandler(this.lockYaxisContextMenuItem_Click);
            // 
            // toolStripSeparator14
            // 
            this.toolStripSeparator14.Name = "toolStripSeparator14";
            resources.ApplyResources(this.toolStripSeparator14, "toolStripSeparator14");
            // 
            // spectrumPropsContextMenuItem
            // 
            this.spectrumPropsContextMenuItem.Name = "spectrumPropsContextMenuItem";
            resources.ApplyResources(this.spectrumPropsContextMenuItem, "spectrumPropsContextMenuItem");
            this.spectrumPropsContextMenuItem.Click += new System.EventHandler(this.spectrumPropsContextMenuItem_Click);
            // 
            // toolStripSeparator15
            // 
            this.toolStripSeparator15.Name = "toolStripSeparator15";
            resources.ApplyResources(this.toolStripSeparator15, "toolStripSeparator15");
            // 
            // zoomSpectrumContextMenuItem
            // 
            this.zoomSpectrumContextMenuItem.Name = "zoomSpectrumContextMenuItem";
            resources.ApplyResources(this.zoomSpectrumContextMenuItem, "zoomSpectrumContextMenuItem");
            this.zoomSpectrumContextMenuItem.Click += new System.EventHandler(this.zoomSpectrumContextMenuItem_Click);
            // 
            // toolStripSeparator27
            // 
            this.toolStripSeparator27.Name = "toolStripSeparator27";
            resources.ApplyResources(this.toolStripSeparator27, "toolStripSeparator27");
            // 
            // showLibraryChromatogramsSpectrumContextMenuItem
            // 
            this.showLibraryChromatogramsSpectrumContextMenuItem.Name = "showLibraryChromatogramsSpectrumContextMenuItem";
            resources.ApplyResources(this.showLibraryChromatogramsSpectrumContextMenuItem, "showLibraryChromatogramsSpectrumContextMenuItem");
            this.showLibraryChromatogramsSpectrumContextMenuItem.Click += new System.EventHandler(this.showChromatogramsSpectrumContextMenuItem_Click);
            // 
            // contextMenuChromatogram
            // 
            this.contextMenuChromatogram.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.applyPeakAllGraphMenuItem,
            this.applyPeakSubsequentGraphMenuItem,
            this.removePeakGraphMenuItem,
            this.toolStripSeparator33,
            this.legendChromContextMenuItem,
            this.peakBoundariesContextMenuItem,
            this.massErrorContextMenuItem,
            this.retentionTimesContextMenuItem,
            this.retentionTimePredContextMenuItem,
            this.peptideIDTimesContextMenuItem,
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
            resources.ApplyResources(this.contextMenuChromatogram, "contextMenuChromatogram");
            // 
            // applyPeakAllGraphMenuItem
            // 
            this.applyPeakAllGraphMenuItem.Name = "applyPeakAllGraphMenuItem";
            resources.ApplyResources(this.applyPeakAllGraphMenuItem, "applyPeakAllGraphMenuItem");
            this.applyPeakAllGraphMenuItem.Click += new System.EventHandler(this.applyPeakAllContextMenuItem_Click);
            // 
            // applyPeakSubsequentGraphMenuItem
            // 
            this.applyPeakSubsequentGraphMenuItem.Name = "applyPeakSubsequentGraphMenuItem";
            resources.ApplyResources(this.applyPeakSubsequentGraphMenuItem, "applyPeakSubsequentGraphMenuItem");
            this.applyPeakSubsequentGraphMenuItem.Click += new System.EventHandler(this.applyPeakSubsequentContextMenuItem_Click);
            // 
            // removePeakGraphMenuItem
            // 
            this.removePeakGraphMenuItem.Name = "removePeakGraphMenuItem";
            resources.ApplyResources(this.removePeakGraphMenuItem, "removePeakGraphMenuItem");
            this.removePeakGraphMenuItem.DropDownOpening += new System.EventHandler(this.removePeakGraphMenuItem_DropDownOpening);
            this.removePeakGraphMenuItem.Click += new System.EventHandler(this.removePeakContextMenuItem_Click);
            // 
            // toolStripSeparator33
            // 
            this.toolStripSeparator33.Name = "toolStripSeparator33";
            resources.ApplyResources(this.toolStripSeparator33, "toolStripSeparator33");
            // 
            // legendChromContextMenuItem
            // 
            this.legendChromContextMenuItem.CheckOnClick = true;
            this.legendChromContextMenuItem.Name = "legendChromContextMenuItem";
            resources.ApplyResources(this.legendChromContextMenuItem, "legendChromContextMenuItem");
            this.legendChromContextMenuItem.Click += new System.EventHandler(this.legendChromContextMenuItem_Click);
            // 
            // peakBoundariesContextMenuItem
            // 
            this.peakBoundariesContextMenuItem.CheckOnClick = true;
            this.peakBoundariesContextMenuItem.Name = "peakBoundariesContextMenuItem";
            resources.ApplyResources(this.peakBoundariesContextMenuItem, "peakBoundariesContextMenuItem");
            this.peakBoundariesContextMenuItem.Click += new System.EventHandler(this.peakBoundariesContextMenuItem_Click);
            // 
            // massErrorContextMenuItem
            // 
            this.massErrorContextMenuItem.CheckOnClick = true;
            this.massErrorContextMenuItem.Name = "massErrorContextMenuItem";
            resources.ApplyResources(this.massErrorContextMenuItem, "massErrorContextMenuItem");
            this.massErrorContextMenuItem.Click += new System.EventHandler(this.massErrorContextMenuItem_Click);
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
            resources.ApplyResources(this.retentionTimesContextMenuItem, "retentionTimesContextMenuItem");
            this.retentionTimesContextMenuItem.DropDownOpening += new System.EventHandler(this.retentionTimesContextMenuItem_DropDownOpening);
            // 
            // allRTContextMenuItem
            // 
            this.allRTContextMenuItem.Name = "allRTContextMenuItem";
            resources.ApplyResources(this.allRTContextMenuItem, "allRTContextMenuItem");
            this.allRTContextMenuItem.Click += new System.EventHandler(this.allRTContextMenuItem_Click);
            // 
            // bestRTContextMenuItem
            // 
            this.bestRTContextMenuItem.Name = "bestRTContextMenuItem";
            resources.ApplyResources(this.bestRTContextMenuItem, "bestRTContextMenuItem");
            this.bestRTContextMenuItem.Click += new System.EventHandler(this.bestRTContextMenuItem_Click);
            // 
            // thresholdRTContextMenuItem
            // 
            this.thresholdRTContextMenuItem.Name = "thresholdRTContextMenuItem";
            resources.ApplyResources(this.thresholdRTContextMenuItem, "thresholdRTContextMenuItem");
            this.thresholdRTContextMenuItem.Click += new System.EventHandler(this.thresholdRTContextMenuItem_Click);
            // 
            // noneRTContextMenuItem
            // 
            this.noneRTContextMenuItem.Name = "noneRTContextMenuItem";
            resources.ApplyResources(this.noneRTContextMenuItem, "noneRTContextMenuItem");
            this.noneRTContextMenuItem.Click += new System.EventHandler(this.noneRTContextMenuItem_Click);
            // 
            // retentionTimePredContextMenuItem
            // 
            this.retentionTimePredContextMenuItem.CheckOnClick = true;
            this.retentionTimePredContextMenuItem.Name = "retentionTimePredContextMenuItem";
            resources.ApplyResources(this.retentionTimePredContextMenuItem, "retentionTimePredContextMenuItem");
            this.retentionTimePredContextMenuItem.Click += new System.EventHandler(this.retentionTimePredContextMenuItem_Click);
            // 
            // peptideIDTimesContextMenuItem
            // 
            this.peptideIDTimesContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.idTimesNoneContextMenuItem,
            this.idTimesMatchingContextMenuItem,
            this.idTimesAlignedContextMenuItem,
            this.idTimesOtherContextMenuItem});
            this.peptideIDTimesContextMenuItem.Name = "peptideIDTimesContextMenuItem";
            resources.ApplyResources(this.peptideIDTimesContextMenuItem, "peptideIDTimesContextMenuItem");
            // 
            // idTimesNoneContextMenuItem
            // 
            this.idTimesNoneContextMenuItem.Name = "idTimesNoneContextMenuItem";
            resources.ApplyResources(this.idTimesNoneContextMenuItem, "idTimesNoneContextMenuItem");
            this.idTimesNoneContextMenuItem.Click += new System.EventHandler(this.idTimesNoneContextMenuItem_Click);
            // 
            // idTimesMatchingContextMenuItem
            // 
            this.idTimesMatchingContextMenuItem.CheckOnClick = true;
            this.idTimesMatchingContextMenuItem.Name = "idTimesMatchingContextMenuItem";
            resources.ApplyResources(this.idTimesMatchingContextMenuItem, "idTimesMatchingContextMenuItem");
            this.idTimesMatchingContextMenuItem.Click += new System.EventHandler(this.peptideIDTimesContextMenuItem_Click);
            // 
            // idTimesAlignedContextMenuItem
            // 
            this.idTimesAlignedContextMenuItem.CheckOnClick = true;
            this.idTimesAlignedContextMenuItem.Name = "idTimesAlignedContextMenuItem";
            resources.ApplyResources(this.idTimesAlignedContextMenuItem, "idTimesAlignedContextMenuItem");
            this.idTimesAlignedContextMenuItem.Click += new System.EventHandler(this.alignedPeptideIDTimesToolStripMenuItem_Click);
            // 
            // idTimesOtherContextMenuItem
            // 
            this.idTimesOtherContextMenuItem.CheckOnClick = true;
            this.idTimesOtherContextMenuItem.Name = "idTimesOtherContextMenuItem";
            resources.ApplyResources(this.idTimesOtherContextMenuItem, "idTimesOtherContextMenuItem");
            this.idTimesOtherContextMenuItem.Click += new System.EventHandler(this.peptideIDTimesFromOtherRunsToolStripMenuItem_Click);
            // 
            // toolStripSeparator16
            // 
            this.toolStripSeparator16.Name = "toolStripSeparator16";
            resources.ApplyResources(this.toolStripSeparator16, "toolStripSeparator16");
            // 
            // transitionsContextMenuItem
            // 
            this.transitionsContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allTranContextMenuItem,
            this.precursorsTranContextMenuItem,
            this.productsTranContextMenuItem,
            this.singleTranContextMenuItem,
            this.totalTranContextMenuItem,
            this.toolStripSeparatorTran,
            this.basePeakContextMenuItem,
            this.ticContextMenuItem,
            this.toolStripSeparatorSplitGraph,
            this.splitGraphContextMenuItem});
            this.transitionsContextMenuItem.Name = "transitionsContextMenuItem";
            resources.ApplyResources(this.transitionsContextMenuItem, "transitionsContextMenuItem");
            this.transitionsContextMenuItem.DropDownOpening += new System.EventHandler(this.transitionsMenuItem_DropDownOpening);
            // 
            // allTranContextMenuItem
            // 
            this.allTranContextMenuItem.Name = "allTranContextMenuItem";
            resources.ApplyResources(this.allTranContextMenuItem, "allTranContextMenuItem");
            this.allTranContextMenuItem.Click += new System.EventHandler(this.allTranMenuItem_Click);
            // 
            // precursorsTranContextMenuItem
            // 
            this.precursorsTranContextMenuItem.Name = "precursorsTranContextMenuItem";
            resources.ApplyResources(this.precursorsTranContextMenuItem, "precursorsTranContextMenuItem");
            this.precursorsTranContextMenuItem.Click += new System.EventHandler(this.precursorsTranMenuItem_Click);
            // 
            // productsTranContextMenuItem
            // 
            this.productsTranContextMenuItem.Name = "productsTranContextMenuItem";
            resources.ApplyResources(this.productsTranContextMenuItem, "productsTranContextMenuItem");
            this.productsTranContextMenuItem.Click += new System.EventHandler(this.productsTranMenuItem_Click);
            // 
            // singleTranContextMenuItem
            // 
            this.singleTranContextMenuItem.Name = "singleTranContextMenuItem";
            resources.ApplyResources(this.singleTranContextMenuItem, "singleTranContextMenuItem");
            this.singleTranContextMenuItem.Click += new System.EventHandler(this.singleTranMenuItem_Click);
            // 
            // totalTranContextMenuItem
            // 
            this.totalTranContextMenuItem.Name = "totalTranContextMenuItem";
            resources.ApplyResources(this.totalTranContextMenuItem, "totalTranContextMenuItem");
            this.totalTranContextMenuItem.Click += new System.EventHandler(this.totalTranMenuItem_Click);
            // 
            // toolStripSeparatorTran
            // 
            this.toolStripSeparatorTran.Name = "toolStripSeparatorTran";
            resources.ApplyResources(this.toolStripSeparatorTran, "toolStripSeparatorTran");
            // 
            // basePeakContextMenuItem
            // 
            this.basePeakContextMenuItem.Name = "basePeakContextMenuItem";
            resources.ApplyResources(this.basePeakContextMenuItem, "basePeakContextMenuItem");
            this.basePeakContextMenuItem.Click += new System.EventHandler(this.basePeakMenuItem_Click);
            // 
            // ticContextMenuItem
            // 
            this.ticContextMenuItem.Name = "ticContextMenuItem";
            resources.ApplyResources(this.ticContextMenuItem, "ticContextMenuItem");
            this.ticContextMenuItem.Click += new System.EventHandler(this.ticMenuItem_Click);
            // 
            // toolStripSeparatorSplitGraph
            // 
            this.toolStripSeparatorSplitGraph.Name = "toolStripSeparatorSplitGraph";
            resources.ApplyResources(this.toolStripSeparatorSplitGraph, "toolStripSeparatorSplitGraph");
            // 
            // splitGraphContextMenuItem
            // 
            this.splitGraphContextMenuItem.Name = "splitGraphContextMenuItem";
            resources.ApplyResources(this.splitGraphContextMenuItem, "splitGraphContextMenuItem");
            this.splitGraphContextMenuItem.Click += new System.EventHandler(this.splitChromGraphMenuItem_Click);
            // 
            // transformChromContextMenuItem
            // 
            this.transformChromContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.transformChromNoneContextMenuItem,
            this.secondDerivativeContextMenuItem,
            this.firstDerivativeContextMenuItem,
            this.smoothSGChromContextMenuItem});
            this.transformChromContextMenuItem.Name = "transformChromContextMenuItem";
            resources.ApplyResources(this.transformChromContextMenuItem, "transformChromContextMenuItem");
            this.transformChromContextMenuItem.DropDownOpening += new System.EventHandler(this.transformChromMenuItem_DropDownOpening);
            // 
            // transformChromNoneContextMenuItem
            // 
            this.transformChromNoneContextMenuItem.Name = "transformChromNoneContextMenuItem";
            resources.ApplyResources(this.transformChromNoneContextMenuItem, "transformChromNoneContextMenuItem");
            this.transformChromNoneContextMenuItem.Click += new System.EventHandler(this.transformChromNoneMenuItem_Click);
            // 
            // secondDerivativeContextMenuItem
            // 
            this.secondDerivativeContextMenuItem.Name = "secondDerivativeContextMenuItem";
            resources.ApplyResources(this.secondDerivativeContextMenuItem, "secondDerivativeContextMenuItem");
            this.secondDerivativeContextMenuItem.Click += new System.EventHandler(this.secondDerivativeMenuItem_Click);
            // 
            // firstDerivativeContextMenuItem
            // 
            this.firstDerivativeContextMenuItem.Name = "firstDerivativeContextMenuItem";
            resources.ApplyResources(this.firstDerivativeContextMenuItem, "firstDerivativeContextMenuItem");
            this.firstDerivativeContextMenuItem.Click += new System.EventHandler(this.firstDerivativeMenuItem_Click);
            // 
            // smoothSGChromContextMenuItem
            // 
            this.smoothSGChromContextMenuItem.Name = "smoothSGChromContextMenuItem";
            resources.ApplyResources(this.smoothSGChromContextMenuItem, "smoothSGChromContextMenuItem");
            this.smoothSGChromContextMenuItem.Click += new System.EventHandler(this.smoothSGChromMenuItem_Click);
            // 
            // toolStripSeparator17
            // 
            this.toolStripSeparator17.Name = "toolStripSeparator17";
            resources.ApplyResources(this.toolStripSeparator17, "toolStripSeparator17");
            // 
            // autoZoomContextMenuItem
            // 
            this.autoZoomContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.autoZoomNoneContextMenuItem,
            this.autoZoomBestPeakContextMenuItem,
            this.autoZoomRTWindowContextMenuItem,
            this.autoZoomBothContextMenuItem});
            this.autoZoomContextMenuItem.Name = "autoZoomContextMenuItem";
            resources.ApplyResources(this.autoZoomContextMenuItem, "autoZoomContextMenuItem");
            this.autoZoomContextMenuItem.DropDownOpening += new System.EventHandler(this.autozoomMenuItem_DropDownOpening);
            // 
            // autoZoomNoneContextMenuItem
            // 
            this.autoZoomNoneContextMenuItem.Name = "autoZoomNoneContextMenuItem";
            resources.ApplyResources(this.autoZoomNoneContextMenuItem, "autoZoomNoneContextMenuItem");
            this.autoZoomNoneContextMenuItem.Click += new System.EventHandler(this.autoZoomNoneMenuItem_Click);
            // 
            // autoZoomBestPeakContextMenuItem
            // 
            this.autoZoomBestPeakContextMenuItem.Name = "autoZoomBestPeakContextMenuItem";
            resources.ApplyResources(this.autoZoomBestPeakContextMenuItem, "autoZoomBestPeakContextMenuItem");
            this.autoZoomBestPeakContextMenuItem.Click += new System.EventHandler(this.autoZoomBestPeakMenuItem_Click);
            // 
            // autoZoomRTWindowContextMenuItem
            // 
            this.autoZoomRTWindowContextMenuItem.Name = "autoZoomRTWindowContextMenuItem";
            resources.ApplyResources(this.autoZoomRTWindowContextMenuItem, "autoZoomRTWindowContextMenuItem");
            this.autoZoomRTWindowContextMenuItem.Click += new System.EventHandler(this.autoZoomRTWindowMenuItem_Click);
            // 
            // autoZoomBothContextMenuItem
            // 
            this.autoZoomBothContextMenuItem.Name = "autoZoomBothContextMenuItem";
            resources.ApplyResources(this.autoZoomBothContextMenuItem, "autoZoomBothContextMenuItem");
            this.autoZoomBothContextMenuItem.Click += new System.EventHandler(this.autoZoomBothMenuItem_Click);
            // 
            // lockYChromContextMenuItem
            // 
            this.lockYChromContextMenuItem.CheckOnClick = true;
            this.lockYChromContextMenuItem.Name = "lockYChromContextMenuItem";
            resources.ApplyResources(this.lockYChromContextMenuItem, "lockYChromContextMenuItem");
            this.lockYChromContextMenuItem.Click += new System.EventHandler(this.lockYChromContextMenuItem_Click);
            // 
            // synchronizeZoomingContextMenuItem
            // 
            this.synchronizeZoomingContextMenuItem.CheckOnClick = true;
            this.synchronizeZoomingContextMenuItem.Name = "synchronizeZoomingContextMenuItem";
            resources.ApplyResources(this.synchronizeZoomingContextMenuItem, "synchronizeZoomingContextMenuItem");
            this.synchronizeZoomingContextMenuItem.Click += new System.EventHandler(this.synchronizeZoomingContextMenuItem_Click);
            // 
            // toolStripSeparator18
            // 
            this.toolStripSeparator18.Name = "toolStripSeparator18";
            resources.ApplyResources(this.toolStripSeparator18, "toolStripSeparator18");
            // 
            // chromPropsContextMenuItem
            // 
            this.chromPropsContextMenuItem.Name = "chromPropsContextMenuItem";
            resources.ApplyResources(this.chromPropsContextMenuItem, "chromPropsContextMenuItem");
            this.chromPropsContextMenuItem.Click += new System.EventHandler(this.chromPropsContextMenuItem_Click);
            // 
            // toolStripSeparator19
            // 
            this.toolStripSeparator19.Name = "toolStripSeparator19";
            resources.ApplyResources(this.toolStripSeparator19, "toolStripSeparator19");
            // 
            // zoomChromContextMenuItem
            // 
            this.zoomChromContextMenuItem.Name = "zoomChromContextMenuItem";
            resources.ApplyResources(this.zoomChromContextMenuItem, "zoomChromContextMenuItem");
            // 
            // toolStripSeparator26
            // 
            this.toolStripSeparator26.Name = "toolStripSeparator26";
            resources.ApplyResources(this.toolStripSeparator26, "toolStripSeparator26");
            // 
            // contextMenuRetentionTimes
            // 
            this.contextMenuRetentionTimes.AllowMerge = false;
            this.contextMenuRetentionTimes.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.timeGraphContextMenuItem,
            this.timePlotContextMenuItem,
            this.rtValueMenuItem,
            this.showRTLegendContextMenuItem,
            this.selectionContextMenuItem,
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
            resources.ApplyResources(this.contextMenuRetentionTimes, "contextMenuRetentionTimes");
            // 
            // timeGraphContextMenuItem
            // 
            this.timeGraphContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.replicateComparisonContextMenuItem,
            this.timePeptideComparisonContextMenuItem,
            this.linearRegressionContextMenuItem,
            this.schedulingContextMenuItem});
            this.timeGraphContextMenuItem.Name = "timeGraphContextMenuItem";
            resources.ApplyResources(this.timeGraphContextMenuItem, "timeGraphContextMenuItem");
            this.timeGraphContextMenuItem.DropDownOpening += new System.EventHandler(this.timeGraphMenuItem_DropDownOpening);
            // 
            // replicateComparisonContextMenuItem
            // 
            this.replicateComparisonContextMenuItem.CheckOnClick = true;
            this.replicateComparisonContextMenuItem.Name = "replicateComparisonContextMenuItem";
            resources.ApplyResources(this.replicateComparisonContextMenuItem, "replicateComparisonContextMenuItem");
            this.replicateComparisonContextMenuItem.Click += new System.EventHandler(this.replicateComparisonMenuItem_Click);
            // 
            // timePeptideComparisonContextMenuItem
            // 
            this.timePeptideComparisonContextMenuItem.Name = "timePeptideComparisonContextMenuItem";
            resources.ApplyResources(this.timePeptideComparisonContextMenuItem, "timePeptideComparisonContextMenuItem");
            this.timePeptideComparisonContextMenuItem.Click += new System.EventHandler(this.timePeptideComparisonMenuItem_Click);
            // 
            // linearRegressionContextMenuItem
            // 
            this.linearRegressionContextMenuItem.CheckOnClick = true;
            this.linearRegressionContextMenuItem.Name = "linearRegressionContextMenuItem";
            resources.ApplyResources(this.linearRegressionContextMenuItem, "linearRegressionContextMenuItem");
            this.linearRegressionContextMenuItem.Click += new System.EventHandler(this.linearRegressionMenuItem_Click);
            // 
            // schedulingContextMenuItem
            // 
            this.schedulingContextMenuItem.Name = "schedulingContextMenuItem";
            resources.ApplyResources(this.schedulingContextMenuItem, "schedulingContextMenuItem");
            this.schedulingContextMenuItem.Click += new System.EventHandler(this.schedulingMenuItem_Click);
            // 
            // timePlotContextMenuItem
            // 
            this.timePlotContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.timeCorrelationContextMenuItem,
            this.timeResidualsContextMenuItem});
            this.timePlotContextMenuItem.Name = "timePlotContextMenuItem";
            resources.ApplyResources(this.timePlotContextMenuItem, "timePlotContextMenuItem");
            // 
            // timeCorrelationContextMenuItem
            // 
            this.timeCorrelationContextMenuItem.Name = "timeCorrelationContextMenuItem";
            resources.ApplyResources(this.timeCorrelationContextMenuItem, "timeCorrelationContextMenuItem");
            this.timeCorrelationContextMenuItem.Click += new System.EventHandler(this.timeCorrelationContextMenuItem_Click);
            // 
            // timeResidualsContextMenuItem
            // 
            this.timeResidualsContextMenuItem.Name = "timeResidualsContextMenuItem";
            resources.ApplyResources(this.timeResidualsContextMenuItem, "timeResidualsContextMenuItem");
            this.timeResidualsContextMenuItem.Click += new System.EventHandler(this.timeResidualsContextMenuItem_Click);
            // 
            // rtValueMenuItem
            // 
            this.rtValueMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allRTValueContextMenuItem,
            this.timeRTValueContextMenuItem,
            this.fwhmRTValueContextMenuItem,
            this.fwbRTValueContextMenuItem});
            this.rtValueMenuItem.Name = "rtValueMenuItem";
            resources.ApplyResources(this.rtValueMenuItem, "rtValueMenuItem");
            this.rtValueMenuItem.DropDownOpening += new System.EventHandler(this.peptideRTValueMenuItem_DropDownOpening);
            // 
            // allRTValueContextMenuItem
            // 
            this.allRTValueContextMenuItem.Name = "allRTValueContextMenuItem";
            resources.ApplyResources(this.allRTValueContextMenuItem, "allRTValueContextMenuItem");
            this.allRTValueContextMenuItem.Click += new System.EventHandler(this.allRTValueContextMenuItem_Click);
            // 
            // timeRTValueContextMenuItem
            // 
            this.timeRTValueContextMenuItem.Name = "timeRTValueContextMenuItem";
            resources.ApplyResources(this.timeRTValueContextMenuItem, "timeRTValueContextMenuItem");
            this.timeRTValueContextMenuItem.Click += new System.EventHandler(this.timeRTValueContextMenuItem_Click);
            // 
            // fwhmRTValueContextMenuItem
            // 
            this.fwhmRTValueContextMenuItem.Name = "fwhmRTValueContextMenuItem";
            resources.ApplyResources(this.fwhmRTValueContextMenuItem, "fwhmRTValueContextMenuItem");
            this.fwhmRTValueContextMenuItem.Click += new System.EventHandler(this.fwhmRTValueContextMenuItem_Click);
            // 
            // fwbRTValueContextMenuItem
            // 
            this.fwbRTValueContextMenuItem.Name = "fwbRTValueContextMenuItem";
            resources.ApplyResources(this.fwbRTValueContextMenuItem, "fwbRTValueContextMenuItem");
            this.fwbRTValueContextMenuItem.Click += new System.EventHandler(this.fwbRTValueContextMenuItem_Click);
            // 
            // showRTLegendContextMenuItem
            // 
            this.showRTLegendContextMenuItem.Name = "showRTLegendContextMenuItem";
            resources.ApplyResources(this.showRTLegendContextMenuItem, "showRTLegendContextMenuItem");
            this.showRTLegendContextMenuItem.Click += new System.EventHandler(this.showRTLegendContextMenuItem_Click);
            // 
            // selectionContextMenuItem
            // 
            this.selectionContextMenuItem.CheckOnClick = true;
            this.selectionContextMenuItem.Name = "selectionContextMenuItem";
            resources.ApplyResources(this.selectionContextMenuItem, "selectionContextMenuItem");
            this.selectionContextMenuItem.Click += new System.EventHandler(this.selectionContextMenuItem_Click);
            // 
            // refineRTContextMenuItem
            // 
            this.refineRTContextMenuItem.CheckOnClick = true;
            this.refineRTContextMenuItem.Name = "refineRTContextMenuItem";
            resources.ApplyResources(this.refineRTContextMenuItem, "refineRTContextMenuItem");
            this.refineRTContextMenuItem.Click += new System.EventHandler(this.refineRTContextMenuItem_Click);
            // 
            // predictionRTContextMenuItem
            // 
            this.predictionRTContextMenuItem.CheckOnClick = true;
            this.predictionRTContextMenuItem.Name = "predictionRTContextMenuItem";
            resources.ApplyResources(this.predictionRTContextMenuItem, "predictionRTContextMenuItem");
            this.predictionRTContextMenuItem.Click += new System.EventHandler(this.predictionRTContextMenuItem_Click);
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
            // setRTThresholdContextMenuItem
            // 
            this.setRTThresholdContextMenuItem.Name = "setRTThresholdContextMenuItem";
            resources.ApplyResources(this.setRTThresholdContextMenuItem, "setRTThresholdContextMenuItem");
            this.setRTThresholdContextMenuItem.Click += new System.EventHandler(this.setRTThresholdContextMenuItem_Click);
            // 
            // toolStripSeparator22
            // 
            this.toolStripSeparator22.Name = "toolStripSeparator22";
            resources.ApplyResources(this.toolStripSeparator22, "toolStripSeparator22");
            // 
            // createRTRegressionContextMenuItem
            // 
            this.createRTRegressionContextMenuItem.Name = "createRTRegressionContextMenuItem";
            resources.ApplyResources(this.createRTRegressionContextMenuItem, "createRTRegressionContextMenuItem");
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
            resources.ApplyResources(this.chooseCalculatorContextMenuItem, "chooseCalculatorContextMenuItem");
            this.chooseCalculatorContextMenuItem.DropDownOpening += new System.EventHandler(this.chooseCalculatorContextMenuItem_DropDownOpening);
            // 
            // placeholderToolStripMenuItem1
            // 
            this.placeholderToolStripMenuItem1.Name = "placeholderToolStripMenuItem1";
            resources.ApplyResources(this.placeholderToolStripMenuItem1, "placeholderToolStripMenuItem1");
            // 
            // toolStripSeparatorCalculators
            // 
            this.toolStripSeparatorCalculators.Name = "toolStripSeparatorCalculators";
            resources.ApplyResources(this.toolStripSeparatorCalculators, "toolStripSeparatorCalculators");
            // 
            // addCalculatorContextMenuItem
            // 
            this.addCalculatorContextMenuItem.Name = "addCalculatorContextMenuItem";
            resources.ApplyResources(this.addCalculatorContextMenuItem, "addCalculatorContextMenuItem");
            this.addCalculatorContextMenuItem.Click += new System.EventHandler(this.addCalculatorContextMenuItem_Click);
            // 
            // updateCalculatorContextMenuItem
            // 
            this.updateCalculatorContextMenuItem.Name = "updateCalculatorContextMenuItem";
            resources.ApplyResources(this.updateCalculatorContextMenuItem, "updateCalculatorContextMenuItem");
            this.updateCalculatorContextMenuItem.Click += new System.EventHandler(this.updateCalculatorContextMenuItem_Click);
            // 
            // toolStripSeparator23
            // 
            this.toolStripSeparator23.Name = "toolStripSeparator23";
            resources.ApplyResources(this.toolStripSeparator23, "toolStripSeparator23");
            // 
            // removeRTOutliersContextMenuItem
            // 
            this.removeRTOutliersContextMenuItem.Name = "removeRTOutliersContextMenuItem";
            resources.ApplyResources(this.removeRTOutliersContextMenuItem, "removeRTOutliersContextMenuItem");
            this.removeRTOutliersContextMenuItem.Click += new System.EventHandler(this.removeRTOutliersContextMenuItem_Click);
            // 
            // removeRTContextMenuItem
            // 
            this.removeRTContextMenuItem.Name = "removeRTContextMenuItem";
            resources.ApplyResources(this.removeRTContextMenuItem, "removeRTContextMenuItem");
            this.removeRTContextMenuItem.Click += new System.EventHandler(this.removeRTContextMenuItem_Click);
            // 
            // toolStripSeparator24
            // 
            this.toolStripSeparator24.Name = "toolStripSeparator24";
            resources.ApplyResources(this.toolStripSeparator24, "toolStripSeparator24");
            // 
            // timePropsContextMenuItem
            // 
            this.timePropsContextMenuItem.Name = "timePropsContextMenuItem";
            resources.ApplyResources(this.timePropsContextMenuItem, "timePropsContextMenuItem");
            this.timePropsContextMenuItem.Click += new System.EventHandler(this.timePropsContextMenuItem_Click);
            // 
            // toolStripSeparator38
            // 
            this.toolStripSeparator38.Name = "toolStripSeparator38";
            resources.ApplyResources(this.toolStripSeparator38, "toolStripSeparator38");
            // 
            // zoomOutRTContextMenuItem
            // 
            this.zoomOutRTContextMenuItem.Name = "zoomOutRTContextMenuItem";
            resources.ApplyResources(this.zoomOutRTContextMenuItem, "zoomOutRTContextMenuItem");
            // 
            // toolStripSeparator25
            // 
            this.toolStripSeparator25.Name = "toolStripSeparator25";
            resources.ApplyResources(this.toolStripSeparator25, "toolStripSeparator25");
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
            this.areaPropsContextMenuItem,
            this.groupReplicatesByContextMenuItem});
            this.contextMenuPeakAreas.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuPeakAreas, "contextMenuPeakAreas");
            // 
            // areaGraphContextMenuItem
            // 
            this.areaGraphContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaReplicateComparisonContextMenuItem,
            this.areaPeptideComparisonContextMenuItem});
            this.areaGraphContextMenuItem.Name = "areaGraphContextMenuItem";
            resources.ApplyResources(this.areaGraphContextMenuItem, "areaGraphContextMenuItem");
            this.areaGraphContextMenuItem.DropDownOpening += new System.EventHandler(this.areaGraphMenuItem_DropDownOpening);
            // 
            // areaReplicateComparisonContextMenuItem
            // 
            this.areaReplicateComparisonContextMenuItem.Name = "areaReplicateComparisonContextMenuItem";
            resources.ApplyResources(this.areaReplicateComparisonContextMenuItem, "areaReplicateComparisonContextMenuItem");
            this.areaReplicateComparisonContextMenuItem.Click += new System.EventHandler(this.areaReplicateComparisonMenuItem_Click);
            // 
            // areaPeptideComparisonContextMenuItem
            // 
            this.areaPeptideComparisonContextMenuItem.Name = "areaPeptideComparisonContextMenuItem";
            resources.ApplyResources(this.areaPeptideComparisonContextMenuItem, "areaPeptideComparisonContextMenuItem");
            this.areaPeptideComparisonContextMenuItem.Click += new System.EventHandler(this.areaPeptideComparisonMenuItem_Click);
            // 
            // peptideOrderContextMenuItem
            // 
            this.peptideOrderContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.peptideOrderDocumentContextMenuItem,
            this.peptideOrderRTContextMenuItem,
            this.peptideOrderAreaContextMenuItem});
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
            // areaNormalizeContextMenuItem
            // 
            this.areaNormalizeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaNormalizeGlobalContextMenuItem,
            this.areaNormalizeMaximumContextMenuItem,
            this.areaNormalizeTotalContextMenuItem,
            this.toolStripSeparator40,
            this.areaNormalizeNoneContextMenuItem});
            this.areaNormalizeContextMenuItem.Name = "areaNormalizeContextMenuItem";
            resources.ApplyResources(this.areaNormalizeContextMenuItem, "areaNormalizeContextMenuItem");
            this.areaNormalizeContextMenuItem.DropDownOpening += new System.EventHandler(this.areaNormalizeContextMenuItem_DropDownOpening);
            // 
            // areaNormalizeGlobalContextMenuItem
            // 
            this.areaNormalizeGlobalContextMenuItem.Name = "areaNormalizeGlobalContextMenuItem";
            resources.ApplyResources(this.areaNormalizeGlobalContextMenuItem, "areaNormalizeGlobalContextMenuItem");
            this.areaNormalizeGlobalContextMenuItem.Click += new System.EventHandler(this.areaNormalizeGlobalContextMenuItem_Click);
            // 
            // areaNormalizeMaximumContextMenuItem
            // 
            this.areaNormalizeMaximumContextMenuItem.Name = "areaNormalizeMaximumContextMenuItem";
            resources.ApplyResources(this.areaNormalizeMaximumContextMenuItem, "areaNormalizeMaximumContextMenuItem");
            this.areaNormalizeMaximumContextMenuItem.Click += new System.EventHandler(this.areaNormalizeMaximumContextMenuItem_Click);
            // 
            // areaNormalizeTotalContextMenuItem
            // 
            this.areaNormalizeTotalContextMenuItem.Name = "areaNormalizeTotalContextMenuItem";
            resources.ApplyResources(this.areaNormalizeTotalContextMenuItem, "areaNormalizeTotalContextMenuItem");
            this.areaNormalizeTotalContextMenuItem.Click += new System.EventHandler(this.areaNormalizeTotalContextMenuItem_Click);
            // 
            // toolStripSeparator40
            // 
            this.toolStripSeparator40.Name = "toolStripSeparator40";
            resources.ApplyResources(this.toolStripSeparator40, "toolStripSeparator40");
            // 
            // areaNormalizeNoneContextMenuItem
            // 
            this.areaNormalizeNoneContextMenuItem.Name = "areaNormalizeNoneContextMenuItem";
            resources.ApplyResources(this.areaNormalizeNoneContextMenuItem, "areaNormalizeNoneContextMenuItem");
            this.areaNormalizeNoneContextMenuItem.Click += new System.EventHandler(this.areaNormalizeNoneContextMenuItem_Click);
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
            // showPeakAreaLegendContextMenuItem
            // 
            this.showPeakAreaLegendContextMenuItem.Name = "showPeakAreaLegendContextMenuItem";
            resources.ApplyResources(this.showPeakAreaLegendContextMenuItem, "showPeakAreaLegendContextMenuItem");
            this.showPeakAreaLegendContextMenuItem.Click += new System.EventHandler(this.showPeakAreaLegendContextMenuItem_Click);
            // 
            // showLibraryPeakAreaContextMenuItem
            // 
            this.showLibraryPeakAreaContextMenuItem.CheckOnClick = true;
            this.showLibraryPeakAreaContextMenuItem.Name = "showLibraryPeakAreaContextMenuItem";
            resources.ApplyResources(this.showLibraryPeakAreaContextMenuItem, "showLibraryPeakAreaContextMenuItem");
            this.showLibraryPeakAreaContextMenuItem.Click += new System.EventHandler(this.showLibraryPeakAreaContextMenuItem_Click);
            // 
            // showDotProductToolStripMenuItem
            // 
            this.showDotProductToolStripMenuItem.Name = "showDotProductToolStripMenuItem";
            resources.ApplyResources(this.showDotProductToolStripMenuItem, "showDotProductToolStripMenuItem");
            this.showDotProductToolStripMenuItem.Click += new System.EventHandler(this.showDotProductToolStripMenuItem_Click);
            // 
            // peptideLogScaleContextMenuItem
            // 
            this.peptideLogScaleContextMenuItem.CheckOnClick = true;
            this.peptideLogScaleContextMenuItem.Name = "peptideLogScaleContextMenuItem";
            resources.ApplyResources(this.peptideLogScaleContextMenuItem, "peptideLogScaleContextMenuItem");
            this.peptideLogScaleContextMenuItem.Click += new System.EventHandler(this.peptideLogScaleContextMenuItem_Click);
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
            // areaPropsContextMenuItem
            // 
            this.areaPropsContextMenuItem.Name = "areaPropsContextMenuItem";
            resources.ApplyResources(this.areaPropsContextMenuItem, "areaPropsContextMenuItem");
            this.areaPropsContextMenuItem.Click += new System.EventHandler(this.areaPropsContextMenuItem_Click);
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
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusGeneral,
            this.statusProgress,
            this.buttonShowAllChromatograms,
            this.statusSequences,
            this.statusPeptides,
            this.statusPrecursors,
            this.statusIons});
            resources.ApplyResources(this.statusStrip, "statusStrip");
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
            this.redoToolBarButton});
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
            // menuMain
            // 
            this.menuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.settingsToolStripMenuItem,
            this.toolsMenu,
            this.helpToolStripMenuItem});
            resources.ApplyResources(this.menuMain, "menuMain");
            this.menuMain.Name = "menuMain";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startPageMenuItem,
            this.newMenuItem,
            this.openMenuItem,
            this.openContainingFolderMenuItem,
            this.toolStripSeparator53,
            this.saveMenuItem,
            this.saveAsMenuItem,
            this.shareDocumentMenuItem,
            this.publishMenuItem,
            this.toolStripSeparator2,
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
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(this.toolStripSeparator2, "toolStripSeparator2");
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
            this.importMassListMenuItem,
            this.importDocumentMenuItem});
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
            this.importFASTAMenuItem.Click += new System.EventHandler(this.importFASTAMenuItem_Click);
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
            // exportToolStripMenuItem
            // 
            this.exportToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportTransitionListMenuItem,
            this.exportIsolationListMenuItem,
            this.exportMethodMenuItem,
            this.toolStripSeparator49,
            this.exportReportMenuItem,
            this.toolStripSeparator50,
            this.eSPFeaturesMenuItem,
            this.mProphetFeaturesMenuItem,
            this.chromatogramsToolStripMenuItem,
            this.chorusRequestToolStripMenuItem});
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
            // eSPFeaturesMenuItem
            // 
            this.eSPFeaturesMenuItem.Name = "eSPFeaturesMenuItem";
            resources.ApplyResources(this.eSPFeaturesMenuItem, "eSPFeaturesMenuItem");
            this.eSPFeaturesMenuItem.Click += new System.EventHandler(this.espFeaturesMenuItem_Click);
            // 
            // mProphetFeaturesMenuItem
            // 
            this.mProphetFeaturesMenuItem.Name = "mProphetFeaturesMenuItem";
            resources.ApplyResources(this.mProphetFeaturesMenuItem, "mProphetFeaturesMenuItem");
            this.mProphetFeaturesMenuItem.Click += new System.EventHandler(this.mProphetFeaturesMenuItem_Click);
            // 
            // chromatogramsToolStripMenuItem
            // 
            this.chromatogramsToolStripMenuItem.Name = "chromatogramsToolStripMenuItem";
            resources.ApplyResources(this.chromatogramsToolStripMenuItem, "chromatogramsToolStripMenuItem");
            this.chromatogramsToolStripMenuItem.Click += new System.EventHandler(this.chromatogramsToolStripMenuItem_Click);
            // 
            // chorusRequestToolStripMenuItem
            // 
            this.chorusRequestToolStripMenuItem.Name = "chorusRequestToolStripMenuItem";
            resources.ApplyResources(this.chorusRequestToolStripMenuItem, "chorusRequestToolStripMenuItem");
            this.chorusRequestToolStripMenuItem.Click += new System.EventHandler(this.chorusRequestToolStripMenuItem_Click);
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
            this.setStandardTypeMenuItem,
            this.modifyPeptideMenuItem,
            this.manageUniquePeptidesMenuItem,
            this.toolStripSeparator30,
            this.manageResultsMenuItem});
            resources.ApplyResources(this.editToolStripMenuItem, "editToolStripMenuItem");
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            // 
            // undoMenuItem
            // 
            resources.ApplyResources(this.undoMenuItem, "undoMenuItem");
            this.undoMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Edit_Undo;
            this.undoMenuItem.Name = "undoMenuItem";
            // 
            // redoMenuItem
            // 
            resources.ApplyResources(this.redoMenuItem, "redoMenuItem");
            this.redoMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Edit_Redo;
            this.redoMenuItem.Name = "redoMenuItem";
            // 
            // toolStripSeparator34
            // 
            this.toolStripSeparator34.Name = "toolStripSeparator34";
            resources.ApplyResources(this.toolStripSeparator34, "toolStripSeparator34");
            // 
            // cutMenuItem
            // 
            this.cutMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Cut;
            resources.ApplyResources(this.cutMenuItem, "cutMenuItem");
            this.cutMenuItem.Name = "cutMenuItem";
            this.cutMenuItem.Click += new System.EventHandler(this.cutMenuItem_Click);
            // 
            // copyMenuItem
            // 
            this.copyMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Copy;
            resources.ApplyResources(this.copyMenuItem, "copyMenuItem");
            this.copyMenuItem.Name = "copyMenuItem";
            this.copyMenuItem.Click += new System.EventHandler(this.copyMenuItem_Click);
            // 
            // pasteMenuItem
            // 
            this.pasteMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Paste;
            resources.ApplyResources(this.pasteMenuItem, "pasteMenuItem");
            this.pasteMenuItem.Name = "pasteMenuItem";
            this.pasteMenuItem.Click += new System.EventHandler(this.pasteMenuItem_Click);
            // 
            // deleteMenuItem
            // 
            this.deleteMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            resources.ApplyResources(this.deleteMenuItem, "deleteMenuItem");
            this.deleteMenuItem.Name = "deleteMenuItem";
            this.deleteMenuItem.Click += new System.EventHandler(this.deleteMenuItem_Click);
            // 
            // selectAllMenuItem
            // 
            resources.ApplyResources(this.selectAllMenuItem, "selectAllMenuItem");
            this.selectAllMenuItem.Name = "selectAllMenuItem";
            this.selectAllMenuItem.Click += new System.EventHandler(this.selectAllMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            resources.ApplyResources(this.toolStripSeparator4, "toolStripSeparator4");
            // 
            // findPeptideMenuItem
            // 
            this.findPeptideMenuItem.Name = "findPeptideMenuItem";
            resources.ApplyResources(this.findPeptideMenuItem, "findPeptideMenuItem");
            this.findPeptideMenuItem.Click += new System.EventHandler(this.findMenuItem_Click);
            // 
            // findNextMenuItem
            // 
            this.findNextMenuItem.Name = "findNextMenuItem";
            resources.ApplyResources(this.findNextMenuItem, "findNextMenuItem");
            this.findNextMenuItem.Click += new System.EventHandler(this.findNextMenuItem_Click);
            // 
            // toolStripSeparator8
            // 
            this.toolStripSeparator8.Name = "toolStripSeparator8";
            resources.ApplyResources(this.toolStripSeparator8, "toolStripSeparator8");
            // 
            // editNoteToolStripMenuItem
            // 
            this.editNoteToolStripMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Comment;
            resources.ApplyResources(this.editNoteToolStripMenuItem, "editNoteToolStripMenuItem");
            this.editNoteToolStripMenuItem.Name = "editNoteToolStripMenuItem";
            this.editNoteToolStripMenuItem.Click += new System.EventHandler(this.editNoteMenuItem_Click);
            // 
            // toolStripSeparator42
            // 
            this.toolStripSeparator42.Name = "toolStripSeparator42";
            resources.ApplyResources(this.toolStripSeparator42, "toolStripSeparator42");
            // 
            // insertToolStripMenuItem
            // 
            this.insertToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.insertFASTAMenuItem,
            this.insertProteinsMenuItem,
            this.insertPeptidesMenuItem,
            this.insertTransitionListMenuItem});
            this.insertToolStripMenuItem.Name = "insertToolStripMenuItem";
            resources.ApplyResources(this.insertToolStripMenuItem, "insertToolStripMenuItem");
            // 
            // insertFASTAMenuItem
            // 
            this.insertFASTAMenuItem.Name = "insertFASTAMenuItem";
            resources.ApplyResources(this.insertFASTAMenuItem, "insertFASTAMenuItem");
            this.insertFASTAMenuItem.Click += new System.EventHandler(this.insertFASTAToolStripMenuItem_Click);
            // 
            // insertProteinsMenuItem
            // 
            this.insertProteinsMenuItem.Name = "insertProteinsMenuItem";
            resources.ApplyResources(this.insertProteinsMenuItem, "insertProteinsMenuItem");
            this.insertProteinsMenuItem.Click += new System.EventHandler(this.insertProteinsToolStripMenuItem_Click);
            // 
            // insertPeptidesMenuItem
            // 
            this.insertPeptidesMenuItem.Name = "insertPeptidesMenuItem";
            resources.ApplyResources(this.insertPeptidesMenuItem, "insertPeptidesMenuItem");
            this.insertPeptidesMenuItem.Click += new System.EventHandler(this.insertPeptidesToolStripMenuItem_Click);
            // 
            // insertTransitionListMenuItem
            // 
            this.insertTransitionListMenuItem.Name = "insertTransitionListMenuItem";
            resources.ApplyResources(this.insertTransitionListMenuItem, "insertTransitionListMenuItem");
            this.insertTransitionListMenuItem.Click += new System.EventHandler(this.insertTransitionListMenuItem_Click);
            // 
            // refineToolStripMenuItem
            // 
            this.refineToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.removeEmptyProteinsMenuItem,
            this.removeEmptyPeptidesMenuItem,
            this.removeDuplicatePeptidesMenuItem,
            this.removeRepeatedPeptidesMenuItem,
            this.removeMissingResultsMenuItem,
            this.toolStripSeparator45,
            this.acceptProteinsMenuItem,
            this.renameProteinsMenuItem,
            this.sortProteinsMenuItem,
            this.toolStripSeparator43,
            this.acceptPeptidesMenuItem,
            this.generateDecoysMenuItem,
            this.reintegrateToolStripMenuItem,
            this.compareModelsToolStripMenuItem,
            this.toolStripSeparator35,
            this.refineAdvancedMenuItem});
            this.refineToolStripMenuItem.Name = "refineToolStripMenuItem";
            resources.ApplyResources(this.refineToolStripMenuItem, "refineToolStripMenuItem");
            // 
            // removeEmptyProteinsMenuItem
            // 
            this.removeEmptyProteinsMenuItem.Name = "removeEmptyProteinsMenuItem";
            resources.ApplyResources(this.removeEmptyProteinsMenuItem, "removeEmptyProteinsMenuItem");
            this.removeEmptyProteinsMenuItem.Click += new System.EventHandler(this.removeEmptyProteinsMenuItem_Click);
            // 
            // removeEmptyPeptidesMenuItem
            // 
            this.removeEmptyPeptidesMenuItem.Name = "removeEmptyPeptidesMenuItem";
            resources.ApplyResources(this.removeEmptyPeptidesMenuItem, "removeEmptyPeptidesMenuItem");
            this.removeEmptyPeptidesMenuItem.Click += new System.EventHandler(this.removeEmptyPeptidesMenuItem_Click);
            // 
            // removeDuplicatePeptidesMenuItem
            // 
            this.removeDuplicatePeptidesMenuItem.Name = "removeDuplicatePeptidesMenuItem";
            resources.ApplyResources(this.removeDuplicatePeptidesMenuItem, "removeDuplicatePeptidesMenuItem");
            this.removeDuplicatePeptidesMenuItem.Click += new System.EventHandler(this.removeDuplicatePeptidesMenuItem_Click);
            // 
            // removeRepeatedPeptidesMenuItem
            // 
            this.removeRepeatedPeptidesMenuItem.Name = "removeRepeatedPeptidesMenuItem";
            resources.ApplyResources(this.removeRepeatedPeptidesMenuItem, "removeRepeatedPeptidesMenuItem");
            this.removeRepeatedPeptidesMenuItem.Click += new System.EventHandler(this.removeRepeatedPeptidesMenuItem_Click);
            // 
            // removeMissingResultsMenuItem
            // 
            this.removeMissingResultsMenuItem.Name = "removeMissingResultsMenuItem";
            resources.ApplyResources(this.removeMissingResultsMenuItem, "removeMissingResultsMenuItem");
            this.removeMissingResultsMenuItem.Click += new System.EventHandler(this.removeMissingResultsMenuItem_Click);
            // 
            // toolStripSeparator45
            // 
            this.toolStripSeparator45.Name = "toolStripSeparator45";
            resources.ApplyResources(this.toolStripSeparator45, "toolStripSeparator45");
            // 
            // acceptProteinsMenuItem
            // 
            this.acceptProteinsMenuItem.Name = "acceptProteinsMenuItem";
            resources.ApplyResources(this.acceptProteinsMenuItem, "acceptProteinsMenuItem");
            this.acceptProteinsMenuItem.Click += new System.EventHandler(this.acceptProteinsMenuItem_Click);
            // 
            // renameProteinsMenuItem
            // 
            this.renameProteinsMenuItem.Name = "renameProteinsMenuItem";
            resources.ApplyResources(this.renameProteinsMenuItem, "renameProteinsMenuItem");
            this.renameProteinsMenuItem.Click += new System.EventHandler(this.renameProteinsToolStripMenuItem_Click);
            // 
            // sortProteinsMenuItem
            // 
            this.sortProteinsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sortProteinsByNameToolStripMenuItem,
            this.sortProteinsByAccessionToolStripMenuItem,
            this.sortProteinsByPreferredNameToolStripMenuItem,
            this.sortProteinsByGeneToolStripMenuItem});
            this.sortProteinsMenuItem.Name = "sortProteinsMenuItem";
            resources.ApplyResources(this.sortProteinsMenuItem, "sortProteinsMenuItem");
            // 
            // sortProteinsByNameToolStripMenuItem
            // 
            this.sortProteinsByNameToolStripMenuItem.Name = "sortProteinsByNameToolStripMenuItem";
            resources.ApplyResources(this.sortProteinsByNameToolStripMenuItem, "sortProteinsByNameToolStripMenuItem");
            this.sortProteinsByNameToolStripMenuItem.Click += new System.EventHandler(this.sortProteinsByNameToolStripMenuItem_Click);
            // 
            // sortProteinsByAccessionToolStripMenuItem
            // 
            this.sortProteinsByAccessionToolStripMenuItem.Name = "sortProteinsByAccessionToolStripMenuItem";
            resources.ApplyResources(this.sortProteinsByAccessionToolStripMenuItem, "sortProteinsByAccessionToolStripMenuItem");
            this.sortProteinsByAccessionToolStripMenuItem.Click += new System.EventHandler(this.sortProteinsByAccessionToolStripMenuItem_Click);
            // 
            // sortProteinsByPreferredNameToolStripMenuItem
            // 
            this.sortProteinsByPreferredNameToolStripMenuItem.Name = "sortProteinsByPreferredNameToolStripMenuItem";
            resources.ApplyResources(this.sortProteinsByPreferredNameToolStripMenuItem, "sortProteinsByPreferredNameToolStripMenuItem");
            this.sortProteinsByPreferredNameToolStripMenuItem.Click += new System.EventHandler(this.sortProteinsByPreferredNameToolStripMenuItem_Click);
            // 
            // sortProteinsByGeneToolStripMenuItem
            // 
            this.sortProteinsByGeneToolStripMenuItem.Name = "sortProteinsByGeneToolStripMenuItem";
            resources.ApplyResources(this.sortProteinsByGeneToolStripMenuItem, "sortProteinsByGeneToolStripMenuItem");
            this.sortProteinsByGeneToolStripMenuItem.Click += new System.EventHandler(this.sortProteinsByGeneToolStripMenuItem_Click);
            // 
            // toolStripSeparator43
            // 
            this.toolStripSeparator43.Name = "toolStripSeparator43";
            resources.ApplyResources(this.toolStripSeparator43, "toolStripSeparator43");
            // 
            // acceptPeptidesMenuItem
            // 
            this.acceptPeptidesMenuItem.Name = "acceptPeptidesMenuItem";
            resources.ApplyResources(this.acceptPeptidesMenuItem, "acceptPeptidesMenuItem");
            this.acceptPeptidesMenuItem.Click += new System.EventHandler(this.acceptPeptidesMenuItem_Click);
            // 
            // generateDecoysMenuItem
            // 
            this.generateDecoysMenuItem.Name = "generateDecoysMenuItem";
            resources.ApplyResources(this.generateDecoysMenuItem, "generateDecoysMenuItem");
            this.generateDecoysMenuItem.Click += new System.EventHandler(this.generateDecoysMenuItem_Click);
            // 
            // reintegrateToolStripMenuItem
            // 
            this.reintegrateToolStripMenuItem.Name = "reintegrateToolStripMenuItem";
            resources.ApplyResources(this.reintegrateToolStripMenuItem, "reintegrateToolStripMenuItem");
            this.reintegrateToolStripMenuItem.Click += new System.EventHandler(this.reintegrateToolStripMenuItem_Click);
            // 
            // compareModelsToolStripMenuItem
            // 
            this.compareModelsToolStripMenuItem.Name = "compareModelsToolStripMenuItem";
            resources.ApplyResources(this.compareModelsToolStripMenuItem, "compareModelsToolStripMenuItem");
            this.compareModelsToolStripMenuItem.Click += new System.EventHandler(this.compareModelsToolStripMenuItem_Click);
            // 
            // toolStripSeparator35
            // 
            this.toolStripSeparator35.Name = "toolStripSeparator35";
            resources.ApplyResources(this.toolStripSeparator35, "toolStripSeparator35");
            // 
            // refineAdvancedMenuItem
            // 
            this.refineAdvancedMenuItem.Name = "refineAdvancedMenuItem";
            resources.ApplyResources(this.refineAdvancedMenuItem, "refineAdvancedMenuItem");
            this.refineAdvancedMenuItem.Click += new System.EventHandler(this.refineMenuItem_Click);
            // 
            // toolStripSeparator6
            // 
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            resources.ApplyResources(this.toolStripSeparator6, "toolStripSeparator6");
            // 
            // expandAllToolStripMenuItem
            // 
            this.expandAllToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.expandProteinsMenuItem,
            this.expandPeptidesMenuItem,
            this.expandPrecursorsMenuItem});
            this.expandAllToolStripMenuItem.Name = "expandAllToolStripMenuItem";
            resources.ApplyResources(this.expandAllToolStripMenuItem, "expandAllToolStripMenuItem");
            // 
            // expandProteinsMenuItem
            // 
            this.expandProteinsMenuItem.Name = "expandProteinsMenuItem";
            resources.ApplyResources(this.expandProteinsMenuItem, "expandProteinsMenuItem");
            this.expandProteinsMenuItem.Click += new System.EventHandler(this.expandProteinsMenuItem_Click);
            // 
            // expandPeptidesMenuItem
            // 
            this.expandPeptidesMenuItem.Name = "expandPeptidesMenuItem";
            resources.ApplyResources(this.expandPeptidesMenuItem, "expandPeptidesMenuItem");
            this.expandPeptidesMenuItem.Click += new System.EventHandler(this.expandPeptidesMenuItem_Click);
            // 
            // expandPrecursorsMenuItem
            // 
            this.expandPrecursorsMenuItem.Name = "expandPrecursorsMenuItem";
            resources.ApplyResources(this.expandPrecursorsMenuItem, "expandPrecursorsMenuItem");
            this.expandPrecursorsMenuItem.Click += new System.EventHandler(this.expandPrecursorsMenuItem_Click);
            // 
            // collapseAllToolStripMenuItem
            // 
            this.collapseAllToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.collapseProteinsMenuItem,
            this.collapsePeptidesMenuItem,
            this.collapsePrecursorsMenuItem});
            this.collapseAllToolStripMenuItem.Name = "collapseAllToolStripMenuItem";
            resources.ApplyResources(this.collapseAllToolStripMenuItem, "collapseAllToolStripMenuItem");
            // 
            // collapseProteinsMenuItem
            // 
            this.collapseProteinsMenuItem.Name = "collapseProteinsMenuItem";
            resources.ApplyResources(this.collapseProteinsMenuItem, "collapseProteinsMenuItem");
            this.collapseProteinsMenuItem.Click += new System.EventHandler(this.collapseProteinsMenuItem_Click);
            // 
            // collapsePeptidesMenuItem
            // 
            this.collapsePeptidesMenuItem.Name = "collapsePeptidesMenuItem";
            resources.ApplyResources(this.collapsePeptidesMenuItem, "collapsePeptidesMenuItem");
            this.collapsePeptidesMenuItem.Click += new System.EventHandler(this.collapsePeptidesMenuItem_Click);
            // 
            // collapsePrecursorsMenuItem
            // 
            this.collapsePrecursorsMenuItem.Name = "collapsePrecursorsMenuItem";
            resources.ApplyResources(this.collapsePrecursorsMenuItem, "collapsePrecursorsMenuItem");
            this.collapsePrecursorsMenuItem.Click += new System.EventHandler(this.collapsePrecursorsMenuItem_Click);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            resources.ApplyResources(this.toolStripSeparator5, "toolStripSeparator5");
            // 
            // setStandardTypeMenuItem
            // 
            this.setStandardTypeMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.noStandardMenuItem,
            this.normStandardMenuItem,
            this.qcStandardMenuItem,
            this.irtStandardMenuItem});
            this.setStandardTypeMenuItem.Name = "setStandardTypeMenuItem";
            resources.ApplyResources(this.setStandardTypeMenuItem, "setStandardTypeMenuItem");
            this.setStandardTypeMenuItem.DropDownOpening += new System.EventHandler(this.setStandardTypeMenuItem_DropDownOpening);
            // 
            // noStandardMenuItem
            // 
            this.noStandardMenuItem.Name = "noStandardMenuItem";
            resources.ApplyResources(this.noStandardMenuItem, "noStandardMenuItem");
            this.noStandardMenuItem.Click += new System.EventHandler(this.noStandardMenuItem_Click);
            // 
            // normStandardMenuItem
            // 
            this.normStandardMenuItem.Name = "normStandardMenuItem";
            resources.ApplyResources(this.normStandardMenuItem, "normStandardMenuItem");
            this.normStandardMenuItem.Click += new System.EventHandler(this.normStandardMenuItem_Click);
            // 
            // qcStandardMenuItem
            // 
            this.qcStandardMenuItem.Name = "qcStandardMenuItem";
            resources.ApplyResources(this.qcStandardMenuItem, "qcStandardMenuItem");
            this.qcStandardMenuItem.Click += new System.EventHandler(this.qcStandardMenuItem_Click);
            // 
            // irtStandardMenuItem
            // 
            this.irtStandardMenuItem.Checked = true;
            this.irtStandardMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            resources.ApplyResources(this.irtStandardMenuItem, "irtStandardMenuItem");
            this.irtStandardMenuItem.Name = "irtStandardMenuItem";
            // 
            // modifyPeptideMenuItem
            // 
            this.modifyPeptideMenuItem.Name = "modifyPeptideMenuItem";
            resources.ApplyResources(this.modifyPeptideMenuItem, "modifyPeptideMenuItem");
            this.modifyPeptideMenuItem.Click += new System.EventHandler(this.modifyPeptideMenuItem_Click);
            // 
            // manageUniquePeptidesMenuItem
            // 
            this.manageUniquePeptidesMenuItem.Name = "manageUniquePeptidesMenuItem";
            resources.ApplyResources(this.manageUniquePeptidesMenuItem, "manageUniquePeptidesMenuItem");
            this.manageUniquePeptidesMenuItem.Click += new System.EventHandler(this.manageUniquePeptidesMenuItem_Click);
            // 
            // toolStripSeparator30
            // 
            this.toolStripSeparator30.Name = "toolStripSeparator30";
            resources.ApplyResources(this.toolStripSeparator30, "toolStripSeparator30");
            // 
            // manageResultsMenuItem
            // 
            this.manageResultsMenuItem.Name = "manageResultsMenuItem";
            resources.ApplyResources(this.manageResultsMenuItem, "manageResultsMenuItem");
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
            this.calibrationCurveMenuItem,
            this.groupComparisonsMenuItem,
            this.resultsGridMenuItem,
            this.documentGridMenuItem,
            this.toolStripSeparator36,
            this.toolBarToolStripMenuItem,
            this.statusToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            resources.ApplyResources(this.viewToolStripMenuItem, "viewToolStripMenuItem");
            this.viewToolStripMenuItem.DropDownOpening += new System.EventHandler(this.viewToolStripMenuItem_DropDownOpening);
            // 
            // peptidesMenuItem
            // 
            this.peptidesMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showTargetsByNameToolStripMenuItem,
            this.showTargetsByAccessionToolStripMenuItem,
            this.showTargetsByPreferredNameToolStripMenuItem,
            this.showTargetsByGeneToolStripMenuItem});
            this.peptidesMenuItem.Name = "peptidesMenuItem";
            resources.ApplyResources(this.peptidesMenuItem, "peptidesMenuItem");
            this.peptidesMenuItem.DropDownOpening += new System.EventHandler(this.peptidesMenuItem_DropDownOpening);
            // 
            // showTargetsByNameToolStripMenuItem
            // 
            this.showTargetsByNameToolStripMenuItem.Checked = true;
            this.showTargetsByNameToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showTargetsByNameToolStripMenuItem.Name = "showTargetsByNameToolStripMenuItem";
            resources.ApplyResources(this.showTargetsByNameToolStripMenuItem, "showTargetsByNameToolStripMenuItem");
            this.showTargetsByNameToolStripMenuItem.Click += new System.EventHandler(this.showTargetsByNameToolStripMenuItem_Click);
            // 
            // showTargetsByAccessionToolStripMenuItem
            // 
            this.showTargetsByAccessionToolStripMenuItem.Name = "showTargetsByAccessionToolStripMenuItem";
            resources.ApplyResources(this.showTargetsByAccessionToolStripMenuItem, "showTargetsByAccessionToolStripMenuItem");
            this.showTargetsByAccessionToolStripMenuItem.Click += new System.EventHandler(this.showTargetsByAccessionToolStripMenuItem_Click);
            // 
            // showTargetsByPreferredNameToolStripMenuItem
            // 
            this.showTargetsByPreferredNameToolStripMenuItem.Name = "showTargetsByPreferredNameToolStripMenuItem";
            resources.ApplyResources(this.showTargetsByPreferredNameToolStripMenuItem, "showTargetsByPreferredNameToolStripMenuItem");
            this.showTargetsByPreferredNameToolStripMenuItem.Click += new System.EventHandler(this.showTargetsByPreferredNameToolStripMenuItem_Click);
            // 
            // showTargetsByGeneToolStripMenuItem
            // 
            this.showTargetsByGeneToolStripMenuItem.Name = "showTargetsByGeneToolStripMenuItem";
            resources.ApplyResources(this.showTargetsByGeneToolStripMenuItem, "showTargetsByGeneToolStripMenuItem");
            this.showTargetsByGeneToolStripMenuItem.Click += new System.EventHandler(this.showTargetsByGeneToolStripMenuItem_Click);
            // 
            // textZoomToolStripMenuItem
            // 
            this.textZoomToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.defaultTextToolStripMenuItem,
            this.largeToolStripMenuItem,
            this.extraLargeToolStripMenuItem});
            this.textZoomToolStripMenuItem.Name = "textZoomToolStripMenuItem";
            resources.ApplyResources(this.textZoomToolStripMenuItem, "textZoomToolStripMenuItem");
            // 
            // defaultTextToolStripMenuItem
            // 
            this.defaultTextToolStripMenuItem.Name = "defaultTextToolStripMenuItem";
            resources.ApplyResources(this.defaultTextToolStripMenuItem, "defaultTextToolStripMenuItem");
            this.defaultTextToolStripMenuItem.Click += new System.EventHandler(this.defaultToolStripMenuItem_Click);
            // 
            // largeToolStripMenuItem
            // 
            this.largeToolStripMenuItem.Name = "largeToolStripMenuItem";
            resources.ApplyResources(this.largeToolStripMenuItem, "largeToolStripMenuItem");
            this.largeToolStripMenuItem.Click += new System.EventHandler(this.largeToolStripMenuItem_Click);
            // 
            // extraLargeToolStripMenuItem
            // 
            this.extraLargeToolStripMenuItem.Name = "extraLargeToolStripMenuItem";
            resources.ApplyResources(this.extraLargeToolStripMenuItem, "extraLargeToolStripMenuItem");
            this.extraLargeToolStripMenuItem.Click += new System.EventHandler(this.extraLargeToolStripMenuItem_Click);
            // 
            // toolStripSeparator41
            // 
            this.toolStripSeparator41.Name = "toolStripSeparator41";
            resources.ApplyResources(this.toolStripSeparator41, "toolStripSeparator41");
            // 
            // spectralLibrariesToolStripMenuItem
            // 
            this.spectralLibrariesToolStripMenuItem.Name = "spectralLibrariesToolStripMenuItem";
            resources.ApplyResources(this.spectralLibrariesToolStripMenuItem, "spectralLibrariesToolStripMenuItem");
            this.spectralLibrariesToolStripMenuItem.Click += new System.EventHandler(this.spectralLibrariesToolStripMenuItem_Click);
            // 
            // toolStripSeparator32
            // 
            this.toolStripSeparator32.Name = "toolStripSeparator32";
            resources.ApplyResources(this.toolStripSeparator32, "toolStripSeparator32");
            // 
            // arrangeGraphsToolStripMenuItem
            // 
            this.arrangeGraphsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.arrangeTiledMenuItem,
            this.arrangeColumnMenuItem,
            this.arrangeRowMenuItem,
            this.arrangedTabbedMenuItem,
            this.groupedMenuItem});
            this.arrangeGraphsToolStripMenuItem.Name = "arrangeGraphsToolStripMenuItem";
            resources.ApplyResources(this.arrangeGraphsToolStripMenuItem, "arrangeGraphsToolStripMenuItem");
            // 
            // arrangeTiledMenuItem
            // 
            this.arrangeTiledMenuItem.Name = "arrangeTiledMenuItem";
            resources.ApplyResources(this.arrangeTiledMenuItem, "arrangeTiledMenuItem");
            this.arrangeTiledMenuItem.Click += new System.EventHandler(this.arrangeTiledMenuItem_Click);
            // 
            // arrangeColumnMenuItem
            // 
            this.arrangeColumnMenuItem.Name = "arrangeColumnMenuItem";
            resources.ApplyResources(this.arrangeColumnMenuItem, "arrangeColumnMenuItem");
            this.arrangeColumnMenuItem.Click += new System.EventHandler(this.arrangeColumnMenuItem_Click);
            // 
            // arrangeRowMenuItem
            // 
            this.arrangeRowMenuItem.Name = "arrangeRowMenuItem";
            resources.ApplyResources(this.arrangeRowMenuItem, "arrangeRowMenuItem");
            this.arrangeRowMenuItem.Click += new System.EventHandler(this.arrangeRowMenuItem_Click);
            // 
            // arrangedTabbedMenuItem
            // 
            this.arrangedTabbedMenuItem.Name = "arrangedTabbedMenuItem";
            resources.ApplyResources(this.arrangedTabbedMenuItem, "arrangedTabbedMenuItem");
            this.arrangedTabbedMenuItem.Click += new System.EventHandler(this.arrangeTabbedMenuItem_Click);
            // 
            // groupedMenuItem
            // 
            this.groupedMenuItem.Name = "groupedMenuItem";
            resources.ApplyResources(this.groupedMenuItem, "groupedMenuItem");
            this.groupedMenuItem.Click += new System.EventHandler(this.arrangeGroupedMenuItem_Click);
            // 
            // toolStripSeparator39
            // 
            this.toolStripSeparator39.Name = "toolStripSeparator39";
            resources.ApplyResources(this.toolStripSeparator39, "toolStripSeparator39");
            // 
            // graphsToolStripMenuItem
            // 
            resources.ApplyResources(this.graphsToolStripMenuItem, "graphsToolStripMenuItem");
            this.graphsToolStripMenuItem.Name = "graphsToolStripMenuItem";
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
            resources.ApplyResources(this.ionTypesMenuItem, "ionTypesMenuItem");
            this.ionTypesMenuItem.Name = "ionTypesMenuItem";
            this.ionTypesMenuItem.DropDownOpening += new System.EventHandler(this.ionTypesMenuItem_DropDownOpening);
            // 
            // aMenuItem
            // 
            this.aMenuItem.CheckOnClick = true;
            this.aMenuItem.Name = "aMenuItem";
            resources.ApplyResources(this.aMenuItem, "aMenuItem");
            this.aMenuItem.Click += new System.EventHandler(this.aMenuItem_Click);
            // 
            // bMenuItem
            // 
            this.bMenuItem.CheckOnClick = true;
            this.bMenuItem.Name = "bMenuItem";
            resources.ApplyResources(this.bMenuItem, "bMenuItem");
            this.bMenuItem.Click += new System.EventHandler(this.bMenuItem_Click);
            // 
            // cMenuItem
            // 
            this.cMenuItem.CheckOnClick = true;
            this.cMenuItem.Name = "cMenuItem";
            resources.ApplyResources(this.cMenuItem, "cMenuItem");
            this.cMenuItem.Click += new System.EventHandler(this.cMenuItem_Click);
            // 
            // xMenuItem
            // 
            this.xMenuItem.CheckOnClick = true;
            this.xMenuItem.Name = "xMenuItem";
            resources.ApplyResources(this.xMenuItem, "xMenuItem");
            this.xMenuItem.Click += new System.EventHandler(this.xMenuItem_Click);
            // 
            // yMenuItem
            // 
            this.yMenuItem.Checked = true;
            this.yMenuItem.CheckOnClick = true;
            this.yMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.yMenuItem.Name = "yMenuItem";
            resources.ApplyResources(this.yMenuItem, "yMenuItem");
            this.yMenuItem.Click += new System.EventHandler(this.yMenuItem_Click);
            // 
            // zMenuItem
            // 
            this.zMenuItem.CheckOnClick = true;
            this.zMenuItem.Name = "zMenuItem";
            resources.ApplyResources(this.zMenuItem, "zMenuItem");
            this.zMenuItem.Click += new System.EventHandler(this.zMenuItem_Click);
            // 
            // precursorIonMenuItem
            // 
            this.precursorIonMenuItem.Name = "precursorIonMenuItem";
            resources.ApplyResources(this.precursorIonMenuItem, "precursorIonMenuItem");
            this.precursorIonMenuItem.Click += new System.EventHandler(this.precursorIonMenuItem_Click);
            // 
            // chargesMenuItem
            // 
            this.chargesMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.charge1MenuItem,
            this.charge2MenuItem,
            this.charge3MenuItem,
            this.charge4MenuItem});
            resources.ApplyResources(this.chargesMenuItem, "chargesMenuItem");
            this.chargesMenuItem.Name = "chargesMenuItem";
            this.chargesMenuItem.DropDownOpening += new System.EventHandler(this.chargesMenuItem_DropDownOpening);
            // 
            // charge1MenuItem
            // 
            this.charge1MenuItem.Checked = true;
            this.charge1MenuItem.CheckOnClick = true;
            this.charge1MenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.charge1MenuItem.Name = "charge1MenuItem";
            resources.ApplyResources(this.charge1MenuItem, "charge1MenuItem");
            this.charge1MenuItem.Click += new System.EventHandler(this.charge1MenuItem_Click);
            // 
            // charge2MenuItem
            // 
            this.charge2MenuItem.CheckOnClick = true;
            this.charge2MenuItem.Name = "charge2MenuItem";
            resources.ApplyResources(this.charge2MenuItem, "charge2MenuItem");
            this.charge2MenuItem.Click += new System.EventHandler(this.charge2MenuItem_Click);
            // 
            // charge3MenuItem
            // 
            this.charge3MenuItem.Name = "charge3MenuItem";
            resources.ApplyResources(this.charge3MenuItem, "charge3MenuItem");
            this.charge3MenuItem.Click += new System.EventHandler(this.charge3MenuItem_Click);
            // 
            // charge4MenuItem
            // 
            this.charge4MenuItem.Name = "charge4MenuItem";
            resources.ApplyResources(this.charge4MenuItem, "charge4MenuItem");
            this.charge4MenuItem.Click += new System.EventHandler(this.charge4MenuItem_Click);
            // 
            // ranksMenuItem
            // 
            this.ranksMenuItem.Checked = true;
            this.ranksMenuItem.CheckOnClick = true;
            this.ranksMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            resources.ApplyResources(this.ranksMenuItem, "ranksMenuItem");
            this.ranksMenuItem.Name = "ranksMenuItem";
            this.ranksMenuItem.Click += new System.EventHandler(this.ranksMenuItem_Click);
            // 
            // toolStripSeparator9
            // 
            this.toolStripSeparator9.Name = "toolStripSeparator9";
            resources.ApplyResources(this.toolStripSeparator9, "toolStripSeparator9");
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
            resources.ApplyResources(this.chromatogramsMenuItem, "chromatogramsMenuItem");
            this.chromatogramsMenuItem.Name = "chromatogramsMenuItem";
            this.chromatogramsMenuItem.DropDownOpening += new System.EventHandler(this.chromatogramsMenuItem_DropDownOpening);
            // 
            // showChromMenuItem
            // 
            this.showChromMenuItem.Name = "showChromMenuItem";
            resources.ApplyResources(this.showChromMenuItem, "showChromMenuItem");
            // 
            // toolStripSeparatorReplicates
            // 
            this.toolStripSeparatorReplicates.Name = "toolStripSeparatorReplicates";
            resources.ApplyResources(this.toolStripSeparatorReplicates, "toolStripSeparatorReplicates");
            // 
            // previousReplicateMenuItem
            // 
            this.previousReplicateMenuItem.Name = "previousReplicateMenuItem";
            resources.ApplyResources(this.previousReplicateMenuItem, "previousReplicateMenuItem");
            this.previousReplicateMenuItem.Click += new System.EventHandler(this.previousReplicateMenuItem_Click);
            // 
            // nextReplicateMenuItem
            // 
            this.nextReplicateMenuItem.Name = "nextReplicateMenuItem";
            resources.ApplyResources(this.nextReplicateMenuItem, "nextReplicateMenuItem");
            this.nextReplicateMenuItem.Click += new System.EventHandler(this.nextReplicateMenuItem_Click);
            // 
            // toolStripSeparator44
            // 
            this.toolStripSeparator44.Name = "toolStripSeparator44";
            resources.ApplyResources(this.toolStripSeparator44, "toolStripSeparator44");
            // 
            // closeAllChromatogramsMenuItem
            // 
            this.closeAllChromatogramsMenuItem.Name = "closeAllChromatogramsMenuItem";
            resources.ApplyResources(this.closeAllChromatogramsMenuItem, "closeAllChromatogramsMenuItem");
            this.closeAllChromatogramsMenuItem.Click += new System.EventHandler(this.closeAllChromatogramsMenuItem_Click);
            // 
            // transitionsMenuItem
            // 
            this.transitionsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allTranMenuItem,
            this.precursorsTranMenuItem,
            this.productsTranMenuItem,
            this.singleTranMenuItem,
            this.totalTranMenuItem,
            this.toolStripSeparatorTranMain,
            this.basePeakMenuItem,
            this.ticMenuItem,
            this.toolStripSeparator48,
            this.splitGraphMenuItem});
            resources.ApplyResources(this.transitionsMenuItem, "transitionsMenuItem");
            this.transitionsMenuItem.Name = "transitionsMenuItem";
            this.transitionsMenuItem.DropDownOpening += new System.EventHandler(this.transitionsMenuItem_DropDownOpening);
            // 
            // allTranMenuItem
            // 
            this.allTranMenuItem.Name = "allTranMenuItem";
            resources.ApplyResources(this.allTranMenuItem, "allTranMenuItem");
            this.allTranMenuItem.Click += new System.EventHandler(this.allTranMenuItem_Click);
            // 
            // precursorsTranMenuItem
            // 
            this.precursorsTranMenuItem.Name = "precursorsTranMenuItem";
            resources.ApplyResources(this.precursorsTranMenuItem, "precursorsTranMenuItem");
            this.precursorsTranMenuItem.Click += new System.EventHandler(this.precursorsTranMenuItem_Click);
            // 
            // productsTranMenuItem
            // 
            this.productsTranMenuItem.Name = "productsTranMenuItem";
            resources.ApplyResources(this.productsTranMenuItem, "productsTranMenuItem");
            this.productsTranMenuItem.Click += new System.EventHandler(this.productsTranMenuItem_Click);
            // 
            // singleTranMenuItem
            // 
            this.singleTranMenuItem.Name = "singleTranMenuItem";
            resources.ApplyResources(this.singleTranMenuItem, "singleTranMenuItem");
            this.singleTranMenuItem.Click += new System.EventHandler(this.singleTranMenuItem_Click);
            // 
            // totalTranMenuItem
            // 
            this.totalTranMenuItem.Name = "totalTranMenuItem";
            resources.ApplyResources(this.totalTranMenuItem, "totalTranMenuItem");
            this.totalTranMenuItem.Click += new System.EventHandler(this.totalTranMenuItem_Click);
            // 
            // toolStripSeparatorTranMain
            // 
            this.toolStripSeparatorTranMain.Name = "toolStripSeparatorTranMain";
            resources.ApplyResources(this.toolStripSeparatorTranMain, "toolStripSeparatorTranMain");
            // 
            // basePeakMenuItem
            // 
            this.basePeakMenuItem.Name = "basePeakMenuItem";
            resources.ApplyResources(this.basePeakMenuItem, "basePeakMenuItem");
            this.basePeakMenuItem.Click += new System.EventHandler(this.basePeakMenuItem_Click);
            // 
            // ticMenuItem
            // 
            this.ticMenuItem.Name = "ticMenuItem";
            resources.ApplyResources(this.ticMenuItem, "ticMenuItem");
            this.ticMenuItem.Click += new System.EventHandler(this.ticMenuItem_Click);
            // 
            // toolStripSeparator48
            // 
            this.toolStripSeparator48.Name = "toolStripSeparator48";
            resources.ApplyResources(this.toolStripSeparator48, "toolStripSeparator48");
            // 
            // splitGraphMenuItem
            // 
            this.splitGraphMenuItem.Name = "splitGraphMenuItem";
            resources.ApplyResources(this.splitGraphMenuItem, "splitGraphMenuItem");
            this.splitGraphMenuItem.Click += new System.EventHandler(this.splitChromGraphMenuItem_Click);
            // 
            // transformChromMenuItem
            // 
            this.transformChromMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.transformChromNoneMenuItem,
            this.secondDerivativeMenuItem,
            this.firstDerivativeMenuItem,
            this.smoothSGChromMenuItem});
            resources.ApplyResources(this.transformChromMenuItem, "transformChromMenuItem");
            this.transformChromMenuItem.Name = "transformChromMenuItem";
            this.transformChromMenuItem.DropDownOpening += new System.EventHandler(this.transformChromMenuItem_DropDownOpening);
            // 
            // transformChromNoneMenuItem
            // 
            this.transformChromNoneMenuItem.Name = "transformChromNoneMenuItem";
            resources.ApplyResources(this.transformChromNoneMenuItem, "transformChromNoneMenuItem");
            this.transformChromNoneMenuItem.Click += new System.EventHandler(this.transformChromNoneMenuItem_Click);
            // 
            // secondDerivativeMenuItem
            // 
            this.secondDerivativeMenuItem.Name = "secondDerivativeMenuItem";
            resources.ApplyResources(this.secondDerivativeMenuItem, "secondDerivativeMenuItem");
            this.secondDerivativeMenuItem.Click += new System.EventHandler(this.secondDerivativeMenuItem_Click);
            // 
            // firstDerivativeMenuItem
            // 
            this.firstDerivativeMenuItem.Name = "firstDerivativeMenuItem";
            resources.ApplyResources(this.firstDerivativeMenuItem, "firstDerivativeMenuItem");
            this.firstDerivativeMenuItem.Click += new System.EventHandler(this.firstDerivativeMenuItem_Click);
            // 
            // smoothSGChromMenuItem
            // 
            this.smoothSGChromMenuItem.Name = "smoothSGChromMenuItem";
            resources.ApplyResources(this.smoothSGChromMenuItem, "smoothSGChromMenuItem");
            this.smoothSGChromMenuItem.Click += new System.EventHandler(this.smoothSGChromMenuItem_Click);
            // 
            // autoZoomMenuItem
            // 
            this.autoZoomMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.autoZoomNoneMenuItem,
            this.autoZoomBestPeakMenuItem,
            this.autoZoomRTWindowMenuItem,
            this.autoZoomBothMenuItem});
            resources.ApplyResources(this.autoZoomMenuItem, "autoZoomMenuItem");
            this.autoZoomMenuItem.Name = "autoZoomMenuItem";
            this.autoZoomMenuItem.DropDownOpening += new System.EventHandler(this.autozoomMenuItem_DropDownOpening);
            // 
            // autoZoomNoneMenuItem
            // 
            this.autoZoomNoneMenuItem.Name = "autoZoomNoneMenuItem";
            resources.ApplyResources(this.autoZoomNoneMenuItem, "autoZoomNoneMenuItem");
            this.autoZoomNoneMenuItem.Click += new System.EventHandler(this.autoZoomNoneMenuItem_Click);
            // 
            // autoZoomBestPeakMenuItem
            // 
            this.autoZoomBestPeakMenuItem.Name = "autoZoomBestPeakMenuItem";
            resources.ApplyResources(this.autoZoomBestPeakMenuItem, "autoZoomBestPeakMenuItem");
            this.autoZoomBestPeakMenuItem.Click += new System.EventHandler(this.autoZoomBestPeakMenuItem_Click);
            // 
            // autoZoomRTWindowMenuItem
            // 
            this.autoZoomRTWindowMenuItem.Name = "autoZoomRTWindowMenuItem";
            resources.ApplyResources(this.autoZoomRTWindowMenuItem, "autoZoomRTWindowMenuItem");
            this.autoZoomRTWindowMenuItem.Click += new System.EventHandler(this.autoZoomRTWindowMenuItem_Click);
            // 
            // autoZoomBothMenuItem
            // 
            this.autoZoomBothMenuItem.Name = "autoZoomBothMenuItem";
            resources.ApplyResources(this.autoZoomBothMenuItem, "autoZoomBothMenuItem");
            this.autoZoomBothMenuItem.Click += new System.EventHandler(this.autoZoomBothMenuItem_Click);
            // 
            // toolStripSeparator10
            // 
            this.toolStripSeparator10.Name = "toolStripSeparator10";
            resources.ApplyResources(this.toolStripSeparator10, "toolStripSeparator10");
            // 
            // retentionTimesMenuItem
            // 
            this.retentionTimesMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.replicateComparisonMenuItem,
            this.timePeptideComparisonMenuItem,
            this.linearRegressionMenuItem,
            this.schedulingMenuItem,
            this.retentionTimeAlignmentsToolStripMenuItem});
            resources.ApplyResources(this.retentionTimesMenuItem, "retentionTimesMenuItem");
            this.retentionTimesMenuItem.Name = "retentionTimesMenuItem";
            this.retentionTimesMenuItem.DropDownOpening += new System.EventHandler(this.timeGraphMenuItem_DropDownOpening);
            // 
            // replicateComparisonMenuItem
            // 
            this.replicateComparisonMenuItem.Name = "replicateComparisonMenuItem";
            resources.ApplyResources(this.replicateComparisonMenuItem, "replicateComparisonMenuItem");
            this.replicateComparisonMenuItem.Click += new System.EventHandler(this.replicateComparisonMenuItem_Click);
            // 
            // timePeptideComparisonMenuItem
            // 
            this.timePeptideComparisonMenuItem.Name = "timePeptideComparisonMenuItem";
            resources.ApplyResources(this.timePeptideComparisonMenuItem, "timePeptideComparisonMenuItem");
            this.timePeptideComparisonMenuItem.Click += new System.EventHandler(this.timePeptideComparisonMenuItem_Click);
            // 
            // linearRegressionMenuItem
            // 
            this.linearRegressionMenuItem.Name = "linearRegressionMenuItem";
            resources.ApplyResources(this.linearRegressionMenuItem, "linearRegressionMenuItem");
            this.linearRegressionMenuItem.Click += new System.EventHandler(this.linearRegressionMenuItem_Click);
            // 
            // schedulingMenuItem
            // 
            this.schedulingMenuItem.Name = "schedulingMenuItem";
            resources.ApplyResources(this.schedulingMenuItem, "schedulingMenuItem");
            this.schedulingMenuItem.Click += new System.EventHandler(this.schedulingMenuItem_Click);
            // 
            // retentionTimeAlignmentsToolStripMenuItem
            // 
            this.retentionTimeAlignmentsToolStripMenuItem.Name = "retentionTimeAlignmentsToolStripMenuItem";
            resources.ApplyResources(this.retentionTimeAlignmentsToolStripMenuItem, "retentionTimeAlignmentsToolStripMenuItem");
            this.retentionTimeAlignmentsToolStripMenuItem.Click += new System.EventHandler(this.retentionTimeAlignmentToolStripMenuItem_Click);
            // 
            // peakAreasMenuItem
            // 
            this.peakAreasMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaReplicateComparisonMenuItem,
            this.areaPeptideComparisonMenuItem});
            resources.ApplyResources(this.peakAreasMenuItem, "peakAreasMenuItem");
            this.peakAreasMenuItem.Name = "peakAreasMenuItem";
            this.peakAreasMenuItem.DropDownOpening += new System.EventHandler(this.areaGraphMenuItem_DropDownOpening);
            // 
            // areaReplicateComparisonMenuItem
            // 
            this.areaReplicateComparisonMenuItem.Name = "areaReplicateComparisonMenuItem";
            resources.ApplyResources(this.areaReplicateComparisonMenuItem, "areaReplicateComparisonMenuItem");
            this.areaReplicateComparisonMenuItem.Click += new System.EventHandler(this.areaReplicateComparisonMenuItem_Click);
            // 
            // areaPeptideComparisonMenuItem
            // 
            this.areaPeptideComparisonMenuItem.Name = "areaPeptideComparisonMenuItem";
            resources.ApplyResources(this.areaPeptideComparisonMenuItem, "areaPeptideComparisonMenuItem");
            this.areaPeptideComparisonMenuItem.Click += new System.EventHandler(this.areaPeptideComparisonMenuItem_Click);
            // 
            // calibrationCurveMenuItem
            // 
            this.calibrationCurveMenuItem.Name = "calibrationCurveMenuItem";
            resources.ApplyResources(this.calibrationCurveMenuItem, "calibrationCurveMenuItem");
            this.calibrationCurveMenuItem.Click += new System.EventHandler(this.calibrationCurvesMenuItem_Click);
            // 
            // groupComparisonsMenuItem
            // 
            this.groupComparisonsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addGroupComparisonMenuItem,
            this.editGroupComparisonListMenuItem});
            this.groupComparisonsMenuItem.Name = "groupComparisonsMenuItem";
            resources.ApplyResources(this.groupComparisonsMenuItem, "groupComparisonsMenuItem");
            this.groupComparisonsMenuItem.DropDownOpening += new System.EventHandler(this.groupComparisonsMenuItem_DropDownOpening);
            // 
            // addGroupComparisonMenuItem
            // 
            this.addGroupComparisonMenuItem.Name = "addGroupComparisonMenuItem";
            resources.ApplyResources(this.addGroupComparisonMenuItem, "addGroupComparisonMenuItem");
            this.addGroupComparisonMenuItem.Click += new System.EventHandler(this.addFoldChangeMenuItem_Click);
            // 
            // editGroupComparisonListMenuItem
            // 
            this.editGroupComparisonListMenuItem.Name = "editGroupComparisonListMenuItem";
            resources.ApplyResources(this.editGroupComparisonListMenuItem, "editGroupComparisonListMenuItem");
            this.editGroupComparisonListMenuItem.Click += new System.EventHandler(this.editGroupComparisonListMenuItem_Click);
            // 
            // resultsGridMenuItem
            // 
            resources.ApplyResources(this.resultsGridMenuItem, "resultsGridMenuItem");
            this.resultsGridMenuItem.Name = "resultsGridMenuItem";
            this.resultsGridMenuItem.Click += new System.EventHandler(this.resultsGridMenuItem_Click);
            // 
            // documentGridMenuItem
            // 
            this.documentGridMenuItem.Name = "documentGridMenuItem";
            resources.ApplyResources(this.documentGridMenuItem, "documentGridMenuItem");
            this.documentGridMenuItem.Click += new System.EventHandler(this.documentGridMenuItem_Click);
            // 
            // toolStripSeparator36
            // 
            this.toolStripSeparator36.Name = "toolStripSeparator36";
            resources.ApplyResources(this.toolStripSeparator36, "toolStripSeparator36");
            // 
            // toolBarToolStripMenuItem
            // 
            this.toolBarToolStripMenuItem.Checked = true;
            this.toolBarToolStripMenuItem.CheckOnClick = true;
            this.toolBarToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.toolBarToolStripMenuItem.Name = "toolBarToolStripMenuItem";
            resources.ApplyResources(this.toolBarToolStripMenuItem, "toolBarToolStripMenuItem");
            this.toolBarToolStripMenuItem.Click += new System.EventHandler(this.toolBarToolStripMenuItem_Click);
            // 
            // statusToolStripMenuItem
            // 
            this.statusToolStripMenuItem.Checked = true;
            this.statusToolStripMenuItem.CheckOnClick = true;
            this.statusToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.statusToolStripMenuItem.Name = "statusToolStripMenuItem";
            resources.ApplyResources(this.statusToolStripMenuItem, "statusToolStripMenuItem");
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
            this.tutorialsMenuItem,
            this.supportMenuItem,
            this.issuesMenuItem,
            this.toolStripSeparator29,
            this.aboutMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            resources.ApplyResources(this.helpToolStripMenuItem, "helpToolStripMenuItem");
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
            // tutorialsMenuItem
            // 
            this.tutorialsMenuItem.Name = "tutorialsMenuItem";
            resources.ApplyResources(this.tutorialsMenuItem, "tutorialsMenuItem");
            this.tutorialsMenuItem.Click += new System.EventHandler(this.tutorialsMenuItem_Click);
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
        private System.Windows.Forms.ToolStripMenuItem applyPeakAllGraphMenuItem;
        private System.Windows.Forms.ToolStripMenuItem applyPeakSubsequentGraphMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removePeakGraphMenuItem;
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
        private System.Windows.Forms.ToolStripMenuItem documentSettingsMenuItem;
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
        private System.Windows.Forms.ToolStripMenuItem rtValueMenuItem;
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
        private System.Windows.Forms.ToolStripMenuItem ratiosToGlobalStandardsMenuItem;
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
        private System.Windows.Forms.ToolStripMenuItem retentionTimeAlignmentsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportIsolationListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsMenu;
        private System.Windows.Forms.ToolStripMenuItem placeholderToolsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem configureToolsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator47;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator46;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem publishMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renameProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem immediateWindowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importPeptideSearchMenuItem;
        private System.Windows.Forms.ToolStripMenuItem groupReplicatesByContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem groupByReplicateContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mProphetFeaturesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peakBoundariesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorTran;
        private System.Windows.Forms.ToolStripMenuItem basePeakContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ticContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorTranMain;
        private System.Windows.Forms.ToolStripMenuItem basePeakMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ticMenuItem;
        private System.Windows.Forms.ToolStripMenuItem idTimesNoneContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem idTimesMatchingContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem idTimesAlignedContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem idTimesOtherContextMenuItem;
        private System.Windows.Forms.ToolStripSplitButton buttonShowAllChromatograms;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorSplitGraph;
        private System.Windows.Forms.ToolStripMenuItem splitGraphContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator48;
        private System.Windows.Forms.ToolStripMenuItem splitGraphMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLibraryChromatogramsSpectrumContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chromatogramsToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton publishToolbarButton;
        private System.Windows.Forms.ToolStripMenuItem documentGridMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reintegrateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updatesToolsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setStandardTypeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem noStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem qcStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem irtStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem normStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaNormalizeGlobalContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator49;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator50;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator51;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator52;
        private System.Windows.Forms.ToolStripMenuItem setStandardTypeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem noStandardMenuItem;
        private System.Windows.Forms.ToolStripMenuItem normStandardMenuItem;
        private System.Windows.Forms.ToolStripMenuItem qcStandardMenuItem;
        private System.Windows.Forms.ToolStripMenuItem irtStandardMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStoreMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorTools;
        private System.Windows.Forms.ToolStripMenuItem chorusRequestToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showTargetsByNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showTargetsByAccessionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showTargetsByPreferredNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showTargetsByGeneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsByNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsByAccessionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsByPreferredNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsByGeneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem compareModelsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem startPageMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addMoleculeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem arrangeColumnMenuItem;
        private System.Windows.Forms.ToolStripMenuItem arrangeRowMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addTransitionMoleculeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem groupComparisonsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addGroupComparisonMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openContainingFolderMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator53;
        private System.Windows.Forms.ToolStripMenuItem acceptProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editGroupComparisonListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem calibrationCurveMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSmallMoleculePrecursorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePlotContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeCorrelationContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeResidualsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeEmptyPeptidesMenuItem;
    }
}

