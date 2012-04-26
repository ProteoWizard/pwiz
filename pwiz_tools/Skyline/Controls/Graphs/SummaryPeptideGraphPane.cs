/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum SummaryPeptideOrder { document, time, area }

    internal abstract class SummaryPeptideGraphPane : SummaryBarGraphPaneBase
    {
        public static SummaryPeptideOrder PeptideOrder
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.AreaPeptideOrderEnum, SummaryPeptideOrder.document);
            }

            set { Settings.Default.AreaPeptideOrderEnum = value.ToString(); }
        }

        protected GraphData _graphData;

        protected SummaryPeptideGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            XAxis.Title.Text = "Peptide";
            XAxis.Type = AxisType.Text;
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

        public override void UpdateGraph(bool checkData)
        {
            Clear();

            TransitionGroupDocNode selectedGroup = null;
            PeptideGroupDocNode selectedProtein = null;
            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode != null)
            {
                if (selectedTreeNode is TransitionTreeNode)
                    selectedGroup = (TransitionGroupDocNode)selectedTreeNode.SrmParent.Model;
                else if (selectedTreeNode is TransitionGroupTreeNode)
                    selectedGroup = (TransitionGroupDocNode)selectedTreeNode.Model;
                else if (selectedTreeNode is PeptideTreeNode)
                {
                    var nodePep = ((PeptideTreeNode)selectedTreeNode).DocNode;
                    if (nodePep.Children.Count > 0)
                        selectedGroup = (TransitionGroupDocNode)nodePep.Children[0];
                }
                var proteinTreeNode = selectedTreeNode.GetNodeOfType<PeptideGroupTreeNode>();
                if (proteinTreeNode != null)
                    selectedProtein = proteinTreeNode.DocNode;
            }

            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            var displayType = GraphChromatogram.GetDisplayType(document);

            _graphData = CreateGraphData(document, selectedProtein, selectedGroup, displayType);

            int iColor = 0;
            foreach (var pointPairList in _graphData.PointPairLists)
            {
                Color color = displayType == DisplayTypeChrom.total
                    ? COLORS_GROUPS[iColor++ % COLORS_GROUPS.Length]
                    : COLORS_TRANSITION[iColor++ % COLORS_TRANSITION.Length];

                BarItem curveItem;
                if (HiLowMiddleErrorBarItem.IsHiLoMiddleErrorList(pointPairList))
                    curveItem = new HiLowMiddleErrorBarItem("", pointPairList, color, Color.Black);
                else
                    curveItem = new MeanErrorBarItem("", pointPairList, color, Color.Black);

                curveItem.Bar.Border.IsVisible = false;
                curveItem.Bar.Fill.Brush = new SolidBrush(color);
                CurveList.Add(curveItem);
            }

            if (ShowSelection && SelectedIndex != -1)
            {
                double yValue = _graphData.SelectedMaxY;
                double yMin = _graphData.SelectedMinY;
                GraphObjList.Add(new BoxObj(SelectedIndex + .5, yValue, 0.99,
                                            yValue - yMin, Color.Black, Color.Empty)
                {
                    IsClippedToChartRect = true,
                });
            }

            UpdateAxes();
        }

        protected abstract GraphData CreateGraphData(SrmDocument document, PeptideGroupDocNode selectedProtein,
            TransitionGroupDocNode selectedGroup, DisplayTypeChrom displayType);

        protected virtual void UpdateAxes()
        {
            UpdateAxes(true);
        }

        protected void UpdateAxes(bool allowLogScale)
        {
            if (Settings.Default.AreaLogScale && allowLogScale)
            {
                YAxis.Title.Text = "Log " + YAxis.Title.Text;
                YAxis.Type = AxisType.Log;
                YAxis.Scale.MinAuto = false;
                FixedYMin = YAxis.Scale.Min = 1;
                YAxis.Scale.Max = _graphData.MaxY * 10;
            }
            else
            {
                YAxis.Type = AxisType.Linear;
                if (_graphData.MinY.HasValue)
                {
                    if (!IsZoomed && !YAxis.Scale.MinAuto)
                        YAxis.Scale.MinAuto = true;
                }
                else
                {
                    YAxis.Scale.MinAuto = false;
                    FixedYMin = YAxis.Scale.Min = 0;
                    YAxis.Scale.Max = _graphData.MaxY * 1.05;
                }
            }
            if (!_graphData.MinY.HasValue && Settings.Default.ShowPeptideCV)
            {
                YAxis.Title.Text += " CV";
                if (!Settings.Default.PeakDecimalCv)
                    YAxis.Title.Text += " (%)";
                if (_graphData.MaxCVSetting != 0)
                {
                    YAxis.Scale.MaxAuto = false;
                    YAxis.Scale.Max = _graphData.MaxCVSetting;
                }
                else if (!IsZoomed && !YAxis.Scale.MaxAuto)
                {
                    YAxis.Scale.MaxAuto = true;
                }
            }
            else if (_graphData.MaxValueSetting != 0 || _graphData.MinValueSetting != 0)
            {
                if (_graphData.MaxValueSetting != 0)
                {
                    YAxis.Scale.MaxAuto = false;
                    YAxis.Scale.Max = _graphData.MaxValueSetting;
                }
                if (_graphData.MinValueSetting != 0)
                {
                    YAxis.Scale.MinAuto = false;
                    YAxis.Scale.Min = _graphData.MinValueSetting;
                    if (!_graphData.MinY.HasValue)
                        FixedYMin = YAxis.Scale.Min;
                }
            }
            else if (!IsZoomed && !YAxis.Scale.MaxAuto)
            {
                YAxis.Scale.MaxAuto = true;
            }

            XAxis.Scale.TextLabels = _graphData.Labels;
            ScaleAxisLabels();

            AxisChange();            
        }

        internal abstract class GraphData : Immutable
        {
            private readonly int? _resultIndex;

            protected GraphData(SrmDocument document, TransitionGroupDocNode selectedGroup, PeptideGroupDocNode selectedProtein, 
                             int? iResult, DisplayTypeChrom displayType)
            {
                _resultIndex = iResult;
                
                // Determine the shortest possible unique ID for each peptide
                var uniqueSeq = new List<KeyValuePair<string, string>>();
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
                        pointListCount = Math.Max(pointListCount, GraphChromatogram.GetDisplayTransitions(nodeGroup, displayType).Count());
                }

                // Build the list of points to show.
                var listPoints = new List<GraphPointData>();
                foreach (PeptideGroupDocNode nodeGroupPep in document.PeptideGroups)
                {
                    if (AreaGraphController.AreaScope == AreaScope.protein)
                    {
                        if (!ReferenceEquals(nodeGroupPep, selectedProtein))
                            continue;
                    } 
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
                if (peptideOrder == SummaryPeptideOrder.time)
                {
                    if (displayTotals)
                        listPoints.Sort(ComparePeptideTimes);
                    else
                        listPoints.Sort(CompareGroupTimes);
                }
                else if (peptideOrder == SummaryPeptideOrder.area)
                {
                    listPoints.Sort(CompareGroupAreas);
                }

                // Init calculated values
                var pointPairLists = new List<PointPairList>();
                var labels = new List<string>();
                var xscalePaths = new List<IdentityPath>();
                double maxY = 0;
                double minY = double.MaxValue;
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
                        if (peptideOrder == SummaryPeptideOrder.time)
                        {
                            label += string.Format(" ({0:F01})", displayTotals ?
                                                                                   dataPoint.TimePepCharge : dataPoint.TimeGroup);                            
                        }
                        labels.Add(label);
                        xscalePaths.Add(dataPoint.IdentityPath);
                    }

                    double groupMaxY = 0;
                    double groupMinY = double.MaxValue;

                    // ReSharper disable DoNotCallOverridableMethodsInConstructor
                    int? resultIndex = _resultIndex;
                    if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.best && nodePep != null)
                        resultIndex = nodePep.BestResult;
                    if (displayTotals)
                    {
                        var labelType = nodeGroup.TransitionGroup.LabelType;
                        pointPairLists[dictTypeToSet[labelType]].Add(CreatePointPair(iGroup, nodeGroup, ref groupMaxY, ref groupMinY, resultIndex));
                    }
                    else
                    {
                        var nodeTrans = GraphChromatogram.GetDisplayTransitions(nodeGroup, displayType).ToArray();
                        for (int i = 0; i < pointListCount; i++)
                        {
                            var pointPairList = pointPairLists[i];
                            pointPairList.Add(i >= nodeTrans.Length
                                                  ? CreatePointPairMissing(iGroup)
                                                  : CreatePointPair(iGroup, nodeTrans[i], ref groupMaxY, ref groupMinY,
                                                                    resultIndex));
                        }
                    }
                    // ReSharper restore DoNotCallOverridableMethodsInConstructor

                    // Save the selected index and its y extent
                    if (ReferenceEquals(selectedGroup, nodeGroup))
                    {
                        selectedIndex = labels.Count - 1;
                        SelectedMaxY = groupMaxY;
                        SelectedMinY = groupMinY;
                    }
                        // If multiple groups in the selection, make sure y extent is max of them
                    else if (selectedIndex == labels.Count - 1)
                    {
                        SelectedMaxY = Math.Max(groupMaxY, SelectedMaxY);
                        SelectedMinY = Math.Min(groupMinY, SelectedMinY);
                    }
                    maxY = Math.Max(maxY, groupMaxY);
                    minY = Math.Min(minY, groupMinY);
                }

                PointPairLists = pointPairLists;
                Labels = labels.ToArray();
                XScalePaths = xscalePaths.ToArray();
                SelectedIndex = selectedIndex;
                MaxY = maxY;
                if (minY != double.MaxValue)
                    MinY = minY;
            }

            public IList<PointPairList> PointPairLists { get; private set; }
            public string[] Labels { get; private set; }
            public IdentityPath[] XScalePaths { get; private set; }
            public double MaxY { get; private set; }
            public double? MinY { get; private set; }
            public int SelectedIndex { get; private set; }
            public double SelectedMaxY { get; private set; }
            public double SelectedMinY { get; private set; }

            public virtual double MaxValueSetting { get { return 0; } }
            public virtual double MinValueSetting { get { return 0; } }
            public virtual double MaxCVSetting { get { return 0; } }

            private static int ComparePeptideTimes(GraphPointData p1, GraphPointData p2)
            {
                if (ReferenceEquals(p1.NodePep, p2.NodePep))
                    return Peptide.CompareGroups(p1.NodeGroup, p2.NodeGroup);
                return Comparer.Default.Compare(p1.TimePepCharge, p2.TimePepCharge);
            }

            private static int CompareGroupTimes(GraphPointData p1, GraphPointData p2)
            {
                return Comparer.Default.Compare(p1.TimeGroup, p2.TimeGroup);
            }

