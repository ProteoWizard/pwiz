/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Graph pane which shows the comparison of retention times across the replicates.
    /// </summary>
    internal class RTReplicateGraphPane : SummaryReplicateGraphPane, IUpdateGraphPaneController
    {
        public RTReplicateGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            YAxis.Title.Text = Resources.RTReplicateGraphPane_RTReplicateGraphPane_Measured_Time;
        }

        public bool UpdateUIOnLibraryChanged()
        {
            return true;
        }
        public bool UpdateUIOnIndexChanged()
        {
            return true;
        }

        public bool CanShowRTLegend { get; private set; }
        public override void UpdateGraph(bool selectionChanged)
        {
            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            var results = document.Settings.MeasuredResults;
            bool resultsAvailable = results != null;
            Clear();
            if (!resultsAvailable)
            {
                Title.Text = Resources.RTReplicateGraphPane_UpdateGraph_No_results_available;
                EmptyGraph(document);
                return;
            }

            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode ??
                                   GraphSummary.StateProvider.SelectedNodes.OfType<SrmTreeNode>().FirstOrDefault();

            if (selectedTreeNode == null || document.FindNode(selectedTreeNode.Path) == null)
            {
                EmptyGraph(document);
                return;
            }

            Title.Text = null;

            DisplayTypeChrom displayType = GraphChromatogram.GetDisplayType(document, selectedTreeNode);
            DocNode selectedNode = selectedTreeNode.Model;
            IdentityPath selectedPath = selectedTreeNode.Path;
            DocNode parentNode = selectedNode;
            IdentityPath parentPath = selectedTreeNode.Path;
            // If the selected tree node is a transition, then its siblings are displayed.
            if (selectedTreeNode is TransitionTreeNode)
            {
                if (displayType != DisplayTypeChrom.single)
                {
                    SrmTreeNode parentTreeNode = selectedTreeNode.SrmParent;
                    parentNode = parentTreeNode.Model;
                    selectedPath = parentTreeNode.Path;
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
                    selectedPath = new IdentityPath(parentPath, children[0].Id);
                }
            }
            else if (!(selectedTreeNode is PeptideGroupTreeNode) && !(selectedTreeNode is TransitionGroupTreeNode))
            {
                Title.Text = Resources.RTReplicateGraphPane_UpdateGraph_Select_a_peptide_to_see_the_retention_time_graph;
                CanShowRTLegend = false;
                return;
            }

            // If a precursor is going to be displayed with display type single
            if (parentNode is TransitionGroupDocNode && displayType == DisplayTypeChrom.single)
            {
                // If no optimization data, then show all the transitions
                if (!results.Chromatograms.Contains(chrom => chrom.OptimizationFunction != null))
                    displayType = DisplayTypeChrom.all;
            }
            var rtTransformOp = GraphSummary.StateProvider.GetRetentionTimeTransformOperation();
            var rtValue = RTPeptideGraphPane.RTValue;
            ReplicateGroupOp replicateGroupOp;
            if (rtValue == RTPeptideValue.All)
            {
                replicateGroupOp = ReplicateGroupOp.FromCurrentSettings(document, GraphValues.AggregateOp.MEAN);
            }
            else
            {
                replicateGroupOp = ReplicateGroupOp.FromCurrentSettings(document);
            }
            var retentionTimeValue = new GraphValues.RetentionTimeTransform(rtValue, rtTransformOp, replicateGroupOp.AggregateOp);
            YAxis.Title.Text = retentionTimeValue.GetAxisTitle();

            var peptidePaths = GetSelectedPeptides().GetUniquePeptidePaths().ToList();
            // if PeptideGroupTreeNode is selected but has only one child isMultiSelect should still be true
            IsMultiSelect = peptidePaths.Count > 1 ||
                (peptidePaths.Count == 1 &&
                GraphSummary.StateProvider.SelectedNodes.FirstOrDefault() is PeptideGroupTreeNode);

            GraphData graphData = new RTGraphData(document, 
                IsMultiSelect 
                ? peptidePaths
                : new[] { selectedPath }.AsEnumerable(), displayType, retentionTimeValue, replicateGroupOp);
            CanShowRTLegend = graphData.DocNodes.Count != 0;
            InitFromData(graphData);

            int selectedReplicateIndex = SelectedIndex;
            double minRetentionTime = double.MaxValue;
            double maxRetentionTime = -double.MaxValue;
            int iColor = 0, iCharge = -1;
            var charge = Adduct.EMPTY;
            int countLabelTypes = document.Settings.PeptideSettings.Modifications.CountLabelTypes;
            int colorOffset = 0;
            var transitionGroupDocNode = parentNode as TransitionGroupDocNode;
            if (transitionGroupDocNode != null && displayType == DisplayTypeChrom.products)
            {
                // If we are only displaying product ions, we want to use an offset in the colors array
                // so that we do not re-use colors that would be used for any precursor ions.
                colorOffset =
                    GraphChromatogram.GetDisplayTransitions(transitionGroupDocNode, DisplayTypeChrom.precursors).Count();
            }
            for (int i = 0; i < graphData.DocNodes.Count; i++)
            {
                var docNode = graphData.DocNodes[i];
                var identityPath = graphData.DocNodePaths[i];
                var pointPairLists = graphData.PointPairLists[i];
                int numSteps = pointPairLists.Count / 2;
                for (int iStep = 0; iStep < pointPairLists.Count; iStep++)
                {
                    int step = iStep - numSteps;
                    var pointPairList = pointPairLists[iStep];
                    Color color;
                    var isSelected = false;
                    var nodeGroup = docNode as TransitionGroupDocNode;
                    if (IsMultiSelect)
                    {
                        var peptides = peptidePaths.Select(path => document.FindNode(path))
                            .Cast<PeptideDocNode>().ToArray();
                        var peptideDocNode = peptides.FirstOrDefault(
                            peptide => 0 <= peptide.FindNodeIndex(docNode.Id));
                        if (peptideDocNode == null)
                        {
                            continue;
                        }
                        color = GraphSummary.StateProvider.GetPeptideGraphInfo(peptideDocNode).Color;
                        if (identityPath.Equals(selectedTreeNode.Path) && step == 0)
                        {
                            color = ChromGraphItem.ColorSelected;
                            isSelected = true;
                        }
                    }
                    else if (parentNode is PeptideDocNode)
                    {
                        // Resharper code inspection v9.0 on TC gets this one wrong
                        // ReSharper disable ExpressionIsAlwaysNull
                        int iColorGroup = GetColorIndex(nodeGroup, countLabelTypes, ref charge, ref iCharge);
                        // ReSharper restore ExpressionIsAlwaysNull
                        color = COLORS_GROUPS[iColorGroup % COLORS_GROUPS.Count];
                    }
                    else if (displayType == DisplayTypeChrom.total)
                    {
                        color = COLORS_GROUPS[iColor%COLORS_GROUPS.Count];
                    }
                    else if (ReferenceEquals(docNode, selectedNode) && step == 0)
                    {
                        color = ChromGraphItem.ColorSelected;
                        isSelected = true;
                    }
                    else
                    {
                        color = COLORS_TRANSITION[(iColor + colorOffset)%COLORS_TRANSITION.Count];
                    }
                    iColor++;

                    string label = graphData.DocNodeLabels[i];
                    if (step != 0)
                        label = string.Format(Resources.RTReplicateGraphPane_UpdateGraph_Step__0__, step);
                    
                    CurveItem curveItem;
                    if(IsMultiSelect)
                    {
                        if (rtValue != RTPeptideValue.All)
                            curveItem = CreateLineItem(label, pointPairList, color);
                        else
                            curveItem = CreateMultiSelectBarItem(label, pointPairList, color);
                    }
                    else if (HiLowMiddleErrorBarItem.IsHiLoMiddleErrorList(pointPairList))
                    {
                        curveItem = new HiLowMiddleErrorBarItem(label, pointPairList, color, Color.Black);
                        BarSettings.Type = BarType.Cluster;
                    }
                    else if (rtValue == RTPeptideValue.All)
                    {
                        curveItem = new MeanErrorBarItem(label, pointPairList, color, Color.Black);
                        BarSettings.Type = BarType.Cluster;
                    }
                    else
                    {
                        curveItem = CreateLineItem(label, pointPairList, color);
                    }

                    if (curveItem != null)
                    {
                        curveItem.Tag = identityPath;

                        var barItem = curveItem as BarItem;
                        if (barItem != null)
                        {
                            barItem.Bar.Border.IsVisible = false;
                            barItem.Bar.Fill.Brush = GetBrushForNode(document.Settings, docNode, color);
                            if (!isSelected)
                                barItem.SortedOverlayPriority = 1;
                        }
                        CurveList.Add(curveItem);

                        if (selectedReplicateIndex != -1 && selectedReplicateIndex < pointPairList.Count)
                        {
                            PointPair pointPair = pointPairList[selectedReplicateIndex];
                            if (!pointPair.IsInvalid)
                            {
                                minRetentionTime = Math.Min(minRetentionTime, pointPair.Z);
                                maxRetentionTime = Math.Max(maxRetentionTime, pointPair.Y);
                            }
                        }
                    }
                }
            }
            // Draw a box around the currently selected replicate
            if (ShowSelection && minRetentionTime != double.MaxValue)
            {
                AddSelection(selectedReplicateIndex, maxRetentionTime, minRetentionTime);
            }
            // Reset the scale when the parent node changes
            if (_parentNode == null || !ReferenceEquals(_parentNode.Id, parentNode.Id))
            {
                XAxis.Scale.MaxAuto = XAxis.Scale.MinAuto = true;
                YAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = true;
            }
            _parentNode = parentNode;
            Legend.IsVisible = !IsMultiSelect && Settings.Default.ShowRetentionTimesLegend;
            GraphSummary.GraphControl.Invalidate();
            AxisChange();
        }

        private void AddSelection(int selectedReplicateIndex, double maxRetentionTime, double minRetentionTime)
        {
            bool selectLines = CurveList.Any(c => c is LineItem);
            if (selectLines)
            {
                GraphObjList.Add(new LineObj(Color.Black, selectedReplicateIndex + 1, 0, selectedReplicateIndex + 1,
                    maxRetentionTime)
                {
                    IsClippedToChartRect = true,
                    Line = new Line {Width = 2, Color = Color.Black, Style = DashStyle.Dash}
                });
            }
            else
            {
                GraphObjList.Add(new BoxObj(selectedReplicateIndex + .5, maxRetentionTime, 1,
                    maxRetentionTime - minRetentionTime, Color.Black, Color.Empty)
                {
                    IsClippedToChartRect = true,
                });
            }
        }

        private CurveItem CreateMultiSelectBarItem(string label, PointPairList pointPairList, Color color)
        {
            for (var index = 0; index < pointPairList.Count; index++)
            {
                PointPair pp = pointPairList[index];
                var middle = pp.Y - (pp.Y - pp.LowValue)/2;
                if (!double.IsNaN(middle))
                    pp.Tag = new MiddleErrorTag(middle, 0);
            }

            var curveItem = new HiLowMiddleErrorBarItem(label, pointPairList, color, color);
            BarSettings.Type = BarType.SortedOverlay;
            return curveItem;
        }

        private PeptidesAndTransitionGroups GetSelectedPeptides()
        {
            return PeptidesAndTransitionGroups.Get(GraphSummary.StateProvider.SelectedNodes, GraphSummary.ResultsIndex, 100);
        }

        protected override PointPair PointPairMissing(int xValue)
        {
            return RTGraphData.RTPointPairMissing(xValue);
        }

        /// <summary>
        /// Holds the data that is currently displayed in the graph.
        /// Currently, we don't hold onto this object, because we never need to look
        /// at the data after the graph is rendered.
        /// </summary>
        private class RTGraphData : GraphData
        {
            public static PointPair RTPointPairMissing(int xValue)
            {
                return HiLowMiddleErrorBarItem.MakePointPair(xValue,
                        PointPairBase.Missing, PointPairBase.Missing, PointPairBase.Missing, 0);
            }

            public RTGraphData(SrmDocument document, IEnumerable<IdentityPath> selectedDocNodePaths, DisplayTypeChrom displayType, GraphValues.RetentionTimeTransform retentionTimeTransform, ReplicateGroupOp replicateGroupOp)
                : base(document, selectedDocNodePaths, displayType, replicateGroupOp, PaneKey.DEFAULT)
            {
                RetentionTimeTransform = retentionTimeTransform;
            }

            private GraphValues.RetentionTimeTransform RetentionTimeTransform { get; set; }

            public override PointPair PointPairMissing(int xValue)
            {
                return RTPointPairMissing(xValue);
            }

            private bool IsMissingAlignment(ChromInfoData chromInfoData)
            {
                Assume.IsNotNull(RetentionTimeTransform, @"RetentionTimeTransform");
                if (null == RetentionTimeTransform.RtTransformOp)
                {
                    return false;
                }
                Assume.IsNotNull(chromInfoData, @"chromInfoData");
                Assume.IsNotNull(chromInfoData.ChromFileInfo, @"chromInfoData.ChromFileInfo");
                IRegressionFunction regressionFunction;
                return !RetentionTimeTransform.RtTransformOp.TryGetRegressionFunction(chromInfoData.ChromFileInfo.FileId, out regressionFunction);
            }

            protected override bool IsMissingValue(TransitionChromInfoData chromInfoData)
            {
                return IsMissingAlignment(chromInfoData) || null == GetRetentionTimeValues(chromInfoData);
            }

            private PointPair CalculatePointPair<TChromInfoData>(int iResult, IEnumerable<TChromInfoData> chromInfoDatas, Func<TChromInfoData, RetentionTimeValues?> getRetentionTimeValues) 
                where TChromInfoData : ChromInfoData
            {
                var startTimes = new List<double>();
                var endTimes = new List<double>();
                var retentionTimes = new List<double>();
                var fwhms = new List<double>();
                foreach (var chromInfoData in chromInfoDatas)
                {
                    var retentionTimeValues = getRetentionTimeValues(chromInfoData).GetValueOrDefault();
                    IRegressionFunction regressionFunction = null;
                    if (null != RetentionTimeTransform.RtTransformOp)
                    {
                        RetentionTimeTransform.RtTransformOp.TryGetRegressionFunction(chromInfoData.ChromFileInfo.FileId, out regressionFunction);
                    }
                    if (regressionFunction == null)
                    {
                        startTimes.Add(retentionTimeValues.StartRetentionTime);
                        endTimes.Add(retentionTimeValues.EndRetentionTime);
                        retentionTimes.Add(retentionTimeValues.RetentionTime);
                        fwhms.Add(retentionTimeValues.Fwhm ?? 0);
                    }
                    else
                    {
                        startTimes.Add(regressionFunction.GetY(retentionTimeValues.StartRetentionTime));
                        endTimes.Add(regressionFunction.GetY(retentionTimeValues.EndRetentionTime));
                        retentionTimes.Add(regressionFunction.GetY(retentionTimeValues.RetentionTime));
                        if (retentionTimeValues.Fwhm.HasValue)
                        {
                            fwhms.Add(regressionFunction.GetY(retentionTimeValues.RetentionTime +
                                                              retentionTimeValues.Fwhm.Value/2)
                                      -
                                      regressionFunction.GetY(retentionTimeValues.RetentionTime -
                                                              retentionTimeValues.Fwhm.Value/2));
                        }
                        else
                        {
                            fwhms.Add(0);
                        }
                    }
                }
                if (RTPeptideValue.All == RTPeptideGraphPane.RTValue)
                {
                    var point = HiLowMiddleErrorBarItem.MakePointPair(iResult, 
                        new Statistics(endTimes).Mean(), 
                        new Statistics(startTimes).Mean(), 
                        new Statistics(retentionTimes).Mean(), 
                        new Statistics(fwhms).Mean());
                    return point.IsInvalid ? PointPairMissing(iResult) : point;
                }
                IEnumerable<double> values;
                switch (RTPeptideGraphPane.RTValue)
                {
                    case RTPeptideValue.FWB:
                        values = startTimes.Select((startTime, index) => endTimes[index] - startTime);
                        break;
                    case RTPeptideValue.FWHM:
                        values = fwhms;
                        break;
                    default:
                        values = retentionTimes;
                        break;
                }
                return RetentionTimeTransform.AggregateOp.MakeBarValue(iResult, values);
            }

            protected override PointPair CreatePointPair(int iResult, ICollection<TransitionChromInfoData> chromInfoDatas)
            {
                return CalculatePointPair(iResult, chromInfoDatas, GetRetentionTimeValues);
            }

            protected override bool IsMissingValue(TransitionGroupChromInfoData chromInfoData)
            {
                return IsMissingAlignment(chromInfoData) || !GetRetentionTimeValues(chromInfoData).HasValue;
            }

            protected override PointPair CreatePointPair(int iResult, ICollection<TransitionGroupChromInfoData> chromInfoDatas)
            {
                return CalculatePointPair(iResult, chromInfoDatas, GetRetentionTimeValues);
            }

            private RetentionTimeValues? GetRetentionTimeValues(TransitionChromInfoData transitionChromInfoData)
            {
                return transitionChromInfoData.GetRetentionTimes();
            }

            private RetentionTimeValues? GetRetentionTimeValues(TransitionGroupChromInfoData transitionGroupChromInfoData)
            {
                return transitionGroupChromInfoData.GetRetentionTimes();
            }
        }
    }
}
