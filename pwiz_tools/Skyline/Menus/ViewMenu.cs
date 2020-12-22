using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Menus
{
    public partial class ViewMenu : SkylineControl
    {
        public ViewMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
            DropDownItems = ImmutableList.ValueOf(viewToolStripMenuItem.DropDownItems.Cast<ToolStripItem>());
            statusToolStripMenuItem.Checked = Settings.Default.ShowStatusBar;
            if (!statusToolStripMenuItem.Checked)
                statusToolStripMenuItem_Click(this, new EventArgs());
            toolBarToolStripMenuItem.Checked = Settings.Default.RTPredictorVisible;
            if (!toolBarToolStripMenuItem.Checked)
            {
                toolBarToolStripMenuItem_Click(this, new EventArgs());
            }

            largeToolStripMenuItem.Checked = Settings.Default.TextZoom == TreeViewMS.LRG_TEXT_FACTOR;
            extraLargeToolStripMenuItem.Checked = Settings.Default.TextZoom == TreeViewMS.XLRG_TEXT_FACTOR;
            defaultTextToolStripMenuItem.Checked =
                !(largeToolStripMenuItem.Checked || extraLargeToolStripMenuItem.Checked);

        }

        public IEnumerable<ToolStripItem> DropDownItems { get; }


        public ToolStripMenuItem ReplicateComparisonMenuItem
        {
            get { return replicateComparisonMenuItem; }
        }

        public ToolStripMenuItem RetentionTimesMenuItem => retentionTimesMenuItem;
        public ToolStripMenuItem RunToRunMenuItem => runToRunMenuItem;

        public ToolStripMenuItem TimePeptideComparisonMenuItem => timePeptideComparisonMenuItem;

        public ToolStripMenuItem RegressionMenuItem => regressionMenuItem;

        public ToolStripMenuItem ScoreToRunMenuItem => scoreToRunMenuItem;

        public ToolStripMenuItem SchedulingMenuItem => schedulingMenuItem;

        public ToolStripMenuItem ResultsGridMenuItem => resultsGridMenuItem;

        public ToolStripMenuItem PeakAreasMenuItem => peakAreasMenuItem;

        public ToolStripMenuItem AreaReplicateComparisonMenuItem => areaReplicateComparisonMenuItem;
        public ToolStripMenuItem AreaPeptideComparisonMenuItem => areaPeptideComparisonMenuItem;
        public ToolStripMenuItem AreaCVHistogramMenuItem => areaCVHistogramMenuItem;
        public ToolStripMenuItem AreaCVHistogram2DMenuItem => areaCVHistogram2DMenuItem;
        public ToolStripMenuItem MassErrorsMenuItem => massErrorsMenuItem;
        public ToolStripMenuItem MassErrorReplicateComparisonMenuItem => massErrorReplicateComparisonMenuItem;
        public ToolStripMenuItem MassErrorPeptideComparisonMenuItem => massErrorPeptideComparisonMenuItem;
        public ToolStripMenuItem DetectionsPlotsMenuItem => detectionsPlotsMenuItem;
        public ToolStripMenuItem DetectionsHistogramMenuItem => detectionsHistogramMenuItem;
        public ToolStripMenuItem DetectionsReplicateComparisonMenuItem => detectionsReplicateComparisonMenuItem;

        public ToolStripMenuItem ChromatogramsMenuItem => chromatogramsMenuItem;
        public ToolStripMenuItem TransitionsMenuItem => transitionsMenuItem;
        public ToolStripMenuItem TransformChromMenuItem => transformChromMenuItem;
        public ToolStripMenuItem AutoZoomMenuItem => autoZoomMenuItem;
        public ToolStripMenuItem ArrangeGraphsToolStripMenuItem => arrangeGraphsToolStripMenuItem;

        public ToolStripMenuItem LibraryMatchToolStripMenuItem => libraryMatchToolStripMenuItem;

        public ToolStripMenuItem IonTypesMenuItem => ionTypesMenuItem;
        public ToolStripMenuItem ChargesMenuItem => chargesMenuItem;
        public ToolStripMenuItem RanksMenuItem => ranksMenuItem;
        public ToolStripMenuItem PrecursorsTranMenuItem => precursorsTranMenuItem;
        public ToolStripMenuItem ProductsTranMenuItem => productsTranMenuItem;
        public ToolStripMenuItem BasePeakMenuItem => basePeakMenuItem;
        public ToolStripMenuItem TicMenuItem => ticMenuItem;
        public ToolStripMenuItem SplitGraphMenuItem => splitGraphMenuItem;
        public ToolStripMenuItem QcMenuItem => qcMenuItem;
        public ToolStripSeparator ToolStripSeparatorTranMain => toolStripSeparatorTranMain;
        public ToolStripMenuItem SingleTranMenuItem => singleTranMenuItem;
        public ToolStripMenuItem AllTranMenuItem => allTranMenuItem;
        public ToolStripMenuItem TotalTranMenuItem => totalTranMenuItem;
        public ToolStripMenuItem TransformChromNoneMenuItem => transformChromNoneMenuItem;
        public ToolStripMenuItem TransformChromInterpolatedMenuItem => transformChromInterpolatedMenuItem;
        public ToolStripMenuItem SecondDerivativeMenuItem => secondDerivativeMenuItem;
        public ToolStripMenuItem FirstDerivativeMenuItem => firstDerivativeMenuItem;
        public ToolStripMenuItem SmoothSGChromMenuItem => smoothSGChromMenuItem;
        public ToolStripMenuItem AutoZoomNoneMenuItem => autoZoomNoneMenuItem;
        public ToolStripMenuItem AutoZoomBestPeakMenuItem => autoZoomBestPeakMenuItem;
        public ToolStripMenuItem AutoZoomRTWindowMenuItem => autoZoomRTWindowMenuItem;
        public ToolStripMenuItem AutoZoomBothMenuItem => autoZoomBothMenuItem;

        public ToolStripMenuItem NextReplicateMenuItem => nextReplicateMenuItem;
        public ToolStripMenuItem PreviousReplicateMenuItem => previousReplicateMenuItem;

        public double TargetsTextFactor
        {
            get { return Settings.Default.TextZoom; }
            set
            {
                Settings.Default.TextZoom = value;
                largeToolStripMenuItem.Checked = (value == TreeViewMS.LRG_TEXT_FACTOR);
                extraLargeToolStripMenuItem.Checked = (value == TreeViewMS.XLRG_TEXT_FACTOR);
                defaultTextToolStripMenuItem.Checked = (!largeToolStripMenuItem.Checked &&
                                                        !extraLargeToolStripMenuItem.Checked);
            }
        }

        private void peptidesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            showTargetsByNameToolStripMenuItem.Checked =
                (Settings.Default.ShowPeptidesDisplayMode == ProteinMetadataManager.ProteinDisplayMode.ByName.ToString());
            showTargetsByAccessionToolStripMenuItem.Checked =
                (Settings.Default.ShowPeptidesDisplayMode == ProteinMetadataManager.ProteinDisplayMode.ByAccession.ToString());
            showTargetsByPreferredNameToolStripMenuItem.Checked =
                (Settings.Default.ShowPeptidesDisplayMode == ProteinMetadataManager.ProteinDisplayMode.ByPreferredName.ToString());
            showTargetsByGeneToolStripMenuItem.Checked =
                (Settings.Default.ShowPeptidesDisplayMode == ProteinMetadataManager.ProteinDisplayMode.ByGene.ToString());
        }

        public void UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode mode)
        {
            Settings.Default.ShowPeptidesDisplayMode = mode.ToString();
            Settings.Default.ShowPeptides = true;
            SkylineWindow.ShowSequenceTreeForm(true, true);

            CollectionUtil.ForEach(FormUtil.OpenForms.OfType<FoldChangeBarGraph>(), b => b.QueueUpdateGraph());
            CollectionUtil.ForEach(FormUtil.OpenForms.OfType<FoldChangeVolcanoPlot>(), v => v.QueueUpdateGraph());
        }

        private void showTargetsByNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode.ByName);
        }

        private void showTargetsByAccessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode.ByAccession);
        }

        private void showTargetsByPreferredNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode.ByPreferredName);
        }

        private void showTargetsByGeneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode.ByGene);
        }
        private void defaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeTextSize(TreeViewMS.DEFAULT_TEXT_FACTOR);
        }

        private void largeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
        }

        private void extraLargeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeTextSize(TreeViewMS.XLRG_TEXT_FACTOR);
        }

        public void ChangeTextSize(double textFactor)
        {
            SkylineWindow.ChangeTextSize(textFactor);
        }
        private void proteomicsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetUIMode(SrmDocument.DOCUMENT_TYPE.proteomic);
        }

        private void moleculeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules);
        }

        private void mixedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetUIMode(SrmDocument.DOCUMENT_TYPE.mixed);
        }

        public void SetUIMode(SrmDocument.DOCUMENT_TYPE mode)
        {
            SkylineWindow.SetUIMode(mode);
        }

        private void spectralLibrariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ViewSpectralLibraries();
        }

        public void ViewSpectralLibraries()
        {
            SkylineWindow.ViewSpectralLibraries();
        }

        private void arrangeRowMenuItem_Click(object sender, EventArgs e)
        {
            ArrangeGraphs(DisplayGraphsType.Row);
        }

        private void arrangeColumnMenuItem_Click(object sender, EventArgs e)
        {
            ArrangeGraphs(DisplayGraphsType.Column);
        }

        public void ArrangeGraphs(DisplayGraphsType displayGraphsType)
        {
            SkylineWindow.ArrangeGraphs(displayGraphsType);
        }
        private void arrangeTabbedMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ArrangeGraphsTabbed();
        }
        private void arrangeGroupedMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ArrangeGraphsGrouped();
        }
        private void arrangeTiledMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ArrangeGraphsTiled();
        }
        private void graphsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowGraphSpectrum(Settings.Default.ShowSpectra = true);
        }

        private void ionTypesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var set = Settings.Default;
            aMenuItem.Checked = set.ShowAIons;
            bMenuItem.Checked = set.ShowBIons;
            cMenuItem.Checked = set.ShowCIons;
            xMenuItem.Checked = set.ShowXIons;
            yMenuItem.Checked = set.ShowYIons;
            zMenuItem.Checked = set.ShowZIons;
            fragmentsMenuItem.Checked = set.ShowFragmentIons;
            precursorIonMenuItem.Checked = set.ShowPrecursorIon;
            UpdateIonTypesMenuItemsVisibility();
        }

        // Update the Ion Types menu for document contents
        public void UpdateIonTypesMenuItemsVisibility()
        {
            aMenuItem.Visible = bMenuItem.Visible = cMenuItem.Visible =
                xMenuItem.Visible = yMenuItem.Visible = zMenuItem.Visible =
                    DocumentUI.DocumentType != SrmDocument.DOCUMENT_TYPE.small_molecules;

            fragmentsMenuItem.Visible = DocumentUI.HasSmallMolecules;
        }


        public GraphSpectrumSettings GraphSpectrumSettings
        {
            get { return SkylineWindow.GraphSpectrumSettings; }
        }
        private void aMenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowAIons = !GraphSpectrumSettings.ShowAIons;
        }

        private void bMenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowBIons = !GraphSpectrumSettings.ShowBIons;
        }

        private void cMenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowCIons = !GraphSpectrumSettings.ShowCIons;
        }

        private void xMenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowXIons = !GraphSpectrumSettings.ShowXIons;
        }

        private void yMenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowYIons = !GraphSpectrumSettings.ShowYIons;
        }

        private void zMenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowZIons = !GraphSpectrumSettings.ShowZIons;
        }

        private void fragmentsMenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowFragmentIons = !GraphSpectrumSettings.ShowFragmentIons;
        }

        private void precursorIonMenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowPrecursorIon = !GraphSpectrumSettings.ShowPrecursorIon;
        }

        private void chargesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var set = SkylineWindow.GraphSpectrumSettings;
            charge1MenuItem.Checked = set.ShowCharge1;
            charge2MenuItem.Checked = set.ShowCharge2;
            charge3MenuItem.Checked = set.ShowCharge3;
            charge4MenuItem.Checked = set.ShowCharge4;
        }

        private void charge1MenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowCharge1 = !GraphSpectrumSettings.ShowCharge1;
        }

        private void charge2MenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowCharge2 = !GraphSpectrumSettings.ShowCharge2;
        }

        private void charge3MenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowCharge3 = !GraphSpectrumSettings.ShowCharge3;
        }

        private void charge4MenuItem_Click(object sender, EventArgs e)
        {
            GraphSpectrumSettings.ShowCharge4 = !GraphSpectrumSettings.ShowCharge4;
        }
        private void ranksMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRanks = !Settings.Default.ShowRanks;
            SkylineWindow.UpdateSpectrumGraph(false);
        }
        private void chromatogramsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = chromatogramsMenuItem;
            if (!DocumentUI.Settings.HasResults)
            {
                // Strange problem in .NET where a dropdown will show when
                // its menuitem is disabled.
                chromatogramsMenuItem.HideDropDown();
                return;
            }

            // If MeasuredResults is null, then this menuitem is incorrectly enabled
            var chromatograms = DocumentUI.Settings.MeasuredResults.Chromatograms;

            int i = 0;
            menu.DropDown.SuspendLayout();
            try
            {
                foreach (var chrom in chromatograms)
                {
                    string name = chrom.Name;
                    ToolStripMenuItem item = null;
                    if (i < menu.DropDownItems.Count)
                        item = menu.DropDownItems[i] as ToolStripMenuItem;
                    if (item == null || name != item.Name)
                    {
                        // Remove the rest of the existing items
                        while (!ReferenceEquals(menu.DropDownItems[i], toolStripSeparatorReplicates))
                            menu.DropDownItems.RemoveAt(i);

                        ShowChromHandler handler = new ShowChromHandler(SkylineWindow, chrom.Name);
                        item = new ToolStripMenuItem(chrom.Name, null,
                            handler.menuItem_Click);
                        menu.DropDownItems.Insert(i, item);
                    }

                    i++;
                }

                // Remove the rest of the existing items
                while (!ReferenceEquals(menu.DropDownItems[i], toolStripSeparatorReplicates))
                    menu.DropDownItems.RemoveAt(i);
            }
            finally
            {
                menu.DropDown.ResumeLayout();
            }

            closeChromatogramMenuItem.Enabled = !string.IsNullOrEmpty(SkylineWindow.SelectedGraphChromName);
        }
        private class ShowChromHandler
        {
            private readonly SkylineWindow _skyline;
            private readonly string _nameChromatogram;

            public ShowChromHandler(SkylineWindow skyline, string nameChromatogram)
            {
                _skyline = skyline;
                _nameChromatogram = nameChromatogram;
            }

            public void menuItem_Click(object sender, EventArgs e)
            {
                _skyline.ShowGraphChrom(_nameChromatogram, true);
            }
        }

        private void nextReplicateMenuItem_Click(object sender, EventArgs e)
        {
            SelectedResultsIndex++;
        }

        private void previousReplicateMenuItem_Click(object sender, EventArgs e)
        {
            SelectedResultsIndex--;
        }

        private void closeChromatogramMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.CloseMostRecentChromatogram();
        }

        private void closeAllChromatogramsMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.CloseAllChromatograms();
        }

        private void transitionsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SkylineWindow.TransitionsMenuItemDropDownOpening();
        }
        private void singleTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowSingleTransition();
        }

        private void precursorsTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPrecursorTransitions();
        }

        private void productsTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowProductTransitions();
        }

        private void allTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowAllTransitions();
        }

        private void totalTranMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowTotalTransitions();
        }

        private void basePeakMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowBasePeak();
        }

        private void ticMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowTic();
        }

        private void onlyQuantitativeMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowOnlyQuantitative(!Settings.Default.ShowQuantitativeOnly);
        }
        private void splitChromGraphMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowSplitChromatogramGraph(!Settings.Default.SplitChromatogramGraph);
        }

        private void transformChromMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SkylineWindow.TransformChromMenuItemDropDownOpening();

        }

        private void transformChromNoneMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.raw);
        }


        private void transformInterpolatedMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.interpolated);
        }


        private void secondDerivativeMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.craw2d);
        }

        private void firstDerivativeMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.craw1d);
        }

        private void smoothSGChromMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.savitzky_golay);
        }
        private void autozoomMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SkylineWindow.AutozoomMenuItemDropDownOpening();
        }

        private void autoZoomNoneMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomNone();
        }

        private void autoZoomBestPeakMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomBestPeak();
        }

        private void autoZoomRTWindowMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomRTWindow();
        }

        private void autoZoomBothMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomBoth();
        }

        private void timeGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SkylineWindow.TimeGraphMenuItemDropDownOpening();
        }
        private void replicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTReplicateGraph();
        }
        private void timePeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTPeptideGraph();
        }
        private void regressionMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTRegressionGraphScoreToRun();
        }

        private void fullReplicateComparisonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTRegressionGraphRunToRun();
        }

        private void retentionTimeAlignmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRetentionTimeAlignmentForm();
        }
        private void schedulingMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTSchedulingGraph();
        }

        private void areaGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.AreaGraphTypes;
            var list = SkylineWindow.ListGraphPeakArea;
            areaReplicateComparisonMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.replicate);
            areaPeptideComparisonMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.peptide);
            areaCVHistogramMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.histogram);
            areaCVHistogram2DMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.histogram2d);
        }
        private void areaReplicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeakAreaReplicateComparison();
        }
        private void areaPeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeakAreaPeptideGraph();
        }
        private void areaCVHistogramToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeakAreaCVHistogram();
        }
        private void areaCVHistogram2DToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeakAreaCVHistogram2D();
        }
        private void graphDetections_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.DetectionGraphTypes;
            var list = SkylineWindow.ListGraphDetections;

            detectionsReplicateComparisonMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.detections);
            detectionsHistogramMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.detections_histogram);
        }
        private void detectionsReplicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowDetectionsReplicateComparisonGraph();
        }
        private void detectionsHistogramMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowDetectionsHistogramGraph();
        }

        private void massErrorMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.MassErrorGraphTypes;
            var list = SkylineWindow.ListGraphMassError;
            massErrorReplicateComparisonMenuItem.Checked =
                SkylineWindow.GraphChecked(list, types, GraphTypeSummary.replicate);
            massErrorPeptideComparisonMenuItem.Checked =
                SkylineWindow.GraphChecked(list, types, GraphTypeSummary.peptide);
            massErrorHistogramMenuItem.Checked =SkylineWindow.GraphChecked(list, types, GraphTypeSummary.histogram);
            massErrorHistogram2DMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.histogram2d);
        }

        private void massErrorReplicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorReplicateComparison();
        }
        private void massErrorPeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorPeptideGraph();
        }
        private void massErrorHistogramMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorHistogramGraph();
        }
        private void massErrorHistogram2DMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorHistogramGraph2D();
        }
        private void calibrationCurvesMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowCalibrationForm();
        }
        private void documentGridMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowDocumentGrid(true);
        }
        private void resultsGridMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowResultsGrid(Settings.Default.ShowResultsGrid = true);
        }

        private void groupComparisonsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            groupComparisonsMenuItem.DropDownItems.Clear();
            if (DocumentUI.Settings.DataSettings.GroupComparisonDefs.Any())
            {
                foreach (var groupComparisonDef in DocumentUI.Settings.DataSettings.GroupComparisonDefs)
                {
                    groupComparisonsMenuItem.DropDownItems.Add(MakeToolStripMenuItem(groupComparisonDef));
                }
                groupComparisonsMenuItem.DropDownItems.Add(new ToolStripSeparator());
            }
            groupComparisonsMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                addGroupComparisonMenuItem,
                editGroupComparisonListMenuItem
            });
        }
        private ToolStripMenuItem MakeToolStripMenuItem(GroupComparisonDef groupComparisonDef)
        {
            return new ToolStripMenuItem(groupComparisonDef.Name, null, (sender, args) =>
            {
                SkylineWindow.ShowGroupComparisonWindow(groupComparisonDef.Name);
            });
        }
        private void addFoldChangeMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AddGroupComparison();
        }
        private void editGroupComparisonListMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.DisplayDocumentSettingsDialogPage(DocumentSettingsDlg.TABS.group_comparisons);
        }
        private void listsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            while (listsMenuItem.DropDownItems.Count > 1)
            {
                listsMenuItem.DropDownItems.RemoveAt(listsMenuItem.DropDownItems.Count - 1);
            }
            foreach (var listData in Document.Settings.DataSettings.Lists)
            {
                string listName = listData.ListDef.Name;
                listsMenuItem.DropDownItems.Add(new ToolStripMenuItem(listName, null, (a, args) =>
                {
                    SkylineWindow.ShowList(listName);
                }));
            }
        }
        private void defineNewListMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AddListDefinition();
        }
        private void auditLogMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowAuditLog();
        }

        private void toolBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowToolBar(toolBarToolStripMenuItem.Checked);
        }
        private void statusToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowStatusBar(statusToolStripMenuItem.Checked);
        }
        public void CheckIonCharge(Adduct adduct, bool check)
        {
            // Set charge settings without causing UI to update
            var set = Settings.Default;
            switch (Math.Abs(adduct.AdductCharge))  // TODO(bspratt) - need a lot more flexibility here, neg charges, M+Na etc
            {
                case 1: set.ShowCharge1 = charge1MenuItem.Checked = check; break;
                case 2: set.ShowCharge2 = charge2MenuItem.Checked = check; break;
                case 3: set.ShowCharge3 = charge3MenuItem.Checked = check; break;
                case 4: set.ShowCharge4 = charge4MenuItem.Checked = check; break;
            }
        }
        public void CheckIonType(IonType type, bool check, bool visible)
        {
            var set = Settings.Default;
            switch (type)
            {
                case IonType.a: set.ShowAIons = aMenuItem.Checked = check; aMenuItem.Visible = visible; break;
                case IonType.b: set.ShowBIons = bMenuItem.Checked = check; bMenuItem.Visible = visible; break;
                case IonType.c: set.ShowCIons = cMenuItem.Checked = check; cMenuItem.Visible = visible; break;
                case IonType.x: set.ShowXIons = xMenuItem.Checked = check; xMenuItem.Visible = visible; break;
                case IonType.y: set.ShowYIons = yMenuItem.Checked = check; yMenuItem.Visible = visible; break;
                case IonType.z: set.ShowZIons = zMenuItem.Checked = check; zMenuItem.Visible = visible; break;
                case IonType.custom: set.ShowFragmentIons = fragmentsMenuItem.Checked = check; fragmentsMenuItem.Visible = visible; break;
            }
        }

        public void DocumentUiChanged()
        {
            proteomicsToolStripMenuItem.Checked = SkylineWindow.ModeUI == SrmDocument.DOCUMENT_TYPE.proteomic;
            moleculeToolStripMenuItem.Checked = SkylineWindow.ModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules;
            mixedToolStripMenuItem.Checked = SkylineWindow.ModeUI == SrmDocument.DOCUMENT_TYPE.mixed;
        }
        public void ViewMenuDropDownOpening()
        {
            viewModificationsMenuItem.DropDownItems.Clear();
            var currentOption = DisplayModificationOption.Current;
            foreach (var opt in DisplayModificationOption.All)
            {
                var menuItem = new ToolStripMenuItem(opt.MenuItemText);
                menuItem.Click += (s, a) => SkylineWindow.SetModifiedSequenceDisplayOption(opt);
                menuItem.Checked = Equals(currentOption, opt);
                viewModificationsMenuItem.DropDownItems.Add(menuItem);
            }
            ranksMenuItem.Checked = Settings.Default.ShowRanks;
        }
    }
}