/*
            private static int ComparePeptideAreas(GraphPointData p1, GraphPointData p2)
            {
                if (ReferenceEquals(p2.NodePep, p1.NodePep))
                    return Peptide.CompareGroups(p2.NodeGroup, p1.NodeGroup);
                return Comparer.Default.Compare(p2.AreaPepCharge, p1.AreaPepCharge);
            }
*/

            private static int CompareGroupAreas(GraphPointData p1, GraphPointData p2)
            {
                return Comparer.Default.Compare(p2.AreaGroup, p1.AreaGroup);
            }

            private void LevelPointPairLists(List<PointPairList> lists)
            {
                // Add missing points to lists to make them all of equal length
                int maxPoints = 0;
                lists.ForEach(l => maxPoints = Math.Max(maxPoints, l.Count));
                lists.ForEach(l => { if (l.Count < maxPoints) l.Add(CreatePointPairMissing(maxPoints - 1)); });
            }

// ReSharper disable SuggestBaseTypeForParameter
            private static int GetChargeCount(PeptideDocNode nodePep)
// ReSharper restore SuggestBaseTypeForParameter
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

            protected static PointPair PointPairMissing(int iGroup)
            {
                return MeanErrorBarItem.MakePointPair(iGroup, PointPairBase.Missing, PointPairBase.Missing);
            }

            protected virtual PointPair CreatePointPairMissing(int iGroup)
            {
                return PointPairMissing(iGroup);
            }

            protected virtual PointPair CreatePointPair(int iGroup, TransitionGroupDocNode nodeGroup, ref double maxY, ref double minY, int? resultIndex)
            {
                if (!nodeGroup.HasResults)
                    return PointPairMissing(iGroup);

                var listValues = new List<double>();
                foreach (var chromInfo in nodeGroup.GetChromInfos(resultIndex))
                {
                    double? value = GetValue(chromInfo);
                    if (chromInfo.OptimizationStep == 0 && value.HasValue)
                        listValues.Add(value.Value);
                }

                return CreatePointPair(iGroup, listValues, ref maxY);
            }

            protected abstract double? GetValue(TransitionGroupChromInfo chromInfo);

            protected virtual PointPair CreatePointPair(int iGroup, TransitionDocNode nodeTran, ref double maxY, ref double minY, int? resultIndex)
            {
                if (!nodeTran.HasResults)
                    return PointPairMissing(iGroup);

                var listValues = new List<double>();
                foreach (var chromInfo in nodeTran.GetChromInfos(resultIndex))
                {
                    if (chromInfo.OptimizationStep == 0 && !chromInfo.IsEmpty)
                        listValues.Add(GetValue(chromInfo));
                }

                return CreatePointPair(iGroup, listValues, ref maxY);
            }

            protected abstract double GetValue(TransitionChromInfo info);

            private static PointPair CreatePointPair(int iGroup, ICollection<double> listValues, ref double maxY)
            {
                if (listValues.Count == 0)
                    return PointPairMissing(iGroup);

                var statValues = new Statistics(listValues);

                PointPair pointPair;
                if (Settings.Default.ShowPeptideCV)
                {
                    double cvRatio = statValues.StdDev() / statValues.Mean();
                    if (!Settings.Default.PeakDecimalCv)
                        cvRatio *= 100;
                    pointPair = MeanErrorBarItem.MakePointPair(iGroup, cvRatio, 0);
                }
                else
                    pointPair = MeanErrorBarItem.MakePointPair(iGroup, statValues.Mean(), statValues.StdDev());
                maxY = Math.Max(maxY, MeanErrorBarItem.GetYTotal(pointPair));
                return pointPair;
            }

            private sealed class PrefixComparer : IComparer<KeyValuePair<string, string>>
            {
                public int Compare(KeyValuePair<string, string> v1, KeyValuePair<string, string> v2)
                {
                    return Comparer.DefaultInvariant.Compare(v1.Key, v2.Key);
                }
            }

            private static readonly PrefixComparer PREFIX_COMPARER = new PrefixComparer();

            private static void AddUniqePrefix(List<KeyValuePair<string, string>> uniqueSeq, string seq, int len)
            {
                // Get the prefix of the specified length
                string prefix = seq.Substring(0, len);

                // If this prefix is not in the dictionary, add it
                var insertVal = new KeyValuePair<string, string>(prefix, seq);
                int iVal = uniqueSeq.BinarySearch(insertVal, PREFIX_COMPARER);
                if (iVal < 0)
                    iVal = ~iVal;

                string nextPrefix = (iVal < uniqueSeq.Count ? uniqueSeq[iVal].Key : "");
                string nextSeq = (iVal < uniqueSeq.Count ? uniqueSeq[iVal].Value : "");

                if (Equals(prefix, nextPrefix))
                {
                    // If this is the same sequence again, ignore it.
                    if (Equals(seq, nextSeq))
                        return;

                    // If the prefix is shorter than the matching sequence, lengthen it
                    // for that sequence.
                    if (len < nextSeq.Length)
                    {
                        uniqueSeq.RemoveAt(iVal);
                        uniqueSeq.Insert(iVal, new KeyValuePair<string, string>(nextSeq.Substring(0, len + 1), nextSeq));
                    }
                    // If the prefix is shorter than the current sequence, lengthen it for
                    // the current sequence also.
                    if (len < seq.Length)
                        len++;

                    // Then try again for this sequence with either a longer next prefix
                    // or a longer current prefix
                    AddUniqePrefix(uniqueSeq, seq, len);
                }
                else if (nextPrefix.StartsWith(prefix))
                {
                    if (len == seq.Length)
                    {
                        uniqueSeq.Insert(iVal, insertVal);
                    }
                    else
                    {
                        // Advance until the prefixes no longer share a common prefix
                        int stopIndex = Math.Min(nextPrefix.Length, seq.Length);
                        do
                        {
                            prefix = seq.Substring(0, ++len);
                        }
                        while (len < stopIndex && nextPrefix.StartsWith(prefix));

                        AddUniqePrefix(uniqueSeq, seq, len);
                    }
                }
                else
                {
                    uniqueSeq.Insert(iVal, insertVal);
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
//            public double AreaPepCharge { get; private set; }
            public double TimeGroup { get; private set; }
            public double TimePepCharge { get; private set; }

// ReSharper disable SuggestBaseTypeForParameter
            private void CalcStats(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
// ReSharper restore SuggestBaseTypeForParameter
            {
                var areas = new List<double>();
                var times = new List<double>();
                foreach (TransitionGroupDocNode nodePepChild in nodePep.Children)
                {
                    double? meanArea, meanTime;
                    CalcStats(nodePepChild, out meanArea, out meanTime);
                    if (nodeGroup.TransitionGroup.PrecursorCharge != nodePepChild.TransitionGroup.PrecursorCharge)
                        continue;
                    if (meanArea.HasValue)
                        areas.Add(meanArea.Value);
                    if (meanTime.HasValue)
                        times.Add(meanTime.Value);
                    if (ReferenceEquals(nodeGroup, nodePepChild))
                    {
                        AreaGroup = meanArea ?? 0;
                        TimeGroup = meanTime ?? 0;
                    }
                }
//                AreaPepCharge = (areas.Count > 0 ? new Statistics(areas).Mean() : 0);
                TimePepCharge = (times.Count > 0 ? new Statistics(times).Mean() : 0);
            }

            private static void CalcStats(TransitionGroupDocNode nodeGroup, out double? meanArea, out double? meanTime)
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
                meanArea = null;
                if (areas.Count > 0)
                    meanArea = new Statistics(areas).Mean();
                meanTime = null;
                if (times.Count > 0)
                    meanTime = new Statistics(times).Mean();
            }
        }
    }
}