
using System;
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
            this.surrogateStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.qcStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.irtStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.modifyPeptideContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editSpectrumFilterContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleQuantitativeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.markTransitionsQuantitativeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.editNoteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorRatios = new System.Windows.Forms.ToolStripSeparator();
            this.ratiosContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ratiosToGlobalStandardsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicatesTreeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleReplicateTreeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bestReplicateTreeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuSpectrum = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.ionTypesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fragmentionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.specialionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorIonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.chargesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            this.ranksContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scoreContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ionMzValuesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.observedMzValuesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicatesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator13 = new System.Windows.Forms.ToolStripSeparator();
            this.lockYaxisContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator14 = new System.Windows.Forms.ToolStripSeparator();
            this.koinaLibMatchItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mirrorMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator61 = new System.Windows.Forms.ToolStripSeparator();
            this.spectrumGraphPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLibSpectrumPropertiesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showFullScanSpectrumPropertiesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator15 = new System.Windows.Forms.ToolStripSeparator();
            this.zoomSpectrumContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator27 = new System.Windows.Forms.ToolStripSeparator();
            this.showLibraryChromatogramsSpectrumContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.synchMzScaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuRetentionTimes = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.timeGraphContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timePeptideComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.regressionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scoreToRunToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runToRunToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.schedulingContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timePlotContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeCorrelationContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeResidualsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timePointsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeTargetsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.targetsAt1FDRToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeStandardsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeDecoysContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rtValueMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fwhmRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fwbRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showRTLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.synchronizeSummaryZoomingContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refineRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.predictionRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicatesRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.averageReplicatesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleReplicateRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bestReplicateRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setRTThresholdContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setRegressionMethodContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.linearRegressionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.kernelDensityEstimationContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logRegressionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loessContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.areaRelativeAbundanceContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVHistogramContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVHistogram2DContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.graphTypeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.barAreaGraphDisplayTypeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lineAreaGraphDisplayTypeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderDocumentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderAreaContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderMassErrorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderDocumentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderAcqTimeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaNormalizeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentScopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.proteinScopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.abundanceTargetsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeTargetsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showPeakAreaLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLibraryPeakAreaContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showDotProductToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideLogScaleContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.relativeAbundanceLogScaleContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideCvsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator28 = new System.Windows.Forms.ToolStripSeparator();
            this.areaPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupReplicatesByContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupByReplicateContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVbinWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCV05binWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCV10binWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCV15binWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCV20binWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pointsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVtargetsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVdecoysToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVTransitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVAllTransitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVCountTransitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVBestTransitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator58 = new System.Windows.Forms.ToolStripSeparator();
            this.areaCVPrecursorsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVProductsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVNormalizedToToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVLogScaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeAboveCVCutoffToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator57 = new System.Windows.Forms.ToolStripSeparator();
            this.abundanceTargetsProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.abundanceTargetsPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeTargetsPeptideListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeTargetsStandardsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.contextMenuMassErrors = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.massErrorGraphContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorReplicateComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorPeptideComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorHistogramContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorHistogram2DContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showMassErrorLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorPointsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorTargetsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorTargets1FDRContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorDecoysContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.binCountContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ppm05ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ppm10ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ppm15ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ppm20ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorTransitionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorAllTransitionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorBestTransitionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator55 = new System.Windows.Forms.ToolStripSeparator();
            this.MassErrorPrecursorsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.MassErrorProductsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorXAxisContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErorrRetentionTimeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorMassToChargContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.massErrorlogScaleContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuDetections = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.detectionsTargetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsTargetPrecursorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsTargetPeptideToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsGraphTypeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsGraphTypeReplicateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsGraphTypeHistogramToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsToolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.detectionsShowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsShowSelectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsShowLegendToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsShowMeanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsShowAtLeastNToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsYScaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsYScaleOneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsYScalePercentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsToolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.detectionsPropertiesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detectionsToolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuTreeNode.SuspendLayout();
            this.contextMenuSpectrum.SuspendLayout();
            this.contextMenuRetentionTimes.SuspendLayout();
            this.contextMenuPeakAreas.SuspendLayout();
            this.panel1.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.mainToolStrip.SuspendLayout();
            this.menuMain.SuspendLayout();
            this.contextMenuMassErrors.SuspendLayout();
            this.contextMenuDetections.SuspendLayout();
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
            this.editSpectrumFilterContextMenuItem,
            this.toggleQuantitativeContextMenuItem,
            this.markTransitionsQuantitativeContextMenuItem,
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
            this.removePeakContextMenuItem.Click += new System.EventHandler(this.removePeakMenuItem_Click);
            // 
            // setStandardTypeContextMenuItem
            // 
            this.setStandardTypeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.noStandardContextMenuItem,
            this.normStandardContextMenuItem,
            this.surrogateStandardContextMenuItem,
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
            // surrogateStandardContextMenuItem
            // 
            this.surrogateStandardContextMenuItem.Name = "surrogateStandardContextMenuItem";
            resources.ApplyResources(this.surrogateStandardContextMenuItem, "surrogateStandardContextMenuItem");
            this.surrogateStandardContextMenuItem.Click += new System.EventHandler(this.surrogateStandardMenuItem_Click);
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
            // editSpectrumFilterContextMenuItem
            // 
            this.editSpectrumFilterContextMenuItem.Name = "editSpectrumFilterContextMenuItem";
            resources.ApplyResources(this.editSpectrumFilterContextMenuItem, "editSpectrumFilterContextMenuItem");
            this.editSpectrumFilterContextMenuItem.Click += new System.EventHandler(this.editSpectrumFilterContextMenuItem_Click);
            // 
            // toggleQuantitativeContextMenuItem
            // 
            this.toggleQuantitativeContextMenuItem.Name = "toggleQuantitativeContextMenuItem";
            resources.ApplyResources(this.toggleQuantitativeContextMenuItem, "toggleQuantitativeContextMenuItem");
            this.toggleQuantitativeContextMenuItem.Click += new System.EventHandler(this.toggleQuantitativeContextMenuItem_Click);
            // 
            // markTransitionsQuantitativeContextMenuItem
            // 
            this.markTransitionsQuantitativeContextMenuItem.Name = "markTransitionsQuantitativeContextMenuItem";
            resources.ApplyResources(this.markTransitionsQuantitativeContextMenuItem, "markTransitionsQuantitativeContextMenuItem");
            this.markTransitionsQuantitativeContextMenuItem.Click += new System.EventHandler(this.markTransitionsQuantitativeContextMenuItem_Click);
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
            this.ionTypesContextMenuItem,
            this.fragmentionsContextMenuItem,
            this.specialionsContextMenuItem,
            this.precursorIonContextMenuItem,
            this.toolStripSeparator11,
            this.chargesContextMenuItem,
            this.toolStripSeparator12,
            this.ranksContextMenuItem,
            this.scoreContextMenuItem,
            this.massErrorToolStripMenuItem,
            this.ionMzValuesContextMenuItem,
            this.observedMzValuesContextMenuItem,
            this.duplicatesContextMenuItem,
            this.toolStripSeparator13,
            this.lockYaxisContextMenuItem,
            this.toolStripSeparator14,
            this.koinaLibMatchItem,
            this.mirrorMenuItem,
            this.toolStripSeparator61,
            this.spectrumGraphPropsContextMenuItem,
            this.showLibSpectrumPropertiesContextMenuItem,
            this.showFullScanSpectrumPropertiesContextMenuItem,
            this.toolStripSeparator15,
            this.zoomSpectrumContextMenuItem,
            this.toolStripSeparator27,
            this.showLibraryChromatogramsSpectrumContextMenuItem,
            this.synchMzScaleToolStripMenuItem});
            this.contextMenuSpectrum.Name = "contextMenuSpectrum";
            resources.ApplyResources(this.contextMenuSpectrum, "contextMenuSpectrum");
            // 
            // ionTypesContextMenuItem
            // 
            this.ionTypesContextMenuItem.Name = "ionTypesContextMenuItem";
            resources.ApplyResources(this.ionTypesContextMenuItem, "ionTypesContextMenuItem");
            this.ionTypesContextMenuItem.DropDownOpening += new System.EventHandler(this.ionTypeMenuItem_DropDownOpening);
            // 
            // fragmentionsContextMenuItem
            // 
            this.fragmentionsContextMenuItem.CheckOnClick = true;
            this.fragmentionsContextMenuItem.Name = "fragmentionsContextMenuItem";
            resources.ApplyResources(this.fragmentionsContextMenuItem, "fragmentionsContextMenuItem");
            this.fragmentionsContextMenuItem.Click += new System.EventHandler(this.fragmentsMenuItem_Click);
            // 
            // specialionsContextMenuItem
            // 
            this.specialionsContextMenuItem.CheckOnClick = true;
            this.specialionsContextMenuItem.Name = "specialionsContextMenuItem";
            resources.ApplyResources(this.specialionsContextMenuItem, "specialionsContextMenuItem");
            this.specialionsContextMenuItem.Click += new System.EventHandler(this.specialionsContextMenuItem_Click);
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
            this.chargesContextMenuItem.Name = "chargesContextMenuItem";
            resources.ApplyResources(this.chargesContextMenuItem, "chargesContextMenuItem");
            this.chargesContextMenuItem.DropDownOpening += new System.EventHandler(this.chargesMenuItem_DropDownOpening);
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
            // scoreContextMenuItem
            // 
            this.scoreContextMenuItem.CheckOnClick = true;
            this.scoreContextMenuItem.Name = "scoreContextMenuItem";
            resources.ApplyResources(this.scoreContextMenuItem, "scoreContextMenuItem");
            this.scoreContextMenuItem.Click += new System.EventHandler(this.scoresContextMenuItem_Click);
            // 
            // massErrorToolStripMenuItem
            // 
            this.massErrorToolStripMenuItem.CheckOnClick = true;
            this.massErrorToolStripMenuItem.Name = "massErrorToolStripMenuItem";
            resources.ApplyResources(this.massErrorToolStripMenuItem, "massErrorToolStripMenuItem");
            this.massErrorToolStripMenuItem.Click += new System.EventHandler(this.massErrorToolStripMenuItem_Click);
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
            // koinaLibMatchItem
            // 
            this.koinaLibMatchItem.Name = "koinaLibMatchItem";
            resources.ApplyResources(this.koinaLibMatchItem, "koinaLibMatchItem");
            this.koinaLibMatchItem.Click += new System.EventHandler(this.koinaLibMatchItem_Click);
            // 
            // mirrorMenuItem
            // 
            this.mirrorMenuItem.Name = "mirrorMenuItem";
            resources.ApplyResources(this.mirrorMenuItem, "mirrorMenuItem");
            this.mirrorMenuItem.Click += new System.EventHandler(this.mirrorMenuItem_Click);
            // 
            // toolStripSeparator61
            // 
            this.toolStripSeparator61.Name = "toolStripSeparator61";
            resources.ApplyResources(this.toolStripSeparator61, "toolStripSeparator61");
            // 
            // spectrumGraphPropsContextMenuItem
            // 
            this.spectrumGraphPropsContextMenuItem.Name = "spectrumGraphPropsContextMenuItem";
            resources.ApplyResources(this.spectrumGraphPropsContextMenuItem, "spectrumGraphPropsContextMenuItem");
            this.spectrumGraphPropsContextMenuItem.Click += new System.EventHandler(this.spectrumGraphPropsContextMenuItem_Click);
            // 
            // showLibSpectrumPropertiesContextMenuItem
            // 
            this.showLibSpectrumPropertiesContextMenuItem.Name = "showLibSpectrumPropertiesContextMenuItem";
            resources.ApplyResources(this.showLibSpectrumPropertiesContextMenuItem, "showLibSpectrumPropertiesContextMenuItem");
            this.showLibSpectrumPropertiesContextMenuItem.Click += new System.EventHandler(this.showLibSpectrumPropertiesContextMenuItem_Click);
            // 
            // showFullScanSpectrumPropertiesContextMenuItem
            // 
            this.showFullScanSpectrumPropertiesContextMenuItem.Name = "showFullScanSpectrumPropertiesContextMenuItem";
            resources.ApplyResources(this.showFullScanSpectrumPropertiesContextMenuItem, "showFullScanSpectrumPropertiesContextMenuItem");
            this.showFullScanSpectrumPropertiesContextMenuItem.Click += new System.EventHandler(this.showFullScanSpectrumPropertiesContextMenuItem_Click);
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
            // synchMzScaleToolStripMenuItem
            // 
            this.synchMzScaleToolStripMenuItem.CheckOnClick = true;
            this.synchMzScaleToolStripMenuItem.Name = "synchMzScaleToolStripMenuItem";
            resources.ApplyResources(this.synchMzScaleToolStripMenuItem, "synchMzScaleToolStripMenuItem");
            this.synchMzScaleToolStripMenuItem.Click += new System.EventHandler(this.synchMzScaleToolStripMenuItem_Click);
            // 
            // contextMenuRetentionTimes
            // 
            this.contextMenuRetentionTimes.AllowMerge = false;
            this.contextMenuRetentionTimes.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.timeGraphContextMenuItem,
            this.timePlotContextMenuItem,
            this.timePointsContextMenuItem,
            this.rtValueMenuItem,
            this.showRTLegendContextMenuItem,
            this.selectionContextMenuItem,
            this.synchronizeSummaryZoomingContextMenuItem,
            this.refineRTContextMenuItem,
            this.predictionRTContextMenuItem,
            this.replicatesRTContextMenuItem,
            this.setRTThresholdContextMenuItem,
            this.setRegressionMethodContextMenuItem,
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
            this.regressionContextMenuItem,
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
            // regressionContextMenuItem
            // 
            this.regressionContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.scoreToRunToolStripMenuItem,
            this.runToRunToolStripMenuItem});
            this.regressionContextMenuItem.Name = "regressionContextMenuItem";
            resources.ApplyResources(this.regressionContextMenuItem, "regressionContextMenuItem");
            // 
            // scoreToRunToolStripMenuItem
            // 
            this.scoreToRunToolStripMenuItem.Name = "scoreToRunToolStripMenuItem";
            resources.ApplyResources(this.scoreToRunToolStripMenuItem, "scoreToRunToolStripMenuItem");
            this.scoreToRunToolStripMenuItem.Click += new System.EventHandler(this.regressionMenuItem_Click);
            // 
            // runToRunToolStripMenuItem
            // 
            this.runToRunToolStripMenuItem.Name = "runToRunToolStripMenuItem";
            resources.ApplyResources(this.runToRunToolStripMenuItem, "runToRunToolStripMenuItem");
            this.runToRunToolStripMenuItem.Click += new System.EventHandler(this.fullReplicateComparisonToolStripMenuItem_Click);
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
            // timePointsContextMenuItem
            // 
            this.timePointsContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.timeTargetsContextMenuItem,
            this.targetsAt1FDRToolStripMenuItem,
            this.timeStandardsContextMenuItem,
            this.timeDecoysContextMenuItem});
            this.timePointsContextMenuItem.Name = "timePointsContextMenuItem";
            resources.ApplyResources(this.timePointsContextMenuItem, "timePointsContextMenuItem");
            // 
            // timeTargetsContextMenuItem
            // 
            this.timeTargetsContextMenuItem.Name = "timeTargetsContextMenuItem";
            resources.ApplyResources(this.timeTargetsContextMenuItem, "timeTargetsContextMenuItem");
            this.timeTargetsContextMenuItem.Click += new System.EventHandler(this.timeTargetsContextMenuItem_Click);
            // 
            // targetsAt1FDRToolStripMenuItem
            // 
            this.targetsAt1FDRToolStripMenuItem.Name = "targetsAt1FDRToolStripMenuItem";
            resources.ApplyResources(this.targetsAt1FDRToolStripMenuItem, "targetsAt1FDRToolStripMenuItem");
            this.targetsAt1FDRToolStripMenuItem.Click += new System.EventHandler(this.targetsAt1FDRToolStripMenuItem_Click);
            // 
            // timeStandardsContextMenuItem
            // 
            this.timeStandardsContextMenuItem.Name = "timeStandardsContextMenuItem";
            resources.ApplyResources(this.timeStandardsContextMenuItem, "timeStandardsContextMenuItem");
            this.timeStandardsContextMenuItem.Click += new System.EventHandler(this.timeStandardsContextMenuItem_Click);
            // 
            // timeDecoysContextMenuItem
            // 
            this.timeDecoysContextMenuItem.Name = "timeDecoysContextMenuItem";
            resources.ApplyResources(this.timeDecoysContextMenuItem, "timeDecoysContextMenuItem");
            this.timeDecoysContextMenuItem.Click += new System.EventHandler(this.timeDecoysContextMenuItem_Click);
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
            // synchronizeSummaryZoomingContextMenuItem
            // 
            this.synchronizeSummaryZoomingContextMenuItem.CheckOnClick = true;
            this.synchronizeSummaryZoomingContextMenuItem.Name = "synchronizeSummaryZoomingContextMenuItem";
            resources.ApplyResources(this.synchronizeSummaryZoomingContextMenuItem, "synchronizeSummaryZoomingContextMenuItem");
            this.synchronizeSummaryZoomingContextMenuItem.Click += new System.EventHandler(this.synchronizeSummaryZoomingContextMenuItem_Click);
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
            // setRegressionMethodContextMenuItem
            // 
            this.setRegressionMethodContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.linearRegressionContextMenuItem,
            this.kernelDensityEstimationContextMenuItem,
            this.logRegressionContextMenuItem,
            this.loessContextMenuItem});
            this.setRegressionMethodContextMenuItem.Name = "setRegressionMethodContextMenuItem";
            resources.ApplyResources(this.setRegressionMethodContextMenuItem, "setRegressionMethodContextMenuItem");
            // 
            // linearRegressionContextMenuItem
            // 
            this.linearRegressionContextMenuItem.Name = "linearRegressionContextMenuItem";
            resources.ApplyResources(this.linearRegressionContextMenuItem, "linearRegressionContextMenuItem");
            this.linearRegressionContextMenuItem.Click += new System.EventHandler(this.linearRegressionContextMenuItem_Click);
            // 
            // kernelDensityEstimationContextMenuItem
            // 
            this.kernelDensityEstimationContextMenuItem.Name = "kernelDensityEstimationContextMenuItem";
            resources.ApplyResources(this.kernelDensityEstimationContextMenuItem, "kernelDensityEstimationContextMenuItem");
            this.kernelDensityEstimationContextMenuItem.Click += new System.EventHandler(this.kernelDensityEstimationContextMenuItem_Click);
            // 
            // logRegressionContextMenuItem
            // 
            this.logRegressionContextMenuItem.Name = "logRegressionContextMenuItem";
            resources.ApplyResources(this.logRegressionContextMenuItem, "logRegressionContextMenuItem");
            this.logRegressionContextMenuItem.Click += new System.EventHandler(this.logRegressionContextMenuItem_Click);
            // 
            // loessContextMenuItem
            // 
            this.loessContextMenuItem.Name = "loessContextMenuItem";
            resources.ApplyResources(this.loessContextMenuItem, "loessContextMenuItem");
            this.loessContextMenuItem.Click += new System.EventHandler(this.loessContextMenuItem_Click);
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
            this.graphTypeToolStripMenuItem,
            this.peptideOrderContextMenuItem,
            this.replicateOrderContextMenuItem,
            this.areaNormalizeContextMenuItem,
            this.scopeContextMenuItem,
            this.abundanceTargetsMenuItem,
            this.excludeTargetsMenuItem,
            this.showPeakAreaLegendContextMenuItem,
            this.showLibraryPeakAreaContextMenuItem,
            this.showDotProductToolStripMenuItem,
            this.peptideLogScaleContextMenuItem,
            this.relativeAbundanceLogScaleContextMenuItem,
            this.peptideCvsContextMenuItem,
            this.toolStripSeparator28,
            this.areaPropsContextMenuItem,
            this.groupReplicatesByContextMenuItem,
            this.areaCVbinWidthToolStripMenuItem,
            this.pointsToolStripMenuItem,
            this.areaCVTransitionsToolStripMenuItem,
            this.areaCVNormalizedToToolStripMenuItem,
            this.areaCVLogScaleToolStripMenuItem,
            this.removeAboveCVCutoffToolStripMenuItem,
            this.toolStripSeparator57});
            this.contextMenuPeakAreas.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuPeakAreas, "contextMenuPeakAreas");
            // 
            // areaGraphContextMenuItem
            // 
            this.areaGraphContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaReplicateComparisonContextMenuItem,
            this.areaPeptideComparisonContextMenuItem,
            this.areaRelativeAbundanceContextMenuItem,
            this.areaCVHistogramContextMenuItem,
            this.areaCVHistogram2DContextMenuItem});
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
            // areaRelativeAbundanceContextMenuItem
            // 
            this.areaRelativeAbundanceContextMenuItem.Name = "areaRelativeAbundanceContextMenuItem";
            resources.ApplyResources(this.areaRelativeAbundanceContextMenuItem, "areaRelativeAbundanceContextMenuItem");
            this.areaRelativeAbundanceContextMenuItem.Click += new System.EventHandler(this.areaRelativeAbundanceMenuItem_Click);
            // 
            // areaCVHistogramContextMenuItem
            // 
            this.areaCVHistogramContextMenuItem.Name = "areaCVHistogramContextMenuItem";
            resources.ApplyResources(this.areaCVHistogramContextMenuItem, "areaCVHistogramContextMenuItem");
            this.areaCVHistogramContextMenuItem.Click += new System.EventHandler(this.areaCVHistogramToolStripMenuItem1_Click);
            // 
            // areaCVHistogram2DContextMenuItem
            // 
            this.areaCVHistogram2DContextMenuItem.Name = "areaCVHistogram2DContextMenuItem";
            resources.ApplyResources(this.areaCVHistogram2DContextMenuItem, "areaCVHistogram2DContextMenuItem");
            this.areaCVHistogram2DContextMenuItem.Click += new System.EventHandler(this.areaCVHistogram2DToolStripMenuItem1_Click);
            // 
            // graphTypeToolStripMenuItem
            // 
            this.graphTypeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.barAreaGraphDisplayTypeMenuItem,
            this.lineAreaGraphDisplayTypeMenuItem});
            this.graphTypeToolStripMenuItem.Name = "graphTypeToolStripMenuItem";
            resources.ApplyResources(this.graphTypeToolStripMenuItem, "graphTypeToolStripMenuItem");
            // 
            // barAreaGraphDisplayTypeMenuItem
            // 
            this.barAreaGraphDisplayTypeMenuItem.Checked = true;
            this.barAreaGraphDisplayTypeMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.barAreaGraphDisplayTypeMenuItem.Name = "barAreaGraphDisplayTypeMenuItem";
            resources.ApplyResources(this.barAreaGraphDisplayTypeMenuItem, "barAreaGraphDisplayTypeMenuItem");
            this.barAreaGraphDisplayTypeMenuItem.Click += new System.EventHandler(this.barAreaGraphTypeMenuItem_Click);
            // 
            // lineAreaGraphDisplayTypeMenuItem
            // 
            this.lineAreaGraphDisplayTypeMenuItem.Name = "lineAreaGraphDisplayTypeMenuItem";
            resources.ApplyResources(this.lineAreaGraphDisplayTypeMenuItem, "lineAreaGraphDisplayTypeMenuItem");
            this.lineAreaGraphDisplayTypeMenuItem.Click += new System.EventHandler(this.lineAreaGraphTypeMenuItem_Click);
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
            // areaNormalizeContextMenuItem
            // 
            this.areaNormalizeContextMenuItem.Name = "areaNormalizeContextMenuItem";
            resources.ApplyResources(this.areaNormalizeContextMenuItem, "areaNormalizeContextMenuItem");
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
            // abundanceTargetsMenuItem
            // 
            this.abundanceTargetsMenuItem.Name = "abundanceTargetsMenuItem";
            resources.ApplyResources(this.abundanceTargetsMenuItem, "abundanceTargetsMenuItem");
            // 
            // excludeTargetsMenuItem
            // 
            this.excludeTargetsMenuItem.Name = "excludeTargetsMenuItem";
            resources.ApplyResources(this.excludeTargetsMenuItem, "excludeTargetsMenuItem");
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
            // 
            // peptideLogScaleContextMenuItem
            // 
            this.peptideLogScaleContextMenuItem.CheckOnClick = true;
            this.peptideLogScaleContextMenuItem.Name = "peptideLogScaleContextMenuItem";
            resources.ApplyResources(this.peptideLogScaleContextMenuItem, "peptideLogScaleContextMenuItem");
            this.peptideLogScaleContextMenuItem.Click += new System.EventHandler(this.peptideLogScaleContextMenuItem_Click);
            // 
            // relativeAbundanceLogScaleContextMenuItem
            // 
            this.relativeAbundanceLogScaleContextMenuItem.CheckOnClick = true;
            this.relativeAbundanceLogScaleContextMenuItem.Name = "relativeAbundanceLogScaleContextMenuItem";
            resources.ApplyResources(this.relativeAbundanceLogScaleContextMenuItem, "relativeAbundanceLogScaleContextMenuItem");
            this.relativeAbundanceLogScaleContextMenuItem.Click += new System.EventHandler(this.relativeAbundanceLogScaleContextMenuItem_Click);
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
            // areaCVbinWidthToolStripMenuItem
            // 
            this.areaCVbinWidthToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaCV05binWidthToolStripMenuItem,
            this.areaCV10binWidthToolStripMenuItem,
            this.areaCV15binWidthToolStripMenuItem,
            this.areaCV20binWidthToolStripMenuItem});
            this.areaCVbinWidthToolStripMenuItem.Name = "areaCVbinWidthToolStripMenuItem";
            resources.ApplyResources(this.areaCVbinWidthToolStripMenuItem, "areaCVbinWidthToolStripMenuItem");
            // 
            // areaCV05binWidthToolStripMenuItem
            // 
            this.areaCV05binWidthToolStripMenuItem.Name = "areaCV05binWidthToolStripMenuItem";
            resources.ApplyResources(this.areaCV05binWidthToolStripMenuItem, "areaCV05binWidthToolStripMenuItem");
            this.areaCV05binWidthToolStripMenuItem.Click += new System.EventHandler(this.areaCV05binWidthToolStripMenuItem_Click);
            // 
            // areaCV10binWidthToolStripMenuItem
            // 
            this.areaCV10binWidthToolStripMenuItem.Name = "areaCV10binWidthToolStripMenuItem";
            resources.ApplyResources(this.areaCV10binWidthToolStripMenuItem, "areaCV10binWidthToolStripMenuItem");
            this.areaCV10binWidthToolStripMenuItem.Click += new System.EventHandler(this.areaCV10binWidthToolStripMenuItem_Click);
            // 
            // areaCV15binWidthToolStripMenuItem
            // 
            this.areaCV15binWidthToolStripMenuItem.Name = "areaCV15binWidthToolStripMenuItem";
            resources.ApplyResources(this.areaCV15binWidthToolStripMenuItem, "areaCV15binWidthToolStripMenuItem");
            this.areaCV15binWidthToolStripMenuItem.Click += new System.EventHandler(this.areaCV15binWidthToolStripMenuItem_Click);
            // 
            // areaCV20binWidthToolStripMenuItem
            // 
            this.areaCV20binWidthToolStripMenuItem.Name = "areaCV20binWidthToolStripMenuItem";
            resources.ApplyResources(this.areaCV20binWidthToolStripMenuItem, "areaCV20binWidthToolStripMenuItem");
            this.areaCV20binWidthToolStripMenuItem.Click += new System.EventHandler(this.areaCV20binWidthToolStripMenuItem_Click);
            // 
            // pointsToolStripMenuItem
            // 
            this.pointsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaCVtargetsToolStripMenuItem,
            this.areaCVdecoysToolStripMenuItem});
            this.pointsToolStripMenuItem.Name = "pointsToolStripMenuItem";
            resources.ApplyResources(this.pointsToolStripMenuItem, "pointsToolStripMenuItem");
            // 
            // areaCVtargetsToolStripMenuItem
            // 
            this.areaCVtargetsToolStripMenuItem.Name = "areaCVtargetsToolStripMenuItem";
            resources.ApplyResources(this.areaCVtargetsToolStripMenuItem, "areaCVtargetsToolStripMenuItem");
            this.areaCVtargetsToolStripMenuItem.Click += new System.EventHandler(this.areaCVtargetsToolStripMenuItem_Click);
            // 
            // areaCVdecoysToolStripMenuItem
            // 
            this.areaCVdecoysToolStripMenuItem.Name = "areaCVdecoysToolStripMenuItem";
            resources.ApplyResources(this.areaCVdecoysToolStripMenuItem, "areaCVdecoysToolStripMenuItem");
            this.areaCVdecoysToolStripMenuItem.Click += new System.EventHandler(this.areaCVdecoysToolStripMenuItem_Click);
            // 
            // areaCVTransitionsToolStripMenuItem
            // 
            this.areaCVTransitionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaCVAllTransitionsToolStripMenuItem,
            this.areaCVCountTransitionsToolStripMenuItem,
            this.areaCVBestTransitionsToolStripMenuItem,
            this.toolStripSeparator58,
            this.areaCVPrecursorsToolStripMenuItem,
            this.areaCVProductsToolStripMenuItem});
            this.areaCVTransitionsToolStripMenuItem.Name = "areaCVTransitionsToolStripMenuItem";
            resources.ApplyResources(this.areaCVTransitionsToolStripMenuItem, "areaCVTransitionsToolStripMenuItem");
            // 
            // areaCVAllTransitionsToolStripMenuItem
            // 
            this.areaCVAllTransitionsToolStripMenuItem.Name = "areaCVAllTransitionsToolStripMenuItem";
            resources.ApplyResources(this.areaCVAllTransitionsToolStripMenuItem, "areaCVAllTransitionsToolStripMenuItem");
            this.areaCVAllTransitionsToolStripMenuItem.Click += new System.EventHandler(this.areaCVAllTransitionsToolStripMenuItem_Click);
            // 
            // areaCVCountTransitionsToolStripMenuItem
            // 
            this.areaCVCountTransitionsToolStripMenuItem.Name = "areaCVCountTransitionsToolStripMenuItem";
            resources.ApplyResources(this.areaCVCountTransitionsToolStripMenuItem, "areaCVCountTransitionsToolStripMenuItem");
            // 
            // areaCVBestTransitionsToolStripMenuItem
            // 
            this.areaCVBestTransitionsToolStripMenuItem.Name = "areaCVBestTransitionsToolStripMenuItem";
            resources.ApplyResources(this.areaCVBestTransitionsToolStripMenuItem, "areaCVBestTransitionsToolStripMenuItem");
            this.areaCVBestTransitionsToolStripMenuItem.Click += new System.EventHandler(this.areaCVBestTransitionsToolStripMenuItem_Click);
            // 
            // toolStripSeparator58
            // 
            this.toolStripSeparator58.Name = "toolStripSeparator58";
            resources.ApplyResources(this.toolStripSeparator58, "toolStripSeparator58");
            // 
            // areaCVPrecursorsToolStripMenuItem
            // 
            this.areaCVPrecursorsToolStripMenuItem.Name = "areaCVPrecursorsToolStripMenuItem";
            resources.ApplyResources(this.areaCVPrecursorsToolStripMenuItem, "areaCVPrecursorsToolStripMenuItem");
            this.areaCVPrecursorsToolStripMenuItem.Click += new System.EventHandler(this.areaCVPrecursorsToolStripMenuItem_Click);
            // 
            // areaCVProductsToolStripMenuItem
            // 
            this.areaCVProductsToolStripMenuItem.Name = "areaCVProductsToolStripMenuItem";
            resources.ApplyResources(this.areaCVProductsToolStripMenuItem, "areaCVProductsToolStripMenuItem");
            this.areaCVProductsToolStripMenuItem.Click += new System.EventHandler(this.areaCVProductsToolStripMenuItem_Click);
            // 
            // areaCVNormalizedToToolStripMenuItem
            // 
            this.areaCVNormalizedToToolStripMenuItem.Name = "areaCVNormalizedToToolStripMenuItem";
            resources.ApplyResources(this.areaCVNormalizedToToolStripMenuItem, "areaCVNormalizedToToolStripMenuItem");
            // 
            // areaCVLogScaleToolStripMenuItem
            // 
            this.areaCVLogScaleToolStripMenuItem.Name = "areaCVLogScaleToolStripMenuItem";
            resources.ApplyResources(this.areaCVLogScaleToolStripMenuItem, "areaCVLogScaleToolStripMenuItem");
            this.areaCVLogScaleToolStripMenuItem.Click += new System.EventHandler(this.areaCVLogScaleToolStripMenuItem_Click);
            // 
            // removeAboveCVCutoffToolStripMenuItem
            // 
            this.removeAboveCVCutoffToolStripMenuItem.Name = "removeAboveCVCutoffToolStripMenuItem";
            resources.ApplyResources(this.removeAboveCVCutoffToolStripMenuItem, "removeAboveCVCutoffToolStripMenuItem");
            this.removeAboveCVCutoffToolStripMenuItem.Click += new System.EventHandler(this.removeAboveCVCutoffToolStripMenuItem_Click);
            // 
            // toolStripSeparator57
            // 
            this.toolStripSeparator57.Name = "toolStripSeparator57";
            resources.ApplyResources(this.toolStripSeparator57, "toolStripSeparator57");
            // 
            // abundanceTargetsProteinsMenuItem
            // 
            this.abundanceTargetsProteinsMenuItem.Name = "abundanceTargetsProteinsMenuItem";
            resources.ApplyResources(this.abundanceTargetsProteinsMenuItem, "abundanceTargetsProteinsMenuItem");
            this.abundanceTargetsProteinsMenuItem.Click += new System.EventHandler(this.abundanceTargetsProteinsMenuItem_Click);
            // 
            // abundanceTargetsPeptidesMenuItem
            // 
            this.abundanceTargetsPeptidesMenuItem.Name = "abundanceTargetsPeptidesMenuItem";
            resources.ApplyResources(this.abundanceTargetsPeptidesMenuItem, "abundanceTargetsPeptidesMenuItem");
            this.abundanceTargetsPeptidesMenuItem.Click += new System.EventHandler(this.abundanceTargetsPeptidesMenuItem_Click);
            // 
            // excludeTargetsPeptideListMenuItem
            // 
            this.excludeTargetsPeptideListMenuItem.Checked = global::pwiz.Skyline.Properties.Settings.Default.ExcludePeptideListsFromAbundanceGraph;
            this.excludeTargetsPeptideListMenuItem.Name = "excludeTargetsPeptideListMenuItem";
            resources.ApplyResources(this.excludeTargetsPeptideListMenuItem, "excludeTargetsPeptideListMenuItem");
            this.excludeTargetsPeptideListMenuItem.Click += new System.EventHandler(this.excludeTargetsPeptideListMenuItem_Click);
            // 
            // excludeTargetsStandardsMenuItem
            // 
            this.excludeTargetsStandardsMenuItem.Checked = global::pwiz.Skyline.Properties.Settings.Default.ExcludeStandardsFromAbundanceGraph;
            this.excludeTargetsStandardsMenuItem.Name = "excludeTargetsStandardsMenuItem";
            resources.ApplyResources(this.excludeTargetsStandardsMenuItem, "excludeTargetsStandardsMenuItem");
            this.excludeTargetsStandardsMenuItem.Click += new System.EventHandler(this.excludeTargetsStandardsMenuItem_Click);
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
            // contextMenuMassErrors
            // 
            this.contextMenuMassErrors.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.massErrorGraphContextMenuItem,
            this.massErrorPropsContextMenuItem,
            this.showMassErrorLegendContextMenuItem,
            this.massErrorPointsContextMenuItem,
            this.binCountContextMenuItem,
            this.massErrorTransitionsContextMenuItem,
            this.massErrorXAxisContextMenuItem,
            this.massErrorlogScaleContextMenuItem});
            this.contextMenuMassErrors.Name = "contextMenuMassErrors";
            resources.ApplyResources(this.contextMenuMassErrors, "contextMenuMassErrors");
            // 
            // massErrorGraphContextMenuItem
            // 
            this.massErrorGraphContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.massErrorReplicateComparisonContextMenuItem,
            this.massErrorPeptideComparisonContextMenuItem,
            this.massErrorHistogramContextMenuItem,
            this.massErrorHistogram2DContextMenuItem});
            this.massErrorGraphContextMenuItem.Name = "massErrorGraphContextMenuItem";
            resources.ApplyResources(this.massErrorGraphContextMenuItem, "massErrorGraphContextMenuItem");
            this.massErrorGraphContextMenuItem.DropDownOpening += new System.EventHandler(this.massErrorMenuItem_DropDownOpening);
            // 
            // massErrorReplicateComparisonContextMenuItem
            // 
            this.massErrorReplicateComparisonContextMenuItem.CheckOnClick = true;
            this.massErrorReplicateComparisonContextMenuItem.Name = "massErrorReplicateComparisonContextMenuItem";
            resources.ApplyResources(this.massErrorReplicateComparisonContextMenuItem, "massErrorReplicateComparisonContextMenuItem");
            this.massErrorReplicateComparisonContextMenuItem.Click += new System.EventHandler(this.massErrorReplicateComparisonMenuItem_Click);
            // 
            // massErrorPeptideComparisonContextMenuItem
            // 
            this.massErrorPeptideComparisonContextMenuItem.Name = "massErrorPeptideComparisonContextMenuItem";
            resources.ApplyResources(this.massErrorPeptideComparisonContextMenuItem, "massErrorPeptideComparisonContextMenuItem");
            this.massErrorPeptideComparisonContextMenuItem.Click += new System.EventHandler(this.massErrorPeptideComparisonMenuItem_Click);
            // 
            // massErrorHistogramContextMenuItem
            // 
            this.massErrorHistogramContextMenuItem.Name = "massErrorHistogramContextMenuItem";
            resources.ApplyResources(this.massErrorHistogramContextMenuItem, "massErrorHistogramContextMenuItem");
            this.massErrorHistogramContextMenuItem.Click += new System.EventHandler(this.massErrorHistogramMenuItem_Click);
            // 
            // massErrorHistogram2DContextMenuItem
            // 
            this.massErrorHistogram2DContextMenuItem.Name = "massErrorHistogram2DContextMenuItem";
            resources.ApplyResources(this.massErrorHistogram2DContextMenuItem, "massErrorHistogram2DContextMenuItem");
            this.massErrorHistogram2DContextMenuItem.Click += new System.EventHandler(this.massErrorHistogram2DMenuItem_Click);
            // 
            // massErrorPropsContextMenuItem
            // 
            this.massErrorPropsContextMenuItem.Name = "massErrorPropsContextMenuItem";
            resources.ApplyResources(this.massErrorPropsContextMenuItem, "massErrorPropsContextMenuItem");
            this.massErrorPropsContextMenuItem.Click += new System.EventHandler(this.massErrorPropsContextMenuItem_Click);
            // 
            // showMassErrorLegendContextMenuItem
            // 
            this.showMassErrorLegendContextMenuItem.Name = "showMassErrorLegendContextMenuItem";
            resources.ApplyResources(this.showMassErrorLegendContextMenuItem, "showMassErrorLegendContextMenuItem");
            this.showMassErrorLegendContextMenuItem.Click += new System.EventHandler(this.showMassErrorLegendContextMenuItem_Click);
            // 
            // massErrorPointsContextMenuItem
            // 
            this.massErrorPointsContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.massErrorTargetsContextMenuItem,
            this.massErrorTargets1FDRContextMenuItem,
            this.massErrorDecoysContextMenuItem});
            this.massErrorPointsContextMenuItem.Name = "massErrorPointsContextMenuItem";
            resources.ApplyResources(this.massErrorPointsContextMenuItem, "massErrorPointsContextMenuItem");
            // 
            // massErrorTargetsContextMenuItem
            // 
            this.massErrorTargetsContextMenuItem.Name = "massErrorTargetsContextMenuItem";
            resources.ApplyResources(this.massErrorTargetsContextMenuItem, "massErrorTargetsContextMenuItem");
            this.massErrorTargetsContextMenuItem.Click += new System.EventHandler(this.massErrorTargetsContextMenuItem_Click);
            // 
            // massErrorTargets1FDRContextMenuItem
            // 
            this.massErrorTargets1FDRContextMenuItem.Name = "massErrorTargets1FDRContextMenuItem";
            resources.ApplyResources(this.massErrorTargets1FDRContextMenuItem, "massErrorTargets1FDRContextMenuItem");
            this.massErrorTargets1FDRContextMenuItem.Click += new System.EventHandler(this.massErrorTargets1FDRContextMenuItem_Click);
            // 
            // massErrorDecoysContextMenuItem
            // 
            this.massErrorDecoysContextMenuItem.Name = "massErrorDecoysContextMenuItem";
            resources.ApplyResources(this.massErrorDecoysContextMenuItem, "massErrorDecoysContextMenuItem");
            this.massErrorDecoysContextMenuItem.Click += new System.EventHandler(this.massErrorDecoysContextMenuItem_Click);
            // 
            // binCountContextMenuItem
            // 
            this.binCountContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ppm05ContextMenuItem,
            this.ppm10ContextMenuItem,
            this.ppm15ContextMenuItem,
            this.ppm20ContextMenuItem});
            this.binCountContextMenuItem.Name = "binCountContextMenuItem";
            resources.ApplyResources(this.binCountContextMenuItem, "binCountContextMenuItem");
            this.binCountContextMenuItem.DropDownOpening += new System.EventHandler(this.binCountContextMenuItem_DropDownOpening);
            // 
            // ppm05ContextMenuItem
            // 
            this.ppm05ContextMenuItem.Name = "ppm05ContextMenuItem";
            resources.ApplyResources(this.ppm05ContextMenuItem, "ppm05ContextMenuItem");
            this.ppm05ContextMenuItem.Click += new System.EventHandler(this.ppm05ContextMenuItem_Click);
            // 
            // ppm10ContextMenuItem
            // 
            this.ppm10ContextMenuItem.Name = "ppm10ContextMenuItem";
            resources.ApplyResources(this.ppm10ContextMenuItem, "ppm10ContextMenuItem");
            this.ppm10ContextMenuItem.Click += new System.EventHandler(this.ppm10ContextMenuItem_Click);
            // 
            // ppm15ContextMenuItem
            // 
            this.ppm15ContextMenuItem.Name = "ppm15ContextMenuItem";
            resources.ApplyResources(this.ppm15ContextMenuItem, "ppm15ContextMenuItem");
            this.ppm15ContextMenuItem.Click += new System.EventHandler(this.ppm15ContextMenuItem_Click);
            // 
            // ppm20ContextMenuItem
            // 
            this.ppm20ContextMenuItem.Name = "ppm20ContextMenuItem";
            resources.ApplyResources(this.ppm20ContextMenuItem, "ppm20ContextMenuItem");
            this.ppm20ContextMenuItem.Click += new System.EventHandler(this.ppm20ContextMenuItem_Click);
            // 
            // massErrorTransitionsContextMenuItem
            // 
            this.massErrorTransitionsContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.massErrorAllTransitionsContextMenuItem,
            this.massErrorBestTransitionsContextMenuItem,
            this.toolStripSeparator55,
            this.MassErrorPrecursorsContextMenuItem,
            this.MassErrorProductsContextMenuItem});
            this.massErrorTransitionsContextMenuItem.Name = "massErrorTransitionsContextMenuItem";
            resources.ApplyResources(this.massErrorTransitionsContextMenuItem, "massErrorTransitionsContextMenuItem");
            this.massErrorTransitionsContextMenuItem.DropDownOpening += new System.EventHandler(this.massErrorTransitionsContextMenuItem_DropDownOpening);
            // 
            // massErrorAllTransitionsContextMenuItem
            // 
            this.massErrorAllTransitionsContextMenuItem.Name = "massErrorAllTransitionsContextMenuItem";
            resources.ApplyResources(this.massErrorAllTransitionsContextMenuItem, "massErrorAllTransitionsContextMenuItem");
            this.massErrorAllTransitionsContextMenuItem.Click += new System.EventHandler(this.massErrorAllTransitionsContextMenuItem_Click);
            // 
            // massErrorBestTransitionsContextMenuItem
            // 
            this.massErrorBestTransitionsContextMenuItem.Name = "massErrorBestTransitionsContextMenuItem";
            resources.ApplyResources(this.massErrorBestTransitionsContextMenuItem, "massErrorBestTransitionsContextMenuItem");
            this.massErrorBestTransitionsContextMenuItem.Click += new System.EventHandler(this.massErrorBestTransitionsContextMenuItem_Click);
            // 
            // toolStripSeparator55
            // 
            this.toolStripSeparator55.Name = "toolStripSeparator55";
            resources.ApplyResources(this.toolStripSeparator55, "toolStripSeparator55");
            // 
            // MassErrorPrecursorsContextMenuItem
            // 
            this.MassErrorPrecursorsContextMenuItem.Name = "MassErrorPrecursorsContextMenuItem";
            resources.ApplyResources(this.MassErrorPrecursorsContextMenuItem, "MassErrorPrecursorsContextMenuItem");
            this.MassErrorPrecursorsContextMenuItem.Click += new System.EventHandler(this.MassErrorPrecursorsContextMenuItem_Click);
            // 
            // MassErrorProductsContextMenuItem
            // 
            this.MassErrorProductsContextMenuItem.Name = "MassErrorProductsContextMenuItem";
            resources.ApplyResources(this.MassErrorProductsContextMenuItem, "MassErrorProductsContextMenuItem");
            this.MassErrorProductsContextMenuItem.Click += new System.EventHandler(this.MassErrorProductsContextMenuItem_Click);
            // 
            // massErrorXAxisContextMenuItem
            // 
            this.massErrorXAxisContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.massErorrRetentionTimeContextMenuItem,
            this.massErrorMassToChargContextMenuItem});
            this.massErrorXAxisContextMenuItem.Name = "massErrorXAxisContextMenuItem";
            resources.ApplyResources(this.massErrorXAxisContextMenuItem, "massErrorXAxisContextMenuItem");
            this.massErrorXAxisContextMenuItem.DropDownOpening += new System.EventHandler(this.massErrorXAxisContextMenuItem_DropDownOpening);
            // 
            // massErorrRetentionTimeContextMenuItem
            // 
            this.massErorrRetentionTimeContextMenuItem.Name = "massErorrRetentionTimeContextMenuItem";
            resources.ApplyResources(this.massErorrRetentionTimeContextMenuItem, "massErorrRetentionTimeContextMenuItem");
            this.massErorrRetentionTimeContextMenuItem.Click += new System.EventHandler(this.massErorrRetentionTimeContextMenuItem_Click);
            // 
            // massErrorMassToChargContextMenuItem
            // 
            this.massErrorMassToChargContextMenuItem.Name = "massErrorMassToChargContextMenuItem";
            resources.ApplyResources(this.massErrorMassToChargContextMenuItem, "massErrorMassToChargContextMenuItem");
            this.massErrorMassToChargContextMenuItem.Click += new System.EventHandler(this.massErrorMassToChargContextMenuItem_Click);
            // 
            // massErrorlogScaleContextMenuItem
            // 
            this.massErrorlogScaleContextMenuItem.Name = "massErrorlogScaleContextMenuItem";
            resources.ApplyResources(this.massErrorlogScaleContextMenuItem, "massErrorlogScaleContextMenuItem");
            this.massErrorlogScaleContextMenuItem.Click += new System.EventHandler(this.massErrorlogScaleContextMenuItem_Click);
            // 
            // contextMenuDetections
            // 
            this.contextMenuDetections.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsTargetToolStripMenuItem,
            this.detectionsGraphTypeToolStripMenuItem,
            this.detectionsToolStripSeparator1,
            this.detectionsShowToolStripMenuItem,
            this.detectionsYScaleToolStripMenuItem,
            this.detectionsToolStripSeparator2,
            this.detectionsPropertiesToolStripMenuItem,
            this.detectionsToolStripSeparator3});
            this.contextMenuDetections.Name = "contextMenuDetections";
            resources.ApplyResources(this.contextMenuDetections, "contextMenuDetections");
            // 
            // detectionsTargetToolStripMenuItem
            // 
            this.detectionsTargetToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsTargetPrecursorToolStripMenuItem,
            this.detectionsTargetPeptideToolStripMenuItem});
            this.detectionsTargetToolStripMenuItem.Name = "detectionsTargetToolStripMenuItem";
            resources.ApplyResources(this.detectionsTargetToolStripMenuItem, "detectionsTargetToolStripMenuItem");
            // 
            // detectionsTargetPrecursorToolStripMenuItem
            // 
            this.detectionsTargetPrecursorToolStripMenuItem.Name = "detectionsTargetPrecursorToolStripMenuItem";
            resources.ApplyResources(this.detectionsTargetPrecursorToolStripMenuItem, "detectionsTargetPrecursorToolStripMenuItem");
            this.detectionsTargetPrecursorToolStripMenuItem.Tag = 0;
            this.detectionsTargetPrecursorToolStripMenuItem.Click += new System.EventHandler(this.detectionsTargetPrecursorToolStripMenuItem_Click);
            // 
            // detectionsTargetPeptideToolStripMenuItem
            // 
            this.detectionsTargetPeptideToolStripMenuItem.Name = "detectionsTargetPeptideToolStripMenuItem";
            resources.ApplyResources(this.detectionsTargetPeptideToolStripMenuItem, "detectionsTargetPeptideToolStripMenuItem");
            this.detectionsTargetPeptideToolStripMenuItem.Tag = 1;
            this.detectionsTargetPeptideToolStripMenuItem.Click += new System.EventHandler(this.detectionsTargetPeptideToolStripMenuItem_Click);
            // 
            // detectionsGraphTypeToolStripMenuItem
            // 
            this.detectionsGraphTypeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsGraphTypeReplicateToolStripMenuItem,
            this.detectionsGraphTypeHistogramToolStripMenuItem});
            this.detectionsGraphTypeToolStripMenuItem.Name = "detectionsGraphTypeToolStripMenuItem";
            resources.ApplyResources(this.detectionsGraphTypeToolStripMenuItem, "detectionsGraphTypeToolStripMenuItem");
            // 
            // detectionsGraphTypeReplicateToolStripMenuItem
            // 
            this.detectionsGraphTypeReplicateToolStripMenuItem.Name = "detectionsGraphTypeReplicateToolStripMenuItem";
            resources.ApplyResources(this.detectionsGraphTypeReplicateToolStripMenuItem, "detectionsGraphTypeReplicateToolStripMenuItem");
            this.detectionsGraphTypeReplicateToolStripMenuItem.Click += new System.EventHandler(this.detectionsGraphTypeReplicateToolStripMenuItem_Click);
            // 
            // detectionsGraphTypeHistogramToolStripMenuItem
            // 
            this.detectionsGraphTypeHistogramToolStripMenuItem.Name = "detectionsGraphTypeHistogramToolStripMenuItem";
            resources.ApplyResources(this.detectionsGraphTypeHistogramToolStripMenuItem, "detectionsGraphTypeHistogramToolStripMenuItem");
            this.detectionsGraphTypeHistogramToolStripMenuItem.Click += new System.EventHandler(this.detectionsGraphTypeHistogramToolStripMenuItem_Click);
            // 
            // detectionsToolStripSeparator1
            // 
            this.detectionsToolStripSeparator1.Name = "detectionsToolStripSeparator1";
            resources.ApplyResources(this.detectionsToolStripSeparator1, "detectionsToolStripSeparator1");
            // 
            // detectionsShowToolStripMenuItem
            // 
            this.detectionsShowToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsShowSelectionToolStripMenuItem,
            this.detectionsShowLegendToolStripMenuItem,
            this.detectionsShowMeanToolStripMenuItem,
            this.detectionsShowAtLeastNToolStripMenuItem});
            this.detectionsShowToolStripMenuItem.Name = "detectionsShowToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowToolStripMenuItem, "detectionsShowToolStripMenuItem");
            // 
            // detectionsShowSelectionToolStripMenuItem
            // 
            this.detectionsShowSelectionToolStripMenuItem.Name = "detectionsShowSelectionToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowSelectionToolStripMenuItem, "detectionsShowSelectionToolStripMenuItem");
            this.detectionsShowSelectionToolStripMenuItem.Click += new System.EventHandler(this.detectionsShowSelectionToolStripMenuItem_Click);
            // 
            // detectionsShowLegendToolStripMenuItem
            // 
            this.detectionsShowLegendToolStripMenuItem.Name = "detectionsShowLegendToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowLegendToolStripMenuItem, "detectionsShowLegendToolStripMenuItem");
            this.detectionsShowLegendToolStripMenuItem.Click += new System.EventHandler(this.detectionsShowLegendToolStripMenuItem_Click);
            // 
            // detectionsShowMeanToolStripMenuItem
            // 
            this.detectionsShowMeanToolStripMenuItem.Name = "detectionsShowMeanToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowMeanToolStripMenuItem, "detectionsShowMeanToolStripMenuItem");
            this.detectionsShowMeanToolStripMenuItem.Click += new System.EventHandler(this.detectionsShowMeanToolStripMenuItem_Click);
            // 
            // detectionsShowAtLeastNToolStripMenuItem
            // 
            this.detectionsShowAtLeastNToolStripMenuItem.Name = "detectionsShowAtLeastNToolStripMenuItem";
            resources.ApplyResources(this.detectionsShowAtLeastNToolStripMenuItem, "detectionsShowAtLeastNToolStripMenuItem");
            this.detectionsShowAtLeastNToolStripMenuItem.Click += new System.EventHandler(this.detectionsShowAtLeastNToolStripMenuItem_Click);
            // 
            // detectionsYScaleToolStripMenuItem
            // 
            this.detectionsYScaleToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detectionsYScaleOneToolStripMenuItem,
            this.detectionsYScalePercentToolStripMenuItem});
            this.detectionsYScaleToolStripMenuItem.Name = "detectionsYScaleToolStripMenuItem";
            resources.ApplyResources(this.detectionsYScaleToolStripMenuItem, "detectionsYScaleToolStripMenuItem");
            // 
            // detectionsYScaleOneToolStripMenuItem
            // 
            this.detectionsYScaleOneToolStripMenuItem.Name = "detectionsYScaleOneToolStripMenuItem";
            resources.ApplyResources(this.detectionsYScaleOneToolStripMenuItem, "detectionsYScaleOneToolStripMenuItem");
            this.detectionsYScaleOneToolStripMenuItem.Tag = 1;
            this.detectionsYScaleOneToolStripMenuItem.Click += new System.EventHandler(this.detectionsYScaleOneToolStripMenuItem_Click);
            // 
            // detectionsYScalePercentToolStripMenuItem
            // 
            this.detectionsYScalePercentToolStripMenuItem.Name = "detectionsYScalePercentToolStripMenuItem";
            resources.ApplyResources(this.detectionsYScalePercentToolStripMenuItem, "detectionsYScalePercentToolStripMenuItem");
            this.detectionsYScalePercentToolStripMenuItem.Tag = 0;
            this.detectionsYScalePercentToolStripMenuItem.Click += new System.EventHandler(this.detectionsYScalePercentToolStripMenuItem_Click);
            // 
            // detectionsToolStripSeparator2
            // 
            this.detectionsToolStripSeparator2.Name = "detectionsToolStripSeparator2";
            resources.ApplyResources(this.detectionsToolStripSeparator2, "detectionsToolStripSeparator2");
            // 
            // detectionsPropertiesToolStripMenuItem
            // 
            this.detectionsPropertiesToolStripMenuItem.Name = "detectionsPropertiesToolStripMenuItem";
            resources.ApplyResources(this.detectionsPropertiesToolStripMenuItem, "detectionsPropertiesToolStripMenuItem");
            this.detectionsPropertiesToolStripMenuItem.Click += new System.EventHandler(this.detectionsPropertiesToolStripMenuItem_Click);
            // 
            // detectionsToolStripSeparator3
            // 
            this.detectionsToolStripSeparator3.Name = "detectionsToolStripSeparator3";
            resources.ApplyResources(this.detectionsToolStripSeparator3, "detectionsToolStripSeparator3");
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
            this.contextMenuRetentionTimes.ResumeLayout(false);
            this.contextMenuPeakAreas.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.mainToolStrip.ResumeLayout(false);
            this.mainToolStrip.PerformLayout();
            this.menuMain.ResumeLayout(false);
            this.menuMain.PerformLayout();
            this.contextMenuMassErrors.ResumeLayout(false);
            this.contextMenuDetections.ResumeLayout(false);
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
        private System.Windows.Forms.ContextMenuStrip contextMenuTreeNode;
        private System.Windows.Forms.ToolStripMenuItem cutContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem pickChildrenContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem editNoteContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuSpectrum;
        private System.Windows.Forms.ToolStripMenuItem ionTypesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fragmentionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem specialionsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
        private System.Windows.Forms.ToolStripMenuItem ranksContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator13;
        private System.Windows.Forms.ToolStripMenuItem zoomSpectrumContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator14;
        private System.Windows.Forms.ToolStripMenuItem duplicatesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem lockYaxisContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLibSpectrumPropertiesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showFullScanSpectrumPropertiesContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator15;
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
        private System.Windows.Forms.ToolStripMenuItem exportTransitionListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportReportMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeGraphContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem regressionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem spectrumGraphPropsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator27;
        private System.Windows.Forms.ToolStripMenuItem schedulingContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem supportMenuItem;
        private System.Windows.Forms.ToolStripMenuItem issuesMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator29;
        private System.Windows.Forms.ToolStripMenuItem homeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem modifyPeptideContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator31;
        private System.Windows.Forms.ToolStripMenuItem shareSettingsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importSettingsMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem deleteContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removePeakContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportMethodMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuPeakAreas;
        private System.Windows.Forms.ToolStripMenuItem areaGraphContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaReplicateComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaPeptideComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaRelativeAbundanceContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaNormalizeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideLogScaleContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem relativeAbundanceLogScaleContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderDocumentContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderAreaContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideCvsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem abundanceTargetsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem abundanceTargetsProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem abundanceTargetsPeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeTargetsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeTargetsPeptideListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeTargetsStandardsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentSettingsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refineToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem synchronizeSummaryZoomingContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem shareDocumentMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator37;
        private System.Windows.Forms.ToolStripMenuItem precursorIonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem integrateAllMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator28;
        private System.Windows.Forms.ToolStripMenuItem areaPropsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePeptideComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem rtValueMenuItem;
        private System.Windows.Forms.ToolStripMenuItem allRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fwhmRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fwbRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePropsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator38;
        private System.Windows.Forms.ToolStripStatusLabel statusPeptides;
        private System.Windows.Forms.ToolStripMenuItem ratiosContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorRatios;
        private System.Windows.Forms.ToolStripMenuItem ratiosToGlobalStandardsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicatesRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem averageReplicatesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleReplicateRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bestReplicateRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicatesTreeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleReplicateTreeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bestReplicateTreeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem videosMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tutorialsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importDocumentMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ionMzValuesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem observedMzValuesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chargesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLibraryPeakAreaContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateOrderContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateOrderDocumentContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateOrderAcqTimeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showDotProductToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showPeakAreaLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showRTLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem scopeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentScopeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem proteinScopeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chooseCalculatorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem placeholderToolStripMenuItem1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorCalculators;
        private System.Windows.Forms.ToolStripMenuItem updateCalculatorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addCalculatorContextMenuItem;
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
        private System.Windows.Forms.ToolStripMenuItem showLibraryChromatogramsSpectrumContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chromatogramsToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton publishToolbarButton;
        private System.Windows.Forms.ToolStripMenuItem updatesToolsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setStandardTypeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem noStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem qcStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem irtStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem normStandardContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator49;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator50;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator51;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator52;
        private System.Windows.Forms.ToolStripMenuItem toolStoreMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorTools;
        private System.Windows.Forms.ToolStripMenuItem startPageMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addMoleculeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addTransitionMoleculeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openContainingFolderMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator53;
        private System.Windows.Forms.ToolStripMenuItem addSmallMoleculePrecursorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePlotContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeCorrelationContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeResidualsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePointsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeTargetsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeStandardsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeDecoysContextMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuMassErrors;
        private System.Windows.Forms.ToolStripMenuItem massErrorGraphContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorReplicateComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorPeptideComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideOrderMassErrorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorPropsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showMassErrorLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorHistogramContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorPointsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorTargetsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorDecoysContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem binCountContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ppm05ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ppm10ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ppm15ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ppm20ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorTransitionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorAllTransitionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorBestTransitionsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator55;
        private System.Windows.Forms.ToolStripMenuItem MassErrorPrecursorsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem MassErrorProductsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorHistogram2DContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorXAxisContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErorrRetentionTimeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorMassToChargContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorlogScaleContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorTargets1FDRContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem surrogateStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem scoreToRunToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runToRunToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator checkForUpdatesSeparator;
        private System.Windows.Forms.ToolStripMenuItem checkForUpdatesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSpectralLibraryMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setRegressionMethodContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem linearRegressionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem kernelDensityEstimationContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loessContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importAssayLibraryMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVHistogramContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVHistogram2DContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVbinWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCV05binWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCV10binWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCV15binWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCV20binWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pointsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVtargetsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVdecoysToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVNormalizedToToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVLogScaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeAboveCVCutoffToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator57;
        private System.Windows.Forms.ToolStripMenuItem graphTypeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem barAreaGraphDisplayTypeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem lineAreaGraphDisplayTypeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAnnotationsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importAnnotationsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVTransitionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVAllTransitionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVCountTransitionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVBestTransitionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator58;
        private System.Windows.Forms.ToolStripMenuItem areaCVPrecursorsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVProductsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorSelectUI;
        private System.Windows.Forms.ToolStripDropDownButton modeUIToolBarDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem targetsAt1FDRToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem webinarsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reportsHelpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem commandLineHelpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem otherDocsHelpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toggleQuantitativeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem markTransitionsQuantitativeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem scoreContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator61;
        private System.Windows.Forms.ToolStripMenuItem koinaLibMatchItem;
        private System.Windows.Forms.ToolStripMenuItem mirrorMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logRegressionContextMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuDetections;
        private System.Windows.Forms.ToolStripMenuItem detectionsTargetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsTargetPrecursorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsTargetPeptideToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsGraphTypeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsGraphTypeReplicateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsGraphTypeHistogramToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator detectionsToolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowSelectionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowLegendToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowMeanToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsShowAtLeastNToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsYScaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detectionsYScaleOneToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator detectionsToolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem detectionsPropertiesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator detectionsToolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem detectionsYScalePercentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem massErrorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem synchMzScaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editSpectrumFilterContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem submitErrorReportMenuItem;
        private System.Windows.Forms.ToolStripMenuItem crashSkylineMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openPanoramaMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runPeptideSearchToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem encyclopeDiaSearchMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFeatureDetectionMenuItem;
    }
}

