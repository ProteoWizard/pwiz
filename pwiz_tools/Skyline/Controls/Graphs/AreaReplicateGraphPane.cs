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
    /// <summary>
    /// Graph pane which shows the comparison of retention times across the replicates.
    /// </summary>
    internal class AreaReplicateGraphPane : SummaryReplicateGraphPane
    {
        public AreaReplicateGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
        }

        private static BarType BarType
        {
            get
            {
                if (Settings.Default.AreaRatioView)
                    return BarType.Cluster;
                if (Settings.Default.AreaPercentView)
                    return BarType.PercentStack;
                return BarType.Stack;
            }
        }

        public override void UpdateGraph(bool checkData)
        {
            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            var results = document.Settings.MeasuredResults;
            bool resultsAvailable = results != null;
            Clear();
            string[] resultNames = GraphData.GetReplicateNames(document).ToArray();
            XAxis.Scale.TextLabels = resultNames;
            ScaleAxisLabels();

            if (!resultsAvailable)
            {
                Title.Text = "No results available";
                AxisChange();
                return;
            }

            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode == null)
            {
                // Add a missing point for each replicate name.
                PointPairList pointPairList = new PointPairList();
                for (int i = 0; i < resultNames.Length; i++)
                    pointPairList.Add(AreaGraphData.AreaPointPairMissing(i));
                AxisChange();
                return;
            }

            BarSettings.Type = BarType;
            Title.Text = null;

            DisplayTypeChrom displayType = GraphChromatogram.DisplayType;
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
                var children = ((DocNodeParent) selectedNode).Children;
                if (children.Count == 1 && displayType != DisplayTypeChrom.total)
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
                Title.Text = "Select a peptide to see the peak area graph";
                return;
            }

            // If a precursor is going to be displayed with display type single
            if (parentNode is TransitionGroupDocNode && displayType == DisplayTypeChrom.single)
            {
                // If no optimization data, then show all the transitions
                if (!optimizationPresent)
                    displayType = DisplayTypeChrom.all;
                // Otherwise, do not stack the bars
                else
                    BarSettings.Type = BarType.Cluster;
            }

            // Normalize optimization data, if it is being shown, and normalization has been chosen.
            bool normalizeOpt = (optimizationPresent && displayType == DisplayTypeChrom.single &&
                                 Settings.Default.AreaPercentView);
            int ratioIndex = -1;
            var standardType = IsotopeLabelType.light;
            if (Settings.Default.AreaRatioView)
            {
                ratioIndex = GraphSummary.RatioIndex;
                standardType = document.Settings.PeptideSettings.Modifications.InternalStandardTypes[ratioIndex];                
            }

            GraphData graphData = new AreaGraphData(parentNode, displayType, ratioIndex, normalizeOpt);

            int selectedReplicateIndex = GraphSummary.ResultsIndex;
            double maxArea = -double.MaxValue;
            double sumArea = 0;
            int iColor = 0;
            for (int i = 0; i < graphData.DocNodes.Count; i++)
            {
                var docNode = graphData.DocNodes[i];
                var pointPairLists = graphData.PointPairLists[i];
                int numSteps = pointPairLists.Count/2;
                for (int iStep = 0; iStep < pointPairLists.Count; iStep++)
                {
                    int step = iStep - numSteps;
                    var pointPairList = pointPairLists[iStep];
                    Color color;
                    if (parentNode is PeptideDocNode || displayType == DisplayTypeChrom.total)
                    {
                        color = COLORS_GROUPS[iColor%COLORS_GROUPS.Length];
                    }
                    else if (docNode.Equals(selectedNode) && step == 0)
                    {
                        color = ChromGraphItem.ColorSelected;
                    }
                    else
                    {
                        color = COLORS_TRANSITION[iColor%COLORS_TRANSITION.Length];
                    }
                    iColor++;
                    // If showing ratios, do not add the standard type to the graph,
                    // since it wiall always be empty, but make sure the colors still
                    // correspond with the other graphs.
                    var nodeGroup = docNode as TransitionGroupDocNode;
                    if (nodeGroup != null && ratioIndex != -1)
                    {
                        var labelType = nodeGroup.TransitionGroup.LabelType;
                        if (ReferenceEquals(labelType, standardType))
                            continue;
                    }

                    string label = graphData.DocNodeLabels[i];
                    if (step != 0)
                        label = string.Format("Step {0}", step);
                    var curveItem = new BarItem(label, pointPairList, color);

                    if (selectedReplicateIndex < pointPairList.Count)
                    {
                        PointPair pointPair = pointPairList[selectedReplicateIndex];
                        if (!pointPair.IsInvalid)
                        {
                            sumArea += pointPair.Y;
                            maxArea = Math.Max(maxArea, pointPair.Y);
                        }
                    }
                    curveItem.Bar.Border.IsVisible = false;
                    curveItem.Bar.Fill.Brush = new SolidBrush(color);
                    curveItem.Tag = new IdentityPath(identityPath, docNode.Id);
                    CurveList.Add(curveItem);
                }
            }
            // Draw a box around the currently selected replicate
            if (ShowSelection && maxArea >  -double.MaxValue)
            {
                double yValue;
                switch (BarSettings.Type)
                {
                    case BarType.Stack:
                        yValue = sumArea;
                        break;
                    case BarType.PercentStack:
                        yValue = 99.99;
                        break;
                    default:
                        yValue = maxArea;
                        break;
                }
                GraphObjList.Add(new BoxObj(selectedReplicateIndex + .5, yValue, 0.99,
                                            yValue, Color.Black, Color.Empty)
                                     {
                                         IsClippedToChartRect = true,
                                     });
            }
            // Reset the scale when the parent node changes
            if (_parentNode != parentNode)
            {
                _parentNode = parentNode;
                XAxis.Scale.MaxAuto = XAxis.Scale.MinAuto = true;
                YAxis.Scale.MaxAuto = true;
            }
            if (BarSettings.Type == BarType.PercentStack)
            {
                YAxis.Scale.Max = 100;
                YAxis.Scale.MaxAuto = false;
                YAxis.Title.Text = "Peak Area Percentage";
                YAxis.Type = AxisType.Linear;
                YAxis.Scale.MinAuto = false;
                FixedYMin = YAxis.Scale.Min = 0;
            }
            else
            {
                if (normalizeOpt)
                {
                    // If currently log scale, reset the y-axis max
                    if (YAxis.Type == AxisType.Log)
                        YAxis.Scale.MaxAuto = true;

                    YAxis.Title.Text = "Percent of Regression Peak Area";
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

                    YAxis.Title.Text = "Log Peak Area";
                    YAxis.Type = AxisType.Log;
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

                    if (Settings.Default.AreaRatioView)
                        YAxis.Title.Text = string.Format("Peak Area Ratio To {0}", standardType.Title);
                    else
                        YAxis.Title.Text = "Peak Area";
                    YAxis.Type = AxisType.Linear;
                    YAxis.Scale.MinAuto = false;
                    FixedYMin = YAxis.Scale.Min = 0;
                }
                if (!YAxis.Scale.MaxAuto && YAxis.Scale.Max == 100)
                    YAxis.Scale.MaxAuto = true;
            }
            AxisChange();
        }

        /// <summary>
        /// Holds the data that is currently displayed in the graph.
        /// Currently, we don't hold onto this object, because we never need to look
        /// at the data after the graph is rendered.
        /// </summary>
        private class AreaGraphData : GraphData
        {
            public static PointPair AreaPointPairMissing(int xValue)
            {
                return new PointPair(xValue, PointPairBase.Missing);                
            }

            private readonly int _ratioIndex;
            private readonly bool _normalize;

            public AreaGraphData(DocNode docNode, DisplayTypeChrom displayType, int ratioIndex, bool normalize)
                : base(docNode, displayType)
            {
                _ratioIndex = ratioIndex;
                _normalize = normalize;
            }

            protected override void InitData()
            {
                base.InitData();

                if (_normalize)
                    Normalize();
            }

            private void Normalize()
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
                    for (int j = 0; j < pointPairListRegression.Count; j++)
                    {
                        // If it is missing, leave it missing.
                        double regressionValue = pointPairListRegression[j].Y;
                        if (regressionValue != PointPairBase.Missing && regressionValue != 0)
                            pointPairListRegression[j].Y = 100;
                    }
                }                
            }

            public override PointPair PointPairMissing(int xValue)
            {
                return AreaPointPairMissing(xValue);
            }

            protected override bool IsMissingValue(TransitionChromInfo chromInfo)
            {
                // TODO: Understand why chromInfo.IsEmpty breaks the area graphs
                return false; // chromInfo.IsEmpty;
            }

            protected override PointPair CreatePointPair(int iResult, TransitionChromInfo chromInfo)
            {
                float? pointY = GetValue(chromInfo);
                return new PointPair(iResult, pointY.HasValue ? pointY.Value : 0);
            }

            protected override bool IsMissingValue(TransitionGroupChromInfo chromInfo)
            {
                return !GetValue(chromInfo).HasValue;
            }

            protected override PointPair CreatePointPair(int iResult, TransitionGroupChromInfo chromInfo)
            {
                return new PointPair(iResult, GetValue(chromInfo).Value);
            }

            private float? GetValue(TransitionGroupChromInfo chromInfo)
            {
                return (_ratioIndex == -1 ? chromInfo.Area : chromInfo.Ratios[_ratioIndex]);
            }

            private float? GetValue(TransitionChromInfo chromInfo)
            {
                return (_ratioIndex == -1 ? chromInfo.Area : chromInfo.Ratios[_ratioIndex]);
            }
        }
    }
}