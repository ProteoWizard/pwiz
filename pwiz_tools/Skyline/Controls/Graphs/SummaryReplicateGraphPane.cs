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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Graph pane which shows the comparison of retention times across the replicates.
    /// </summary>
    internal abstract class SummaryReplicateGraphPane : SummaryGraphPane
    {
        protected static readonly Color[] COLORS_TRANSITION = GraphChromatogram.COLORS_LIBRARY;
        protected static readonly Color[] COLORS_GROUPS = GraphChromatogram.COLORS_GROUPS;

        protected DocNode _parentNode;

        protected SummaryReplicateGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            XAxis.Title.Text = "Replicate";
            XAxis.Type = AxisType.Text;
        }

        /// <summary>
        /// This works around a issue in Zedgraph's ValueHandler.BarCenterValue
        /// (it incorrectly assumes that HiLowBarItems do place themselves 
        /// next to each other like all other BarItems).
        /// </summary>
        /// <param name="curve">The BarItem</param>
        /// <param name="barWidth">The width of the bar</param>
        /// <param name="iCluster">The index of the point in CurveItem.Points</param>
        /// <param name="val">The x-value of the point.</param>
        /// <param name="iOrdinal">The index of the BarItem in the CurveList</param>
        private double BarCenterValue(CurveItem curve, float barWidth, int iCluster,
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
        /// Works around a issue in ValueHandler.BarCenterValue
        /// </summary>
        private bool FindNearestBar(PointF point, out CurveItem nearestCurve, out int iNearest)
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

        protected static IdentityPath GetIdentityPath(CurveItem curveItem)
        {
            return curveItem.Tag as IdentityPath;
        }

        public override bool HandleKeyDownEvent(object sender, KeyEventArgs keyEventArgs)
        {
            switch (keyEventArgs.KeyCode)
            {
                case Keys.Left:
                case Keys.Up:
                    ChangeReplicate(GraphSummary.ResultsIndex - 1);
                    return true;
                case Keys.Right:
                case Keys.Down:
                    ChangeReplicate(GraphSummary.ResultsIndex + 1);
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
            GraphSummary.Cursor = Cursors.Hand;
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
            if (GraphSummary.ResultsIndex != iNearest)
                ChangeReplicate(iNearest);
            else
                GraphSummary.StateProvider.SelectedPath = identityPath;
            return true;
        }

        protected void ChangeReplicate(int index)
        {
            if (index < 0)
                return;
            var document = GraphSummary.DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults || index >= document.Settings.MeasuredResults.Chromatograms.Count)
                return;
            GraphSummary.StateProvider.SelectedResultsIndex = index;
            GraphSummary.Focus();
        }

        public override void HandleResizeEvent()
        {
            ScaleAxisLabels();
        }

        protected void ScaleAxisLabels()
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
        internal abstract class GraphData : Immutable
        {
            protected GraphData(DocNode docNode, DisplayTypeChrom displayType)
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

            public abstract PointPair PointPairMissing(int xValue);

            private List<PointPairList> GetPointPairLists(TransitionDocNode transition,
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
                        if (0 > iStep || iStep >= pointPairLists.Count || IsMissingValue(chromInfo))
                            continue;

                        // Replace the added missing point with the real value
                        var pointPairList = pointPairLists[iStep];
                        pointPairList[pointPairList.Count - 1] = CreatePointPair(iResult, chromInfo);
                    }
                }
                return pointPairLists;
            }

            protected abstract bool IsMissingValue(TransitionChromInfo chromInfo);

            protected abstract PointPair CreatePointPair(int iResult, TransitionChromInfo chromInfo);

            private static int GetCountSteps(IList<TransitionChromInfo> result)
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
                return maxStep*2 + 1;
            }

            private List<PointPairList> GetPointPairLists(TransitionGroupDocNode transitionGroupDocNode,
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
                        if (0 > iStep || iStep >= pointPairLists.Count || IsMissingValue(chromInfo))
                            continue;

                        // Replace the added missing point with the real value
                        var pointPairList = pointPairLists[iStep];
                        pointPairList[pointPairList.Count - 1] = CreatePointPair(iResult, chromInfo);
                    }
                }
                return pointPairLists;
            }

            protected abstract bool IsMissingValue(TransitionGroupChromInfo chromInfo);

            protected abstract PointPair CreatePointPair(int iResult, TransitionGroupChromInfo chromInfo);

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