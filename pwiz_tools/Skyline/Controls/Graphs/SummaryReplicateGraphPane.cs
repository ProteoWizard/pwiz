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
using System.Collections.ObjectModel;
using System.Linq;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum SummaryReplicateOrder { document, time }

    /// <summary>
    /// Graph pane which shows the comparison of retention times across the replicates.
    /// </summary>
    internal abstract class SummaryReplicateGraphPane : SummaryBarGraphPaneBase
    {
        public static SummaryReplicateOrder ReplicateOrder
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.ReplicateOrderEnum, SummaryReplicateOrder.document);
            }

            set { Settings.Default.ReplicateOrderEnum = value.ToString(); }
        }

        protected DocNode _parentNode;
        protected IList<int> _replicateIndices;

        protected SummaryReplicateGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            XAxis.Title.Text = "Replicate";
            XAxis.Type = AxisType.Text;
        }

        protected virtual void InitFromData(GraphData graphData)
        {
            string[] resultNamesOrdered = graphData.GetReplicateNames().ToArray();
            if (!ArrayUtil.EqualsDeep(resultNamesOrdered, XAxis.Scale.TextLabels))
            {
                XAxis.Scale.TextLabels = resultNamesOrdered;
                ScaleAxisLabels();
            }
            _replicateIndices = graphData.ReplicateIndices;
        }

        protected override int SelectedIndex
        {
            get { return (_replicateIndices != null ? _replicateIndices.IndexOf(GraphSummary.ResultsIndex) : -1); }
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
            if (0 > selectedIndex || selectedIndex >= _replicateIndices.Count)
                return;

            int iResult = IndexOfReplicate(selectedIndex);
            if (GraphSummary.ResultsIndex != iResult || GraphChromatogram.IsSingleTransitionDisplay)
            {
                ChangeSelectedIndex(iResult);                
            }
            else if (identityPath != null)
            {
                GraphSummary.StateProvider.SelectedPath = identityPath;
            }
        }

        protected void ChangeSelectedIndex(int iResult)
        {
            if (iResult < 0)
                return;
            var document = GraphSummary.DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults || iResult >= document.Settings.MeasuredResults.Chromatograms.Count)
                return;
            GraphSummary.StateProvider.SelectedResultsIndex = iResult;
            GraphSummary.Focus();
        }

        public int IndexOfReplicate(int index)
        {
            return (_replicateIndices != null ? _replicateIndices[index] : -1);
        }


        /// <summary>
        /// Holds the data that is currently displayed in the graph.
        /// Currently, we don't hold onto this object, because we never need to look
        /// at the data after the graph is rendered.
        /// </summary>
        internal abstract class GraphData : Immutable
        {
            private readonly SrmDocument _document;
            private readonly DocNode _docNode;
            private readonly DisplayTypeChrom _displayType;

            private ReadOnlyCollection<DocNode> _docNodes;
            private ReadOnlyCollection<String> _docNodeLabels;
            private ReadOnlyCollection<List<PointPairList>> _pointPairLists;
            private ReadOnlyCollection<int> _replicateIndices;

            protected GraphData(SrmDocument document, DocNode docNode, DisplayTypeChrom displayType)
            {
                _document = document;
                _docNode = docNode;
                _displayType = displayType;
            }

            protected DisplayTypeChrom DisplayType { get { return _displayType; } }

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
                    var nodeTran = (TransitionDocNode)_docNode;
                    ReplicateIndices = GetReplicateIndices(nodeTran).ToArray();
                    docNodes.Add(nodeTran);
                    pointPairLists.Add(GetPointPairLists(null, nodeTran, _displayType));
                    docNodeLabels.Add(ChromGraphItem.GetTitle(nodeTran));
                }
                else if (_docNode is TransitionGroupDocNode)
                {
                    var nodeGroup = (TransitionGroupDocNode)_docNode;
                    ReplicateIndices = GetReplicateIndices(nodeGroup).ToArray();
                    if (_displayType == DisplayTypeChrom.single || _displayType == DisplayTypeChrom.total)
                    {
                        docNodes.Add(nodeGroup);
                        pointPairLists.Add(GetPointPairLists(nodeGroup, _displayType));
                        docNodeLabels.Add(ChromGraphItem.GetTitle(nodeGroup));
                    }
                    else
                    {
                        foreach (TransitionDocNode nodeTran in GraphChromatogram.GetDisplayTransitions(nodeGroup, _displayType))
                        {
                            docNodes.Add(nodeTran);
                            pointPairLists.Add(GetPointPairLists(nodeGroup, nodeTran, _displayType));
                            docNodeLabels.Add(ChromGraphItem.GetTitle(nodeTran));
                        }
                    }
                }
                else if (_docNode is PeptideDocNode)
                {
                    var nodePep = (PeptideDocNode) _docNode;
                    ReplicateIndices = GetReplicateIndices(nodePep).ToArray();
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        docNodes.Add(nodeGroup);
                        pointPairLists.Add(GetPointPairLists(nodeGroup, DisplayTypeChrom.total));
                        docNodeLabels.Add(ChromGraphItem.GetTitle(nodeGroup));
                    }
                }
                PointPairLists = pointPairLists;
                DocNodes = docNodes;
                DocNodeLabels = docNodeLabels;
            }

            public IList<int> ReplicateIndices
            {
                get { return _replicateIndices; }
                private set { _replicateIndices = MakeReadOnly(value); }
            }

            public IEnumerable<string> GetReplicateNames()
            {
                EnsureData();
                if (ReplicateIndices == null || !_document.Settings.HasResults)
                    return GetReplicateNames(_document);

                return from iResult in ReplicateIndices
                       select _document.Settings.MeasuredResults.Chromatograms[iResult].Name;
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

            private List<PointPairList> GetPointPairLists(TransitionGroupDocNode nodeGroup,
                                                          TransitionDocNode nodeTran,
                                                          DisplayTypeChrom displayType)
            {
                var pointPairLists = new List<PointPairList>();
                if (!nodeTran.HasResults)
                {
                    pointPairLists.Add(new PointPairList());
                    return pointPairLists;                    
                }
                int maxSteps = 1;
                bool allowSteps = (displayType == DisplayTypeChrom.single);
                if (allowSteps)
                {
                    foreach (var result in nodeTran.Results)
                        maxSteps = Math.Max(maxSteps, GetCountSteps(result));                    
                }
                for (int i = 0; i < maxSteps; i++)
                    pointPairLists.Add(new PointPairList());

                int numSteps = maxSteps/2;
                foreach (int iResult in nodeGroup != null
                                            ? GetReplicateIndices(nodeGroup)
                                            : GetReplicateIndices(nodeTran))
                {
                    // Fill everything with missing data until filled for real
                    foreach (PointPairList pairList in pointPairLists)
                        pairList.Add(PointPairMissing(iResult));

                    if (0 > iResult || iResult >= nodeTran.Results.Count)
                        continue;
                    var result = nodeTran.Results[iResult];
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

            private List<PointPairList> GetPointPairLists(TransitionGroupDocNode nodeGroup,
                                                         DisplayTypeChrom displayType)
            {
                var pointPairLists = new List<PointPairList>();
                if (!nodeGroup.HasResults)
                {
                    pointPairLists.Add(new PointPairList());
                    return pointPairLists;
                }
                int maxSteps = 1;
                bool allowSteps = (displayType == DisplayTypeChrom.single);
                if (allowSteps)
                {
                    foreach (var result in nodeGroup.Results)
                        maxSteps = Math.Max(maxSteps, GetCountSteps(result));
                }
                for (int i = 0; i < maxSteps; i++)
                    pointPairLists.Add(new PointPairList());

                int numSteps = maxSteps / 2;
                foreach (int iResult in GetReplicateIndices(nodeGroup))
                {
                    // Fill everything with missing data until filled for real
                    for (int i = 0; i < maxSteps; i++)
                        pointPairLists[i].Add(PointPairMissing(iResult));

                    var result = nodeGroup.Results[iResult];
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

            private IEnumerable<int> GetReplicateIndices(PeptideDocNode nodePep)
            {
                return GetReplicateIndices(i =>
                                               {
                                                   var result = nodePep.HasResults && i < nodePep.Results.Count ? nodePep.Results[i] : null;
                                                   return (result != null ? result[0].FileId : null);
                                               });
            }

            private IEnumerable<int> GetReplicateIndices(TransitionDocNode nodeTran)
            {
                return GetReplicateIndices(i =>
                {
                    var result = nodeTran.HasResults && i < nodeTran.Results.Count ? nodeTran.Results[i] : null;
                    return (result != null ? result[0].FileId : null);
                });
            }

            private IEnumerable<int> GetReplicateIndices(TransitionGroupDocNode nodeGroup)
            {
                return GetReplicateIndices(i =>
                {
                    var result = nodeGroup.HasResults && i < nodeGroup.Results.Count ? nodeGroup.Results[i] : null;
                    return (result != null ? result[0].FileId : null);
                });
            }

            private IEnumerable<int> GetReplicateIndices(Func<int, ChromFileInfoId> getFileId)
            {
                var chromatograms = _document.Settings.MeasuredResults.Chromatograms;
                var order = ReplicateOrder;
                if (order == SummaryReplicateOrder.document)
                {
                    for (int iResult = 0; iResult < chromatograms.Count; iResult++)
                        yield return iResult;
                }
                else
                {
                    var listIndexFile = new List<KeyValuePair<int, ChromFileInfo>>();
                    for (int iResult = 0; iResult < chromatograms.Count; iResult++)
                    {
                        var chromSet = _document.Settings.MeasuredResults.Chromatograms[iResult];
                        
                        ChromFileInfoId fileId = getFileId(iResult);
                        ChromFileInfo fileInfo = (fileId != null ? chromSet.GetFileInfo(fileId) : null);

                        listIndexFile.Add(new KeyValuePair<int, ChromFileInfo>(iResult, fileInfo));
                    }

                    // Sort by acquisition time, followed by document order for entries with
                    // an acquisition time
                    listIndexFile.Sort((p1, p2) =>
                                           {
                                               var t1 = p1.Value != null ? p1.Value.RunStartTime : null;
                                               var t2 = p2.Value != null ? p2.Value.RunStartTime : null;
                                               if (t1 != null && t2 != null)
                                                   return Comparer.Default.Compare(t1, t2);
                                               // Put all null values at the end, in document order
                                               if (t1 != null)
                                                   return -1;
                                               if (t2 != null)
                                                   return 1;
                                               return Comparer.Default.Compare(p1.Key, p2.Key);
                                           });

                    foreach (var pair in listIndexFile)
                        yield return pair.Key;
                }
            }
        }
    }
}