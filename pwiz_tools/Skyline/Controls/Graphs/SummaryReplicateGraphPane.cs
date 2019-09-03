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
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
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

        protected bool IsMultiSelect { get; set; }

        protected SummaryReplicateGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            XAxis.Title.Text = Resources.SummaryReplicateGraphPane_SummaryReplicateGraphPane_Replicate;
            XAxis.Type = AxisType.Text;
            IsRepeatRemovalAllowed = true;
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

                var index = _replicateGroups.IndexOf(SelectedGroup);
                if (index < 0) // Graph has been updated, but the selection hasn't
                    SelectedGroup = null;

                if (SelectedGroup == null || !SelectedGroup.ReplicateIndexes.Contains(GraphSummary.ResultsIndex))
                    return _replicateGroups.IndexOf(group => group.ReplicateIndexes.Contains(GraphSummary.ResultsIndex));
                else
                    return index;
            }
        }

        private ReplicateGroup SelectedGroup { get; set; }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            return curveItem.Tag as IdentityPath;
        }

        public void SetSelectedFile(string file)
        {
            var group = _replicateGroups.FirstOrDefault(g => (g.FileInfo != null && g.FileInfo.FilePath.GetFileName() == file) || (g.FileInfo == null && file == null));
            if (group != null)
            {
                SelectedGroup = group;
                GraphSummary.UpdateUI();
            }  
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
            if (_replicateGroups == null || 0 > selectedIndex || selectedIndex >= _replicateGroups.Count)
                return;

            var previousFile = SelectedGroup != null ? SelectedGroup.FileInfo : null;
            SelectedGroup = _replicateGroups[selectedIndex];

            if (SelectedGroup.FileInfo != null)
            {
                foreach (var graph in Program.MainWindow.GraphChromatograms)
                {
                    var files = graph.Files;
                    var index = files.IndexOf(SelectedGroup.FileInfo.FilePath.GetFileName());
                    if (index >= 0)
                        graph.SelectedFileIndex = index;
                } 
            }

            // When the user clicks on the graph, they either intend to change
            // the currently selected replicate, or the currently selected IdentityPath.
            // We want to prevent the UI from drilling in too deep when the user
            // just wants to see a different replicate at the same level currently 
            // being viewed (e.g. peptide).
            // Therefore, only change the currently selected IdentityPath if the currently 
            // selected Replicate is the last replicate in the ReplicateGroup that they
            // have clicked on.
            // Updated 10/3/2016 now can drill down to peptide level because multi peptide RT graph is supported.
            var resultIndicesArray = IndexOfReplicate(selectedIndex).ToArray();
            int iSelection = Array.IndexOf(resultIndicesArray, GraphSummary.ResultsIndex);
            if (iSelection == resultIndicesArray.Length - 1 && (IsMultiSelect || !GraphChromatogram.IsSingleTransitionDisplay) && ReferenceEquals(previousFile, SelectedGroup.FileInfo))
            {
                    GraphSummary.StateProvider.SelectPath(identityPath);
                    UpdateGraph(true);
                    return;
            }
            // If there is more than one replicate in the group that they
            // have clicked on, then select the next replicate in that group.
            // The user has clicked on a group which does not contain the 
            // currently selected replicate, then iSelection will be -1, and 
            // the first replicate in that group will be selected.
            int newReplicateIndex = resultIndicesArray[((iSelection + 1) % resultIndicesArray.Length)];
            ChangeSelectedIndex(newReplicateIndex);
            UpdateGraph(true);
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

        protected virtual PointPair PointPairMissing(int xValue)
        {
            return GraphData.CreatePairMissing(xValue);
        }

        protected void EmptyGraph(SrmDocument document)
        {
            string[] resultNames = GraphData.GetReplicateLabels(document).ToArray();
            XAxis.Scale.TextLabels = resultNames;
            var originalTextLabels = new string[XAxis.Scale.TextLabels.Length];
            Array.Copy(XAxis.Scale.TextLabels, originalTextLabels, XAxis.Scale.TextLabels.Length);
            OriginalXAxisLabels = originalTextLabels;

            ScaleAxisLabels();
            // Add a missing point for each replicate name.
            PointPairList pointPairList = new PointPairList();
            for (int i = 0; i < resultNames.Length; i++)
                pointPairList.Add(PointPairMissing(i));
            AxisChange();
        }

        protected Brush GetBrushForNode(SrmSettings settings, DocNode docNode, Color color)
        {
            var transitionDocNode = docNode as TransitionDocNode;
            if (transitionDocNode == null || transitionDocNode.IsQuantitative(settings))
            {
                return new SolidBrush(color);
            }
            return new HatchBrush(HatchStyle.Percent50, color, SystemColors.Window);
        }

        protected static CurveItem CreateLineItem(string label, PointPairList pointPairList, Color color)
        {
            return new LineErrorBarItem(label, pointPairList, color, Color.Black);
        }

        /// <summary>
        /// Holds the data that is currently displayed in the graph.
        /// Currently, we don't hold onto this object, because we never need to look
        /// at the data after the graph is rendered.
        /// </summary>
        public abstract class GraphData : Immutable
        {
            public static PointPair CreatePairMissing(int xValue)
            {
                // Using PointPairBase.Missing caused too many problems in area graphs
                // Zero is essentially missing for column graphs, unlike the retention time hi-lo graphs
                return new PointPair(xValue, 0);
            }

            private readonly SrmDocument _document;
            private readonly ImmutableList<IdentityPath> _selectedDocNodePaths;
            private readonly DisplayTypeChrom _displayType;
            private PaneKey _paneKey;

            private ImmutableList<DocNode> _docNodes;
            private ImmutableList<IdentityPath> _docNodePaths;
            private ImmutableList<String> _docNodeLabels;
            private ImmutableList<List<PointPairList>> _pointPairLists;
            private ImmutableList<ReplicateGroup> _replicateGroups;


            protected GraphData(SrmDocument document, IdentityPath identityPath, DisplayTypeChrom displayType, ReplicateGroupOp replicateGroupOp, PaneKey paneKey)
                : this(document, new[] { identityPath }, displayType, replicateGroupOp, paneKey)
            {
            }

            protected GraphData(SrmDocument document, IEnumerable<IdentityPath> selectedDocNodePaths, DisplayTypeChrom displayType,
                ReplicateGroupOp replicateGroupOp, PaneKey paneKey)
            {
                _document = document;
                _selectedDocNodePaths = ImmutableList.ValueOf(selectedDocNodePaths);
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

            private class PointPairRef
            {
                public PointPairRef(PointPair pointPair, int pointPairListIndex, int groupIndex)
                {
                    PointPair = pointPair;
                    PointPairListIndex = pointPairListIndex;
                    GroupIndex = groupIndex;
                }

                public PointPair PointPair { get; private set; }
                public int PointPairListIndex { get; private set; }
                public int GroupIndex { get; private set; }
            }

            protected virtual void InitData()
            {
                List<DocNode> docNodes = new List<DocNode>();
                List<IdentityPath> docNodePaths = new List<IdentityPath>();
                List<List<PointPairList>> pointPairLists = new List<List<PointPairList>>();
                List<String> docNodeLabels = new List<string>();
                ReplicateGroups = new ReplicateGroup[0];
                foreach (var docNodePath in _selectedDocNodePaths)
                {
                    var docNode = _document.FindNode(docNodePath);
                    var replicateIndices = Enumerable.Range(0, _document.Settings.MeasuredResults.Chromatograms.Count);
                    // ReSharper disable CanBeReplacedWithTryCastAndCheckForNull
                    if (docNode is TransitionDocNode)
                    {
                        var nodeTran = (TransitionDocNode) docNode;
                        ReplicateGroups = GetReplicateGroups(replicateIndices).ToArray();
                        docNodes.Add(nodeTran);
                        docNodePaths.Add(docNodePath);
                        pointPairLists.Add(GetPointPairLists(null, nodeTran, _displayType));
                        docNodeLabels.Add(ChromGraphItem.GetTitle(nodeTran));
                    }
                    else if (docNode is TransitionGroupDocNode)
                    {
                        var nodeGroup = (TransitionGroupDocNode) docNode;
                        ReplicateGroups = GetReplicateGroups(replicateIndices).ToArray();
                        if (_displayType == DisplayTypeChrom.single || _displayType == DisplayTypeChrom.total)
                        {
                            docNodes.Add(nodeGroup);
                            docNodePaths.Add(docNodePath);
                            pointPairLists.Add(GetPointPairLists(nodeGroup, _displayType));
                            docNodeLabels.Add(ChromGraphItem.GetTitle(nodeGroup));
                        }
                        else
                        {
                            foreach (TransitionDocNode nodeTran in GraphChromatogram.GetDisplayTransitions(nodeGroup,
                                _displayType))
                            {
                                docNodes.Add(nodeTran);
                                docNodePaths.Add(new IdentityPath(docNodePath, nodeTran.Id));
                                pointPairLists.Add(GetPointPairLists(nodeGroup, nodeTran, _displayType));
                                docNodeLabels.Add(ChromGraphItem.GetTitle(nodeTran));
                            }
                        }
                    }
                    else if (docNode is PeptideDocNode)
                    {
                        var nodePep = (PeptideDocNode) docNode;
                        ReplicateGroups = GetReplicateGroups(replicateIndices).ToArray();
                        var isMultiSelect = _selectedDocNodePaths.Count > 1 ||
                                            (_selectedDocNodePaths.Count == 1 && Program.MainWindow != null &&
                                             Program.MainWindow.SelectedNode is PeptideGroupTreeNode);
                        foreach (var tuple in GetPeptidePointPairLists(nodePep, isMultiSelect))
                        {
                            docNodes.Add(tuple.Node);
                            docNodePaths.Add(docNodePath);
                            pointPairLists.Add(tuple.PointPairList);
                            docNodeLabels.Add(tuple.DisplaySeq);
                        }
                    }
                }
                // ReSharper restore CanBeReplacedWithTryCastAndCheckForNull
                PointPairLists = pointPairLists;
                DocNodes = docNodes;
                _docNodePaths = ImmutableList.ValueOf(docNodePaths);
                DocNodeLabels = docNodeLabels;

                var oldGroupNames = ReplicateGroups.Select(g => g.GroupName).ToArray();
                var uniqueGroupNames = oldGroupNames.Distinct().ToArray();

                // Instert "All" groups and point pair lists in their correct positions
                InsertAllGroupsAndPointPairLists(uniqueGroupNames, docNodes.Count);

                // Collect all references to points that have a valid Y value
                var references = CollectValidPointRefs(uniqueGroupNames, oldGroupNames, docNodes.Count);

                // Merge groups if their replicate index is the same and each peptide only occurs in one of the files
                MergeGroups(uniqueGroupNames, references);  

                // Remove groups that don't have any peptides in them
                RemoveEmptyGroups(docNodes.Count);
            }

            protected virtual bool IncludeTransition(TransitionDocNode transitionDocNode)
            {
                return transitionDocNode.ExplicitQuantitative || !Settings.Default.ShowQuantitativeOnly;
            }

            private void InsertAllGroupsAndPointPairLists(string[] uniqueGroupNames, int docNodeCount)
            {
                var uniqueGroups = uniqueGroupNames.Select(n => new ReplicateGroup(n,
                    ReplicateIndexSet.OfValues(ReplicateGroups.Where(r => r.GroupName == n).SelectMany(r => r.ReplicateIndexes)),
                    null, true));

                var newGroups = ReplicateGroups.ToList();
                foreach (var group in uniqueGroups)
                {
                    var group1 = group;
                    var firstIndex = newGroups.IndexOf(r => r.GroupName == group1.GroupName);
                    newGroups.Insert(firstIndex, group);

                    for (var node = 0; node < docNodeCount; ++node)
                        for (var step = 0; step < PointPairLists[node].Count; ++step)
                            if(PointPairLists[node][step].Any())
                                PointPairLists[node][step].Insert(firstIndex, new PointPair(firstIndex, double.NaN));
                            
                }
                ReplicateGroups = newGroups;
            }

            private bool ShouldMerge(List<List<PointPairRef>>[] references, IEnumerable<int> groupIndices)
            {
                return references.All(list => list.SelectMany(l => l).Count(p => !p.PointPair.IsMissing && groupIndices.Contains(p.PointPairListIndex)) <= 1);
            }

            private void MergeGroups(string[] uniqueGroupNames, List<List<PointPairRef>>[] references)
            {
                for (var nameIndex = 0; nameIndex < uniqueGroupNames.Length; ++nameIndex)
                {
                    var name = uniqueGroupNames[nameIndex];
                    var groupIndices = ReplicateGroups.Select((r, index) => new { RepGroup = r, Index = index })
                        .Where(g => g.RepGroup.GroupName == name).Select(g => g.Index).ToArray();

                    if (ShouldMerge(references, groupIndices.Skip(1)))
                    {
                        for (var node = 0; node < references.Length; ++node)
                        {
                            for (var step = 0; step < references[node].Count; ++step)
                            {
                                for (var group = 0; group < references[node][step].Count; ++group)
                                {
                                    if (groupIndices.Skip(1).Contains(references[node][step][group].PointPairListIndex))
                                    {
                                        var list = PointPairLists[node][step];
                                        var index = groupIndices.First(i => ReplicateGroups[i].IsAllGroup);

                                        if (list[index].IsInvalid || !references[node][step][group].PointPair.IsInvalid)
                                        {
                                            var x = list[index].X; // Backup x value
                                            list[index] = new PointPair(references[node][step][group].PointPair) { X = x };
                                            references[node][step][group].PointPair.X = references[node][step][group].PointPair.Y = double.NaN;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            private bool IsPointPairValid(PointPair pair)
            {
                return pair.IsInvalid == pair.IsMissing;
            }

            private List<List<PointPairRef>>[] CollectValidPointRefs(string[] uniqueGroupNames, string[] oldGroupNames, int docNodeCount)
            {
                var references = new List<List<PointPairRef>>[docNodeCount];
                for (var node = 0; node < docNodeCount; ++node)
                {
                    references[node] = new List<List<PointPairRef>>();
                    for (var step = 0; step < PointPairLists[node].Count; ++step)
                    {
                        references[node].Add(new List<PointPairRef>());
                        var groupIndex = 0;
                        for (var group = 0; group < PointPairLists[node][step].Count; ++group)
                        {
                            PointPairLists[node][step][group].X = group;
                            if (IsPointPairValid(PointPairLists[node][step][group]))
                                references[node][step].Add(new PointPairRef(PointPairLists[node][step][group], group, groupIndex++));
                        }
                    }
                }
                return references;
            }

            private bool ShouldRemoveGroup(int groupIndex)
            {
                return !PointPairLists.Any(p => p.Any(ppl => ppl.Any() && !ppl[groupIndex].IsInvalid));
            }

            private void RemoveEmptyGroups(int docNodeCount)
            {
                var groups = ReplicateGroups.ToList();
                for (var i = 0; i < groups.Count; ++i)
                {
                    var index = i;
                    var removeGroup = ShouldRemoveGroup(index);
                    if (removeGroup && groups[index].IsAllGroup)
                    {
                        var fileGroups = groups.Select((g, j) => j).Where(j => groups[j].GroupName == groups[index].GroupName && !groups[j].IsAllGroup);
                        removeGroup = fileGroups.Any(j => !ShouldRemoveGroup(j));
                    }
                    if (removeGroup)
                    {
                        for (var node = 0; node < docNodeCount; ++node)
                        {
                            for (var step = 0; step < PointPairLists[node].Count; ++step)
                            {
                                if (PointPairLists[node][step].Any())
                                {
                                    PointPairLists[node][step].RemoveAt(i);
                                    // Fix x values
                                    for (var j = i; j < PointPairLists[node][step].Count; ++j)
                                        --PointPairLists[node][step][j].X;
                                }
                            }
                        }
                        groups.RemoveAt(i);
                        --i;
                    }
                }
                ReplicateGroups = groups;
            }

            protected virtual List<LineInfo> GetPeptidePointPairLists(PeptideDocNode nodePep, bool multiplePeptides)
            {
                var pointPairLists = new List<LineInfo>();
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    if (!_paneKey.IncludesTransitionGroup(nodeGroup))
                    {
                        continue;
                    }
                    pointPairLists.Add(new LineInfo(nodeGroup, ChromGraphItem.GetTitle(nodeGroup), GetPointPairLists(nodeGroup, DisplayTypeChrom.all)));
                }
                return pointPairLists;
            }

            public IList<ReplicateGroup> ReplicateGroups
            {
                get { return _replicateGroups; }
                private set { _replicateGroups = MakeReadOnly(value); }
            }

            private class GroupNameInfo
            {
                public GroupNameInfo(int totalCount, int currentIndex)
                {
                    TotalCount = totalCount;
                    CurrentIndex = currentIndex;
                }

                public int TotalCount { get; private set; }
                public int CurrentIndex { get; set; }
            }

            public IEnumerable<string> GetReplicateLabels()
            {
                EnsureData();
                if (ReplicateGroups == null || !_document.Settings.HasResults)
                    return GetReplicateLabels(_document);

                var groupNameInfos = ReplicateGroups
                    .Select(r => r.GroupName)
                    .GroupBy(n => n, (n, g) => new { name = n, info = new GroupNameInfo(g.Count(), 1) })
                    .ToDictionary(g => g.name, g => g.info);

                var result = new string[ReplicateGroups.Count];
                for (var i = 0; i < ReplicateGroups.Count; ++i)
                {
                    var name = result[i] = ReplicateGroups[i].GroupName;
                    if (groupNameInfos[name].TotalCount <= 1)
                        continue;

                    var index = groupNameInfos[name].CurrentIndex++;
                    result[i] += string.Format(CultureInfo.CurrentCulture, @" ({0})", index);
                }

                return result;
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

            public ImmutableList<IdentityPath> DocNodePaths {
                get
                {
                    EnsureData();
                    return _docNodePaths;
                }
                set { _docNodePaths = value; }
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

            public ReplicateGroupOp ReplicateGroupOp { get; private set; }

            public virtual PointPair PointPairMissing(int xValue)
            {
                return CreatePairMissing(xValue);
            }

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
                            var chromInfosForStep = new List<TChromInfoData>();
                            if (replicateGroup.FileInfo != null)
                            {
                                var info = chromInfoDatas.FirstOrDefault(chromInfoData => chromInfoData != null && chromInfoData.ChromFileInfo.Equals(replicateGroup.FileInfo) && step == chromInfoData.OptimizationStep);
                                chromInfosForStep.Add(info);
                            }
                            else
                            {
                                var step1 = step;
                                var chromInfos =
                                    chromInfoDatas.Where(
                                        chromInfoData => chromInfoData != null &&
                                                         step1 == chromInfoData.OptimizationStep);
                                chromInfosForStep.AddRange(chromInfos);
                            }
                                
                            chromInfoDatasForStep.AddRange(chromInfosForStep.Where(c => c != null && !isMissingValue(c)));
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
                if (!IncludeTransition(nodeTran))
                {
                    return new List<PointPairList>();
                }
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

            protected virtual IEnumerable<ReplicateGroup> GetReplicateGroups(IEnumerable<int> replicateIndexes)
            {
                if (_document.Settings.MeasuredResults == null)
                {
                    return new ReplicateGroup[0]; // Likely user removed all results while Mass Errors window was open
                }

                var annotationCalculator = new AnnotationCalculator(_document);
                var chromatograms = _document.Settings.MeasuredResults.Chromatograms;

                var result = new List<ReplicateGroup>();
                if (ReplicateGroupOp.GroupByValue == null)
                {
                    foreach (var index in replicateIndexes)
                    {
                        var chromatogram = _document.MeasuredResults.Chromatograms[index];
                        result.AddRange(chromatogram.MSDataFileInfos.Select(fileInfo => new ReplicateGroup(chromatogram.Name, ReplicateIndexSet.Singleton(index), fileInfo)));
                    }

                    var query = result.OrderBy(g => 0);
                    if (!string.IsNullOrEmpty(OrderByReplicateAnnotation))
                    {
                        var orderByReplicateValue = ReplicateValue.FromPersistedString(_document.Settings, OrderByReplicateAnnotation);
                        if (orderByReplicateValue != null)
                        {
                            query = result.OrderBy(
                                g => orderByReplicateValue.GetValue(annotationCalculator,
                                    chromatograms[g.ReplicateIndexes.First()]), CollectionUtil.ColumnValueComparer);
                        }
                    }

                    if (ReplicateOrder == SummaryReplicateOrder.document)
                    {
                        result = new List<ReplicateGroup>(query.ThenBy(g => g.ReplicateIndexes.ToArray(),
                                Comparer<int[]>.Create((a, b) =>
                                {
                                    for (var i = 0; i < Math.Min(a.Length, b.Length); ++i)
                                    {
                                        if (a[i] != b[i])
                                            return a[i].CompareTo(b[i]);
                                    }

                                    return a.Length.CompareTo(b.Length);
                                })).ThenBy(g => g.FileInfo.FileIndex));
                    }
                    else if (ReplicateOrder == SummaryReplicateOrder.time)
                    {
                        result = new List<ReplicateGroup>(query
                            .ThenBy(g => g.FileInfo.RunStartTime,
                                Comparer<DateTime?>.Create(
                                    (a, b) => (a ?? DateTime.MaxValue).CompareTo(
                                        b ?? DateTime.MaxValue))).ThenBy(g => g.FileInfo.FileIndex).ToList());
                    }
                }
                else
                {
                    var lookup = replicateIndexes.ToLookup(replicateIndex =>
                        ReplicateGroupOp.GroupByValue.GetValue(annotationCalculator, chromatograms[replicateIndex]));
                    var keys = lookup.Select(grouping => grouping.Key).ToList();
                    if (keys.Count > 2)
                    {
                        // If there are more than 2 groups then exclude replicates with blank annotation values.
                        keys.Remove(null);
                    }

                    keys.Sort(CollectionUtil.ColumnValueComparer);
                    // ReSharper disable AssignNullToNotNullAttribute
                    foreach (var key in keys)
                    {
                        result.Add(new ReplicateGroup(key?.ToString() ?? string.Empty, ReplicateIndexSet.OfValues(lookup[key])));
                    }
                    // ReSharper restore AssignNullToNotNullAttribute
                }

                return result.Distinct();
            }

            private void AddReplicateGroup(ICollection<ReplicateGroup> replicateGroups, DocNode docNode, int replicateIndex)
            {
                var nodeGroup = docNode as TransitionGroupDocNode;
                if (nodeGroup != null)
                {
                    var chromSet = _document.MeasuredResults.Chromatograms[replicateIndex];
                    foreach (var c in nodeGroup.ChromInfos)
                    {
                        var info = chromSet.GetFileInfo(c.FileId);
                        if (info != null)
                            replicateGroups.Add(new ReplicateGroup(chromSet.Name, ReplicateIndexSet.Singleton(replicateIndex), info));
                    }
                    return;
                }

                var nodePep = docNode as PeptideDocNode;
                if (nodePep != null)
                {
                   nodePep.TransitionGroups.ForEach(n => AddReplicateGroup(replicateGroups, n, replicateIndex));
                }
            }
        }
    }

    public class ReplicateIndexSet : ValueSet<ReplicateIndexSet, int>
    {
    }


    public class ReplicateGroup
    {
        public ReplicateGroup(string groupName, ReplicateIndexSet replicateIndexes, ChromFileInfo fileInfo = null, bool isAllGroup = false)
        {
            GroupName = groupName;
            FileInfo = fileInfo;
            ReplicateIndexes = replicateIndexes;
            IsAllGroup = isAllGroup;
        }

        public string GroupName { get; private set; }

        public ChromFileInfo FileInfo { get; private set; }
        public ReplicateIndexSet ReplicateIndexes { get; private set; }
        public bool IsAllGroup { get; private set; }

        #region object overrides

        protected bool Equals(ReplicateGroup other)
        {
            return string.Equals(GroupName, other.GroupName) && Equals(FileInfo, other.FileInfo) && Equals(ReplicateIndexes, other.ReplicateIndexes) && IsAllGroup == other.IsAllGroup;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ReplicateGroup)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (GroupName != null ? GroupName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (FileInfo != null ? FileInfo.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ReplicateIndexes != null ? ReplicateIndexes.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsAllGroup.GetHashCode();
                return hashCode;
            }
        }        

        #endregion
    }

    public class LineInfo
    {
        public DocNode Node { set; get; }
        public string DisplaySeq { set; get; }
        public List<PointPairList> PointPairList { set; get; }
        public LineInfo(DocNode _node, string _displaySeq, List<PointPairList> _ppList)
        {
            Node = _node;
            DisplaySeq = _displaySeq;
            PointPairList = _ppList;
        }
    }
}
