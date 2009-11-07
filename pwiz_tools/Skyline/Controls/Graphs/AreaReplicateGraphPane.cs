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
    internal class AreaReplicateGraphPane : AreaGraphPane
    {
        private static readonly Color[] COLORS_TRANSITION = GraphChromatogram.COLORS_LIBRARY;
        private static readonly Color[] COLORS_GROUPS = GraphChromatogram.COLORS_GROUPS;

        private DocNode _parentNode;

        public AreaReplicateGraphPane()
        {
            XAxis.Title.Text = "Replicate";
            XAxis.Type = AxisType.Text;
        }

        private static BarType BarType
        {
            get
            {
                return Settings.Default.AreaPercentView ? BarType.PercentStack : BarType.Stack;
            }
        }

        /// <summary>
        /// This works around a bug in Zedgraph's ValueHandler.BarCenterValue
        /// (it incorrectly assumes that HiLowBarItems do place themselves 
        /// next to each other like all other BarItems).
        /// </summary>
        /// <param name="curve">The BarItem</param>
        /// <param name="barWidth">The width of the bar</param>
        /// <param name="iCluster">The index of the point in CurveItem.Points</param>
        /// <param name="val">The x-value of the point.</param>
        /// <param name="iOrdinal">The index of the BarItem in the CurveList</param>
        double BarCenterValue(CurveItem curve, float barWidth, int iCluster,
                              double val, int iOrdinal)
        {
            float clusterWidth = BarSettings.GetClusterWidth();
            float clusterGap = BarSettings.MinClusterGap * barWidth;
            float barGap = barWidth * BarSettings.MinBarGap;

            if (curve.IsBar && BarSettings.Type != BarType.Cluster)
                iOrdinal = 0;

            float centerPix = XAxis.Scale.Transform(curve.IsOverrideOrdinal, iCluster, val)
                              - clusterWidth / 2.0F + clusterGap / 2.0F +
                              iOrdinal * (barWidth + barGap) + 0.5F * barWidth;
            return XAxis.Scale.ReverseTransform(centerPix);
        }
        
        /// <summary>
        /// Works around a bug in ValueHandler.BarCenterValue
        /// </summary>
        bool FindNearestBar(PointF point, out CurveItem nearestCurve, out int iNearest)
        {
            double x, y;
            ReverseTransform(point, out x, out y);
            PointF pointCenter = new PointF(XAxis.Scale.Transform(Math.Round(x)), point.Y);
            if (!FindNearestPoint(pointCenter, out nearestCurve, out iNearest))
            {
                return false;
            }
            double minDist = double.MaxValue;
            for (int iCurve = 0; iCurve < CurveList.Count; iCurve ++)
            {
                CurveItem curve = CurveList[iCurve];
                double barCenter = BarCenterValue(curve, curve.GetBarWidth(this), iNearest, Math.Round(x), iCurve);
                double dist = Math.Abs(barCenter - x);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestCurve = curve;
                }
            }
            return true;
        }

        private static IdentityPath GetIdentityPath(CurveItem curveItem)
        {
            return curveItem.Tag as IdentityPath;
        }

        public override bool HandleKeyDownEvent(object sender, KeyEventArgs keyEventArgs)
        {
            switch (keyEventArgs.KeyCode)
            {
                case Keys.Left:
                case Keys.Up:
                    ChangeReplicate(GraphPeakArea.ResultsIndex - 1);
                    return true;
                case Keys.Right:
                case Keys.Down:
                    ChangeReplicate(GraphPeakArea.ResultsIndex + 1);
                    return true;
            }
            return false;
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            if (mouseEventArgs.Button != MouseButtons.None)
                return false;

            CurveItem nearestCurve;
            int iNearest;
            if (!FindNearestBar(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out nearestCurve, out iNearest))
            {
                return false;
            }
            IdentityPath identityPath = GetIdentityPath(nearestCurve);
            if (identityPath == null)
            {
                return false;
            }
            GraphPeakArea.Cursor = Cursors.Hand;
            return true;
        }

        public override bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            CurveItem nearestCurve;
            int iNearest;
            if (!FindNearestBar(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out nearestCurve, out iNearest))
            {
                return false;
            }
            IdentityPath identityPath = GetIdentityPath(nearestCurve);
            if (identityPath == null)
            {
                return false;
            }
            // Just change the active replicate, if the user clicks on a
            // different replicate.  Change the selection, if the replicate
            // is already selected.  This keeps the UI from drilling in too
            // deep when the user just wants to see a different replicate
            // at the same level currently being view (e.g. peptide)
            if (GraphPeakArea.ResultsIndex != iNearest)
                ChangeReplicate(iNearest);
            else
                GraphPeakArea.StateProvider.SelectedPath = identityPath;
            return true;
        }

        private void ChangeReplicate(int index)
        {
            if (index < 0)
                return;
            var document = GraphPeakArea.DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults || index >= document.Settings.MeasuredResults.Chromatograms.Count)
                return;
            GraphPeakArea.StateProvider.SelectedResultsIndex = index;
            GraphPeakArea.Focus();
        }

        public override void HandleResizeEvent()
        {
            ScaleAxisLabels();
        }

        private void ScaleAxisLabels()
        {
            int dyAvailable = (int) Rect.Height/4;
            int countLabels = (XAxis.Scale.TextLabels != null ? XAxis.Scale.TextLabels.Count() : 0);
            int dxAvailable = (int) Rect.Width/Math.Max(1, countLabels);
            int dpAvailable;
            if (dyAvailable > dxAvailable)
            {
                dpAvailable = dyAvailable;
                XAxis.Scale.FontSpec.Angle = 90;
            }
            else
            {
                dpAvailable = dxAvailable;
                XAxis.Scale.FontSpec.Angle = 0;
            }
            var fontSpec = XAxis.Scale.FontSpec;
            int pointSize;
            for (pointSize = 12; pointSize > 4; pointSize--)
            {
                using (var font = new Font(fontSpec.Family, pointSize))
                {
                    var maxWidth = MaxWidth(font, XAxis.Scale.TextLabels);
                    if (maxWidth <= dpAvailable)
                    {
                        break;
                    }
                }
            }
            XAxis.Scale.FontSpec.Size = pointSize;
        }

        private static int MaxWidth(Font font, IEnumerable<String> labels)
        {
            var result = 0;
            if (labels != null)
            {
                foreach (var label in labels)
                    result = Math.Max(result, SystemMetrics.GetTextWidth(font, label));
            }
            return result;
        }

        public override void UpdateGraph(bool checkData)
        {
            SrmDocument document = GraphPeakArea.DocumentUIContainer.DocumentUI;
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
            var selectedTreeNode = GraphPeakArea.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode == null)
            {
                // Add a missing point for each replicate name.
                PointPairList pointPairList = new PointPairList();
                for (int i = 0; i < resultNames.Length; i++)
                    pointPairList.Add(GraphData.PointPairMissing(i));
                AxisChange();
                return;
            }

            BarSettings.Type = BarType;
            YAxis.Title.Text = "Peak Area";
            Title.Text = null;

            DisplayTypeChrom displayType = GraphChromatogram.DisplayType;
            DocNode selectedNode = selectedTreeNode.Model;
            DocNode parentNode = selectedNode;
            IdentityPath identityPath = selectedTreeNode.Path;
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
                    parentNode = children[0];
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
                if (!results.Chromatograms.Contains(chrom => chrom.OptimizationFunction != null))
                    displayType = DisplayTypeChrom.all;
                    // Otherwise, do not stack the bars
                else
                    BarSettings.Type = BarType.Cluster;
            }

            GraphData graphData = new GraphData(parentNode, displayType);

            int selectedReplicateIndex = GraphPeakArea.ResultsIndex;
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
            if (maxArea >  -double.MaxValue)
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
                YAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = true;
            }
            if (BarSettings.Type == BarType.PercentStack)
            {
                YAxis.Scale.Max = 100;
                YAxis.Scale.MaxAuto = false;
                YAxis.Title.Text = "Peak Area Percentage";
            }
            else if (!YAxis.Scale.MaxAuto && YAxis.Scale.Max == 100)
            {
                YAxis.Scale.MaxAuto = true;
            }
            AxisChange();
        }

        public void Clear()
        {
            CurveList.Clear();
            GraphObjList.Clear();
        }

        /// <summary>
        /// Holds the data that is currently displayed in the graph.
        /// Currently, we don't hold onto this object, because we never need to look
        /// at the data after the graph is rendered.
        /// </summary>
        class GraphData : Immutable
        {
            public GraphData(DocNode docNode, DisplayTypeChrom displayType)
            {
                List<DocNode> docNodes = new List<DocNode>();
                List<List<PointPairList>> pointPairLists = new List<List<PointPairList>>();
                List<String> docNodeLabels = new List<string>();
                if (docNode is TransitionDocNode)
                {
                    TransitionDocNode transitionDocNode = (TransitionDocNode) docNode;
                    docNodes.Add(transitionDocNode);
                    pointPairLists.Add(GetPointPairLists(transitionDocNode, displayType));
                    docNodeLabels.Add(ChromGraphItem.GetTitle(transitionDocNode));
                }
                else if (docNode is TransitionGroupDocNode)
                {
                    if (displayType != DisplayTypeChrom.all)
                    {
                        TransitionGroupDocNode transitionGroup = (TransitionGroupDocNode)docNode;
                        docNodes.Add(transitionGroup);
                        pointPairLists.Add(GetPointPairLists(transitionGroup, displayType));
                        docNodeLabels.Add(ChromGraphItem.GetTitle(transitionGroup));                        
                    }
                    else
                    {
                        foreach (TransitionDocNode transitionDocNode in ((TransitionGroupDocNode)docNode).Children)
                        {
                            docNodes.Add(transitionDocNode);
                            pointPairLists.Add(GetPointPairLists(transitionDocNode, displayType));
                            docNodeLabels.Add(ChromGraphItem.GetTitle(transitionDocNode));
                        }                        
                    }
                }
                else if (docNode is PeptideDocNode)
                {
                    foreach (TransitionGroupDocNode transitionGroup in ((PeptideDocNode)docNode).Children)
                    {
                        docNodes.Add(transitionGroup);
                        pointPairLists.Add(GetPointPairLists(transitionGroup, DisplayTypeChrom.total));
                        docNodeLabels.Add(ChromGraphItem.GetTitle(transitionGroup));
                    }

                }
                PointPairLists = pointPairLists;
                DocNodes = docNodes;
                DocNodeLabels = docNodeLabels;
            }

            public static IEnumerable<string> GetReplicateNames(SrmDocument document)
            {
                if (!document.Settings.HasResults)
                    return new string[0];

                return from chromatogram in document.Settings.MeasuredResults.Chromatograms
                       select chromatogram.Name;
            }

            public IList<DocNode> DocNodes { get; private set; }
            public IList<String> DocNodeLabels { get; private set; }
            public IList<List<PointPairList>> PointPairLists { get; private set; }

            public static PointPair PointPairMissing(int xValue)
            {
                return new PointPair(xValue, PointPairBase.Missing);
            }

            private static List<PointPairList> GetPointPairLists(TransitionDocNode transition,
                                                                 DisplayTypeChrom displayType)
            {
                var pointPairLists = new List<PointPairList>();
                if (!transition.HasResults)
                {
                    pointPairLists.Add(new PointPairList());
                    return pointPairLists;                    
                }
                int maxSteps = 1;
                bool allowSteps = (displayType == DisplayTypeChrom.single);
                if (allowSteps)
                {
                    foreach (var result in transition.Results)
                        maxSteps = Math.Max(maxSteps, GetCountSteps(result));                    
                }
                for (int i = 0; i < maxSteps; i++)
                    pointPairLists.Add(new PointPairList());

                int numSteps = maxSteps/2;
                for (int iResult = 0; iResult < transition.Results.Count; iResult++)
                {
                    // Fill everything with missing data until filled for real
                    for (int i = 0; i < maxSteps; i++)
                        pointPairLists[i].Add(PointPairMissing(iResult));

                    var result = transition.Results[iResult];
                    if (result == null || result.Count == 0)
                        continue;

                    // Add areas by result index to point pair lists for every step
                    // to be shown.
                    foreach (var chromInfo in result)
                    {
                        int step = chromInfo.OptimizationStep;
                        int iStep = step + numSteps;
                        if (0 > iStep || iStep >= pointPairLists.Count)
                            continue;

                        // Replace the added missing point with the real value
                        var pointPairList = pointPairLists[iStep];
                        pointPairList[pointPairList.Count - 1] = new PointPair(iResult, chromInfo.Area);
                    }
                }
                return pointPairLists;
            }

            private static int GetCountSteps(IList<TransitionChromInfo> result)
            {
                // Only for the first file
                int fileIndex = result[0].FileIndex;
                int maxStep = 0;
                foreach (var chromInfo in result)
                {
                    if (chromInfo.FileIndex != fileIndex)
                        continue;
                    maxStep = Math.Max(maxStep, chromInfo.OptimizationStep);
                }
                return maxStep*2 + 1;
            }

            static List<PointPairList> GetPointPairLists(TransitionGroupDocNode transitionGroupDocNode,
                                                         DisplayTypeChrom displayType)
            {
                var pointPairLists = new List<PointPairList>();
                if (!transitionGroupDocNode.HasResults)
                {
                    pointPairLists.Add(new PointPairList());
                    return pointPairLists;
                }
                int maxSteps = 1;
                bool allowSteps = (displayType == DisplayTypeChrom.single);
                if (allowSteps)
                {
                    foreach (var result in transitionGroupDocNode.Results)
                        maxSteps = Math.Max(maxSteps, GetCountSteps(result));
                }
                for (int i = 0; i < maxSteps; i++)
                    pointPairLists.Add(new PointPairList());

                int numSteps = maxSteps / 2;
                for (int iResult = 0; iResult < transitionGroupDocNode.Results.Count; iResult++)
                {
                    // Fill everything with missing data until filled for real
                    for (int i = 0; i < maxSteps; i++)
                        pointPairLists[i].Add(PointPairMissing(iResult));

                    var result = transitionGroupDocNode.Results[iResult];
                    if (result == null || result.Count == 0 || !result[0].RetentionTime.HasValue)
                        continue;

                    // Add areas by result index to point pair lists for every step
                    // to be shown.
                    foreach (var chromInfo in result)
                    {
                        int step = chromInfo.OptimizationStep;
                        int iStep = step + numSteps;
                        if (0 > iStep || iStep >= pointPairLists.Count || !chromInfo.Area.HasValue)
                            continue;

                        // Replace the added missing point with the real value
                        var pointPairList = pointPairLists[iStep];
                        pointPairList[pointPairList.Count - 1] = new PointPair(iResult, chromInfo.Area.Value);
                    }
                }
                return pointPairLists;
            }

            private static int GetCountSteps(IList<TransitionGroupChromInfo> result)
            {
                // Only for the first file
                if (result == null)
                    return 0;

                int fileIndex = result[0].FileIndex;
                int maxStep = 0;
                foreach (var chromInfo in result)
                {
                    if (chromInfo.FileIndex != fileIndex)
                        continue;
                    maxStep = Math.Max(maxStep, chromInfo.OptimizationStep);
                }
                return maxStep * 2 + 1;
            }

        }
    }
}