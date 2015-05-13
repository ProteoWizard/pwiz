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
    internal class RTReplicateGraphPane : SummaryReplicateGraphPane
    {
        public RTReplicateGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            YAxis.Title.Text = Resources.RTReplicateGraphPane_RTReplicateGraphPane_Measured_Time;
        }

        public bool CanShowRTLegend { get; private set; }

        public override void UpdateGraph(bool checkData)
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
            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode == null || document.FindNode(selectedTreeNode.Path) == null)
            {
                EmptyGraph(document);
                return;
            }

            Title.Text = null;

            DisplayTypeChrom displayType = GraphChromatogram.GetDisplayType(document, selectedTreeNode);
            DocNode selectedNode = selectedTreeNode.Model;
            DocNode parentNode = selectedNode;
            IdentityPath identityPath = selectedTreeNode.Path;
            // If the selected tree node is a transition, then its siblings are displayed.
            if (selectedTreeNode is TransitionTreeNode)
            {
                if (displayType != DisplayTypeChrom.single)
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
            }
            else if (!(selectedTreeNode is TransitionGroupTreeNode))
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
            GraphValues.ReplicateGroupOp replicateGroupOp;
            if (rtValue == RTPeptideValue.All)
            {
                replicateGroupOp = GraphValues.ReplicateGroupOp.FromCurrentSettings(document.Settings, GraphValues.AggregateOp.MEAN);
            }
            else
            {
                replicateGroupOp = GraphValues.ReplicateGroupOp.FromCurrentSettings(document.Settings);
            }
            var retentionTimeValue = new GraphValues.RetentionTimeTransform(rtValue, rtTransformOp, replicateGroupOp.AggregateOp);
            YAxis.Title.Text = retentionTimeValue.GetAxisTitle();
            GraphData graphData = new RTGraphData(document, parentNode, displayType, retentionTimeValue, replicateGroupOp);
            CanShowRTLegend = graphData.DocNodes.Count != 0;
            InitFromData(graphData);

            int selectedReplicateIndex = SelectedIndex;
            double minRetentionTime = double.MaxValue;
            double maxRetentionTime = -double.MaxValue;
            int iColor = 0, iCharge = -1;
            int? charge = null;
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
                var pointPairLists = graphData.PointPairLists[i];
                int numSteps = pointPairLists.Count / 2;
                for (int iStep = 0; iStep < pointPairLists.Count; iStep++)
                {
                    int step = iStep - numSteps;
                    var pointPairList = pointPairLists[iStep];
                    Color color;
                    var nodeGroup = docNode as TransitionGroupDocNode;
                    if (parentNode is PeptideDocNode)
                    {
                        // Resharper code inspection v9.0 on TC gets this one wrong
                        // ReSharper disable ExpressionIsAlwaysNull
                        int iColorGroup = GetColorIndex(nodeGroup, countLabelTypes, ref charge, ref iCharge);
                        // ReSharper restore ExpressionIsAlwaysNull
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
                        color = COLORS_TRANSITION[(iColor + colorOffset)%COLORS_TRANSITION.Length];
                    }
                    iColor++;

                    string label = graphData.DocNodeLabels[i];
                    if (step != 0)
                        label = string.Format(Resources.RTReplicateGraphPane_UpdateGraph_Step__0__, step);
                    BarItem curveItem;
                    if (HiLowMiddleErrorBarItem.IsHiLoMiddleErrorList(pointPairList))
                    {
                        curveItem = new HiLowMiddleErrorBarItem(label, pointPairList, color, Color.Black);
                    }
                    else
                    {
                        curveItem = new MeanErrorBarItem(label, pointPairList, color, Color.Black);
                    }
                    if (selectedReplicateIndex != -1 && selectedReplicateIndex < pointPairList.Count)
                    {
                        PointPair pointPair = pointPairList[selectedReplicateIndex];
                        if (!pointPair.IsInvalid)
                        {
                            minRetentionTime = Math.Min(minRetentionTime, pointPair.Z);
                            maxRetentionTime = Math.Max(maxRetentionTime, pointPair.Y);
                        }
                    }
                    curveItem.Bar.Border.IsVisible = false;
                    curveItem.Bar.Fill.Brush = new SolidBrush(color);
                    curveItem.Tag = new IdentityPath(identityPath, docNode.Id);
                    CurveList.Add(curveItem);
                }
            }
            // Draw a box around the currently selected replicate
            if (ShowSelection && minRetentionTime != double.MaxValue)
            {
                GraphObjList.Add(new BoxObj(selectedReplicateIndex + .5, maxRetentionTime, 1,
                                            maxRetentionTime - minRetentionTime, Color.Black, Color.Empty)
                                     {
                                         IsClippedToChartRect = true,
                                     });
            }
            // Reset the scale when the parent node changes
            if (_parentNode == null || !ReferenceEquals(_parentNode.Id, parentNode.Id))
            {
                XAxis.Scale.MaxAuto = XAxis.Scale.MinAuto = true;
                YAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = true;
            }
            _parentNode = parentNode;
            Legend.IsVisible = Settings.Default.ShowRetentionTimesLegend;
            AxisChange();
        }

        private void EmptyGraph(SrmDocument document)
        {
            string[] resultNames = GraphData.GetReplicateLabels(document).ToArray();
            XAxis.Scale.TextLabels = resultNames;
            ScaleAxisLabels();
            // Add a missing point for each replicate name.
            PointPairList pointPairList = new PointPairList();
            for (int i = 0; i < resultNames.Length; i++)
                pointPairList.Add(RTGraphData.RTPointPairMissing(i));
            AxisChange();
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

            public RTGraphData(SrmDocument document, DocNode docNode, DisplayTypeChrom displayType, GraphValues.RetentionTimeTransform retentionTimeTransform, GraphValues.ReplicateGroupOp replicateGroupOp)
                : base(document, docNode, displayType, replicateGroupOp, PaneKey.DEFAULT)
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
                Assume.IsNotNull(RetentionTimeTransform, "RetentionTimeTransform"); // Not L10N
                if (null == RetentionTimeTransform.RtTransformOp)
                {
                    return false;
                }
                Assume.IsNotNull(chromInfoData, "chromInfoData"); // Not L10N
                Assume.IsNotNull(chromInfoData.ChromFileInfo, "chromInfoData.ChromFileInfo"); // Not L10N
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
                    return HiLowMiddleErrorBarItem.MakePointPair(iResult, 
                        new Statistics(endTimes).Mean(), 
                        new Statistics(startTimes).Mean(), 
                        new Statistics(retentionTimes).Mean(), 
                        new Statistics(fwhms).Mean());
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