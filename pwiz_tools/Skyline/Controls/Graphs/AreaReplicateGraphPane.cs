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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum AreaExpectedValue { none, library, isotope_dist }  

    /// <summary>
    /// Graph pane which shows the comparison of retention times across the replicates.
    /// </summary>
    public class AreaReplicateGraphPane : SummaryReplicateGraphPane
    {
        private int _lableHeight;
        public AreaReplicateGraphPane(GraphSummary graphSummary, PaneKey paneKey)
            : base(graphSummary)
        {
            PaneKey = paneKey;
        }

        protected override void InitFromData(GraphData graphData)
        {
            base.InitFromData(graphData);
            if (IsExpectedVisible)
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
                switch (AreaGraphController.AreaView)
                {
                    case AreaNormalizeToView.area_ratio_view:
                        return BarType.Cluster;
                    case AreaNormalizeToView.area_percent_view:
                        return BarType.PercentStack;
                    default:
                        return BarType.Stack;
                }
            }
        }

        public AreaExpectedValue ExpectedVisible { get; set; }

        public bool IsExpectedVisible
        {
            get { return ExpectedVisible != AreaExpectedValue.none && Settings.Default.ShowLibraryPeakArea; }
        }

        protected override int FirstDataIndex
        {
            get { return IsExpectedVisible ? 1 : 0; }
        }

        public bool CanShowDotProduct { get; private set; }

        public bool IsDotProductVisible { get { return CanShowDotProduct && Settings.Default.ShowDotProductPeakArea; } }

        public bool CanShowPeakAreaLegend { get; private set; }
        
        public IList<double> SumAreas { get; private set; }

        public TransitionGroupDocNode ParentGroupNode { get; private set; }

        public override void Draw(Graphics g)
        {
            // Make sure changes are not only drawn when the graph is updated.
            if (IsDotProductVisible)
                AddDotProductLabels(g, ParentGroupNode, SumAreas);

            base.Draw(g);
        }

        protected override bool IsRedrawRequired(Graphics g)
        {
            if (base.IsRedrawRequired(g))
                return true;

            // Have to call AddDotProductLabels twice, since the X-scale may not be up
            // to date before calling Draw.  If nothing changes, this will be a no-op
            if (IsDotProductVisible)
            {
                int dotpLabelsCount = _dotpLabels.Count;
                AddDotProductLabels(g, ParentGroupNode, SumAreas);
                if (dotpLabelsCount != _dotpLabels.Count)
                    return true;
            }
            return false;
        }

        public override void UpdateGraph(bool checkData)
        {
            _dotpLabels = new GraphObjList();

            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            var results = document.Settings.MeasuredResults;
            bool resultsAvailable = results != null;
            Clear();

            if (!resultsAvailable)
            {
                Title.Text = Resources.AreaReplicateGraphPane_UpdateGraph_No_results_available;
                EmptyGraph(document);
                return;
            }

            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode == null || document.FindNode(selectedTreeNode.Path) == null)
            {
                EmptyGraph(document);
                return;
            }

            BarSettings.Type = BarType;
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
                if (children.Length == 1 && displayType != DisplayTypeChrom.total)
                {
                    selectedNode = parentNode = children[0];
                    identityPath = new IdentityPath(identityPath, parentNode.Id);
                }
                else
                {
                    BarSettings.Type = BarType.Cluster;
                }
            }
            else if (!(selectedTreeNode is TransitionGroupTreeNode))
            {
                Title.Text = Resources.AreaReplicateGraphPane_UpdateGraph_Select_a_peptide_to_see_the_peak_area_graph;
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
            int ratioIndex = AreaGraphData.RATIO_INDEX_NONE;
            var standardType = IsotopeLabelType.light;

            var areaView = AreaGraphController.AreaView;
            if (areaView == AreaNormalizeToView.area_ratio_view)
            {
                ratioIndex = GraphSummary.RatioIndex;
                standardType = document.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes[ratioIndex];                
            }
            else if (areaView == AreaNormalizeToView.area_global_standard_view)
            {
                if (document.Settings.HasGlobalStandardArea)
                    ratioIndex = ChromInfo.RATIO_INDEX_GLOBAL_STANDARDS;
                else
                    areaView = AreaNormalizeToView.none;
            }

            // Sets normalizeData to optimization, maximum_stack, maximum, total, or none
            AreaNormalizeToData normalizeData;
            if (optimizationPresent && displayType == DisplayTypeChrom.single &&
                areaView == AreaNormalizeToView.area_percent_view)
            {
                normalizeData = AreaNormalizeToData.optimization;
            }
            else if (areaView == AreaNormalizeToView.area_maximum_view)
            {
                normalizeData = BarSettings.Type == BarType.Stack
                                    ? AreaNormalizeToData.maximum_stack
                                    : AreaNormalizeToData.maximum;
            }
            else if (BarSettings.Type == BarType.PercentStack)
            {
                normalizeData = AreaNormalizeToData.total;
            }
            else
            {
                normalizeData = AreaNormalizeToData.none;
            }

            // Calculate graph data points
            // IsExpectedVisible depends on ExpectedVisible
            ExpectedVisible = AreaExpectedValue.none;
            if (parentGroupNode != null &&
                    displayType != DisplayTypeChrom.total &&
                    areaView != AreaNormalizeToView.area_ratio_view &&
                    !(optimizationPresent && displayType == DisplayTypeChrom.single))
            {
                var displayTrans = GraphChromatogram.GetDisplayTransitions(parentGroupNode, displayType).ToArray();
                bool isShowingMs = displayTrans.Any(nodeTran => nodeTran.IsMs1);
                bool isShowingMsMs = displayTrans.Any(nodeTran => !nodeTran.IsMs1);
                bool isFullScanMs = document.Settings.TransitionSettings.FullScan.IsEnabledMs && isShowingMs;
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
            var expectedValue = IsExpectedVisible ? ExpectedVisible : AreaExpectedValue.none;
            var replicateGroupOp = GraphValues.ReplicateGroupOp.FromCurrentSettings(document.Settings);
            var graphData = new AreaGraphData(document,
                                              parentNode,
                                              displayType,
                                              replicateGroupOp,
                                              ratioIndex,
                                              normalizeData,
                                              expectedValue,
                                              PaneKey);

            var aggregateOp = replicateGroupOp.AggregateOp;
            // Avoid stacking CVs
            if (aggregateOp.Cv || aggregateOp.CvDecimal)
                BarSettings.Type = BarType.Cluster;

            int countNodes = graphData.DocNodes.Count;
            if (countNodes == 0)
                ExpectedVisible = AreaExpectedValue.none;
            CanShowDotProduct = ExpectedVisible != AreaExpectedValue.none &&
                                areaView != AreaNormalizeToView.area_percent_view;
            CanShowPeakAreaLegend = countNodes != 0;

            InitFromData(graphData);

            // Add data to the graph
            int selectedReplicateIndex = SelectedIndex;
            if (IsExpectedVisible)
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
                var nodeDisplayType = GraphChromatogram.GetDisplayType(document, selectedTreeNode);
                if (displayType == DisplayTypeChrom.products && 
                    (nodeDisplayType != DisplayTypeChrom.single || 
                    (nodeDisplayType == DisplayTypeChrom.single && !optimizationPresent)))
                {
                    colorOffset =
                        GraphChromatogram.GetDisplayTransitions(parentGroupNode, DisplayTypeChrom.precursors).Count();
                }   
            }
            
            int iColor = 0, iCharge = -1;
            int? charge = null;
            int countLabelTypes = document.Settings.PeptideSettings.Modifications.CountLabelTypes;
            for (int i = 0; i < countNodes; i++)
            {
                var docNode = graphData.DocNodes[i];
                var pointPairLists = graphData.PointPairLists[i];
                int numSteps = pointPairLists.Count/2;
                for (int iStep = 0; iStep < pointPairLists.Count; iStep++)
                {
                    int step = iStep - numSteps;
                    var pointPairList = pointPairLists[iStep];
                    Color color;
                    var nodeGroup = docNode as TransitionGroupDocNode;
                    if (parentNode is PeptideDocNode)
                    {
                        int iColorGroup = GetColorIndex(nodeGroup, countLabelTypes, ref charge, ref iCharge);
                        color = COLORS_GROUPS[iColorGroup % COLORS_GROUPS.Length];
                    }
                    else if (displayType == DisplayTypeChrom.total)
                    {
                        color = COLORS_GROUPS[iColor%COLORS_GROUPS.Length];
                    }
                    else if (docNode.Equals(selectedNode) && step == 0)
                    {
                        color = ChromGraphItem.ColorSelected;
                    }
                    else
                    {
                        color = COLORS_TRANSITION[(iColor + colorOffset) % COLORS_TRANSITION.Length];
                    }
                    iColor++;
                    // If showing ratios, do not add the standard type to the graph,
                    // since it will always be empty, but make sure the colors still
                    // correspond with the other graphs.
                    if (nodeGroup != null && ratioIndex >= 0)
                    {
                        var labelType = nodeGroup.TransitionGroup.LabelType;
                        if (ReferenceEquals(labelType, standardType))
                            continue;
                    }

                    string label = graphData.DocNodeLabels[i];
                    if (step != 0)
                        label = string.Format(Resources.AreaReplicateGraphPane_UpdateGraph_Step__0_, step);
                    BarItem curveItem;
                    // Only use a MeanErrorBarItem if bars are not going to be stacked.
                    // TODO(nicksh): AreaGraphData.NormalizeTo does not know about MeanErrorBarItem 
                    if (BarSettings.Type != BarType.Stack && BarSettings.Type != BarType.PercentStack && normalizeData == AreaNormalizeToData.none)
                    {
                        curveItem = new MeanErrorBarItem(label, pointPairList, color, Color.Black);
                    }
                    else 
                    {
                        curveItem = new BarItem(label, pointPairList, color);
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

                    // Add area for this transition to each area entry
                    AddAreasToSums(pointPairList, sumAreas);

                    curveItem.Bar.Border.IsVisible = false;
                    curveItem.Bar.Fill.Brush = new SolidBrush(color);
                    curveItem.Tag = new IdentityPath(identityPath, docNode.Id);
                    CurveList.Add(curveItem);
                }
            }

            ParentGroupNode = parentGroupNode;
            SumAreas = sumAreas;

            // Draw a box around the currently selected replicate
            if (ShowSelection && maxArea >  -double.MaxValue)
            {
                double yValue;
                switch (BarSettings.Type)
                {
                    case BarType.Stack:
                        // The Math.Min(sumArea, .999) makes sure that if graph is in normalized view
                        // height of the selection rectangle does not exceed 1, so that top of the rectangle
                        // can be viewed when y-axis scale maximum is at 1
                        yValue = (areaView == AreaNormalizeToView.area_maximum_view ? Math.Min(sumArea, .999) : sumArea);
                        break;
                    case BarType.PercentStack:
                        yValue = 99.99;
                        break;
                    default:
                        // Scale the selection box to fit exactly the bar height
                        yValue = (areaView == AreaNormalizeToView.area_maximum_view ? Math.Min(maxArea, .999) : maxArea);
                        break;
                }
                GraphObjList.Add(new BoxObj(selectedReplicateIndex + .5, yValue, 0.99,
                                            yValue, Color.Black, Color.Empty)
                                     {
                                         IsClippedToChartRect = true,
                                     });
            }
            // Reset the scale when the parent node changes
            bool resetAxes = (_parentNode == null || !ReferenceEquals(_parentNode.Id, parentNode.Id));
            _parentNode = parentNode;

            UpdateAxes(resetAxes, aggregateOp, normalizeData, areaView, standardType);
        }

        private void UpdateAxes(bool resetAxes, GraphValues.AggregateOp aggregateOp, AreaNormalizeToData normalizeData,
            AreaNormalizeToView areaView, IsotopeLabelType standardType)
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
                if (normalizeData == AreaNormalizeToData.optimization)
                {
                    // If currently log scale or normalized to max, reset the y-axis max
                    if (YAxis.Type == AxisType.Log || YAxis.Scale.Max == 1)
                        YAxis.Scale.MaxAuto = true;

                    YAxis.Title.Text = aggregateOp.AnnotateTitle(Resources.AreaReplicateGraphPane_UpdateGraph_Percent_of_Regression_Peak_Area);
                    YAxis.Type = AxisType.Linear;
                    YAxis.Scale.MinAuto = false;
                    FixedYMin = YAxis.Scale.Min = 0;
                }
                else if (areaView == AreaNormalizeToView.area_maximum_view)
                {
                    YAxis.Scale.Max = 1;
                    if (IsDotProductVisible)
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
                    switch (areaView)
                    {
                        case AreaNormalizeToView.area_ratio_view:
                            yTitle = string.Format(Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area_Ratio_To__0_,
                                                   standardType.Title);
                            break;
                        case AreaNormalizeToView.area_global_standard_view:
                            yTitle = Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area_Ratio_To_Global_Standards;
                            break;
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
            Legend.IsVisible = Settings.Default.ShowPeakAreaLegend;
            AxisChange();

            // Reformat Y-Axis for labels and whiskers
            var maxY = GraphHelper.GetMaxY(CurveList,this);
            if (IsDotProductVisible)
            {
                var extraSpace = _lableHeight*(maxY/(Chart.Rect.Height - _lableHeight*2))*2;
                maxY += extraSpace;
            }
       
            GraphHelper.ReformatYAxis(this, maxY);
        }

        private void AddAreasToSums(PointPairList pointPairList, IList<double> sumAreas)
        {
            for (int i = 0; i < pointPairList.Count; i++)
            {
                PointPair pointPair = pointPairList[i];
                int index = i;
                if (pointPair.IsInvalid)
                    continue;

                if (IsExpectedVisible)
                {
                    // Skip finding the sumArea for the first bar if the library is showing
                    if (i == 0)
                        continue;

                    // offset index by 1, since (n + 1)th bar corresponds to the nth replicate
                    index--;
                }
                sumAreas[index] += pointPair.Y;
            }
        }

        private GraphObjList _dotpLabels;

        private IEnumerable<string> DotProductStrings
        {
            get
            {
                 return (_dotpLabels.Count > 0 || !IsDotProductVisible)
                     ? _dotpLabels.Select(l => ((TextObj)l).Text)
                     : SumAreas.Select((t, i) => GetDotProductResultsText(ParentGroupNode, i));
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
                // x shift of the dot product labels
                var xShift = 1;
                if (IsExpectedVisible)
                    // shift dot product labels over by 1 more, if library is visible
                    xShift++;

                for (int i = 0; i < sumAreas.Count; i++)
                {
                    string text = GetDotProductResultsText(nodeGroup, i);
                    if (string.IsNullOrEmpty(text))
                        continue;

                    TextObj textObj = new TextObj(text,
                                                  i + xShift, sumAreas[i],
                                                  CoordType.AxisXYScale,
                                                  AlignH.Center,
                                                  AlignV.Bottom)
                                          {
                                              IsClippedToChartRect = true,
                                              ZOrder = ZOrder.F_BehindGrid,
                                          };


                    textObj.FontSpec.Border.IsVisible = false;
                    textObj.FontSpec.Size = pointSize.Value;
                    _lableHeight =(int) textObj.FontSpec.GetHeight(CalcScaleFactor());
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

        private string GetDotProductResultsText(TransitionGroupDocNode nodeGroup, int indexResult)
        {
            var replicateIndices = IndexOfReplicate(indexResult);
            IEnumerable<float?> values;
            switch (ExpectedVisible)
            {
                case AreaExpectedValue.library:
                    if (replicateIndices.IsEmpty)
                    {
                        values = new[] {nodeGroup.GetLibraryDotProduct(-1)};
                    }
                    else
                    {
                        values = replicateIndices.Select(nodeGroup.GetLibraryDotProduct);
                    }
                    break;
                case AreaExpectedValue.isotope_dist:
                    if (replicateIndices.IsEmpty)
                    {
                        values = new[] {nodeGroup.GetIsotopeDotProduct(-1)};
                    }
                    else
                    {
                        values = replicateIndices.Select(nodeGroup.GetIsotopeDotProduct);
                    }
                    break;
                default:
                    return null;
            }
            var statistics = new Statistics(values
                .Select(value => value.HasValue ? (double?) value : null)
                .Where(value=>value.HasValue)
                .Cast<double>());
            if (statistics.Length == 0)
            {
                return null;
            }
            return GetDotProductText((float) statistics.Mean());
        }

        private string DotpLabelText
        {
            get
            {
                switch (ExpectedVisible)
                {
                    case AreaExpectedValue.library:
                        return "dotp"; // Not L10N
                    case AreaExpectedValue.isotope_dist:
                        return "idotp"; // Not L10N
                    default:
                        return string.Empty; 
                }
            }
        }

        private string GetDotProductText(float? dotpValue)
        {
            return dotpValue.HasValue ? string.Format("{0}\n{1:F02}", DotpLabelText, dotpValue) : null; // Not L10N
        }

        private void EmptyGraph(SrmDocument document)
        {
            string[] resultNames = GraphData.GetReplicateLabels(document).ToArray();

            XAxis.Scale.TextLabels = resultNames;
            var originalTextLabels = new string[XAxis.Scale.TextLabels.Length];
            Array.Copy(XAxis.Scale.TextLabels, originalTextLabels, XAxis.Scale.TextLabels.Length);
            OriginalXAxisLabels = originalTextLabels;
            
            ScaleAxisLabels();
            // Add a missing point for each replicate name.
            PointPairList pointPairList = new PointPairList();
            for (int i = 0; i < resultNames.Length; i++)
                pointPairList.Add(AreaGraphData.AreaPointPairMissing(i));
            AxisChange();
        }

        protected override int SelectedIndex
        {
            get
            {
                // If library is showing
                if (IsExpectedVisible)
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
            if (IsExpectedVisible)
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

        private enum AreaNormalizeToData { none, optimization, maximum_stack, maximum, total }

        /// <summary>
        /// Holds the data that is currently displayed in the graph.
        /// Currently, we don't hold onto this object, because we never need to look
        /// at the data after the graph is rendered.
        /// </summary>
        private class AreaGraphData : GraphData
        {
            public const int RATIO_INDEX_NONE = -1;

            public static PointPair AreaPointPairMissing(int xValue)
            {
                // Using PointPairBase.Missing caused too many problems in area graphs
                // Zero is essentially missing for column graphs, unlike the retention time hi-lo graphs
                return new PointPair(xValue, 0);
            }

            private readonly DocNode _docNode;
            private readonly int _ratioIndex;
            private readonly AreaNormalizeToData _normalize;
            private readonly AreaExpectedValue _expectedVisible;

            public AreaGraphData(SrmDocument document,
                                 DocNode docNode,
                                 DisplayTypeChrom displayType,
                                 GraphValues.ReplicateGroupOp replicateGroupOp,
                                 int ratioIndex,
                                 AreaNormalizeToData normalize,
                                 AreaExpectedValue expectedVisible,
                                 PaneKey paneKey)
                : base(document, docNode, displayType, replicateGroupOp, paneKey)
            {
                _docNode = docNode;
                _ratioIndex = ratioIndex;
                _normalize = normalize;
                _expectedVisible = expectedVisible;
            }

            protected override void InitData()
            {
                base.InitData();
         
                if (_expectedVisible != AreaExpectedValue.none)
                {
                    var nodeGroup = (TransitionGroupDocNode) _docNode;
                    var expectedIntensities = from nodeTran in GraphChromatogram.GetDisplayTransitions(nodeGroup, DisplayType)
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

                switch (_normalize)
                {
                    case AreaNormalizeToData.none:
                        // If library column is showing, make library column as tall as the tallest stack
                        if (_expectedVisible != AreaExpectedValue.none)
                            NormalizeMaxStack();
                        break;
                    case AreaNormalizeToData.optimization:
                        NormalizeOpt();
                        break;
                    case AreaNormalizeToData.maximum:
                        NormalizeMax();
                        break;
                    case AreaNormalizeToData.maximum_stack:
                        NormalizeMaxStack();
                        break;
                    case AreaNormalizeToData.total:
                        FixupForTotals();
                        break;
                }
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
            private void NormalizeTo(double denominator, double libraryHeight)
            {
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
                                // If library is displayed and the set of data to plot is at
                                // index 0 (where we store library intensity data)
                                // calculate the proportion of the denominator for each point
                                if (_expectedVisible != AreaExpectedValue.none && i == 0)
                                    pointPairList[i].Y *= (denominator/libraryHeight);
                                if(_normalize != AreaNormalizeToData.none)
                                    pointPairList[i].Y /= denominator;
                            }
                            else
                                pointPairList[i].Y = 0;
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
                                if (_expectedVisible != AreaExpectedValue.none && i == 0)
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

                // Finds the maximum bar height from the list of bar heights
                if (listTotals.Count != 0)
                {
                    double firstColumnHeight = listTotals[0];
                    // If the library column is visible, remove it before getting the max height
                    if (_expectedVisible != AreaExpectedValue.none)
                        listTotals.RemoveAt(0);
                    double maxBarHeight = listTotals.Aggregate(Math.Max);

                    // Normalizes each non-missing point by max bar height
                    NormalizeTo(maxBarHeight, _expectedVisible != AreaExpectedValue.none
                                                  ? firstColumnHeight
                                                  : 0);
                }
            }

            // Sets each missing point to be 0, so that the percent stack will show
            private void FixupForTotals()
            {
                NormalizeTo(1, 1);
            }

            public override PointPair PointPairMissing(int xValue)
            {
                return AreaPointPairMissing(xValue);
            }

            protected override bool IsMissingValue(TransitionChromInfoData chromInfo)
            {
                return false;
            }

            protected override PointPair CreatePointPair(int iResult, ICollection<TransitionChromInfoData> chromInfoDatas)
            {
                return ReplicateGroupOp.AggregateOp.MakeBarValue(iResult, 
                    chromInfoDatas.Select(chromInfoData => (double) (GetValue(chromInfoData.ChromInfo) ?? 0)));
            }

            protected override bool IsMissingValue(TransitionGroupChromInfoData chromInfoData)
            {
                return !GetValue(chromInfoData.ChromInfo).HasValue;
            }

            protected override PointPair CreatePointPair(int iResult, ICollection<TransitionGroupChromInfoData> chromInfoDatas)
            {
                return ReplicateGroupOp.AggregateOp.MakeBarValue(iResult, 
                    chromInfoDatas.Select(chromInfoData => (double) (GetValue(chromInfoData.ChromInfo) ?? 0)));
            }

            private float? GetValue(TransitionGroupChromInfo chromInfo)
            {
                if (chromInfo == null)
                    return null;
                if (_ratioIndex == RATIO_INDEX_NONE)
                    return chromInfo.Area;
                var ratioValue = chromInfo.GetRatio(_ratioIndex);
                return ratioValue == null ? (float?)null : ratioValue.Ratio;
            }

            private float? GetValue(TransitionChromInfo chromInfo)
            {
                if (chromInfo == null)
                    return null;
                if (_ratioIndex == RATIO_INDEX_NONE)
                    return chromInfo.Area;
                return chromInfo.GetRatio(_ratioIndex);
            }
        }
    }
}