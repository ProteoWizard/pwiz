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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class PeptideDocNode : DocNodeParent
    {
        public PeptideDocNode(Peptide id, TransitionGroupDocNode[] children)
            : this(id, null, children, true)
        {
        }

        public PeptideDocNode(Peptide id, ExplicitMods mods)
            : this(id, mods, null, Annotations.EMPTY, null, new TransitionGroupDocNode[0], true)
        {
        }

        public PeptideDocNode(Peptide id, ExplicitMods mods, TransitionGroupDocNode[] children, bool autoManageChildren)
            : this(id, mods, null, Annotations.EMPTY, null, children, autoManageChildren)
        {
        }

        public PeptideDocNode(Peptide id, ExplicitMods mods, int? rank, Annotations annotations,
            Results<PeptideChromInfo> results, TransitionGroupDocNode[] children, bool autoManageChildren)
            : base(id, annotations, children, autoManageChildren)
        {
            ExplicitMods = mods;
            Rank = rank;
            Results = results;
            BestResult = CalcBestResult();
        }

        public Peptide Peptide { get { return (Peptide)Id; } }

        public PeptideModKey Key { get { return new PeptideModKey(Peptide, ExplicitMods); } }

        public PeptideSequenceModKey SequenceKey { get { return new PeptideSequenceModKey(Peptide.Sequence, ExplicitMods); } }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.peptide; } }

        public ExplicitMods ExplicitMods { get; private set; }

        public bool HasExplicitMods { get { return ExplicitMods != null; } }

        public bool HasVariableMods { get { return HasExplicitMods && ExplicitMods.IsVariableStaticMods; } }

        public bool AreVariableModsPossible(int maxVariableMods, IList<StaticMod> modsVarAvailable)
        {
            if (HasVariableMods)
            {
                var explicitModsVar = ExplicitMods.StaticModifications;
                if (explicitModsVar.Count > maxVariableMods)
                    return false;
                foreach (var explicitMod in explicitModsVar)
                {
                    if (!modsVarAvailable.Contains(explicitMod.Modification))
                        return false;
                }
            }
            return true;
        }

        public bool CanHaveImplicitStaticMods
        {
            get
            {
                return !HasExplicitMods ||
                       !ExplicitMods.IsModified(IsotopeLabelType.light) ||
                       ExplicitMods.IsVariableStaticMods;
            }
        }

        public bool CanHaveImplicitHeavyMods(IsotopeLabelType labelType)
        {
            return !HasExplicitMods || !ExplicitMods.IsModified(labelType);
        }

        public bool HasChildType(IsotopeLabelType labelType)
        {
            return Children.Contains(nodeGroup => ReferenceEquals(labelType,
                                                                  ((TransitionGroupDocNode)nodeGroup).TransitionGroup.LabelType));
        }

        public bool HasChildCharge(int charge)
        {
            return Children.Contains(nodeGroup => Equals(charge,
                                                         ((TransitionGroupDocNode) nodeGroup).TransitionGroup.PrecursorCharge));
        }

        public int? Rank { get; private set; }
        public bool IsDecoy { get { return Peptide.IsDecoy; } }

        public Results<PeptideChromInfo> Results { get; private set; }

        public bool HasResults { get { return Results != null; } }

        public ChromInfoList<PeptideChromInfo> GetSafeChromInfo(int i)
        {
            return (HasResults && Results.Count > i ? Results[i] : null);
        }

        public float GetRankValue(PeptideRankId rankId)
        {
            float value = float.MinValue;
            foreach (TransitionGroupDocNode nodeGroup in Children)
                value = Math.Max(value, nodeGroup.GetRankValue(rankId));
            return value;
        }

        public float? GetPeakCountRatio(int i)
        {
            if (i == -1)
                return AveragePeakCountRatio;

            var result = GetSafeChromInfo(i);
            if (result == null)
                return null;
            return result.GetAverageValue(chromInfo => chromInfo.PeakCountRatio);
        }

        public float? AveragePeakCountRatio
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.PeakCountRatio);
            }
        }

        public float? GetSchedulingTime(int i)
        {
            return GetMeasuredRetentionTime(i);
//            return GetPeakCenterTime(i);
        }

        public float? SchedulingTime
        {
            get { return AverageMeasuredRetentionTime; }
//            get { return AveragePeakCenterTime; }
        }

        public float? GetSchedulingTime(ChromFileInfoId fileId)
        {
            return GetMeasuredRetentionTime(fileId);
//            return GetPeakCenterTime(fileId);
        }

        public float? GetMeasuredRetentionTime(int i)
        {
            if (i == -1)
                return AverageMeasuredRetentionTime;

            var result = GetSafeChromInfo(i);
            if (result == null)
                return null;
            return result.GetAverageValue(chromInfo => chromInfo.RetentionTime.HasValue
                                             ? chromInfo.RetentionTime.Value
                                             : (float?)null);
        }

        public float? AverageMeasuredRetentionTime
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.RetentionTime.HasValue
                                             ? chromInfo.RetentionTime.Value
                                             : (float?)null);
            }
        }

        public float? GetMeasuredRetentionTime(ChromFileInfoId fileId)
        {
            double totalTime = 0;
            int countTime = 0;
            foreach (var chromInfo in TransitionGroups.SelectMany(nodeGroup => nodeGroup.ChromInfos))
            {
                if (fileId != null && !ReferenceEquals(fileId, chromInfo.FileId))
                    continue;
                float? retentionTime = chromInfo.RetentionTime;
                if (!retentionTime.HasValue)
                    continue;

                totalTime += retentionTime.Value;
                countTime++;
            }
            if (countTime == 0)
                return null;
            return (float)(totalTime / countTime);
        }

        public float? GetPeakCenterTime(int i)
        {
            if (i == -1)
                return AveragePeakCenterTime;

            double totalTime = 0;
            int countTime = 0;
            foreach (var nodeGroup in TransitionGroups)
            {
                if (!nodeGroup.HasResults)
                    continue;
                var result = nodeGroup.Results[i];
                if (result == null)
                    continue;

                foreach (var chromInfo in result)
                {
                    float? centerTime = GetPeakCenterTime(chromInfo);
                    if (!centerTime.HasValue)
                        continue;

                    totalTime += centerTime.Value;
                    countTime++;
                }
            }
            if (countTime == 0)
                return null;
            return (float)(totalTime / countTime);
        }

        public float? AveragePeakCenterTime
        {
            get { return GetPeakCenterTime((ChromFileInfoId) null); }
        }

        public float? GetPeakCenterTime(ChromFileInfoId fileId)
        {
            double totalTime = 0;
            int countTime = 0;
            foreach (var chromInfo in TransitionGroups.SelectMany(nodeGroup => nodeGroup.ChromInfos))
            {
                if (fileId != null && !ReferenceEquals(fileId, chromInfo.FileId))
                    continue;
                float? centerTime = GetPeakCenterTime(chromInfo);
                if (!centerTime.HasValue)
                    continue;

                totalTime += centerTime.Value;
                countTime++;
            }
            if (countTime == 0)
                return null;
            return (float)(totalTime / countTime);
        }

        private float? GetPeakCenterTime(TransitionGroupChromInfo chromInfo)
        {
            if (!chromInfo.StartRetentionTime.HasValue || !chromInfo.EndRetentionTime.HasValue)
                return null;

            return (chromInfo.StartRetentionTime.Value + chromInfo.EndRetentionTime.Value) / 2;
        }

        private float? GetAverageResultValue(Func<PeptideChromInfo, float?> getVal)
        {
            return HasResults ? Results.GetAverageValue(getVal) : null;
        }

        public int BestResult { get; private set; }

        /// <summary>
        /// Returns the index of the "best" result for a peptide.  This is currently
        /// base solely on total peak area, could be enhanced in the future to be
        /// more like picking the best peak in the import code, including factors
        /// such as peak-found-ratio and dot-product.
        /// </summary>
        private int CalcBestResult()
        {
            if (!HasResults)
                return -1;

            int iBest = -1;
            double bestArea = double.MinValue;
            for (int i = 0; i < Results.Count; i++)
            {
                double productArea = 0;
                foreach (TransitionGroupDocNode nodeGroup in Children)
                {
                    double groupArea = 0;
                    double groupTranMeasured = 0;
                    foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                    {
                        if (!nodeTran.HasResults)
                            continue;
                        var result = nodeTran.Results[i];
                        if (result == null)
                            continue;
                        // Use average area over all files in a replicate to avoid
                        // counting a replicate as best, simply because it has more
                        // measurements.  Most of the time there should only be one
                        // file per precursor per replicate.
                        double tranArea = 0;
                        double tranMeasured = 0;
                        foreach (var chromInfo in result)
                        {
                            if (chromInfo != null && chromInfo.Area > 0)
                            {
                                tranArea += chromInfo.Area;
                                tranMeasured++;                                
                            }
                        }
                        groupArea += tranArea/result.Count;
                        groupTranMeasured += tranMeasured/result.Count;
                    }
                    productArea += ChromDataPeakList.ScorePeak(groupArea, groupTranMeasured,
                        nodeGroup.Children.Count);
                }
                if (productArea > bestArea)
                {
                    iBest = i;
                    bestArea = productArea;
                }
            }
            return iBest;            
        }

        #region Property change methods

        public PeptideDocNode ChangeExplicitMods(ExplicitMods prop)
        {
            return ChangeProp(ImClone(this), im => im.ExplicitMods = prop);
        }     

        public PeptideDocNode ChangeRank(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.Rank = prop);
        }

        public PeptideDocNode ChangeResults(Results<PeptideChromInfo> prop)
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.Results = prop;
                                                     im.BestResult = im.CalcBestResult();
                                                 });
        }

        #endregion

        /// <summary>
        /// Node level depths below this node
        /// </summary>
