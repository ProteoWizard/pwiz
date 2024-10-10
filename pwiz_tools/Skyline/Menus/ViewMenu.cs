/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.Spectra;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Menus
{
    public partial class ViewMenu : SkylineControl, IMenuControlImplementer
    {
        private bool ProteomicsEnabled { get; set; }
        private bool SmallMoleculesEnabled { get; set; }

        public ViewMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
            DropDownItems = ImmutableList.ValueOf(viewToolStripMenuItem.DropDownItems.Cast<ToolStripItem>());
            statusToolStripMenuItem.Checked = Settings.Default.ShowStatusBar;
            if (!statusToolStripMenuItem.Checked)
            {
                SkylineWindow.ShowStatusBar(statusToolStripMenuItem.Checked);
            }
            toolBarToolStripMenuItem.Checked = Settings.Default.RTPredictorVisible;
            if (!toolBarToolStripMenuItem.Checked)
            {
                SkylineWindow.ShowToolBar(toolBarToolStripMenuItem.Checked);
            }

            largeToolStripMenuItem.Checked = Settings.Default.TextZoom == TreeViewMS.LRG_TEXT_FACTOR;
            extraLargeToolStripMenuItem.Checked = Settings.Default.TextZoom == TreeViewMS.XLRG_TEXT_FACTOR;
            defaultTextToolStripMenuItem.Checked =
                !(largeToolStripMenuItem.Checked || extraLargeToolStripMenuItem.Checked);
            ProteomicsEnabled = false;
            SmallMoleculesEnabled = false;
        }

        public IEnumerable<ToolStripItem> DropDownItems { get; }

        public ToolStripMenuItem NextReplicateMenuItem => nextReplicateMenuItem;
        public ToolStripMenuItem PreviousReplicateMenuItem => previousReplicateMenuItem;

        #region Text Size
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
        #endregion

        #region Target Display Mode
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
            ShowTargetsWindow();
        }

        private void ShowTargetsWindow()
        {
            SkylineWindow.ShowSequenceTreeForm(true, true);

            CollectionUtil.ForEach(FormUtil.OpenForms.OfType<FoldChangeBarGraph>(), b => b.QueueUpdateGraph());
            CollectionUtil.ForEach(FormUtil.OpenForms.OfType<FoldChangeVolcanoPlot>(), v => v.QueueUpdateGraph());
            SkylineWindow.UpdatePeakAreaGraph();
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
        #endregion

        #region UI Modes
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
        #endregion

        private void spectralLibrariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ViewSpectralLibraries();
        }

        public void ViewSpectralLibraries()
        {
            SkylineWindow.ViewSpectralLibraries();
        }

        #region Arrange Graphs
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
        #endregion
        private void libraryMatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowGraphSpectrum(Settings.Default.ShowSpectra = true);
        }

        #region View Ion Types
        public void CheckIonCharge(Adduct adduct, bool check)
        {
            // Set charge settings without causing UI to update
            var set = Settings.Default;
            switch (Math.Abs(adduct.AdductCharge))  // TODO(bspratt) - need a lot more flexibility here, neg charges, M+Na etc
            {
                case 1: set.ShowCharge1 = check; break;
                case 2: set.ShowCharge2 = check; break;
                case 3: set.ShowCharge3 = check; break;
                case 4: set.ShowCharge4 = check; break;
            }
            UpdateChargesMenu();
        }

        public void UpdateChargesMenu()
        {
            if (chargesMenuItem.DropDownItems.Count > 0 && chargesMenuItem.DropDownItems[0] is MenuControl<ChargeSelectionPanel> chargeSelector)
            {
                chargeSelector.Update(GraphSpectrumSettings, DocumentUI.Settings.PeptideSettings);
            }
            else
            {
                chargesMenuItem.DropDownItems.Clear();
                var selectorControl = new MenuControl<ChargeSelectionPanel>(GraphSpectrumSettings, DocumentUI.Settings.PeptideSettings);
                chargesMenuItem.DropDownItems.Add(selectorControl);
                selectorControl.HostedControl.OnChargeChanged += IonChargeSelector_ionChargeChanged;
            }
        }

        public void IonChargeSelector_ionChargeChanged(int charge, bool show)
        {
            switch (charge)
            {
                case 1:
                    SkylineWindow.ShowCharge1(show);
                    break;
                case 2:
                    SkylineWindow.ShowCharge2(show);
                    break;
                case 3:
                    SkylineWindow.ShowCharge3(show);
                    break;
                case 4:
                    SkylineWindow.ShowCharge4(show);
                    break;
            }
        }

        public void UpdateIonTypeMenu()
        {
            if (ProteomicsEnabled)
            {
                if (ionTypesMenuItem.DropDownItems.Count > 0 &&
                    ionTypesMenuItem.DropDownItems[0] is MenuControl<IonTypeSelectionPanel> ionSelector)
                {
                    ionSelector.Update(GraphSpectrumSettings, DocumentUI.Settings.PeptideSettings);
                }
                else
                {
                    ionTypesMenuItem.DropDownItems.Clear();
                    var ionTypeSelector = new MenuControl<IonTypeSelectionPanel>(GraphSpectrumSettings,
                        DocumentUI.Settings.PeptideSettings);
                    ionTypesMenuItem.DropDownItems.Add(ionTypeSelector);
                    ionTypeSelector.HostedControl.IonTypeChanged += IonTypeSelector_IonTypeChanges;
                    ionTypeSelector.HostedControl.LossChanged += IonTypeSelector_LossChanged;
                }
            }
            else
            {
                if (ionTypesMenuItem.DropDownItems.Count > 0 &&
                    ionTypesMenuItem.DropDownItems[0] is MenuControl<IonTypeSelectionPanel> ionSelector)
                {
                    ionTypesMenuItem.DropDownItems.Clear();
                    ionSelector.HostedControl.IonTypeChanged -= IonTypeSelector_IonTypeChanges;
                    ionSelector.HostedControl.LossChanged -= IonTypeSelector_LossChanged;
                }
            }
            ionTypesMenuItem.Visible = ProteomicsEnabled;
            fragmentsMenuItem.Visible = SmallMoleculesEnabled;
            fragmentsMenuItem.Checked = GraphSpectrumSettings.ShowFragmentIons;
            precursorIonMenuItem.Checked = GraphSpectrumSettings.ShowPrecursorIon;
            specialIonsMenuItem.Checked = GraphSpectrumSettings.ShowSpecialIons;
        }

        private void IonTypeSelector_IonTypeChanges(IonType type, bool show)
        {
            switch (type)
            {
                case IonType.a:
                    SkylineWindow.ShowAIons(show);
                    break;
                case IonType.b:
                    SkylineWindow.ShowBIons(show);
                    break;
                case IonType.c:
                    SkylineWindow.ShowCIons(show);
                    break;
                case IonType.x:
                    SkylineWindow.ShowXIons(show);
                    break;
                case IonType.y:
                    SkylineWindow.ShowYIons(show);
                    break;
                case IonType.z:
                    SkylineWindow.ShowZIons(show);
                    break;
                case IonType.zh:
                    SkylineWindow.ShowZHIons(show);
                    break;
                case IonType.zhh:
                    SkylineWindow.ShowZHHIons(show);
                    break;
            }
        }

        private void IonTypeSelector_LossChanged(string[] losses)
        {
            SkylineWindow.ShowLosses(losses);
        }

        public void CheckIonType(IonType type, bool check)
        {
            var set = Settings.Default;
            
            switch (type)
            {
                case IonType.a: set.ShowAIons= check; break;
                case IonType.b: set.ShowBIons= check; break;
                case IonType.c: set.ShowCIons= check; break;
                case IonType.x: set.ShowXIons= check; break;
                case IonType.y: set.ShowYIons= check; break;
                case IonType.z: set.ShowZIons= check; break;
                case IonType.zh: set.ShowZHIons = check; break;
                case IonType.zhh: set.ShowZHHIons = check; break;
                case IonType.custom: set.ShowFragmentIons = fragmentsMenuItem.Checked = check; break;
            }
        }

        public void EnableProteomicIons(bool visible)
        {
            ProteomicsEnabled = visible;
            UpdateIonTypeMenu();
        }

        public void EnableSmallMoleculeIons(bool visible)
        {
            SmallMoleculesEnabled = visible;
            UpdateIonTypeMenu();
        }

        private void ionTypesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UpdateIonTypeMenu();
        }

        public MenuControl<T> GetHostedControl<T>() where T : Panel, IControlSize, new()
        {
            var chargesItem = viewToolStripMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.DropDownItems.OfType<MenuControl<T>>().Any());
            if (chargesItem != null)
                return chargesItem.DropDownItems[0] as MenuControl<T>;
            return null;
        }

        public void DisconnectHandlers()
        {
            var chargeSelector = GetHostedControl<ChargeSelectionPanel>();

            if (chargeSelector != null)
            {
                chargeSelector.HostedControl.OnChargeChanged -= IonChargeSelector_ionChargeChanged;
            }

            var ionTypeSelector = GetHostedControl<IonTypeSelectionPanel>();
            if (ionTypeSelector != null)
            {
                ionTypeSelector.HostedControl.IonTypeChanged -= IonTypeSelector_IonTypeChanges;
                ionTypeSelector.HostedControl.LossChanged -= IonTypeSelector_LossChanged;
            }
        }

        public GraphSpectrumSettings GraphSpectrumSettings
        {
            get { return SkylineWindow.GraphSpectrumSettings; }
        }

        private void fragmentsMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowFragmentIons(!GraphSpectrumSettings.ShowFragmentIons);
        }

        private void specialIonsMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowSpecialIons(!GraphSpectrumSettings.ShowSpecialIons);
        }

        private void precursorIonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPrecursorIon(!GraphSpectrumSettings.ShowPrecursorIon);
        }

        private void chargesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UpdateChargesMenu();
        }


        private void ranksMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRanks = !Settings.Default.ShowRanks;
            SkylineWindow.UpdateSpectrumGraph(false);
        }
        #endregion

        #region Chromatograms
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
        #endregion

        #region Transitions
        private void transitionsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var displayType = GraphChromatogram.DisplayType;

            // If both MS1 and MS/MS ions are not possible, then menu items to differentiate precursors and
            // products are not necessary.
            bool showIonTypeOptions = SkylineWindow.IsMultipleIonSources;
            precursorsTranMenuItem.Visible = productsTranMenuItem.Visible = showIonTypeOptions;

            if (!showIonTypeOptions &&
                (displayType == DisplayTypeChrom.precursors || displayType == DisplayTypeChrom.products))
                displayType = DisplayTypeChrom.all;

            // Only show all ions chromatogram options when at least one chromatogram of this type exists
            bool showAllIonsOptions = DocumentUI.Settings.HasResults && 
                                      DocumentUI.Settings.MeasuredResults.HasAllIonsChromatograms;
            basePeakMenuItem.Visible = ticMenuItem.Visible = qcMenuItem.Visible =toolStripSeparatorTranMain.Visible = showAllIonsOptions;

            if (!showAllIonsOptions &&
                (displayType == DisplayTypeChrom.base_peak || displayType == DisplayTypeChrom.tic ||
                 displayType == DisplayTypeChrom.qc))
                displayType = DisplayTypeChrom.all;

            if (showAllIonsOptions)
            {
                qcMenuItem.DropDownItems.Clear();
                var qcTraceNames = DocumentUI.MeasuredResults.QcTraceNames.ToList();
                if (qcTraceNames.Count > 0)
                {
                    var qcTraceItems = new ToolStripItem[qcTraceNames.Count];
                    var qcContextTraceItems = new ToolStripItem[qcTraceNames.Count];
                    for (int i = 0; i < qcTraceNames.Count; i++)
                    {
                        qcTraceItems[i] = new ToolStripMenuItem(qcTraceNames[i], null, qcMenuItem_Click)
                        {
                            Checked = displayType == DisplayTypeChrom.qc &&
                                      Settings.Default.ShowQcTraceName == qcTraceNames[i]
                        };
                        qcContextTraceItems[i] = new ToolStripMenuItem(qcTraceNames[i], null, qcMenuItem_Click)
                        {
                            Checked = displayType == DisplayTypeChrom.qc &&
                                      Settings.Default.ShowQcTraceName == qcTraceNames[i]
                        };
                    }

                    qcMenuItem.DropDownItems.AddRange(qcTraceItems);
                }
                else
                    qcMenuItem.Visible = false;
            }

            precursorsTranMenuItem.Checked = (displayType == DisplayTypeChrom.precursors);
            productsTranMenuItem.Checked = (displayType == DisplayTypeChrom.products);
            singleTranMenuItem.Checked = (displayType == DisplayTypeChrom.single);
            allTranMenuItem.Checked = (displayType == DisplayTypeChrom.all);
            totalTranMenuItem.Checked = (displayType == DisplayTypeChrom.total);
            basePeakMenuItem.Checked = (displayType == DisplayTypeChrom.base_peak);
            ticMenuItem.Checked = (displayType == DisplayTypeChrom.tic);
            splitGraphMenuItem.Checked = Settings.Default.SplitChromatogramGraph;
            onlyQuantitativeMenuItem.Checked = Settings.Default.ShowQuantitativeOnly;
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
        #endregion
        #region Transform
        private void transformChromMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var transform = GraphChromatogram.Transform;

            transformChromNoneMenuItem.Checked = (transform == TransformChrom.raw);
            transformChromInterpolatedMenuItem.Checked = (transform == TransformChrom.interpolated);
            secondDerivativeMenuItem.Checked = (transform == TransformChrom.craw2d);
            smoothSGChromMenuItem.Checked = (transform == TransformChrom.savitzky_golay);
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

        private void smoothSGChromMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetTransformChrom(TransformChrom.savitzky_golay);
        }
        #endregion

        #region Auto Zoom
        private void autozoomMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool hasRt = (DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime != null);
            autoZoomRTWindowMenuItem.Enabled = hasRt;
            autoZoomBothMenuItem.Enabled = hasRt;

            var zoom = SkylineWindow.EffectiveAutoZoom;
            if (!hasRt)
            {
                if (zoom == AutoZoomChrom.window)
                    zoom = AutoZoomChrom.none;
                else if (zoom == AutoZoomChrom.both)
                    zoom = AutoZoomChrom.peak;
            }

            autoZoomNoneMenuItem.Checked = (zoom == AutoZoomChrom.none);
            autoZoomBestPeakMenuItem.Checked = (zoom == AutoZoomChrom.peak);
            autoZoomRTWindowMenuItem.Checked = (zoom == AutoZoomChrom.window);
            autoZoomBothMenuItem.Checked = (zoom == AutoZoomChrom.both);
        }

        private void autoZoomNoneMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomNone();
        }

        private void autoZoomBestPeakMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetAutoZoomChrom(AutoZoomChrom.peak);
        }

        private void autoZoomRTWindowMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomRTWindow();
        }

        private void autoZoomBothMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AutoZoomBoth();
        }
        #endregion

        #region Retention Times
        private void retentionTimesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.RTGraphTypes;
            var list = SkylineWindow.ListGraphRetentionTime;
            bool runToRunRegression = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.run_to_run_regression);
            bool scoreToRunRegression = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.score_to_run_regression);

            runToRunMenuItem.Checked = runToRunRegression;
            scoreToRunMenuItem.Checked = scoreToRunRegression;
            regressionMenuItem.Checked = runToRunRegression || scoreToRunRegression;

            replicateComparisonMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.replicate);
            timePeptideComparisonMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.peptide);
            schedulingMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.schedule);
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
        #endregion

        #region Peak Areas
        private void areaGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.AreaGraphTypes;
            var list = SkylineWindow.ListGraphPeakArea;
            areaReplicateComparisonMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.replicate);
            areaPeptideComparisonMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.peptide);
            areaRelativeAbundanceMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.abundance);
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
        private void areaRelativeAbundanceMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeakAreaRelativeAbundanceGraph();
        }
        private void areaCVHistogramToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeakAreaCVHistogram();
        }
        private void areaCVHistogram2DToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeakAreaCVHistogram2D();
        }
        #endregion
        #region Detections
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
        #endregion

        #region Mass Errors
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
        #endregion
        private void calibrationCurvesMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowCalibrationForm();
        }

        #region Grids
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
        #endregion
        private void toolBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowToolBar(toolBarToolStripMenuItem.Checked);
        }
        private void statusToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowStatusBar(statusToolStripMenuItem.Checked);
        }
        public void DocumentUiChanged()
        {
            proteomicsToolStripMenuItem.Checked = SkylineWindow.ModeUI == SrmDocument.DOCUMENT_TYPE.proteomic;
            moleculeToolStripMenuItem.Checked = SkylineWindow.ModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules;
            mixedToolStripMenuItem.Checked = SkylineWindow.ModeUI == SrmDocument.DOCUMENT_TYPE.mixed;
        }
        public void ViewMenuDropDownOpening()
        {
            viewTargetsMenuItem.Checked = SkylineWindow.SequenceTreeFormIsVisible;
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
            UpdateChargesMenu();
        }

        public void UpdateGraphUi(Action ensureLayoutLocked, SrmSettings settingsNew, bool deserialized)
        {
            EnableGraphSpectrum(ensureLayoutLocked, settingsNew, deserialized);
            var enable = settingsNew.HasResults;
            bool enableSchedule = SkylineWindow.IsRetentionTimeGraphTypeEnabled(GraphTypeSummary.schedule);
            bool enableRunToRun = SkylineWindow.IsRetentionTimeGraphTypeEnabled(GraphTypeSummary.run_to_run_regression);
            if (replicateComparisonMenuItem.Enabled != enable ||
                retentionTimesMenuItem.Enabled != enableSchedule ||
                runToRunMenuItem.Enabled != enableRunToRun)
            {
                retentionTimesMenuItem.Enabled = enableSchedule;
                replicateComparisonMenuItem.Enabled = enable;
                timePeptideComparisonMenuItem.Enabled = enable;
                regressionMenuItem.Enabled = enable;
                scoreToRunMenuItem.Enabled = enable;
                runToRunMenuItem.Enabled = enableRunToRun;
                schedulingMenuItem.Enabled = enableSchedule;
                if (!deserialized)
                {
                    ensureLayoutLocked();
                    SkylineWindow.UpdateUIGraphRetentionTime(SkylineWindow.IsRetentionTimeGraphTypeEnabled);
                }
            }

            if (resultsGridMenuItem.Enabled != enable)
            {
                resultsGridMenuItem.Enabled = enable;
                if (!deserialized)
                {
                    ensureLayoutLocked();
                    SkylineWindow.ShowResultsGrid(enable && Settings.Default.ShowResultsGrid);
                }
            }

            if (candidatePeaksToolStripMenuItem.Enabled != enable)
                candidatePeaksToolStripMenuItem.Enabled = enable;

            if (peakAreasMenuItem.Enabled != enable)
            {
                peakAreasMenuItem.Enabled = enable;
                areaReplicateComparisonMenuItem.Enabled = enable;
                areaPeptideComparisonMenuItem.Enabled = enable;
                areaRelativeAbundanceMenuItem.Enabled = enable;
                areaCVHistogramMenuItem.Enabled = enable;
                areaCVHistogram2DMenuItem.Enabled = enable;

                if (!deserialized)
                {
                    ensureLayoutLocked();
                    SkylineWindow.UpdateUIGraphPeakArea(enable);
                }
            }
            if (massErrorsMenuItem.Enabled != enable)
            {
                massErrorsMenuItem.Enabled = enable;
                massErrorReplicateComparisonMenuItem.Enabled = enable;
                massErrorPeptideComparisonMenuItem.Enabled = enable;

                if (!deserialized)
                {
                    ensureLayoutLocked();
                    SkylineWindow.UpdateUIGraphMassError(enable);
                }
            }

            if (detectionsPlotsMenuItem.Enabled != enable)
            {
                detectionsPlotsMenuItem.Enabled = enable;
                detectionsHistogramMenuItem.Enabled = enable;
                detectionsReplicateComparisonMenuItem.Enabled = Enabled;
                if (!deserialized)
                {
                    ensureLayoutLocked();
                    SkylineWindow.UpdateUIGraphDetection(enable);
                }
            }
            chromatogramsMenuItem.Enabled = enable;
            calibrationCurveMenuItem.Enabled = enable;
            transitionsMenuItem.Enabled = enable;
            transformChromMenuItem.Enabled = enable;
            autoZoomMenuItem.Enabled = enable;
            arrangeGraphsToolStripMenuItem.Enabled = enable;
        }

        public void EnableGraphSpectrum(Action ensureLayoutLocked, SrmSettings settings, bool deserialized)
        {
            bool hasLibraries = settings.PeptideSettings.Libraries.HasLibraries;
            bool enable = hasLibraries || KoinaHelpers.KoinaSettingsValid;
            if (enable)
            {
                UpdateIonTypeMenu();
                UpdateChargesMenu();
            }

            bool enableChanging = libraryMatchToolStripMenuItem.Enabled != enable;
            if (enableChanging)
            {
                libraryMatchToolStripMenuItem.Enabled = enable;
                ionTypesMenuItem.Enabled = enable;
                specialIonsMenuItem.Enabled = enable;
                precursorIonMenuItem.Enabled = enable;
                chargesMenuItem.Enabled = enable;
                ranksMenuItem.Enabled = enable;
            }

            // Make sure we don't keep a spectrum graph around because it was
            // persisted when Koina settings were on and they no longer are
            if ((enableChanging && !deserialized) || (deserialized && !hasLibraries && !enable))
            {
                ensureLayoutLocked();
                SkylineWindow.ShowGraphSpectrum(enable && Settings.Default.ShowSpectra);
            }
        }
        private void qcMenuItem_Click(object sender, EventArgs e)
        {
            var qcTraceItem = sender as ToolStripMenuItem;
            if (qcTraceItem == null)
                throw new InvalidOperationException(@"qcMenuItem_Click must be triggered by a ToolStripMenuItem");
            SkylineWindow.ShowQc(qcTraceItem.Text);
        }

        private void candidatePeaksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowCandidatePeaks();
        }

        private void viewTargetsMenuItem_click(object sender, EventArgs e)
        {
            ShowTargetsWindow();
        }

        private void spectrumGridMenuItem_Click(object sender, EventArgs e)
        {
            ShowSpectrumGridForm();
        }

        public void ShowSpectrumGridForm()
        {
            var spectrumGridForm = new SpectrumGridForm(SkylineWindow);
            spectrumGridForm.Show(SkylineWindow);
        }

        private void liveReportsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            // The "Spectrum Grid" menu item is only visible if the user was holding down shift
            spectrumGridMenuItem.Visible = 0 != (ModifierKeys & Keys.Shift);
        }
    }
}
