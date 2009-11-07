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
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Graph pane which shows the comparison of retention times across the replicates.
    /// </summary>
    internal class RTReplicateGraphPane : RTGraphPane
    {
        private static readonly Color[] COLORS_TRANSITION = GraphChromatogram.COLORS_LIBRARY;
        private static readonly Color[] COLORS_GROUPS = GraphChromatogram.COLORS_GROUPS;

        private DocNode _parentNode;

        public RTReplicateGraphPane()
        {
            XAxis.Title.Text = "Replicate";
            XAxis.Type = AxisType.Text;
            YAxis.Title.Text = "Measured Time";
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
                    ChangeReplicate(GraphRetentionTime.ResultsIndex - 1);
                    return true;
                case Keys.Right:
                case Keys.Down:
                    ChangeReplicate(GraphRetentionTime.ResultsIndex + 1);
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
            GraphRetentionTime.Cursor = Cursors.Hand;
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
            if (GraphRetentionTime.ResultsIndex != iNearest)
                ChangeReplicate(iNearest);
            else
                GraphRetentionTime.StateProvider.SelectedPath = identityPath;
            return true;
        }

        private void ChangeReplicate(int index)
        {
            if (index < 0)
                return;
            var document = GraphRetentionTime.DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults || index >= document.Settings.MeasuredResults.Chromatograms.Count)
                return;
            GraphRetentionTime.StateProvider.SelectedResultsIndex = index;
            GraphRetentionTime.Focus();
        }

        public override void HandleResizeEvent()
        {
            ScaleAxisLabels();
        }

        private void ScaleAxisLabels()
        {
            int dyAvailable = (int) Rect.Height/4;
            int dxAvailable = (int) Rect.Width/Math.Max(1, XAxis.Scale.TextLabels.Count());
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

        private int MaxWidth(Font font, IEnumerable<String> labels)
        {
            var result = 0;
            foreach (var label in labels)
            {
                result = Math.Max(result, SystemMetrics.GetTextWidth(font, label));
            }
            return result;
        }

        public override void UpdateGraph(bool checkData)
        {
            SrmDocument document = GraphRetentionTime.DocumentUIContainer.DocumentUI;
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
            var selectedTreeNode = GraphRetentionTime.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode == null)
            {
                // Add a missing point for each replicate name.
                PointPairList pointPairList = new PointPairList();
                for (int i = 0; i < resultNames.Length; i++)
                    pointPairList.Add(GraphData.PointPairMissing(i));
                AxisChange();
                return;
            }
            const DisplayTypeChrom displayType = DisplayTypeChrom.all;
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
                    parentNode = children[0];
                    identityPath = new IdentityPath(identityPath, parentNode.Id);
                }
            }
            else if (!(selectedTreeNode is TransitionGroupTreeNode))
            {
                Title.Text = "Select a peptide to see the retention time graph";
                return;
            }
            Title.Text = null;
            GraphData graphData = new GraphData(parentNode, displayType);

            int selectedReplicateIndex = GraphRetentionTime.ResultsIndex;
            double minRetentionTime = double.MaxValue;
            double maxRetentionTime = -double.MaxValue;
            for (int i = 0; i < graphData.DocNodes.Count; i++)
            {
                var docNode = graphData.DocNodes[i];
                Color color;
                if (parentNode is PeptideDocNode || displayType == DisplayTypeChrom.total)
                {
                    color = COLORS_GROUPS[i % COLORS_GROUPS.Length];                    
                }
                else if (docNode.Equals(selectedNode) && graphData.DocNodes.Count != 1)
                {
                    color = ChromGraphItem.ColorSelected;
                }
                else
                {
                    color = COLORS_TRANSITION[i%COLORS_TRANSITION.Length];
                }
                PointPairList pointPairList = graphData.PointPairLists[i];
                var curveItem = new HiLowMiddleErrorBarItem(
                    graphData.DocNodeLabels[i], pointPairList, color, Color.Black);
                if (selectedReplicateIndex < pointPairList.Count)
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
            // Draw a box around the currently selected replicate
            if (minRetentionTime != double.MaxValue)
            {
                GraphObjList.Add(new BoxObj(selectedReplicateIndex + .5, maxRetentionTime, 1,
                                            maxRetentionTime - minRetentionTime, Color.Black, Color.Empty)
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
                List<PointPairList> pointPairLists = new List<PointPairList>();
                List<String> docNodeLabels = new List<string>();
                if (docNode is TransitionDocNode)
                {
                    TransitionDocNode transitionDocNode = (TransitionDocNode) docNode;
                    docNodes.Add(transitionDocNode);
                    pointPairLists.Add(GetPointPairList(transitionDocNode));
                    docNodeLabels.Add(ChromGraphItem.GetTitle(transitionDocNode));
                }
                else if (docNode is TransitionGroupDocNode)
                {
                    if (displayType == DisplayTypeChrom.total)
                    {
                        TransitionGroupDocNode transitionGroup = (TransitionGroupDocNode)docNode;
                        docNodes.Add(transitionGroup);
                        pointPairLists.Add(GetPointPairList(transitionGroup));
                        docNodeLabels.Add(ChromGraphItem.GetTitle(transitionGroup));                        
                    }
                    else
                    {
                        foreach (TransitionDocNode transitionDocNode in ((TransitionGroupDocNode)docNode).Children)
                        {
                            docNodes.Add(transitionDocNode);
                            pointPairLists.Add(GetPointPairList(transitionDocNode));
                            docNodeLabels.Add(ChromGraphItem.GetTitle(transitionDocNode));
                        }                        
                    }
                }
                else if (docNode is PeptideDocNode)
                {
                    foreach (TransitionGroupDocNode transitionGroup in ((PeptideDocNode)docNode).Children)
                    {
                        docNodes.Add(transitionGroup);
                        pointPairLists.Add(GetPointPairList(transitionGroup));
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
            public IList<PointPairList> PointPairLists { get; private set; }

            public static PointPair PointPairMissing(int xValue)
            {
                return HiLowMiddleErrorBarItem.MakePointPair(xValue,
                                                             PointPairBase.Missing, PointPairBase.Missing, PointPairBase.Missing, 0);
            }

            static PointPairList GetPointPairList(TransitionDocNode transition)
            {
                PointPairList pointPairList = new PointPairList();
                if (!transition.HasResults)
                    return pointPairList;
                for (int iResult = 0; iResult < transition.Results.Count; iResult++)
                {
                    var result = transition.Results[iResult];
                    if (result == null || result.Count == 0)
                    {
                        pointPairList.Add(PointPairMissing(iResult));
                        continue;
                    }
                    var transitionResult = result[0];
                    if (transitionResult.StartRetentionTime == 0 || transitionResult.EndRetentionTime == 0)
                    {
                        pointPairList.Add(PointPairMissing(iResult));
                        continue;
                    }
                    pointPairList.Add(HiLowMiddleErrorBarItem.MakePointPair(iResult, transitionResult.EndRetentionTime,
                                                                            transitionResult.StartRetentionTime, transitionResult.RetentionTime, transitionResult.Fwhm));
                }
                return pointPairList;
            }

            static PointPairList GetPointPairList(TransitionGroupDocNode transitionGroupDocNode)
            {
                PointPairList pointPairList = new PointPairList();
                if (!transitionGroupDocNode.HasResults)
                    return pointPairList;
                for (int iResult = 0; iResult < transitionGroupDocNode.Results.Count; iResult++)
                {
                    var result = transitionGroupDocNode.Results[iResult];
                    if (result == null || result.Count == 0 || !result[0].RetentionTime.HasValue)
                    {
                        pointPairList.Add(PointPairMissing(iResult));
                        continue;
                    }
                    var transitionGroupResult = result[0];
                    if (!transitionGroupResult.StartRetentionTime.HasValue || !transitionGroupResult.EndRetentionTime.HasValue)
                    {
                        pointPairList.Add(PointPairMissing(iResult));
                        continue;
                    }
                    pointPairList.Add(HiLowMiddleErrorBarItem.MakePointPair(iResult,
                                                                            transitionGroupResult.EndRetentionTime.Value,
                                                                            transitionGroupResult.StartRetentionTime.Value,
                                                                            transitionGroupResult.RetentionTime.Value,
                                                                            transitionGroupResult.Fwhm ?? 0));
                }
                return pointPairList;
            }
        }
    }
}