// ReSharper disable InconsistentNaming
        public enum Level { TransitionGroups, Transitions }
// ReSharper restore InconsistentNaming

        public int TransitionGroupCount { get { return GetCount((int)Level.TransitionGroups); } }
        public int TransitionCount { get { return GetCount((int)Level.Transitions); } }

        public IEnumerable<TransitionGroupDocNode> TransitionGroups
        {
            get { return Children.Cast<TransitionGroupDocNode>(); }
        }

        public bool HasHeavyTransitionGroups
        {
            get
            {
                return Children.Contains(node =>
                                         !((TransitionGroupDocNode) node).TransitionGroup.LabelType.IsLight);
            }
        }

        public bool HasLibInfo
        {
            get
            {
                return Children.Contains(node => ((TransitionGroupDocNode)node).HasLibInfo);
            }
        }

        public bool IsUserModified
        {
            get
            {
                if (!Annotations.IsEmpty)
                    return true;
                return Children.Cast<TransitionGroupDocNode>().Contains(nodeGroup => nodeGroup.IsUserModified);
            }
        }

        public PeptideDocNode Merge(PeptideDocNode nodePepMerge)
        {
            return Merge(nodePepMerge, (n, nMerge) => n.Merge(nMerge));
        }

        public PeptideDocNode Merge(PeptideDocNode nodePepMerge,
            Func<TransitionGroupDocNode, TransitionGroupDocNode, TransitionGroupDocNode> mergeMatch)
        {
            var childrenNew = Children.Cast<TransitionGroupDocNode>().ToList();
            // Remember where all the existing children are
            var dictPepIndex = new Dictionary<TransitionGroup, int>();
            for (int i = 0; i < childrenNew.Count; i++)
            {
                var key = childrenNew[i].TransitionGroup;
                if (!dictPepIndex.ContainsKey(key))
                    dictPepIndex[key] = i;
            }
            // Add the new children to the end, or merge when the node is already present
            foreach (TransitionGroupDocNode nodeGroup in nodePepMerge.Children)
            {
                int i;
                if (!dictPepIndex.TryGetValue(nodeGroup.TransitionGroup, out i))
                    childrenNew.Add(nodeGroup);
                else if (mergeMatch != null)
                    childrenNew[i] = mergeMatch(childrenNew[i], nodeGroup);
            }
            childrenNew.Sort(Peptide.CompareGroups);
            return (PeptideDocNode)ChangeChildrenChecked(childrenNew.Cast<DocNode>().ToArray());
        }

        public PeptideDocNode MergeUserInfo(PeptideDocNode nodePepMerge, SrmSettings settings, SrmSettingsDiff diff)
        {
            var result = Merge(nodePepMerge, (n, nMerge) => n.MergeUserInfo(nMerge, settings, diff));
            var annotations = Annotations.Merge(nodePepMerge.Annotations);
            if (!ReferenceEquals(annotations, Annotations))
                result = (PeptideDocNode) result.ChangeAnnotations(annotations);
            return result.UpdateResults(settings);
        }

        public PeptideDocNode ChangeSettings(SrmSettings settingsNew, SrmSettingsDiff diff)
        {
            return ChangeSettings(settingsNew, diff, true);
        }

        public PeptideDocNode ChangeSettings(SrmSettings settingsNew, SrmSettingsDiff diff, bool recurse)
        {
            Debug.Assert(!diff.DiffPeptideProps); // No settings dependent properties yet.

            // If the peptide has explicit modifications, and the modifications have
            // changed, see if any of the explicit modifications have changed
            var explicitMods = ExplicitMods;
            if (HasExplicitMods &&
                !diff.IsUnexplainedExplicitModificationAllowed &&
                diff.SettingsOld != null &&
                !ReferenceEquals(settingsNew.PeptideSettings.Modifications,
                                 diff.SettingsOld.PeptideSettings.Modifications))
            {
                explicitMods = ExplicitMods.ChangeGlobalMods(settingsNew);
                if (explicitMods == null || !ArrayUtil.ReferencesEqual(explicitMods.GetHeavyModifications().ToArray(),
                                                                       ExplicitMods.GetHeavyModifications().ToArray()))
                {
                    diff = new SrmSettingsDiff(diff, SrmSettingsDiff.ALL);                    
                }
                else if (!ReferenceEquals(explicitMods.StaticModifications, ExplicitMods.StaticModifications))
                {
                    diff = new SrmSettingsDiff(diff, SrmSettingsDiff.PROPS);
                }
            }

            TransitionInstrument instrument = settingsNew.TransitionSettings.Instrument;
            PeptideDocNode nodeResult = this;
            if (diff.DiffTransitionGroups && settingsNew.TransitionSettings.Filter.AutoSelect && AutoManageChildren)
            {
                IList<DocNode> childrenNew = new List<DocNode>();

                Dictionary<Identity, DocNode> mapIdToChild = CreateIdContentToChildMap();
                foreach (TransitionGroup tranGroup in Peptide.GetTransitionGroups(settingsNew, explicitMods, true))
                {
                    TransitionGroupDocNode nodeGroup;
                    SrmSettingsDiff diffNode = diff;

                    DocNode existing;
                    // Add values that existed before the change.
                    if (mapIdToChild.TryGetValue(tranGroup, out existing))
                        nodeGroup = (TransitionGroupDocNode)existing;
                        // Add new node
                    else
                    {
                        TransitionDocNode[] transitions = GetMatchingTransitions(
                            tranGroup, settingsNew, explicitMods);

                        nodeGroup = new TransitionGroupDocNode(tranGroup, transitions);
                        // If not recursing, then ChangeSettings will not be called on nodeGroup.  So, make
                        // sure its precursor m/z is set correctly.
                        if (!recurse)
                            nodeGroup = nodeGroup.ChangePrecursorMz(settingsNew, explicitMods);
                        diffNode = SrmSettingsDiff.ALL;
                    }

                    if (nodeGroup != null)
                    {
                        TransitionGroupDocNode nodeChanged = recurse ? nodeGroup.ChangeSettings(settingsNew, explicitMods, diffNode) : nodeGroup;
                        if (instrument.IsMeasurable(nodeChanged.PrecursorMz))
                            childrenNew.Add(nodeChanged);
                    }
                }

                // If only using rank limited peptides, then choose only the single
                // highest ranked precursor charge.
                PeptideRankId rankId = settingsNew.PeptideSettings.Libraries.RankId;
                if (rankId != null && settingsNew.PeptideSettings.Libraries.PeptideCount.HasValue)
                    childrenNew = FilterHighestRank(childrenNew, rankId);

                nodeResult = (PeptideDocNode) ChangeChildrenChecked(childrenNew);                
            }
            else
            {
                // Even with auto-select off, transition groups for which there is
                // no longer a precursor calculator must be removed.
                if (diff.DiffTransitionGroups && nodeResult.HasHeavyTransitionGroups)
                {
                    IList<DocNode> childrenNew = new List<DocNode>();
                    foreach (TransitionGroupDocNode nodeGroup in nodeResult.Children)
                    {
                        if (settingsNew.HasPrecursorCalc(nodeGroup.TransitionGroup.LabelType, explicitMods))
                            childrenNew.Add(nodeGroup);
                    }

                    nodeResult = (PeptideDocNode)ChangeChildrenChecked(childrenNew);
                }

                // Update properties and children, if necessary
                if (diff.DiffTransitionGroupProps ||
                    diff.DiffTransitions || diff.DiffTransitionProps ||
                    diff.DiffResults)
                {
                    IList<DocNode> childrenNew = new List<DocNode>();

                    // Enumerate the nodes making necessary changes.
                    foreach (TransitionGroupDocNode nodeGroup in nodeResult.Children)
                    {
                        TransitionGroupDocNode nodeChanged = nodeGroup.ChangeSettings(settingsNew, explicitMods, diff);
                        // Skip if the node can no longer be measured on the target instrument
                        if (!instrument.IsMeasurable(nodeChanged.PrecursorMz))
                            continue;
                        // Skip this node, if it is heavy and the update caused it to have the
                        // same m/z value as the light value.
                        if (!nodeChanged.TransitionGroup.LabelType.IsLight)
                        {
                            double precursorMassLight = settingsNew.GetPrecursorMass(
                                IsotopeLabelType.light, Peptide.Sequence, explicitMods);
                            double precursorMzLight = SequenceMassCalc.GetMZ(precursorMassLight,
                                                                             nodeChanged.TransitionGroup.PrecursorCharge);
                            if (nodeChanged.PrecursorMz == precursorMzLight)
                                continue;
                        }

                        childrenNew.Add(nodeChanged);
                    }

                    nodeResult = (PeptideDocNode)ChangeChildrenChecked(childrenNew);
                }                
            }

            if (!ReferenceEquals(explicitMods, ExplicitMods))
                nodeResult = nodeResult.ChangeExplicitMods(explicitMods);
            if (diff.DiffResults || ChangedResults(nodeResult))
                nodeResult = nodeResult.UpdateResults(settingsNew /*, diff*/);

            return nodeResult;
        }

        /// <summary>
        /// Make sure children are preserved as much as possible.  It may not be possible
        /// to always preserve children, because the settings in the target document may
        /// not allow certain states (e.g. label types that to not exist in the target).
        /// </summary>
        public PeptideDocNode EnsureChildren(SrmSettings settings, bool peptideList)
        {
            var result = this;

            // Make a first attempt at changing to the new settings to figure out
            // whether this will change the children.
            var changed = result.ChangeSettings(settings, SrmSettingsDiff.ALL);
            // If the children are auto-managed, and they changed, figure out whether turning off
            // auto-manage will allow the children to be preserved better.
            if (result.AutoManageChildren && !AreEquivalentChildren(result.Children, changed.Children))
            {
                // Turn off auto-manage and change again.
                var resultAutoManageFalse = (PeptideDocNode)result.ChangeAutoManageChildren(false);
                var changedAutoManageFalse = resultAutoManageFalse.ChangeSettings(settings, SrmSettingsDiff.ALL);
                // If the children are not the same as they were with auto-manage on, then use
                // a the version of this node with auto-manage turned off.
                if (!AreEquivalentChildren(changed.Children, changedAutoManageFalse.Children))
                {
                    result = resultAutoManageFalse;
                    changed = changedAutoManageFalse;
                }
            }
            // If this is being added to a peptide list, but was a FASTA sequence,
            // make sure the Peptide ID no longer points to the old FASTA sequence.
            if (peptideList && Peptide.FastaSequence != null)
            {
                result = new PeptideDocNode(new Peptide(null, Peptide.Sequence, null, null, Peptide.MissedCleavages),
                                            result.ExplicitMods, new TransitionGroupDocNode[0], result.AutoManageChildren); 
            }
            // Create a new child list, using existing children where GlobalIndexes match.
            var dictIndexToChild = Children.ToDictionary(child => child.Id.GlobalIndex);
            var listChildren = new List<DocNode>();
            foreach (TransitionGroupDocNode nodePep in changed.Children)
            {
                DocNode child;
                if (dictIndexToChild.TryGetValue(nodePep.Id.GlobalIndex, out child))
                {
                    listChildren.Add(((TransitionGroupDocNode)child).EnsureChildren(result, ExplicitMods, settings));
                }
            }
            return (PeptideDocNode)result.ChangeChildrenChecked(listChildren);
        }

        public static bool AreEquivalentChildren(IList<DocNode> children1, IList<DocNode> children2)
        {
            if(children1.Count != children2.Count)
                return false;
            for (int i = 0; i < children1.Count; i++)
            {
                if(!Equals(children1[i].Id, children2[i].Id))
                    return false;
            }
            return true;
        }

        public PeptideDocNode EnsureMods(PeptideModifications source, PeptideModifications target,
                                         MappedList<string, StaticMod> defSetStat, MappedList<string, StaticMod> defSetHeavy)
        {
            // Create explicit mods matching the implicit mods on this peptide for each document.
            var sourceImplicitMods = new ExplicitMods(this, source.StaticModifications, defSetStat, source.GetHeavyModifications(), defSetHeavy);
            var targetImplicitMods = new ExplicitMods(this, target.StaticModifications, defSetStat, target.GetHeavyModifications(), defSetHeavy);
            
            // If modifications match, no need to create explicit modifications for the peptide.
            if (sourceImplicitMods.Equals(targetImplicitMods))
                return this;

            // Add explicit mods if static mods not implicit in the target document.
            IList<ExplicitMod> newExplicitStaticMods = null;
            bool preserveVariable = HasVariableMods;
            // Preserve non-variable explicit modifications
            if (!preserveVariable && HasExplicitMods && ExplicitMods.StaticModifications != null)
            {
                // If they are not the same as the implicit modifications in the new document
                if (!ArrayUtil.EqualsDeep(ExplicitMods.StaticModifications, targetImplicitMods.StaticModifications))
                    newExplicitStaticMods = ExplicitMods.StaticModifications;
            }
            else if (!ArrayUtil.EqualsDeep(sourceImplicitMods.StaticModifications, targetImplicitMods.StaticModifications))
            {
                preserveVariable = false;
                newExplicitStaticMods = sourceImplicitMods.StaticModifications;
            }
            else if (preserveVariable)
            {
                newExplicitStaticMods = ExplicitMods.StaticModifications;
            }
                
            // Drop explicit mods if matching implicit mods are found in the target document.
            IList<TypedExplicitModifications> newExplicitHeavyMods = new List<TypedExplicitModifications>();
            // For each heavy label type, add explicit mods if static mods not found in the target document.
            foreach (TypedExplicitModifications targetDocMod in targetImplicitMods.GetHeavyModifications())
            {
                // Use explicit modifications when available.  Otherwise, compare against new implicit modifications
                IList<ExplicitMod> heavyMods = (HasExplicitMods ? ExplicitMods.GetModifications(targetDocMod.LabelType) : null) ??
                    sourceImplicitMods.GetModifications(targetDocMod.LabelType);
                if (heavyMods != null && !ArrayUtil.EqualsDeep(heavyMods, targetDocMod.Modifications) && heavyMods.Count > 0)
                    newExplicitHeavyMods.Add(new TypedExplicitModifications(Peptide, targetDocMod.LabelType, heavyMods));
            }

            if (newExplicitStaticMods != null || newExplicitHeavyMods.Count > 0)
                return ChangeExplicitMods(new ExplicitMods(Peptide, newExplicitStaticMods, newExplicitHeavyMods, preserveVariable));
            return ChangeExplicitMods(null);
        }

        private static IList<DocNode> FilterHighestRank(IList<DocNode> childrenNew, PeptideRankId rankId)
        {
            if (childrenNew.Count < 2)
                return childrenNew;
            int maxCharge = 0;
            float maxValue = float.MinValue;
            foreach (TransitionGroupDocNode nodeGroup in childrenNew)
            {
                float rankValue = nodeGroup.GetRankValue(rankId);
                if (rankValue > maxValue)
                {
                    maxCharge = nodeGroup.TransitionGroup.PrecursorCharge;
                    maxValue = rankValue;
                }
            }
            var listHighestRankChildren = new List<DocNode>();
            foreach (TransitionGroupDocNode nodeGroup in childrenNew)
            {
                if (nodeGroup.TransitionGroup.PrecursorCharge == maxCharge)
                    listHighestRankChildren.Add(nodeGroup);
            }
            return listHighestRankChildren;
        }

        public TransitionDocNode[] GetMatchingTransitions(TransitionGroup tranGroup, SrmSettings settings, ExplicitMods explicitMods)
        {
            // If no calculator for this type, then not possible to calculate transtions
            var calc = settings.GetFragmentCalc(tranGroup.LabelType, explicitMods);
            if (calc == null)
                return null;

            int iMatch = Children.IndexOf(nodeGroup =>
                                          ((TransitionGroupDocNode)nodeGroup).TransitionGroup.PrecursorCharge == tranGroup.PrecursorCharge);
            if (iMatch == -1)
                return null;
            TransitionGroupDocNode nodeGroupMatching = (TransitionGroupDocNode) Children[iMatch];
            // If the matching node is auto-managed, and auto-select is on in the settings,
            // then returning no transitions should allow transitions to be chosen correctly
            // automatically.
            if (nodeGroupMatching.AutoManageChildren && settings.TransitionSettings.Filter.AutoSelect)
                return null;
            var listTrans = new List<TransitionDocNode>();
            foreach (TransitionDocNode nodeTran in nodeGroupMatching.Children)
            {
                var transition = nodeTran.Transition;
                var losses = nodeTran.Losses;
                var tranNew = new Transition(tranGroup,
                                             transition.IonType,
                                             transition.CleavageOffset,
                                             transition.MassIndex,
                                             transition.Charge);
                var isotopeDist = nodeGroupMatching.IsotopeDist;
                double massH = calc.GetFragmentMass(tranNew, isotopeDist);
                var isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(tranNew, isotopeDist);
                listTrans.Add(new TransitionDocNode(tranNew, losses, massH, isotopeDistInfo, null));
            }
            return listTrans.ToArray();
        }

        private PeptideDocNode UpdateResults(SrmSettings settingsNew /*, SrmSettingsDiff diff*/)
        {
            // First check whether any child results are present
            if (!settingsNew.HasResults || Children.Count == 0)
            {
                if (!HasResults)
                    return this;
                return ChangeResults(null);
            }

            // Update the results summary
            var resultsCalc = new PeptideResultsCalculator(settingsNew);
            foreach (TransitionGroupDocNode nodeGroup in Children)
                resultsCalc.AddGroupChromInfo(nodeGroup);

            return resultsCalc.UpdateResults(this);
        }

        private bool ChangedResults(DocNodeParent nodePeptide)
        {
            if (nodePeptide.Children.Count != Children.Count)
                return true;

            int iChild = 0;
            foreach (TransitionGroupDocNode nodeGroup in Children)
            {
                // Results will differ if the identies of the children differ
                // at all.
                var nodeGroup2 = (TransitionGroupDocNode)nodePeptide.Children[iChild];
                if (!ReferenceEquals(nodeGroup.Id, nodeGroup2.Id))
                    return true;

                // or if the results for any child have changed
                if (!ReferenceEquals(nodeGroup.Results, nodeGroup2.Results))
                    return true;

                iChild++;
            }
            return false;
        }

        public override string GetDisplayText(DisplaySettings settings)
        {
            return PeptideTreeNode.DisplayText(this, settings);
        }

        private sealed class PeptideResultsCalculator
        {
            private readonly List<PeptideChromInfoListCalculator> _listResultCalcs =
                new List<PeptideChromInfoListCalculator>();

            public PeptideResultsCalculator(SrmSettings settings)
            {
                Settings = settings;
            }

            private SrmSettings Settings { get; set; }
            private int TransitionGroupCount { get; set; }

            public void AddGroupChromInfo(TransitionGroupDocNode nodeGroup)
            {
                TransitionGroupCount++;

                if (nodeGroup.HasResults)
                {
                    int countResults = nodeGroup.Results.Count;
                    while (_listResultCalcs.Count < countResults)
                    {
                        var calc = new PeptideChromInfoListCalculator(Settings, _listResultCalcs.Count);
                        _listResultCalcs.Add(calc);
                    }
                    for (int i = 0; i < countResults; i++)
                    {
                        var calc = _listResultCalcs[i];
                        calc.AddChromInfoList(nodeGroup);
                        foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                            calc.AddChromInfoList(nodeTran);
                    }
                }
            }

            public PeptideDocNode UpdateResults(PeptideDocNode nodePeptide)
            {
                var listChromInfoList = _listResultCalcs.ConvertAll(calc =>
                                                                    calc.CalcChromInfoList(TransitionGroupCount));
                var results = Results<PeptideChromInfo>.Merge(nodePeptide.Results, listChromInfoList);
                if (!ReferenceEquals(results, nodePeptide.Results))
                    nodePeptide = nodePeptide.ChangeResults(results);

                var listGroupsNew = new List<DocNode>();
                foreach (TransitionGroupDocNode nodeGroup in nodePeptide.Children)
                {
                    // Update transition group ratios
                    var nodeGroupConvert = nodeGroup;
                    var listGroupInfoList = _listResultCalcs.ConvertAll(
                        calc => calc.UpdateTransitonGroupRatios(nodeGroupConvert,
                                                                nodeGroupConvert.HasResults
                                                                    ? nodeGroupConvert.Results[calc.ResultsIndex]
                                                                    : null));
                    var resultsGroup = Results<TransitionGroupChromInfo>.Merge(nodeGroup.Results, listGroupInfoList);
                    var nodeGroupNew = nodeGroup;
                    if (!ReferenceEquals(resultsGroup, nodeGroup.Results))
                        nodeGroupNew = nodeGroup.ChangeResults(resultsGroup);

                    var listTransNew = new List<DocNode>();
                    foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                    {
                        // Update transition ratios
                        var nodeTranConvert = nodeTran;
                        var listTranInfoList = _listResultCalcs.ConvertAll(
                            calc => calc.UpdateTransitonRatios(nodeTranConvert, nodeTranConvert.Results[calc.ResultsIndex]));
                        var resultsTran = Results<TransitionChromInfo>.Merge(nodeTran.Results, listTranInfoList);
                        listTransNew.Add(ReferenceEquals(resultsTran, nodeTran.Results)
                                             ? nodeTran
                                             : nodeTran.ChangeResults(resultsTran));
                    }
                    listGroupsNew.Add(nodeGroupNew.ChangeChildrenChecked(listTransNew));
                }
                return (PeptideDocNode) nodePeptide.ChangeChildrenChecked(listGroupsNew);
            }
        }

        private sealed class PeptideChromInfoListCalculator
        {
            public PeptideChromInfoListCalculator(SrmSettings settings, int resultsIndex)
            {
                ResultsIndex = resultsIndex;
                Settings = settings;
                Calculators = new Dictionary<int, PeptideChromInfoCalculator>();
            }

            public int ResultsIndex { get; private set; }

            private SrmSettings Settings { get; set; }
            private Dictionary<int, PeptideChromInfoCalculator> Calculators { get; set; }

            public void AddChromInfoList(TransitionGroupDocNode nodeGroup)
            {
                var listInfo = nodeGroup.Results[ResultsIndex];
                if (listInfo == null)
                    return;

                foreach (var info in listInfo)
                {
                    if (info.OptimizationStep != 0)
                        continue;

                    PeptideChromInfoCalculator calc;
                    if (!Calculators.TryGetValue(info.FileIndex, out calc))
                    {
                        calc = new PeptideChromInfoCalculator(Settings, ResultsIndex);
                        Calculators.Add(info.FileIndex, calc);
                    }
                    calc.AddChromInfo(nodeGroup, info);
                }
            }

            public void AddChromInfoList(TransitionDocNode nodeTran)
            {
                var listInfo = nodeTran.Results[ResultsIndex];
                if (listInfo == null)
                    return;

                foreach (var info in listInfo)
                {
                    if (info.OptimizationStep != 0)
                        continue;

                    PeptideChromInfoCalculator calc;
                    if (!Calculators.TryGetValue(info.FileIndex, out calc))
                    {
                        calc = new PeptideChromInfoCalculator(Settings, ResultsIndex);
                        Calculators.Add(info.FileIndex, calc);
                    }
                    calc.AddChromInfo(nodeTran, info);
                }
            }

            public IList<PeptideChromInfo> CalcChromInfoList(int transitionGroupCount)
            {
                if (Calculators.Count == 0)
                    return null;

                var listCalc = new List<PeptideChromInfoCalculator>(Calculators.Values);
                listCalc.Sort((c1, c2) => c1.FileOrder - c2.FileOrder);

                var listInfo = listCalc.ConvertAll(calc => calc.CalcChromInfo(transitionGroupCount));
                return (listInfo[0] != null ? listInfo : null);
            }

            public IList<TransitionChromInfo> UpdateTransitonRatios(TransitionDocNode nodeTran,
                                                                    IList<TransitionChromInfo> listInfo)
            {
                if (Calculators.Count == 0 || listInfo == null)
                    return null;

                var listInfoNew = new List<TransitionChromInfo>();
                var standardTypes = Settings.PeptideSettings.Modifications.InternalStandardTypes;
                foreach (var info in listInfo)
                {
                    PeptideChromInfoCalculator calc;
                    if (!Calculators.TryGetValue(info.FileIndex, out calc))
                        Debug.Assert(false);    // Should never happen
                    else
                    {
                        var infoNew = info;
                        var labelType = nodeTran.Transition.Group.LabelType;

                        var ratios = new float?[standardTypes.Count];
                        for (int i = 0; i < ratios.Length; i++)
                            ratios[i] = calc.CalcTransitionRatio(nodeTran, labelType, standardTypes[i]);
                        if (!ArrayUtil.EqualsDeep(ratios, info.Ratios))
                            infoNew = infoNew.ChangeRatios(ratios);
                        
                        listInfoNew.Add(infoNew);
                    }
                }
                if (ArrayUtil.ReferencesEqual(listInfo, listInfoNew))
                    return listInfo;
                return listInfoNew;
            }

            public IList<TransitionGroupChromInfo> UpdateTransitonGroupRatios(TransitionGroupDocNode nodeGroup,
                                                                              IList<TransitionGroupChromInfo> listInfo)
            {
                if (Calculators.Count == 0 || listInfo == null)
                    return null;

                var listInfoNew = new List<TransitionGroupChromInfo>();
                var standardTypes = Settings.PeptideSettings.Modifications.InternalStandardTypes;
                foreach (var info in listInfo)
                {
                    PeptideChromInfoCalculator calc;
                    if (!Calculators.TryGetValue(info.FileIndex, out calc))
                        Debug.Assert(false);    // Should never happen
                    else
                    {
                        var infoNew = info;
                        var labelType = nodeGroup.TransitionGroup.LabelType;

                        var ratios = new float?[standardTypes.Count];
                        var ratioStdevs = new float?[standardTypes.Count];
                        for (int i = 0; i < ratios.Length; i++)
                        {
                            float? stdev;
                            ratios[i] = calc.CalcTransitionGroupRatio(nodeGroup,
                                                                      labelType, standardTypes[i], out stdev);
                            ratioStdevs[i] = stdev;
                        }
                        if (!ArrayUtil.EqualsDeep(ratios, info.Ratios) ||
                            !ArrayUtil.EqualsDeep(ratioStdevs, info.RatioStdevs))
                            infoNew = infoNew.ChangeRatios(ratios, ratioStdevs);

                        listInfoNew.Add(infoNew);
                    }
                }
                if (ArrayUtil.ReferencesEqual(listInfo, listInfoNew))
                    return listInfo;
                return listInfoNew;
            }
        }

        private sealed class PeptideChromInfoCalculator
        {
            public PeptideChromInfoCalculator(SrmSettings settings, int resultsIndex)
            {
                Settings = settings;
                ResultsIndex = resultsIndex;
                TranAreas = new Dictionary<TransitionKey, float>();
            }

            private SrmSettings Settings { get; set; }
            private int ResultsIndex { get; set; }
            private ChromFileInfoId FileId { get; set; }
            public int FileOrder { get; private set; }
            private double PeakCountRatioTotal { get; set; }
            private int ResultsCount { get; set; }
            private int RetentionTimesMeasured { get; set; }
            private double RetentionTimeTotal { get; set; }

            private Dictionary<TransitionKey, float> TranAreas { get; set; }

// ReSharper disable UnusedParameter.Local
            public void AddChromInfo(TransitionGroupDocNode nodeGroup,
                                     TransitionGroupChromInfo info)
// ReSharper restore UnusedParameter.Local
            {
                if (info == null)
                    return;

                Debug.Assert(FileId == null || ReferenceEquals(info.FileId, FileId));
                FileId = info.FileId;
                FileOrder = Settings.MeasuredResults.Chromatograms[ResultsIndex].IndexOfId(FileId);

                ResultsCount++;
                PeakCountRatioTotal += info.PeakCountRatio;
                if (info.RetentionTime.HasValue)
                {
                    RetentionTimesMeasured++;
                    RetentionTimeTotal += info.RetentionTime.Value;
                }
            }

            public void AddChromInfo(TransitionDocNode nodeTran, TransitionChromInfo info)
            {
                // Only add non-zero areas
                if (info.Area == 0)
                    return;

                float area;
                var key = new TransitionKey(nodeTran.Transition);
                if (!TranAreas.TryGetValue(key, out area))
                    TranAreas.Add(key, info.Area);
                else
                    TranAreas[key] = area + info.Area;
            }

            public PeptideChromInfo CalcChromInfo(int transitionGroupCount)
            {
                if (ResultsCount == 0)
                    return null;

                float peakCountRatio = (float) (PeakCountRatioTotal/transitionGroupCount);

                float? retentionTime = null;
                if (RetentionTimesMeasured > 0)
                    retentionTime = (float) (RetentionTimeTotal/RetentionTimesMeasured);
                var mods = Settings.PeptideSettings.Modifications;
                var listRatios = new List<PeptideLabelRatio>();
                foreach (var standardType in mods.InternalStandardTypes)
                {
                    foreach (var labelType in mods.GetModificationTypes())
                    {
                        if (ReferenceEquals(standardType, labelType))
                            continue;

                        float? stdev;
                        float? ratio = CalcTransitionGroupRatio(-1, labelType, standardType, out stdev);
                        listRatios.Add(new PeptideLabelRatio(labelType, standardType, ratio, stdev));
                    }                    
                }

                return new PeptideChromInfo(FileId, peakCountRatio, retentionTime, listRatios.ToArray());
            }

            public float? CalcTransitionRatio(TransitionDocNode nodeTran,
                                              IsotopeLabelType labelTypeNum, IsotopeLabelType labelTypeDenom)
            {
                // Avoid 1.0 ratios for self-to-self
                if (ReferenceEquals(labelTypeNum, labelTypeDenom))
                    return null;

                float areaNum, areaDenom;
                var keyNum = new TransitionKey(nodeTran.Transition, labelTypeNum);
                var keyDenom = new TransitionKey(nodeTran.Transition, labelTypeDenom);
                if (!TranAreas.TryGetValue(keyNum, out areaNum) ||
                    !TranAreas.TryGetValue(keyDenom, out areaDenom))
                    return null;
                return areaNum/areaDenom;
            }

            public float? CalcTransitionGroupRatio(TransitionGroupDocNode nodeGroup,
                                                   IsotopeLabelType labelTypeNum,
                                                   IsotopeLabelType labelTypeDenom,
                                                   out float? stdev)
            {
                return CalcTransitionGroupRatio(nodeGroup.TransitionGroup.PrecursorCharge,
                                                labelTypeNum, labelTypeDenom, out stdev);
            }

            private float? CalcTransitionGroupRatio(int precursorCharge,
                                                    IsotopeLabelType labelTypeNum,
                                                    IsotopeLabelType labelTypeDenom,
                                                    out float? stdev)
            {
                // Avoid 1.0 ratios for self-to-self
                if (ReferenceEquals(labelTypeNum, labelTypeDenom))
                {
                    stdev = null;
                    return null;
                }

                double areaTotalNum = 0;
                double areaTotalDenom = 0;

                List<double> ratios = new List<double>();
                List<double> weights = new List<double>();

                foreach (var pair in GetAreaPairs(labelTypeNum))
                {
                    var key = pair.Key;
                    if (precursorCharge != -1 && key.PrecursorCharge != precursorCharge)
                        continue;

                    float areaNum = pair.Value;
                    float areaDenom;
                    if (!TranAreas.TryGetValue(new TransitionKey(key, labelTypeDenom), out areaDenom))
                        continue;

                    areaTotalNum += areaNum;
                    areaTotalDenom += areaDenom;

                    ratios.Add(areaNum/areaDenom);
                    weights.Add(areaDenom);
                }

                switch (ratios.Count)
                {
                    case 0:
                        stdev = null;
                        return null;
                    case 1:
                        stdev = 0;
                        return (float)ratios[0];
                }

                var stats = new Statistics(ratios);
                var statsW = new Statistics(weights);
                stdev = (float)stats.StdDev(statsW);
                double mean = areaTotalNum/areaTotalDenom;
                Debug.Assert(Math.Abs(mean - stats.Mean(statsW)) < 0.0001);
                // Make sure the value does not exceed the bounds of a float.
                return (float) Math.Min(float.MaxValue, Math.Max(float.MinValue, mean));
            }

            private IEnumerable<KeyValuePair<TransitionKey, float>> GetAreaPairs(IsotopeLabelType labelType)
            {
                return from pair in TranAreas
                       where ReferenceEquals(labelType, pair.Key.LabelType)
                       select pair;
            }
        }

        private struct TransitionKey
        {
            private readonly IonType _ionType;
            private readonly int _ionOrdinal;
            private readonly int _charge;
            private readonly int _precursorCharge;
            private readonly IsotopeLabelType _labelType;

            public TransitionKey(Transition transition)
                : this (transition, transition.Group.LabelType)
            {
            }

            public TransitionKey(Transition transition, IsotopeLabelType labelType)
            {
                _ionType = transition.IonType;
                _ionOrdinal = transition.Ordinal;
                _charge = transition.Charge;
                _precursorCharge = transition.Group.PrecursorCharge;
                _labelType = labelType;
            }

            public TransitionKey(TransitionKey key, IsotopeLabelType labelType)
            {
                _ionType = key._ionType;
                _ionOrdinal = key._ionOrdinal;
                _charge = key._charge;
                _precursorCharge = key._precursorCharge;
                _labelType = labelType;
            }

            public int PrecursorCharge { get { return _precursorCharge; } }
            public IsotopeLabelType LabelType { get { return _labelType; } }

            #region object overrides

            private bool Equals(TransitionKey other)
            {
                return Equals(other._ionType, _ionType) &&
                       other._ionOrdinal == _ionOrdinal &&
                       other._charge == _charge &&
                       other._precursorCharge == _precursorCharge &&
                       Equals(other._labelType, _labelType);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (obj.GetType() != typeof (TransitionKey)) return false;
                return Equals((TransitionKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int result = _ionType.GetHashCode();
                    result = (result*397) ^ _ionOrdinal;
                    result = (result*397) ^ _charge;
                    result = (result*397) ^ _precursorCharge;
                    result = (result*397) ^ _labelType.GetHashCode();
                    return result;
                }
            }

            #endregion
        }

        #region object overrides

        public bool Equals(PeptideDocNode other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                Equals(other.ExplicitMods, ExplicitMods) &&
                other.Rank.Equals(Rank) &&
                Equals(other.Results, Results) &&
                other.BestResult == BestResult;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as PeptideDocNode);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (ExplicitMods != null ? ExplicitMods.GetHashCode() : 0);
                result = (result*397) ^ (Rank.HasValue ? Rank.Value : 0);
                result = (result*397) ^ (Results != null ? Results.GetHashCode() : 0);
                result = (result*397) ^ BestResult;
                return result;
            }
        }

        public override string ToString()
        {
            return Rank.HasValue ? string.Format("{0} (rank {1})", Peptide, Rank) : Peptide.ToString();
        }

        #endregion
    }
}