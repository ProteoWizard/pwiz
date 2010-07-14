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
    public class PeptideDocNode : DocNodeParent
    {
        public PeptideDocNode(Peptide id, TransitionGroupDocNode[] children)
            : this(id, children, true)
        {
        }

        public PeptideDocNode(Peptide id, TransitionGroupDocNode[] children, bool autoManageChildren)
            : this(id, null, Annotations.EMPTY, null, null, children, autoManageChildren)
        {
        }

        public PeptideDocNode(Peptide id, ExplicitMods mods)
            : this(id, null, Annotations.EMPTY, mods, null, new TransitionGroupDocNode[0], true)
        {
        }

        public PeptideDocNode(Peptide id, int? rank, Annotations annotations, ExplicitMods mods,
                Results<PeptideChromInfo> results, TransitionGroupDocNode[] children, bool autoManageChildren)
            : base(id, annotations, children, autoManageChildren)
        {
            ExplicitMods = mods;
            Rank = rank;
            Results = results;
            BestResult = CalcBestResult();
        }

        public Peptide Peptide { get { return (Peptide)Id; } }

        public PeptideModKey Key { get { return new PeptideModKey(Peptide, ExplicitMods);}}

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.peptide; } }

        public ExplicitMods ExplicitMods { get; private set; }

        public bool HasExplicitMods { get { return ExplicitMods != null; }}

        public bool HasVariableMods { get { return HasExplicitMods && ExplicitMods.IsVariableStaticMods; } }

        public bool AreVariableModsPossible(int maxVariableMods, IEnumerable<StaticMod> modsVarAvailable)
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

        public bool HasChildType(IsotopeLabelType labelType)
        {
            return Children.Contains(nodeGroup => ReferenceEquals(labelType,
                ((TransitionGroupDocNode)nodeGroup).TransitionGroup.LabelType));
        }

        public int? Rank { get; private set; }

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
            return result[0].PeakCountRatio;
        }

        public float? AveragePeakCountRatio
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.PeakCountRatio);
            }
        }

        public float? GetMeasuredRetentionTime(int i)
        {
            if (i == -1)
                return AverageMeasuredRetentionTime;

            var result = GetSafeChromInfo(i);
            if (result == null)
                return null;
            return result[0].RetentionTime;
        }

        public float? AverageMeasuredRetentionTime
        {
            get
            {
                return GetAverageResultValue(chromInfo =>
                    !chromInfo.RetentionTime.HasValue ?
                        (float?) null : chromInfo.RetentionTime.Value);
            }
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
                        for (int j = 0; j < result.Count; j++)
                        {
                            var chromInfo = result[j];
                            if (chromInfo != null && chromInfo.Area > 0)
                            {
                                tranArea += chromInfo.Area;
                                tranMeasured++;                                
                            }
                        }
                        groupArea += tranArea/result.Count;
                        groupTranMeasured += tranMeasured/result.Count;
                    }
                    productArea += groupArea * Math.Pow(10, groupTranMeasured);
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

        public PeptideDocNode ChangeSettings(SrmSettings settingsNew, SrmSettingsDiff diff)
        {
            Debug.Assert(!diff.DiffPeptideProps); // No settings dependent properties yet.

            // If the peptide has explicit modifications, and the modifications have
            // changed, see if any of the explicit modifications have changed
            var explicitMods = ExplicitMods;
            if (HasExplicitMods && diff.SettingsOld != null &&
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

                        nodeGroup = new TransitionGroupDocNode(tranGroup,
                            settingsNew.GetPrecursorMass(tranGroup.LabelType, Peptide.Sequence, explicitMods),
                            settingsNew.GetRelativeRT(tranGroup.LabelType, Peptide.Sequence, explicitMods),
                            transitions ?? new TransitionDocNode[0], transitions == null);
                        diffNode = SrmSettingsDiff.ALL;
                    }

                    if (nodeGroup != null)
                    {
                        TransitionGroupDocNode nodeChanged = nodeGroup.ChangeSettings(settingsNew, explicitMods, diffNode);
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

        public PeptideDocNode EnsureMods(PeptideModifications source, PeptideModifications target,
            MappedList<string, StaticMod> defSetStat, MappedList<string, StaticMod> defSetHeavy)
        {
            // Create explicit mods matching the implicit mods on this peptide for each document.
            var sourceImplicitMods = new ExplicitMods(this, source.StaticModifications, defSetStat, source.GetHeavyModifications(), defSetHeavy);
            var targetImplicitMods = new ExplicitMods(this, target.StaticModifications, defSetStat, target.GetHeavyModifications(), defSetHeavy);
            
            // If modifications match, no need to create explicit modifications for the peptide.
            if (sourceImplicitMods.Equals(targetImplicitMods))
                return this;

            // If the target document contains any implicit mods matching explicit mods on the node, drop these modifcations.
            IList<ExplicitMod> newExplicitStaticMods = (HasExplicitMods && ExplicitMods.StaticModifications != null)? ExplicitMods.StaticModifications.ToList()
                                                           : new List<ExplicitMod>();
            foreach(ExplicitMod mod in targetImplicitMods.StaticModifications)
            {
                newExplicitStaticMods.Remove(mod);
            }

            // Add explicit modifications for an implicit modifications from the source document that are not in the target document.
            foreach(ExplicitMod mod in sourceImplicitMods.StaticModifications)
            {
                if (!targetImplicitMods.StaticModifications.Contains(mod))
                    newExplicitStaticMods.Add(mod);
            }

            // Foreach heavy mod label type, add explicit mods for any heavy modification from the source docment that is not in the target document.
            IList<TypedExplicitModifications> newExplicitHeavyMods = new List<TypedExplicitModifications>();
            foreach (TypedExplicitModifications targetDocMod in targetImplicitMods.GetHeavyModifications())
            {
                IList<ExplicitMod> heavyMods = sourceImplicitMods.GetModifications(targetDocMod.LabelType);
                
                if (heavyMods == null || targetDocMod.Modifications.Equals(heavyMods))
                    continue;

                IList<ExplicitMod> missingHeavyMods = new List<ExplicitMod>();
                foreach (ExplicitMod sourceDocMod in heavyMods)
                {
                    if(!targetDocMod.Modifications.Contains(sourceDocMod))
                        missingHeavyMods.Add(sourceDocMod);
                }
                if (missingHeavyMods.Count > 0)
                    newExplicitHeavyMods.Add(new TypedExplicitModifications(Peptide, targetDocMod.LabelType, missingHeavyMods));
            }

            if (newExplicitStaticMods.Count() > 0)
                return ChangeExplicitMods(new ExplicitMods(Peptide, newExplicitStaticMods, newExplicitHeavyMods));
            else if (newExplicitHeavyMods.Count > 0)
                return ChangeExplicitMods(new ExplicitMods(Peptide, null, newExplicitHeavyMods));
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
                var tranNew = new Transition(tranGroup,
                    transition.IonType, transition.CleavageOffset, transition.Charge);
                double massH = settings.GetFragmentMass(tranGroup.LabelType, explicitMods, tranNew);
                listTrans.Add(new TransitionDocNode(tranNew, massH, null));
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
                        calc.AddChromInfoList(nodeGroup, nodeGroup.Results[i]);
                        foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                            calc.AddChromInfoList(nodeTran, nodeTran.Results[i]);
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
                    var listGroupInfoList = _listResultCalcs.ConvertAll(calc =>
                        calc.UpdateTransitonGroupRatios(nodeGroupConvert,
                            nodeGroupConvert.HasResults ? nodeGroupConvert.Results[calc.ResultsIndex] : null));
                    var resultsGroup = Results<TransitionGroupChromInfo>.Merge(nodeGroup.Results, listGroupInfoList);
                    var nodeGroupNew = nodeGroup;
                    if (!ReferenceEquals(results, nodeGroup.Results))
                        nodeGroupNew = nodeGroup.ChangeResults(resultsGroup);

                    var listTransNew = new List<DocNode>();
                    foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                    {
                        // Update transition ratios
                        var nodeTranConvert = nodeTran;
                        var listTranInfoList = _listResultCalcs.ConvertAll(calc =>
                            calc.UpdateTransitonRatios(nodeTranConvert, nodeTranConvert.Results[calc.ResultsIndex]));
                        var resultsTran = Results<TransitionChromInfo>.Merge(nodeTran.Results, listTranInfoList);
                        listTransNew.Add(ReferenceEquals(results, nodeTran.Results) ?
                            nodeTran : nodeTran.ChangeResults(resultsTran));
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

            public void AddChromInfoList(TransitionGroupDocNode nodeGroup,
                IEnumerable<TransitionGroupChromInfo> listInfo)
            {
                if (listInfo == null)
                    return;

                foreach (var info in listInfo)
                {
                    if (info.OptimizationStep != 0)
                        continue;

                    PeptideChromInfoCalculator calc;
                    if (!Calculators.TryGetValue(info.FileIndex, out calc))
                    {
                        calc = new PeptideChromInfoCalculator(Settings);
                        Calculators.Add(info.FileIndex, calc);
                    }
                    calc.AddChromInfo(nodeGroup, info);
                }
            }

            public void AddChromInfoList(TransitionDocNode nodeTran,
                IEnumerable<TransitionChromInfo> listInfo)
            {
                if (listInfo == null)
                    return;

                foreach (var info in listInfo)
                {
                    if (info.OptimizationStep != 0)
                        continue;

                    PeptideChromInfoCalculator calc;
                    if (!Calculators.TryGetValue(info.FileIndex, out calc))
                    {
                        calc = new PeptideChromInfoCalculator(Settings);
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
                listCalc.Sort((c1, c2) => c1.FileIndex - c2.FileIndex);

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
            public PeptideChromInfoCalculator(SrmSettings settings)
            {
                FileIndex = -1;
                Settings = settings;
                TranAreas = new Dictionary<TransitionKey, float>();
            }

            public int FileIndex { get; private set; }
            private SrmSettings Settings { get; set; }
            private double PeakCountRatioTotal { get; set; }
            private int ResultsCount { get; set; }
            private int RetentionTimesMeasured { get; set; }
            private double RetentionTimeTotal { get; set; }

            private Dictionary<TransitionKey, float> TranAreas { get; set; }

// ReSharper disable UnusedParameter.Local
            public void AddChromInfo(TransitionGroupDocNode nodeGroup, TransitionGroupChromInfo info)
// ReSharper restore UnusedParameter.Local
            {
                if (info == null)
                    return;

                Debug.Assert(FileIndex == -1 || info.FileIndex == FileIndex);
                FileIndex = info.FileIndex;

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

                return new PeptideChromInfo(FileIndex, peakCountRatio, retentionTime, listRatios.ToArray());
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

        public bool Equals(PeptideDocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.Rank.Equals(Rank);
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
                return (base.GetHashCode()*397) ^ (Rank.HasValue ? Rank.Value : 0);
            }
        }

        public override string ToString()
        {
            return Rank.HasValue ? string.Format("{0} (rank {1})", Peptide, Rank) : Peptide.ToString();
        }

        #endregion
    }

    public class Peptide : Identity
    {
        private readonly FastaSequence _fastaSequence;

        public Peptide(FastaSequence fastaSequence, string sequence, int? begin, int? end, int missedCleavages)
        {
            _fastaSequence = fastaSequence;

            Sequence = sequence;
            Begin = begin;
            End = end;
            MissedCleavages = missedCleavages;

            Validate();
        }

        public FastaSequence FastaSequence { get { return _fastaSequence; } }

        public string Sequence { get; private set; }
        public int? Begin { get; private set; }
        public int? End { get; private set; } // non-inclusive
        public int MissedCleavages { get; private set; }

        public int Length { get { return Sequence.Length; } }

        public char PrevAA
        {
            get
            {
                if (!Begin.HasValue)
                    return 'X';
                int begin = Begin.Value;
                return (begin == 0 ? '-' : _fastaSequence.Sequence[begin - 1]);
            }
        }

        public char NextAA
        {
            get
            {
                if (!End.HasValue)
                    return 'X';
                int end = End.Value;
                return (end == _fastaSequence.Sequence.Length ? '-' : _fastaSequence.Sequence[end]);
            }
        }

        public static int CompareGroups(TransitionGroupDocNode node1, TransitionGroupDocNode node2)
        {
            return CompareGroups(node1.TransitionGroup, node2.TransitionGroup);
        }

        public static int CompareGroups(TransitionGroup group1, TransitionGroup group2)
        {
            int chargeDiff = group1.PrecursorCharge - group2.PrecursorCharge;
            if (chargeDiff != 0)
                return chargeDiff;
            return group1.LabelType.CompareTo(group2.LabelType);
        }

        public IEnumerable<TransitionGroup> GetTransitionGroups(SrmSettings settings, ExplicitMods mods, bool useFilter)
        {
            IList<int> precursorCharges = settings.TransitionSettings.Filter.PrecursorCharges;
            if (!useFilter)
            {
                precursorCharges = new List<int>();
                for (int i = TransitionGroup.MIN_PRECURSOR_CHARGE; i < TransitionGroup.MAX_PRECURSOR_CHARGE; i++)
                    precursorCharges.Add(i);
            }

            TransitionInstrument instrument = settings.TransitionSettings.Instrument;

            var modSettings = settings.PeptideSettings.Modifications;

            double precursorMassLight = settings.GetPrecursorMass(IsotopeLabelType.light, Sequence, mods);
            var listPrecursorMasses = new List<KeyValuePair<IsotopeLabelType, double>>
                { new KeyValuePair<IsotopeLabelType, double>(IsotopeLabelType.light, precursorMassLight) };

            foreach (var typeMods in modSettings.GetHeavyModifications())
            {
                IsotopeLabelType labelType = typeMods.LabelType;
                double precursorMass = precursorMassLight;
                if (settings.HasPrecursorCalc(labelType, mods))
                    precursorMass = settings.GetPrecursorMass(labelType, Sequence, mods);

                listPrecursorMasses.Add(new KeyValuePair<IsotopeLabelType, double>(labelType, precursorMass));
            }

            foreach (int charge in precursorCharges)
            {
                if (useFilter && !settings.Accept(this, mods, charge))
                    continue;

                for (int i = 0; i < listPrecursorMasses.Count; i++)
                {
                    var pair = listPrecursorMasses[i];
                    IsotopeLabelType labelType = pair.Key;
                    double precursorMass = pair.Value;
                    // Only return a heavy group, if the precursor masses differ
                    // between the light and heavy calculators
                    if (i == 0 || precursorMass != precursorMassLight)
                    {
                        if (instrument.IsMeasurable(SequenceMassCalc.GetMZ(precursorMass, charge)))
                            yield return new TransitionGroup(this, charge, labelType);
                    }
                }
            }
        }

        public IEnumerable<PeptideDocNode> CreateDocNodes(SrmSettings settings, IPeptideFilter filter)
        {
            // Always return the unmodified peptide doc node first
            var nodePepUnmod = new PeptideDocNode(this, new TransitionGroupDocNode[0]);
            if (filter.Accept(this, null))
                yield return nodePepUnmod;

            // First build a list of the amino acids in this peptide which can be modified,
            // and the modifications which apply to them.
            List<KeyValuePair<IList<StaticMod>, int>> listListMods = null;

            var mods = settings.PeptideSettings.Modifications;
            // Nothing to do, if no variable mods in the document
            if (mods.HasVariableModifications)
            {
                // Enurate each amino acid in the sequence
                int len = Sequence.Length;
                for (int i = 0; i < len; i++)
                {
                    char aa = Sequence[i];
                    // Test each modification to see if it applies
                    foreach (var mod in mods.VariableModifications)
                    {
                        // If the modification does apply, store it in the list
                        if (mod.IsMod(aa, i, len))
                        {
                            if (listListMods == null)
                                listListMods = new List<KeyValuePair<IList<StaticMod>, int>>();
                            if (listListMods.Count == 0 || listListMods[listListMods.Count - 1].Value != i)
                                listListMods.Add(new KeyValuePair<IList<StaticMod>, int>(new List<StaticMod>(), i));
                            listListMods[listListMods.Count - 1].Key.Add(mod);
                        }
                    }
                }
            }

            // If not applicable modifications were found, return a single DocNode for the
            // peptide passed in
            if (listListMods == null)
                yield break;

            int maxModCount = Math.Min(settings.PeptideSettings.Modifications.MaxVariableMods, listListMods.Count);
            for (int modCount = 1; modCount <= maxModCount; modCount++)
            {
                var modeStateMachine = new VariableModStateMachine(nodePepUnmod, modCount, listListMods);
                foreach (var nodePep in modeStateMachine.GetPeptideNodes())
                {
                    if (filter.Accept(nodePep.Peptide, nodePep.ExplicitMods))
                        yield return nodePep;
                }
            }
        }

        /// <summary>
        /// State machine that provides a <see cref="IEnumerable{PeptideDocNode}"/> for
        /// enumerating all modified states of a peptide, given the peptide, a number of
        /// possible modifications, and the set of possible modifications.
        /// </summary>
        private sealed class VariableModStateMachine
        {
            private readonly PeptideDocNode _nodePepUnmod;
            private readonly int _modCount;
            private readonly IList<KeyValuePair<IList<StaticMod>, int>> _listListMods;

            /// <summary>
            /// Contains indexes into _listListMods specifying amino acids currently
            /// modified.
            /// </summary>
            private readonly int[] _arrayModIndexes1;

            /// <summary>
            /// Contains indexes into the static mod lists of _listListMods specifying
            /// which modification is currently applied to the amino acid specified
            /// by _arrayModIndexes1.
            /// </summary>
            private readonly int[] _arrayModIndexes2;

            /// <summary>
            /// Index to the currently active elements in _arrayModIndexes arrays.
            /// </summary>
            private int _cursorIndex;

            public VariableModStateMachine(PeptideDocNode nodePepUnmod, int modCount,
                IList<KeyValuePair<IList<StaticMod>, int>> listListMods)
            {
                _nodePepUnmod = nodePepUnmod;
                _modCount = modCount;
                _listListMods = listListMods;

                // Fill the mod indexes list with the first possible state
                _arrayModIndexes1 = new int[_modCount];
                for (int i = 0; i < modCount; i++)
                    _arrayModIndexes1[i] = i;
                // Second set of indexes start all zero initialized
                _arrayModIndexes2 = new int[_modCount];
                // Set the cursor to the last modification
                _cursorIndex = modCount - 1;
            }

            public IEnumerable<PeptideDocNode> GetPeptideNodes()
            {
                while (_cursorIndex >= 0)
                {
                    yield return CurrentPeptide;

                    if (!ShiftCurrentMod())
                    {
                        // Attempt to advance any mod to the left of the current mod
                        do
                        {
                            _cursorIndex--;
                        }
                        while (_cursorIndex >= 0 && !ShiftCurrentMod());

                        // If a mod was successfully advanced, reset all mods to its right
                        // and start over with them.
                        if (_cursorIndex >= 0)
                        {
                            for (int i = 1; i < _modCount - _cursorIndex; i++)
                            {
                                _arrayModIndexes1[_cursorIndex + i] = _arrayModIndexes1[_cursorIndex] + i;
                                _arrayModIndexes2[_cursorIndex + i] = 0;
                            }
                            _cursorIndex = _modCount - 1;
                        }
                    }
                }
            }

            private bool ShiftCurrentMod()
            {
                int modIndex = _arrayModIndexes1[_cursorIndex];
                if (_arrayModIndexes2[_cursorIndex] < _listListMods[modIndex].Key.Count - 1)
                {
                    // Shift the current amino acid through all possible modification states
                    _arrayModIndexes2[_cursorIndex]++;
                }
                else if (modIndex < _listListMods.Count - _modCount + _cursorIndex)
                {
                    // Shift the current modification through all possible positions
                    _arrayModIndexes1[_cursorIndex]++;
                    _arrayModIndexes2[_cursorIndex] = 0;
                }
                else
                {
                    // Current modification has seen all possible states
                    return false;
                }
                return true;
            }

            private PeptideDocNode CurrentPeptide
            {
                get
                {
                    var variableMods = new ExplicitMod[_modCount];
                    for (int i = 0; i < _modCount; i++)
                    {
                        var pair = _listListMods[_arrayModIndexes1[i]];
                        var mod = pair.Key[_arrayModIndexes2[i]];

                        variableMods[i] = new ExplicitMod(pair.Value, mod);
                    }
                    var explicitMods = new ExplicitMods(_nodePepUnmod.Peptide, variableMods,
                                                        new TypedExplicitModifications[0], true);
                    // Make a new copy of the peptid ID to give it a new GlobalIndex.
                    return new PeptideDocNode((Peptide)_nodePepUnmod.Peptide.Copy(), explicitMods);
                }
            }
        }

        private void Validate()
        {
            if (_fastaSequence == null)
            {
                if (Begin.HasValue || End.HasValue)
                    throw new InvalidDataException("Peptides without a protein sequence do not support the start and end properties.");

                // No FastaSequence checked the sequence, so check it hear.
                FastaSequence.ValidateSequence(Sequence);
            }
            else
            {
                // Otherwise, validate the peptide sequence against the group sequence
                if (!Begin.HasValue || !End.HasValue)
                    throw new InvalidDataException("Peptides from protein sequences must have start end end values.");
                if (0 > Begin.Value || End.Value > _fastaSequence.Sequence.Length)
                    throw new InvalidDataException("Peptide sequence exceeds the bounds of the protein sequence.");

                string sequenceCheck = _fastaSequence.Sequence.Substring(Begin.Value, End.Value - Begin.Value);
                if (!Equals(Sequence, sequenceCheck))
                {
                    throw new InvalidDataException(string.Format("The peptide sequence {0} does not agree with the protein sequence {1} at ({2}:{3}).",
                        Sequence, sequenceCheck, Begin.Value, End.Value));
                }
            }
            // CONSIDER: Validate missed cleavages some day?
        }

        #region object overrides

        public bool Equals(Peptide obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj._fastaSequence, _fastaSequence) &&
                Equals(obj.Sequence, Sequence) &&
                obj.Begin.Equals(Begin) &&
                obj.End.Equals(End) &&
                obj.MissedCleavages == MissedCleavages;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Peptide)) return false;
            return Equals((Peptide) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (_fastaSequence != null ? _fastaSequence.GetHashCode() : 0);
                result = (result*397) ^ Sequence.GetHashCode();
                result = (result*397) ^ (Begin.HasValue ? Begin.Value : 0);
                result = (result*397) ^ (End.HasValue ? End.Value : 0);
                result = (result*397) ^ MissedCleavages;
                return result;
            }
        }

        public override string ToString()
        {
            if (!Begin.HasValue)
            {
                if (MissedCleavages == 0)
                    return Sequence;
                else
                    return string.Format("{0} (missed {1})", Sequence, MissedCleavages);
            }
            else
            {
                string format = "{0}.{1}.{2} [{3}, {4}]";
                if (MissedCleavages > 0)
                    format = "{0}.{1}.{2} [{3}, {4}] (missed {5})";
                return string.Format(format, PrevAA, Sequence, NextAA,
                                     Begin.Value, End.Value - 1, MissedCleavages);
            }
        }

        #endregion
    }

    public sealed class PeptideModKey
    {
        public PeptideModKey(Peptide peptide, ExplicitMods modifications)
        {
            Peptide = peptide;
            Modifications = modifications;
        }

        private Peptide Peptide { get; set; }
        private ExplicitMods Modifications { get; set; }

        #region object overrides

        private bool Equals(PeptideModKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Peptide, Peptide) &&
                Equals(other.Modifications, Modifications);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(PeptideModKey)) return false;
            return Equals((PeptideModKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Peptide.GetHashCode() * 397) ^
                    (Modifications != null ? Modifications.GetHashCode() : 0);
            }
        }

        #endregion
    }
}