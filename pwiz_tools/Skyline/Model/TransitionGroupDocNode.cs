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
            : this(id, Annotations.EMPTY, massH, relativeRT, null, null, children, true)
        {
        }

        public TransitionGroupDocNode(TransitionGroup id,
                                      double massH,
                                      RelativeRT relativeRT,
                                      TransitionDocNode[] children,
                                      bool autoManageChildren)
            : this(id, Annotations.EMPTY, massH, relativeRT, null, null, children, autoManageChildren)
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

        public bool IsLight { get { return TransitionGroup.LabelType.IsLight; } }

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
            if (i == -1)
                return AveragePeakCountRatio;

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

        public float? GetLibraryDotProduct(int i)
        {
            if (i == -1)
                return AverageLibraryDotProduct;

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

        public float? GetPeakAreaRatio(int i, out float? stdev)
        {
            return GetPeakAreaRatio(i, 0, out stdev);
        }

        public float? GetPeakAreaRatio(int i, int indexIS, out float? stdev)
        {
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
            {
                stdev = null;
                return null;
            }

            stdev = chromInfo.RatioStdevs[indexIS];
            return chromInfo.Ratios[indexIS];
        }

        public float? GetSchedulingPeakTime(SrmDocument document)
        {
            if (!HasResults)
                return null;

            // Try to get a scheduling time from non-optimization data, unless this
            // document contains only optimization data.  This is because optimization
            // data may have been taken under completely different chromatographic
            // condictions.
            int valCount = 0;
            double valTotal = 0;
            int valCountOpt = 0;
            double valTotalOpt = 0;

            for (int i = 0; i < Results.Count; i++)
            {
                var result = Results[i];
                if (result == null)
                    continue;
                var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[i];
                foreach (var chromInfo in result)
                {
                    if (chromInfo == null ||
//                            chromInfo.PeakCountRatio < 0.5 || - caused problems
                        !chromInfo.StartRetentionTime.HasValue ||
                        !chromInfo.EndRetentionTime.HasValue)
                        continue;
                    double centerTime = (chromInfo.StartRetentionTime.Value + chromInfo.EndRetentionTime.Value)/2;
                    if (chromatogramSet.OptimizationFunction == null)
                    {
                        valTotal += centerTime;
                        valCount++;                        
                    }
                    else
                    {
                        valTotalOpt += centerTime;
                        valCountOpt++;
                    }
                }
            }

            // If possible return the scheduling time based on non-optimization data.
            if (valCount != 0)
                return (float)(valTotal / valCount);
                // If only optimization was found, then use it.
            else if (valTotalOpt != 0)
                return (float)(valTotalOpt / valCountOpt);
            // No usable data at all.
            return null;
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

                // TODO: Use TransitionLossKey
                Dictionary<TransitionLossKey, DocNode> mapIdToChild = CreateTransitionLossToChildMap();
                foreach (TransitionDocNode nodeTran in TransitionGroup.GetTransitions(settingsNew, mods, precursorMz, libInfo, transitionRanks, true))
                {
                    TransitionDocNode nodeTranResult;

                    DocNode existing;
                    // Add values that existed before the change.
                    if (mapIdToChild.TryGetValue(nodeTran.Key, out existing))
                    {
                        nodeTranResult = (TransitionDocNode) existing;
                        if (diff.DiffTransitionProps)
                        {
                            var tran = nodeTranResult.Transition;
                            var losses = nodeTranResult.Losses;
                            double massH = settingsNew.GetFragmentMass(TransitionGroup.LabelType, mods, tran);
                            var info = TransitionDocNode.GetLibInfo(tran, Transition.CalcMass(massH, losses), transitionRanks);
                            nodeTranResult = new TransitionDocNode(tran, losses, massH, info);

                            Helpers.AssignIfEquals(ref nodeTranResult, (TransitionDocNode) existing);
                        }
                    }
                    // Add the new node
                    else
                    {
                        nodeTranResult = nodeTran;
                    }

                    if (nodeTranResult != null)
                        childrenNew.Add(nodeTranResult);
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
            else
            {
                if (diff.DiffTransitions && diff.SettingsOld != null)
                {
                    // If neutral loss modifications changed, remove all transitions with neutral
                    // loss modifications which are no longer possible.
                    var modsNew = settingsNew.PeptideSettings.Modifications;
                    var modsLossNew = modsNew.NeutralLossModifications.ToArray();
                    var modsOld = diff.SettingsOld.PeptideSettings.Modifications;
                    var modsLossOld = modsOld.NeutralLossModifications.ToArray();
                    if (modsNew.MaxNeutralLosses < modsOld.MaxNeutralLosses ||
                        !ArrayUtil.EqualsDeep(modsLossNew, modsLossOld))
                    {
                        IList<DocNode> childrenNew = new List<DocNode>();
                        foreach (TransitionDocNode nodeTransition in nodeResult.Children)
                        {
                            if (nodeTransition.IsLossPossible(modsNew.MaxNeutralLosses, modsLossNew))
                                childrenNew.Add(nodeTransition);
                        }

                        nodeResult = (TransitionGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
                    }
                }

                if (diff.DiffTransitionProps)
                {
                    IList<DocNode> childrenNew = new List<DocNode>();

                    // Enumerate the nodes making necessary changes.
                    foreach (TransitionDocNode nodeTransition in nodeResult.Children)
                    {
                        var tran = nodeTransition.Transition;
                        var losses = nodeTransition.Losses;
                        MassType massType = settingsNew.TransitionSettings.Prediction.FragmentMassType;
                        if (losses != null && massType != losses.MassType)
                            losses = losses.ChangeMassType(massType);
                        var annotations = nodeTransition.Annotations;   // Don't lose annotations
                        var results = nodeTransition.Results;           // Results changes happen later
                        double massH = settingsNew.GetFragmentMass(TransitionGroup.LabelType, mods, tran);
                        var info = TransitionDocNode.GetLibInfo(tran, Transition.CalcMass(massH, losses), transitionRanks);
                        var nodeNew = new TransitionDocNode(tran, annotations, losses, massH, info, results);

                        Helpers.AssignIfEquals(ref nodeNew, nodeTransition);
                        childrenNew.Add(nodeNew);
                    }

                    // Change as little as possible
                    if (!ArrayUtil.ReferencesEqual(childrenNew, Children))
                        nodeResult = new TransitionGroupDocNode(nodeResult, precursorMz, relativeRT, childrenNew);
                    else if (precursorMz != PrecursorMz || relativeRT != RelativeRT)
                        nodeResult = new TransitionGroupDocNode(nodeResult, precursorMz, relativeRT, Children);
                }
                else if (diff.DiffTransitionGroupProps)
                {
                    nodeResult = new TransitionGroupDocNode(nodeResult, precursorMz, relativeRT, Children);
                }
            }

            // One final check for a library info change
            if (!Equals(libInfo, nodeResult.LibInfo))
                nodeResult = nodeResult.ChangeLibInfo(libInfo);

            // A change in the precursor m/z may impact which results match this node
            if (diff.DiffResults || ChangedResults(nodeResult) || precursorMz != PrecursorMz)
                nodeResult = nodeResult.UpdateResults(settingsNew, diff, this);

            return nodeResult;
        }

        public DocNode EnsureChildren(PeptideDocNode parent, ExplicitMods mods, SrmSettings settings)
        {
            var result = this;
            // Check if children will change as a result of ChangeSettings.
            var changed = result.ChangeSettings(settings, mods, SrmSettingsDiff.ALL);
            if (result.AutoManageChildren && !AreEquivalentChildren(result.Children, changed.Children))
            {
                changed = result = (TransitionGroupDocNode)result.ChangeAutoManageChildren(false);
                changed = changed.ChangeSettings(settings, mods, SrmSettingsDiff.ALL);
            }
            // Make sure node points to correct parent.
            if (!ReferenceEquals(parent.Peptide, TransitionGroup.Peptide))
            {
                result = new TransitionGroupDocNode(new TransitionGroup(parent.Peptide, TransitionGroup.PrecursorCharge, TransitionGroup.LabelType),
                                                    Annotations, 0.0, RelativeRT, LibInfo, Results, new TransitionDocNode[0], result.AutoManageChildren);
                result = new TransitionGroupDocNode(result, PrecursorMz, RelativeRT, new List<DocNode>());
            }
            // Match children resulting from ChangeSettings to current children
            var dictIndexToChild = Children.ToDictionary(child => child.Id.GlobalIndex);
            var listChildren = new List<DocNode>();
            foreach (TransitionDocNode nodePep in changed.Children)
            {
                DocNode child;
                if (dictIndexToChild.TryGetValue(nodePep.Id.GlobalIndex, out child))
                {
                    listChildren.Add(((TransitionDocNode)child).EnsureChildren(result, settings));
                }
            }
            return result.ChangeChildrenChecked(listChildren);
        }

        private static bool AreEquivalentChildren(IList<DocNode> children1, IList<DocNode> children2)
        {
            if (children1.Count != children2.Count)
                return false;
            for (int i = 0; i < children1.Count; i++)
            {
                if (!Equals(((TransitionDocNode)children1[i]).Key, ((TransitionDocNode)children2[i]).Key))
                    return false;
            }
            return true;
        }

        private Dictionary<TransitionLossKey, DocNode> CreateTransitionLossToChildMap()
        {
            return Children.ToDictionary(child => ((TransitionDocNode) child).Key);
        }

        private TransitionGroupDocNode UpdateResults(SrmSettings settingsNew, SrmSettingsDiff diff,
                                                     TransitionGroupDocNode nodePrevious)
        {
            // Make sure no results are present, if the new settings has no results
            if (!settingsNew.HasResults)
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
            else if (Children.Count == 0)
            {
                // If no children, just use a null populated list of the right size.
                int countResults = settingsNew.MeasuredResults.Chromatograms.Count;
                var resultsNew = new ChromInfoList<TransitionGroupChromInfo>[countResults];
                return ChangeResults(new Results<TransitionGroupChromInfo>(resultsNew));
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
                                            IList<float?> ratios = (chromInfo != null ?
                                                                                          chromInfo.Ratios
                                                                        :
                                                                            new float?[settingsNew.PeptideSettings.Modifications.InternalStandardTypes.Count]);

                                            chromInfo = new TransitionChromInfo(fileIndex, step, peak, ratios, false);
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
                        var calc = new TransitionGroupChromInfoCalculator(fileIndex, step,
                                                                          TransitionCount, chromInfo.Ratios.Count, chromInfoGroup);
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
            public TransitionGroupChromInfoCalculator(int fileIndex, int optimizationStep, int transitionCount, int ratioCount, TransitionGroupChromInfo chromInfo)
            {
                FileIndex = fileIndex;
                OptimizationStep = optimizationStep;
                TransitionCount = transitionCount;

                // Use existing ratio until it can be recalculated
                if (chromInfo != null)
                {
                    Ratios = chromInfo.Ratios;
                    RatioStdevs = chromInfo.RatioStdevs;
                    Annotations = chromInfo.Annotations;
                }
                else
                {
                    Ratios = new float?[ratioCount];
                    RatioStdevs = new float?[ratioCount];
                    Annotations = Annotations.EMPTY;
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
            private IList<float?> Ratios { get; set; }
            private IList<float?> RatioStdevs { get; set; }
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
                                                    Ratios,
                                                    RatioStdevs,
                                                    LibraryDotProduct,
                                                    Annotations,
                                                    UserSet);
            }
        }

        /// <summary>
        /// True if children of a given node are equivalent to the children
        /// of this node.
        /// </summary>
        public bool EquivalentChildren(TransitionGroupDocNode nodeGroup)
        {
            if (Children.Count != nodeGroup.Children.Count)
                return false;
            for (int i = 0; i < Children.Count; i++)
            {
                TransitionDocNode nodeTran1 = (TransitionDocNode) Children[i];
                TransitionDocNode nodeTran2 = (TransitionDocNode) nodeGroup.Children[i];
                if (!nodeTran1.Transition.Equivalent(nodeTran2.Transition))
                    return false;
                // Losses must also be equal
                if (!Equals(nodeTran1.Losses, nodeTran2.Losses))
                    return false;
            }
            return true;
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
            int ratioCount = settings.PeptideSettings.Modifications.InternalStandardTypes.Count;
            
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
                            if (iTran != -1)
                            {
                                var tranInfoOld = tranInfoList[iTran];
                                if (Math.Min(tranInfoOld.EndRetentionTime, endMax) -
                                    Math.Max(tranInfoOld.StartRetentionTime, startMin) > overlapThreshold)
                                {
                                    continue;
                                }
                            }
                        }
                        nodeTranNew = (TransitionDocNode) nodeTranNew.ChangePeak(
                                                              indexSet, indexFile, step, peakNew, ratioCount);
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
            int ratioCount = settings.PeptideSettings.Modifications.InternalStandardTypes.Count;

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
                            nodeTranNew = (TransitionDocNode) nodeTranNew.ChangePeak(indexSet, indexFile, step,
                                                                                     chromInfo.CalcPeak(startIndex, endIndex, flags), ratioCount);
                        }
                        listChildrenNew.Add(nodeTranNew);
                    }
                }
            }
            return ChangeChildrenChecked(listChildrenNew);
        }

        protected override DocNodeParent SynchChildren(SrmSettings settings, DocNodeParent parent, DocNodeParent sibling)
        {
            var nodePep = (PeptideDocNode)parent;
            var nodeGroupSynch = (TransitionGroupDocNode)sibling;

            // Only synchronize groups with the same charge.
            if (TransitionGroup.PrecursorCharge != nodeGroupSynch.TransitionGroup.PrecursorCharge)
                return this;

            // Start with the current node as the default
            var nodeResult = this;

            // Use same auto-manage setting
            if (AutoManageChildren != nodeGroupSynch.AutoManageChildren)
            {
                nodeResult = (TransitionGroupDocNode) nodeResult.ChangeAutoManageChildren(
                                                          nodeGroupSynch.AutoManageChildren);
            }

            // If not automanaged, then set the explicit transitions
            if (!nodeResult.AutoManageChildren)
            {
                var childrenNew = new List<DocNode>();
                foreach (TransitionDocNode nodeTran in nodeGroupSynch.Children)
                {
                    var tranMatch = nodeTran.Transition;
                    var tran = new Transition(TransitionGroup, tranMatch.IonType, tranMatch.CleavageOffset, tranMatch.Charge);
                    var losses = nodeTran.Losses;
                    // m/z and library info calculated later
                    childrenNew.Add(new TransitionDocNode(tran, losses, 0, null));
                }
                nodeResult = (TransitionGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
            }

            // Change settings to creat auto-manage children, or calculate
            // mz values, library ranks and result matching
            return nodeResult.ChangeSettings(settings, nodePep.ExplicitMods, SrmSettingsDiff.ALL);
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
}