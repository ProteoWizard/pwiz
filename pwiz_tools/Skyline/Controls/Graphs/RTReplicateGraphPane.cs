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
using System.Drawing;
using System.Linq;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
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
        }

        public bool CanShowRTLegend { get; private set; }

        public int AlignToReplicate
        {
            get { return GraphSummary.StateProvider.AlignToReplicate; }
        }

        public override void UpdateGraph(bool checkData)
        {
            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            var results = document.Settings.MeasuredResults;
            bool resultsAvailable = results != null;
            Clear();
            if (!resultsAvailable)
            {
                Title.Text = "No results available";
                EmptyGraph(document);
                return;
            }
            ChromatogramSet alignToChromatogramSet = null;
            if (AlignToReplicate >= 0)
            {
                alignToChromatogramSet = document.Settings.MeasuredResults.Chromatograms[AlignToReplicate];
            }
            if (alignToChromatogramSet != null)
            {
                YAxis.Title.Text = string.Format("Time aligned to {0}", alignToChromatogramSet.Name);
            }
            else
            {
                YAxis.Title.Text = "Measured Time";
            }

            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode == null || document.FindNode(selectedTreeNode.Path) == null)
            {
                EmptyGraph(document);
                return;
            }

            Title.Text = null;

            DisplayTypeChrom displayType = GraphChromatogram.GetDisplayType(document);
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
                Title.Text = "Select a peptide to see the retention time graph";
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
            FileRetentionTimeAlignments fileRetentionTimeAlignments = null;
            if (AlignToReplicate >= 0)
            {
                ChromFileInfo chromFileInfo = ReplicateIndexToChromFileInfo(results, parentNode, AlignToReplicate);
                if (chromFileInfo != null)
                {
                    fileRetentionTimeAlignments = document.Settings.DocumentRetentionTimes.FileAlignments.Find(chromFileInfo);
                }
            }
            if (alignToChromatogramSet != null && fileRetentionTimeAlignments == null)
            {
                Title.Text = "Unable to align retention times";
                EmptyGraph(document);
                return;
            }
            GraphData graphData = new RTGraphData(document, parentNode, displayType);
            CanShowRTLegend = graphData.DocNodes.Count != 0;
            InitFromData(graphData);

            int selectedReplicateIndex = SelectedIndex;
            double minRetentionTime = double.MaxValue;
            double maxRetentionTime = -double.MaxValue;
            int iColor = 0, iCharge = -1, charge = -1;
            int countLabelTypes = document.Settings.PeptideSettings.Modifications.CountLabelTypes;
            for (int i = 0; i < graphData.DocNodes.Count; i++)
            {
                var docNode = graphData.DocNodes[i];
                var pointPairLists = graphData.PointPairLists[i];
                int numSteps = pointPairLists.Count / 2;
                for (int iStep = 0; iStep < pointPairLists.Count; iStep++)
                {
                    int step = iStep - numSteps;
                    var pointPairList = pointPairLists[iStep];
                    pointPairList = AlignPointPairList(fileRetentionTimeAlignments, results, parentNode, graphData, pointPairList);
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
                        color = COLORS_TRANSITION[iColor%COLORS_TRANSITION.Length];
                    }
                    iColor++;

                    string label = graphData.DocNodeLabels[i];
                    if (step != 0)
                        label = string.Format("Step {0}", step);
                    var curveItem = new HiLowMiddleErrorBarItem(label, pointPairList, color, Color.Black);
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

        private ChromFileInfo ReplicateIndexToChromFileInfo(MeasuredResults measuredResults, DocNode docNode, int replicateIndex)
        {
            var peptideDocNode = docNode as PeptideDocNode;
            if (peptideDocNode != null)
            {
                docNode = peptideDocNode.TransitionGroups.FirstOrDefault();
            }
            var transitionGroupDocNode = docNode as TransitionGroupDocNode;
            if (transitionGroupDocNode != null)
            {
                return measuredResults.GetChromFileInfo(transitionGroupDocNode.Results, replicateIndex);
            }
            var transitionDocNode = docNode as TransitionDocNode;
            if (transitionDocNode != null)
            {
                return measuredResults.GetChromFileInfo(transitionDocNode.Results, replicateIndex);
            }
            return null;
        }

        private PointPairList AlignPointPairList(FileRetentionTimeAlignments fileRetentionTimeAlignments, MeasuredResults measuredResults, DocNode docNode, GraphData graphData, PointPairList pointPairList)
        {
            if (fileRetentionTimeAlignments == null)
            {
                return pointPairList;
            }
            var result = new PointPairList();
            for (int iPoint = 0; iPoint < pointPairList.Count; iPoint++)
            {
                int replicateIndex = graphData.ReplicateIndices[iPoint];
                var pointPair = pointPairList[iPoint];
                if (replicateIndex == AlignToReplicate || pointPair.IsInvalid)
                {
                    result.Add(pointPair);
                    continue;
                }
                var chromFileInfo = ReplicateIndexToChromFileInfo(measuredResults, docNode, replicateIndex);
                RetentionTimeAlignment retentionTimeAlignment = null;
                if (chromFileInfo != null)
                {
                    retentionTimeAlignment = fileRetentionTimeAlignments.RetentionTimeAlignments.Find(chromFileInfo);
                }
                PointPair newPoint = null;
                if (retentionTimeAlignment != null)
                {
                    newPoint = HiLowMiddleErrorBarItem.StretchPointPair(pointPair, retentionTimeAlignment.RegressionLine);
                }
                if (newPoint == null)
                {
                    result.Add(RTGraphData.RTPointPairMissing((int) pointPair.X));
                }
                else
                {
                    result.Add(newPoint);
                }
            }
            return result;
        }

        private void EmptyGraph(SrmDocument document)
        {
            string[] resultNames = GraphData.GetReplicateNames(document).ToArray();
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

            public RTGraphData(SrmDocument document, DocNode docNode, DisplayTypeChrom displayType)
                : base(document, docNode, displayType)
            {
            }

            public override PointPair PointPairMissing(int xValue)
            {
                return RTPointPairMissing(xValue);
            }

            protected override bool IsMissingValue(TransitionChromInfo chromInfo)
            {
                return chromInfo.StartRetentionTime == 0 || chromInfo.EndRetentionTime == 0;
            }

            protected override PointPair CreatePointPair(int iResult, TransitionChromInfo chromInfo)
            {
                return HiLowMiddleErrorBarItem.MakePointPair(iResult,
                                                             chromInfo.EndRetentionTime,
                                                             chromInfo.StartRetentionTime,
                                                             chromInfo.RetentionTime,
                                                             chromInfo.Fwhm);
            }

            protected override bool IsMissingValue(TransitionGroupChromInfo chromInfo)
            {
                return !chromInfo.RetentionTime.HasValue ||
                    !chromInfo.StartRetentionTime.HasValue ||
                    !chromInfo.EndRetentionTime.HasValue;
            }

            protected override PointPair CreatePointPair(int iResult, TransitionGroupChromInfo chromInfo)
            {
                return HiLowMiddleErrorBarItem.MakePointPair(iResult,
                                                             chromInfo.EndRetentionTime ?? 0,
                                                             chromInfo.StartRetentionTime ?? 0,
                                                             chromInfo.RetentionTime ?? 0,
                                                             chromInfo.Fwhm ?? 0);
            }
        }
    }
}