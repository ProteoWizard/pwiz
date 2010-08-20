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
using System.Collections.ObjectModel;
using System.Linq;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Graph pane which shows the comparison of retention times across the replicates.
    /// </summary>
    internal abstract class SummaryReplicateGraphPane : SummaryBarGraphPaneBase
    {
        protected DocNode _parentNode;

        protected SummaryReplicateGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            XAxis.Title.Text = "Replicate";
            XAxis.Type = AxisType.Text;
        }

        protected override int SelectedIndex
        {
            get { return GraphSummary.ResultsIndex; }
        }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            return curveItem.Tag as IdentityPath;
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
            // Just change the active replicate, if the user clicks on a
            // different replicate.  Change the selection, if the replicate
            // is already selected.  This keeps the UI from drilling in too
            // deep when the user just wants to see a different replicate
            // at the same level currently being view (e.g. peptide)
            if (GraphSummary.ResultsIndex != selectedIndex ||
                    GraphChromatogram.DisplayType == DisplayTypeChrom.single)
                ChangeSelectedIndex(selectedIndex);
            else if (identityPath != null)
                GraphSummary.StateProvider.SelectedPath = identityPath;
        }

        protected void ChangeSelectedIndex(int index)
        {
            if (index < 0)
                return;
            var document = GraphSummary.DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults || index >= document.Settings.MeasuredResults.Chromatograms.Count)
                return;
            GraphSummary.StateProvider.SelectedResultsIndex = index;
            GraphSummary.Focus();
        }

        /// <summary>
        /// Holds the data that is currently displayed in the graph.
        /// Currently, we don't hold onto this object, because we never need to look
        /// at the data after the graph is rendered.
        /// </summary>
        internal abstract class GraphData : Immutable
        {
            private readonly DocNode _docNode;
            private readonly DisplayTypeChrom _displayType;

            private ReadOnlyCollection<DocNode> _docNodes;
            private ReadOnlyCollection<String> _docNodeLabels;
            private ReadOnlyCollection<List<PointPairList>> _pointPairLists;

            protected GraphData(DocNode docNode, DisplayTypeChrom displayType)
            {
                _docNode = docNode;
                _displayType = displayType;
            }

            /// <summary>
            /// Moved out of the constructor for better support of virtual functions called
            /// in this code.
            /// </summary>
            protected void EnsureData()
            {
                if (_pointPairLists == null)
                    InitData();
            }

            protected virtual void InitData()
            {
                List<DocNode> docNodes = new List<DocNode>();
                List<List<PointPairList>> pointPairLists = new List<List<PointPairList>>();
                List<String> docNodeLabels = new List<string>();
                if (_docNode is TransitionDocNode)
                {
                    TransitionDocNode transitionDocNode = (TransitionDocNode)_docNode;
                    docNodes.Add(transitionDocNode);
                    pointPairLists.Add(GetPointPairLists(transitionDocNode, _displayType));
                    docNodeLabels.Add(ChromGraphItem.GetTitle(transitionDocNode));
                }
                else if (_docNode is TransitionGroupDocNode)
                {
                    if (_displayType != DisplayTypeChrom.all)
                    {
                        TransitionGroupDocNode transitionGroup = (TransitionGroupDocNode)_docNode;
                        docNodes.Add(transitionGroup);
                        pointPairLists.Add(GetPointPairLists(transitionGroup, _displayType));
                        docNodeLabels.Add(ChromGraphItem.GetTitle(transitionGroup));
                    }
                    else
                    {
                        foreach (TransitionDocNode transitionDocNode in ((TransitionGroupDocNode)_docNode).Children)
                        {
                            docNodes.Add(transitionDocNode);
                            pointPairLists.Add(GetPointPairLists(transitionDocNode, _displayType));
                            docNodeLabels.Add(ChromGraphItem.GetTitle(transitionDocNode));
                        }
                    }
                }
                else if (_docNode is PeptideDocNode)
                {
                    foreach (TransitionGroupDocNode transitionGroup in ((PeptideDocNode)_docNode).Children)
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

            public IList<DocNode> DocNodes
            {
                get
                {
                    EnsureData();
                    return _docNodes;
                }
                private set
                {
                    _docNodes = MakeReadOnly(value);
                }
            }

            public IList<String> DocNodeLabels
            {
                get
                {
                    EnsureData();
                    return _docNodeLabels;
                }
                private set
                {
                    _docNodeLabels = MakeReadOnly(value);
                }
            }

            public IList<List<PointPairList>> PointPairLists
            {
                get
                {
                    EnsureData();
                    return _pointPairLists;
                }
                private set
                {
                    _pointPairLists = MakeReadOnly(value);
                }
            }

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