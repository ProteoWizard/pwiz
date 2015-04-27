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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
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
    public abstract class SummaryReplicateGraphPane : SummaryBarGraphPaneBase
    {
        public static SummaryReplicateOrder ReplicateOrder
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.ReplicateOrderEnum, SummaryReplicateOrder.document);
            }

            set { Settings.Default.ReplicateOrderEnum = value.ToString(); }
        }

        public static string GroupByReplicateAnnotation
        {
            get { return Settings.Default.GroupByReplicateAnnotation; }
            set { Settings.Default.GroupByReplicateAnnotation = value; }
        }

        public static string OrderByReplicateAnnotation
        {
            get { return Settings.Default.OrderByReplicateAnnotation; }
            set { Settings.Default.OrderByReplicateAnnotation = value; }
        }

        protected DocNode _parentNode;
        protected IList<ReplicateGroup> _replicateGroups;

        protected SummaryReplicateGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            XAxis.Title.Text = Resources.SummaryReplicateGraphPane_SummaryReplicateGraphPane_Replicate;
            XAxis.Type = AxisType.Text;
        }

        protected virtual void InitFromData(GraphData graphData)
        {
            string[] resultNamesOrdered = graphData.GetReplicateLabels().ToArray();
            XAxis.Title.Text = graphData.ReplicateGroupOp.ReplicateAxisTitle;
            if (!ArrayUtil.EqualsDeep(resultNamesOrdered, XAxis.Scale.TextLabels))
            {
                XAxis.Scale.TextLabels = resultNamesOrdered;
                ScaleAxisLabels();
            }
            _replicateGroups = graphData.ReplicateGroups;
        }

        protected override int SelectedIndex
        {
            get
            {
                if (_replicateGroups == null)
                {
                    return -1;
                }
                return _replicateGroups.IndexOf(group => group.ReplicateIndexes.Contains(GraphSummary.ResultsIndex));
            }
        }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            return curveItem.Tag as IdentityPath;
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
            if (0 > selectedIndex || selectedIndex >= _replicateGroups.Count)
                return;

            // When the user clicks on the graph, they either intend to change
            // the currently selected replicate, or the currently selected IdentityPath.
            // We want to prevent the UI from drilling in too deep when the user
            // just wants to see a different replicate at the same level currently 
            // being viewed (e.g. peptide).
            // Therefore, only change the currently selected IdentityPath if the currently 
            // selected Replicate is the last replicate in the ReplicateGroup that they
            // have clicked on.
            var resultIndicesArray = IndexOfReplicate(selectedIndex).ToArray();
            int iSelection = Array.IndexOf(resultIndicesArray, GraphSummary.ResultsIndex);
            if (iSelection == resultIndicesArray.Length - 1 && !GraphChromatogram.IsSingleTransitionDisplay)
            {
                if (!Equals(GraphSummary.StateProvider.SelectedPath, identityPath))
                {
                    GraphSummary.StateProvider.SelectedPath = identityPath;
                    return;
                }
            }
            // If there is more than one replicate in the group that they
            // have clicked on, then select the next replicate in that group.
            // The user has clicked on a group which does not contain the 
            // currently selected replicate, then iSelection will be -1, and 
            // the first replicate in that group will be selected.
            int newReplicateIndex = resultIndicesArray[((iSelection + 1) % resultIndicesArray.Length)];
            ChangeSelectedIndex(newReplicateIndex);                
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

        public ReplicateIndexSet IndexOfReplicate(int index)
        {
            return (_replicateGroups != null ? _replicateGroups[index].ReplicateIndexes : ReplicateIndexSet.EMPTY);
        }


        /// <summary>
        /// Holds the data that is currently displayed in the graph.
        /// Currently, we don't hold onto this object, because we never need to look
        /// at the data after the graph is rendered.
        /// </summary>
        public abstract class GraphData : Immutable
        {
            private readonly SrmDocument _document;
            private readonly DocNode _docNode;
            private readonly DisplayTypeChrom _displayType;
            private PaneKey _paneKey;

            private ImmutableList<DocNode> _docNodes;
            private ImmutableList<String> _docNodeLabels;
            private ImmutableList<List<PointPairList>> _pointPairLists;
            private ImmutableList<ReplicateGroup> _replicateGroups;


            protected GraphData(SrmDocument document, DocNode docNode, DisplayTypeChrom displayType, GraphValues.ReplicateGroupOp replicateGroupOp, PaneKey paneKey)
            {
                _document = document;
                _docNode = docNode;
                _displayType = displayType;
                ReplicateGroupOp = replicateGroupOp;
                _paneKey = paneKey;
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
// ReSharper disable CanBeReplacedWithTryCastAndCheckForNull
                if (_docNode is TransitionDocNode)
                {
                    var nodeTran = (TransitionDocNode)_docNode;
                    ReplicateGroups = GetReplicateGroups(GetReplicateIndices(nodeTran)).ToArray();
                    docNodes.Add(nodeTran);
                    pointPairLists.Add(GetPointPairLists(null, nodeTran, _displayType));
                    docNodeLabels.Add(ChromGraphItem.GetTitle(nodeTran));
                }
                else if (_docNode is TransitionGroupDocNode)
                {
                    var nodeGroup = (TransitionGroupDocNode)_docNode;
                    ReplicateGroups = GetReplicateGroups(GetReplicateIndices(nodeGroup)).ToArray();
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
                    ReplicateGroups = GetReplicateGroups(GetReplicateIndices(nodePep)).ToArray();
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        if (!_paneKey.IncludesTransitionGroup(nodeGroup))
                        {
                            continue;
                        }
                        docNodes.Add(nodeGroup);
                        pointPairLists.Add(GetPointPairLists(nodeGroup, DisplayTypeChrom.total));
                        docNodeLabels.Add(ChromGraphItem.GetTitle(nodeGroup));
                    }
                }
// ReSharper restore CanBeReplacedWithTryCastAndCheckForNull
                PointPairLists = pointPairLists;
                DocNodes = docNodes;
                DocNodeLabels = docNodeLabels;
            }

            public IList<ReplicateGroup> ReplicateGroups
            {
                get { return _replicateGroups; }
                private set { _replicateGroups = MakeReadOnly(value); }
            }

            public IEnumerable<string> GetReplicateLabels()
            {
                EnsureData();
                if (ReplicateGroups == null || !_document.Settings.HasResults)
                    return GetReplicateLabels(_document);

                return ReplicateGroups.Select(replicateGroup => replicateGroup.GroupName);
            }

            public static IEnumerable<string> GetReplicateLabels(SrmDocument document)
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

            public GraphValues.ReplicateGroupOp ReplicateGroupOp { get; private set; }

            public abstract PointPair PointPairMissing(int xValue);

            private List<PointPairList> MakePointPairLists<TChromInfoData>(
                DisplayTypeChrom displayType, 
                IList<ICollection<TChromInfoData>> chromInfoResults,
                Func<TChromInfoData, bool> isMissingValue, 
                Func<int, ICollection<TChromInfoData>, PointPair> createPointPair) where TChromInfoData : ChromInfoData
            {
                var pointPairLists = new List<PointPairList>();
                if (null == chromInfoResults)
                {
                    pointPairLists.Add(new PointPairList());
                    return pointPairLists;
                }
                int maxSteps = 1;
                bool allowSteps = (displayType == DisplayTypeChrom.single);
                if (allowSteps)
                {
                    foreach (var result in chromInfoResults)
                        maxSteps = Math.Max(maxSteps, GetCountSteps(result));
                }
                for (int i = 0; i < maxSteps; i++)
                    pointPairLists.Add(new PointPairList());

                int numSteps = maxSteps / 2;
                IList<ReplicateGroup> replicateGroups = _replicateGroups;
                for (int iGroup = 0; iGroup < replicateGroups.Count; iGroup++)
                {
                    // Fill everything with missing data until filled for real
                    foreach (PointPairList pairList in pointPairLists)
                        pairList.Add(PointPairMissing(iGroup));
                    var replicateGroup = replicateGroups[iGroup];
                    var chromInfoLists = new List<ICollection<TChromInfoData>>();
                    foreach (var replicateIndex in replicateGroup.ReplicateIndexes)
                    {
                        if (replicateIndex < 0 || replicateIndex >= chromInfoResults.Count)
                        {
                            continue;
                        }
                        chromInfoLists.Add(chromInfoResults[replicateIndex]);
                    }
                    if (0 == chromInfoLists.Count)
                    {
                        continue;
                    }
                    var optimizationSteps = chromInfoLists
                        .SelectMany(chromInfoList => from chromInfoData in chromInfoList
                                                     where chromInfoData.ChromInfo != null
                                                     select chromInfoData.OptimizationStep)
                        .Distinct()
                        .ToArray();
                    foreach (int step in optimizationSteps)
                    {
                        int iStep = step + numSteps;
                        if (0 > iStep || iStep >= pointPairLists.Count)
                            continue;
                        var chromInfoDatasForStep = new List<TChromInfoData>();
                        foreach (var chromInfoDatas in chromInfoLists)
                        {
                            var chromInfoForStep = chromInfoDatas.FirstOrDefault(chromInfoData =>
                                chromInfoData != null && step == chromInfoData.OptimizationStep);

                            if (null == chromInfoForStep || isMissingValue(chromInfoForStep))
                            {
                                continue;
                            }
                            chromInfoDatasForStep.Add(chromInfoForStep);
                        }
                        var pointPairList = pointPairLists[iStep];
                        pointPairList[pointPairList.Count - 1] = createPointPair(iGroup, chromInfoDatasForStep);
                    }
                }
                return pointPairLists;

            }

            private List<PointPairList> GetPointPairLists(TransitionGroupDocNode nodeGroup,
                                                          TransitionDocNode nodeTran,
                                                          DisplayTypeChrom displayType)
            {
                var transitionChromInfoDatas = TransitionChromInfoData.GetTransitionChromInfoDatas(
                    _document.Settings.MeasuredResults, nodeTran.Results);

                return MakePointPairLists(displayType, transitionChromInfoDatas, IsMissingValue, CreatePointPair);
            }

            protected abstract bool IsMissingValue(TransitionChromInfoData chromInfo);

            protected abstract PointPair CreatePointPair(int iResult, ICollection<TransitionChromInfoData> chromInfos);

            private static int GetCountSteps(IEnumerable<ChromInfoData> result)
            {
                // Only for the first file
                if (result == null)
                    return 0;

                int? fileIndex = null;
                int maxStep = 0;
                foreach (var chromInfoData in result.Where(chromInfoData => chromInfoData.ChromInfo != null))
                {
                    if (fileIndex.HasValue)
                    {
                        if (fileIndex != chromInfoData.ChromInfo.FileIndex)
                        {
                            continue;
                        }  
                    } 
                    else
                    {
                        fileIndex = chromInfoData.ChromInfo.FileIndex;
                    }
                    maxStep = Math.Max(maxStep, chromInfoData.OptimizationStep);
                }
                return maxStep*2 + 1;
            }

            private List<PointPairList> GetPointPairLists(TransitionGroupDocNode nodeGroup,
                                                         DisplayTypeChrom displayType)
            {
                var transitionGroupChromInfoDatas = TransitionGroupChromInfoData.GetTransitionGroupChromInfoDatas(
                    _document.Settings.MeasuredResults, nodeGroup.Results);
                return MakePointPairLists(displayType, transitionGroupChromInfoDatas, IsMissingValue, CreatePointPair);
            }

            protected abstract bool IsMissingValue(TransitionGroupChromInfoData chromInfoData);

            protected abstract PointPair CreatePointPair(int iResult, ICollection<TransitionGroupChromInfoData> chromInfo);

            private IEnumerable<ReplicateGroup> GetReplicateGroups(IEnumerable<int> replicateIndexes)
            {
                var chromatograms = _document.Settings.MeasuredResults.Chromatograms;
                if (ReplicateGroupOp.GroupByAnnotation == null)
                {
                    return
                        replicateIndexes.Select(replicateIndex 
                            => new ReplicateGroup(chromatograms[replicateIndex].Name,
                                                  ReplicateIndexSet.Singleton(replicateIndex)));
                }
                var lookup = replicateIndexes.ToLookup(replicateIndex => chromatograms[replicateIndex].Annotations.GetAnnotation(ReplicateGroupOp.GroupByAnnotation));
                var keys = lookup.Select(grouping => grouping.Key).ToList();
                if (keys.Count > 2)
                {
                    // If there are more than 2 groups then exclude replicates with blank annotation values.
                    keys.Remove(null);
                }
                keys.Sort();
// ReSharper disable AssignNullToNotNullAttribute
                return keys.Select(key => new ReplicateGroup((key ?? string.Empty).ToString(), ReplicateIndexSet.OfValues(lookup[key])));
// ReSharper restore AssignNullToNotNullAttribute
            }

            private IEnumerable<int> GetReplicateIndices(PeptideDocNode nodePep)
            {
                return GetReplicateIndices(i => GetChromFileInfoId(nodePep.Results, i));
            }

            private IEnumerable<int> GetReplicateIndices(TransitionDocNode nodeTran)
            {
                return GetReplicateIndices(i => GetChromFileInfoId(nodeTran.Results, i));
            }

            private IEnumerable<int> GetReplicateIndices(TransitionGroupDocNode nodeGroup)
            {
                return GetReplicateIndices(i => GetChromFileInfoId(nodeGroup.Results, i));
            }

            private IEnumerable<int> GetReplicateIndices(Func<int, ChromFileInfoId> getFileId)
            {
                var chromatograms = _document.Settings.MeasuredResults.Chromatograms;
                if (ReplicateOrder == SummaryReplicateOrder.document && null == OrderByReplicateAnnotation)
                {
                    for (int iResult = 0; iResult < chromatograms.Count; iResult++)
                        yield return iResult;
                }
                else
                {
                    // Create a list of tuple's that will sort according to user's options.
                    // The sort is optionally by an annotation value, and then optionally acquired time, 
                    // and finally document order.
                    var listIndexFile = new List<Tuple<object, DateTime, int>>();
                    AnnotationDef orderByReplicateAnnotationDef = null;
                    if (null != OrderByReplicateAnnotation)
                    {
                        orderByReplicateAnnotationDef = _document.Settings.DataSettings.AnnotationDefs.FirstOrDefault(
                                annotationDef => annotationDef.Name == OrderByReplicateAnnotation);
                    }
                    for (int iResult = 0; iResult < chromatograms.Count; iResult++)
                    {
                        var chromSet = _document.Settings.MeasuredResults.Chromatograms[iResult];
                        
                        ChromFileInfoId fileId = getFileId(iResult);
                        ChromFileInfo fileInfo = (fileId != null ? chromSet.GetFileInfo(fileId) : null);
                        object annotationValue = null;
                        DateTime replicateTime = DateTime.MaxValue;
                        if (null != orderByReplicateAnnotationDef)
                        {
                            annotationValue = chromSet.Annotations.GetAnnotation(orderByReplicateAnnotationDef);
                        }
                        if (null != fileInfo && ReplicateOrder == SummaryReplicateOrder.time)
                        {
                            replicateTime = fileInfo.RunStartTime ?? DateTime.MaxValue;
                        }
                        listIndexFile.Add(new Tuple<object, DateTime, int>(annotationValue, replicateTime, iResult));
                    }

                    listIndexFile.Sort();
                    foreach (var tuple in listIndexFile)
                        yield return tuple.Item3;
                }
            }
        }

        private static ChromFileInfoId GetChromFileInfoId<TItem>(Results<TItem> results, int iReplicate) where TItem : ChromInfo
        {
            if (null == results || iReplicate >= results.Count)
            {
                return null;
            }
            var chromInfoList = results[iReplicate];
            if (chromInfoList != null && chromInfoList.Count > 0 && chromInfoList[0] != null)
            {
                return chromInfoList[0].FileId;
            }
            return null;
        }
    }

    public class ReplicateIndexSet : ValueSet<ReplicateIndexSet, int>
    {
    }

    public class ReplicateGroup
    {
        public ReplicateGroup(string groupName, ReplicateIndexSet replicateIndexes)
        {
            GroupName = groupName;
            ReplicateIndexes = replicateIndexes;
        }
        public string GroupName { get; private set; }
        public ReplicateIndexSet ReplicateIndexes { get; private set; }
    }
}