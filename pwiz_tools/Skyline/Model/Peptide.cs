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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class PeptideDocNode : DocNodeParent
    {
        public PeptideDocNode(Peptide id, TransitionGroupDocNode[] children)
            : this(id, null, Annotations.Empty, null, null, children, true)
        {
        }

// ReSharper disable SuggestBaseTypeForParameter
        public PeptideDocNode(Peptide id, int? rank, Annotations annotations, ExplicitMods mods,
                Results<PeptideChromInfo> results, TransitionGroupDocNode[] children, bool autoManageChildren)
// ReSharper restore SuggestBaseTypeForParameter
            : base(id, annotations, children, autoManageChildren)
        {
            ExplicitMods = mods;
            Rank = rank;
            Results = results;
        }

        public Peptide Peptide { get { return (Peptide)Id; } }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.peptide; } }

        public ExplicitMods ExplicitMods { get; private set; }

        public bool HasExplicitMods { get { return ExplicitMods != null; }}

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

        public float? GetMeasuredRatioToStandard(int i)
        {
            var result = GetSafeChromInfo(i);
            if (result == null)
                return null;
            return result[0].RatioToStandard;
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

        #region Property change methods

        public PeptideDocNode ChangeExplicitMods(ExplicitMods prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ExplicitMods = v, prop);
        }     

        public PeptideDocNode ChangeRank(int? prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Rank = v, prop);
        }

        public PeptideDocNode ChangeResults(Results<PeptideChromInfo> prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Results = v, prop);
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
                    ((TransitionGroupDocNode) node).TransitionGroup.LabelType != IsotopeLabelType.light);
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
                if (!ReferenceEquals(explicitMods.HeavyModifications, ExplicitMods.HeavyModifications))
                    diff = new SrmSettingsDiff(diff, SrmSettingsDiff.ALL);
                else if (!ReferenceEquals(explicitMods.StaticModifications, ExplicitMods.StaticModifications))
                    diff = new SrmSettingsDiff(diff, SrmSettingsDiff.PROPS);
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
                // Even with auto-select off, if there is no heavy mass calculator,
                // for this peptide, then it may not contain heavy precursors.
                if (diff.DiffTransitionGroups && nodeResult.HasHeavyTransitionGroups &&
                    !settingsNew.HasPrecursorCalc(IsotopeLabelType.heavy, explicitMods))
                {
                    IList<DocNode> childrenNew = new List<DocNode>();
                    foreach (TransitionGroupDocNode nodeGroup in nodeResult.Children)
                    {
                        if (nodeGroup.TransitionGroup.LabelType == IsotopeLabelType.light)
                            childrenNew.Add(nodeGroup);
                    }

                    nodeResult = (PeptideDocNode)ChangeChildren(childrenNew);
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
                        if (instrument.IsMeasurable(nodeChanged.PrecursorMz))
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
            var resultsCalc = new PeptideResultsCalculator();
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

            private int TransitionGroupCount { get; set; }

            public void AddGroupChromInfo(TransitionGroupDocNode nodeGroup)
            {
                TransitionGroupCount++;

                if (nodeGroup.HasResults)
                {
                    int countResults = nodeGroup.Results.Count;
                    while (_listResultCalcs.Count < countResults)
                        _listResultCalcs.Add(new PeptideChromInfoListCalculator(_listResultCalcs.Count));
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
            public PeptideChromInfoListCalculator(int resultsIndex)
            {
                ResultsIndex = resultsIndex;
                Calculators = new Dictionary<int, PeptideChromInfoCalculator>();
            }

            public int ResultsIndex { get; private set; }

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
                        calc = new PeptideChromInfoCalculator();
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
                        calc = new PeptideChromInfoCalculator();
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

                // Only add ratios to the internal standard transitions
                if (nodeTran.Transition.Group.LabelType == IsotopeLabelType.light)
                    return listInfo;

                var listInfoNew = new List<TransitionChromInfo>();
                foreach (var info in listInfo)
                {
                    PeptideChromInfoCalculator calc;
                    if (!Calculators.TryGetValue(info.FileIndex, out calc))
                        Debug.Assert(false);    // Should never happen
                    else
                    {
                        float? ratio = calc.CalcTransitionRatio(nodeTran);
                        listInfoNew.Add(Equals(ratio, info.Ratio) ?
                            info : info.ChangeRatio(ratio));
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

                // Only add ratios to the internal standard transitions
                if (nodeGroup.TransitionGroup.LabelType == IsotopeLabelType.light)
                    return listInfo;

                var listInfoNew = new List<TransitionGroupChromInfo>();
                foreach (var info in listInfo)
                {
                    PeptideChromInfoCalculator calc;
                    if (!Calculators.TryGetValue(info.FileIndex, out calc))
                        Debug.Assert(false);    // Should never happen
                    else
                    {
                        float? stdev;
                        float? ratio = calc.CalcTransitionGroupRatio(nodeGroup, out stdev);
                        listInfoNew.Add(Equals(ratio, info.Ratio) && Equals(stdev, info.RatioStdev)?
                            info : info.ChangeRatio(ratio, stdev));
                    }
                }
                if (ArrayUtil.ReferencesEqual(listInfo, listInfoNew))
                    return listInfo;
                return listInfoNew;
            }
        }

        private sealed class PeptideChromInfoCalculator
        {
            public PeptideChromInfoCalculator()
            {
                FileIndex = -1;
                TranRatiosToStandard = new Dictionary<TransitionKey, RatioToStandardCalculator>();
            }

            public int FileIndex { get; private set; }
            private double PeakCountRatioTotal { get; set; }
            private int ResultsCount { get; set; }
            private int RetentionTimesMeasured { get; set; }
            private double RetentionTimeTotal { get; set; }

            private Dictionary<TransitionKey, RatioToStandardCalculator> TranRatiosToStandard { get; set; }

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
                RatioToStandardCalculator ratioCalc;
                var key = new TransitionKey(nodeTran.Transition);
                if (!TranRatiosToStandard.TryGetValue(key, out ratioCalc))
                {
                    ratioCalc = new RatioToStandardCalculator();
                    TranRatiosToStandard.Add(key, ratioCalc);
                }
                ratioCalc.AddArea(nodeTran.Transition.Group.LabelType, info.Area);
            }

            public PeptideChromInfo CalcChromInfo(int transitionGroupCount)
            {
                if (ResultsCount == 0)
                    return null;

                float peakCountRatio = (float) (PeakCountRatioTotal/transitionGroupCount);

                float? retentionTime = null;
                if (RetentionTimesMeasured > 0)
                    retentionTime = (float) (RetentionTimeTotal/RetentionTimesMeasured);
                float? stdev;
                float? ratioToStandard = CalcTransitionGroupRatio(-1, out stdev);

                return new PeptideChromInfo(FileIndex, peakCountRatio, retentionTime, ratioToStandard);
            }

            public float? CalcTransitionRatio(TransitionDocNode nodeTran)
            {
                RatioToStandardCalculator ratioCalc;
                var key = new TransitionKey(nodeTran.Transition);
                if (!TranRatiosToStandard.TryGetValue(key, out ratioCalc))
                    return null;
                return ratioCalc.Ratio;
            }

            public float? CalcTransitionGroupRatio(TransitionGroupDocNode nodeGroup, out float? stdev)
            {
                return CalcTransitionGroupRatio(nodeGroup.TransitionGroup.PrecursorCharge, out stdev);
            }

            private float? CalcTransitionGroupRatio(int precursorCharge, out float? stdev)
            {
                double areaTotal = 0;
                double standardAreaTotal = 0;

                List<double> ratios = new List<double>();
                List<double> weights = new List<double>();

                foreach (var pair in TranRatiosToStandard)
                {
                    if (precursorCharge != -1 && pair.Key.PrecursorCharge != precursorCharge)
                        continue;

                    var ratioCalc = pair.Value;
                    // Skip values with no valid ratio
                    float? ratio = ratioCalc.Ratio;
                    if (ratio.HasValue)
                    {
                        areaTotal += ratioCalc.Area;
                        standardAreaTotal += ratioCalc.StandardArea;

                        ratios.Add(ratio.Value);
                        weights.Add(ratioCalc.StandardArea);
                    }
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
                double mean = areaTotal/standardAreaTotal;
                Debug.Assert(Math.Abs(mean - stats.Mean(statsW)) < 0.0001);
                // Make sure the value does not exceed the bounds of a float.
                return (float) Math.Min(float.MaxValue, Math.Max(float.MinValue, mean));
            }
        }

        private struct TransitionKey
        {
            private readonly IonType _ionType;
            private readonly int _ionOrdinal;
            private readonly int _charge;
            private readonly int _precursorCharge;

            public TransitionKey(Transition transition)
            {
                _ionType = transition.IonType;
                _ionOrdinal = transition.Ordinal;
                _charge = transition.Charge;
                _precursorCharge = transition.Group.PrecursorCharge;
            }

            public int PrecursorCharge { get { return _precursorCharge; } }

            #region object overrides

            private bool Equals(TransitionKey other)
            {
                return Equals(other._ionType, _ionType) &&
                    other._ionOrdinal == _ionOrdinal &&
                    other._charge == _charge &&
                    other._precursorCharge == _precursorCharge;
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
                    return result;
                }
            }

            #endregion
        }

        private sealed class RatioToStandardCalculator
        {
            public float Area { get; private set; }
            public float StandardArea { get; private set; }

            public float? Ratio
            {
                get
                {
                    if (Area != 0 && StandardArea != 0)
                        return Area/StandardArea;
                    return null;
                }
            }

            public void AddArea(IsotopeLabelType labelType, float? area)
            {
                if (!area.HasValue)
                    return;

                if (labelType == IsotopeLabelType.light)
                    Area += area.Value;
                else
                    StandardArea = area.Value;
            }
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
            return ((int)group1.LabelType) - ((int)group2.LabelType);
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

            bool heavy = (mods != null ? mods.HasHeavyModifications :
                settings.PeptideSettings.Modifications.HasHeavyImplicitModifications);

            double precursorMass = settings.GetPrecursorMass(IsotopeLabelType.light, Sequence, mods);
            double precursorMassHeavy = (heavy ? settings.GetPrecursorMass(IsotopeLabelType.heavy, Sequence, mods) : 0);

            foreach (int charge in precursorCharges)
            {
                if (useFilter && !settings.Accept(this, mods, charge))
                    continue;

                if (instrument.IsMeasurable(SequenceMassCalc.GetMZ(precursorMass, charge)))
                    yield return new TransitionGroup(this, charge, IsotopeLabelType.light);
                if (heavy)
                {
                    // Only return a heavy group, if the precursor masses differ
                    // between the light and heavy calculators
                    if (precursorMass != precursorMassHeavy &&
                            instrument.IsMeasurable(SequenceMassCalc.GetMZ(precursorMassHeavy, charge)))
                        yield return new TransitionGroup(this, charge, IsotopeLabelType.heavy);                    
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
}