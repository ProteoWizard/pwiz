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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum AreaPeptideOrder { document, time, area }

    internal class AreaPeptideGraphPane : SummaryBarGraphPaneBase
    {
        public static AreaPeptideOrder PeptideOrder
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.AreaPeptideOrderEnum, AreaPeptideOrder.document);
            }
        }

        private GraphData _graphData;

        public AreaPeptideGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            XAxis.Title.Text = "Peptide";
            XAxis.Type = AxisType.Text;
        }

        public override void UpdateGraph(bool checkData)
        {
            Clear();

            TransitionGroupDocNode selectedGroup = null;
            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode != null)
            {
                if (selectedTreeNode is TransitionTreeNode)
                    selectedGroup = (TransitionGroupDocNode)selectedTreeNode.SrmParent.Model;
                else if (selectedTreeNode is TransitionGroupTreeNode)
                    selectedGroup = (TransitionGroupDocNode) selectedTreeNode.Model;
                else if (selectedTreeNode is PeptideTreeNode)
                {
                    var nodePep = ((PeptideTreeNode)selectedTreeNode).DocNode;
                    if (nodePep.Children.Count > 0)
                        selectedGroup = (TransitionGroupDocNode)nodePep.Children[0];
                }
            }

            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            var displayType = GraphChromatogram.DisplayType;

            _graphData = new GraphData(document, selectedGroup, displayType);

            int iColor = 0;
            foreach (var pointPairList in _graphData.PointPairLists)
            {
                Color color;
                if (displayType == DisplayTypeChrom.total)
                    color = COLORS_GROUPS[iColor++ % COLORS_GROUPS.Length];
                else
                    color = COLORS_TRANSITION[iColor++ % COLORS_TRANSITION.Length];
                var curveItem = new MeanErrorBarItem("", pointPairList, color, Color.Black);
                curveItem.Bar.Border.IsVisible = false;
                curveItem.Bar.Fill.Brush = new SolidBrush(color);
                CurveList.Add(curveItem);                
            }

            if (SelectedIndex != -1)
            {
                double yValue = _graphData.SelectedY;
                GraphObjList.Add(new BoxObj(SelectedIndex + .5, yValue, 0.99,
                                            yValue, Color.Black, Color.Empty)
                {
                    IsClippedToChartRect = true,
                });
            }

            if (Settings.Default.AreaLogScale)
            {
                YAxis.Title.Text = "Log Peak Area";
                YAxis.Type = AxisType.Log;
                YAxis.Scale.MinAuto = false;
                FixedYMin = YAxis.Scale.Min = 1;
                YAxis.Scale.Max = _graphData.MaxY * 10;
            }
            else
            {
                YAxis.Title.Text = "Peak Area";
                YAxis.Type = AxisType.Linear;
                YAxis.Scale.MinAuto = false;
                FixedYMin = YAxis.Scale.Min = 0;
                YAxis.Scale.Max = _graphData.MaxY * 1.05;
            }
            if (Settings.Default.AreaPeptideCV)
                YAxis.Title.Text += " CV";

            XAxis.Scale.TextLabels = _graphData.Labels;
            ScaleAxisLabels();

            AxisChange();
        }

        protected override int SelectedIndex
        {
            get { return _graphData != null ? _graphData.SelectedIndex : -1; }
        }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            if (0 <= barIndex || barIndex < _graphData.XScalePaths.Length)
                return _graphData.XScalePaths[barIndex];
            return null;
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
            if (0 <= selectedIndex && selectedIndex < _graphData.XScalePaths.Length)
                GraphSummary.StateProvider.SelectedPath = _graphData.XScalePaths[selectedIndex];
        }

        internal class GraphData : Immutable
        {
            public GraphData(SrmDocument document, TransitionGroupDocNode selectedGroup,
                DisplayTypeChrom displayType)
            {
                // Determine the shortest possible unique ID for each peptide
                var uniqueSeq = new Dictionary<string, string>();
                foreach (var nodePep in document.Peptides)
                    AddUniqePrefix(uniqueSeq, nodePep.Peptide.Sequence, 3);
                // Flip the dictionary from ID - peptide to peptide - ID
                var seqId = new Dictionary<string, string>();
                foreach (var seqPair in uniqueSeq)
                    seqId.Add(seqPair.Value, seqPair.Key);

                int pointListCount = 0;
                var dictTypeToSet = new Dictionary<IsotopeLabelType, int>();
                
                // Figure out how many point lists to create
                bool displayTotals = (displayType == DisplayTypeChrom.total);
                if (displayTotals)
                {
                    foreach (var nodeGroup in document.TransitionGroups)
                    {
                        IsotopeLabelType labelType = nodeGroup.TransitionGroup.LabelType;
                        if (!dictTypeToSet.ContainsKey(labelType))
                            dictTypeToSet.Add(labelType, pointListCount++);
                    }
                }
                else
                {
                    foreach (var nodeGroup in document.TransitionGroups)
                        pointListCount = Math.Max(pointListCount, nodeGroup.Children.Count);
                }

                // Build the list of points to show.
                var listPoints = new List<GraphPointData>();
                foreach (PeptideGroupDocNode nodeGroupPep in document.PeptideGroups)
                {
                    foreach (PeptideDocNode nodePep in nodeGroupPep.Children)
                    {
                        foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                        {
                            var path = new IdentityPath(nodeGroupPep.PeptideGroup,
                                nodePep.Peptide, nodeGroup.TransitionGroup);
                            listPoints.Add(new GraphPointData(nodePep, nodeGroup, path));
                        }
                    }
                }

                // Sort into correct order
                var peptideOrder = PeptideOrder;
                if (peptideOrder == AreaPeptideOrder.time)
                {
                    if (displayTotals)
                        listPoints.Sort(ComparePeptideTimes);
                    else
                        listPoints.Sort(CompareGroupTimes);
                }
                else if (peptideOrder == AreaPeptideOrder.area)
                {
                    if (displayTotals)
                        listPoints.Sort(ComparePeptideAreas);
                    else
                        listPoints.Sort(CompareGroupAreas);
                }

                // Init calculated values
                var pointPairLists = new List<PointPairList>();
                var labels = new List<string>();
                var xscalePaths = new List<IdentityPath>();
                double maxY = 0;
                int selectedIndex = -1;

                for (int i = 0; i < pointListCount; i++)
                    pointPairLists.Add(new PointPairList());

                // Calculate lists and values
                PeptideDocNode nodePepCurrent = null;
                int chargeCount = 0, chargeCurrent = 0;
                foreach (var dataPoint in listPoints)
                {
                    var nodePep = dataPoint.NodePep;
                    var nodeGroup = dataPoint.NodeGroup;

                    if (!ReferenceEquals(nodePep, nodePepCurrent))
                    {
                        nodePepCurrent = nodePep;

                        chargeCount = GetChargeCount(nodePep);
                        chargeCurrent = 0;
                    }

                    bool addLabel = !displayTotals;
                    if (displayTotals && nodeGroup.TransitionGroup.PrecursorCharge != chargeCurrent)
                    {
                        LevelPointPairLists(pointPairLists);
                        addLabel = true;
                    }
                    chargeCurrent = nodeGroup.TransitionGroup.PrecursorCharge;

                    var transitionGroup = nodeGroup.TransitionGroup;
                    int iGroup = labels.Count;

                    if (addLabel)
                    {
                        string label = seqId[transitionGroup.Peptide.Sequence] +
                                       (chargeCount > 1
                                            ? Transition.GetChargeIndicator(transitionGroup.PrecursorCharge)
                                            : "");
                        if (!displayTotals)
                            label += transitionGroup.LabelTypeText;
                        if (peptideOrder == AreaPeptideOrder.time)
                        {
                            label += string.Format(" ({0:F01})", displayTotals ?
                                dataPoint.TimePep : dataPoint.TimeGroup);                            
                        }
                        labels.Add(label);
                        xscalePaths.Add(dataPoint.IdentityPath);
                    }

                    double groupY = 0;
                    if (displayTotals)
                    {
                        var labelType = nodeGroup.TransitionGroup.LabelType;
                        pointPairLists[dictTypeToSet[labelType]].Add(CreatePointPair(iGroup, nodeGroup, ref groupY));
                    }
                    else
                    {
                        for (int i = 0; i < pointListCount; i++)
                        {
                            var pointPairList = pointPairLists[i];
                            if (i >= nodeGroup.Children.Count)
                                pointPairList.Add(PointPairMissing(iGroup));
                            else
                            {
                                pointPairList.Add(CreatePointPair(iGroup,
                                    (TransitionDocNode) nodeGroup.Children[i], ref groupY));
                            }
                        }
                    }

                    // Save the selected index and its y extent
                    if (ReferenceEquals(selectedGroup, nodeGroup))
                    {
                        selectedIndex = labels.Count - 1;
                        SelectedY = groupY;                        
                    }
                    // If multiple groups in the selection, make sure y extent is max of them
                    else if (selectedIndex == labels.Count - 1)
                    {
                        SelectedY = Math.Max(groupY, SelectedY);
                    }
                    maxY = Math.Max(maxY, groupY);
                }

                PointPairLists = pointPairLists;
                Labels = labels.ToArray();
                XScalePaths = xscalePaths.ToArray();
                SelectedIndex = selectedIndex;
                MaxY = maxY;
            }

            public IList<PointPairList> PointPairLists { get; private set; }
            public string[] Labels { get; private set; }
            public IdentityPath[] XScalePaths { get; private set; }
            public double MaxY { get; private set; }
            public int SelectedIndex { get; private set; }
            public double SelectedY { get; private set; }

            private static int ComparePeptideTimes(GraphPointData p1, GraphPointData p2)
            {
                if (ReferenceEquals(p1.NodePep, p2.NodePep))
                    return Peptide.CompareGroups(p1.NodeGroup, p2.NodeGroup);
                return Comparer.Default.Compare(p1.TimePep, p2.TimePep);
            }

            private static int CompareGroupTimes(GraphPointData p1, GraphPointData p2)
            {
                return Comparer.Default.Compare(p1.TimeGroup, p2.TimeGroup);
            }

            private static int ComparePeptideAreas(GraphPointData p1, GraphPointData p2)
            {
                if (ReferenceEquals(p2.NodePep, p1.NodePep))
                    return Peptide.CompareGroups(p2.NodeGroup, p1.NodeGroup);
                return Comparer.Default.Compare(p2.AreaPep, p1.AreaPep);
            }

            private static int CompareGroupAreas(GraphPointData p1, GraphPointData p2)
            {
                return Comparer.Default.Compare(p2.AreaGroup, p1.AreaGroup);
            }

            private static void LevelPointPairLists(List<PointPairList> lists)
            {
                // Add missing points to lists to make them all of equal length
                int maxPoints = 0;
                lists.ForEach(l => maxPoints = Math.Max(maxPoints, l.Count));
                lists.ForEach(l => { if (l.Count < maxPoints) l.Add(PointPairMissing(maxPoints - 1)); });
            }

            private static int GetChargeCount(PeptideDocNode nodePep)
            {
                int chargeCount = 0;
                bool[] chargesPresent = new bool[TransitionGroup.MAX_PRECURSOR_CHARGE];
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    int charge = nodeGroup.TransitionGroup.PrecursorCharge;
                    if (!chargesPresent[charge])
                    {
                        chargesPresent[charge] = true;
                        chargeCount++;
                    }
                }
                return chargeCount;
            }

            private static PointPair PointPairMissing(int iGroup)
            {
                return MeanErrorBarItem.MakePointPair(iGroup, PointPairBase.Missing, PointPairBase.Missing);
            }

            private static PointPair CreatePointPair(int iGroup, TransitionGroupDocNode nodeGroup, ref double maxY)
            {
                if (!nodeGroup.HasResults)
                    return PointPairMissing(iGroup);

                var listAreas = new List<double>();
                foreach (var chromInfo in nodeGroup.ChromInfos)
                {
                    if (chromInfo.OptimizationStep == 0 && chromInfo.Area.HasValue)
                        listAreas.Add(chromInfo.Area.Value);
                }

                return CreatePointPair(iGroup, listAreas, ref maxY);
            }

            private static PointPair CreatePointPair(int iGroup, TransitionDocNode nodeTran, ref double maxY)
            {
                if (!nodeTran.HasResults)
                    return PointPairMissing(iGroup);

                var listAreas = new List<double>();
                foreach (var chromInfo in nodeTran.ChromInfos)
                {
                    if (chromInfo.OptimizationStep == 0 && !chromInfo.IsEmpty)
                        listAreas.Add(chromInfo.Area);
                }

                return CreatePointPair(iGroup, listAreas, ref maxY);
            }

            private static PointPair CreatePointPair(int iGroup, List<double> listAreas, ref double maxY)
            {
                if (listAreas.Count == 0)
                    return PointPairMissing(iGroup);

                var statAreas = new Statistics(listAreas.ToArray());

                PointPair pointPair;
                if (Settings.Default.AreaPeptideCV)
                    pointPair = MeanErrorBarItem.MakePointPair(iGroup, statAreas.StdDev()/statAreas.Mean(), 0);
                else
                    pointPair = MeanErrorBarItem.MakePointPair(iGroup, statAreas.Mean(), statAreas.StdDev());
                maxY = Math.Max(maxY, MeanErrorBarItem.GetYTotal(pointPair));
                return pointPair;
            }

            private static void AddUniqePrefix(IDictionary<string, string> uniqueSeq, string seq, int len)
            {
                // Get the prefix of the specified length
                string prefix = seq.Substring(0, len);

                // If this prefix is not in the dictionary, add it
                string conflict;
                if (!uniqueSeq.TryGetValue(seq, out conflict))
                    uniqueSeq.Add(prefix, seq);
                else
                {
                    // Otherwise, remove the current conflicting peptide, and
                    // add it back with one more character of the prefix
                    uniqueSeq.Remove(prefix);
                    uniqueSeq.Add(conflict.Substring(0, len + 1), conflict);

                    // Then try again for this prefix with one more character
                    AddUniqePrefix(uniqueSeq, seq, len + 1);
                }
            }
        }

        private class GraphPointData
        {
            public GraphPointData(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, IdentityPath identityPath)
            {
                NodePep = nodePep;
                NodeGroup = nodeGroup;
                IdentityPath = identityPath;

                CalcStats(nodePep, nodeGroup);
            }

            public PeptideDocNode NodePep { get; private set; }
            public TransitionGroupDocNode NodeGroup { get; private set; }
            public IdentityPath IdentityPath { get; private set; }
            public double AreaGroup { get; private set; }
            public double AreaPep { get; private set; }
            public double TimeGroup { get; private set; }
            public double TimePep { get; private set; }

            private void CalcStats(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
            {
                var areas = new List<double>();
                var times = new List<double>();
                foreach (TransitionGroupDocNode nodePepChild in nodePep.Children)
                {
                    double meanArea, meanTime;
                    CalcStats(nodePepChild, out meanArea, out meanTime);
                    areas.Add(meanArea);
                    times.Add(meanTime);
                    if (ReferenceEquals(nodeGroup, nodePepChild))
                    {
                        AreaGroup = meanArea;
                        TimeGroup = meanTime;
                    }
                }
                AreaPep = (areas.Count > 0 ? new Statistics(areas.ToArray()).Mean() : 0);
                TimePep = (times.Count > 0 ? new Statistics(times.ToArray()).Mean() : 0);
            }

            private static void CalcStats(TransitionGroupDocNode nodeGroup, out double meanArea, out double meanTime)
            {
                var areas = new List<double>();
                var times = new List<double>();
                foreach (var chromInfo in nodeGroup.ChromInfos)
                {
                    if (chromInfo.Area.HasValue)
                        areas.Add(chromInfo.Area.Value);
                    if (chromInfo.RetentionTime.HasValue)
                        times.Add(chromInfo.RetentionTime.Value);
                }
                meanArea = (areas.Count > 0 ? new Statistics(areas.ToArray()).Mean() : 0);
                meanTime = (times.Count > 0 ? new Statistics(times.ToArray()).Mean() : 0);
            }
        }
    }
}