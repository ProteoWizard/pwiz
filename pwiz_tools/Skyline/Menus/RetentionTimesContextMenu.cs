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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Menus
{
    public partial class RetentionTimesContextMenu : ContextMenuControl
    {
        public RetentionTimesContextMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
        }

        private GraphSummary _graphSummary;

        public void BuildRTGraphMenu(GraphSummary graph, ToolStrip menuStrip, Point mousePt, RTGraphController controller)
        {
            _graphSummary = graph;

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            menuStrip.Items.Insert(iInsert++, timeGraphContextMenuItem);
            runToRunToolStripMenuItem.Enabled = SkylineWindow.IsRetentionTimeGraphTypeEnabled(GraphTypeSummary.run_to_run_regression);

            GraphTypeSummary graphType = graph.Type;
            if (graphType == GraphTypeSummary.score_to_run_regression || graphType == GraphTypeSummary.run_to_run_regression)
            {
                var runToRun = graphType == GraphTypeSummary.run_to_run_regression;
                menuStrip.Items.Insert(iInsert++, timePlotContextMenuItem);
                timeCorrelationContextMenuItem.Checked = RTGraphController.PlotType == PlotTypeRT.correlation;
                timeResidualsContextMenuItem.Checked = RTGraphController.PlotType == PlotTypeRT.residuals;

                menuStrip.Items.Insert(iInsert++,setRegressionMethodContextMenuItem);
                linearRegressionContextMenuItem.Checked = RTGraphController.RegressionMethod == RegressionMethodRT.linear;
                kernelDensityEstimationContextMenuItem.Checked = RTGraphController.RegressionMethod == RegressionMethodRT.kde;
                logRegressionContextMenuItem.Checked = RTGraphController.RegressionMethod == RegressionMethodRT.log;
                loessContextMenuItem.Checked = RTGraphController.RegressionMethod == RegressionMethodRT.loess;

                var showPointsTypeStandards = SkylineWindow.Document.GetRetentionTimeStandards().Any();
                var showPointsTypeDecoys = SkylineWindow.Document.PeptideGroups.Any(nodePepGroup => nodePepGroup.Children.Cast<PeptideDocNode>().Any(nodePep => nodePep.IsDecoy));
                var qvalues = SkylineWindow.Document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained;
                if (showPointsTypeStandards || showPointsTypeDecoys || qvalues)
                {
                    menuStrip.Items.Insert(iInsert++, timePointsContextMenuItem);
                    targetsAt1FDRToolStripMenuItem.Visible =
                        SkylineWindow.Document.Settings.HasResults &&
                        SkylineWindow.Document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained;
                    timeStandardsContextMenuItem.Visible = showPointsTypeStandards;
                    timeDecoysContextMenuItem.Visible = showPointsTypeDecoys;
                    timeTargetsContextMenuItem.Checked = RTGraphController.PointsType == PointsTypeRT.targets;
                    targetsAt1FDRToolStripMenuItem.Checked = RTGraphController.PointsType == PointsTypeRT.targets_fdr;
                    timeStandardsContextMenuItem.Checked = RTGraphController.PointsType == PointsTypeRT.standards;
                    timeDecoysContextMenuItem.Checked = RTGraphController.PointsType == PointsTypeRT.decoys;
                }

                refineRTContextMenuItem.Checked = set.RTRefinePeptides;
                //Grey out so user knows we cannot refine with current regression method
                refineRTContextMenuItem.Enabled = RTGraphController.CanDoRefinementForRegressionMethod;
                menuStrip.Items.Insert(iInsert++, refineRTContextMenuItem);
                if (!runToRun)
                {
                    predictionRTContextMenuItem.Checked = set.RTPredictorVisible;
                    menuStrip.Items.Insert(iInsert++, predictionRTContextMenuItem);
                    iInsert = SkylineWindow.AddReplicatesContextMenu(menuStrip, iInsert);
                }

                menuStrip.Items.Insert(iInsert++, setRTThresholdContextMenuItem);
                if (!runToRun)
                {
                    menuStrip.Items.Insert(iInsert++, toolStripSeparator22);
                    menuStrip.Items.Insert(iInsert++, createRTRegressionContextMenuItem);
                    menuStrip.Items.Insert(iInsert++, chooseCalculatorContextMenuItem);
                }
                var regressionRT = controller.RegressionRefined;
                createRTRegressionContextMenuItem.Enabled = (regressionRT != null) && !runToRun;
                updateCalculatorContextMenuItem.Visible = (regressionRT != null &&
                    Settings.Default.RTScoreCalculatorList.CanEditItem(regressionRT.Calculator) && !runToRun);
                bool showDelete = controller.ShowDelete(mousePt);
                bool showDeleteOutliers = controller.ShowDeleteOutliers;
                if (showDelete || showDeleteOutliers)
                {
                    menuStrip.Items.Insert(iInsert++, toolStripSeparator23);
                    if (showDelete)
                        menuStrip.Items.Insert(iInsert++, removeRTContextMenuItem);
                    if (showDeleteOutliers)
                        menuStrip.Items.Insert(iInsert++, removeRTOutliersContextMenuItem);
                }
            }
            else if (graphType == GraphTypeSummary.schedule)
            {
                menuStrip.Items.Insert(iInsert++, toolStripSeparator38);
                menuStrip.Items.Insert(iInsert++, timePropsContextMenuItem);
            }
            else
            {
                menuStrip.Items.Insert(iInsert++, new ToolStripSeparator());
                menuStrip.Items.Insert(iInsert++, rtValueMenuItem);
                SkylineWindow.AddTransitionContextMenu(menuStrip, iInsert++);
                if (graphType == GraphTypeSummary.replicate)
                {
                    iInsert = SkylineWindow.AddReplicateOrderAndGroupByMenuItems(menuStrip, iInsert);
                    var rtReplicateGraphPane = graph.GraphPanes.FirstOrDefault() as RTReplicateGraphPane;
                    if (rtReplicateGraphPane != null && rtReplicateGraphPane.CanShowRTLegend)
                    {
                        showRTLegendContextMenuItem.Checked = set.ShowRetentionTimesLegend;
                        menuStrip.Items.Insert(iInsert++, showRTLegendContextMenuItem);
                    }
                    if (rtReplicateGraphPane != null)
                    {
                        ChromFileInfoId chromFileInfoId = null;
                        if (SkylineWindow.DocumentUI.Settings.HasResults)
                        {
                            var chromatogramSet = SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms[SkylineWindow.SelectedResultsIndex];
                            if (chromatogramSet.MSDataFileInfos.Count == 1)
                            {
                                chromFileInfoId = chromatogramSet.MSDataFileInfos[0].FileId;
                            }
                        }
                        iInsert = InsertAlignmentMenuItems(menuStrip.Items, chromFileInfoId, iInsert);
                    }
                }
                else if (graphType == GraphTypeSummary.peptide)
                {
                    SkylineWindow.AddPeptideOrderContextMenu(menuStrip, iInsert++);
                    iInsert = SkylineWindow.AddReplicatesContextMenu(menuStrip, iInsert);
                    SkylineWindow.AddScopeContextMenu(menuStrip, iInsert++);
                    InsertAlignmentMenuItems(menuStrip.Items, null, iInsert);
                }
                if (graphType == GraphTypeSummary.peptide ||  graphType == GraphTypeSummary.abundance || null != SummaryReplicateGraphPane.GroupByReplicateAnnotation)
                {
                    menuStrip.Items.Insert(iInsert++, SkylineWindow.peptideCvsContextMenuItem);
                    SkylineWindow.peptideCvsContextMenuItem.Checked = set.ShowPeptideCV;
                }
                SkylineWindow.selectionContextMenuItem.Checked = set.ShowReplicateSelection;
                menuStrip.Items.Insert(iInsert++, SkylineWindow.selectionContextMenuItem);
                SkylineWindow.synchronizeSummaryZoomingContextMenuItem.Checked = set.SynchronizeSummaryZooming;
                menuStrip.Items.Insert(iInsert++, SkylineWindow.synchronizeSummaryZoomingContextMenuItem);
                menuStrip.Items.Insert(iInsert++, toolStripSeparator38);
                menuStrip.Items.Insert(iInsert++, timePropsContextMenuItem);

                var isotopeLabelType = graph.GraphPaneFromPoint(mousePt) != null
                    ? graph.GraphPaneFromPoint(mousePt).PaneKey.IsotopeLabelType
                    : null;
                using var chromatogramContextMenu = new ChromatogramContextMenu(SkylineWindow);
                chromatogramContextMenu.AddApplyRemovePeak(menuStrip, isotopeLabelType, -1, ref iInsert);
            }

            menuStrip.Items.Insert(iInsert, new ToolStripSeparator());
        }

        /// <summary>
        /// If the predicted retention time is auto calculated, add a "Show {Prediction} score" menu item.
        /// If there are retention time alignments available for the specified chromFileInfoId, then adds
        /// a "Align Times To {Specified File}" menu item to a context menu.
        /// </summary>
        private int InsertAlignmentMenuItems(ToolStripItemCollection items, ChromFileInfoId chromFileInfoId, int iInsert)
        {
            var predictRT = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime;
            if (predictRT != null && predictRT.IsAutoCalculated)
            {
                var menuItem = new ToolStripMenuItem(
                    string.Format(Resources.SkylineWindow_ShowCalculatorScoreFormat, predictRT.Calculator.Name), null,
                    (sender, eventArgs) => SkylineWindow.AlignToRtPrediction = !SkylineWindow.AlignToRtPrediction)
                {
                    Checked = SkylineWindow.AlignToRtPrediction,
                };
                items.Insert(iInsert++, menuItem);
            }

            return iInsert;
        }

        public void SetupCalculatorChooser()
        {
            while (!ReferenceEquals(chooseCalculatorContextMenuItem.DropDownItems[0], toolStripSeparatorCalculators))
                chooseCalculatorContextMenuItem.DropDownItems.RemoveAt(0);

            //If no calculator has been picked for use in the graph, get the best one.
            var autoItem = new ToolStripMenuItem(SkylineResources.SkylineWindow_SetupCalculatorChooser_Auto, null,
                delegate { SkylineWindow.ChooseCalculator(string.Empty); })
            {
                Checked = string.IsNullOrEmpty(Settings.Default.RTCalculatorName)
            };
            chooseCalculatorContextMenuItem.DropDownItems.Insert(0, autoItem);

            int i = 0;
            var document = SkylineWindow.DocumentUI;
            foreach (var optionVariable in RtCalculatorOption.GetOptions(document))
            {
                var option = optionVariable;
                var menuItem = new ToolStripMenuItem(option.DisplayName, null, delegate { SkylineWindow.ChooseCalculator(option); })
                {
                    Checked = Equals(option, Settings.Default.RtCalculatorOption)
                };
                chooseCalculatorContextMenuItem.DropDownItems.Insert(i++, menuItem);
            }
        }

        #region Event Handlers

        private void timeGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.RTGraphTypes;
            bool runToRunRegression = SkylineWindow.GraphChecked(SkylineWindow.ListGraphRetentionTime, types, GraphTypeSummary.run_to_run_regression);
            bool scoreToRunRegression = SkylineWindow.GraphChecked(SkylineWindow.ListGraphRetentionTime, types, GraphTypeSummary.score_to_run_regression);

            runToRunToolStripMenuItem.Checked = runToRunRegression;
            scoreToRunToolStripMenuItem.Checked = scoreToRunRegression;
            regressionContextMenuItem.Checked = runToRunRegression || scoreToRunRegression;

            replicateComparisonContextMenuItem.Checked = SkylineWindow.GraphChecked(SkylineWindow.ListGraphRetentionTime, types, GraphTypeSummary.replicate);
            timePeptideComparisonContextMenuItem.Checked = SkylineWindow.GraphChecked(SkylineWindow.ListGraphRetentionTime, types, GraphTypeSummary.peptide);
            schedulingContextMenuItem.Checked = SkylineWindow.GraphChecked(SkylineWindow.ListGraphRetentionTime, types, GraphTypeSummary.schedule);
        }

        private void regressionMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTRegressionGraphScoreToRun();
        }

        private void fullReplicateComparisonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTRegressionGraphRunToRun();
        }

        private void linearRegressionContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRegressionMethod(RegressionMethodRT.linear);
        }

        private void kernelDensityEstimationContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRegressionMethod(RegressionMethodRT.kde);
        }

        private void logRegressionContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRegressionMethod(RegressionMethodRT.log);
        }

        private void loessContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRegressionMethod(RegressionMethodRT.loess);
        }

        private void timeCorrelationContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPlotType(PlotTypeRT.correlation);
        }

        private void timeResidualsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPlotType(PlotTypeRT.residuals);
        }

        private void timeTargetsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPointsType(PointsTypeRT.targets);
        }

        private void targetsAt1FDRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RTLinearRegressionGraphPane.ShowReplicate != ReplicateDisplay.single &&
                RTGraphController.GraphType == GraphTypeSummary.score_to_run_regression)
            {
                using (var dlg = new MultiButtonMsgDlg(
                    SkylineResources.SkylineWindow_targetsAt1FDRToolStripMenuItem_Click_Showing_targets_at_1__FDR_will_set_the_replicate_display_type_to_single__Do_you_want_to_continue_,
                    MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                {
                    if (dlg.ShowDialog(SkylineWindow) != DialogResult.Yes)
                        return;
                }
            }

            SkylineWindow.ShowSingleReplicate();
            SkylineWindow.ShowPointsType(PointsTypeRT.targets_fdr);
        }

        private void timeStandardsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPointsType(PointsTypeRT.standards);
        }

        private void timeDecoysContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPointsType(PointsTypeRT.decoys);
        }

        private void timePeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTPeptideGraph();
        }

        private void showRTLegendContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTLegend(!Settings.Default.ShowRetentionTimesLegend);
        }

        private void replicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTReplicateGraph();
        }

        private void schedulingMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTSchedulingGraph();
        }

        private void refineRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.RTRefinePeptides = refineRTContextMenuItem.Checked;
            SkylineWindow.UpdateRetentionTimeGraph();
        }

        private void predictionRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.RTPredictorVisible = predictionRTContextMenuItem.Checked;
            SkylineWindow.UpdateRetentionTimeGraph();
        }

        private void setRTThresholdContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRegressionRTThresholdDlg();
        }

        private void createRTRegressionContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.CreateRegression();
        }

        private void chooseCalculatorContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SetupCalculatorChooser();
        }

        private void addCalculatorContextMenuItem_Click(object sender, EventArgs e)
        {
            var list = Settings.Default.RTScoreCalculatorList;
            var calcNew = list.EditItem(SkylineWindow, null, list, null);
            if (calcNew != null)
                list.SetValue(calcNew);
        }

        private void updateCalculatorContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowEditCalculatorDlg();
        }

        private void removeRTOutliersContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.RemoveRTOutliers();
        }

        private void removeRTContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.EditDelete();
        }

        private void peptideRTValueMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            RTPeptideValue rtValue = RTPeptideGraphPane.RTValue;
            allRTValueContextMenuItem.Checked = (rtValue == RTPeptideValue.All);
            timeRTValueContextMenuItem.Checked = (rtValue == RTPeptideValue.Retention);
            fwhmRTValueContextMenuItem.Checked = (rtValue == RTPeptideValue.FWHM);
            fwbRTValueContextMenuItem.Checked = (rtValue == RTPeptideValue.FWB);
        }

        private void allRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            // No CVs with all retention time values showing
            Settings.Default.ShowPeptideCV = false;
            SkylineWindow.ShowRTPeptideValue(RTPeptideValue.All);
        }

        private void timeRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTPeptideValue(RTPeptideValue.Retention);
        }

        private void fwhmRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTPeptideValue(RTPeptideValue.FWHM);
        }

        private void fwbRTValueContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTPeptideValue(RTPeptideValue.FWB);
        }

        private void timePropsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowRTPropertyDlg(_graphSummary);
        }

        #endregion
    }
}
