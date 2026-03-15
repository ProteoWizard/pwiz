/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Menus
{
    public partial class PeakAreasContextMenu : ContextMenuControl
    {
        private GraphSummary _graphSummary;
        public PeakAreasContextMenu(SkylineWindow skylineWindow, GraphSummary graphSummary) : base(skylineWindow)
        {
            InitializeComponent();
            _graphSummary = graphSummary;
        }


        public void BuildAreaGraphMenu(ToolStrip menuStrip, Point mousePt)
        {
            var graphSummary = _graphSummary;
            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            menuStrip.Items.Insert(iInsert++, areaGraphContextMenuItem);
            var graphType = _graphSummary.Type;
            if (graphType == GraphTypeSummary.replicate)
            {
                menuStrip.Items.Insert(iInsert++, graphTypeToolStripMenuItem);
            }

            menuStrip.Items.Insert(iInsert++, new ToolStripSeparator());

            var isHistogram = graphType == GraphTypeSummary.histogram || graphType == GraphTypeSummary.histogram2d;

            if (isHistogram)
            {
                SkylineWindow.EditMenu.AddGroupByMenuItems(menuStrip, SkylineWindow.ReplicateGroupByContextMenuItem, SkylineWindow.SetAreaCVGroup, true, AreaGraphController.GroupByGroup, ref iInsert);
            }
            else if (graphType != GraphTypeSummary.abundance)
            {
                AddTransitionContextMenu(menuStrip, iInsert++);
            }
            else
            {
                // Protein level comparisons are likely not meaningful for small molecule documents,
                // so only offer peptide level comparison.
                // CONSIDER support protein level in mixed cases?
                if (!SkylineWindow.IsSmallMoleculeOrMixedUI)
                {
                    AddTargetsContextMenu(menuStrip, iInsert++);
                }
                AddExcludeTargetsContextMenu(menuStrip, iInsert++);
            }
            if (graphType == GraphTypeSummary.replicate)
            {
                iInsert = SkylineWindow.AddReplicateOrderAndGroupByMenuItems(menuStrip, iInsert);
                var normalizeOptions = new List<NormalizeOption>();
                normalizeOptions.Add(NormalizeOption.DEFAULT);
                normalizeOptions.AddRange(NormalizeOption.AvailableNormalizeOptions(DocumentUI));
                normalizeOptions.Add(NormalizeOption.MAXIMUM);
                normalizeOptions.Add(NormalizeOption.TOTAL);
                normalizeOptions.Add(null); // separator
                normalizeOptions.Add(NormalizeOption.NONE);
                areaNormalizeContextMenuItem.DropDownItems.Clear();
                areaNormalizeContextMenuItem.DropDownItems.AddRange(MakeNormalizeToMenuItems(normalizeOptions, AreaGraphController.AreaNormalizeOption.Constrain(DocumentUI.Settings)).ToArray());
                menuStrip.Items.Insert(iInsert++, areaNormalizeContextMenuItem);
                AreaReplicateGraphPane areaReplicateGraphPane;
                if (graphSummary.GraphControl.MasterPane.PaneList.Count == 1)
                    areaReplicateGraphPane = (AreaReplicateGraphPane)graphSummary.GraphControl.MasterPane.PaneList[0];
                else
                    areaReplicateGraphPane = (AreaReplicateGraphPane)graphSummary.GraphControl.MasterPane.FindPane(mousePt);

                if (areaReplicateGraphPane != null)
                {
                    // If the area replicate graph is being displayed and it shows a legend,
                    // display the "Legend" option
                    if (areaReplicateGraphPane.CanShowPeakAreaLegend)
                    {
                        showPeakAreaLegendContextMenuItem.Checked = set.ShowPeakAreaLegend;
                        menuStrip.Items.Insert(iInsert++, showPeakAreaLegendContextMenuItem);
                    }

                    // If the area replicate graph is being displayed and it can show a library,
                    // display the "Show Library" option
                    var expectedVisible = areaReplicateGraphPane.ExpectedVisible;
                    if (expectedVisible.CanShowExpected())
                    {
                        showLibraryPeakAreaContextMenuItem.Checked = set.ShowLibraryPeakArea;
                        showLibraryPeakAreaContextMenuItem.Text = expectedVisible == AreaExpectedValue.library
                                                                      ? SkylineResources.SkylineWindow_BuildAreaGraphMenu_Show_Library
                                                                      : SkylineResources.SkylineWindow_BuildAreaGraphMenu_Show_Expected;
                        menuStrip.Items.Insert(iInsert++, showLibraryPeakAreaContextMenuItem);
                    }

                    // If the area replicate graph is being displayed and it can show dot products,
                    // display the "Show Dot Product" option
                    if (areaReplicateGraphPane.CanShowDotProduct)
                    {
                        showDotProductToolStripMenuItem.DropDownItems.Clear();
                        var optionsList = DotProductDisplayOptionExtension.ListAll();
                        if(areaReplicateGraphPane.IsLineGraph)
                            optionsList = new[]{ DotProductDisplayOption.none, DotProductDisplayOption.line};
                        showDotProductToolStripMenuItem.DropDownItems.AddRange(optionsList.Select(MakeShowDotpMenuItem).ToArray());
                        menuStrip.Items.Insert(iInsert++, showDotProductToolStripMenuItem);
                    }
                }
            }
            else if (graphType == GraphTypeSummary.peptide)
            {
                SkylineWindow.AddPeptideOrderContextMenu(menuStrip, iInsert++);
                SkylineWindow.AddScopeContextMenu(menuStrip, iInsert++);
            }
            if (graphType == GraphTypeSummary.abundance || graphType == GraphTypeSummary.peptide)
            {
                iInsert = SkylineWindow.AddReplicatesContextMenu(menuStrip, iInsert);
            }

            if (isHistogram)
            {
                bool trained = Document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained;
                bool decoys = Document.Settings.PeptideSettings.Integration.PeakScoringModel.UsesDecoys;

                if (trained || decoys)
                {
                    UpdateAreaPointsTypeMenuItems();

                    menuStrip.Items.Insert(iInsert++, pointsToolStripMenuItem);
                }

                UpdateAreaCVTransitionsMenuItems();

                var maxTransCount = Document.MoleculeTransitionGroups
                    .Select(g => g.TransitionCount).Append(0).Max();
                for (int i = 1; i <= maxTransCount; i++)
                {
                    var tmp = new ToolStripMenuItem(i.ToString(), null,
                        areaCVCountTransitionsToolStripMenuItem_Click)
                    {
                        Checked = AreaGraphController.AreaCVTransitionsCount == i
                    };
                    areaCVCountTransitionsToolStripMenuItem.DropDownItems.Add(tmp);
                }

                menuStrip.Items.Insert(iInsert++, areaCVTransitionsToolStripMenuItem);


                UpdateAreaBinWidthMenuItems();
                menuStrip.Items.Insert(iInsert++, areaCVbinWidthToolStripMenuItem);
                var normalizeOptions = new List<NormalizeOption>();
                normalizeOptions.Add(NormalizeOption.DEFAULT);
                normalizeOptions.AddRange(NormalizeOption.AvailableNormalizeOptions(DocumentUI));
                normalizeOptions.Add(null); // separator
                normalizeOptions.Add(NormalizeOption.NONE);

                areaCVNormalizedToToolStripMenuItem.DropDownItems.Clear();
                areaCVNormalizedToToolStripMenuItem.DropDownItems.AddRange(MakeNormalizeToMenuItems(normalizeOptions, AreaGraphController.AreaCVNormalizeOption.Constrain(DocumentUI.Settings)).ToArray());
                menuStrip.Items.Insert(iInsert++, areaCVNormalizedToToolStripMenuItem);

                if (graphType == GraphTypeSummary.histogram2d)
                {
                    areaCVLogScaleToolStripMenuItem.Checked = Settings.Default.AreaCVLogScale;
                    menuStrip.Items.Insert(iInsert++, areaCVLogScaleToolStripMenuItem);
                }

                selectionContextMenuItem.Checked = set.ShowReplicateSelection;
                menuStrip.Items.Insert(iInsert++, selectionContextMenuItem);

                menuStrip.Items.Insert(iInsert++, new ToolStripSeparator());
                menuStrip.Items.Insert(iInsert++, removeAboveCVCutoffToolStripMenuItem);
            }
            else
            {
                if (graphType == GraphTypeSummary.peptide || graphType == GraphTypeSummary.abundance || !string.IsNullOrEmpty(Settings.Default.GroupByReplicateAnnotation))
                {
                    menuStrip.Items.Insert(iInsert++, peptideCvsContextMenuItem);
                    peptideCvsContextMenuItem.Checked = set.ShowPeptideCV;
                }

                if (graphType != GraphTypeSummary.abundance)
                {
                    menuStrip.Items.Insert(iInsert++, peptideLogScaleContextMenuItem);
                    peptideLogScaleContextMenuItem.Checked = set.AreaLogScale;
                }
                else
                {
                    menuStrip.Items.Insert(iInsert++, relativeAbundanceLogScaleContextMenuItem);
                    relativeAbundanceLogScaleContextMenuItem.Checked = set.RelativeAbundanceLogScale;
                }
                selectionContextMenuItem.Checked = set.ShowReplicateSelection;
                menuStrip.Items.Insert(iInsert++, selectionContextMenuItem);
                if (graphType == GraphTypeSummary.abundance)
                {
                    menuStrip.Items.Insert(iInsert++, new ToolStripMenuItem(GraphsResources.FoldChangeVolcanoPlot_BuildContextMenu_Auto_Arrange_Labels, null, OnLabelOverlapClick)
                    {
                        Checked = Settings.Default.GroupComparisonAvoidLabelOverlap
                    });
                    if (Settings.Default.GroupComparisonAvoidLabelOverlap &&
                        graphSummary.GraphPaneFromPoint(mousePt) is SummaryRelativeAbundanceGraphPane abundancePane)
                    {
                        var suspendResumeText = Settings.Default.GroupComparisonSuspendLabelLayout
                            ? GraphsResources.FoldChangeVolcanoPlot_BuildContextMenu_RestartLabelLayout
                            : GraphsResources.FoldChangeVolcanoPlot_BuildContextMenu_PauseLabelLayout;
                        menuStrip.Items.Insert(iInsert++, new ToolStripMenuItem(suspendResumeText, null, abundancePane.OnSuspendLayout));
                    }
                }
                else
                {
                    synchronizeSummaryZoomingContextMenuItem.Checked = set.SynchronizeSummaryZooming;
                    menuStrip.Items.Insert(iInsert++, synchronizeSummaryZoomingContextMenuItem);
                }
            }

            menuStrip.Items.Insert(iInsert++, new ToolStripSeparator());
            if(graphType != GraphTypeSummary.abundance)
                menuStrip.Items.Insert(iInsert++, areaPropsContextMenuItem);
            else
            {
                menuStrip.Items.Insert(iInsert++, relativeAbundanceFormattingMenuItem);
            }
            menuStrip.Items.Insert(iInsert, new ToolStripSeparator());

            if (!isHistogram && graphType != GraphTypeSummary.abundance)
            {
                var isotopeLabelType = graphSummary.GraphPaneFromPoint(mousePt) != null
                    ? graphSummary.GraphPaneFromPoint(mousePt).PaneKey.IsotopeLabelType
                    : null;
                using var chromatogramContextMenu = new ChromatogramContextMenu(SkylineWindow);
                chromatogramContextMenu.AddApplyRemovePeak(menuStrip, isotopeLabelType, -1, ref iInsert);
            }
        }

        private void AddTargetsContextMenu(ToolStrip menuStrip, int iInsert)
        {
            abundanceTargetsPeptidesMenuItem.Checked = !Settings.Default.AreaProteinTargets;
            abundanceTargetsProteinsMenuItem.Checked = Settings.Default.AreaProteinTargets;
            menuStrip.Items.Insert(iInsert, abundanceTargetsMenuItem);
        }

        private void AddExcludeTargetsContextMenu(ToolStrip menuStrip, int iInsert)
        {
            excludeTargetsStandardsMenuItem.Checked = Settings.Default.ExcludeStandardsFromAbundanceGraph;
            excludeTargetsPeptideListMenuItem.Checked = Settings.Default.ExcludePeptideListsFromAbundanceGraph;
            excludeTargetsPeptideListMenuItem.Visible = !SkylineWindow.IsSmallMoleculeOrMixedUI;
            menuStrip.Items.Insert(iInsert, excludeTargetsMenuItem);
        }

        private ToolStripItem MakeNormalizeToMenuItem(NormalizeOption normalizeOption, bool isChecked)
        {
            if (normalizeOption == null)
            {
                return new ToolStripSeparator();
            }

            string caption = normalizeOption.Caption;
            if (normalizeOption == NormalizeOption.DEFAULT)
            {
                var selectedNormalizationMethods =
                    NormalizationMethod.GetMoleculeNormalizationMethods(DocumentUI, SequenceTree.SelectedPaths);
                if (selectedNormalizationMethods.Count == 1)
                {
                    caption = string.Format(QuantificationStrings.SkylineWindow_MakeNormalizeToMenuItem_Default___0__, selectedNormalizationMethods.First().NormalizeToCaption);
                }
            }

            return new ToolStripMenuItem(caption, null, NormalizeMenuItemOnClick)
            {
                Tag = normalizeOption,
                Checked = isChecked,
            };
        }

        private ToolStripItem MakeShowDotpMenuItem(DotProductDisplayOption displayOption)
        {
            return new ToolStripMenuItem(displayOption.GetLocalizedString(), null, DotpDisplayOptionMenuItemOnClick)
            {
                Checked = displayOption.IsSet(Settings.Default), Tag = displayOption
            };
        }

        private void DotpDisplayOptionMenuItemOnClick(object sender, EventArgs eventArgs)
        {
            var displayOption = (DotProductDisplayOption)((ToolStripMenuItem)sender).Tag;
            Settings.Default.PeakAreaDotpDisplay = displayOption.ToString();
            SkylineWindow.UpdateSummaryGraphs();
        }

        private IEnumerable<ToolStripItem> MakeNormalizeToMenuItems(IEnumerable<NormalizeOption> normalizeOptions,
            NormalizeOption selectedOption)
        {
            return normalizeOptions.Select(option=>MakeNormalizeToMenuItem(option, option == selectedOption));
        }

        private void NormalizeMenuItemOnClick(object sender, EventArgs eventArgs)
        {
            var normalizeOption = (NormalizeOption) ((ToolStripMenuItem) sender).Tag;
            SkylineWindow.NormalizeAreaGraphTo(normalizeOption);
        }

        private void UpdateAreaCVTransitionsMenuItems()
        {
            areaCVAllTransitionsToolStripMenuItem.Checked = AreaGraphController.AreaCVTransitions == AreaCVTransitions.all;
            areaCVBestTransitionsToolStripMenuItem.Checked = AreaGraphController.AreaCVTransitions == AreaCVTransitions.best;
            var selectedCount = AreaGraphController.AreaCVTransitionsCount;
            for (int i = 0; i < areaCVCountTransitionsToolStripMenuItem.DropDownItems.Count; i++)
            {
                ((ToolStripMenuItem)areaCVCountTransitionsToolStripMenuItem.DropDownItems[i]).Checked =
                    selectedCount - 1 == i;
            }
            areaCVPrecursorsToolStripMenuItem.Checked = AreaGraphController.AreaCVMsLevel == AreaCVMsLevel.precursors;
            areaCVProductsToolStripMenuItem.Checked = AreaGraphController.AreaCVMsLevel == AreaCVMsLevel.products;
        }

        private void UpdateAreaBinWidthMenuItems()
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            var unit = Settings.Default.AreaCVShowDecimals ? string.Empty : @"%";

            areaCV05binWidthToolStripMenuItem.Text = 0.5 / factor + unit;
            areaCV10binWidthToolStripMenuItem.Text = 1.0 / factor + unit;
            areaCV15binWidthToolStripMenuItem.Text = 1.5 / factor + unit;
            areaCV20binWidthToolStripMenuItem.Text = 2.0 / factor + unit;

            var binwidth = Settings.Default.AreaCVHistogramBinWidth;
            areaCV05binWidthToolStripMenuItem.Checked = binwidth == 0.5 / factor;
            areaCV10binWidthToolStripMenuItem.Checked = binwidth == 1.0 / factor;
            areaCV15binWidthToolStripMenuItem.Checked = binwidth == 1.5 / factor;
            areaCV20binWidthToolStripMenuItem.Checked = binwidth == 2.0 / factor;
        }

        private void UpdateAreaPointsTypeMenuItems()
        {
            var pointsType = AreaGraphController.PointsType;
            var shouldUseQValues = AreaGraphController.ShouldUseQValues(Document);
            var decoys = Document.Settings.PeptideSettings.Integration.PeakScoringModel.UsesDecoys;

            if (!decoys && pointsType == PointsTypePeakArea.decoys)
            {
                pointsType = AreaGraphController.PointsType = PointsTypePeakArea.targets;
            }

            areaCVtargetsToolStripMenuItem.Checked = pointsType == PointsTypePeakArea.targets;
            areaCVtargetsToolStripMenuItem.Text = shouldUseQValues ? string.Format(SkylineResources.SkylineWindow_UpdateAreaPointsTypeMenuItems_Targets_at__0___FDR, Settings.Default.AreaCVQValueCutoff * 100.0) : SkylineResources.SkylineWindow_UpdateAreaPointsTypeMenuItems_Targets;
            areaCVdecoysToolStripMenuItem.Visible = decoys;
            areaCVdecoysToolStripMenuItem.Checked = pointsType == PointsTypePeakArea.decoys;
        }

        private void OnLabelOverlapClick(object o, EventArgs eventArgs)
        {
            Settings.Default.GroupComparisonAvoidLabelOverlap = !Settings.Default.GroupComparisonAvoidLabelOverlap;
        }

        private void areaGraphMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.AreaGraphTypes;
            var list = SkylineWindow.ListGraphPeakArea;
            areaReplicateComparisonContextMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.replicate);
            areaPeptideComparisonContextMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.peptide);
            areaRelativeAbundanceContextMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.abundance);
            areaCVHistogramContextMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.histogram);
            areaCVHistogram2DContextMenuItem.Checked = SkylineWindow.GraphChecked(list, types, GraphTypeSummary.histogram2d);
        }

        private void areaReplicateComparisonMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.ShowPeakAreaReplicateComparison();

        private void areaPeptideComparisonMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.ShowPeakAreaPeptideGraph();

        private void areaRelativeAbundanceMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.ShowPeakAreaRelativeAbundanceGraph();

        private void areaCVHistogramToolStripMenuItem1_Click(object sender, EventArgs e)
            => SkylineWindow.ShowPeakAreaCVHistogram();

        private void areaCVHistogram2DToolStripMenuItem1_Click(object sender, EventArgs e)
            => SkylineWindow.ShowPeakAreaCVHistogram2D();

        private void barAreaGraphTypeMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaGraphDisplayType(AreaGraphDisplayType.bars);

        private void lineAreaGraphTypeMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaGraphDisplayType(AreaGraphDisplayType.lines);

        private void showLibraryPeakAreaContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowLibraryPeakArea = !Settings.Default.ShowLibraryPeakArea;
            SkylineWindow.UpdateSummaryGraphs();
        }

        private void showPeakAreaLegendContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.ShowPeakAreaLegend(!Settings.Default.ShowPeakAreaLegend);

        private void peptideLogScaleContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.ShowPeptideLogScale(peptideLogScaleContextMenuItem.Checked);

        private void relativeAbundanceLogScaleContextMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.ShowRelativeAbundanceLogScale(relativeAbundanceLogScaleContextMenuItem.Checked);

        private void areaPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            switch (_graphSummary.Type)
            {
                case GraphTypeSummary.abundance:
                case GraphTypeSummary.replicate:
                case GraphTypeSummary.peptide:
                    SkylineWindow.ShowAreaPropertyDlg();
                    break;
                case GraphTypeSummary.histogram:
                case GraphTypeSummary.histogram2d:
                    SkylineWindow.ShowAreaCVPropertyDlg(_graphSummary);
                    break;
            }
        }

        private void areaCVLogScaleToolStripMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.EnableAreaCVLogScale(!Settings.Default.AreaCVLogScale);

        private void areaCVAllTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaCVTransitions(AreaCVTransitions.all, -1);

        private void areaCVCountTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            int selectedIdx = ((ToolStripMenuItem)item.OwnerItem).DropDownItems.IndexOf(item) + 1;
            SkylineWindow.SetAreaCVTransitions(AreaCVTransitions.count, selectedIdx);
        }

        private void areaCVBestTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaCVTransitions(AreaCVTransitions.best, -1);

        private void areaCVPrecursorsToolStripMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaCVMsLevel(AreaCVMsLevel.precursors);

        private void areaCVProductsToolStripMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaCVMsLevel(AreaCVMsLevel.products);

        private void areaCVtargetsToolStripMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.targets);

        private void areaCVdecoysToolStripMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.decoys);

        private void areaCV05binWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            SkylineWindow.SetAreaCVBinWidth(0.5 / factor);
        }

        private void areaCV10binWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            SkylineWindow.SetAreaCVBinWidth(1.0 / factor);
        }

        private void areaCV15binWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            SkylineWindow.SetAreaCVBinWidth(1.5 / factor);
        }

        private void areaCV20binWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var factor = AreaGraphController.GetAreaCVFactorToPercentage();
            SkylineWindow.SetAreaCVBinWidth(2.0 / factor);
        }

        private void removeAboveCVCutoffToolStripMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.RemoveAboveCVCutoff(_graphSummary);

        private void abundanceTargetsProteinsMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaProteinTargets(true);

        private void abundanceTargetsPeptidesMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetAreaProteinTargets(false);

        private void excludeTargetsPeptideListMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.SetExcludePeptideListsFromAbundanceGraph(!Settings.Default.ExcludePeptideListsFromAbundanceGraph);

        private void relativeAbundanceFormattingMenuItem_Click(object sender, EventArgs e)
            => SkylineWindow.ShowRelativeAbundanceFormatting();

        private void excludeTargetsStandardsMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ExcludeStandardsFromAbundanceGraph = !Settings.Default.ExcludeStandardsFromAbundanceGraph;
            SkylineWindow.UpdateSummaryGraphs();
        }
    }
}
