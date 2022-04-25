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
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;
using Array = System.Array;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum AreaExpectedValue { none, library, isotope_dist, ratio_to_label }

    public static class AreaExpectedValueExtension
    {
        public static bool IsVisible(this AreaExpectedValue expectedValue)
        {
            return CanShowExpected(expectedValue) && Settings.Default.ShowLibraryPeakArea;
        }

        public static bool CanShowExpected(this AreaExpectedValue expectedValue)
        {
            return (expectedValue == AreaExpectedValue.isotope_dist ||
                    expectedValue == AreaExpectedValue.library);
        }

        public static string GetDotpLabel(this AreaExpectedValue expectedValue)
        {
            switch (expectedValue)
            {
                case AreaExpectedValue.library:
                    return @"dotp";
                case AreaExpectedValue.isotope_dist:
                    return @"idotp";
                case AreaExpectedValue.ratio_to_label:
                    return @"rdotp";
                default:
                    return string.Empty;
            }
        }

        public static float GetDotpValueCutoff(this AreaExpectedValue expectedValue, Settings set)
        {
            switch (expectedValue)
            {
                case AreaExpectedValue.library:
                    return set.PeakAreaDotpCutoffValue;
                case AreaExpectedValue.isotope_dist:
                    return set.PeakAreaIDotpCutoffValue;
                case AreaExpectedValue.ratio_to_label:
                    return set.PeakAreaRDotpCutoffValue;
                default:
                    return 0.9f;
            }
        }
        public static void SetDotpValueCutoff(this AreaExpectedValue expectedValue, Settings set, float val)
        {
            Assume.IsTrue(val >= 0 || val <= 1, string.Format(CultureInfo.CurrentCulture, Resources.AreaChartPropertyDlg_ValidateDotpRange__0__must_be_betwen_0_and_1, expectedValue.GetDotpLabel()));

            switch (expectedValue)
            {
                case AreaExpectedValue.library:
                    set.PeakAreaDotpCutoffValue = val;
                    break;
                case AreaExpectedValue.isotope_dist:
                    set.PeakAreaIDotpCutoffValue = val;
                    break;
                case AreaExpectedValue.ratio_to_label:
                    set.PeakAreaRDotpCutoffValue = val;
                    break;
            }
        }
    }
    public enum DotProductDisplayOption { none, label, line }

    public static class DotProductDisplayOptionExtension
    {
        public static string GetLocalizedString(this DotProductDisplayOption option)
        {
            switch (option)
            {
                case DotProductDisplayOption.label:
                    return Resources.DotpDisplayOption_label;
                case DotProductDisplayOption.line:
                    return Resources.DotpDisplayOption_line;
                case DotProductDisplayOption.none:
                    return Resources.DotpDisplayOption_None;
                default:
                    return "";
            }
        }

        public static DotProductDisplayOption ParseLocalizedString(string optionString)
        {
            if(Resources.DotpDisplayOption_label.Equals(optionString))
                return DotProductDisplayOption.label;
            else if(Resources.DotpDisplayOption_line.Equals(optionString))
                return DotProductDisplayOption.line;
            else 
                return DotProductDisplayOption.none;
        }

        public static bool IsSet(this DotProductDisplayOption displayOption, Settings set)
        {
            return displayOption.ToString().Equals(set.PeakAreaDotpDisplay);
        }

        public static DotProductDisplayOption GetCurrent(Settings set)
        {
            return ListAll().First(op => op.ToString().Equals(set.PeakAreaDotpDisplay));
        }

        public static IEnumerable<DotProductDisplayOption> ListAll()
        {
            return new[] {DotProductDisplayOption.none, DotProductDisplayOption.label, DotProductDisplayOption.line};
        }
    }

    /// <summary>
    /// Graph pane which shows the comparison of retention times across the replicates.
    /// </summary>
    public class AreaReplicateGraphPane : SummaryReplicateGraphPane
    {
        private int _labelHeight;
        private ImmutableList<float> _dotpData;
        public AreaReplicateGraphPane(GraphSummary graphSummary, PaneKey paneKey)
            : base(graphSummary)
        {
            PaneKey = paneKey;
            ToolTip = new ToolTipImplementation(this);
        }

        protected override void InitFromData(GraphData graphData)
        {
            base.InitFromData(graphData);
            if (ExpectedVisible.IsVisible())
            {
                // add an XAxis label of "Library" at the left most column
                string[] labels = OriginalXAxisLabels;
                string[] withLibLabel = new string[labels.Length + 1];
                withLibLabel[0] = ExpectedVisible == AreaExpectedValue.library ? 
                    Resources.AreaReplicateGraphPane_InitFromData_Library : 
                    Resources.AreaReplicateGraphPane_InitFromData_Expected;

                Array.Copy(labels, 0, withLibLabel, 1, labels.Length);
               
                XAxis.Scale.TextLabels = withLibLabel;
                ScaleAxisLabels();
            }
        }

        private static BarType BarType
        {
            get
            {
                if (AreaGraphController.AreaNormalizeOption.IsRatioToLabel)
                {
                    return BarType.Cluster;
                }

                if (AreaGraphController.AreaNormalizeOption == NormalizeOption.TOTAL)
                {
                    return BarType.PercentStack;
                }

                return BarType.Stack;
            }
        }

        public AreaExpectedValue ExpectedVisible { get; set; }

        protected override int FirstDataIndex
        {
            get { return ExpectedVisible.IsVisible() ? 1 : 0; }
        }

        public bool CanShowDotProduct { get; private set; }

        public bool DotProductLabelsVisible
        {
            get{return CanShowDotProduct && DotProductDisplayOption.label.IsSet(Settings.Default);}
        }

        public bool IsLineGraph
        {
            get { return CurveList.Count > 0 && CurveList.All(curve => curve.IsLine); }
        }

        public bool CanShowPeakAreaLegend { get; private set; }
        
        public IList<double> SumAreas { get; private set; }

        public TransitionGroupDocNode ParentGroupNode { get; private set; }

        private NormalizedValueCalculator NormalizedValueCalculator { get; set; }

        public override void Draw(Graphics g)
        {
            // Make sure changes are not only drawn when the graph is updated.
            if (DotProductLabelsVisible)
                AddDotProductLabels(g, ParentGroupNode, SumAreas);

            base.Draw(g);
        }

        protected override bool IsRedrawRequired(Graphics g)
        {
            if (base.IsRedrawRequired(g))
                return true;

            // Have to call AddDotProductLabels twice, since the X-scale may not be up
            // to date before calling Draw.  If nothing changes, this will be a no-op
            if (DotProductLabelsVisible)
            {
                int dotpLabelsCount = _dotpLabels.Count;
                AddDotProductLabels(g, ParentGroupNode, SumAreas);
                if (dotpLabelsCount != _dotpLabels.Count)
                    return true;
            }
            return false;
        }

        private PeptidesAndTransitionGroups GetSelectedPeptides()
        {
            return PeptidesAndTransitionGroups.Get(GraphSummary.StateProvider.SelectedNodes, GraphSummary.ResultsIndex, 100);
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            _dotpLabels = new GraphObjList();

            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            var results = document.Settings.MeasuredResults;
            NormalizedValueCalculator = new NormalizedValueCalculator(document);
            bool resultsAvailable = results != null;
            Clear();

            if (!resultsAvailable)
            {
                Title.Text = Resources.AreaReplicateGraphPane_UpdateGraph_No_results_available;
                EmptyGraph(document);
                return;
            }

            var peptidePaths = GetSelectedPeptides().GetUniquePeptidePaths().ToList();
            var pepCount = peptidePaths.Count;

            NormalizeOption normalizeOption = AreaGraphController.AreaNormalizeOption.Constrain(document.Settings);

            IsMultiSelect = pepCount > 1 ||
                            (pepCount == 1 &&
                             GraphSummary.StateProvider.SelectedNodes.FirstOrDefault() is PeptideGroupTreeNode);
            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (GraphSummary.StateProvider.SelectedNode is EmptyNode) // if EmptyNode selected
            {
                selectedTreeNode = GraphSummary.StateProvider.SelectedNodes.OfType<SrmTreeNode>().FirstOrDefault();
            }
            if (selectedTreeNode == null || document.FindNode(selectedTreeNode.Path) == null)
            {
                Title.Text = Helpers.PeptideToMoleculeTextMapper.Translate(Resources.AreaReplicateGraphPane_UpdateGraph_Select_a_peptide_to_see_the_peak_area_graph, document.DocumentType);
                EmptyGraph(document);
                return;
            }

            if (normalizeOption == NormalizeOption.CALIBRATED)
            {
                if (selectedTreeNode is TransitionTreeNode)
                {
                    selectedTreeNode = selectedTreeNode.SrmParent;
                }

                if (selectedTreeNode is TransitionGroupTreeNode)
                {
                    selectedTreeNode = selectedTreeNode.SrmParent;
                }
            }
            BarSettings.Type = BarType;
            if (IsMultiSelect)
                BarSettings.Type = BarType.Cluster;
            Title.Text = null;

            DisplayTypeChrom displayType;
            if (Equals(PaneKey, PaneKey.PRECURSORS))
            {
                displayType = DisplayTypeChrom.precursors;
            }
            else if (Equals(PaneKey, PaneKey.PRODUCTS))
            {
                displayType = DisplayTypeChrom.products;
            }
            else
            {
                displayType = GraphChromatogram.GetDisplayType(document, selectedTreeNode);
            }

            DocNode selectedNode = selectedTreeNode.Model;
            DocNode parentNode = selectedNode;
            IdentityPath identityPath = selectedTreeNode.Path;
            bool optimizationPresent = results.Chromatograms.Contains(
                chrom => chrom.OptimizationFunction != null);

            // If the selected tree node is a transition, then its siblings are displayed.
            if (selectedTreeNode is TransitionTreeNode)
            {
                if (displayType == DisplayTypeChrom.single)
                {
                    BarSettings.Type = BarType.Cluster;
                }
                else
                {
                    SrmTreeNode parentTreeNode = selectedTreeNode.SrmParent;
                    parentNode = parentTreeNode.Model;
                    identityPath = parentTreeNode.Path;
                }
            }
            // If the selected node is a peptide with one child, then show the children,
            // unless chromatogram display type is total
            else if (selectedTreeNode is PeptideTreeNode)
            {
                var children = ((PeptideDocNode) selectedNode).TransitionGroups
                    .Where(PaneKey.IncludesTransitionGroup)
                    .ToArray();
                if (children.Length == 1 && normalizeOption != NormalizeOption.CALIBRATED)
                {
                    selectedNode = parentNode = children[0];
                    identityPath = new IdentityPath(identityPath, parentNode.Id);
                }
                else
                {
                    BarSettings.Type = BarType.Cluster;
                }
            }
            else if (!(selectedTreeNode is TransitionGroupTreeNode || selectedTreeNode is PeptideGroupTreeNode))
            {
                Title.Text = Helpers.PeptideToMoleculeTextMapper.Translate(Resources.AreaReplicateGraphPane_UpdateGraph_Select_a_peptide_to_see_the_peak_area_graph, document.DocumentType);
                EmptyGraph(document);
                CanShowPeakAreaLegend = false;
                CanShowDotProduct = false;
                return;
            }

            var parentGroupNode = parentNode as TransitionGroupDocNode;
            
            // If a precursor is going to be displayed with display type single
            if (parentGroupNode != null && displayType == DisplayTypeChrom.single)
            {
                // If no optimization data, then show all the transitions
                if (!optimizationPresent)
                    displayType = DisplayTypeChrom.all;
                // Otherwise, do not stack the bars
                else
                    BarSettings.Type = BarType.Cluster;
            }

            // Sets normalizeData to optimization, maximum_stack, maximum, total, or none
            DataScalingOption dataScalingOption;
            if (optimizationPresent && displayType == DisplayTypeChrom.single &&
                normalizeOption == NormalizeOption.TOTAL)
            {
                dataScalingOption = DataScalingOption.optimization;
            }
            else if (normalizeOption == NormalizeOption.MAXIMUM)
            {
                dataScalingOption = BarSettings.Type == BarType.Stack
                                    ? DataScalingOption.maximum_stack
                                    : DataScalingOption.maximum;
            }
            else if (BarSettings.Type == BarType.PercentStack)
            {
                dataScalingOption = DataScalingOption.total;
            }
            else
            {
                dataScalingOption = DataScalingOption.none;
            }

            // Calculate graph data points
            // IsExpectedVisible depends on ExpectedVisible
            ExpectedVisible = AreaExpectedValue.none;
            if (parentGroupNode != null &&
                    displayType != DisplayTypeChrom.total &&
                    !normalizeOption.IsRatioToLabel &&
                    !(optimizationPresent && displayType == DisplayTypeChrom.single))
            {
                var displayTrans = GraphChromatogram.GetDisplayTransitions(parentGroupNode, displayType).ToArray();
                bool isShowingMs = displayTrans.Any(nodeTran => nodeTran.IsMs1);
                bool isShowingMsMs = displayTrans.Any(nodeTran => !nodeTran.IsMs1);
                bool isFullScanMs = document.Settings.TransitionSettings.FullScan.IsEnabledMs && isShowingMs;
                if (!IsMultiSelect)
                {
                    if (isFullScanMs)
                    {
                        if (!isShowingMsMs && parentGroupNode.HasIsotopeDist)
                            ExpectedVisible = AreaExpectedValue.isotope_dist;
                    }
                    else
                    {
                        if (parentGroupNode.HasLibInfo)
                            ExpectedVisible = AreaExpectedValue.library;
                    }
                }
            }
            if (normalizeOption.NormalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel && !IsMultiSelect)
            {
                var graphLabelType = parentGroupNode?.LabelType ?? PaneKey.IsotopeLabelType;
                if (!NormalizationMethod.RatioToLabel.Matches(ratioToLabel, graphLabelType))
                    ExpectedVisible = AreaExpectedValue.ratio_to_label;
            }

            var graphType = AreaGraphController.GraphDisplayType;
            var expectedValue = !DotProductDisplayOption.none.IsSet(Settings.Default) ? ExpectedVisible : AreaExpectedValue.none;
            if (ExpectedVisible.CanShowExpected())
                expectedValue = ExpectedVisible;

            var replicateGroupOp = ReplicateGroupOp.FromCurrentSettings(document);
            var graphData = IsMultiSelect  ? 
                new AreaGraphData(document,
                    peptidePaths,
                    displayType,
                    replicateGroupOp,
                    normalizeOption,
                    dataScalingOption,
                    expectedValue,
                    PaneKey) : 
                new AreaGraphData(document,
                    identityPath,
                    displayType,
                    replicateGroupOp,
                    normalizeOption,
                    dataScalingOption,
                    expectedValue,
                    PaneKey,
                    graphType == AreaGraphDisplayType.bars);

            var aggregateOp = replicateGroupOp.AggregateOp;
            // Avoid stacking CVs
            if (aggregateOp.Cv || aggregateOp.CvDecimal)
                BarSettings.Type = BarType.Cluster;

            int countNodes = graphData.DocNodes.Count;
            if (countNodes == 0)
                ExpectedVisible = AreaExpectedValue.none;
            CanShowDotProduct = ExpectedVisible != AreaExpectedValue.none;
            CanShowPeakAreaLegend = countNodes != 0;

            InitFromData(graphData);

            // Add data to the graph
            int selectedReplicateIndex = SelectedIndex;
            if (ExpectedVisible.IsVisible())
            {
                if (GraphSummary.ActiveLibrary)
                    selectedReplicateIndex = 0;
            }
            
            double maxArea = -double.MaxValue;
            double sumArea = 0;
     
            // An array to keep track of height of all bars to determine 
            // where each dot product annotation (if showing) should be placed
            var sumAreas = new double[graphData.ReplicateGroups.Count];

            // If only one bar to show, then use cluster instead of stack bar type, since nothing to stack
            // Important for mean error bar checking below
            if (BarSettings.Type == BarType.Stack && countNodes == 1 && graphData.PointPairLists[0].Count == 1)
                BarSettings.Type = BarType.Cluster;

            int colorOffset = 0;
            if(parentGroupNode != null)
            {
                // We want the product ion colors to stay the same whether they are displayed:
                // 1. In a single pane with the precursor ions (Transitions -> All)
                // 2. In a separate pane of the split graph (Transitions -> All AND Transitions -> Split Graph)
                // 3. In a single pane by themselves (Transition -> Products)
                // We will use an offset in the colors array for cases 2 and 3 so that we do not reuse the precursor ion colors.
                var nodeDisplayType = GraphChromatogram.GetDisplayType(document, parentGroupNode);
                if (displayType == DisplayTypeChrom.products && 
                    (nodeDisplayType != DisplayTypeChrom.single || 
                     !optimizationPresent))
                {
                    colorOffset =
                        GraphChromatogram.GetDisplayTransitions(parentGroupNode, DisplayTypeChrom.precursors).Count();
                }   
            }
            
            int iColor = 0, iCharge = -1;
            var charge = Adduct.EMPTY;
            int countLabelTypes = document.Settings.PeptideSettings.Modifications.CountLabelTypes;
            for (int i = 0; i < countNodes; i++)
            {
                var docNode = graphData.DocNodes[i];
                identityPath = graphData.DocNodePaths[i];
                var pointPairLists = graphData.PointPairLists[i];
                int numSteps = pointPairLists.Count/2;
                for (int iStep = 0; iStep < pointPairLists.Count; iStep++)
                {
                    int step = iStep - numSteps;
                    var pointPairList = pointPairLists[iStep];
                    Color color;
                    var nodeGroup = docNode as TransitionGroupDocNode;
                    if (IsMultiSelect || parentNode is PeptideDocNode && nodeGroup == null)
                    {
                        var peptideDocNode = docNode as PeptideDocNode;
                        if (peptideDocNode == null)
                        {
                            continue;
                        }
                        color = GraphSummary.StateProvider.GetPeptideGraphInfo(peptideDocNode).Color;
                        if (identityPath.Equals(selectedTreeNode.Path) && step == 0)
                        {
                            color = ColorScheme.ChromGraphItemSelected;
                        }
                    }
                    else if (parentNode is PeptideDocNode)
                    {
                        int iColorGroup = GetColorIndex(nodeGroup, countLabelTypes, ref charge, ref iCharge);
                        color = COLORS_GROUPS[iColorGroup % COLORS_GROUPS.Count];
                    }
                    else if (displayType == DisplayTypeChrom.total)
                    {
                        color = COLORS_GROUPS[iColor%COLORS_GROUPS.Count];
                    }
                    else if (ReferenceEquals(docNode, selectedNode) && step == 0)
                    {
                        color = ColorScheme.ChromGraphItemSelected;
                    }
                    else
                    {
                        color = COLORS_TRANSITION[(iColor + colorOffset) % COLORS_TRANSITION.Count];
                    }
                    iColor++;
                    // If showing ratios, do not add the standard type to the graph,
                    // since it will always be empty, but make sure the colors still
                    // correspond with the other graphs.
                    if (nodeGroup != null && countNodes > 1)
                    {
                        if (NormalizationMethod.RatioToLabel.Matches(normalizeOption.NormalizationMethod, nodeGroup.TransitionGroup.LabelType))
                            continue;
                    }

                    string label = graphData.DocNodeLabels[i];
                    if (step != 0)
                        label = string.Format(Resources.AreaReplicateGraphPane_UpdateGraph_Step__0_, step);
                    CurveItem curveItem;

                    // Only use a MeanErrorBarItem if bars are not going to be stacked.
                    // TODO(nicksh): AreaGraphData.NormalizeTo does not know about MeanErrorBarItem 
                    if (graphType == AreaGraphDisplayType.lines)
                    {
                        curveItem = CreateLineItem(label, pointPairList, color);
                    }
                    else if (!IsMultiSelect && BarSettings.Type != BarType.Stack && BarSettings.Type != BarType.PercentStack && dataScalingOption == DataScalingOption.none)
                    {
                        curveItem = new MeanErrorBarItem(label, pointPairList, color, Color.Black);
                    }
                    else 
                    {
                        if (IsMultiSelect)
                        {
                            curveItem = CreateLineItem(label, pointPairList, color);
                        }
                        else
                        {
                            curveItem = new BarItem(label, pointPairList, color);
                        }
                    }
                    if (0 <= selectedReplicateIndex && selectedReplicateIndex < pointPairList.Count)
                    {
                        PointPair pointPair = pointPairList[selectedReplicateIndex];
                        if (!pointPair.IsInvalid)
                        {
                            sumArea += pointPair.Y;
                            maxArea = Math.Max(maxArea, pointPair.Y);
                        }
                    }

                    // Aggregate area of this transition for each area entry
                    Func<double,double, double> aggregateFunc = (sums, y) => sums + y;
                    //if bar type is cluster we need to show dotp label above the highest peak, not at the total peak height
                    if (BarSettings.Type == BarType.Cluster)
                        aggregateFunc = Math.Max;   
                    AddAreasToSums(pointPairList, sumAreas, aggregateFunc);

                    var lineItem = curveItem as LineItem;
                    if (lineItem != null)
                    {
                        lineItem.Tag = identityPath;
                        CurveList.Add(lineItem);
                    }
                    else
                    {
                        var barItem = (BarItem) curveItem;
                        barItem.Bar.Border.IsVisible = false;
                        barItem.Bar.Fill.Brush = GetBrushForNode(document.Settings, docNode, color);
                        barItem.Tag = new IdentityPath(identityPath, docNode.Id);
                        CurveList.Add(barItem);
                    }
                }
            }

            ParentGroupNode = parentGroupNode;
            SumAreas = sumAreas;

            // Draw a box around the currently selected replicate
            if (ShowSelection && maxArea >  -double.MaxValue)
            {
                AddSelection(normalizeOption, selectedReplicateIndex, sumArea, maxArea);
            }
            // Reset the scale when the parent node changes
            bool resetAxes = (_parentNode == null || !ReferenceEquals(_parentNode.Id, parentNode.Id));
            _parentNode = parentNode;

            _dotpData = null;
            if (graphData.DotpData != null && graphData.DotpData.Any(data => !double.IsNaN(data.Y))) 
                _dotpData = ImmutableList.ValueOf(graphData.DotpData.Select(point => (float)point.Y));

            if (ExpectedVisible != AreaExpectedValue.none &&
                DotProductDisplayOption.line.IsSet(Settings.Default))
            {
                Y2Axis.IsVisible = true;
                Y2Axis.Scale.Min = 0;
                Y2Axis.Scale.Max = 1.1;
                Y2Axis.Title.Text = DotpLabelText;
                var dotpLine = new LineItem(DotpLabelText, graphData.DotpData, Color.DimGray, SymbolType.Circle )
                {
                    IsY2Axis = true, Line = new Line() { Style = DashStyle.Dash, Color = Color.DimGray, Width = 2.0f},
                    Symbol = new Symbol() { Type = SymbolType.Diamond, Size = 5f, Fill = new Fill(Color.DimGray)}
                };
                CurveList.Insert(0, dotpLine);
                ToolTip.TargetCurves.ClearAndAdd(dotpLine);
            }
            else
            {
                Y2Axis.IsVisible = false;
            }

            UpdateAxes(resetAxes, aggregateOp, dataScalingOption, normalizeOption);

            if (Settings.Default.PeakAreaDotpCutoffShow && DotProductDisplayOption.line.IsSet(Settings.Default) && _dotpData != null)
            {
                var cutoff = ExpectedVisible.GetDotpValueCutoff(Settings.Default);
                var highlightValues = new PointPairList(graphData.DotpData.Select(point => point.Y < cutoff ? point : new PointPair(){X = point.X, Y = float.NaN}).ToList());
                var cutoffHighlightLine = new LineItem("", highlightValues, Color.DimGray, SymbolType.Circle)
                {
                    IsY2Axis = true,
                    Line = new Line() { Color = Color.Transparent},
                    Symbol = new Symbol() { Type = SymbolType.Diamond, Size = 9f, Fill = new Fill(Color.Red), Border = new Border(Color.Red, 1) }
                };
                cutoffHighlightLine.Label.IsVisible = false;
                CurveList.Add(cutoffHighlightLine);
                ToolTip.TargetCurves.Add(cutoffHighlightLine);


                var belowCutoffCount = _dotpData.Count(dotp => dotp <= cutoff);
                var labelText = string.Format(Resources.AreaReplicateGraphPane_Replicates_Count_Above_Below_Cutoff,
                    _dotpData.Count - belowCutoffCount, belowCutoffCount, DotpLabelText);
                var labelObject = new TextObj(labelText, 1, 0, CoordType.ChartFraction, AlignH.Right, AlignV.Top)
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.E_BehindCurves,
                    FontSpec = GraphSummary.CreateFontSpec(Color.Black),
                };
                labelObject.FontSpec.Fill = new Fill(Color.Transparent);
                GraphObjList.Add(labelObject);
                var cutoffLine = new LineObj()
                {
                    IsClippedToChartRect = true,
                    Location = new Location(0, cutoff, CoordType.XChartFractionY2Scale){Rect = new RectangleF(0, cutoff, 1, 0)},
                    Line = new LineBase(Color.Red)
                };
                GraphObjList.Add(cutoffLine);
                //This is a placeholder to make sure the line shows in the legend.
                CurveList.Insert(0, new LineItem(string.Format(CultureInfo.CurrentCulture,
                    Resources.AreaReplicateGraphPane_Dotp_Cutoff_Line_Label, DotpLabelText, cutoff))
                {
                    Points = new PointPairList(new[] { new PointPair(0, 0) }),
                    Symbol = new Symbol(SymbolType.None, Color.Transparent)
                });
            }
        }

        public override ImmutableList<float> GetToolTipDataSeries()
        {
            return _dotpData;
        }
        public override void PopulateTooltip(int index)
        {
            ToolTip.ClearData();
            ToolTip.AddLine(Resources.AreaReplicateGraphPane_Tooltip_Replicate, XAxis.Scale.TextLabels[index]);
            ToolTip.AddLine(string.Format(CultureInfo.CurrentCulture, Resources.AreaReplicateGraphPane_Tooltip_Dotp, DotpLabelText), 
                string.Format(CultureInfo.CurrentCulture, @"{0:F02}", _dotpData[index]));
        }

        private void AddSelection(NormalizeOption areaView, int selectedReplicateIndex, double sumArea, double maxArea)
        {
            double yValue;
            switch (BarSettings.Type)
            {
                case BarType.Stack:
                    // The Math.Min(sumArea, .999) makes sure that if graph is in normalized view
                    // height of the selection rectangle does not exceed 1, so that top of the rectangle
                    // can be viewed when y-axis scale maximum is at 1
                    yValue = (areaView == NormalizeOption.MAXIMUM ? Math.Min(sumArea, .999) : sumArea);
                    break;
                case BarType.PercentStack:
                    yValue = 99.99;
                    break;
                default:
                    // Scale the selection box to fit exactly the bar height
                    yValue = (areaView == NormalizeOption.MAXIMUM ? Math.Min(maxArea, .999) : maxArea);
                    break;
            }
            if (IsLineGraph)
            {
                GraphObjList.Add(new LineObj(Color.Black, selectedReplicateIndex + 1, 0, selectedReplicateIndex + 1, maxArea)
                {
                    IsClippedToChartRect = true,
                    Line = new Line() {Width = 2, Color = Color.Black, Style = DashStyle.Dash}
                });
            }
            else
            {
                GraphObjList.Add(new BoxObj(selectedReplicateIndex + .5, yValue, 0.99,
                        -yValue, Color.Black, Color.Empty)
                // Just passing in yValue here doesn't work when log scale is enabled, -yValue works with and without log scale enabled
                {
                    IsClippedToChartRect = true,
                });
            }
        }

        private void UpdateAxes(bool resetAxes, GraphValues.AggregateOp aggregateOp, DataScalingOption dataScalingOption,
            NormalizeOption normalizeOption)
        {
            if (resetAxes)
            {
                XAxis.Scale.MaxAuto = XAxis.Scale.MinAuto = true;
                YAxis.Scale.MaxAuto = true;
            }
            if (BarSettings.Type == BarType.PercentStack)
            {
                YAxis.Scale.Max = 100;
                YAxis.Scale.MaxAuto = false;
                YAxis.Title.Text = aggregateOp.AnnotateTitle(Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area_Percentage);
                YAxis.Type = AxisType.Linear;
                YAxis.Scale.MinAuto = false;
                FixedYMin = YAxis.Scale.Min = 0;
            }
            else
            {
                if (dataScalingOption == DataScalingOption.optimization)
                {
                    // If currently log scale or normalized to max, reset the y-axis max
                    if (YAxis.Type == AxisType.Log || YAxis.Scale.Max == 1)
                        YAxis.Scale.MaxAuto = true;

                    YAxis.Title.Text = aggregateOp.AnnotateTitle(Resources.AreaReplicateGraphPane_UpdateGraph_Percent_of_Regression_Peak_Area);
                    YAxis.Type = AxisType.Linear;
                    YAxis.Scale.MinAuto = false;
                    FixedYMin = YAxis.Scale.Min = 0;
                }
                else if (normalizeOption == NormalizeOption.MAXIMUM)
                {
                    YAxis.Scale.Max = 1;
                    if (DotProductLabelsVisible)
                        // Make YAxis Scale Max a little higher to accommodate for the dot products
                        YAxis.Scale.Max = 1.1;
                    YAxis.Scale.MaxAuto = false;
                    YAxis.Title.Text = aggregateOp.AnnotateTitle(Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area_Normalized);
                    YAxis.Type = AxisType.Linear;
                    YAxis.Scale.MinAuto = false;
                    FixedYMin = YAxis.Scale.Min = 0;
                }
                else if (Settings.Default.AreaLogScale)
                {
                    // If currently not log scale, reset the y-axis max
                    if (YAxis.Type != AxisType.Log)
                        YAxis.Scale.MaxAuto = true;
                    if (Settings.Default.PeakAreaMaxArea != 0)
                    {
                        YAxis.Scale.MaxAuto = false;
                        YAxis.Scale.Max = Settings.Default.PeakAreaMaxArea;
                    }

                    YAxis.Type = AxisType.Log;
                    YAxis.Title.Text = GraphValues.AnnotateLogAxisTitle(aggregateOp.AnnotateTitle(
                        Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area));
                    YAxis.Scale.MinAuto = false;
                    FixedYMin = YAxis.Scale.Min = 1;
                }
                else
                {
                    // If currently log scale, reset the y-axis max
                    if (YAxis.Type == AxisType.Log)
                        YAxis.Scale.MaxAuto = true;
                    if (Settings.Default.PeakAreaMaxArea != 0)
                    {
                        YAxis.Scale.MaxAuto = false;
                        YAxis.Scale.Max = Settings.Default.PeakAreaMaxArea;
                    }
                    else if (!YAxis.Scale.MaxAuto)
                    {
                        YAxis.Scale.MaxAuto = true;
                    }
                    string yTitle = Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area;
                    if (normalizeOption == NormalizeOption.CALIBRATED)
                    {
                        yTitle = CalibrationCurveFitter.AppendUnits(QuantificationStrings.Calculated_Concentration,
                            GraphSummary.StateProvider.SelectionDocument.Settings.PeptideSettings.Quantification.Units);
                    }
                    else
                    {
                        NormalizationMethod normalizationMethod = null;
                        if (normalizeOption == NormalizeOption.DEFAULT)
                        {
                            var normalizationMethods = NormalizationMethod.GetMoleculeNormalizationMethods(
                                GraphSummary.StateProvider.SelectionDocument,
                                GraphSummary.StateProvider.SelectedNodes.OfType<SrmTreeNode>()
                                    .Select(node => node.Path));
                            if (normalizationMethods.Count == 1)
                            {
                                normalizationMethod = normalizationMethods.First();
                            }
                        }

                        normalizationMethod = normalizationMethod ?? normalizeOption.NormalizationMethod;
                        if (normalizationMethod != null)
                        {
                            if (NormalizationMethod.RatioToLabel.Matches(normalizationMethod, PaneKey.IsotopeLabelType))
                                yTitle = Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area;
                            else 
                                yTitle = normalizationMethod.GetAxisTitle(Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area);
                        }
                        else
                        {
                            yTitle = QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Normalized_Peak_Area;
                        }
                    }
                    YAxis.Title.Text = aggregateOp.AnnotateTitle(yTitle);
                    YAxis.Type = AxisType.Linear;
                    YAxis.Scale.MinAuto = false;
                    FixedYMin = YAxis.Scale.Min = 0;
                }
                // Handle a switch from percent stack
                if (!YAxis.Scale.MaxAuto && YAxis.Scale.Max == 100)
                    YAxis.Scale.MaxAuto = true;
            }
            Legend.IsVisible = !IsMultiSelect && Settings.Default.ShowPeakAreaLegend;
            AxisChange();

            // Reformat Y-Axis for labels and whiskers
            var maxY = GraphHelper.GetMaxY(CurveList,this);
            if (DotProductLabelsVisible)
            {
                var extraSpace = _labelHeight*(maxY/(Chart.Rect.Height - _labelHeight*2))*2;
                maxY += extraSpace;
            }
       
            GraphHelper.ReformatYAxis(this, maxY > 0 ? maxY : 0.1); // Avoid same min and max, since it blanks the entire graph pane
            GraphSummary.GraphControl.Refresh();
        }

        private void AddAreasToSums(PointPairList pointPairList, IList<double> sumAreas, Func<double, double, double> aggregateFunc)
        {
            for (int i = 0; i < pointPairList.Count; i++)
            {
                PointPair pointPair = pointPairList[i];
                int index = i;
                if (pointPair.IsInvalid)
                    continue;

                if (ExpectedVisible.IsVisible())
                {
                    // Skip finding the sumArea for the first bar if the library is showing
                    if (i == 0)
                        continue;

                    // offset index by 1, since (n + 1)th bar corresponds to the nth replicate
                    index--;
                }
                sumAreas[index] = aggregateFunc(sumAreas[index], pointPair.Y);
            }
        }

        private GraphObjList _dotpLabels;

        private IEnumerable<string> DotProductStrings
        {
            get
            {
                 return (_dotpLabels.Count > 0 || !DotProductLabelsVisible)
                     ? _dotpLabels.Select(l => ((TextObj)l).Text)
                     : SumAreas.Select((t, i) => GetDotProductResultsText(i + FirstDataIndex));
            }
        }

        public IEnumerable<double> DotProducts
        {
            get
            {
                return DotProductStrings.Select(l => double.Parse(l.Split('\n')[1]));
            }
        }

        private void AddDotProductLabels(Graphics g, TransitionGroupDocNode nodeGroup, IList<double> sumAreas)
        {
            if (IsLineGraph)
                return;

            // Create temporary label to calculate positions
            var pointSize = GetDotProductsPointSize(g);
            bool visible = pointSize.HasValue;
            bool visibleState = _dotpLabels.Count > 0;

            if (visible == visibleState && (!visibleState || ((TextObj)_dotpLabels[0]).FontSpec.Size == pointSize))
                return;

            foreach (GraphObj pa in _dotpLabels)
                GraphObjList.Remove(pa);
            _dotpLabels.Clear();

            if (visible)
            {
                for (int i = 0; i < sumAreas.Count; i++)
                {
                    string text = GetDotProductResultsText(i + FirstDataIndex);
                    if (string.IsNullOrEmpty(text))
                        continue;

                    TextObj textObj = new TextObj(text,
                                                  i + FirstDataIndex + 1, sumAreas[i],
                                                  CoordType.AxisXYScale,
                                                  AlignH.Center,
                                                  AlignV.Bottom)
                                          {
                                              IsClippedToChartRect = true,
                                              ZOrder = ZOrder.E_BehindCurves
                                          };


                    textObj.FontSpec.Border.IsVisible = false;
                    textObj.FontSpec.Size = pointSize.Value;
                    textObj.FontSpec.Fill = new Fill(Color.Transparent);
                    _labelHeight =(int) textObj.FontSpec.GetHeight(CalcScaleFactor());
                    GraphObjList.Add(textObj);
                    _dotpLabels.Add(textObj);
                }
            }
        }

        private int? GetDotProductsPointSize(Graphics g)
        {
            for (int pointSize = (int) Settings.Default.AreaFontSize; pointSize > 4; pointSize--)
            {
                var fontLabel = new FontSpec {Size = pointSize};
                var sizeLabel = fontLabel.MeasureString(g, DotpLabelText, CalcScaleFactor());

                float labelWidth = (float) XAxis.Scale.ReverseTransform((XAxis.Scale.Transform(0) + sizeLabel.Width));

                if (labelWidth < 1.2)
                    return pointSize;          
            }
            return null;
        }

        private string GetDotProductResultsText(int indexResult)
        {
            if (_dotpData?.Count > 0 && indexResult < _dotpData.Count && !float.IsNaN(_dotpData[indexResult]))
            {
                var separator = DotProductDisplayOption.line.IsSet(Settings.Default) ? (Func<IEnumerable<string>, string>)TextUtil.SpaceSeparate : TextUtil.LineSeparate;
                return separator(new [] { DotpLabelText , string.Format(@"{0:F02}", _dotpData[indexResult]) } ) ;
            }
            else
                return null;
        }

        private string DotpLabelText
        {
            get
            {
                return ExpectedVisible.GetDotpLabel();
            }
        }

        protected override int SelectedIndex
        {
            get
            {
                // If library is showing
                if (ExpectedVisible.IsVisible())
                {
                    // If the MS/MS Spectrum document is selected, 
                    // the seletion box is currently on library column,
                    // so return a selectionIndex of 0
                    if (GraphSummary.ActiveLibrary)
                        return 0;
                    
                    return base.SelectedIndex + 1;
                }
                return base.SelectedIndex;
            }
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
            if (ExpectedVisible.IsVisible())
            {
                if (selectedIndex < 0)
                    return;
                if (selectedIndex == 0)
                {
                    // Show MS/MS Spectrum tab and keep focus on the graph
                    GraphSummary.ActiveLibrary = true;
                    GraphSummary.StateProvider.ActivateSpectrum();
                    GraphSummary.Focus();
                    return;
                }
                GraphSummary.ActiveLibrary = false;
                selectedIndex--;
            }

            base.ChangeSelection(selectedIndex, identityPath);
        }

        private enum DataScalingOption { none, optimization, maximum_stack, maximum, total }

        /// <summary>
        /// Holds the data that is currently displayed in the graph.
        /// Currently, we don't hold onto this object, because we never need to look
        /// at the data after the graph is rendered.
        /// </summary>
        private class AreaGraphData : GraphData
        {
            public static readonly NormalizeOption RATIO_INDEX_NONE = NormalizeOption.NONE;

            private readonly DocNode _docNode;
            private readonly NormalizeOption _normalizeOption;
            private readonly DataScalingOption _dataScalingOption;
            private readonly AreaExpectedValue _expectedVisible;
            private readonly bool _zeroMissingValues;
            private PointPairList _dotpData;

            public AreaGraphData(SrmDocument document,
                                 IdentityPath identityPath,
                                 DisplayTypeChrom displayType,
                                 ReplicateGroupOp replicateGroupOp,
                                 NormalizeOption normalizeOption,
                                 DataScalingOption dataScalingOption,
                                 AreaExpectedValue expectedVisible,
                                 PaneKey paneKey,
                                 bool zeroMissingValues)
                : this(document, new []{identityPath}, displayType, replicateGroupOp, normalizeOption, dataScalingOption, expectedVisible, paneKey, zeroMissingValues)
            {
                _docNode = document.FindNode(identityPath);
            }

            public AreaGraphData(SrmDocument document,
                                 IEnumerable<IdentityPath> selectedDocNodePaths,
                                 DisplayTypeChrom displayType,
                                 ReplicateGroupOp replicateGroupOp,
                                 NormalizeOption normalizeOption,
                                 DataScalingOption dataScalingOption,
                                 AreaExpectedValue expectedVisible,
                                 PaneKey paneKey,
                                 bool zeroMissingValues = false)
                : base(document, selectedDocNodePaths, displayType, replicateGroupOp, paneKey)
            {
                _normalizeOption = normalizeOption;
                _dataScalingOption = dataScalingOption;
                _expectedVisible = expectedVisible;
                _zeroMissingValues = zeroMissingValues;
                NormalizedValueCalculator = new NormalizedValueCalculator(document);
            }

            protected override void InitData()
            {
                base.InitData();

                if (_docNode is TransitionGroupDocNode nodeGroup && _expectedVisible != AreaExpectedValue.none)
                {
                    if (_expectedVisible.IsVisible())
                    {
                        var expectedIntensities =
                            from nodeTran in GraphChromatogram.GetDisplayTransitions(nodeGroup, DisplayType)
                            select GetExpectedValue(nodeTran);
                        var intensityArray = expectedIntensities.ToArray();

                        for (int i = 0; i < PointPairLists.Count; i++)
                        {
                            if (i >= intensityArray.Length)
                                continue;

                            var pointPairLists2 = PointPairLists[i];
                            foreach (var pointPairList in pointPairLists2)
                            {
                                pointPairList.Insert(0, 0, intensityArray[i]);
                            }
                        }
                    }

                    if (_expectedVisible != AreaExpectedValue.none)
                    {
                        _dotpData = new PointPairList();
                        if(_expectedVisible.IsVisible())
                            _dotpData.Insert(0, 0, double.NaN);
                        for (var replicateGroupIndex = 0;
                            replicateGroupIndex < ReplicateGroups.Count;
                            replicateGroupIndex++)
                        {
                            var xValue = replicateGroupIndex + (_expectedVisible.IsVisible() ? 1 : 0);
                            _dotpData.Insert(xValue, xValue, GetDotProductResults(nodeGroup, replicateGroupIndex));
                        }
                    }
                }

                switch (_dataScalingOption)
                {
                    case DataScalingOption.none:
                        // If library column is showing, make library column as tall as the tallest stack
                        if (_expectedVisible.IsVisible())
                            NormalizeMaxStack();
                        break;
                    case DataScalingOption.optimization:
                        NormalizeOpt();
                        break;
                    case DataScalingOption.maximum:
                        NormalizeMax();
                        break;
                    case DataScalingOption.maximum_stack:
                        NormalizeMaxStack();
                        break;
                    case DataScalingOption.total:
                        FixupForTotals();
                        break;
                }
            }

            private float GetDotProductResults(TransitionGroupDocNode nodeGroup, int indexResult)
            {
                if (_expectedVisible == AreaExpectedValue.none)
                    return float.NaN;
                if (_expectedVisible == AreaExpectedValue.ratio_to_label)
                {
                    if (_normalizeOption.NormalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
                    {
                        var precursorNodePath = DocNodePath.GetNodePath(nodeGroup.Id, _document);
                        if (precursorNodePath.Peptide != null && !NormalizationMethod.RatioToLabel.Matches(ratioToLabel, nodeGroup.LabelType))
                        {
                            var ratio = NormalizedValueCalculator.GetTransitionGroupRatioValue(
                                ratioToLabel,
                                precursorNodePath.Peptide, nodeGroup,
                                nodeGroup.GetChromInfoEntry(indexResult));
                            if (ratio?.HasDotProduct ?? false)
                                return ratio.DotProduct;
                        }
                    }
                }
                else
                {
                    var replicateIndices= ReplicateGroups[indexResult].ReplicateIndexes;
                    if (replicateIndices.IsEmpty)
                        replicateIndices = ReplicateIndexSet.OfValues( new[] {-1} );
                    IList<float?> values = new float?[] { };

                    if(_expectedVisible == AreaExpectedValue.library)
                        values = replicateIndices.Select(nodeGroup.GetLibraryDotProduct).ToList();
                    if (_expectedVisible == AreaExpectedValue.isotope_dist)
                        values = replicateIndices.Select(nodeGroup.GetIsotopeDotProduct).ToList();
                    if (!values.Any(val => val.HasValue && !float.IsNaN(val.Value)))
                        return float.NaN;
                    var statistics = new Statistics(values
                        .Select(value => value.HasValue ? (double?)value : null)
                        .Where(value => value.HasValue)
                        .Cast<double>());
                    return (float)statistics.Mean();
                }
                return float.NaN;
            }

            protected NormalizedValueCalculator NormalizedValueCalculator { get; private set; }
            public PointPairList DotpData => _dotpData;

            public override PointPair PointPairMissing(int xValue)
            {
                return _zeroMissingValues
                    ? base.PointPairMissing(xValue)
                    : new PointPair(xValue, PointPairBase.Missing);
            }

            private float GetExpectedValue(TransitionDocNode nodeTran)
            {
                switch (_expectedVisible)
                {
                    case AreaExpectedValue.library:
                        return nodeTran.HasLibInfo ? nodeTran.LibInfo.Intensity : 0;
                    case AreaExpectedValue.isotope_dist:
                        return nodeTran.HasDistInfo ? nodeTran.IsotopeDistInfo.Proportion : 0;
                    default:
                        return 0;
                }
            }

            /// <summary>
            /// Normalize optimization data to the regression predicted value.
            /// </summary>
            private void NormalizeOpt()
            {
                foreach (var pointPairLists in PointPairLists)
                {
                    if (pointPairLists.Count == 0)
                        continue;

                    int numSteps = pointPairLists.Count/2;
                    var pointPairListRegression = pointPairLists[numSteps];
                    // Normalize all non-regression values to be percent of the regression
                    for (int i = 0; i < pointPairLists.Count; i++)
                    {
                        if (i == numSteps)
                            continue;

                        var pointPairList = pointPairLists[i];
                        for (int j = 0; j < pointPairList.Count; j++)
                        {
                            // If the regression value is missing, then normalization is not possible.
                            double regressionValue = pointPairListRegression[j].Y;
                            if (regressionValue == PointPairBase.Missing || regressionValue == 0)
                                pointPairList[j].Y = PointPairBase.Missing;
                            // If the value itself is not missing, then do the normalization
                            else if (pointPairList[j].Y != PointPairBase.Missing)
                                pointPairList[j].Y = pointPairList[j].Y / pointPairListRegression[j].Y * 100;                            
                        }
                    }
                    // And make the regression values 100 percent
                    foreach (PointPair regression in pointPairListRegression)
                    {
                        // If it is missing, leave it missing.
                        double regressionValue = regression.Y;
                        if (regressionValue != PointPairBase.Missing && regressionValue != 0)
                            regression.Y = 100;
                    }
                }                
            }

            /// <summary>
            /// Divides each Y value by some factor and makes missing values zeros:
            /// for NormalizeMax: denominator is the maxHeight
            /// for NormalizeMaxStack: maxBarHeight
            /// for FixupForTotals: 1
            /// </summary>
            /// <param name="denominator">Divide all point y values by this number</param>
            /// <param name="libraryHeight">Total height of the library column</param>
            private void NormalizeTo(double? denominator, double libraryHeight)
            {
                IList<double> listTotals = null;
                if (!denominator.HasValue)
                {
                    if (_docNode == null)
                        denominator = 1;    // Multi-select peptid graph
                    else
                        listTotals = GetTotalsList().Select(t => t / 100).ToArray();  // Normalize to 100%
                }

                foreach (var pointPairLists in PointPairLists)
                {
                    if (pointPairLists.Count == 0)
                        continue;

                    foreach (var pointPairList in pointPairLists)
                    {
                        for (int i = 0; i < pointPairList.Count; i++ )
                        {
                            if (pointPairList[i].Y != PointPairBase.Missing)
                            {
                                double pointDenom = denominator ?? listTotals[i];

                                // If library is displayed and the set of data to plot is at
                                // index 0 (where we store library intensity data)
                                // calculate the proportion of the denominator for each point
                                if (_expectedVisible.IsVisible() && i == 0 && denominator.HasValue)
                                    pointPairList[i].Y *= (pointDenom/libraryHeight);
                                if (_dataScalingOption != DataScalingOption.none)
                                {
                                    pointPairList[i].Y /= pointDenom;
                                    var errorTag = pointPairList[i].Tag as ErrorTag;
                                    if (errorTag != null && errorTag.Error != 0 && errorTag.Error != PointPairBase.Missing)
                                        pointPairList[i].Tag = new ErrorTag(errorTag.Error/pointDenom);
                                }
                            }
                            else if (_zeroMissingValues)
                            {
                                pointPairList[i].Y = 0;
                            }
                        }
                    }
                }
            }

            // Goes through each pointPairLists and finds the one with the maximum height
            // Then normalizes the data to that maximum height
            private void NormalizeMax()
            {
                double maxHeight = -double.MaxValue;
                double libraryHeight = 0;
                foreach (var pointPairLists in PointPairLists)
                {
                    if (pointPairLists.Count == 0)
                        continue;

                    foreach (var pointPairList in pointPairLists)
                    {
                        for (int i = 0; i < pointPairList.Count; i++)
                        {
                            if (pointPairList[i].Y != PointPairBase.Missing)
                            {
                                if (_expectedVisible.IsVisible() && i == 0)
                                    libraryHeight += pointPairList[i].Y;
                                else
                                    maxHeight = Math.Max(maxHeight, pointPairList[i].Y);
                            }
                        }
                    }
                }

                // Normalizes each non-missing point by max bar height
                NormalizeTo(maxHeight, libraryHeight);
            }

            // Goes through each pointPairLists and finds the maximum stacked bar height
            // Then normalizes the data to the maximum stacked bar height
            private void NormalizeMaxStack()
            {
                var listTotals = GetTotalsList();

                // Finds the maximum bar height from the list of bar heights
                if (listTotals.Count != 0)
                {
                    double firstColumnHeight = listTotals[0];
                    // If the library column is visible, remove it before getting the max height
                    if (_expectedVisible.IsVisible())
                        listTotals.RemoveAt(0);
                    double maxBarHeight = listTotals.Aggregate(Math.Max);

                    // Normalizes each non-missing point by max bar height
                    NormalizeTo(maxBarHeight, _expectedVisible.IsVisible()
                                                  ? firstColumnHeight
                                                  : 0);
                }
            }

            private IList<double> GetTotalsList()
            {
                var listTotals = new List<double>();
                // Populates a list storing each of the bar heights
                foreach (var pointPairLists in PointPairLists)
                {
                    if (pointPairLists.Count == 0)
                        continue;

                    foreach (var pointPairList in pointPairLists)
                    {
                        for (int i = 0; i < pointPairList.Count; i++)
                        {
                            while (listTotals.Count < pointPairList.Count)
                                listTotals.Add(0);

                            if (pointPairList[i].Y != PointPairBase.Missing)
                            {
                                listTotals[i] += pointPairList[i].Y;
                            }
                        }
                    }
                }
                return listTotals;
            }

            // Sets each missing point to be 0, so that the percent stack will show
            private void FixupForTotals()
            {
                NormalizeTo(null, 1);
            }

            protected override bool IsMissingValue(TransitionChromInfoData chromInfo)
            {
                return !_zeroMissingValues && !GetValue(chromInfo).HasValue;
            }

            protected override PointPair CreatePointPair(int iResult, ICollection<TransitionChromInfoData> chromInfoDatas)
            {
                return ReplicateGroupOp.AggregateOp.MakeBarValue(iResult, 
                    chromInfoDatas.Where(c => !IsMissingValue(c)).Select(c => (double) (GetValue(c) ?? 0)));
            }

            protected override bool IsMissingValue(TransitionGroupChromInfoData chromInfoData)
            {
                return !GetValue(chromInfoData).HasValue;
            }

            protected override PointPair CreatePointPair(int iResult, ICollection<TransitionGroupChromInfoData> chromInfoDatas)
            {
                return ReplicateGroupOp.AggregateOp.MakeBarValue(iResult, 
                    chromInfoDatas.Select(chromInfoData => (double) (GetValue(chromInfoData) ?? 0)));
            }

            protected override List<LineInfo> GetPeptidePointPairLists(PeptideGroupDocNode peptideGroup, PeptideDocNode nodePep, bool multiplePeptides)
            {
                if (_normalizeOption == NormalizeOption.CALIBRATED 
                    || multiplePeptides && _normalizeOption == NormalizeOption.DEFAULT)
                {
                    return new List<LineInfo>
                    {
                        new LineInfo(nodePep, nodePep.ModifiedSequenceDisplay,
                            new List<PointPairList> {GetCalibratedPeptidePointList(peptideGroup, nodePep)})
                    };
                }

                var tuples = base.GetPeptidePointPairLists(peptideGroup, nodePep, multiplePeptides);
                if (!multiplePeptides)
                {
                    return tuples;
                }
                var pointLists = new List<List<PointPair>>();
                foreach (var tuple in tuples)
                {
                    foreach (var pointPairList in tuple.PointPairList)
                    {
                        for (int i = 0; i < pointPairList.Count; i++)
                        {
                            if (i >= pointLists.Count)
                            {
                                pointLists.Add(new List<PointPair>());
                            }
                            pointLists[i].Add(pointPairList[i]);
                        }
                    }
                }
                var normalizationMethod =
                    NormalizedValueCalculator.NormalizationMethodForMolecule(nodePep, _normalizeOption);
                var result = new PointPairList();
                foreach (var points in pointLists)
                {
                    var x = points[0].X;
                    ErrorTag tag = null;
                    double y;
                    if (!(normalizationMethod is NormalizationMethod.RatioToLabel))
                    {
                        y = points.Sum(point => point.Y);
                        tag = CalcErrorTag(points, false);
                    }
                    else
                    {
                        var validPoints = new List<PointPair>();
                        for (var i = 0; i < points.Count; i++)
                        {
                            var isStandard = NormalizationMethod.RatioToLabel.Matches(normalizationMethod,
                                (nodePep.Children[i] as TransitionGroupDocNode)?.LabelType);
                            if (!double.IsNaN(points[i].Y) && !isStandard)
                                validPoints.Add(points[i]);
                        }

                        if (validPoints.Count == 0)
                        {
                            y = double.NaN;
                        }
                        else
                        {
                            y = validPoints.Average(p => p.Y);
                            tag = CalcErrorTag(validPoints, true);
                        }
                    }
                    result.Add(new PointPair(x, y){Tag = tag});
                }
                return new List<LineInfo>()
                {
                    new LineInfo(nodePep, nodePep.ModifiedSequenceDisplay, new List<PointPairList> {result})
                };
            }

            public PointPairList GetCalibratedPeptidePointList(PeptideGroupDocNode peptideGroup,
                PeptideDocNode peptideDocNode)
            {
                var document = NormalizedValueCalculator.Document;
                var peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(document, peptideGroup, peptideDocNode);
                CalibrationCurve calibrationCurve = null;
                var calibrationCurveFitter = new CalibrationCurveFitter(peptideQuantifier, document.Settings);
                if (_normalizeOption == NormalizeOption.CALIBRATED)
                {
                    calibrationCurve = calibrationCurveFitter.GetCalibrationCurve();
                }

                Func<int, double?> getValue = replicateIndex =>
                {
                    if (calibrationCurve == null)
                    {
                        return calibrationCurveFitter.GetNormalizedPeakArea(
                            new CalibrationPoint(replicateIndex, null));
                    }

                    return calibrationCurveFitter.GetCalculatedConcentration(calibrationCurve, replicateIndex);
                };

                Func<int, ICollection<PeptideChromInfoData>, PointPair> makePointPair = (replicateIndex, datas) =>
                {
                    var values = datas.Select(data => getValue(data.ReplicateIndex)).OfType<double>();
                    return ReplicateGroupOp.AggregateOp.MakeBarValue(replicateIndex, values);
                };

                Func<PeptideChromInfoData, bool> isMissing = chromInfoData => !getValue(chromInfoData.ReplicateIndex).HasValue;
                return GetPeptidePointPairList(peptideGroup, peptideDocNode, isMissing, makePointPair);

            }

            private ErrorTag CalcErrorTag(IList<PointPair> points, bool average)
            {
                double? variance = null;
                int count = 0;
                foreach (var errorTag in points.Select(point => point.Tag as ErrorTag))
                {
                    if (errorTag != null && errorTag.Error != PointPairBase.Missing)
                    {
                        // CONSIDER: Some chance of overflow with this method of calculating variance
                        variance = (variance ?? 0) + errorTag.Error * errorTag.Error;
                        count++;
                    }
                }
                if (!variance.HasValue)
                    return null;
                if (average)
                    variance = variance/count;
                return new ErrorTag(Math.Sqrt(variance.Value));
            }

            private float? GetValue(TransitionGroupChromInfoData chromInfo)
            {
                return (float?) NormalizedValueCalculator.GetTransitionGroupDataValue(_normalizeOption, chromInfo);
            }

            private float? GetValue(TransitionChromInfoData chromInfo)
            {
                return (float?) NormalizedValueCalculator.GetTransitionDataValue(_normalizeOption, chromInfo);
            }
        }
    }
}
