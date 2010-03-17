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
using System.Diagnostics;
using System.IO;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class TransitionGroupDocNode : DocNodeParent
    {
        public const int MIN_DOT_PRODUCT_TRANSITIONS = 4;

        public TransitionGroupDocNode(TransitionGroup id,
                                      double massH,
                                      RelativeRT relativeRT,
                                      TransitionDocNode[] children)
            : this(id, Annotations.Empty, massH, relativeRT, null, null, children, true)
        {
        }

        public TransitionGroupDocNode(TransitionGroup id,
                                      double massH,
                                      RelativeRT relativeRT,
                                      TransitionDocNode[] children,
                                      bool autoManageChildren)
            : this(id, Annotations.Empty, massH, relativeRT, null, null, children, autoManageChildren)
        {
        }

        public TransitionGroupDocNode(TransitionGroup id,
                                      Annotations annotations,
                                      double massH,
                                      RelativeRT relativeRT,
                                      SpectrumHeaderInfo libInfo,
                                      Results<TransitionGroupChromInfo> results,
                                      TransitionDocNode[] children,
                                      bool autoManageChildren)
            : base(id, annotations, children, autoManageChildren)
        {
            PrecursorMz = SequenceMassCalc.GetMZ(massH, id.PrecursorCharge);
            RelativeRT = relativeRT;
            LibInfo = libInfo;
            Results = results;
        }

        private TransitionGroupDocNode(TransitionGroupDocNode group,
                                       double precursorMz,
                                       RelativeRT relativeRT,
                                       IList<DocNode> children)
            : base(group.TransitionGroup, group.Annotations, children, group.AutoManageChildren)
        {
            PrecursorMz = precursorMz;
            RelativeRT = relativeRT;
            LibInfo = group.LibInfo;
            Results = group.Results;
        }

        public TransitionGroup TransitionGroup { get { return (TransitionGroup) Id; }}

        public bool IsLight { get { return TransitionGroup.LabelType == IsotopeLabelType.light; } }

        public RelativeRT RelativeRT { get; private set; }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.precursor; } }

        public double PrecursorMz { get; private set; }

        public SpectrumHeaderInfo LibInfo { get; private set; }

        public bool HasLibInfo { get { return LibInfo != null; } }

        public float GetRankValue(PeptideRankId rankId)
        {
            if (!HasLibInfo)
                return float.MinValue;
            if (ReferenceEquals(rankId, LibrarySpec.PEP_RANK_PICKED_INTENSITY))
            {
                // CONSIDER: Can manually picked transitions end up in this
                //           calculation, and if so how big a problem is that?
                float intensity = 0;
                foreach (TransitionDocNode nodeTran in Children)
                {
                    if (nodeTran.HasLibInfo)
                        intensity += nodeTran.LibInfo.Intensity;
                }
                return intensity;
            }
            return LibInfo.GetRankValue(rankId);
        }

        public Results<TransitionGroupChromInfo> Results { get; private set; }

        public bool HasResults { get { return Results != null; } }

        public IEnumerable<TransitionGroupChromInfo> ChromInfos
        {
            get
            {
                if (HasResults)
                {
                    foreach (var result in Results)
                    {
                        if (result == null)
                            continue;
                        foreach (var chromInfo in result)
                            yield return chromInfo;
                    }
                }
            }
        }

        public ChromInfoList<TransitionGroupChromInfo> GetSafeChromInfo(int i)
        {
            return (HasResults && Results.Count > i ? Results[i] : null);
        }

        private TransitionGroupChromInfo GetChromInfoEntry(int i)
        {
            var result = GetSafeChromInfo(i);
            // CONSIDER: Also specify the file index and/or optimization step?
            if (result != null)
            {
                foreach (var chromInfo in result)
                {
                    if (chromInfo.OptimizationStep == 0)
                        return chromInfo;
                }
            }
            return null;
        }

        public float? GetPeakCountRatio(int i)
        {
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return chromInfo.PeakCountRatio;
        }

        public float? AveragePeakCountRatio
        {
            get
            {
                return GetAverageResultValue(chromInfo =>
                    chromInfo.OptimizationStep != 0 ?
                        (float?) null : chromInfo.PeakCountRatio);
            }
        }

        public float? GetPeakAreaRatio(int i, out float? stdev)
        {
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
            {
                stdev = null;
                return null;                
            }

            stdev = chromInfo.RatioStdev;
            return chromInfo.Ratio;
        }

        public float? GetLibraryDotProduct(int i)
        {
            // CONSIDER: Also specify the file index?
            var result = GetChromInfoEntry(i);
            if (result == null)
                return null;
            return result.LibraryDotProduct;
        }

        public float? AverageLibraryDotProduct
        {
            get
            {
                return GetAverageResultValue(chromInfo =>
                    chromInfo.OptimizationStep != 0 ?
                        (float?)null : chromInfo.LibraryDotProduct);
            }
        }

        public float? AveragePeakCenterTime
        {
            get
            {
                return GetAverageResultValue(chromInfo =>
                    chromInfo.OptimizationStep != 0 || chromInfo.PeakCountRatio < 0.5 ?
                        (float?) null :
                        (chromInfo.StartRetentionTime.Value + chromInfo.EndRetentionTime.Value) / 2);
            }
        }

        private float? GetAverageResultValue(Func<TransitionGroupChromInfo, float?> getVal)
        {
            return HasResults ? Results.GetAverageValue(getVal) : null;
        }

        /// <summary>
        /// Node level depths below this node
        /// </summary>
        // ReSharper disable InconsistentNaming
        public enum Level { Transitions }
        // ReSharper restore InconsistentNaming

        public int TransitionCount { get { return GetCount((int)Level.Transitions); } }

        public TransitionGroupDocNode ChangeSettings(SrmSettings settingsNew, ExplicitMods mods, SrmSettingsDiff diff)
        {
            double precursorMz = PrecursorMz;
            RelativeRT relativeRT = RelativeRT;
            SpectrumHeaderInfo libInfo = LibInfo;
            var transitionRanks = new Dictionary<double, LibraryRankedSpectrumInfo.RankedMI>();
            if (diff.DiffTransitionGroupProps)
            {
                string seq = TransitionGroup.Peptide.Sequence;
                double massH = settingsNew.GetPrecursorMass(TransitionGroup.LabelType, seq, mods);
                precursorMz = SequenceMassCalc.GetMZ(massH, TransitionGroup.PrecursorCharge);
                relativeRT = settingsNew.GetRelativeRT(TransitionGroup.LabelType, seq, mods);
            }

            bool autoSelectTransitions = diff.DiffTransitions &&
                settingsNew.TransitionSettings.Filter.AutoSelect && AutoManageChildren;

            if (diff.DiffTransitionGroupProps || diff.DiffTransitions || diff.DiffTransitionProps)
            {
                // Skip transition ranking, if only transition group properties changed
                var transitionRanksLib = transitionRanks;
                if (!diff.DiffTransitions && !diff.DiffTransitionProps)
                    transitionRanksLib = null;
                // If transitions are not changing, then it is necessary to get all rankings,
                // since any group may contain reranked transitions
                TransitionGroup.GetLibraryInfo(settingsNew, mods, autoSelectTransitions, ref libInfo, transitionRanksLib);
            }

            TransitionGroupDocNode nodeResult = this;
            if (autoSelectTransitions)
            {
                IList<DocNode> childrenNew = new List<DocNode>();

                Dictionary<Identity, DocNode> mapIdToChild = CreateIdContentToChildMap();
                foreach (TransitionDocNode nodeNew in TransitionGroup.GetTransitions(settingsNew, mods, precursorMz, libInfo, transitionRanks, true))
                {
                    TransitionDocNode nodeTransition;

                    DocNode existing;
                    // Add values that existed before the change.
                    if (mapIdToChild.TryGetValue(nodeNew.Transition, out existing))
                    {
                        nodeTransition = (TransitionDocNode) existing;
                        if (diff.DiffTransitionProps)
                        {
                            var tran = nodeTransition.Transition;
                            double massH = settingsNew.GetFragmentMass(TransitionGroup.LabelType, mods, tran);
                            var info = TransitionDocNode.GetLibInfo(tran, massH, transitionRanks);
                            nodeTransition = new TransitionDocNode(tran, massH, info);

                            Helpers.AssignIfEquals(ref nodeTransition, (TransitionDocNode) existing);
                        }
                    }
                    // Add the new node
                    else
                    {
                        nodeTransition = nodeNew;
                    }

                    if (nodeTransition != null)
                        childrenNew.Add(nodeTransition);
                }

                if (!ArrayUtil.ReferencesEqual(childrenNew, Children))
                    nodeResult = new TransitionGroupDocNode(this, precursorMz, relativeRT, childrenNew);
                else
                {
                    if (precursorMz != PrecursorMz)
                        nodeResult = new TransitionGroupDocNode(this, precursorMz, relativeRT, Children);
                    else
                    {
                        // If nothing changed, use this node.
                        nodeResult = this;
                    }
                }
            }
            else if (diff.DiffTransitionProps)
            {
                IList<DocNode> childrenNew = new List<DocNode>();

                // Enumerate the nodes making necessary changes.
                foreach (TransitionDocNode nodeTransition in Children)
                {
                    var tran = nodeTransition.Transition;
                    var annotations = nodeTransition.Annotations;   // Don't lose annotations
                    var results = nodeTransition.Results;           // Results changes happen later
                    double massH = settingsNew.GetFragmentMass(TransitionGroup.LabelType, mods, tran);
                    var info = TransitionDocNode.GetLibInfo(tran, massH, transitionRanks);
                    var nodeNew = new TransitionDocNode(tran, annotations, massH, info, results);

                    Helpers.AssignIfEquals(ref nodeNew, nodeTransition);
                    childrenNew.Add(nodeNew);
                }

                // Change as little as possible
                if (!ArrayUtil.ReferencesEqual(childrenNew, Children))
                    nodeResult = new TransitionGroupDocNode(this, precursorMz, relativeRT, childrenNew);
                else if (precursorMz != PrecursorMz || relativeRT != RelativeRT)
                    nodeResult = new TransitionGroupDocNode(this, precursorMz, relativeRT, Children);
                else
                    nodeResult = this;
            }
            else if (diff.DiffTransitionGroupProps)
            {
                nodeResult = new TransitionGroupDocNode(this, precursorMz, relativeRT, Children);
            }

            // One final check for a library info change
            if (!Equals(libInfo, nodeResult.LibInfo))
                nodeResult = nodeResult.ChangeLibInfo(libInfo);

            if (diff.DiffResults || ChangedResults(nodeResult))
                nodeResult = nodeResult.UpdateResults(settingsNew, diff, this);

            return nodeResult;
        }

        private TransitionGroupDocNode UpdateResults(SrmSettings settingsNew, SrmSettingsDiff diff,
            TransitionGroupDocNode nodePrevious)
        {
            // Make sure no results are present, if the new settings has no results,
            // or this node has no children
            if (!settingsNew.HasResults || Children.Count == 0)
            {
                if (!HasResults)
                    return this;

                // Clear results from this node
                var nodeResult = ChangeResults(null);
                // Clear results from all children
                DocNode[] childrenNew = new DocNode[Children.Count];
                for (int iTran = 0; iTran < childrenNew.Length; iTran++)
                {
                    var nodeTran = (TransitionDocNode)Children[iTran];
                    childrenNew[iTran] = nodeTran.ChangeResults(null);
                }
                return (TransitionGroupDocNode) nodeResult.ChangeChildren(childrenNew);
            }
            else
            {
                // Recalculate results information
                bool integrateAll = settingsNew.TransitionSettings.Integration.IsIntegrateAll;

                // Store indexes to previous results in a dictionary for lookup
                var dictChromIdIndex = new Dictionary<int, int>();
                var settingsOld = diff.SettingsOld;
                
                if (settingsOld != null && settingsOld.HasResults)
                {
                    // As long as integration strategy has not changed
                    if (integrateAll == settingsOld.TransitionSettings.Integration.IsIntegrateAll)
                    {
                        int i = 0;
                        foreach (var chromSet in settingsOld.MeasuredResults.Chromatograms)
                            dictChromIdIndex.Add(chromSet.Id.GlobalIndex, i++);
                    }
                }

                float mzMatchTolerance = (float) settingsNew.TransitionSettings.Instrument.MzMatchTolerance;
                var measuredResults = settingsNew.MeasuredResults;
                var listChromSets = measuredResults.Chromatograms;
                var resultsCalc = new TransitionGroupResultsCalculator(this, listChromSets, dictChromIdIndex);
                foreach (var chromatograms in listChromSets)
                {
                    ChromatogramGroupInfo[] arrayChromInfo;
                    // Check if this object has existing results information
                    int indexOld;
                    if (!dictChromIdIndex.TryGetValue(chromatograms.Id.GlobalIndex, out indexOld))
                        indexOld = -1;
                    else
                    {
                        Debug.Assert(settingsOld != null && settingsOld.HasResults);

                        // If there is existing results information, and it was set
                        // by the user, then preserve it, and skip automatic peak picking
                        var resultOld = Results != null ? Results[indexOld] : null;
                        if (resultOld != null && (UserSetResults(resultOld) ||
                                // or this set of results is not yet loaded
                                !chromatograms.IsLoaded ||
                                // or not forcing a full recalc of all peaks, chromatograms have not
                                // changed and the node has not otherwise changed yet.
                                // (happens while loading results)
                                (!diff.DiffResultsAll &&
                                 ReferenceEquals(chromatograms, settingsOld.MeasuredResults.Chromatograms[indexOld]) &&
                                 Equals(this, nodePrevious))))
                        {
                            for (int iTran = 0; iTran < Children.Count; iTran++)
                            {
                                var nodeTran = (TransitionDocNode)Children[iTran];
                                var results = nodeTran.HasResults ? nodeTran.Results[indexOld] : null;
                                if (results == null)
                                    resultsCalc.AddTransitionChromInfo(iTran, null);
                                else
                                    resultsCalc.AddTransitionChromInfo(iTran, results.ToArray());
                            }
                            continue;                            
                        }
                    }

                    if (measuredResults.TryLoadChromatogram(chromatograms, this, mzMatchTolerance, false, out arrayChromInfo))
                    {
                        // Find the file indexes once
                        int countGroupInfos = arrayChromInfo.Length;
                        int[] fileIndexes = new int[countGroupInfos];
                        for (int j = 0; j < countGroupInfos; j++)
                            fileIndexes[j] = chromatograms.IndexOfFile(arrayChromInfo[j]);

                        // Figure out the number of steps for this chromatogram set, if it has
                        // an optimization function.
                        int numSteps = 0;
                        if (chromatograms.OptimizationFunction != null)
                            numSteps = chromatograms.OptimizationFunction.StepCount;

                        // Calculate the transition info, and the max values for the transition group
                        for (int iTran = 0; iTran < Children.Count; iTran++)
                        {
                            var nodeTran = (TransitionDocNode) Children[iTran];
                            // Use existing information, if it is still equivalent to the
                            // chosen peak.
                            var results = nodeTran.HasResults && indexOld != -1 ?
                                nodeTran.Results[indexOld] : null;

                            var listTranInfo = new List<TransitionChromInfo>();
                            for (int j = 0; j < countGroupInfos; j++)
                            {
                                // Get all transition chromatogram info for this file.
                                ChromatogramGroupInfo chromGroupInfo = arrayChromInfo[j];
                                int fileIndex = fileIndexes[j];

                                ChromatogramInfo[] infos = chromGroupInfo.GetAllTransitionInfo((float)nodeTran.Mz,
                                    mzMatchTolerance, chromatograms.OptimizationFunction);

                                // Always add the right number of steps to the list, no matter
                                // how many entries were returned.
                                int offset = infos.Length/2 - numSteps;
                                int countInfos = numSteps*2 + 1;
                                // Make sure nothing gets added when no measurements are present
                                if (infos.Length == 0)
                                    countInfos = 0;
                                for (int i = 0; i < countInfos; i++)
                                {
                                    ChromatogramInfo info = null;
                                    int iInfo = i + offset;
                                    if (0 <= iInfo && iInfo < infos.Length)
                                        info = infos[iInfo];

                                    // Check for existing info that was set by the user.
                                    int step = i - numSteps;
                                    var chromInfo = FindChromInfo(results, fileIndex, step);
                                    if (chromInfo == null || !chromInfo.UserSet)
                                    {
                                        ChromPeak peak = (info != null && info.BestPeakIndex != -1 ?
                                            info.GetPeak(info.BestPeakIndex) : ChromPeak.EMPTY);
                                        if (!integrateAll && peak.IsForcedIntegration)
                                            peak = ChromPeak.EMPTY;

                                        // Avoid creating new info objects that represent the same data
                                        // in use before.
                                        if (chromInfo == null || !chromInfo.Equivalent(fileIndex, step, peak))
                                        {
                                            // Use the old ratio for now, and it will be corrected by the peptide,
                                            // if it is incorrect.
                                            float? ratio = (chromInfo != null ? chromInfo.Ratio : null);
                                            chromInfo = new TransitionChromInfo(fileIndex, step, peak, ratio, false);
                                        }
                                    }

                                    listTranInfo.Add(chromInfo);
                                }
                            }
                            if (listTranInfo.Count == 0)
                                resultsCalc.AddTransitionChromInfo(iTran, null);
                            else
                                resultsCalc.AddTransitionChromInfo(iTran, listTranInfo);
                        }
                    }
                    else
                    {
                        for (int iTran = 0; iTran < Children.Count; iTran++)
                            resultsCalc.AddTransitionChromInfo(iTran, null);
                    }
                }

                return resultsCalc.UpdateTransitionGroupNode(this);
            }
        }

        private static TransitionChromInfo FindChromInfo(IEnumerable<TransitionChromInfo> results, int fileIndex, int step)
        {
            if (results != null)
            {
                foreach (var chromInfo in results)
                {
                    if (fileIndex == chromInfo.FileIndex && step == chromInfo.OptimizationStep)
                        return chromInfo;
                }
            }
            return null;
        }

        /// <summary>
        /// Determine if all <see cref="TransitionGroupChromInfo"/> elements in a list
        /// were set by the user.  Usually there will only be one to check.
        /// </summary>
        /// <param name="results">The list to check</param>
        /// <returns>True if all were set</returns>
        private static bool UserSetResults(IList<TransitionGroupChromInfo> results)
        {
            return results.IndexOf(info => info != null && !info.UserSet) == -1;
        }

        private bool ChangedResults(DocNodeParent nodeGroup)
        {
            if (nodeGroup.Children.Count != Children.Count)
                return true;

            int iChild = 0;
            foreach (TransitionDocNode nodeTran in Children)
            {
                // Results will differ if the identies of the children differ
                // at all.
                var nodeTran2 = (TransitionDocNode) nodeGroup.Children[iChild];
                if (!ReferenceEquals(nodeTran.Id, nodeTran2.Id))
                    return true;

                // or if the results for any transition have changed
                if (!ReferenceEquals(nodeTran.Results, nodeTran2.Results))
                    return true;

                iChild++;
            }
            return false;
        }

        private sealed class TransitionGroupResultsCalculator
        {
            private readonly TransitionGroupDocNode _nodeGroup;
            private readonly List<TransitionGroupChromInfoListCalculator> _listResultCalcs;
            private readonly List<IList<TransitionChromInfo>>[] _arrayChromInfoSets;
            // Allow look-up of former result position
            private readonly IList<ChromatogramSet> _listChromSets;
            private readonly Dictionary<int, int> _dictChromIdIndex;

            public TransitionGroupResultsCalculator(TransitionGroupDocNode nodeGroup,
                                                    IList<ChromatogramSet> listChromSets,
                                                    Dictionary<int, int> dictChromIdIndex)
            {
                _nodeGroup = nodeGroup;
                _listChromSets = listChromSets;
                _dictChromIdIndex = dictChromIdIndex;

                // Shouldn't be necessary to create one of these, if there are
                // no transitions
                int countTransitions = nodeGroup.Children.Count;
                Debug.Assert(countTransitions > 0);
                _listResultCalcs = new List<TransitionGroupChromInfoListCalculator>();
                _arrayChromInfoSets = new List<IList<TransitionChromInfo>>[countTransitions];
                for (int iTran = 0; iTran < countTransitions; iTran++)
                    _arrayChromInfoSets[iTran] = new List<IList<TransitionChromInfo>>();
            }

            public void AddTransitionChromInfo(int iTran, IList<TransitionChromInfo> info)
            {
                var listInfo = _arrayChromInfoSets[iTran];
                int iNext = listInfo.Count;
                listInfo.Add(info);

                // Make sure the list of group result calculators has iNext entries
                while (_listResultCalcs.Count <= iNext)
                {
                    int transitionCount = _arrayChromInfoSets.Length;
                    ChromInfoList<TransitionGroupChromInfo> listChromInfo = null;
                    int iResult = _listResultCalcs.Count;
                    if (_nodeGroup.HasResults)
                    {
                        int iResultOld = GetOldPosition(iResult);
                        if (iResultOld != -1)
                            listChromInfo = _nodeGroup.Results[iResultOld];
                    }
                    _listResultCalcs.Add(new TransitionGroupChromInfoListCalculator(transitionCount, listChromInfo));
                }
                // Add the iNext entry
                _listResultCalcs[iNext].AddChromInfoList(info);
            }

            private int GetOldPosition(int iResult)
            {
                if (iResult < _listChromSets.Count)
                {
                    int iResultOld;
                    if (_dictChromIdIndex.TryGetValue(_listChromSets[iResult].Id.GlobalIndex, out iResultOld))
                        return iResultOld;
                }
                return -1;
            }

            public TransitionGroupDocNode UpdateTransitionGroupNode(TransitionGroupDocNode nodeGroup)
            {
                // Make sure transitions are correctly ranked by the area they add
                // to the total
                RankAndCorrelateTransitions(nodeGroup);

                // Update nodes with new results as necessary
                IList<DocNode> childrenNew = new List<DocNode>();
                for (int iTran = 0, len = nodeGroup.Children.Count; iTran < len; iTran++)
                {
                    var nodeTran = (TransitionDocNode)nodeGroup.Children[iTran];
                    childrenNew.Add(UpdateTranisitionNode(nodeTran, iTran));
                }

                var listChromInfoLists = _listResultCalcs.ConvertAll(calc => calc.CalcChromInfoList());
                var results = Results<TransitionGroupChromInfo>.Merge(nodeGroup.Results, listChromInfoLists);

                var nodeGroupNew = nodeGroup;
                if (!Equals(results, nodeGroupNew.Results))
                    nodeGroupNew = nodeGroupNew.ChangeResults(results);

                nodeGroupNew = (TransitionGroupDocNode)nodeGroupNew.ChangeChildrenChecked(childrenNew);
                return nodeGroupNew;
            }

            private TransitionDocNode UpdateTranisitionNode(TransitionDocNode nodeTran, int iTran)
            {
                var listChromInfoLists = _arrayChromInfoSets[iTran];
                var results = Results<TransitionChromInfo>.Merge(nodeTran.Results, listChromInfoLists);
                if (Equals(results, nodeTran.Results))
                    return nodeTran;
                return nodeTran.ChangeResults(results);
            }

            private void RankAndCorrelateTransitions(TransitionGroupDocNode nodeGroup)
            {
                int countTransitions = _arrayChromInfoSets.Length;
                var arrayRanked = new KeyValuePair<int, TransitionChromInfo>[countTransitions];
                for (int i = 0; i < _listResultCalcs.Count; i++)
                {
                    for (int iInfo = 0; /* internal break */ ; iInfo++)
                    {
                        int countInfo = 0;
                        double[] peakAreas = null, libIntensities = null;
                        if (nodeGroup.HasLibInfo && nodeGroup.Children.Count >= MIN_DOT_PRODUCT_TRANSITIONS)
                        {
                            peakAreas = new double[countTransitions];
                            libIntensities = new double[countTransitions];
                        }
                        int fileIndex = 0, optStep = 0;
                        for (int iTran = 0; iTran < countTransitions; iTran++)
                        {
                            var results = _arrayChromInfoSets[iTran][i];
                            var chromInfo = (results != null && iInfo < results.Count ? results[iInfo] : null);
                            arrayRanked[iTran] =
                                new KeyValuePair<int, TransitionChromInfo>(iTran, chromInfo);
                            // Count non-null info
                            if (chromInfo != null)
                            {
                                countInfo++;
                                fileIndex = chromInfo.FileIndex;
                                optStep = chromInfo.OptimizationStep;
                            }

                            // Store information for correlation score
                            if (peakAreas != null)
                            {
                                peakAreas[iTran] = GetSafeArea(chromInfo);
                                libIntensities[iTran] = GetSafeIntensity((TransitionDocNode)nodeGroup.Children[iTran]);
                            }
                        }

                        // End when no rankable info is found for a file index
                        if (countInfo == 0)
                            break;                            

                        // Calculate correlation score
                        if (peakAreas != null)
                            _listResultCalcs[i].SetLibInfo(fileIndex, optStep, peakAreas, libIntensities);

                        // Sort by area descending
                        Array.Sort(arrayRanked, (p1, p2) =>
                                                Comparer<float>.Default.Compare(GetSafeRankArea(p2.Value),
                                                                                GetSafeRankArea(p1.Value)));
                        // Change any TransitionChromInfo items that do not have the right rank.
                        for (int iRank = 0; iRank < countTransitions; iRank++)
                        {
                            var pair = arrayRanked[iRank];
                            if (pair.Value == null)
                                continue;
                            int rank = (pair.Value.Area > 0 ? iRank + 1 : 0);
                            if (pair.Value.Rank != rank)
                                _arrayChromInfoSets[pair.Key][i][iInfo] = pair.Value.ChangeRank(rank);
                        }
                    }
                }
            }


            private static float GetSafeArea(TransitionChromInfo info)
            {
                return (info != null ? info.Area : 0.0f);
            }

            private static float GetSafeRankArea(TransitionChromInfo info)
            {
                return (info != null ? info.Area : -1.0f);
            }

            private static float GetSafeIntensity(TransitionDocNode nodeTran)
            {
                return (nodeTran.HasLibInfo ? nodeTran.LibInfo.Intensity : 0);
            }
        }

        private sealed class TransitionGroupChromInfoListCalculator
        {
            private readonly ChromInfoList<TransitionGroupChromInfo> _listChromInfo;

            public TransitionGroupChromInfoListCalculator(int transitionCount,
                ChromInfoList<TransitionGroupChromInfo> listChromInfo)
            {
                _listChromInfo = listChromInfo;

                TransitionCount = transitionCount;
                Calculators = new List<TransitionGroupChromInfoCalculator>();
            }

            private int TransitionCount { get; set; }
            private List<TransitionGroupChromInfoCalculator> Calculators { get; set; }
            private static int IndexOfTransitionGroupChromInfo(ChromInfoList<TransitionGroupChromInfo> listChromInfo, int fileIndex, int optStep)
            {
                return listChromInfo.IndexOf(info => info.FileIndex == fileIndex && info.OptimizationStep == optStep);
            }
            public void AddChromInfoList(IEnumerable<TransitionChromInfo> listInfo)
            {
                if (listInfo == null)
                    return;

                foreach (var chromInfo in listInfo)
                {
                    if (chromInfo == null)
                        continue;

                    int fileIndex = chromInfo.FileIndex;
                    int step = chromInfo.OptimizationStep;
                    int i = IndexOfFile(fileIndex, step);
                    if (i >= 0)
                        Calculators[i].AddChromInfo(chromInfo);
                    else
                    {
                        TransitionGroupChromInfo chromInfoGroup = null;
                        if (_listChromInfo != null)
                        {
                            int iFile = IndexOfTransitionGroupChromInfo(_listChromInfo, fileIndex, step);
                            if (iFile != -1)
                                chromInfoGroup = _listChromInfo[iFile];
                        }
                        var calc = new TransitionGroupChromInfoCalculator(fileIndex, step, TransitionCount, chromInfoGroup);
                        calc.AddChromInfo(chromInfo);
                        Calculators.Insert(~i, calc);
                    }
                }
            }            

            public IList<TransitionGroupChromInfo> CalcChromInfoList()
            {
                var listInfo = Calculators.ConvertAll(calc => calc.CalcChromInfo());
                return (listInfo.Count > 0 && listInfo[0] != null ? listInfo : null);
            }

            public void SetLibInfo(int fileIndex, int optStep, double[] peakAreas, double[] libIntensities)
            {
                int iFile = IndexOfFile(fileIndex, optStep);
                Debug.Assert(iFile >= 0);   // Should have already been added
                Calculators[iFile].SetLibInfo(peakAreas, libIntensities);
            }

            /// <summary>
            /// Returns the index in the list of calculators of the calculator
            /// representing a particular file and optimization step.  If no calculator
            /// yet exists for the file and optimization step, then (like binary search)
            /// the bitwise complement of the first calculator with a higher index is
            /// returned.
            /// </summary>
            /// <param name="fileIndex">The file index</param>
            /// <param name="optimizationStep">The optimization step</param>
            /// <returns>Index of specified calculator, or bitwise complement of the first
            /// entry with greater index value</returns>
            private int IndexOfFile(int fileIndex, int optimizationStep)
            {
                int i = 0;
                foreach (var calc in Calculators)
                {
                    if (calc.FileIndex == fileIndex)
                    {
                        if (calc.OptimizationStep == optimizationStep)
                            return i;
                        else if (calc.OptimizationStep > optimizationStep)
                            return ~i;
                    }
                    else if (calc.FileIndex > fileIndex)
                        return ~i;
                    i++;
                }
                return ~i;
            }
        }

        private sealed class TransitionGroupChromInfoCalculator
        {
            public TransitionGroupChromInfoCalculator(int fileIndex, int optimizationStep, int transitionCount, TransitionGroupChromInfo chromInfo)
            {
                FileIndex = fileIndex;
                OptimizationStep = optimizationStep;
                TransitionCount = transitionCount;

                // Use existing ratio until it can be recalculated
                if (chromInfo != null)
                {
                    Ratio = chromInfo.Ratio;
                    RatioStdev = chromInfo.RatioStdev;
                    Annotations = chromInfo.Annotations;
                }
                else
                {
                    Annotations = Annotations.Empty;
                }
            }

            public int FileIndex { get; private set; }
            public int OptimizationStep { get; private set; }
            private int TransitionCount { get; set; }
            private int PeakCount { get; set; }
            private int ResultsCount { get; set; }
            private float MaxHeight { get; set; }
            private float? RetentionTime { get; set; }
            private float? StartTime { get; set; }
            private float? EndTime { get; set; }
            private float? Fwhm { get; set; }
            private float? Area { get; set; }
            private float? BackgroundArea { get; set; }
            private float? LibraryDotProduct { get; set; }
            private float? Ratio { get; set; }
            private float? RatioStdev { get; set; }
            private Annotations Annotations { get; set; }
            private bool UserSet { get; set; }

            private float PeakCountRatio { get { return ((float) PeakCount)/TransitionCount; } }

            public void AddChromInfo(TransitionChromInfo info)
            {
                if (info == null)
                    return;

                ResultsCount++;

                Debug.Assert(info.FileIndex == FileIndex,
                    string.Format("Grouping transitions from file {0} with file {1}", info.FileIndex, FileIndex));
                FileIndex = info.FileIndex;

                if (!info.IsEmpty)
                {
                    PeakCount++;

                    Area = (Area ?? 0) + info.Area;
                    BackgroundArea = (BackgroundArea ?? 0) + info.BackgroundArea;

                    if (info.Height > MaxHeight)
                    {
                        MaxHeight = info.Height;
                        RetentionTime = info.RetentionTime;
                    }
                    if (info.StartRetentionTime != 0)
                        StartTime = Math.Min(info.StartRetentionTime, StartTime ?? float.MaxValue);
                    if (info.EndRetentionTime != 0)
                        EndTime = Math.Max(info.EndRetentionTime, EndTime ?? float.MinValue);
                    if (!info.IsFwhmDegenerate)
                        Fwhm = Math.Max(info.Fwhm, Fwhm ?? float.MinValue);                        
                }

                if (info.UserSet)
                    UserSet = true;
            }

            public void SetLibInfo(double[] peakAreas, double[] libIntensities)
            {
                // Only do this once.
                if (LibraryDotProduct.HasValue)
                    return;

                var statPeakAreas = new Statistics(peakAreas);
                var statLibIntensities = new Statistics(libIntensities);
                LibraryDotProduct = (float) statPeakAreas.AngleSqrt(statLibIntensities);
            }

            public TransitionGroupChromInfo CalcChromInfo()
            {
                if (ResultsCount == 0)
                    return null;
                return new TransitionGroupChromInfo(FileIndex,
                                                    OptimizationStep,
                                                    PeakCountRatio,
                                                    RetentionTime,
                                                    StartTime,
                                                    EndTime,
                                                    Fwhm,
                                                    Area,
                                                    BackgroundArea,
                                                    Ratio,
                                                    RatioStdev,
                                                    LibraryDotProduct,
                                                    Annotations,
                                                    UserSet);
            }
        }

        #region Property change methods

        public TransitionGroupDocNode ChangeLibInfo(SpectrumHeaderInfo prop)
        {
            return ChangeProp(ImClone(this), im => im.LibInfo = prop);
        }

        public TransitionGroupDocNode ChangeResults(Results<TransitionGroupChromInfo> prop)
        {
            return ChangeProp(ImClone(this), im => im.Results = prop);
        }

        public DocNode ChangePeak(SrmSettings settings,
                                  ChromatogramGroupInfo chromInfoGroup,
                                  double mzMatchTolerance,
                                  int indexSet,
                                  int indexFile,
                                  OptimizableRegression regression,
                                  Identity tranId,
                                  double retentionTime)
        {
            bool integrateAll = settings.TransitionSettings.Integration.IsIntegrateAll;
            // Find the index of the peak group referenced by this retention time.
            int indexPeakBest = -1;
            // Use the peak closest to the time passed in.
            double minDeltaRT = double.MaxValue;
            foreach (TransitionDocNode nodeTran in Children)
            {
                if (tranId != null && !ReferenceEquals(tranId, nodeTran.Id))
                    continue;
                var chromInfo = chromInfoGroup.GetTransitionInfo((float)nodeTran.Mz, (float)mzMatchTolerance);
                if (chromInfo == null)
                    continue;
                int indexPeak = chromInfo.IndexOfPeak(retentionTime);
                if (indexPeak == -1)
                    continue;
                var peak = chromInfo.GetPeak(indexPeak);
                if (!integrateAll && peak.IsForcedIntegration)
                    continue;
                double deltaRT = Math.Abs(retentionTime - peak.RetentionTime);
                if (deltaRT < minDeltaRT)
                {
                    minDeltaRT = deltaRT;
                    indexPeakBest = indexPeak; 
                }
            }
            if (indexPeakBest == -1)
                throw new ArgumentOutOfRangeException(string.Format("No peak found at {0:F01}", retentionTime));
            // Calculate extents of the peaks being added
            double startMin = double.MaxValue, endMax = double.MinValue;
            foreach (TransitionDocNode nodeTran in Children)
            {
                var chromInfo = chromInfoGroup.GetTransitionInfo((float)nodeTran.Mz, (float)mzMatchTolerance);
                if (chromInfo == null)
                    continue;
                ChromPeak peakNew = chromInfo.GetPeak(indexPeakBest);
                if (peakNew.IsEmpty || (!integrateAll && peakNew.IsForcedIntegration))
                    continue;
                startMin = Math.Min(startMin, peakNew.StartTime);
                endMax = Math.Max(endMax, peakNew.EndTime);
            }
            // Overlap threshold is 50% of the max peak extents
            double overlapThreshold = (endMax - startMin)/2;
            // Update all transitions with the new information
            var listChildrenNew = new List<DocNode>();
            foreach (TransitionDocNode nodeTran in Children)
            {
                var chromInfoArray = chromInfoGroup.GetAllTransitionInfo(
                    (float)nodeTran.Mz, (float)mzMatchTolerance, regression);
                // Shouldn't need to update a transition with no chrom info
                if (chromInfoArray.Length == 0)
                    listChildrenNew.Add(nodeTran.RemovePeak(indexSet, indexFile));
                else
                {
                    // CONSIDER: Do this more efficiently?  Only when there is opimization
                    //           data will the loop execute more than once.
                    int numSteps = chromInfoArray.Length/2;
                    var nodeTranNew = nodeTran;
                    for (int i = 0; i < chromInfoArray.Length; i++)
                    {
                        var chromInfo = chromInfoArray[i];
                        int step = i - numSteps;

                        ChromPeak peakNew = chromInfo.GetPeak(indexPeakBest);
                        // If the peak is empty, but the old peak has sufficient overlap with
                        // the peaks being added, then keep it.
                        if (peakNew.IsEmpty || peakNew.IsForcedIntegration)
                        {
                            var tranInfoList = nodeTran.Results[indexSet];
                            int iTran = tranInfoList.IndexOf(info =>
                                info.FileIndex == indexFile && info.OptimizationStep == step);
                            var tranInfoOld = tranInfoList[iTran];
                            if (Math.Min(tranInfoOld.EndRetentionTime, endMax) -
                                Math.Max(tranInfoOld.StartRetentionTime, startMin) > overlapThreshold)
                            {
                                continue;
                            }
                        }
                        nodeTranNew = (TransitionDocNode) nodeTranNew.ChangePeak(
                            indexSet, indexFile, step, peakNew);
                    }
                    listChildrenNew.Add(nodeTranNew);
                }
            }
            return ChangeChildrenChecked(listChildrenNew);
        }

        public DocNode ChangePeak(SrmSettings settings,
                                  ChromatogramGroupInfo chromInfoGroup,
                                  double mzMatchTolerance,
                                  int indexSet,
                                  int indexFile,
                                  OptimizableRegression regression,
                                  Transition transition,
                                  double startTime,
                                  double endTime)
        {
            // Recalculate peaks based on new boundaries
            var listChildrenNew = new List<DocNode>();
            int startIndex = chromInfoGroup.IndexOfNearestTime((float)startTime);
            int endIndex = chromInfoGroup.IndexOfNearestTime((float)endTime);
            ChromPeak.FlagValues flags = 0;
            if (settings.MeasuredResults.IsTimeNormalArea)
                flags = ChromPeak.FlagValues.time_normalized;
            foreach (TransitionDocNode nodeTran in Children)
            {
                if (transition != null && !ReferenceEquals(transition, nodeTran.Transition))
                    listChildrenNew.Add(nodeTran);
                else
                {
                    var chromInfoArray = chromInfoGroup.GetAllTransitionInfo(
                        (float)nodeTran.Mz, (float)mzMatchTolerance, regression);

                    // Shouldn't need to update a transition with no chrom info
                    if (chromInfoArray.Length == 0)
                        listChildrenNew.Add(nodeTran.RemovePeak(indexSet, indexFile));
                    else
                    {
                        // CONSIDER: Do this more efficiently?  Only when there is opimization
                        //           data will the loop execute more than once.
                        int numSteps = chromInfoArray.Length/2;
                        var nodeTranNew = nodeTran;
                        for (int i = 0; i < chromInfoArray.Length; i++)
                        {
                            var chromInfo = chromInfoArray[i];
                            int step = i - numSteps;
                            nodeTranNew = (TransitionDocNode) nodeTranNew.ChangePeak(
                                indexSet, indexFile, step, chromInfo.CalcPeak(startIndex, endIndex, flags));
                        }
                        listChildrenNew.Add(nodeTranNew);
                    }
                }
            }
            return ChangeChildrenChecked(listChildrenNew);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionGroupDocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                obj.PrecursorMz == PrecursorMz &&
                Equals(obj.LibInfo, LibInfo) &&
                Equals(obj.Results, Results);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as TransitionGroupDocNode);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ PrecursorMz.GetHashCode();
                result = (result*397) ^ (LibInfo != null ? LibInfo.GetHashCode() : 0);
                result = (result*397) ^ (Results != null ? Results.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public enum IsotopeLabelType { light, heavy }

    public class TransitionGroup : Identity
    {
        public const int MIN_PRECURSOR_CHARGE = 1;
        public const int MAX_PRECURSOR_CHARGE = 5;

        private readonly Peptide _peptide;

        public TransitionGroup(Peptide peptide, int precursorCharge, IsotopeLabelType labelType)
        {
            _peptide = peptide;

            PrecursorCharge = precursorCharge;
            LabelType = labelType;

            Validate();
        }

        public Peptide Peptide { get { return _peptide; } }

        public int PrecursorCharge { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }

        public string LabelTypeText
        {
            get { return (LabelType == IsotopeLabelType.heavy ? " (heavy)" : ""); }
        }

        public static int CompareTransitions(TransitionDocNode node1, TransitionDocNode node2)
        {
            Transition tran1 = node1.Transition, tran2 = node2.Transition;
            // TODO: To generate the same ordering as GetTransitions, some attention
            //       would have to be paid to the ordering in the SrmSettings.TransitionSettings
            //       At least this groups the types, and orders by ion ordinal...
            int diffType = ((int) tran1.IonType) - ((int) tran2.IonType);
            if (diffType != 0)
                return diffType;
            int diffCharge = tran1.Charge - tran2.Charge;
            if (diffCharge != 0)
                return diffCharge;
            return tran1.CleavageOffset - tran2.CleavageOffset;
        }

        public IEnumerable<TransitionDocNode> GetTransitions(SrmSettings settings, ExplicitMods mods, double precursorMz,
            SpectrumHeaderInfo libInfo, IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks,
            bool useFilter)
        {
            // Get necessary mass calculators and masses
            var calcFilterPre = settings.GetPrecursorCalc(IsotopeLabelType.light, mods);
            var calcFilter = settings.GetFragmentCalc(IsotopeLabelType.light, mods);
            var calcPredict = settings.GetFragmentCalc(LabelType, mods);

            string sequence = Peptide.Sequence;
            double precursorMassPredict = calcPredict.GetPrecursorFragmentMass(sequence);
            if (!useFilter)
                yield return CreateTransitionNode(precursorMassPredict, transitionRanks);

            double[,] massesPredict = calcPredict.GetFragmentIonMasses(sequence);
            int len = massesPredict.GetLength(1);
            if (len == 0)
                yield break;

            double[,] massesFilter = massesPredict;
            if (!ReferenceEquals(calcFilter, calcPredict))
            {
                // Get the normal m/z values for filtering, so that light and heavy
                // ion picks will match.
                precursorMz = SequenceMassCalc.GetMZ(calcFilterPre.GetPrecursorMass(sequence), PrecursorCharge);
                massesFilter = calcFilter.GetFragmentIonMasses(sequence);
            }

            var tranSettings = settings.TransitionSettings;
            var filter = tranSettings.Filter;

            // Get filter settings
            var charges = filter.ProductCharges;
            var types = filter.IonTypes;
            var startFinder = filter.FragmentRangeFirst;
            var endFinder = filter.FragmentRangeLast;
            bool pro = filter.IncludeNProline;
            bool gluasp = filter.IncludeCGluAsp;

            // Get library settings
            var pick = tranSettings.Libraries.Pick;
            if (!useFilter)
            {
                pick = TransitionLibraryPick.all;
                charges = Transition.ALL_CHARGES;
                types = Transition.ALL_TYPES;
            }
            // If there are no libraries or no library information, then
            // picking cannot use library information
            else if (!settings.PeptideSettings.Libraries.HasLibraries || libInfo == null)
                pick = TransitionLibraryPick.none;
            // If picking relies on library information
            else if (pick != TransitionLibraryPick.none)
            {
                // If it is not yet loaded, or nothing got ranked, return an empty enumeration
                if (!settings.PeptideSettings.Libraries.IsLoaded || transitionRanks.Count == 0)
                    yield break;
            }

            // Get instrument settings
            int minMz = tranSettings.Instrument.MinMz;
            int maxMz = tranSettings.Instrument.MaxMz;

            // Loop over potential product ions picking transitions
            foreach (IonType type in types)
            {
                foreach (int charge in charges)
                {
                    // Product ion charge can never be lower than precursor.
                    if (PrecursorCharge < charge)
                        continue;

                    int start = 0, end = 0;
                    if (pick != TransitionLibraryPick.all)
                    {
                        start = startFinder.FindStartFragment(massesFilter, type, charge, precursorMz);
                        end = endFinder.FindEndFragment(type, start, len);
                        if (Transition.IsCTerminal(type))
                            Helpers.Swap(ref start, ref end);
                    }

                    for (int i = 0; i < len; i++)
                    {
                        // Get the predicted m/z that would be used in the transition
                        double massH = massesPredict[(int) type, i];
                        double ionMz = SequenceMassCalc.GetMZ(massH, charge);
                        // Make sure the fragment m/z value falls within the valid instrument range.
                        // CONSIDER: This means that a heavy transition might excede the instrument
                        //           range where a light one is accepted, leading to a disparity
                        //           between heavy and light transtions picked.
                        if (minMz > ionMz || ionMz > maxMz)
                            continue;

                        if (pick == TransitionLibraryPick.all)
                        {
                            if (!useFilter)
                                yield return CreateTransitionNode(type, i, charge, massH, transitionRanks);
                            else
                            {
                                LibraryRankedSpectrumInfo.RankedMI rmi;
                                if (transitionRanks.TryGetValue(ionMz, out rmi) && rmi.IonType == type && rmi.Charge == charge)
                                    yield return CreateTransitionNode(type, i, charge, massH, transitionRanks);
                            }
                        }
                        else if ((start <= i && i <= end) ||
                            (pro && IsPro(sequence, i)) ||
                            (gluasp && IsGluAsp(sequence, i)))
                        {
                            if (pick == TransitionLibraryPick.none)
                                yield return CreateTransitionNode(type, i, charge, massH, transitionRanks);
                            else if (transitionRanks.ContainsKey(ionMz))
                                yield return CreateTransitionNode(type, i, charge, massH, transitionRanks);
                        }
                    }
                }
            }
        }

        private TransitionDocNode CreateTransitionNode(double precursorMassH, IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks)
        {
            Transition transition = new Transition(this);
            var info = TransitionDocNode.GetLibInfo(transition, precursorMassH, transitionRanks);
            return new TransitionDocNode(transition, precursorMassH, info);
        }

        private TransitionDocNode CreateTransitionNode(IonType type, int cleavageOffset, int charge, double massH, IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks)
        {
            Transition transition = new Transition(this, type, cleavageOffset, charge);
            var info = TransitionDocNode.GetLibInfo(transition, massH, transitionRanks);
            return new TransitionDocNode(transition, massH, info);
        }

        public void GetLibraryInfo(SrmSettings settings, ExplicitMods mods, bool useFilter,
            ref SpectrumHeaderInfo libInfo,
            Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks)
        {
            PeptideLibraries libraries = settings.PeptideSettings.Libraries;
            // No libraries means no library info
            if (!libraries.HasLibraries)
            {
                libInfo = null;
                return;
            }
            // If not loaded, leave everything alone, and let the update
            // when loading is complete fix things.
            else if (!libraries.IsLoaded)
                return;

            IsotopeLabelType typeInfo;
            if (!settings.TryGetLibInfo(Peptide.Sequence, PrecursorCharge, mods, out typeInfo, out libInfo))
                libInfo = null;                
            else if (transitionRanks != null)
            {
                try
                {
                    SpectrumPeaksInfo spectrumInfo;
                    string sequenceMod = settings.GetModifiedSequence(Peptide.Sequence, typeInfo, mods);
                    if (libraries.TryLoadSpectrum(new LibKey(sequenceMod, PrecursorCharge), out spectrumInfo))
                    {
                        var spectrumInfoR = new LibraryRankedSpectrumInfo(spectrumInfo, typeInfo,
                            this, settings, mods, useFilter, 50);
                        foreach (var rmi in spectrumInfoR.PeaksRanked)
                        {
                            Debug.Assert(!transitionRanks.ContainsKey(rmi.PredictedMz));
                            transitionRanks.Add(rmi.PredictedMz, rmi);
                        }
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        public static bool IsGluAsp(string sequence, int cleavageOffset)
        {
            char c = Transition.GetFragmentCTermAA(sequence, cleavageOffset);
            return (c == 'G' || c == 'A');
        }

        public static bool IsPro(string sequence, int cleavageOffset)
        {
            return (Transition.GetFragmentNTermAA(sequence, cleavageOffset) == 'P');
        }

        private void Validate()
        {
            if (MIN_PRECURSOR_CHARGE > PrecursorCharge || PrecursorCharge > MAX_PRECURSOR_CHARGE)
            {
                throw new InvalidDataException(string.Format("Precursor charge {0} must be between {1} and {2}.",
                    PrecursorCharge, MIN_PRECURSOR_CHARGE, MAX_PRECURSOR_CHARGE));
            }
        }

        #region object overrides

        public bool Equals(TransitionGroup obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj._peptide, _peptide) &&
                obj.PrecursorCharge == PrecursorCharge &&
                obj.LabelType.Equals(LabelType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionGroup)) return false;
            return Equals((TransitionGroup) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _peptide.GetHashCode();
                result = (result*397) ^ PrecursorCharge;
                result = (result*397) ^ LabelType.GetHashCode();
                return result;
            }
        }

        public override string ToString()
        {
            if (LabelType == IsotopeLabelType.heavy)
                return string.Format("Charge {0} (heavy)", PrecursorCharge);
            else
                return string.Format("Charge {0}", PrecursorCharge);
        }

        #endregion
    }
}
