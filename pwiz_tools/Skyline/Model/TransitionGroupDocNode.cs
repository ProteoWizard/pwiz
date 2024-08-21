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
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class TransitionGroupDocNode : DocNodeParent
    {
        public const int MIN_DOT_PRODUCT_TRANSITIONS = 2;
        public const int MIN_DOT_PRODUCT_MS1_TRANSITIONS = 2;

        public const int MIN_TREND_REPLICATES = 4;
        public const int MAX_TREND_REPLICATES = 6;

        /// <summary>
        /// General use constructor.  A call to <see cref="ChangeSettings"/> is expected before the
        /// node is put into real use in a document.
        /// </summary>
        /// <param name="id">The <see cref="TransitionGroup"/> identity for this node</param>
        /// <param name="children">A set of explicit children, or null if children should be auto-managed</param>
        /// <param name="explicitTransitionGroupValues">Optional values like ion mobility etc</param>
        public TransitionGroupDocNode(TransitionGroup id, TransitionDocNode[] children, ExplicitTransitionGroupValues explicitTransitionGroupValues = null)
            : this(id,
                   Annotations.EMPTY,
                   null,
                   null,
                   null,
                   explicitTransitionGroupValues ?? ExplicitTransitionGroupValues.EMPTY,
                   null,
                   children,
                   children == null)
        {
        }

        public TransitionGroupDocNode(TransitionGroup id,
                                      Annotations annotations,
                                      SrmSettings settings,
                                      ExplicitMods mods,
                                      SpectrumHeaderInfo libInfo,
                                      ExplicitTransitionGroupValues explicitValues,
                                      Results<TransitionGroupChromInfo> results,
                                      TransitionDocNode[] children,
                                      bool autoManageChildren)
            : base(id, annotations, children ?? new TransitionDocNode[0], autoManageChildren)
        {
            if (settings != null)
            {
                IsotopeDistInfo isotopeDist;
                TypedMass mass;
                PrecursorMz = new SignedMz(CalcPrecursorMZ(settings, mods, out isotopeDist, out mass), id.PrecursorAdduct.AdductCharge < 0);
                PrecursorMzMassType = mass.MassType;
                IsotopeDist = isotopeDist;
                RelativeRT = CalcRelativeRT(settings, mods);
            }
            else
            {
                PrecursorMz = SignedMz.ZERO;
            }
            LibInfo = libInfo;
            ExplicitValues = explicitValues ?? ExplicitTransitionGroupValues.EMPTY;
            Results = results;
        }

        private TransitionGroupDocNode(TransitionGroupDocNode group,
                                       double precursorMz,
                                       IsotopeDistInfo isotopeDist,
                                       RelativeRT relativeRT,
                                       IList<DocNode> children)
            : base(group.TransitionGroup, group.Annotations, children, group.AutoManageChildren)
        {
            PrecursorMz = new SignedMz(precursorMz, group.PrecursorCharge < 0);
            PrecursorMzMassType = group.PrecursorMzMassType;
            IsotopeDist = isotopeDist;
            RelativeRT = relativeRT;
            LibInfo = group.LibInfo;
            Results = group.Results;
            ExplicitValues = group.ExplicitValues ?? ExplicitTransitionGroupValues.EMPTY;
            PrecursorConcentration = group.PrecursorConcentration;
        }

        public TransitionGroup TransitionGroup { get { return (TransitionGroup) Id; }}

        public TransitionGroupDocNode CloneTransitionGroupId()
        {
            var newTransitionGroup = new TransitionGroup(TransitionGroup.Peptide,
                TransitionGroup.PrecursorAdduct,
                TransitionGroup.LabelType, true,
                TransitionGroup.DecoyMassShift);
            var newTransitions = Transitions.Select(t => t.ChangeTransitionGroup(newTransitionGroup)).ToList();
            return ChangeTransitionGroupId(newTransitionGroup, newTransitions);

        }

        [TrackChildren(ignoreName:true, defaultValues:typeof(DefaultValuesNullOrEmpty))]
        public IEnumerable<TransitionDocNode> Transitions { get { return Children.Cast<TransitionDocNode>(); } }

        public IEnumerable<TransitionDocNode> GetQuantitativeTransitions(SrmSettings settings)
        {
            return Transitions.Where(tran => tran.IsQuantitative(settings));
        }

        protected override IList<DocNode> OrderedChildren(IList<DocNode> children)
        {
            if (IsCustomIon && children.Count > 1 && !SrmDocument.IsConvertedFromProteomicTestDocNode(this))
            {
                // Enforce order that facilitates Isotope ratio calculation, especially in cases where all we have is mz
                return children.OrderBy(t => (TransitionDocNode)t, new TransitionDocNode.CustomIonEquivalenceComparer()).ToArray();
            }
            else
            {
                return children;
            }
        }

        /// <summary>
        /// Deep copy for use in small molecule user editing - we have to deep copy entire branches for changes to IDs
        /// </summary>
        /// <returns> Copy of transition group and its transitions, all with new IDs and backpointers</returns>
        public TransitionGroupDocNode UpdateSmallMoleculeTransitionGroup(Peptide parentNew, TransitionGroup groupNew, SrmSettings settings)
        {
            Assume.IsTrue(IsCustomIon);
            var children = new List<TransitionDocNode>();
            groupNew = groupNew ?? new TransitionGroup(parentNew ?? TransitionGroup.Peptide, TransitionGroup.PrecursorAdduct, TransitionGroup.LabelType, false, TransitionGroup.DecoyMassShift);
            var nodeGroupTemp = new TransitionGroupDocNode(groupNew, Annotations, settings, null, LibInfo, ExplicitValues, Results, null, false); // Just need this for the revised isotope distribution
            foreach (var nodeTran in Transitions)
            {
                var transition = nodeTran.Transition;
                var adduct = transition.IonType == IonType.precursor
                             ? groupNew.PrecursorAdduct : transition.Adduct;
                var molecule = transition.IonType == IonType.precursor
                             ? groupNew.CustomMolecule : transition.CustomIon;
                var tranNew = new Transition(groupNew, transition.IonType, transition.CleavageOffset,
                    transition.MassIndex, adduct, transition.DecoyMassShift, molecule);
                TypedMass moleculeMass;
                if (transition.IonType == IonType.precursor && nodeGroupTemp.IsotopeDist != null)
                {
                    var peakIndex = nodeGroupTemp.IsotopeDist.MassIndexToPeakIndex(transition.MassIndex);
                    if (peakIndex < 0 || peakIndex >= nodeGroupTemp.IsotopeDist.CountPeaks)
                    {
                        // Mass index is no longer valid: remove the transition
                        continue;
                    }
                    moleculeMass = nodeGroupTemp.IsotopeDist.GetMassI(transition.MassIndex);
                }
                else
                {
                    moleculeMass = nodeTran.GetMoleculeMass(molecule);
                }
                var nodeTranNew = new TransitionDocNode(tranNew, nodeTran.Annotations, nodeTran.Losses,
                    moleculeMass, nodeTran.QuantInfo, nodeTran.ExplicitValues, nodeTran.Results);
                children.Add(nodeTranNew);
            }
            return new TransitionGroupDocNode(groupNew, Annotations, settings, null, LibInfo, ExplicitValues, Results, children.ToArray(), AutoManageChildren);
        }

        public IEnumerable<TransitionDocNode> GetMsTransitions(bool fullScanMs)
        {
            if (fullScanMs)
            {
                foreach (var nodeTran in Transitions.Where(nodeTran => nodeTran.IsMs1))
                    yield return nodeTran;
            }
        }

        public IEnumerable<TransitionDocNode> GetMsMsTransitions(bool fullScanMs)
        {
            return fullScanMs ? Transitions.Where(nodeTran => !nodeTran.IsMs1) : Transitions;
        }

        public bool IsCustomIon { get { return TransitionGroup.IsCustomIon;  } }

        public CustomMolecule CustomMolecule
        {
            get { return TransitionGroup.CustomMolecule;  }
        }

        public LibKey GetLibKey(SrmSettings settings, PeptideDocNode nodePep)
        {
            if (IsCustomIon)
            {
                return new LibKey(nodePep.CustomMolecule.GetSmallMoleculeLibraryAttributes(),
                    PrecursorAdduct);
            }

            return new LibKey(settings.GetModifiedSequence(nodePep.Peptide.Target, LabelType, nodePep.ExplicitMods),
                PrecursorAdduct.AdductCharge);
        }

        /// <summary>
        /// // Gives list of precursors - formerly in TransitionGroupTreeNode.GetChoices
        /// </summary>
        public IList<DocNode> GetPrecursorChoices(SrmSettings settings, ExplicitMods mods, bool useFilter)
        {
            SpectrumHeaderInfo libInfo = null;
            var transitionRanks = new Dictionary<double, LibraryRankedSpectrumInfo.RankedMI>();
            GetLibraryInfo(settings, mods, useFilter, ref libInfo, transitionRanks);

            var listChoices = new List<DocNode>();
            foreach (TransitionDocNode nodeTran in GetTransitions(settings, mods,
                PrecursorMz, IsotopeDist, libInfo, transitionRanks, useFilter))
            {
                listChoices.Add(nodeTran);
            }
            return listChoices;
        }

        public bool IsLight { get { return TransitionGroup.LabelType.IsLight; } }

        public RelativeRT RelativeRT { get; private set; }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.precursor; } }

        public MassType PrecursorMzMassType { get; private set; } // What kind of mass was used to calculate Mz?
        public SignedMz PrecursorMz { get; private set; }

        public SpectrumClassFilter SpectrumClassFilter { get; private set; }

        [TrackChildren(defaultValues:typeof(DefaultValuesNullOrEmpty))]
        public IList<FilterClause> SpectrumFilter
        {
            get { return SpectrumClassFilter.Clauses; }
        }

        public TransitionGroupDocNode ChangeSpectrumClassFilter(SpectrumClassFilter spectrumClassFilter)
        {
            if (Equals(SpectrumClassFilter, spectrumClassFilter))
            {
                return this;
            }
            return ChangeProp(ImClone(this), im =>
            {
                im.SpectrumClassFilter = spectrumClassFilter;
            });
        }

        public PrecursorKey PrecursorKey
        {
            get
            {
                return new PrecursorKey(PrecursorAdduct, SpectrumClassFilter);
            }
        }
        public int PrecursorCharge { get { return TransitionGroup.PrecursorAdduct.AdductCharge; } }

        private class SmallMoleculeOnly : DefaultValues
        {
            public override bool IsDefault(object obj, object parentObject)
            {
                var docNode = (TransitionGroupDocNode)parentObject;
                return !docNode.IsCustomIon;
            }

            public override bool IgnoreIfDefault
            {
                get { return true; }
            }
        }

        [Track(defaultValues: typeof(SmallMoleculeOnly))]
        public Adduct PrecursorAdduct { get { return TransitionGroup.PrecursorAdduct; } }

        [Track(defaultValues: typeof(SmallMoleculeOnly))]
        public IsotopeLabelType LabelType
        {
            get { return TransitionGroup.LabelType; }
        }

        /// <summary>
        /// For transition lists with explicit values for CE, ion mobility
        /// </summary>
        [TrackChildren]
        public ExplicitTransitionGroupValues ExplicitValues { get; private set; }
        [Track(defaultValues: typeof(DefaultValuesNull))]
        public double? PrecursorConcentration { get; private set; }

        public Peptide Peptide { get { return TransitionGroup.Peptide; } }

        public bool IsDecoy { get { return TransitionGroup.DecoyMassShift.HasValue; } }

        public IsotopeDistInfo IsotopeDist { get; private set; }

        public bool HasIsotopeDist { get { return IsotopeDist != null; } }

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
                // They can, and it is a real problem, because it means this
                // ranking is not stable, like the others.  At least one fix
                // has been made in PeptideDocNode.ChangeSettings() to deal
                // with this.
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
                        if (result.IsEmpty)
                            continue;
                        foreach (var chromInfo in result)
                            yield return chromInfo;
                    }
                }
            }
        }

        // Return a collection of adducts which includes our precursor adduct, and any transition adducts, ordered by charge
        public IEnumerable<Adduct> InUseAdducts
        {
            get
            {
                var inUseAdducts = Children.Select(c => ((TransitionDocNode)c).Transition.Adduct).ToList();
                inUseAdducts.Add(PrecursorAdduct);
                return inUseAdducts.Distinct().OrderBy(a => Math.Abs(a.AdductCharge)).ThenBy(a => a.ToString());                
            }
        }

        public IEnumerable<TransitionGroupChromInfo> GetChromInfos(int? i)
        {
            if (!i.HasValue)
                return ChromInfos;
            return GetSafeChromInfo(i.Value);
        }

        public ChromInfoList<TransitionGroupChromInfo> GetSafeChromInfo(int i)
        {
            return (HasResults && Results.Count > i ? Results[i] : default(ChromInfoList<TransitionGroupChromInfo>));
        }

        public TransitionGroupChromInfo GetChromInfo(int resultsIndex, ChromFileInfoId chromFileInfoId)
        {
            return GetSafeChromInfo(resultsIndex).FirstOrDefault(chromInfo =>
                chromFileInfoId == null || ReferenceEquals(chromFileInfoId, chromInfo.FileId));
        }


        public TransitionGroupChromInfo GetChromInfoEntry(int i)
        {
            var result = GetSafeChromInfo(i);
            // CONSIDER: Also specify the file index and/or optimization step?
            foreach (var chromInfo in result)
            {
                if (chromInfo.OptimizationStep == 0)
                    return chromInfo;
            }
            return null;
        }

        public int? GetRank(TransitionGroupDocNode nodeGroupPrimary, TransitionDocNode nodeTran, int? replicateIndex)
        {
            if (nodeGroupPrimary == null || ReferenceEquals(this, nodeGroupPrimary))
                return nodeTran.GetRank(replicateIndex, HasResultRanks);

            var nodeTranEquivalent = nodeGroupPrimary.FindEquivalentTransition(nodeTran);
            if (nodeTranEquivalent == null)
                return null;
            return nodeTranEquivalent.GetRank(replicateIndex, nodeGroupPrimary.HasResultRanks);
        }

        public bool HasLibRanks
        {
            get { return Transitions.Any(nodeTran => nodeTran.HasLibInfo && nodeTran.LibInfo.Rank > 0); }
        }

        public bool HasResultRanks
        {
            get { return Transitions.Any(nodeTran => nodeTran.ResultsRank.HasValue); }
        }

        public bool HasReplicateRanks(int? replicateIndex)
        {
            if (!replicateIndex.HasValue)
                return HasResultRanks;
            return Transitions.Any(nodeTran => nodeTran.GetPeakRank(replicateIndex.Value).HasValue);
        }

        public float? GetPeakCountRatio(int i)
        {
            if (i == -1)
                return AveragePeakCountRatio;

            // CONSIDER: Also specify the file index?
            var result = GetSafeChromInfo(i);
            if (result.IsEmpty)
                return null;
            return result.GetAverageValue(chromInfo => chromInfo.OptimizationStep == 0
                                                              ? chromInfo.PeakCountRatio
                                                              : (float?)null);
        }

        public float? AveragePeakCountRatio
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.OptimizationStep == 0
                                                              ? chromInfo.PeakCountRatio
                                                              : (float?) null);
            }
        }

        public float? GetPeakArea(int i, double? qvalueCutoff = null)
        {
            if (i == -1)
                return AveragePeakArea;

            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            if (qvalueCutoff.HasValue)
            {
                if (!(chromInfo.QValue.HasValue && chromInfo.QValue.Value < qvalueCutoff.Value))
                    return null;
            }
            return chromInfo.Area;
        }

        public float? AveragePeakArea
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.OptimizationStep != 0
                                                              ? null
                                                              : chromInfo.Area);
            }
        }

        public float? GetIsotopeDotProduct(int i)
        {
            if (i == -1)
                return AverageIsotopeDotProduct;

            // CONSIDER: Also specify the file index?
            var result = GetSafeChromInfo(i);
            if (result.IsEmpty)
                return null;
            return result.GetAverageValue(chromInfo => chromInfo.OptimizationStep == 0 && chromInfo.Area.HasValue
                                                              ? chromInfo.IsotopeDotProduct
                                                              : null);
        }

        public float? AverageIsotopeDotProduct
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.OptimizationStep == 0 && chromInfo.Area.HasValue
                                                              ? chromInfo.IsotopeDotProduct
                                                              : null);
            }
        }

        public float? GetLibraryDotProduct(int i)
        {
            if (i == -1)
                return AverageLibraryDotProduct;

            // CONSIDER: Also specify the file index?
            var result = GetSafeChromInfo(i);
            if (result.IsEmpty)
                return null;
            return result.GetAverageValue(chromInfo => chromInfo.OptimizationStep == 0 && chromInfo.Area.HasValue
                                                              ? chromInfo.LibraryDotProduct
                                                              : null);
        }

        public float? AverageLibraryDotProduct
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.OptimizationStep == 0 && chromInfo.Area.HasValue
                                                              ? chromInfo.LibraryDotProduct
                                                              : null);
            }
        }

        public class ScheduleTimes        
        {
            public float CenterTime { get; set; }
            public float Width { get; set; }
            public int? ReplicateNum { get; set; }
        }

        // TODO: Test this code.
        private ScheduleTimes GetSchedulingTrendTimes(int replicateNum)
        {
            int valCount = 0;
            double valTotal = 0;
            ScheduleTimes scheduleTimes = new ScheduleTimes();

            // Use 6 replicates if results have at least 6 replicates
            // otherwise use the number of Results available (at least 4)
            double[] centerTimes = new double[ Math.Min(Results.Count, MAX_TREND_REPLICATES) ];
            double[] replicateNums = new double[centerTimes.Length];
            double maxPeakWindowRange = 0;
            for (int i = 0; i < Results.Count; i++)
            {
                var result = Results[i];
                if (result.IsEmpty)
                    continue;

                foreach (var chromInfo in result)
                {
                    if (chromInfo == null ||
                            !chromInfo.StartRetentionTime.HasValue ||
                            !chromInfo.EndRetentionTime.HasValue)
                        return null;
                    // Make an array of the last 4 or 6 (depending on data available) center Times to use for linear regression
                    if (i >= Results.Count - centerTimes.Length)
                    {
                        valTotal += (chromInfo.StartRetentionTime.Value + chromInfo.EndRetentionTime.Value) / 2.0;
                        valCount++;
                        // TODO: This will only work, if all of the final replicates have data.
                        int timesIndex = i - Results.Count + centerTimes.Length;
                        centerTimes[timesIndex] = (float)(valTotal / valCount);
                        replicateNums[timesIndex] = timesIndex;
                    }
                    maxPeakWindowRange = Math.Max(maxPeakWindowRange,
                                                  chromInfo.EndRetentionTime.Value -
                                                  chromInfo.StartRetentionTime.Value);
                }
            }
            Statistics statCenterTimes = new Statistics(centerTimes);
            Statistics statReplicateNums = new Statistics(replicateNums);
            double centerTimesSlope = statCenterTimes.Slope(statReplicateNums);
            double centerTimesIntercept = statCenterTimes.Intercept(statReplicateNums);
            var centerTimesResiduals = statCenterTimes.Residuals(statReplicateNums);
            double centerTimesStdDev = centerTimesResiduals.StdDev();
            double centerFirstPredict = centerTimesIntercept + (centerTimes.Length + 1) * centerTimesSlope + maxPeakWindowRange / 2 + centerTimesStdDev;
            double centerLastPredict = centerFirstPredict + replicateNum * centerTimesSlope - maxPeakWindowRange / 2 - centerTimesStdDev;
            scheduleTimes.CenterTime = (float) (centerTimesIntercept + centerTimesSlope*replicateNums.Length);
            scheduleTimes.Width = (float) Math.Abs(centerFirstPredict - centerLastPredict);
            return scheduleTimes;
        }

        /// <summary>
        /// Ensures that all precursors with matching retention time get the same
        /// scheduling peak times.
        /// </summary>
        public static ScheduleTimes GetSchedulingPeakTimes(IEnumerable<TransitionGroupDocNode> schedulingGroups,
            SrmDocument document, ExportSchedulingAlgorithm algorithm, int? replicateNum, Predicate<ChromatogramSet> replicateFilter)
        {
            var arrayScheduleTimes = schedulingGroups.Select(nodeGroup =>
                    nodeGroup.GetSchedulingPeakTimes(document, algorithm, replicateNum, replicateFilter))
                .Where(scheduleTimes => scheduleTimes != null)
                .ToArray();
            if (arrayScheduleTimes.Length < 2)
                return arrayScheduleTimes.FirstOrDefault();

            
            // If multiple matching times, prefer any that matched the specified replicate
            if (replicateNum.HasValue)
            {
                var matchingTimes = arrayScheduleTimes.Where(t => t.ReplicateNum == replicateNum).ToArray();
                if (matchingTimes.Length > 0)
                    arrayScheduleTimes = matchingTimes;
            }
            return new ScheduleTimes
                       {
                           CenterTime = arrayScheduleTimes.Average(st => st.CenterTime)
                       };
        }

        public ScheduleTimes GetSchedulingPeakTimes(SrmDocument document, ExportSchedulingAlgorithm algorithm, int? replicateNum, Predicate<ChromatogramSet> replicateFilter)
        {
            if (!HasResults)
                return null;

            int valCount = 0;
            double valTotal = 0;
            int? valReplicate = null;
            // Try to get a scheduling time from non-optimization data, unless this
            // document contains only optimization data.  This is because optimization
            // data may have been taken under completely different chromatographic
            // conditions.
            int valCountOpt = 0;
            double valTotalOpt = 0;
            int? valReplicateOpt = null;
            ScheduleTimes scheduleTimes = new ScheduleTimes();

            // CONSIDER:  Need to set a width for algorithms other than trends?
            if (!replicateNum.HasValue || algorithm == ExportSchedulingAlgorithm.Average)
            {
                // Sum the center times for averaging
                for (int i = 0; i < Results.Count; i++)
                {
                    var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[i];
                    if (replicateFilter != null && !replicateFilter(chromatogramSet))
                        continue;

                    if (chromatogramSet.OptimizationFunction == null)
                        AddSchedulingTimes(i, ref valCount, ref valTotal);
                    else
                        AddSchedulingTimes(i, ref valCountOpt, ref valTotalOpt);
                }
            }
            else if (algorithm == ExportSchedulingAlgorithm.Single)
            {
                // Try using the specified index
                if (replicateNum.Value < Results.Count)
                {
                    AddSchedulingTimes(replicateNum.Value, ref valCount, ref valTotal);
                }

                // If no usable peak found for the specified replicate, try to find a
                // usable peak in other replicates.
                if (valCount != 0)
                    valReplicate = replicateNum.Value;
                else
                {
                    // Iterate from end to give replicates closer to the end (more recent)
                    // higher priority over those closer to the beginning, when they are
                    // equally distant from the original replicate.
                    int deltaBest = int.MaxValue;
                    int deltaBestOpt = int.MaxValue;
                    for (int i = Results.Count - 1; i >= 0; i--)
                    {
                        int deltaRep = Math.Abs(i - replicateNum.Value);
                        var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[i];
                        int deltaBestCompare = (chromatogramSet.OptimizationFunction == null ? deltaBest : deltaBestOpt);
                        // If the delta of this replicate from the chosen replicate is greater
                        // than the existing closest replicate, skip it.
                        if (deltaRep >= deltaBestCompare)
                            continue;

                        int valCountTmp = 0;
                        double valTotalTmp = 0;
                        AddSchedulingTimes(i, ref valCountTmp, ref valTotalTmp);
                        if (valCountTmp == 0)
                            continue;

                        // Make any found peak for this closer replicate the current best.
                        if (chromatogramSet.OptimizationFunction == null)
                        {
                            valCount = valCountTmp;
                            valTotal = valTotalTmp;
                            valReplicate = i;
                            deltaBest = deltaBestCompare;
                        }
                        else
                        {
                            valCountOpt = valCountTmp;
                            valTotalOpt = valTotalTmp;
                            valReplicateOpt = i;
                            deltaBestOpt = deltaBestCompare;
                        }
                    }
                }
            }
            else // Trends Option
            {
                return GetSchedulingTrendTimes(replicateNum.Value);
            }

            // If possible return the scheduling time based on non-optimization data.
            if (valCount != 0)
            {
                scheduleTimes.CenterTime = (float)(valTotal/valCount);
                scheduleTimes.ReplicateNum = valReplicate;
                return scheduleTimes;
            }
            // If only optimization was found, then use it.
            else if (valTotalOpt != 0)
            {
                scheduleTimes.CenterTime = (float) (valTotalOpt/valCountOpt);
                scheduleTimes.ReplicateNum = valReplicateOpt;
                return scheduleTimes;
            }
            // No usable data at all.
            return null;
        }

        private void AddSchedulingTimes(int replicateIndex, ref int valCount, ref double valTotal)
        {
            var result = Results[replicateIndex];
            if (result.IsEmpty)
                return;

            foreach (var chromInfo in Results[replicateIndex])
            {
//                double? schedulingTime = GetCenterTime(chromInfo);
                double? schedulingTime = GetRetentionTime(chromInfo);
                if (!schedulingTime.HasValue)
                    continue;

                valTotal += schedulingTime.Value;
                valCount++;
            }            
        }

        public static double? GetCenterTime(TransitionGroupChromInfo chromInfo)
        {
            if (chromInfo == null ||
//                            chromInfo.PeakCountRatio < 0.5 || - caused problems
                    !chromInfo.StartRetentionTime.HasValue ||
                    !chromInfo.EndRetentionTime.HasValue)
                return null;
            return (chromInfo.StartRetentionTime.Value + chromInfo.EndRetentionTime.Value) / 2.0;            
        }

        public static double? GetRetentionTime(TransitionGroupChromInfo chromInfo)
        {
            return chromInfo != null ? chromInfo.RetentionTime : null;
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

        public bool IsUserModified
        {
            get
            {
                if (!Annotations.IsEmpty)
                    return true;
                if (HasResults && Results.SelectMany(l => l)
                                            .Contains(chromInfo => chromInfo.IsUserModified))
                    return true;
                return Children.Cast<TransitionDocNode>().Contains(nodeTran => nodeTran.IsUserModified);
            }
        }

        private double CalcPrecursorMZ(SrmSettings settings, ExplicitMods mods, out IsotopeDistInfo isotopeDist, out TypedMass mass)
        {
            if (mods != null && mods.HasCrosslinks)
            {
                return CalcCrosslinkedPrecursorMz(settings, mods, out isotopeDist, out mass);
            }

            return CalcSelfPrecursorMZ(settings, mods, out isotopeDist, out mass);
        }


        private double CalcSelfPrecursorMZ(SrmSettings settings, ExplicitMods mods, out IsotopeDistInfo isotopeDist, out TypedMass mass)
        {
            var seq = TransitionGroup.Peptide.Target;
            var adduct = TransitionGroup.PrecursorAdduct;
            IsotopeLabelType labelType = TransitionGroup.LabelType;
            ParsedMolecule isotopicFormula = null;
            double mz;
            IPrecursorMassCalc calc;
            if (IsCustomIon)
            {
                var labelTypeForCalc =  IsotopeLabelType.light; // Don't need to look up the isotope, if we have one it's embedded in the adduct description
                calc = settings.GetPrecursorCalc(labelTypeForCalc, mods);
                var typedMods = settings.PeptideSettings.Modifications.GetModificationsByName(labelType.Name);
                mass = calc.GetPrecursorMass(CustomMolecule, typedMods, adduct, out isotopicFormula); // Mass including effect of isotopes (incl. any adduct isotopes M<isotopes>+<atoms>), but not the adduct atoms (M+<atoms>) themselves
                mz = adduct.MzFromNeutralMass(mass);
            }
            else
            {
                calc = settings.GetPrecursorCalc(labelType, mods);
                mass = calc.GetPrecursorMass(seq);
                mz = SequenceMassCalc.GetMZ(mass, adduct) + 
                     SequenceMassCalc.GetPeptideInterval(TransitionGroup.DecoyMassShift);
                if (TransitionGroup.DecoyMassShift.HasValue)
                    mass = new TypedMass(SequenceMassCalc.GetMH(mz, adduct.AdductCharge), calc.MassType);
            }

            isotopeDist = null;
            var fullScan = settings.TransitionSettings.FullScan;
            if (fullScan.IsHighResPrecursor)
            {
                MassDistribution massDist;
                if (!TransitionGroup.IsCustomIon)
                {
                    massDist = calc.GetMzDistribution(seq, adduct, fullScan.IsotopeAbundances);
                    if (TransitionGroup.DecoyMassShift.HasValue)
                        massDist = ShiftMzDistribution(massDist, TransitionGroup.DecoyMassShift.Value);
                }
                else if (isotopicFormula != null)
                {
                    massDist = calc.GetMZDistribution(isotopicFormula.GetMoleculeMassOffset(), adduct, fullScan.IsotopeAbundances);
                }
                else
                {
                    massDist = calc.GetMZDistributionSinglePoint(mz);
                }

                isotopeDist = GetIsotopeDistInfo(settings, massDist, mass);
            }
            return mz;
        }

        private double CalcCrosslinkedPrecursorMz(SrmSettings settings, ExplicitMods mods,
            out IsotopeDistInfo isotopeDist, out TypedMass mass)
        {
            var crosslinkBuilder = new CrosslinkBuilder(settings, TransitionGroup.Peptide, mods, LabelType);
            MassType massType = settings.TransitionSettings.Prediction.PrecursorMassType;
            mass = crosslinkBuilder.GetPrecursorMass(massType);
            Assume.IsFalse(mass.IsMassH());
            if (settings.TransitionSettings.FullScan.IsHighResPrecursor)
            {
                double decoyMassShift = 0;
                if (TransitionGroup.DecoyMassShift.HasValue)
                {
                    decoyMassShift = SequenceMassCalc.GetPeptideInterval(TransitionGroup.DecoyMassShift.Value);
                }
                isotopeDist = crosslinkBuilder.GetPrecursorIsotopeDistInfo(PrecursorAdduct, decoyMassShift);
            }
            else
            {
                isotopeDist = null;
            }

            return (mass / PrecursorCharge) + BioMassCalc.MassProton;
        }

        private IsotopeDistInfo GetIsotopeDistInfo(SrmSettings settings, MassDistribution massDist, TypedMass monoMassH)
        {
            return IsotopeDistInfo.MakeIsotopeDistInfo(massDist, monoMassH, PrecursorAdduct, settings.TransitionSettings.FullScan);
        }

        public ParsedMolecule GetNeutralFormula(SrmSettings settings, ExplicitMods mods)
        {
            if (IsCustomIon)
            {
                return CustomMolecule.ParsedMolecule;
            }
            IPrecursorMassCalc massCalc = settings.GetPrecursorCalc(LabelType, mods);
            var moleculeMassOffset = massCalc.GetMolecularFormula(Peptide.Sequence);
            moleculeMassOffset = moleculeMassOffset.Plus((mods?.CrosslinkStructure ?? CrosslinkStructure.EMPTY)
                .GetNeutralFormula(settings, LabelType));
            
            return ParsedMolecule.Create(moleculeMassOffset);
        }

        private static MassDistribution ShiftMzDistribution(MassDistribution massDist, int massShift)
        {
            double shift = SequenceMassCalc.GetPeptideInterval(massShift);
            // Use "OffsetAndDivide" to shift the mass distribution without reapplying binning.
            return massDist.OffsetAndDivide(shift, 1);
        }

        private RelativeRT CalcRelativeRT(SrmSettings settings, ExplicitMods mods)
        {
            return settings.GetRelativeRT(TransitionGroup.LabelType, TransitionGroup.Peptide.Target, mods);
        }

        public TransitionGroupDocNode ChangePrecursorMz(SrmSettings settings, ExplicitMods mods)
        {
            return ChangeProp(ImClone(this), im =>
            {
                IsotopeDistInfo isotopeDist;
                TypedMass mass;
                im.PrecursorMz = new SignedMz(CalcPrecursorMZ(settings, mods, out isotopeDist, out mass), im.PrecursorCharge < 0);
                im.PrecursorMzMassType = mass.MassType;
                // Preserve reference equality, if no change to isotope peaks
                Helpers.AssignIfEquals(ref isotopeDist, IsotopeDist);
                im.IsotopeDist = isotopeDist;
            });
        }

        public TransitionGroupDocNode ChangePrecursorConcentration(double? precursorConcentration)
        {
            return ChangeProp(ImClone(this), im => im.PrecursorConcentration = precursorConcentration);
        }

        public TransitionGroupDocNode ChangePeptide(Peptide peptide)
        {
            var newId = new TransitionGroup(peptide, TransitionGroup.PrecursorAdduct, TransitionGroup.LabelType, true,
                TransitionGroup.DecoyMassShift);
            return ChangeTransitionGroupId(newId, Transitions.Select(t => t.ChangeTransitionGroup(newId)));
        }

        public TransitionGroupDocNode ChangeTransitionGroupId(TransitionGroup newId, IEnumerable<TransitionDocNode> newTransitions)
        {
            var node = (TransitionGroupDocNode)ChangeId(newId);
            node = (TransitionGroupDocNode)node.ChangeChildren(newTransitions.Cast<DocNode>().ToList());
            return node;
        }

        public TransitionGroupDocNode ChangeSettings(SrmSettings settingsNew, PeptideDocNode nodePep, ExplicitMods mods, SrmSettingsDiff diff)
        {
            double precursorMz = PrecursorMz;
            IsotopeDistInfo isotopeDist = IsotopeDist;
            RelativeRT relativeRT = RelativeRT;
            SpectrumHeaderInfo libInfo = LibInfo;
            bool dotProductChange = false;
            var transitionRanks = new Dictionary<double, LibraryRankedSpectrumInfo.RankedMI>();
            if (diff.DiffTransitionGroupProps)
            {
                precursorMz = CalcPrecursorMZ(settingsNew, mods, out isotopeDist, out _);
                // Preserve reference equality if no change
                Helpers.AssignIfEquals(ref isotopeDist, IsotopeDist);
                relativeRT = CalcRelativeRT(settingsNew, mods);
            }

            bool autoSelectTransitions = diff.DiffTransitions &&
                                         settingsNew.TransitionSettings.Filter.AutoSelect && AutoManageChildren;

            if (!IsDecoy && (diff.DiffTransitionGroupProps || diff.DiffTransitions || diff.DiffTransitionProps))
            {
                // Skip transition ranking, if only transition group properties changed
                var transitionRanksLib = transitionRanks;
                if (!diff.DiffTransitions && !diff.DiffTransitionProps)
                    transitionRanksLib = null;
                // TODO: Use a TransitionCreationContext object instead, and defer ranking until it is needed
                //       Otherwise, this can cause a lot of unnecessary work loading MS1 filtering documents.
                // If transitions are not changing, then it is necessary to get all rankings,
                // since any group may contain reranked transitions
                GetLibraryInfo(settingsNew, mods, autoSelectTransitions, ref libInfo, transitionRanksLib);
            }

            CrosslinkBuilder crosslinkBuilder = null;

            TransitionGroupDocNode nodeResult = this;
            if (autoSelectTransitions)
            {
                IList<DocNode> childrenNew = new List<DocNode>();
                // TODO: Use TransitionLossKey
                Dictionary<TransitionLossKey, DocNode> mapIdToChild = CreateTransitionLossToChildMap();
                foreach (TransitionDocNode nodeTran in GetTransitions(settingsNew, mods,
                        precursorMz, isotopeDist, libInfo, transitionRanks, true))
                {
                    TransitionDocNode nodeTranResult;

                    DocNode existing;
                    // Add values that existed before the change.
                    if (mapIdToChild.TryGetValue(nodeTran.Key(this), out existing))
                    {
                        nodeTranResult = (TransitionDocNode) existing;
                        if (diff.DiffTransitionProps)
                        {
                            var tran = nodeTranResult.Transition;
                            var annotations = nodeTranResult.Annotations;
                            var explicitValues = nodeTranResult.ExplicitValues;
                            var losses = nodeTran.Losses;
                            TypedMass massH = settingsNew.RecalculateTransitionMass(mods, nodeTran, isotopeDist);
                            var quantInfo = TransitionDocNode.TransitionQuantInfo
                                .GetTransitionQuantInfo(nodeTranResult.ComplexFragmentIon, isotopeDist, Transition.CalcMass(massH, losses), transitionRanks)
                                .UseValuesFrom(nodeTranResult.QuantInfo);
                            if (!ReferenceEquals(quantInfo.LibInfo, nodeTranResult.LibInfo))
                                dotProductChange = true;
                            var results = nodeTranResult.Results;
                            if (mods != null && mods.HasCrosslinks)
                            {
                                crosslinkBuilder ??= new CrosslinkBuilder(settingsNew, TransitionGroup.Peptide, mods,
                                    LabelType);

                                nodeTranResult = crosslinkBuilder.MakeTransitionDocNode(
                                    nodeTran.ComplexFragmentIon, isotopeDist, annotations, quantInfo,
                                    explicitValues, results);
                            }
                            else
                            {
                                nodeTranResult = new TransitionDocNode(tran, annotations, losses,
                                    massH, quantInfo, explicitValues, results);
                            }

                            // Reuse the object "existing" if it's the same as nodeTranResult
                            if (Equals(nodeTranResult, existing))
                            {
                                // But be careful of the fact that "TransitionLosses.Equals" only compares the masses,
                                // so also make sure that the TransitionLoss objects in the list are the same
                                if (Equals(nodeTranResult.Losses?.Losses, ((TransitionDocNode)existing).Losses?.Losses))
                                {
                                    nodeTranResult = (TransitionDocNode) existing;
                                }
                            }
                        }
                    }
                    // Add the new node
                    else
                    {
                        nodeTranResult = nodeTran;
                    }

                    if (nodeTranResult != null)
                    {
                        Assume.IsTrue(settingsNew.TransitionSettings.Instrument.IsMeasurable(nodeTranResult.Mz, precursorMz));
                        childrenNew.Add(nodeTranResult);
                    }
                }

                if (!ArrayUtil.ReferencesEqual(childrenNew, Children))
                    nodeResult = new TransitionGroupDocNode(this, precursorMz, isotopeDist, relativeRT, childrenNew)
                        .ChangeSpectrumClassFilter(SpectrumClassFilter);
                else
                {
                    if (precursorMz != PrecursorMz || !Equals(isotopeDist, IsotopeDist) || relativeRT != RelativeRT)
                        nodeResult = new TransitionGroupDocNode(this, precursorMz, isotopeDist, relativeRT, Children)
                            .ChangeSpectrumClassFilter(SpectrumClassFilter);
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
                    if (nodePep.HasExplicitMods && nodePep.ExplicitMods.HasNeutralLosses)
                    {
                        modsLossNew = modsLossNew
                            .Union(nodePep.ExplicitMods.NeutralLossModifications.Select(m => m.Modification))
                            .ToArray();
                    }
                    IList<DocNode> childrenNew = new List<DocNode>();
                    foreach (TransitionDocNode nodeTransition in nodeResult.Children)
                    {
                        if (nodeTransition.IsLossPossible(modsNew.MaxNeutralLosses, modsLossNew) &&
                            settingsNew.TransitionSettings.Filter.IsAvailableReporterIon(nodeTransition) &&
                            settingsNew.TransitionSettings.Instrument.IsMeasurable(nodeTransition.Mz, precursorMz))
                            childrenNew.Add(nodeTransition);
                    }

                    nodeResult = (TransitionGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
                }

                if (diff.DiffTransitionProps)
                {
                    IList<DocNode> childrenNew = new List<DocNode>(nodeResult.Children.Count);

                    // Enumerate the nodes making necessary changes.
                    foreach (TransitionDocNode nodeTransition in nodeResult.Children)
                    {
                        var tran = nodeTransition.Transition;
                        var losses = nodeTransition.Losses;
                        MassType massType = settingsNew.TransitionSettings.Prediction.FragmentMassType;
                        if (losses != null && massType != losses.MassType)
                            losses = losses.ChangeMassType(massType);
                        var annotations = nodeTransition.Annotations;   // Don't lose annotations
                        var explicitValues = nodeTransition.ExplicitValues;
                        var results = nodeTransition.Results;           // Results changes happen later
                        // Discard isotope transitions which are no longer valid
                        if (!TransitionDocNode.IsValidIsotopeTransition(tran, isotopeDist))
                            continue;
                        var massH = settingsNew.RecalculateTransitionMass(mods, nodeTransition, isotopeDist);
                        var quantInfo = TransitionDocNode.TransitionQuantInfo.GetTransitionQuantInfo(nodeTransition.ComplexFragmentIon, isotopeDist,
                            Transition.CalcMass(massH, losses), transitionRanks).UseValuesFrom(nodeTransition.QuantInfo);
                        if (!ReferenceEquals(quantInfo.LibInfo, nodeTransition.LibInfo))
                            dotProductChange = true;

                        // Avoid overwriting valid transition lib info before the libraries are loaded or for decoys
                        if (libInfo != null && quantInfo.LibInfo == null && (IsDecoy || !settingsNew.PeptideSettings.Libraries.IsLoaded))
                            quantInfo = quantInfo.ChangeLibInfo(nodeTransition.LibInfo);
                        TransitionDocNode nodeNew;
                        if (mods != null && mods.HasCrosslinks)
                        {
                            crosslinkBuilder = crosslinkBuilder ??
                                               new CrosslinkBuilder(settingsNew, TransitionGroup.Peptide, mods,
                                                   LabelType);

                            nodeNew = crosslinkBuilder.MakeTransitionDocNode(nodeTransition.ComplexFragmentIon, isotopeDist,
                                nodeTransition.Annotations, quantInfo, explicitValues, results);
                        }
                        else
                        {
                            nodeNew = new TransitionDocNode(tran, annotations, losses,
                                massH, quantInfo, explicitValues, results);
                        }

                        Helpers.AssignIfEquals(ref nodeNew, nodeTransition);
                        if (settingsNew.TransitionSettings.Instrument.IsMeasurable(nodeNew.Mz, precursorMz))
                            childrenNew.Add(nodeNew);
                    }

                    // Change as little as possible
                    if (!ArrayUtil.ReferencesEqual(childrenNew, Children))
                        nodeResult = new TransitionGroupDocNode(nodeResult, precursorMz, isotopeDist, relativeRT, childrenNew)
                            .ChangeSpectrumClassFilter(SpectrumClassFilter);
                    else if (precursorMz != PrecursorMz || !Equals(isotopeDist, IsotopeDist) || relativeRT != RelativeRT)
                        nodeResult = new TransitionGroupDocNode(nodeResult, precursorMz, isotopeDist, relativeRT, Children)
                            .ChangeSpectrumClassFilter(SpectrumClassFilter);
                }
                else if (diff.DiffTransitionGroupProps)
                {
                    nodeResult = new TransitionGroupDocNode(nodeResult, precursorMz, isotopeDist, relativeRT, Children)
                        .ChangeSpectrumClassFilter(SpectrumClassFilter);
                }
            }

            // One final check for a library info change
            bool libInfoChange = !Equals(libInfo, nodeResult.LibInfo);
            if (libInfoChange)
            {
                nodeResult = nodeResult.ChangeLibInfo(libInfo);
                dotProductChange = true;
            }

            // A change in the precursor m/z may impact which results match this node
            // Or if the dot-product may need to be recalculated
            if (diff.DiffResults || ChangedResults(nodeResult) || precursorMz != PrecursorMz || dotProductChange)
                nodeResult = nodeResult.UpdateResults(settingsNew, diff, nodePep, this);

            return nodeResult;
        }

        public IEnumerable<TransitionDocNode> GetTransitions(SrmSettings settings, ExplicitMods mods, double precursorMz,
            IsotopeDistInfo isotopeDist, SpectrumHeaderInfo libInfo, Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks, bool useFilter)
        {
            if (mods == null || !mods.HasCrosslinks)
            {
                return TransitionGroup.GetTransitions(settings, this, mods, precursorMz, isotopeDist, libInfo, transitionRanks,
                    useFilter, true);
            }
            var crosslinkBuilder = new CrosslinkBuilder(settings, TransitionGroup.Peptide, mods, LabelType);
            return crosslinkBuilder.GetTransitionDocNodes(TransitionGroup, precursorMz, isotopeDist, transitionRanks,
                useFilter);
        }

        public DocNode EnsureChildren(PeptideDocNode parent, ExplicitMods mods, SrmSettings settings)
        {
            var result = this;
            // Check if children will change as a result of ChangeSettings.
            var changed = result.ChangeSettings(settings, parent, mods, SrmSettingsDiff.ALL);
            if (result.AutoManageChildren && !AreEquivalentChildren(result.Children, changed.Children))
            {
                changed = result = (TransitionGroupDocNode)result.ChangeAutoManageChildren(false);
                changed = changed.ChangeSettings(settings, parent, mods, SrmSettingsDiff.ALL);
            }
            // Make sure node points to correct parent.
            if (!ReferenceEquals(parent.Peptide, TransitionGroup.Peptide))
            {
                result = (TransitionGroupDocNode) ChangeId(new TransitionGroup(parent.Peptide,
                    TransitionGroup.PrecursorAdduct, TransitionGroup.LabelType));
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
                TransitionDocNode nodeTran1 = (TransitionDocNode)children1[i];
                TransitionDocNode nodeTran2 = (TransitionDocNode)children2[i];
                if (!nodeTran1.Transition.Equivalent(nodeTran2.Transition))
                    return false;
                // Losses must also be equal
                if (!Equals(nodeTran1.Losses, nodeTran2.Losses))
                    return false;
            }
            return true;
        }

        private TransitionDocNode FindEquivalentTransition(TransitionDocNode nodeTran)
        {
            return Transitions.FirstOrDefault(t => t.Key(this).Equivalent(nodeTran.Key(this)));
        }

        private Dictionary<TransitionLossKey, DocNode> CreateTransitionLossToChildMap()
        {
            // TODO: Solve issue where precursor gets many fragments with the same TransitionLossKey
//            var keys = Children.Select(child => ((TransitionDocNode) child).Key(this)).ToList();
//            if (keys.Count != keys.Distinct().Count())
//                Console.WriteLine("Issue");

            return Children.ToDictionary(child => ((TransitionDocNode) child).Key(this));
        }

        private static readonly IDictionary<int, int> EMPTY_RESULTS_LOOKUP = new Dictionary<int, int>();

        public TransitionGroupDocNode UpdateResults(SrmSettings settingsNew,
                                                    SrmSettingsDiff diff,
                                                    PeptideDocNode nodePep,
                                                    TransitionGroupDocNode nodePrevious)
        {
            if (!settingsNew.HasResults)
            {
                // Make sure no results are present, if the new settings has no results
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
            if (Children.Count == 0)
            {
                // If no children, just use a null populated list of the right size.
                return ChangeResults(settingsNew.MeasuredResults.EmptyTransitionGroupResults);
            }
            if (!settingsNew.MeasuredResults.Chromatograms.Any(c => c.IsLoaded) &&
                (!HasResults || Results.All(r => r.IsEmpty)))
            {
                // If nothing is loaded yet and the old settings had no results then initialize to empty results
                return UpdateResultsToEmpty(settingsNew.MeasuredResults);
            }
            // Store indexes to previous results in a dictionary for lookup
            var settingsOld = diff.SettingsOld;
            var dictChromIdIndex = settingsOld != null && settingsOld.HasResults
                ? settingsOld.MeasuredResults.IdToIndexDictionary
                : EMPTY_RESULTS_LOOKUP;

            // Store keys for previous children in a set, if the children have changed due
            // to a user action, and not simply loading (when nodePrevious may be null).
            HashSet<TransitionLossKey> setTranPrevious = null;
            if (nodePrevious != null && !AreEquivalentChildren(Children, nodePrevious.Children) &&
                // Only necessary if children were added
                Children.Count > nodePrevious.Children.Count)
            {
                setTranPrevious = new HashSet<TransitionLossKey>(
                    from child in nodePrevious.Children
                    select ((TransitionDocNode)child).Key(this));
            }

            var resultsCalc = new TransitionGroupResultsCalculator(settingsNew, nodePep, this, dictChromIdIndex);
            var measuredResults = settingsNew.MeasuredResults;
            List<IList<ChromatogramGroupInfo>> allChromatogramGroupInfos = null;
            try
            {
                if (MustReadAllChromatograms(settingsNew, diff))
                {
                    allChromatogramGroupInfos = measuredResults.LoadChromatogramsForAllReplicates(nodePep, this,
                        (float) settingsNew.TransitionSettings.Instrument.MzMatchTolerance);
                    ChromatogramGroupInfo.LoadPeaksForAll(allChromatogramGroupInfos.SelectMany(list => list), false);
                }
            }
            catch (FileModifiedException)
            {
                // Unable to read results for all replicates: fall back to reading replicates individually
            }

            for (int chromIndex = 0; chromIndex < measuredResults.Chromatograms.Count; chromIndex++)
            {
                CalcResultsForReplicate(resultsCalc, chromIndex, settingsNew, diff, nodePep, nodePrevious,
                    setTranPrevious, allChromatogramGroupInfos?[chromIndex]);
            }

            return resultsCalc.UpdateTransitionGroupNode(this);
        }

        private void CalcResultsForReplicate(TransitionGroupResultsCalculator resultsCalc, int chromIndex, SrmSettings settingsNew, SrmSettingsDiff diff, PeptideDocNode nodePep, TransitionGroupDocNode nodePrevious, HashSet<TransitionLossKey> setTranPrevious, IList<ChromatogramGroupInfo> chromGroupInfos)
        {
            var measuredResults = settingsNew.MeasuredResults;
            var settingsOld = diff.SettingsOld;
            var dictChromIdIndex = settingsOld?.MeasuredResults?.IdToIndexDictionary;
            var chromatograms = measuredResults.Chromatograms[chromIndex];
            var resultsHandler = settingsNew.PeptideSettings.Integration.ResultsHandler;
            bool chromatogramDataChanged = measuredResults.HasNewChromatogramData(chromIndex);

            resultsCalc.AddSet();

            // Check if this object has existing results information
            int iResultOld;
            if (!diff.DiffResults)
            {
                iResultOld = chromIndex;
            }
            else if (dictChromIdIndex == null
                     || !dictChromIdIndex.TryGetValue(chromatograms.Id.GlobalIndex, out iResultOld)
                     || Results != null && iResultOld >= Results.Count)
            {
                iResultOld = -1;
            }

            if (iResultOld != -1)
            {
                if (Results == null || iResultOld >= Results.Count || Results[iResultOld].IsEmpty)
                {
                    iResultOld = -1;
                }
            }

            bool canUseOldResults = false;
            // Check whether we can reuse the existing information without having to look at the ChromatogramInfo
            if (iResultOld != -1 && resultsHandler == null)
            {
                canUseOldResults = CanUseOldResults(settingsNew, diff, nodePrevious, chromIndex, iResultOld);
            }

            float mzMatchTolerance = (float)settingsNew.TransitionSettings.Instrument.MzMatchTolerance;
            if (!canUseOldResults)
            {
                if (chromGroupInfos == null)
                {
                    try
                    {
                        measuredResults.TryLoadChromatogram(chromatograms, nodePep, this, mzMatchTolerance,
                            out var arrayChromGroupInfo);
                        chromGroupInfos = arrayChromGroupInfo ?? Array.Empty<ChromatogramGroupInfo>();
                        foreach (var chromGroupInfo in chromGroupInfos)
                        {
                            // We will need the peaks later, so make sure they can be read now
                            if (chromGroupInfo.NumPeaks > 0)
                            {
                                chromGroupInfo.GetTransitionPeak(0, 0);
                            }
                        }
                    }
                    catch (FileModifiedException)
                    {
                        if (iResultOld != -1 && resultsHandler == null)
                        {
                            canUseOldResults = true;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }

            if (canUseOldResults)
            {
                for (int iTran = 0; iTran < Children.Count; iTran++)
                {
                    var nodeTran = (TransitionDocNode)Children[iTran];
                    var results = nodeTran.HasResults ? nodeTran.Results[iResultOld] : default(ChromInfoList<TransitionChromInfo>);
                    if (results.IsEmpty)
                        resultsCalc.AddTransitionChromInfo(iTran, null);
                    else
                    {
                        resultsCalc.AddTransitionChromInfo(iTran, results
                            .Where(chromInfo => chromatograms.IndexOfId(chromInfo.FileId) >= 0).ToList());
                    }
                }

                return;
            }

            double qcutoff = double.MaxValue;
            bool keepUserSet = true;
            if (resultsHandler != null)
            {
                keepUserSet = !resultsHandler.OverrideManual;
                if (!resultsHandler.IncludeDecoys && IsDecoy)
                    resultsHandler = null;
                else
                    qcutoff = resultsHandler.QValueCutoff;
            }

            // Check for any user set transitions in the previous node that
            // should be used to set peak boundaries on any new nodes.
            Dictionary<int, TransitionChromInfo> dictUserSetInfoBest = null;
            bool mismatchedEmptyReintegrated = false;
            if (keepUserSet && iResultOld != -1)
            {
                // Or we have reintegrated peaks that are not matching the current integrate all setting
                if (settingsOld == null)
                    mismatchedEmptyReintegrated = nodePrevious.IsMismatchedEmptyReintegrated(iResultOld);
                if (setTranPrevious != null || mismatchedEmptyReintegrated || chromatogramDataChanged)
                    dictUserSetInfoBest = nodePrevious.FindBestUserSetInfo(iResultOld);
            }
            if (chromGroupInfos.Count == 0)
            {
                bool useOldResults = iResultOld != -1 && !chromatograms.IsLoadedAndAvailable(measuredResults);

                for (int iTran = 0; iTran < Children.Count; iTran++)
                {
                    var nodeTran = (TransitionDocNode)Children[iTran];
                    var results = default(ChromInfoList<TransitionChromInfo>);
                    if (useOldResults)
                    {
                        if (nodeTran.HasResults && nodeTran.Results.Count > iResultOld)
                        {
                            results = nodeTran.Results[iResultOld];
                        }
                    }
                    if (results.IsEmpty)
                        resultsCalc.AddTransitionChromInfo(iTran, null);
                    else
                        resultsCalc.AddTransitionChromInfo(iTran, results.ToList());
                }

                return;
            }

            // Make sure each file only appears once in the list, since downstream
            // code has problems with multiple measurements in the same file.
            // Most measurements should happen only once per replicate, meaning this
            // if clause is an unusual case.  A race condition pre-0.7 occasionally
            // resulted in writing precursor entries multiple times to the cache file.
            // This code also corrects that problem by ignoring all but the first
            // instance.
            if (chromGroupInfos.Count > 1)
                chromGroupInfos = chromGroupInfos.Distinct(ChromatogramGroupInfo.PathComparer).ToList();
            // Find the file indexes once
            int countGroupInfos = chromGroupInfos.Count;
            var fileIds = new ChromFileInfoId[countGroupInfos];
            // and matching reintegration statistics, if any
            PeakFeatureStatistics[] reintegratePeaks = resultsHandler != null
                ? new PeakFeatureStatistics[countGroupInfos]
                : null;
            for (int j = 0; j < countGroupInfos; j++)
            {
                var fileId = chromatograms.FindFile(chromGroupInfos[j]);

                fileIds[j] = fileId;

                if (resultsHandler != null)
                {
                    reintegratePeaks[j] = resultsHandler.GetPeakFeatureStatistics(nodePep.Peptide, fileId);
                }
            }
            resultsCalc.AddReintegrateInfo(resultsHandler, fileIds, reintegratePeaks);

            // Figure out the number of steps for this chromatogram set, if it has
            // an optimization function.
            int numSteps = 0;
            if (chromatograms.OptimizationFunction != null)
                numSteps = chromatograms.OptimizationFunction.StepCount;

            // Calculate the transition info, and the max values for the transition group
            for (int iTran = 0; iTran < Children.Count; iTran++)
            {
                var nodeTran = (TransitionDocNode)Children[iTran];
                // Use existing information, if it is still equivalent to the
                // chosen peak.
                var results = nodeTran.HasResults && iResultOld != -1 ?
                    nodeTran.Results[iResultOld] : default(ChromInfoList<TransitionChromInfo>);

                // Singleton chrom infos are most common. So avoid creating a list every time
                TransitionChromInfo firstChromInfo = null;
                IList<TransitionChromInfo> listTranInfo = null;
                for (int j = 0; j < countGroupInfos; j++)
                {
                    // Get all transition chromatogram info for this file.
                    ChromatogramGroupInfo chromGroupInfo = chromGroupInfos[j];
                    PeakGroupIntegrator peakGroupIntegrator = null;
                    ChromFileInfoId fileId = fileIds[j];
                    PeakFeatureStatistics reintegratePeak = reintegratePeaks != null ? reintegratePeaks[j] : null;

                    var listChromInfo = chromGroupInfo.GetAllTransitionInfo(nodeTran,
                        mzMatchTolerance, chromatograms.OptimizationFunction, TransformChrom.interpolated);
                    if (listChromInfo.IsEmpty)
                    {
                        // Make sure nothing gets added when no measurements are present
                        continue;
                    }

                    // Always add the right number of steps to the list, no matter
                    // how many entries were returned.
                    for (int step = -numSteps; step <= numSteps; step++)
                    {
                        ChromatogramInfo info = listChromInfo.GetChromatogramForStep(step);
                        // Check for existing info that was set by the user.
                        UserSet userSet = UserSet.FALSE;
                        var chromInfo = FindChromInfo(results, fileId, step);
                        bool notUserSet;
                        if (resultsHandler == null)
                        {
                            // If we don't have a model then we shouldn't change peaks that are "REINTEGRATED".
                            notUserSet = chromInfo == null || chromInfo.UserSet == UserSet.FALSE;
                        }
                        else
                        {
                            notUserSet = chromInfo == null || chromInfo.UserSet == UserSet.FALSE ||
                                         chromInfo.UserSet == UserSet.REINTEGRATED;
                        }
                        if (!keepUserSet || notUserSet || mismatchedEmptyReintegrated || chromatogramDataChanged)
                        {
                            ChromPeak peak = ChromPeak.EMPTY;
                            IonMobilityFilter ionMobility = IonMobilityFilter.EMPTY;
                            if (info != null)
                            {
                                TransitionGroupChromInfo chromGroupInfoMatch;
                                if (dictUserSetInfoBest != null)
                                {
                                    TransitionChromInfo chromInfoBest;
                                    if (mismatchedEmptyReintegrated)
                                    {
                                        // If we are reintegrating, then copy the peak boundaries of the best peak
                                        dictUserSetInfoBest.TryGetValue(fileId.GlobalIndex,
                                            out chromInfoBest);
                                    }
                                    else
                                    {
                                        // Otherwise, use the same peak boundaries
                                        chromInfoBest = chromInfo;
                                    }

                                    if (chromInfoBest != null)
                                    {
                                        peakGroupIntegrator ??= MakePeakGroupIntegrator(settingsNew, chromatograms, chromGroupInfo);
                                        peak = CalcPeak(settingsNew, peakGroupIntegrator, info, chromInfoBest);
                                        userSet = chromInfoBest.UserSet;
                                    }
                                }
                                // Or if there is a matching peak on another precursor in the peptide
                                else if (nodePep.HasResults && !HasResults &&
                                    TryGetMatchingGroupInfo(nodePep, chromIndex, fileId, step, out chromGroupInfoMatch))
                                {
                                    peakGroupIntegrator ??= MakePeakGroupIntegrator(settingsNew, chromatograms, chromGroupInfo);
                                    peak = CalcMatchingPeak(settingsNew, peakGroupIntegrator, info, chromGroupInfoMatch, reintegratePeak, qcutoff, ref userSet);
                                }
                                // Otherwise use the best peak chosen at import time
                                else
                                {
                                    int bestIndex = GetBestIndex(info, reintegratePeak, qcutoff, ref userSet);
                                    if (bestIndex != -1)
                                        peak = info.GetPeak(bestIndex);
                                }
                                ionMobility = info.GetIonMobilityFilter();
                            }

                            // Avoid creating new info objects that represent the same data
                            // in use before.
                            if (chromInfo == null || !chromInfo.Equivalent(fileId, step, peak, ionMobility) || chromInfo.UserSet != userSet)
                            {
                                int ratioCount = settingsNew.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
                                chromInfo = CreateTransitionChromInfo(chromInfo, fileId, step, peak, ionMobility, ratioCount, userSet);
                            }
                        }

                        if (firstChromInfo == null)
                            firstChromInfo = chromInfo;
                        else
                        {
                            if (listTranInfo == null)
                                listTranInfo = new List<TransitionChromInfo>(countGroupInfos) { firstChromInfo };
                            listTranInfo.Add(chromInfo);
                        }
                    }
                }
                if (firstChromInfo == null)
                    resultsCalc.AddTransitionChromInfo(iTran, null);
                else if (listTranInfo == null)
                    resultsCalc.AddTransitionChromInfo(iTran, new SingletonList<TransitionChromInfo>(firstChromInfo));
                else
                    resultsCalc.AddTransitionChromInfo(iTran, listTranInfo);
            }
        }

        public PeakGroupIntegrator MakePeakGroupIntegrator(SrmSettings settings, ChromatogramSet chromatogramSet, ChromatogramGroupInfo chromatogramGroupInfo)
        {
            var tolerance = (float) settings.TransitionSettings.Instrument.MzMatchTolerance;
            var timeIntervals = (chromatogramGroupInfo.TimeIntensitiesGroup as RawTimeIntensities)?.TimeIntervals;
            var peakGroupIntegrator =
                new PeakGroupIntegrator(settings.TransitionSettings.FullScan.AcquisitionMethod, timeIntervals);
            foreach (var transition in Transitions)
            {
                var optStepChromatograms = chromatogramGroupInfo.GetAllTransitionInfo(transition, tolerance,
                    chromatogramSet.OptimizationFunction, TransformChrom.raw);
                var chromatogramInfo = optStepChromatograms.GetChromatogramForStep(0);
                if (chromatogramInfo == null)
                {
                    continue;
                }
                peakGroupIntegrator.AddPeakIntegrator(chromatogramInfo.MakePeakIntegrator(peakGroupIntegrator));
            }

            return peakGroupIntegrator;
        }

        private bool MustReadAllChromatograms(SrmSettings settingsNew, SrmSettingsDiff settingsDiff)
        {
            if (null != settingsNew.PeptideSettings.Integration.ResultsHandler)
            {
                return true;
            }

            var settingsOld = settingsDiff.SettingsOld;
            if (settingsOld == null)
            {
                return true;
            }

            if (settingsNew.TransitionSettings.Instrument.MzMatchTolerance !=
                settingsOld.TransitionSettings.Instrument.MzMatchTolerance)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the area values in the TransitionChromInfo's can be trusted so that the data from the .skyd does not need to be examined.
        /// </summary>
        private bool CanUseOldResults(SrmSettings settingsNew, SrmSettingsDiff diff, TransitionGroupDocNode nodePrevious, int chromIndex, int iResultOld)
        {
            if (MustReadAllChromatograms(settingsNew, diff))
            {
                return false;
            }
            var measuredResults = settingsNew.MeasuredResults;
            var chromatograms = settingsNew.MeasuredResults.Chromatograms[chromIndex];
            var settingsOld = diff.SettingsOld;
            if (!chromatograms.IsLoadedAndAvailable(measuredResults))
            {
                return true;
            }
            if (settingsOld == null)
            {
                return false;
            }
            if (settingsNew.TransitionSettings.Instrument.MzMatchTolerance != settingsOld.TransitionSettings.Instrument.MzMatchTolerance)
            {
                return false;
            }
            if (measuredResults.HasNewChromatogramData(chromIndex))
            {
                return false;
            }
            if (!ReferenceEquals(chromatograms, settingsOld.MeasuredResults?.Chromatograms[iResultOld]))
            {
                return false;
            }
            if (!Equals(this, nodePrevious))
            {
                return false;
            }

            foreach (var transition in Transitions)
            {
                if (transition.Results == null || iResultOld >= transition.Results.Count)
                {
                    return false;
                }

                if (transition.Results[iResultOld].IsEmpty)
                {
                    return false;
                }
            }
            return true;
        }

        private TransitionGroupDocNode UpdateResultsToEmpty(MeasuredResults measuredResults)
        {
            // If the results are already empty at this level, then no need to change anything
            if (HasResults && Results.Count == measuredResults.Chromatograms.Count && Results.All(r => r.IsEmpty))
                return this;

            IList<DocNode> childrenNew = new List<DocNode>(Children.Count);
            foreach (TransitionDocNode nodeTransition in Children)
                childrenNew.Add(nodeTransition.ChangeResults(measuredResults.EmptyTransitionResults));

            var empty = measuredResults.EmptyTransitionGroupResults;
            return (TransitionGroupDocNode) ChangeResults(empty).ChangeChildren(childrenNew);
        }

        private static int GetBestIndex(ChromatogramInfo info, PeakFeatureStatistics reintegratePeak, double qcutoff, ref UserSet userSet)
        {
            int bestIndex = info.BestPeakIndex;
            if (reintegratePeak != null)
            {
                // If a cut-off for qvalues is set and q value is not low enough
                // then pick no peak at all.
                var qvalue = reintegratePeak.QValue;
                if (qvalue.HasValue && qvalue.Value > qcutoff)
                {
                    userSet = UserSet.REINTEGRATED;
                    bestIndex = -1;
                }
                // Otherwise, if the reintegrate peak is different from the default
                // best peak, then use it and mark the peak as chosen by reintegration
                else if (bestIndex != reintegratePeak.BestPeakIndex)
                {
                    userSet = UserSet.REINTEGRATED;
                    bestIndex = reintegratePeak.BestPeakIndex;
                }
            }
            return bestIndex;
        }

        private static ChromPeak CalcPeak(SrmSettings settingsNew, PeakGroupIntegrator peakGroupIntegrator,
            ChromatogramInfo info, TransitionChromInfo chromInfoBest)
        {
            if (chromInfoBest.IsEmpty)
            {
                return ChromPeak.EMPTY;
            }

            ChromPeak.FlagValues flags = 0;
            if (settingsNew.MeasuredResults.IsTimeNormalArea)
                flags = ChromPeak.FlagValues.time_normalized;
            return info.CalcPeak(peakGroupIntegrator, chromInfoBest.StartRetentionTime, chromInfoBest.EndRetentionTime,
                flags);
        }

        private static ChromPeak CalcMatchingPeak(SrmSettings settingsNew,
                                                  PeakGroupIntegrator peakGroupIntegrator,
                                                  ChromatogramInfo info,
                                                  TransitionGroupChromInfo chromGroupInfoMatch,
                                                  PeakFeatureStatistics reintegratePeak,
                                                  double qcutoff, 
                                                  ref UserSet userSet)
        {
            ChromPeak.FlagValues flags = 0;
            if (settingsNew.MeasuredResults.IsTimeNormalArea)
                flags = ChromPeak.FlagValues.time_normalized;
            var peak = info.CalcPeak(peakGroupIntegrator, chromGroupInfoMatch.StartRetentionTime.Value, chromGroupInfoMatch.EndRetentionTime.Value, flags);
            userSet = UserSet.MATCHED;
            var userSetBest = UserSet.FALSE;
            int bestIndex = GetBestIndex(info, reintegratePeak, qcutoff, ref userSetBest);
            if (bestIndex != -1)
            {
                var peakBest = info.GetPeak(bestIndex);
                if (peakBest.StartTime == peak.StartTime && peakBest.EndTime == peak.EndTime)
                {
                    userSet = userSetBest;
                }
            }
            return peak;
        }

        private IEnumerable<TransitionGroupDocNode> GetMatchingGroups(PeptideDocNode nodePep)
        {
            if (nodePep.HasResults && !HasResults && RelativeRT == RelativeRT.Matching)
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    if (nodeGroup.HasResults && nodeGroup.RelativeRT == RelativeRT.Matching &&
                            !ReferenceEquals(nodeGroup.TransitionGroup, TransitionGroup))
                        yield return nodeGroup;
                }
            }
        }

        private bool TryGetMatchingGroupInfo(PeptideDocNode nodePep, int chromIndex, ChromFileInfoId fileId, int step, out TransitionGroupChromInfo chromGroupInfoMatch)
        {
            foreach (var nodeGroup in GetMatchingGroups(nodePep))
            {
                var results = nodeGroup.GetSafeChromInfo(chromIndex);
                chromGroupInfoMatch = FindGroupChromInfo(results, fileId, step);
                if (chromGroupInfoMatch != null &&
                    chromGroupInfoMatch.StartRetentionTime.HasValue &&
                    chromGroupInfoMatch.EndRetentionTime.HasValue)
                {
                    return true;
                }
            }
            chromGroupInfoMatch = null;
            return false;
        }

        private static TransitionChromInfo CreateTransitionChromInfo(TransitionChromInfo chromInfo, ChromFileInfoId fileId,
                                              int step, ChromPeak peak, IonMobilityFilter ionMobility, int ratioCount, UserSet userSet)
        {
            // Use the old ratio for now, and it will be corrected by the peptide,
            // if it is incorrect.
            Annotations annotations = chromInfo != null ? chromInfo.Annotations : Annotations.EMPTY;
            return new TransitionChromInfo(fileId, step, peak, ionMobility, annotations, userSet);
        }

        /// <summary>
        /// Returns true if there are empty reintegrated peaks.
        /// </summary>
        private bool IsMismatchedEmptyReintegrated(int indexResult)
        {
            foreach (TransitionDocNode nodeTran in Children)
            {
                if (!nodeTran.HasResults)
                    continue;

                var chromInfoList = nodeTran.Results[indexResult];
                if (chromInfoList.IsEmpty)
                    continue;

                foreach (var chromInfo in chromInfoList)
                {
                    if (chromInfo == null || chromInfo.UserSet != UserSet.REINTEGRATED || chromInfo.OptimizationStep != 0)
                        continue;
                    if (chromInfo.IsEmpty)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Find the <see cref="TransitionChromInfo"/> set by the user with the largest
        /// peak area for each file represented in a specific result set.
        /// </summary>
        private Dictionary<int, TransitionChromInfo> FindBestUserSetInfo(int indexResult)
        {
            Dictionary<int, TransitionChromInfo> dictInfo = null;

            foreach (TransitionDocNode nodeTran in Children)
            {
                if (!nodeTran.HasResults)
                    continue;

                var chromInfoList = nodeTran.Results[indexResult];
                if (chromInfoList.IsEmpty)
                    continue;

                foreach (var chromInfo in chromInfoList)
                {
                    if (chromInfo == null || chromInfo.UserSet == UserSet.FALSE || chromInfo.OptimizationStep != 0)
                        continue;

                    if (dictInfo == null)
                        dictInfo = new Dictionary<int, TransitionChromInfo>();

                    TransitionChromInfo chromInfoBest;
                    if (dictInfo.TryGetValue(chromInfo.FileIndex, out chromInfoBest))
                    {
                        if (chromInfoBest.Area >= chromInfo.Area)
                            continue;
                        dictInfo.Remove(chromInfoBest.FileIndex);
                    }
                    dictInfo.Add(chromInfo.FileIndex, chromInfo);
                }
            }

            return dictInfo;
        }

        private static TransitionGroupChromInfo FindGroupChromInfo(IList<TransitionGroupChromInfo> results,
                                                         ChromFileInfoId fileId, int step)
        {
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    var chromInfo = results[i];
                    if (ReferenceEquals(fileId, chromInfo.FileId) && step == chromInfo.OptimizationStep)
                        return chromInfo;
            }
            }
            return null;
        }

        private static TransitionChromInfo FindChromInfo(IList<TransitionChromInfo> results,
                                                         ChromFileInfoId fileId, int step)
        {
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    var chromInfo = results[i];
                    if (ReferenceEquals(fileId, chromInfo.FileId) && step == chromInfo.OptimizationStep)
                        return chromInfo;
                }
            }
            return null;
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

        public override string GetDisplayText(DisplaySettings settings)
        {
            return TransitionGroupTreeNode.DisplayText(this, settings);
        }

        private sealed class TransitionGroupResultsCalculator
        {
            private readonly PeptideDocNode _nodePep;
            private readonly TransitionGroupDocNode _nodeGroup;
            private readonly List<TransitionGroupChromInfoListCalculator> _listResultCalcs;
            private readonly TransitionChromInfoSet[] _arrayTransitionChromInfoSets;
            // Allow look-up of former result position
            private readonly IDictionary<int, int> _dictChromIdIndex;

            public TransitionGroupResultsCalculator(SrmSettings settings,
                                                    PeptideDocNode nodePep,
                                                    TransitionGroupDocNode nodeGroup,                                                    
                                                    IDictionary<int, int> dictChromIdIndex)
            {
                Settings = settings;

                _nodePep = nodePep;
                _nodeGroup = nodeGroup;
                _dictChromIdIndex = dictChromIdIndex;

                // Shouldn't be necessary to create one of these, if there are
                // no transitions
                int countTransitions = nodeGroup.Children.Count;
                Assume.IsTrue(countTransitions > 0);
                _listResultCalcs = new List<TransitionGroupChromInfoListCalculator>();
                _arrayTransitionChromInfoSets = new TransitionChromInfoSet[countTransitions];
                for (int iTran = 0; iTran < countTransitions; iTran++)
                    _arrayTransitionChromInfoSets[iTran] = new TransitionChromInfoSet();
            }

            private SrmSettings Settings { get; set; }

            public void AddSet()
            {
                int transitionCount = _arrayTransitionChromInfoSets.Length;
                ChromInfoList<TransitionGroupChromInfo> listChromInfo = default(ChromInfoList<TransitionGroupChromInfo>);
                int iResult = _listResultCalcs.Count;
                if (_nodeGroup.HasResults)
                {
                    int iResultOld = GetOldPosition(iResult);
                    if (iResultOld != -1 && iResultOld < _nodeGroup.Results.Count)
                    {
                        listChromInfo = _nodeGroup.Results[iResultOld];
                    }
                }
                _listResultCalcs.Add(new TransitionGroupChromInfoListCalculator(Settings, _nodePep,
                    iResult, transitionCount, listChromInfo));
            }

            public void AddReintegrateInfo(MProphetResultsHandler resultsHandler, ChromFileInfoId[] fileIds, PeakFeatureStatistics[] reintegratePeaks)
            {
                _listResultCalcs.Last().AddReintegrateInfo(resultsHandler, fileIds, reintegratePeaks);
            }

            public void AddTransitionChromInfo(int iTran, IList<TransitionChromInfo> info)
            {
                var nodeTran = (TransitionDocNode) _nodeGroup.Children[iTran];
                var listInfo = _arrayTransitionChromInfoSets[iTran].ChromInfoLists;
                int iNext = listInfo.Count;
                listInfo.Add(info);

                // Add the iNext entry
                _listResultCalcs[iNext].AddChromInfoList(nodeTran, info);
            }

            private int GetOldPosition(int iResult)
            {
                if (iResult < Settings.MeasuredResults.Chromatograms.Count)
                {
                    int iResultOld;
                    var chromatograms = Settings.MeasuredResults.Chromatograms[iResult];
                    if (_dictChromIdIndex.TryGetValue(chromatograms.Id.GlobalIndex, out iResultOld))
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
                    childrenNew.Add(UpdateTransitionNode(nodeTran, iTran));
                }

                var listChromInfoLists = _listResultCalcs.ConvertAll(calc => calc.CalcChromInfoList());
                var results = Results<TransitionGroupChromInfo>.Merge(nodeGroup.Results, listChromInfoLists);

                var nodeGroupNew = nodeGroup;
                if (!Results<TransitionGroupChromInfo>.EqualsDeep(results, nodeGroupNew.Results))
                    nodeGroupNew = nodeGroupNew.ChangeResults(results);

                nodeGroupNew = (TransitionGroupDocNode)nodeGroupNew.ChangeChildrenChecked(childrenNew);
                return nodeGroupNew;
            }

            private TransitionDocNode UpdateTransitionNode(TransitionDocNode nodeTran, int iTran)
            {
                var chromInfoSet = _arrayTransitionChromInfoSets[iTran];
                var results = Results<TransitionChromInfo>.Merge(nodeTran.Results, chromInfoSet.ChromInfoLists);
                if (!Results<TransitionChromInfo>.EqualsDeep(results, nodeTran.Results))
                    nodeTran = nodeTran.ChangeResults(results);
                if (nodeTran.ResultsRank != chromInfoSet.AverageRank)
                    nodeTran = nodeTran.ChangeResultsRank(chromInfoSet.AverageRank);
                return nodeTran;
            }

            private void RankAndCorrelateTransitions(TransitionGroupDocNode nodeGroup)
            {
                int countTransitions = _arrayTransitionChromInfoSets.Length;
                
                var arrayRanked = new IndexedTypedInfo[countTransitions];
                var arrayRankedAverage = new KeyValuePair<int, MeanArea>[countTransitions];
                for (int i = 0; i < countTransitions; i++)
                    arrayRankedAverage[i] = new KeyValuePair<int, MeanArea>(i, new MeanArea());

                bool isFullScanMs = Settings.TransitionSettings.FullScan.IsEnabledMs;
                double[] peakAreas = null, libIntensities = null;
                if (nodeGroup.HasLibInfo)
                {
                    int countTransMsMs = nodeGroup.GetMsMsTransitions(isFullScanMs).Count(t => t.ParticipatesInScoring);
                    if (countTransMsMs >= MIN_DOT_PRODUCT_TRANSITIONS)
                    {
                        peakAreas = new double[countTransMsMs];
                        libIntensities = new double[countTransMsMs];
                    }
                }
                double[] peakAreasMs = null, isoProportionsMs = null;
                if (nodeGroup.HasIsotopeDist)
                {
                    int countTransMs = nodeGroup.GetMsTransitions(isFullScanMs).Count();
                    if (countTransMs >= MIN_DOT_PRODUCT_MS1_TRANSITIONS)
                    {
                        peakAreasMs = new double[countTransMs];
                        isoProportionsMs = new double[countTransMs];
                    }
                }
                for (int iChrom = 0; iChrom < _listResultCalcs.Count; iChrom++)
                {
                    var arrayFileSteps = GetTransitionFileSteps(iChrom);
                    foreach (var fileStep in arrayFileSteps)
                    {
                        int countInfo = 0, countLibTrans = 0, countIsoTrans = 0;
                        ChromFileInfoId fileId = fileStep.FileId;
                        int optStep = fileStep.OptimizationStep;
                        for (int iTran = 0; iTran < countTransitions; iTran++)
                        {
                            // CONSIDER: Current TransitionChromInfo lookup is O(n^2), but on usually very small
                            //           lists.  Using a faster lookup for large lists would be slower for the
                            //           most common case.
                            var nodeTran = (TransitionDocNode)nodeGroup.Children[iTran];
                            var chromInfo = GetTransitionChromInfo(iTran, iChrom, fileId, optStep);
                            arrayRanked[iTran] = new IndexedTypedInfo(iTran, nodeTran.IsMs1, chromInfo, nodeTran.ParticipatesInScoring);
                            arrayRankedAverage[iTran].Value.AddArea(nodeTran.ParticipatesInScoring ? GetSafeArea(chromInfo) : -1);
                            // Count non-null info
                            if (chromInfo != null)
                                countInfo++;

                            // Store information for correlation score
                            if (peakAreas != null && (!isFullScanMs || !nodeTran.IsMs1) && nodeTran.ParticipatesInScoring)
                            {
                                peakAreas[countLibTrans] = GetSafeArea(chromInfo);
                                libIntensities[countLibTrans] = GetSafeLibIntensity(nodeTran);
                                countLibTrans++;
                            }
                            if (peakAreasMs != null && nodeTran.IsMs1)
                            {
                                peakAreasMs[countIsoTrans] = GetSafeArea(chromInfo);
                                isoProportionsMs[countIsoTrans] = GetSafeIsotopeDistProportion(nodeTran);
                                countIsoTrans++;
                            }
                        }

                        // End when no rankable info is found for a file index
                        if (countInfo == 0)
                            break;                            

                        // Calculate correlation score
                        if (peakAreas != null)
                            _listResultCalcs[iChrom].SetLibInfo(fileId, optStep, peakAreas, libIntensities);
                        if (peakAreasMs != null)
                            _listResultCalcs[iChrom].SetIsotopeDistInfo(fileId, optStep, peakAreasMs, isoProportionsMs);

                        // Sort by area descending
                        Array.Sort(arrayRanked, IndexedTypedInfo.CompareAreaDesc);
                        // Change any TransitionChromInfo items that do not have the right rank.
                        short iRankMs = 0, iRankMsMs = 0;
                        for (int iRank = 0; iRank < countTransitions; iRank++)
                        {
                            var pair = arrayRanked[iRank];
                            if (pair.Info == null)
                                continue;
                            short rank = 0, rankByLevel = 0;
                            if (pair.Info.Area > 0)
                            {
                                rank = (short) (iRank + 1);
                                rankByLevel = pair.IsMs1 ? ++iRankMs : ++iRankMsMs;
                            }
                            if (pair.Info.Rank != rank || pair.Info.RankByLevel != rankByLevel)
                                pair.Result = pair.Info.ChangeRank(false, rank, rankByLevel);
                        }

                        foreach (var pair in arrayRanked)
                        {
                            if (pair.Result != null)
                                SetTransitionChromInfo(pair.Index, iChrom, fileId, optStep, pair.Result);
                        }
                    }
                }

                // Store average ranks
                Array.Sort(arrayRankedAverage, (m1, m2) =>
                                               Comparer<double>.Default.Compare(m2.Value.Mean, m1.Value.Mean));
                for (int iRank = 0; iRank < countTransitions; iRank++)
                {
                    var rankedAverage = arrayRankedAverage[iRank];
                    if (rankedAverage.Value.Mean == 0)
                        break;
                    _arrayTransitionChromInfoSets[rankedAverage.Key].AverageRank = iRank + 1;
                }
            }

            private class IndexedTypedInfo
            {
                public IndexedTypedInfo(int index, bool isMs1, TransitionChromInfo info, bool scorable)
                {
                    Index = index;
                    IsMs1 = isMs1;
                    Info = info;
                    ParticipatesInScoring = scorable;
                }

                public int Index { get; private set; }
                public bool IsMs1 { get; private set; }
                public bool ParticipatesInScoring { get; private set; }
                public TransitionChromInfo Info { get; private set; }
                public TransitionChromInfo Result { get; set; }

                public static int CompareAreaDesc(IndexedTypedInfo p1, IndexedTypedInfo p2)
                {
                    return Comparer<float>.Default.Compare(GetSafeRankArea(p2), GetSafeRankArea(p1));
                }

            }

            private IEnumerable<FileStep> GetTransitionFileSteps(int iChrom)
            {
                // By far the most common case is that there is only 1:
                // importing single-file replicates without optimization
                ChromFileInfoId id = null;
                int optimizationStep = 0;
                foreach (var set in _arrayTransitionChromInfoSets)
                {
                    if (set.ChromInfoLists[iChrom] == null)
                        continue;
                    foreach (var info in set.ChromInfoLists[iChrom])
                    {
                        if (id == null)
                        {
                            id = info.FileId;
                            optimizationStep = info.OptimizationStep;
                        }
                        else if (!ReferenceEquals(id, info.FileId) || optimizationStep != info.OptimizationStep)
                        {
                            id = null;
                            break;
                        }
                    }
                }

                if (id != null)
                    yield return new FileStep(id, optimizationStep);
                else
                {
                    // Use the longer query in more complex cases, with ToArray allocation
                    // since the set could change during retrieval
                    foreach (var step in _arrayTransitionChromInfoSets
                    .Where(s => s.ChromInfoLists[iChrom] != null)
                    .SelectMany(s => s.ChromInfoLists[iChrom])
                    .Select(info => new FileStep(info.FileId, info.OptimizationStep))
                        .Distinct()
                        .ToArray())
                    {
                        yield return step;
            }
                }
            }

            private TransitionChromInfo GetTransitionChromInfo(int iTran, int iChrom, ChromFileInfoId fileId, int optStep)
            {
                var chromInfoList = _arrayTransitionChromInfoSets[iTran].ChromInfoLists[iChrom];
                if (chromInfoList != null)
                {
                    for (int i = 0; i < chromInfoList.Count; i++)
                    {
                        var chromInfo = chromInfoList[i];
                        if (ReferenceEquals(fileId, chromInfo.FileId) && optStep == chromInfo.OptimizationStep)
                            return chromInfo;
                    }
                }
                    return null;
            }

            private void SetTransitionChromInfo(int iTran, int iChrom, ChromFileInfoId fileId, int optStep,
                                                TransitionChromInfo transitionChromInfo)
            {
                var chromInfoList = _arrayTransitionChromInfoSets[iTran].ChromInfoLists[iChrom];
                int chromInfoIndex = chromInfoList.IndexOf(chromInfo =>
                    ReferenceEquals(fileId, chromInfo.FileId) && optStep == chromInfo.OptimizationStep);
                chromInfoList[chromInfoIndex] = transitionChromInfo;
            }

            private static float GetSafeArea(TransitionChromInfo info)
            {
                return (info != null ? info.Area : 0.0f);
            }

            private static float GetSafeRankArea(IndexedTypedInfo info)
            {
                return (info is { ParticipatesInScoring: true } ? GetSafeArea(info.Info) : -1.0f);
            }

            private static float GetSafeLibIntensity(TransitionDocNode nodeTran)
            {
                return (nodeTran.HasLibInfo ? nodeTran.LibInfo.Intensity : 0);
            }

            private static float GetSafeIsotopeDistProportion(TransitionDocNode nodeTran)
            {
                return (nodeTran.HasDistInfo ? nodeTran.IsotopeDistInfo.Proportion : 0);
            }

            private class TransitionChromInfoSet
            {
                public TransitionChromInfoSet()
                {
                    ChromInfoLists = new List<IList<TransitionChromInfo>>();
                }

                public int? AverageRank { get; set; }
                public List<IList<TransitionChromInfo>> ChromInfoLists { get; private set; }
            }

            private struct FileStep
            {
                public FileStep(ChromFileInfoId fileId, int optimizationStep) : this()
                {
                    FileId = fileId;
                    OptimizationStep = optimizationStep;
                }

                public ChromFileInfoId FileId { get; private set; }
                public int OptimizationStep { get; private set; }

                #region object overrides
                
                private bool Equals(FileStep other)
                {
                    return ReferenceEquals(other.FileId, FileId) &&
                        other.OptimizationStep == OptimizationStep;
                }

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) return false;
                    if (obj.GetType() != typeof(FileStep)) return false;
                    return Equals((FileStep)obj);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        return (FileId.GlobalIndex.GetHashCode() * 397) ^ OptimizationStep;
                    }
                }

                #endregion
            }

            private class MeanArea
            {
                private int Count { get; set; }
                public double Mean { get; private set; }

                public void AddArea(double area)
                {
                    Count++;
                    Mean += (area - Mean)/Count;
                }
            }
        }

        private sealed class TransitionGroupChromInfoListCalculator
        {
            private readonly PeptideDocNode _nodePep;
            private readonly ChromInfoList<TransitionGroupChromInfo> _listChromInfo;

            public TransitionGroupChromInfoListCalculator(SrmSettings settings,
                                                          PeptideDocNode nodePep,
                                                          int resultsIndex,
                                                          int transitionCount,
                                                          ChromInfoList<TransitionGroupChromInfo> listChromInfo)
            {
                Settings = settings;
                ResultsIndex = resultsIndex;
                TransitionCount = transitionCount;
                _nodePep = nodePep;

                _listChromInfo = listChromInfo;

                Calculators = new List<TransitionGroupChromInfoCalculator>();
            }

            private SrmSettings Settings { get; set; }
            private int ResultsIndex { get; set; }
            private int TransitionCount { get; set; }
            private List<TransitionGroupChromInfoCalculator> Calculators { get; set; }

            private MProphetResultsHandler ReintegrateResults { get; set; }
            private ChromFileInfoId[] ReintegrateFileIds { get; set; }
            private PeakFeatureStatistics[] ReintegratePeaks { get; set; }

            private PeakFeatureStatistics GetReintegratePeak(ChromFileInfoId fileId, int step)
            {
                if (ReintegrateResults != null && step == 0)
                {
                    int i = ReintegrateFileIds.IndexOf(id => id.GlobalIndex == fileId.GlobalIndex);
                    if (i != -1)
                        return ReintegratePeaks[i];
                }
                return null;
            }

            public void AddReintegrateInfo(MProphetResultsHandler resultsHandler, ChromFileInfoId[] fileIds, PeakFeatureStatistics[] reintegratePeaks)
            {
                if (resultsHandler != null)
                {
                    ReintegrateResults = resultsHandler;
                    ReintegrateFileIds = fileIds;
                    ReintegratePeaks = reintegratePeaks;
                }
            }

            public void AddChromInfoList(TransitionDocNode nodeTran, IList<TransitionChromInfo> listInfo)
            {
                if (listInfo == null)
                    return;

                for (int iInfo = 0; iInfo < listInfo.Count; iInfo++)
                {
                    var chromInfo = listInfo[iInfo];
                    if (chromInfo == null)
                        continue;

                    ChromFileInfoId fileId = chromInfo.FileId;
                    int fileOrder = IndexOfFileInSettings(fileId);
                    if (fileOrder == -1)
                        throw new InvalidDataException(ModelResources.TransitionGroupChromInfoListCalculator_AddChromInfoList_Attempt_to_add_integration_information_for_missing_file);
                    int step = chromInfo.OptimizationStep;
                    int i = IndexOfCalc(fileOrder, step);
                    if (i >= 0)
                        Calculators[i].AddChromInfo(nodeTran, chromInfo);
                    else
                    {
                        var explicitPeakBounds = Settings.GetExplicitPeakBounds(_nodePep,
                            Settings.MeasuredResults.Chromatograms[ResultsIndex].MSDataFileInfos[fileOrder].FilePath);
                        var chromInfoGroup = FindChromInfo(fileId, step);
                        var calc = new TransitionGroupChromInfoCalculator(Settings,
                                                                          ResultsIndex,
                                                                          fileId,
                                                                          step,
                                                                          TransitionCount,
                                                                          chromInfoGroup,
                                                                          GetReintegratePeak(fileId, step), 
                                                                          explicitPeakBounds);
                        calc.AddChromInfo(nodeTran, chromInfo);
                        Calculators.Insert(~i, calc);
                    }
                }
            }            

            public IList<TransitionGroupChromInfo> CalcChromInfoList()
            {
                var listInfo = Calculators.ConvertAll(calc => calc.CalcChromInfo());
                return (listInfo.Count > 0 && listInfo[0] != null ? listInfo : null);
            }

            public void SetLibInfo(ChromFileInfoId fileId, int optStep, double[] peakAreas, double[] libIntensities)
            {
                int iFile = IndexOfCalc(fileId, optStep);
                Debug.Assert(iFile >= 0);   // Should have already been added
                Calculators[iFile].SetLibInfo(peakAreas, libIntensities);
            }

            public void SetIsotopeDistInfo(ChromFileInfoId fileId, int optStep, double[] peakAreas, double[] isotopeDistProportions)
            {
                int iFile = IndexOfCalc(fileId, optStep);
                Debug.Assert(iFile >= 0);   // Should have already been added
                Calculators[iFile].SetIsotopeDistInfo(peakAreas, isotopeDistProportions);
            }

            private TransitionGroupChromInfo FindChromInfo(ChromFileInfoId fileId, int optStep)
            {
                return _listChromInfo.FirstOrDefault(info => ReferenceEquals(fileId, info.FileId) && optStep == info.OptimizationStep);
            }

            private int IndexOfFileInSettings(ChromFileInfoId fileId)
            {
                return Settings.MeasuredResults.Chromatograms[ResultsIndex].IndexOfId(fileId);
            }

            private int IndexOfCalc(ChromFileInfoId fileId, int optStep)
            {
                return Calculators.IndexOf(calc => ReferenceEquals(fileId, calc.FileId) && optStep == calc.OptimizationStep);
            }

            /// <summary>
            /// Returns the index in the list of calculators of the calculator
            /// representing a particular file and optimization step.  If no calculator
            /// yet exists for the file and optimization step, then (like binary search)
            /// the bitwise complement of the first calculator with a higher index is
            /// returned.
            /// </summary>
            /// <param name="fileOrder">The file index</param>
            /// <param name="optimizationStep">The optimization step</param>
            /// <returns>Index of specified calculator, or bitwise complement of the first
            /// entry with greater index value</returns>
            private int IndexOfCalc(int fileOrder, int optimizationStep)
            {
                int i = 0;
                foreach (var calc in Calculators)
                {
                    if (calc.FileOrder == fileOrder)
                    {
                        if (calc.OptimizationStep == optimizationStep)
                            return i;
                        else if (calc.OptimizationStep > optimizationStep)
                            return ~i;
                    }
                    else if (calc.FileOrder > fileOrder)
                        return ~i;
                    i++;
                }
                return ~i;
            }
        }

        private sealed class TransitionGroupChromInfoCalculator
        {
            public TransitionGroupChromInfoCalculator(SrmSettings settings,
                                                        int resultsIndex,
                                                        ChromFileInfoId fileId,
                                                        int optimizationStep,
                                                        int transitionCount,
                                                        TransitionGroupChromInfo chromInfo,
                                                        PeakFeatureStatistics reintegratePeak,
                                                        ExplicitPeakBounds explicitPeakBounds)
            {
                Settings = settings;
                ResultsIndex = resultsIndex;
                FileId = fileId;
                OptimizationStep = optimizationStep;
                TransitionCount = transitionCount;
                UserSet = UserSet.FALSE;

                // Use existing ratios, annotations and peak scores
                if (chromInfo != null)
                {
                    QValue = chromInfo.QValue;
                    ZScore = chromInfo.ZScore;

                    Annotations = chromInfo.Annotations;

                    IonMobilityInfo = chromInfo.IonMobilityInfo;
                }
                else
                {
                    Annotations = Annotations.EMPTY;

                    IonMobilityInfo = TransitionGroupIonMobilityInfo.EMPTY; 
                }

                if (reintegratePeak != null)
                {
                    QValue = reintegratePeak.QValue;
                    ZScore = reintegratePeak.BestScore;
                }
                ExplicitPeakBounds = explicitPeakBounds;
            }

            private ExplicitPeakBounds ExplicitPeakBounds { get; set; }
            private SrmSettings Settings { get; set; }
            private int ResultsIndex { get; set; }
            public ChromFileInfoId FileId { get; private set; }
            public int FileOrder { get; private set; }
            public int OptimizationStep { get; private set; }
            private int TransitionCount { get; set; }
            private int PeakCount { get; set; }
            private int ResultsCount { get; set; }
            private RetentionTimeValues BestRetentionTimes { get; set; }
            private RetentionTimeValues NonQuantitativeRetentionTimes { get; set; }
            private TransitionGroupIonMobilityInfo IonMobilityInfo { get; set; }
            private float? Fwhm { get; set; }
            private float? Area { get; set; } // Area of all peaks, including non-scoring peaks that don't influence RT determination
            private float? AreaMs1 { get; set; }
            private float? AreaFragment { get; set; }
            private float? BackgroundArea { get; set; }
            private float? BackgroundAreaMs1 { get; set; }
            private float? BackgroundAreaFragment { get; set; }
            private float? MassError { get; set; }
            private int? Truncated { get; set; }
            private PeakIdentification Identified { get; set; }
            private float? LibraryDotProduct { get; set; }
            private float? IsotopeDotProduct { get; set; }
            private float? QValue { get; set; }
            private float? ZScore { get; set; }
            private Annotations Annotations { get; set; }
            private UserSet UserSet { get; set; }

            private float PeakCountRatio
            {
                get { return ((float) PeakCount)/TransitionCount; }
            }

            public void AddChromInfo(TransitionDocNode nodeTran, TransitionChromInfo info)
            {
                if (info == null)
                    return;

                // Aggregate ion mobility information across all transitions
                IonMobilityInfo = IonMobilityInfo.AddIonMobilityFilterInfo(info.IonMobility, nodeTran.Transition.IsPrecursor());

                ResultsCount++;

                if (!ReferenceEquals(info.FileId, FileId))
                {
                    Assume.IsTrue(ReferenceEquals(info.FileId, FileId),
                                 string.Format(
                                     ModelResources
                                         .TransitionGroupChromInfoCalculator_AddChromInfo_Grouping_transitions_from_file__0__with_file__1__,
                                     info.FileIndex, FileId.GlobalIndex));
                }
                FileId = info.FileId;
                FileOrder = Settings.MeasuredResults.Chromatograms[ResultsIndex].IndexOfId(FileId);

                if (!info.IsEmpty)
                {
                    if (info.IsGoodPeak(Settings.TransitionSettings.Integration.IsIntegrateAll))
                        PeakCount++;
                    var retentionTimeValues = nodeTran.ParticipatesInScoring ? RetentionTimeValues.FromTransitionChromInfo(info) : null;
                    if (nodeTran.IsQuantitative(Settings))
                    {
                        Area = (Area ?? 0) + info.Area;
                        BackgroundArea = (BackgroundArea ?? 0) + info.BackgroundArea;
                        if (info.MassError.HasValue)
                        {
                            double massError = MassError ?? 0;
                            massError += (info.MassError.Value - massError) * info.Area / Area.Value;
                            MassError = (float)massError;
                        }

                        BestRetentionTimes = RetentionTimeValues.Merge(BestRetentionTimes, retentionTimeValues);
                        if (!info.IsFwhmDegenerate)
                            Fwhm = Math.Max(info.Fwhm, Fwhm ?? float.MinValue);
                        if (info.IsTruncated.HasValue)
                        {
                            if (!Truncated.HasValue)
                                Truncated = 0;
                            if (info.IsTruncated.Value)
                                Truncated++;
                        }
                        switch (Settings.GetChromSource(nodeTran))
                        {
                            case ChromSource.ms1:
                                AreaMs1 = (AreaMs1 ?? 0) + info.Area;
                                BackgroundAreaMs1 = (BackgroundAreaMs1 ?? 0) + info.BackgroundArea;
                                break;
                            case ChromSource.fragment:
                                AreaFragment = (AreaFragment ?? 0) + info.Area;
                                BackgroundAreaFragment = (BackgroundAreaFragment ?? 0) + info.BackgroundArea;
                                break;
                        }
                    }
                    else
                    {
                        NonQuantitativeRetentionTimes = RetentionTimeValues.Merge(NonQuantitativeRetentionTimes, retentionTimeValues);
                    }

                    if (info.IsIdentified &&
                        (info.Identified == PeakIdentification.TRUE || Identified == PeakIdentification.FALSE))
                    {
                        Identified = info.Identified;
                    }
                }

                UserSet = AddUserSetInfo(UserSet, info.UserSet);
            }

            /// <summary>
            /// Rules for changing the group UserSet based on adding a new transition.  TRUE overrides IMPORTED 
            /// overrides REINTEGRATED overrides FALSE
            /// </summary>
            /// <param name="groupUserSet">Current UserSet status of the chromatograms for this transition group</param>
            /// <param name="tranUserSet"> Current UserSet status of the chromatogram for the transition to be added</param>
            private static UserSet AddUserSetInfo(UserSet groupUserSet, UserSet tranUserSet)
            {
                return UserSetExtension.GetBest(groupUserSet, tranUserSet);
            }

            public void SetLibInfo(double[] peakAreas, double[] libIntensities)
            {
                // Only do this once.
                if (LibraryDotProduct.HasValue)
                    return;

                var statPeakAreas = new Statistics(peakAreas);
                var statLibIntensities = new Statistics(libIntensities);
                LibraryDotProduct = (float) statPeakAreas.NormalizedContrastAngleSqrt(statLibIntensities);
            }

            public void SetIsotopeDistInfo(double[] peakAreas, double[] isotopeDistProportions)
            {
                // Only do this once.
                if (IsotopeDotProduct.HasValue)
                    return;

                var statPeakAreas = new Statistics(peakAreas);
                var statIsotopeDistProportions = new Statistics(isotopeDistProportions);
                IsotopeDotProduct = (float)statPeakAreas.NormalizedContrastAngleSqrt(statIsotopeDistProportions);
            }

            public TransitionGroupChromInfo CalcChromInfo()
            {
                if (ResultsCount == 0)
                    return null;
                var qValue = QValue;
                if (!qValue.HasValue && ExplicitPeakBounds != null)
                {
                    if (ExplicitPeakBounds.Score != ExplicitPeakBounds.UNKNOWN_SCORE)
                    {
                        qValue = (float)ExplicitPeakBounds.Score;
                    }
                }

                var retentionTimeValues = BestRetentionTimes ?? NonQuantitativeRetentionTimes;
                return new TransitionGroupChromInfo(FileId,
                                                    OptimizationStep,
                                                    PeakCountRatio,
                                                    (float?) retentionTimeValues?.RetentionTime,
                                                    (float?) retentionTimeValues?.StartRetentionTime,
                                                    (float?) retentionTimeValues?.EndRetentionTime,
                                                    IonMobilityInfo,
                                                    Fwhm,
                                                    Area, AreaMs1, AreaFragment,
                                                    BackgroundArea, BackgroundAreaMs1, BackgroundAreaFragment,
                                                    (float?) BestRetentionTimes?.Height,
                                                    MassError,
                                                    Truncated,
                                                    Identified,
                                                    LibraryDotProduct,
                                                    IsotopeDotProduct,
                                                    qValue,
                                                    ZScore,
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
            return AreEquivalentChildren(Children, nodeGroup.Children);
        }

        /// <summary>
        /// Calculate precursor ion mass, paying attention to whether this is a peptide or a small molecule
        /// </summary>
        public TypedMass GetPrecursorIonMass()
        {
            var precursorAdduct = TransitionGroup.PrecursorAdduct;
            var precursorMz = PrecursorMz;
            return TransitionGroup.Peptide.IsCustomMolecule ? 
                precursorAdduct.MassFromMz(precursorMz, PrecursorMzMassType) :
                SequenceMassCalc.GetMH(precursorMz, precursorAdduct, PrecursorMzMassType);
        }

        /// <summary>
        /// Return precursor's neutral mass rounded for XML I/O
        /// </summary>
        public double GetPrecursorIonPersistentNeutralMass()
        {
            var ionMass = GetPrecursorIonMass();
            return TransitionGroup.Peptide.IsCustomMolecule ? Math.Round(ionMass, SequenceMassCalc.MassPrecision) : SequenceMassCalc.PersistentNeutral(ionMass);
        }

        public class CustomIonPrecursorComparer : IComparer<TransitionGroupDocNode>
        {
            public int Compare(TransitionGroupDocNode left, TransitionGroupDocNode right)
            {
                var test = Peptide.CompareGroups(left, right);
                if (test != 0)
                    return test;
                // ReSharper disable PossibleNullReferenceException
                return left.PrecursorMz.CompareTo(right.PrecursorMz);
                // ReSharper restore PossibleNullReferenceException
            }
        }
        
        #region Property change methods

        public TransitionGroupDocNode ChangeLibInfo(SpectrumHeaderInfo prop)
        {
            return ChangeProp(ImClone(this), im => im.LibInfo = prop);
        }

        public TransitionGroupDocNode ChangeExplicitValues(ExplicitTransitionGroupValues prop)
        {
            return Equals(prop, ExplicitValues) ? this : ChangeProp(ImClone(this), im => im.ExplicitValues = prop);
        }

        public TransitionGroupDocNode ChangeResults(Results<TransitionGroupChromInfo> prop)
        {
            return Results<TransitionGroupChromInfo>.EqualsDeep(Results, prop) ? 
                   this : 
                   ChangeProp(ImClone(this), im => im.Results = prop);
        }

        public TransitionGroupDocNode ChangePrecursorAnnotations(ChromFileInfoId fileId, Annotations annotations)
        {
            var groupChromInfo = ChromInfos.FirstOrDefault(info => ReferenceEquals(info.FileId, fileId));
            if (groupChromInfo == null)
                throw new InvalidDataException(string.Format(ModelResources.TransitionGroupDocNode_ChangePrecursorAnnotations_File_Id__0__does_not_match_any_file_in_document_,
                                               fileId.GlobalIndex));
            groupChromInfo = groupChromInfo.ChangeAnnotations(annotations);
            return ChangeResults(Results<TransitionGroupChromInfo>.ChangeChromInfo(Results,
                                                                                   fileId,
                                                                                   groupChromInfo));
        }

        public TransitionGroupDocNode AddPrecursorAnnotations(ChromFileInfoId fileId, Dictionary<string, string> annotations)
        {
            var groupChromInfo = ChromInfos.FirstOrDefault(info => ReferenceEquals(info.FileId, fileId));
            if (groupChromInfo == null)
                throw new InvalidDataException(string.Format(ModelResources.TransitionGroupDocNode_ChangePrecursorAnnotations_File_Id__0__does_not_match_any_file_in_document_, 
                                               fileId.GlobalIndex));
            var groupAnnotations = groupChromInfo.Annotations;
            foreach (var annotation in annotations)
                groupAnnotations = groupAnnotations.ChangeAnnotation(annotation.Key, annotation.Value);
            return ChangePrecursorAnnotations(fileId, groupAnnotations);
        }

        public DocNode ChangePeak(SrmSettings settings,
                                  ChromatogramGroupInfo chromGroupInfo,
                                  double mzMatchTolerance,
                                  int indexSet,
                                  ChromFileInfoId fileId,
                                  OptimizableRegression regression,
                                  Identity tranId,
                                  double retentionTime,
                                  UserSet userSet)
        {
            int ratioCount = settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
            
            // Find the index of the peak group referenced by this retention time.
            int indexPeakBest = -1;
            // Use the peak closest to the time passed in.
            double minDeltaRT = double.MaxValue;
            foreach (TransitionDocNode nodeTran in Children)
            {
                if (tranId != null && !ReferenceEquals(tranId, nodeTran.Id))
                    continue;
                var chromInfo = chromGroupInfo.GetTransitionInfo(nodeTran, (float)mzMatchTolerance);
                if (chromInfo == null)
                    continue;
                int indexPeak = chromInfo.IndexOfPeak(retentionTime);
                if (indexPeak == -1)
                    continue;
                var peak = chromInfo.GetPeak(indexPeak);
                double deltaRT = Math.Abs(retentionTime - peak.RetentionTime);
                if (deltaRT < minDeltaRT)
                {
                    minDeltaRT = deltaRT;
                    indexPeakBest = indexPeak; 
                }
            }
            if (indexPeakBest == -1)
                throw new ArgumentOutOfRangeException(string.Format(ModelResources.TransitionGroupDocNode_ChangePeak_No_peak_found_at__0__, retentionTime));
            // Calculate extents of the peaks being added
            double startMin = double.MaxValue, endMax = double.MinValue;
            foreach (TransitionDocNode nodeTran in Children)
            {
                var chromInfo = chromGroupInfo.GetTransitionInfo(nodeTran, (float)mzMatchTolerance);
                if (chromInfo == null)
                    continue;
                ChromPeak peakNew = chromInfo.GetPeak(indexPeakBest);
                if (peakNew.IsEmpty)
                    continue;
                startMin = Math.Min(startMin, peakNew.StartTime);
                endMax = Math.Max(endMax, peakNew.EndTime);
            }
            // Update all transitions with the new information
            var listChildrenNew = new List<DocNode>(Children.Count);
            foreach (TransitionDocNode nodeTran in Children)
            {
                var optStepChromatograms = chromGroupInfo.GetAllTransitionInfo(nodeTran, (float)mzMatchTolerance, regression, TransformChrom.interpolated);
                // Shouldn't need to update a transition with no chrom info
                if (optStepChromatograms.IsEmpty) 
                    listChildrenNew.Add(nodeTran.RemovePeak(indexSet, fileId, userSet));
                else
                {
                    int numSteps = optStepChromatograms.StepCount;
                    var nodeTranNew = nodeTran;
                    for (int step = -numSteps; step <= numSteps; step++)
                    {
                        var chromInfo = optStepChromatograms.GetChromatogramForStep(step);
                        if (chromInfo == null)
                        {
                            continue;
                        }
                        ChromPeak peakNew = chromInfo.GetPeak(indexPeakBest);
                        nodeTranNew = (TransitionDocNode) nodeTranNew.ChangePeak(indexSet, fileId, step, peakNew,
                            chromInfo.GetIonMobilityFilter(), ratioCount, userSet);
                    }
                    listChildrenNew.Add(nodeTranNew);
                }
            }
            return ChangeChildrenChecked(listChildrenNew);
        }

        public DocNode ChangePeak(SrmSettings settings,
                                  ChromatogramGroupInfo chromGroupInfo,
                                  int indexSet,
                                  ChromFileInfoId fileId,
                                  OptimizableRegression regression,
                                  Transition transition,
                                  double? startTime,
                                  double? endTime,
                                  PeakIdentification identified,
                                  UserSet userSet,
                                  bool preserveMissingPeaks)
        {
            // Error if only one of startTime and endTime is null
            if (startTime == null && endTime != null)
                throw new ArgumentException(string.Format(ModelResources.TransitionGroupDocNode_ChangePeak_Missing_Start_Time_in_Change_Peak));
            if (startTime != null && endTime == null)
                throw new ArgumentException(string.Format(ModelResources.TransitionGroupDocNode_ChangePeak_Missing_End_Time_In_Change_Peak));
            int ratioCount = settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;

            // Recalculate peaks based on new boundaries
            var listChildrenNew = new List<DocNode>(Children.Count);
            ChromPeak.FlagValues flags = 0;
            if (settings.MeasuredResults.IsTimeNormalArea)
                flags |= ChromPeak.FlagValues.time_normalized;
            if (identified != PeakIdentification.FALSE)
                flags |= ChromPeak.FlagValues.contains_id;
            if (identified == PeakIdentification.ALIGNED)
                flags |= ChromPeak.FlagValues.used_id_alignment;
            float mzMatchTolerance = (float) settings.TransitionSettings.Instrument.MzMatchTolerance;
            var peakGroupIntegrator = MakePeakGroupIntegrator(settings,
                settings.MeasuredResults.Chromatograms[indexSet], chromGroupInfo);
            foreach (TransitionDocNode nodeTran in Children)
            {
                if (transition != null && !ReferenceEquals(transition, nodeTran.Transition))
                {
                    listChildrenNew.Add(nodeTran);
                    continue;
                }
                if (preserveMissingPeaks)
                {
                    if (null != nodeTran.Results && indexSet < nodeTran.Results.Count)
                    {
                        var existingChromInfos = nodeTran.Results[indexSet];
                        if (existingChromInfos.All(chromInfo => chromInfo.IsEmpty))
                        {
                            listChildrenNew.Add(nodeTran);
                            continue;
                        }
                    }
                }
                var listChromInfo = chromGroupInfo.GetAllTransitionInfo(nodeTran, mzMatchTolerance, regression, TransformChrom.interpolated);

                // Shouldn't need to update a transition with no chrom info
                // Also if startTime is null, remove the peak
                if (listChromInfo.IsEmpty || startTime == null)
                    listChildrenNew.Add(nodeTran.RemovePeak(indexSet, fileId, userSet));
                else
                {
                    int numSteps = regression?.StepCount ?? 0;
                    var nodeTranNew = nodeTran;
                    for (int step = -numSteps; step <= numSteps; step++)
                    {
                        var chromInfo = listChromInfo.GetChromatogramForStep(step);
                        ChromPeak chromPeak = chromInfo?.CalcPeak(peakGroupIntegrator, (float) startTime, (float) endTime, flags) 
                                              ?? ChromPeak.EMPTY;
                        nodeTranNew = (TransitionDocNode) nodeTranNew.ChangePeak(indexSet, fileId, step, chromPeak,
                            chromInfo?.GetIonMobilityFilter(), ratioCount, userSet);
                    }
                    listChildrenNew.Add(nodeTranNew);
                }
            }
            return ChangeChildrenChecked(listChildrenNew);
        }

        protected override DocNodeParent SynchRemovals(DocNodeParent siblingBefore, DocNodeParent siblingAfter)
        {
            var nodeGroupBefore = (TransitionGroupDocNode)siblingBefore;
            var nodeGroupSynch = (TransitionGroupDocNode)siblingAfter;

            // Only synchronize groups with the same adduct, ignoring any isotopes specified in the adducts for match purposes.
            if (!TransitionGroup.PrecursorAdduct.Unlabeled.Equals(nodeGroupSynch.TransitionGroup.PrecursorAdduct.Unlabeled))
                return this;
            // Only synchronize groups with the same Spectrum Filter
            if (!SpectrumClassFilter.Equals(nodeGroupSynch.SpectrumClassFilter))
                return this;
            // Start with the current node as the default
            var nodeResult = this;

            // Use same auto-manage setting
            if (AutoManageChildren != nodeGroupSynch.AutoManageChildren)
            {
                nodeResult = (TransitionGroupDocNode) nodeResult.ChangeAutoManageChildren(
                    nodeGroupSynch.AutoManageChildren);
            }

            var childrenRemoved = nodeGroupBefore.Transitions.Where(c => !nodeGroupSynch.Children.Contains(c)).ToList();
            var childrenNew = new List<DocNode>();
            foreach (TransitionDocNode nodeTran in Transitions)
            {
                var tranKey = nodeTran.Key(this);
                if (!childrenRemoved.Contains(t => t.Key(nodeGroupBefore).Equivalent(tranKey)))
                    childrenNew.Add(nodeTran);
            }
            return (TransitionGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
        }

        protected override DocNodeParent SynchChildren(SrmSettings settings, DocNodeParent parent, DocNodeParent sibling)
        {
            var nodePep = (PeptideDocNode)parent;
            var nodeGroupSynch = (TransitionGroupDocNode)sibling;

            // Only synchronize groups with the same adduct, ignoring any isotopes specified in the adducts for match purposes.
            if (!TransitionGroup.PrecursorAdduct.Unlabeled.Equals(nodeGroupSynch.TransitionGroup.PrecursorAdduct.Unlabeled))
                return this;

            // Start with the current node as the default
            var nodeResult = this;

            // Use same auto-manage setting
            if (AutoManageChildren != nodeGroupSynch.AutoManageChildren)
            {
                nodeResult = (TransitionGroupDocNode) nodeResult.ChangeAutoManageChildren(
                                                          nodeGroupSynch.AutoManageChildren);
            }

            var childrenNew = new List<DocNode>();
            foreach (TransitionDocNode nodeTran in nodeGroupSynch.Children)
            {
                var tranMatch = nodeTran.Transition;
                var tran = new Transition(TransitionGroup,
                                            tranMatch.IonType,
                                            tranMatch.CleavageOffset,
                                            tranMatch.MassIndex,
                                            tranMatch.IsPrecursor() ? TransitionGroup.PrecursorAdduct : tranMatch.Adduct, // Our own precursor adduct may include needed isotope info, for small molecules
                                            tranMatch.DecoyMassShift,
                                            tranMatch.CustomIon);
                var losses = nodeTran.Losses;
                // m/z, isotope distribution and library info calculated later
                var nodeTranNew = new TransitionDocNode(tran, losses, TypedMass.ZERO_MONO_MASSH, TransitionDocNode.TransitionQuantInfo.DEFAULT, nodeTran.ExplicitValues);
                // keep existing nodes, if we have them
                var nodeTranExist = nodeResult.Transitions.FirstOrDefault(n => Equals(n.Key(this), nodeTranNew.Key(this)));
                childrenNew.Add(nodeTranExist ?? nodeTranNew);
            }
            nodeResult = (TransitionGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
            // Update properties so that the next settings change will update results correctly
            nodeResult = nodeResult.ChangeSettings(settings, nodePep, nodePep.ExplicitMods, SrmSettingsDiff.PROPS);
            var diff = new SrmSettingsDiff(settings, true);
            return nodeResult.UpdateResults(settings, diff, nodePep, this);
        }

        /// <summary>
        /// Merges the transitions of another <see cref="TransitionGroupDocNode"/> into this one,
        /// giving this node precedence when both nodes have matching transitions.
        /// </summary>
        /// <param name="nodeGroupMerge">The node from which transitions are merged</param>
        /// <returns>A new copy of this node with merged children, or this node if all children match</returns>
        public TransitionGroupDocNode Merge(TransitionGroupDocNode nodeGroupMerge)
        {
            // CONSIDER: This code prefers existing doc nodes as long as the key is the same
            //           This works for the PasteDlg case for which it was written, but it is
            //           conceivable that a call would expect the merged doc node to take precedence.
            return Merge(nodeGroupMerge, null);
        }

        public TransitionGroupDocNode Merge(TransitionGroupDocNode nodeGroupMerge,
            Func<TransitionDocNode, TransitionDocNode, TransitionDocNode> mergeMatch)
        {
            var childrenNew = Children.Cast<TransitionDocNode>().ToList();
            // Remember where all the existing children are
            var dictPepIndex = new Dictionary<TransitionLossKey, int>();
            for (int i = 0; i < childrenNew.Count; i++)
            {
                var key = childrenNew[i].Key(this);
                if (!dictPepIndex.ContainsKey(key))
                    dictPepIndex[key] = i;
            }
            // Add the new children to the end, or merge when the node is already present
            foreach (TransitionDocNode nodeTran in nodeGroupMerge.Children)
            {
                int i;
                if (!dictPepIndex.TryGetValue(nodeTran.Key(nodeGroupMerge), out i))
                    childrenNew.Add(nodeTran);
                else if (mergeMatch != null)
                    childrenNew[i] = mergeMatch(childrenNew[i], nodeTran);
            }
            childrenNew.Sort(TransitionGroup.CompareTransitions);
            return (TransitionGroupDocNode)ChangeChildrenChecked(childrenNew.Cast<DocNode>().ToArray());
        }

        public TransitionGroupDocNode MergeUserInfo(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroupMerge,
            SrmSettings settings, SrmSettingsDiff diff)
        {
            var result = Merge(nodeGroupMerge, (n, nMerge) => n.MergeUserInfo(settings, nMerge));
            var annotations = Annotations.Merge(nodeGroupMerge.Annotations);
            if (!ReferenceEquals(annotations, Annotations))
                result = (TransitionGroupDocNode)result.ChangeAnnotations(annotations);
            var resultsInfo = MergeResultsUserInfo(settings, nodeGroupMerge.Results);
            if (!ReferenceEquals(resultsInfo, Results))
                result = result.ChangeResults(resultsInfo);
            return result.UpdateResults(settings, diff, nodePep, this);
        }

        private Results<TransitionGroupChromInfo> MergeResultsUserInfo(
            SrmSettings settings, Results<TransitionGroupChromInfo> results)
        {
            if (!HasResults)
                return Results;

            var dictFileIdToChromInfo = results.SelectMany(l => l)
                                               // Merge everything that does not already exist (handled below),
                                               // as merging only user modified causes loss of information in
                                               // updates
                                               //.Where(i => i.IsUserModified)
                                               .ToDictionary(i => i.FileIndex);

            var listResults = new List<ChromInfoList<TransitionGroupChromInfo>>();
            for (int i = 0; i < results.Count; i++)
            {
                List<TransitionGroupChromInfo> listChromInfo = null;
                var chromSet = settings.MeasuredResults.Chromatograms[i];
                var chromInfoList = Results[i];
                foreach (var fileInfo in chromSet.MSDataFileInfos)
                {
                    TransitionGroupChromInfo chromInfo;
                    if (!dictFileIdToChromInfo.TryGetValue(fileInfo.FileIndex, out chromInfo))
                        continue;
                    if (listChromInfo == null)
                    {
                        listChromInfo = new List<TransitionGroupChromInfo>(chromInfoList);
                    }
                    int iExist = listChromInfo.IndexOf(chromInfoExist =>
                                                       ReferenceEquals(chromInfoExist.FileId, chromInfo.FileId) &&
                                                       chromInfoExist.OptimizationStep == chromInfo.OptimizationStep);
                    if (iExist == -1)
                        listChromInfo.Add(chromInfo);
                    else if (chromInfo.IsUserModified)
                        listChromInfo[iExist] = chromInfo;
                }
                if (listChromInfo != null)
                    chromInfoList = new ChromInfoList<TransitionGroupChromInfo>(listChromInfo);
                listResults.Add(chromInfoList);
            }
            if (ArrayUtil.InnerReferencesEqual<TransitionGroupChromInfo, ChromInfoList<TransitionGroupChromInfo>>(listResults, Results))
                return Results;
            return new Results<TransitionGroupChromInfo>(listResults);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionGroupDocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var equal = base.Equals(obj) &&
                        obj.PrecursorMz == PrecursorMz &&
                        Equals(obj.IsotopeDist, IsotopeDist) &&
                        Equals(obj.LibInfo, LibInfo) &&
                        Equals(obj.Results, Results) &&
                        Equals(obj.CustomMolecule, CustomMolecule) &&
                        Equals(obj.ExplicitValues, ExplicitValues) &&
                        Equals(obj.PrecursorConcentration, PrecursorConcentration) &&
                        Equals(obj.SpectrumClassFilter, SpectrumClassFilter);
            return equal;
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
                result = (result*397) ^ (IsotopeDist != null ? IsotopeDist.GetHashCode() : 0);
                result = (result*397) ^ (LibInfo != null ? LibInfo.GetHashCode() : 0);
                result = (result*397) ^ (Results != null ? Results.GetHashCode() : 0);
                result = (result*397) ^ ExplicitValues.GetHashCode();
                result = (result*397) ^ (CustomMolecule != null ? CustomMolecule.GetHashCode() : 0);
                result = (result*397) ^ PrecursorConcentration.GetHashCode();
                result = (result*397) ^ SpectrumClassFilter.GetHashCode();
                return result;
            }
        }

        public override string ToString()
        {
            return TextUtil.SpaceSeparate(TransitionGroup.Peptide.ToString(), TransitionGroup.ToString());
        }

        #endregion

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
            if (!libraries.IsLoaded)
                return;

            IsotopeLabelType labelType;
            if (!settings.TryGetLibInfo(TransitionGroup.Peptide, TransitionGroup.PrecursorAdduct, mods, out labelType, out libInfo))
                libInfo = null;                
            else if (transitionRanks != null)
            {
                try
                {
                    SpectrumPeaksInfo spectrumInfo;
                    LibKey key;
                    if (TransitionGroup.Peptide.IsCustomMolecule)
                    {
                        var adduct = settings.GetModifiedAdduct(TransitionGroup.PrecursorAdduct, TransitionGroup.Peptide.CustomMolecule.UnlabeledFormula, labelType, mods);
                        key = new LibKey(TransitionGroup.Peptide.CustomMolecule.GetSmallMoleculeLibraryAttributes(), adduct);
                    }
                    else
                    {
                        var sequenceMod = settings.GetModifiedSequence(TransitionGroup.Peptide.Target, labelType, mods);
                        key = new LibKey(sequenceMod, TransitionGroup.PrecursorAdduct.AdductCharge);
                    }
                    if (libraries.TryLoadSpectrum(key, out spectrumInfo))
                    {
                        var spectrumInfoR = LibraryRankedSpectrumInfo.NewLibraryRankedSpectrumInfo(spectrumInfo, labelType,
                            this, settings, mods, useFilter, TransitionGroup.MAX_MATCHED_MSMS_PEAKS);
                        foreach (var rmi in spectrumInfoR.PeaksRanked)
                        {
                            var firstIon = rmi.MatchedIons.First();
                            AddRmiToTransitionRanks(transitionRanks, firstIon, rmi);
                            if (!useFilter)
                            {
                                foreach (var otherIon in rmi.MatchedIons.Skip(1))
                                {
                                    AddRmiToTransitionRanks(transitionRanks, otherIon, rmi);
                                }
                            }
                        }
                    }
                }
                // Catch and ignore file access exceptions
                catch (IOException) {}
                catch (UnauthorizedAccessException) {}
                catch (ObjectDisposedException) {}
            }
        }

        private static void AddRmiToTransitionRanks(Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> transitionRanks, MatchedFragmentIon firstIon, LibraryRankedSpectrumInfo.RankedMI rmi)
        {
            LibraryRankedSpectrumInfo.RankedMI existing;
            if (!transitionRanks.TryGetValue(firstIon.PredictedMz, out existing))
            {
                transitionRanks.Add(firstIon.PredictedMz, rmi);
            }
            else if (rmi.HasAnnotations)
            {
                // Combine annotations
                var combined = new List<SpectrumPeakAnnotation>(existing.Annotations);
                combined.AddRange(rmi.Annotations);
                transitionRanks[firstIon.PredictedMz] = existing.ChangeAnnotations(combined);
                Assume.AreNotEqual(existing, transitionRanks[firstIon.PredictedMz]); 
            }
        }

        public override string AuditLogText
        {
            get { return TransitionGroupTreeNode.GetLabel(TransitionGroup, PrecursorMz, string.Empty); }
        }
    }
}