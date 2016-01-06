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
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class TransitionGroupDocNode : DocNodeParent
    {
        public const int MIN_DOT_PRODUCT_TRANSITIONS = 3;
        public const int MIN_DOT_PRODUCT_MS1_TRANSITIONS = 3;

        public const int MIN_TREND_REPLICATES = 4;
        public const int MAX_TREND_REPLICATES = 6;

        /// <summary>
        /// General use constructor.  A call to <see cref="ChangeSettings"/> is expected before the
        /// node is put into real use in a document.
        /// </summary>
        /// <param name="id">The <see cref="TransitionGroup"/> identity for this node</param>
        /// <param name="children">A set of explicit children, or null if children should be auto-managed</param>
        public TransitionGroupDocNode(TransitionGroup id, TransitionDocNode[] children)
            : this(id,
                   Annotations.EMPTY,
                   null,
                   null,
                   null,
                   ExplicitTransitionGroupValues.EMPTY,
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
                PrecursorMz = CalcPrecursorMZ(settings, mods, out isotopeDist);
                IsotopeDist = isotopeDist;
                RelativeRT = CalcRelativeRT(settings, mods);
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
            PrecursorMz = precursorMz;
            IsotopeDist = isotopeDist;
            RelativeRT = relativeRT;
            LibInfo = group.LibInfo;
            Results = group.Results;
            ExplicitValues = group.ExplicitValues ?? ExplicitTransitionGroupValues.EMPTY;
        }

        public TransitionGroup TransitionGroup { get { return (TransitionGroup) Id; }}

        public IEnumerable<TransitionDocNode> Transitions { get { return Children.Cast<TransitionDocNode>(); } }

        protected override IList<DocNode> OrderedChildren(IList<DocNode> children)
        {
            if (IsCustomIon && children.Any() && !SrmDocument.IsSpecialNonProteomicTestDocNode(this) && !SrmDocument.IsConvertedFromProteomicTestDocNode(this))
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
            groupNew = groupNew ?? new TransitionGroup(parentNew ?? TransitionGroup.Peptide, TransitionGroup.CustomIon, TransitionGroup.PrecursorCharge, TransitionGroup.LabelType, false, TransitionGroup.DecoyMassShift);
            foreach (var nodeTran in Transitions)
            {
                var transition = nodeTran.Transition;
                var tranNew = new Transition(groupNew, transition.IonType, transition.CleavageOffset,
                    transition.MassIndex, transition.Charge, transition.DecoyMassShift, transition.CustomIon);
                var nodeTranNew = new TransitionDocNode(tranNew, nodeTran.Annotations, nodeTran.Losses,
                    nodeTran.GetIonMass(), nodeTran.IsotopeDistInfo, nodeTran.LibInfo, nodeTran.Results);
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

        public DocNodeCustomIon CustomIon
        {
            get { return TransitionGroup.CustomIon;  }
        }

        /// <summary>
        /// For transition lists with explicit values for CE, drift time etc
        /// </summary>
        public ExplicitTransitionGroupValues ExplicitValues { get; private set; }

        public bool IsLight { get { return TransitionGroup.LabelType.IsLight; } }

        public RelativeRT RelativeRT { get; private set; }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.precursor; } }

        public double PrecursorMz { get; private set; }

        public int PrecursorCharge { get { return TransitionGroup.PrecursorCharge; } }

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
                        if (result == null)
                            continue;
                        foreach (var chromInfo in result)
                            yield return chromInfo;
                    }
                }
            }
        }

        public IEnumerable<TransitionGroupChromInfo> GetChromInfos(int? i)
        {
            if (!i.HasValue)
                return ChromInfos;
            var chromInfos = GetSafeChromInfo(i.Value);
            if (chromInfos != null)
                return chromInfos;
            return new TransitionGroupChromInfo[0];
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
            if (result == null)
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

        public float? GetPeakArea(int i)
        {
            if (i == -1)
                return AveragePeakArea;

            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return chromInfo.Area;
        }

        public float? AveragePeakArea
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.OptimizationStep != 0
                                                              ? (float?)null
                                                              : chromInfo.Area);
            }
        }

        public float? GetIsotopeDotProduct(int i)
        {
            if (i == -1)
                return AverageIsotopeDotProduct;

            // CONSIDER: Also specify the file index?
            var result = GetSafeChromInfo(i);
            if (result == null)
                return null;
            return result.GetAverageValue(chromInfo => chromInfo.OptimizationStep == 0
                                                              ? chromInfo.IsotopeDotProduct
                                                              : (float?)null);
        }

        public float? AverageIsotopeDotProduct
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.OptimizationStep == 0
                                                              ? chromInfo.IsotopeDotProduct
                                                              : (float?)null);
            }
        }

        public float? GetLibraryDotProduct(int i)
        {
            if (i == -1)
                return AverageLibraryDotProduct;

            // CONSIDER: Also specify the file index?
            var result = GetSafeChromInfo(i);
            if (result == null)
                return null;
            return result.GetAverageValue(chromInfo => chromInfo.OptimizationStep == 0
                                                              ? chromInfo.LibraryDotProduct
                                                              : (float?)null);
        }

        public float? AverageLibraryDotProduct
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.OptimizationStep == 0
                                                              ? chromInfo.LibraryDotProduct
                                                              : (float?) null);
            }
        }

        public RatioValue GetPeakAreaRatio(int i)
        {
            return GetPeakAreaRatio(i, 0);
        }

        public RatioValue GetPeakAreaRatio(int i, int indexIS)
        {
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
            {
                return null;
            }

            return chromInfo.GetRatio(indexIS);
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
                if (result == null)
                    continue;

                foreach (var chromInfo in result)
                {
                    if (chromInfo == null ||
                            !chromInfo.StartRetentionTime.HasValue ||
                            !chromInfo.EndRetentionTime.HasValue)
                        return null;
                    // Make an array of the last 4 or 6 (depending on data available) center Times to use for linear regresson
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
            if (result == null)
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
                if (HasResults && Results.Where(l => l != null)
                                            .SelectMany(l => l)
                                            .Contains(chromInfo => chromInfo.IsUserModified))
                    return true;
                return Children.Cast<TransitionDocNode>().Contains(nodeTran => nodeTran.IsUserModified);
            }
        }

        private double CalcPrecursorMZ(SrmSettings settings, ExplicitMods mods, out IsotopeDistInfo isotopeDist)
        {
            string seq = TransitionGroup.Peptide.Sequence;
            int charge = TransitionGroup.PrecursorCharge;
            IsotopeLabelType labelType = TransitionGroup.LabelType;
            IPrecursorMassCalc calc;
            double mass, mz;
            if (IsCustomIon)
            {
                calc = settings.GetDefaultPrecursorCalc();
                mass = CustomIon.GetMass(settings.TransitionSettings.Prediction.PrecursorMassType);
                mz = BioMassCalc.CalculateIonMz(mass, charge);
            }
            else
            {
                calc = settings.GetPrecursorCalc(labelType, mods);
                mass = calc.GetPrecursorMass(seq);
                mz = SequenceMassCalc.GetMZ(mass, charge) + 
                    SequenceMassCalc.GetPeptideInterval(TransitionGroup.DecoyMassShift);
                if (TransitionGroup.DecoyMassShift.HasValue)
                    mass = SequenceMassCalc.GetMH(mz, charge);
            }

            isotopeDist = null;
            var fullScan = settings.TransitionSettings.FullScan;
            if (fullScan.IsHighResPrecursor)
            {
                MassDistribution massDist;
                if (calc == null)
                    calc = settings.GetPrecursorCalc(labelType, mods);
                if (!TransitionGroup.IsCustomIon)
                {
                    massDist = calc.GetMzDistribution(seq, charge, fullScan.IsotopeAbundances);
                    if (TransitionGroup.DecoyMassShift.HasValue)
                        massDist = ShiftMzDistribution(massDist, TransitionGroup.DecoyMassShift.Value);
                }
                else if (CustomIon.Formula != null)
                {
                    massDist = calc.GetMZDistributionFromFormula(CustomIon.Formula,
                        charge, fullScan.IsotopeAbundances);
                }
                else
                {
                    massDist = calc.GetMZDistributionSinglePoint(mz, charge);
                }
                isotopeDist = new IsotopeDistInfo(massDist, mass, !TransitionGroup.Peptide.IsCustomIon, charge,
                    settings.TransitionSettings.FullScan.GetPrecursorFilterWindow,
                    // Centering resolution must be inversely proportional to charge state
                    // High charge states can bring major peaks close enough toghether to
                    // cause them to be combined and centered in isotope distribution valleys
                    TransitionFullScan.ISOTOPE_PEAK_CENTERING_RES / (1 + charge/15.0),
                    TransitionFullScan.MIN_ISOTOPE_PEAK_ABUNDANCE);
            }
            return mz;
        }

        private static MassDistribution ShiftMzDistribution(MassDistribution massDist, int massShift)
        {
            double shift = SequenceMassCalc.GetPeptideInterval(massShift);
            return MassDistribution.NewInstance(massDist.ToDictionary(p => p.Key + shift, p => p.Value),
                massDist.MassResolution, massDist.MinimumAbundance);
        }

        private RelativeRT CalcRelativeRT(SrmSettings settings, ExplicitMods mods)
        {
            return settings.GetRelativeRT(TransitionGroup.LabelType, TransitionGroup.Peptide.Sequence, mods);
        }

        public TransitionGroupDocNode ChangePrecursorMz(SrmSettings settings, ExplicitMods mods)
        {
            return ChangeProp(ImClone(this), im =>
            {
                IsotopeDistInfo isotopeDist;
                im.PrecursorMz = CalcPrecursorMZ(settings, mods, out isotopeDist);
                // Preserve reference equality, if no change to isotope peaks
                Helpers.AssignIfEquals(ref isotopeDist, IsotopeDist);
                im.IsotopeDist = isotopeDist;
            });
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
                precursorMz = CalcPrecursorMZ(settingsNew, mods, out isotopeDist);
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
                TransitionGroup.GetLibraryInfo(settingsNew, mods, autoSelectTransitions, ref libInfo, transitionRanksLib);
            }

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
                            var losses = nodeTranResult.Losses;
                            double massH = settingsNew.GetFragmentMass(TransitionGroup.LabelType, mods, tran, isotopeDist);
                            var isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(tran, losses, isotopeDist);
                            var info = isotopeDistInfo == null ? TransitionDocNode.GetLibInfo(tran, Transition.CalcMass(massH, losses), transitionRanks) : null;
                            Helpers.AssignIfEquals(ref info, nodeTranResult.LibInfo);
                            if (!ReferenceEquals(info, nodeTranResult.LibInfo))
                                dotProductChange = true;
                            var results = nodeTranResult.Results;
                            nodeTranResult = new TransitionDocNode(tran, annotations, losses,
                                massH, isotopeDistInfo, info, results);

                            Helpers.AssignIfEquals(ref nodeTranResult, (TransitionDocNode) existing);
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
                    nodeResult = new TransitionGroupDocNode(this, precursorMz, isotopeDist, relativeRT, childrenNew);
                else
                {
                    if (precursorMz != PrecursorMz || !Equals(isotopeDist, IsotopeDist) || relativeRT != RelativeRT)
                        nodeResult = new TransitionGroupDocNode(this, precursorMz, isotopeDist, relativeRT, Children);
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
                        !ArrayUtil.EqualsDeep(modsLossNew, modsLossOld) ||
                        !Equals(settingsNew.TransitionSettings.Instrument, diff.SettingsOld.TransitionSettings.Instrument))
                    {
                        IList<DocNode> childrenNew = new List<DocNode>();
                        foreach (TransitionDocNode nodeTransition in nodeResult.Children)
                        {
                            if (nodeTransition.IsLossPossible(modsNew.MaxNeutralLosses, modsLossNew) &&
                                settingsNew.TransitionSettings.Instrument.IsMeasurable(nodeTransition.Mz, precursorMz))
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
                        // Discard isotope transitions which are no longer valid
                        if (!TransitionDocNode.IsValidIsotopeTransition(tran, isotopeDist))
                            continue;
                        double massH = settingsNew.GetFragmentMass(TransitionGroup.LabelType, mods, tran, isotopeDist);
                        var isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(tran, losses, isotopeDist);
                        var info = isotopeDistInfo == null ? TransitionDocNode.GetLibInfo(tran, Transition.CalcMass(massH, losses), transitionRanks) : null;
                        Helpers.AssignIfEquals(ref info, nodeTransition.LibInfo);
                        if (!ReferenceEquals(info, nodeTransition.LibInfo))
                            dotProductChange = true;

                        // Avoid overwriting valid transition lib info before the libraries are loaded or for decoys
                        if (libInfo != null && info == null && (IsDecoy || !settingsNew.PeptideSettings.Libraries.IsLoaded))
                            info = nodeTransition.LibInfo;
                        var nodeNew = new TransitionDocNode(tran, annotations, losses,
                            massH, isotopeDistInfo, info, results);

                        Helpers.AssignIfEquals(ref nodeNew, nodeTransition);
                        if (settingsNew.TransitionSettings.Instrument.IsMeasurable(nodeNew.Mz, precursorMz))
                            childrenNew.Add(nodeNew);
                    }

                    // Change as little as possible
                    if (!ArrayUtil.ReferencesEqual(childrenNew, Children))
                        nodeResult = new TransitionGroupDocNode(nodeResult, precursorMz, isotopeDist, relativeRT, childrenNew);
                    else if (precursorMz != PrecursorMz || !Equals(isotopeDist, IsotopeDist) || relativeRT != RelativeRT)
                        nodeResult = new TransitionGroupDocNode(nodeResult, precursorMz, isotopeDist, relativeRT, Children);
                }
                else if (diff.DiffTransitionGroupProps)
                {
                    nodeResult = new TransitionGroupDocNode(nodeResult, precursorMz, isotopeDist, relativeRT, Children);
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
            return TransitionGroup.GetTransitions(settings, this, mods, precursorMz, isotopeDist, libInfo, transitionRanks,
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
                result = (TransitionGroupDocNode) ChangeId(new TransitionGroup(parent.Peptide, CustomIon,
                    TransitionGroup.PrecursorCharge, TransitionGroup.LabelType));
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
            return Children.ToDictionary(child => ((TransitionDocNode) child).Key(this));
        }

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
                    int i = 0;
                    foreach (var chromSet in settingsOld.MeasuredResults.Chromatograms)
                        dictChromIdIndex.Add(chromSet.Id.GlobalIndex, i++);
                }

                // Store keys for previous children in a set, if the children have changed due
                // to a user action, and not simply loading (when nodePrevious may be null).
                HashSet<TransitionLossKey> setTranPrevious = null;
                if (nodePrevious != null && !AreEquivalentChildren(Children, nodePrevious.Children) &&
                    // Only necessry if children were added
                    Children.Count > nodePrevious.Children.Count)
                {
                    setTranPrevious = new HashSet<TransitionLossKey>(
                        from child in nodePrevious.Children
                        select ((TransitionDocNode)child).Key(this));
                }

                float mzMatchTolerance = (float)settingsNew.TransitionSettings.Instrument.MzMatchTolerance;
                var resultsCalc = new TransitionGroupResultsCalculator(settingsNew, this, dictChromIdIndex);
                var resultsHandler = settingsNew.PeptideSettings.Integration.ResultsHandler;
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
                var measuredResults = settingsNew.MeasuredResults;
                for (int chromIndex = 0; chromIndex < measuredResults.Chromatograms.Count; chromIndex++)
                {
                    var chromatograms = measuredResults.Chromatograms[chromIndex];

                    resultsCalc.AddSet();

                    ChromatogramGroupInfo[] arrayChromInfo;
                    // Check if this object has existing results information
                    int iResultOld;
                    if (!dictChromIdIndex.TryGetValue(chromatograms.Id.GlobalIndex, out iResultOld) ||
                        (Results != null && iResultOld >= Results.Count))
                    {
                        iResultOld = -1;
                    }
                    // But never if performing reintegration, since there will always be existing information
                    // for everything, and this will just cause reintegration to do nothing.
                    else if (resultsHandler == null)
                    {
                        Assume.IsNotNull(settingsOld);
                        Assume.IsTrue(settingsOld.HasResults);

                        // If there is existing results information, and it was set
                        // by the user, then preserve it, and skip automatic peak picking
                        var resultOld = Results != null ? Results[iResultOld] : null;
                        if (resultOld != null &&
                                // Do not reuse results, if integrate all has changed
                                integrateAll == settingsOld.TransitionSettings.Integration.IsIntegrateAll &&
                                (// Unfortunately, it is always possible that new results need
                                 // to be added from other files.  So this must be handled below.
                                 //(UserSetResults(resultOld) && setTranPrevious == null) ||
                                 // or this set of results is not yet loaded
                                 !chromatograms.IsLoaded ||
                                 // or not forcing a full recalc of all peaks, chromatograms have not
                                 // changed and the node has not otherwise changed yet.
                                 // (happens while loading results)
                                 // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                 (!diff.DiffResultsAll && settingsOld != null &&
                                  ReferenceEquals(chromatograms, settingsOld.MeasuredResults.Chromatograms[iResultOld]) &&
                                  Equals(this, nodePrevious))))
                        {
                            for (int iTran = 0; iTran < Children.Count; iTran++)
                            {
                                var nodeTran = (TransitionDocNode)Children[iTran];
                                var results = nodeTran.HasResults ? nodeTran.Results[iResultOld] : null;
                                if (results == null)
                                    resultsCalc.AddTransitionChromInfo(iTran, null);
                                else
                                    resultsCalc.AddTransitionChromInfo(iTran, results.ToArray());
                            }
                            continue;                            
                        }
                    }

                    // Check for any user set transitions in the previous node that
                    // should be used to set peak boundaries on any new nodes.
                    Dictionary<int, TransitionChromInfo> dictUserSetInfoBest = null;
                    bool missmatchedEmptyReintegrated = false;
                    if (keepUserSet && iResultOld != -1)
                    {
                        // Or we have reintegrated peaks that are not matching the current integrate all setting
                        missmatchedEmptyReintegrated = nodePrevious.IsMismatchedEmptyReintegrated(iResultOld, integrateAll);
                        if (setTranPrevious != null || missmatchedEmptyReintegrated)
                            dictUserSetInfoBest = nodePrevious.FindBestUserSetInfo(iResultOld);
                    }
                    
                    bool loadPoints = (dictUserSetInfoBest != null || GetMatchingGroups(nodePep).Any());
                    if (!measuredResults.TryLoadChromatogram(chromatograms, nodePep, this, mzMatchTolerance, loadPoints,
                                                            out arrayChromInfo))
                    {
                        for (int iTran = 0; iTran < Children.Count; iTran++)
                            resultsCalc.AddTransitionChromInfo(iTran, null);
                    }
                    else
                    {
                        // Make sure each file only appears once in the list, since downstream
                        // code has problems with multiple measurements in the same file.
                        // Most measuremenst should happen only once per replicate, meaning this
                        // if clause is an unusual case.  A race condition pre-0.7 occasionally
                        // resulted in writing precursor entries multiple times to the cache file.
                        // This code also corrects that problem by ignoring all but the first
                        // instance.
                        if (arrayChromInfo.Length > 1)
                            arrayChromInfo = arrayChromInfo.Distinct(ChromatogramGroupInfo.PathComparer).ToArray();
                        // Find the file indexes once
                        int countGroupInfos = arrayChromInfo.Length;
                        var fileIds = new ChromFileInfoId[countGroupInfos];
                        // and matching reintegration statistics, if any
                        PeakFeatureStatistics[] reintegratePeaks = resultsHandler != null
                            ? new PeakFeatureStatistics[countGroupInfos]
                            : null;
                        for (int j = 0; j < countGroupInfos; j++)
                        {
                            var fileId = chromatograms.FindFile(arrayChromInfo[j]);

                            fileIds[j] = fileId;

                            if (resultsHandler != null)
                            {
                                reintegratePeaks[j] = resultsHandler.GetPeakFeatureStatistics(
                                    nodePep.Peptide.GlobalIndex, fileId.GlobalIndex);
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
                            var nodeTran = (TransitionDocNode) Children[iTran];
                            // Use existing information, if it is still equivalent to the
                            // chosen peak.
                            var results = nodeTran.HasResults && iResultOld != -1 ?
                                nodeTran.Results[iResultOld] : null;

                            var listTranInfo = new List<TransitionChromInfo>();
                            for (int j = 0; j < countGroupInfos; j++)
                            {
                                // Get all transition chromatogram info for this file.
                                ChromatogramGroupInfo chromGroupInfo = arrayChromInfo[j];
                                ChromFileInfoId fileId = fileIds[j];
                                PeakFeatureStatistics reintegratePeak = reintegratePeaks != null ? reintegratePeaks[j] : null;

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
                                    UserSet userSet = UserSet.FALSE;
                                    var chromInfo = FindChromInfo(results, fileId, step);
                                    if (!keepUserSet || chromInfo == null || chromInfo.UserSet == UserSet.FALSE || missmatchedEmptyReintegrated)
                                    {
                                        ChromPeak peak = ChromPeak.EMPTY;
                                        if (info != null)
                                        {
                                            // If the peak boundaries have been set by the user, make sure this peak matches
                                            TransitionChromInfo chromInfoBest;
                                            TransitionGroupChromInfo chromGroupInfoMatch;
                                            if (dictUserSetInfoBest != null &&
                                                    dictUserSetInfoBest.TryGetValue(fileId.GlobalIndex, out chromInfoBest))
                                            {
                                                peak = CalcPeak(settingsNew, info, chromInfoBest);
                                                userSet = chromInfoBest.UserSet;
                                            }
                                            // Or if there is a matching peak on another precursor in the peptide
                                            else if (TryGetMatchingGroupInfo(nodePep, chromIndex, fileId, step, out chromGroupInfoMatch))
                                            {
                                                peak = CalcMatchingPeak(settingsNew, info, chromGroupInfoMatch, reintegratePeak, qcutoff, integrateAll, ref userSet);
                                            }
                                            // Otherwize use the best peak chosen at import time
                                            else
                                            {
                                                int bestIndex = GetBestIndex(info, reintegratePeak, qcutoff, ref userSet);
                                                if (bestIndex != -1)
                                                    peak = info.GetPeak(bestIndex);
                                                peak = CheckForcedPeak(peak, integrateAll);
                                            }
                                        }

                                        // Avoid creating new info objects that represent the same data
                                        // in use before.
                                        if (chromInfo == null || !chromInfo.Equivalent(fileId, step, peak) || chromInfo.UserSet != userSet)
                                        {
                                            int ratioCount = settingsNew.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
                                            chromInfo = CreateTransionChromInfo(chromInfo, fileId, step, peak, ratioCount, userSet);
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
                }

                return resultsCalc.UpdateTransitionGroupNode(this);
            }
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

        private static ChromPeak CalcPeak(SrmSettings settingsNew,
                                          ChromatogramInfo info,
                                          TransitionChromInfo chromInfoBest)
        {
            int startIndex = info.IndexOfNearestTime(chromInfoBest.StartRetentionTime);
            int endIndex = info.IndexOfNearestTime(chromInfoBest.EndRetentionTime);
            ChromPeak.FlagValues flags = 0;
            if (settingsNew.MeasuredResults.IsTimeNormalArea)
                flags = ChromPeak.FlagValues.time_normalized;
            return info.CalcPeak(startIndex, endIndex, flags);
        }

        private static ChromPeak CalcMatchingPeak(SrmSettings settingsNew,
                                                  ChromatogramInfo info,
                                                  TransitionGroupChromInfo chromGroupInfoMatch,
                                                  PeakFeatureStatistics reintegratePeak,
                                                  double qcutoff, 
                                                  bool integrateAll,
                                                  ref UserSet userSet)
        {
            int startIndex = info.IndexOfNearestTime(chromGroupInfoMatch.StartRetentionTime.Value);
            int endIndex = info.IndexOfNearestTime(chromGroupInfoMatch.EndRetentionTime.Value);
            ChromPeak.FlagValues flags = 0;
            if (settingsNew.MeasuredResults.IsTimeNormalArea)
                flags = ChromPeak.FlagValues.time_normalized;
            var peak = info.CalcPeak(startIndex, endIndex, flags);
            userSet = UserSet.MATCHED;
            var userSetBest = UserSet.FALSE;
            int bestIndex = GetBestIndex(info, reintegratePeak, qcutoff, ref userSetBest);
            if (bestIndex != -1)
            {
                var peakBest = info.GetPeak(bestIndex);
                if (peakBest.StartTime == peak.StartTime && peakBest.EndTime == peak.EndTime)
                {
                    peak = CheckForcedPeak(peakBest, integrateAll);
                    userSet = userSetBest;
                }
            }
            return peak;
        }

        private IEnumerable<TransitionGroupDocNode> GetMatchingGroups(PeptideDocNode nodePep)
        {
            if (!HasResults && RelativeRT == RelativeRT.Matching)
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    if (!ReferenceEquals(nodeGroup.TransitionGroup, TransitionGroup) &&
                            nodeGroup.HasResults &&
                            nodeGroup.RelativeRT == RelativeRT.Matching)
                        yield return nodeGroup;
                }
            }
        }

        private bool TryGetMatchingGroupInfo(PeptideDocNode nodePep, int chromIndex, ChromFileInfoId fileId, int step, out TransitionGroupChromInfo chromGroupInfoMatch)
        {
            foreach (var nodeGroup in GetMatchingGroups(nodePep))
            {
                var results = nodeGroup.Results[chromIndex];
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

        private static ChromPeak CheckForcedPeak(ChromPeak peak, bool integrateAll)
        {
            if (!integrateAll && peak.IsForcedIntegration)
                return ChromPeak.EMPTY;
            return peak;
        }

        private static TransitionChromInfo CreateTransionChromInfo(TransitionChromInfo chromInfo, ChromFileInfoId fileId,
                                                            int step, ChromPeak peak, int ratioCount, UserSet userSet)
        {
            // Use the old ratio for now, and it will be corrected by the peptide,
            // if it is incorrect.
            IList<float?> ratios = chromInfo != null ? chromInfo.Ratios : TransitionChromInfo.GetEmptyRatios(ratioCount);
            Annotations annotations = chromInfo != null ? chromInfo.Annotations : Annotations.EMPTY;

            return new TransitionChromInfo(fileId, step, peak, ratios, annotations, userSet);
        }

        /// <summary>
        /// Returns true if settings are moving to integrate all and there are empty
        /// reintegrated peaks.
        /// </summary>
        private bool IsMismatchedEmptyReintegrated(int indexResult, bool integrateAll)
        {
            foreach (TransitionDocNode nodeTran in Children)
            {
                if (!nodeTran.HasResults)
                    continue;

                var chromInfoList = nodeTran.Results[indexResult];
                if (chromInfoList == null)
                    continue;

                foreach (var chromInfo in chromInfoList)
                {
                    if (chromInfo == null || chromInfo.UserSet != UserSet.REINTEGRATED || chromInfo.OptimizationStep != 0)
                        continue;
                    if (integrateAll && chromInfo.IsEmpty)
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
                if (chromInfoList == null)
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

        private static TransitionGroupChromInfo FindGroupChromInfo(IEnumerable<TransitionGroupChromInfo> results,
                                                         ChromFileInfoId fileId, int step)
        {
            if (results != null)
            {
                return results.FirstOrDefault(chromInfo =>
                    ReferenceEquals(fileId, chromInfo.FileId) &&
                    step == chromInfo.OptimizationStep);
            }
            return null;
        }

        private static TransitionChromInfo FindChromInfo(IEnumerable<TransitionChromInfo> results,
                                                         ChromFileInfoId fileId, int step)
        {
            if (results != null)
            {
                return results.FirstOrDefault(chromInfo =>
                    ReferenceEquals(fileId, chromInfo.FileId) &&
                    step == chromInfo.OptimizationStep);
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
            private readonly TransitionGroupDocNode _nodeGroup;
            private readonly List<TransitionGroupChromInfoListCalculator> _listResultCalcs;
            private readonly TransitionChromInfoSet[] _arrayTransitionChromInfoSets;
            // Allow look-up of former result position
            private readonly Dictionary<int, int> _dictChromIdIndex;

            public TransitionGroupResultsCalculator(SrmSettings settings, 
                                                    TransitionGroupDocNode nodeGroup,                                                    
                                                    Dictionary<int, int> dictChromIdIndex)
            {
                Settings = settings;

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
                ChromInfoList<TransitionGroupChromInfo> listChromInfo = null;
                int iResult = _listResultCalcs.Count;
                if (_nodeGroup.HasResults)
                {
                    int iResultOld = GetOldPosition(iResult);
                    if (iResultOld != -1 && iResultOld < _nodeGroup.Results.Count)
                    {
                        listChromInfo = _nodeGroup.Results[iResultOld];
                    }
                }
                _listResultCalcs.Add(new TransitionGroupChromInfoListCalculator(Settings,
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
                    childrenNew.Add(UpdateTranisitionNode(nodeTran, iTran));
                }

                var listChromInfoLists = _listResultCalcs.ConvertAll(calc => calc.CalcChromInfoList());
                var results = Results<TransitionGroupChromInfo>.Merge(nodeGroup.Results, listChromInfoLists);

                var nodeGroupNew = nodeGroup;
                if (!Results<TransitionGroupChromInfo>.EqualsDeep(results, nodeGroupNew.Results))
                    nodeGroupNew = nodeGroupNew.ChangeResults(results);

                nodeGroupNew = (TransitionGroupDocNode)nodeGroupNew.ChangeChildrenChecked(childrenNew);
                return nodeGroupNew;
            }

            private TransitionDocNode UpdateTranisitionNode(TransitionDocNode nodeTran, int iTran)
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
                
                var arrayRanked = new KeyValuePair<int, TransitionChromInfo>[countTransitions];
                var arrayRankedAverage = new KeyValuePair<int, MeanArea>[countTransitions];
                for (int i = 0; i < countTransitions; i++)
                    arrayRankedAverage[i] = new KeyValuePair<int, MeanArea>(i, new MeanArea());

                bool isFullScanMs = Settings.TransitionSettings.FullScan.IsEnabledMs;
                double[] peakAreas = null, libIntensities = null;
                if (nodeGroup.HasLibInfo)
                {
                    var nodeTransMsMs = nodeGroup.GetMsMsTransitions(isFullScanMs).ToArray();
                    int countTransMsMs = nodeTransMsMs.Length;
                    if (countTransMsMs >= MIN_DOT_PRODUCT_TRANSITIONS)
                    {
                        peakAreas = new double[countTransMsMs];
                        libIntensities = new double[countTransMsMs];
                    }
                }
                double[] peakAreasMs = null, isoProportionsMs = null;
                if (nodeGroup.HasIsotopeDist)
                {
                    var nodeTransMs = nodeGroup.GetMsTransitions(isFullScanMs).ToArray();
                    int countTransMs = nodeTransMs.Length;
                    if (countTransMs >= MIN_DOT_PRODUCT_MS1_TRANSITIONS)
                    {
                        peakAreasMs = new double[countTransMs];
                        isoProportionsMs = new double[countTransMs];
                    }
                }
                for (int iChrom = 0; iChrom < _listResultCalcs.Count; iChrom++)
                {
                    foreach (var fileStep in GetTransitionFileSteps(iChrom).ToArray())
                    {
                        int countInfo = 0, countLibTrans = 0, countIsoTrans = 0;
                        ChromFileInfoId fileId = fileStep.FileId;
                        int optStep = fileStep.OptimizationStep;
                        for (int iTran = 0; iTran < countTransitions; iTran++)
                        {
                            // CONSIDER: Current TransitionChromInfo lookup is O(n^2), but on usually very small
                            //           lists.  Using a faster lookup for large lists would be slower for the
                            //           most common case.
                            var chromInfo = GetTranitionChromInfo(iTran, iChrom, fileId, optStep);
                            arrayRanked[iTran] = new KeyValuePair<int, TransitionChromInfo>(iTran, chromInfo);
                            arrayRankedAverage[iTran].Value.AddArea(GetSafeArea(chromInfo));
                            // Count non-null info
                            if (chromInfo != null)
                                countInfo++;

                            // Store information for correlation score
                            var nodeTran = (TransitionDocNode) nodeGroup.Children[iTran];
                            if (peakAreas != null && (!isFullScanMs || !nodeTran.IsMs1))
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
                                SetTransitionChromInfo(pair.Key, iChrom, fileId, optStep, pair.Value.ChangeRank(rank));
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

            private IEnumerable<FileStep> GetTransitionFileSteps(int iChrom)
            {
                return _arrayTransitionChromInfoSets
                    .Where(s => s.ChromInfoLists[iChrom] != null)
                    .SelectMany(s => s.ChromInfoLists[iChrom])
                    .Select(info => new FileStep(info.FileId, info.OptimizationStep))
                    .Distinct();
            }

            private TransitionChromInfo GetTranitionChromInfo(int iTran, int iChrom, ChromFileInfoId fileId, int optStep)
            {
                var chromInfoList = _arrayTransitionChromInfoSets[iTran].ChromInfoLists[iChrom];
                if (chromInfoList == null)
                    return null;
                return chromInfoList.FirstOrDefault(chromInfo =>
                    Equals(fileId, chromInfo.FileId) && optStep == chromInfo.OptimizationStep);
            }

            private void SetTransitionChromInfo(int iTran, int iChrom, ChromFileInfoId fileId, int optStep,
                                                TransitionChromInfo transitionChromInfo)
            {
                var chromInfoList = _arrayTransitionChromInfoSets[iTran].ChromInfoLists[iChrom];
                int chromInfoIndex = chromInfoList.IndexOf(chromInfo =>
                    Equals(fileId, chromInfo.FileId) && optStep == chromInfo.OptimizationStep);
                chromInfoList[chromInfoIndex] = transitionChromInfo;
            }

            private static float GetSafeArea(TransitionChromInfo info)
            {
                return (info != null ? info.Area : 0.0f);
            }

            private static float GetSafeRankArea(TransitionChromInfo info)
            {
                return (info != null ? info.Area : -1.0f);
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
                    return Equals(other.FileId, FileId) &&
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
                        return (FileId.GetHashCode() * 397) ^ OptimizationStep;
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
            private readonly ChromInfoList<TransitionGroupChromInfo> _listChromInfo;

            public TransitionGroupChromInfoListCalculator(SrmSettings settings,
                                                          int resultsIndex,
                                                          int transitionCount,
                                                          ChromInfoList<TransitionGroupChromInfo> listChromInfo)
            {
                Settings = settings;
                ResultsIndex = resultsIndex;
                TransitionCount = transitionCount;

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

            public void AddChromInfoList(TransitionDocNode nodeTran, IEnumerable<TransitionChromInfo> listInfo)
            {
                if (listInfo == null)
                    return;

                foreach (var chromInfo in listInfo)
                {
                    if (chromInfo == null)
                        continue;

                    ChromFileInfoId fileId = chromInfo.FileId;
                    int fileOrder = IndexOfFileInSettings(fileId);
                    if (fileOrder == -1)
                        throw new InvalidDataException(Resources.TransitionGroupChromInfoListCalculator_AddChromInfoList_Attempt_to_add_integration_information_for_missing_file);
                    int step = chromInfo.OptimizationStep;
                    int i = IndexOfCalc(fileOrder, step);
                    if (i >= 0)
                        Calculators[i].AddChromInfo(nodeTran, chromInfo);
                    else
                    {
                        var chromInfoGroup = FindChromInfo(fileId, step);
                        var calc = new TransitionGroupChromInfoCalculator(Settings,
                                                                          ResultsIndex,
                                                                          fileId,
                                                                          step,
                                                                          TransitionCount,
                                                                          chromInfo.Ratios.Count,
                                                                          chromInfoGroup,
                                                                          ReintegrateResults,
                                                                          GetReintegratePeak(fileId, step));
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
                if (_listChromInfo == null)
                    return null;
                int iInfo = _listChromInfo.IndexOf(info => ReferenceEquals(fileId, info.FileId) && optStep == info.OptimizationStep);
                if (iInfo == -1)
                    return null;
                return _listChromInfo[iInfo];
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
                                                        int ratioCount,
                                                        TransitionGroupChromInfo chromInfo,
                                                        MProphetResultsHandler reintegrateResults,
                                                        PeakFeatureStatistics reintegratePeak)
            {
                Settings = settings;
                ResultsIndex = resultsIndex;
                FileId = fileId;
                OptimizationStep = optimizationStep;
                TransitionCount = transitionCount;
                UserSet = UserSet.FALSE;

                // Use existing ratio until it can be recalculated
                if (chromInfo != null)
                {
                    Ratios = chromInfo.Ratios;
                    Annotations = chromInfo.Annotations;
                }
                else
                {
                    Ratios = TransitionGroupChromInfo.GetEmptyRatios(ratioCount);
                    Annotations = Annotations.EMPTY;
                }

                if (reintegratePeak != null)
                {
                    // TODO: Hack! make these values not annotations
                    if (reintegrateResults.AddAnnotation && reintegratePeak.QValue.HasValue)
                        Annotations = Annotations.ChangeAnnotation(MProphetResultsHandler.AnnotationName, reintegratePeak.QValue.Value.ToString(CultureInfo.CurrentCulture));
                    if (reintegrateResults.AddMAnnotation)
                        Annotations = Annotations.ChangeAnnotation(MProphetResultsHandler.MAnnotationName, reintegratePeak.BestScore.ToString(CultureInfo.CurrentCulture));
                }
            }

            private SrmSettings Settings { get; set; }
            private int ResultsIndex { get; set; }
            public ChromFileInfoId FileId { get; private set; }
            public int FileOrder { get; private set; }
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
            private IList<RatioValue> Ratios { get; set; }
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

                ResultsCount++;

                Assume.IsTrue(ReferenceEquals(info.FileId, FileId),
                             string.Format(
                                 Resources
                                     .TransitionGroupChromInfoCalculator_AddChromInfo_Grouping_transitions_from_file__0__with_file__1__,
                                 info.FileIndex, FileId.GlobalIndex));
                FileId = info.FileId;
                FileOrder = Settings.MeasuredResults.Chromatograms[ResultsIndex].IndexOfId(FileId);

                if (!info.IsEmpty)
                {
                    if (info.Area > 0)
                        PeakCount++;

                    Area = (Area ?? 0) + info.Area;
                    BackgroundArea = (BackgroundArea ?? 0) + info.BackgroundArea;
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

                    if (info.MassError.HasValue)
                    {
                        double massError = MassError ?? 0;
                        massError += (info.MassError.Value - massError)*info.Area/Area.Value;
                        MassError = (float) massError;
                    }

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
                    if (info.IsTruncated.HasValue)
                    {
                        if (!Truncated.HasValue)
                            Truncated = 0;
                        if (info.IsTruncated.Value)
                            Truncated++;
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
                return new TransitionGroupChromInfo(FileId,
                                                    OptimizationStep,
                                                    PeakCountRatio,
                                                    RetentionTime,
                                                    StartTime,
                                                    EndTime,
                                                    Fwhm,
                                                    Area, AreaMs1, AreaFragment,
                                                    BackgroundArea, BackgroundAreaMs1, BackgroundAreaFragment,
                                                    MaxHeight,
                                                    Ratios,
                                                    MassError,
                                                    Truncated,
                                                    Identified,
                                                    LibraryDotProduct,
                                                    IsotopeDotProduct,
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
        public double GetPrecursorIonMass()
        {
            var precursorCharge = TransitionGroup.PrecursorCharge;
            var precursorMz = PrecursorMz;
            return TransitionGroup.Peptide.IsCustomIon ? BioMassCalc.CalculateIonMassFromMz(precursorMz, precursorCharge) : SequenceMassCalc.GetMH(precursorMz, precursorCharge);
        }

        /// <summary>
        /// Return precursor's neutral mass rounded for XML I/O
        /// </summary>
        public double GetPrecursorIonPersistentNeutralMass()
        {
            double ionMass = GetPrecursorIonMass();
            return TransitionGroup.Peptide.IsCustomIon ? Math.Round(ionMass, SequenceMassCalc.MassPrecision) : SequenceMassCalc.PersistentNeutral(ionMass);
        }

        public class CustomIonPrecursorComparer : IComparer<TransitionGroupDocNode>
        {
            public int Compare(TransitionGroupDocNode left, TransitionGroupDocNode right)
            {
                var test = Peptide.CompareGroups(left, right);
                if (test != 0)
                    return test;
                return left.PrecursorMz.CompareTo(right.PrecursorMz);
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
                throw new InvalidDataException(string.Format(Resources.TransitionGroupDocNode_ChangePrecursorAnnotations_File_Id__0__does_not_match_any_file_in_document_,
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
                throw new InvalidDataException(string.Format(Resources.TransitionGroupDocNode_ChangePrecursorAnnotations_File_Id__0__does_not_match_any_file_in_document_, 
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
                var chromInfo = chromGroupInfo.GetTransitionInfo((float)nodeTran.Mz, (float)mzMatchTolerance);
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
                throw new ArgumentOutOfRangeException(string.Format(Resources.TransitionGroupDocNode_ChangePeak_No_peak_found_at__0__, retentionTime));
            // Calculate extents of the peaks being added
            double startMin = double.MaxValue, endMax = double.MinValue;
            foreach (TransitionDocNode nodeTran in Children)
            {
                var chromInfo = chromGroupInfo.GetTransitionInfo((float)nodeTran.Mz, (float)mzMatchTolerance);
                if (chromInfo == null)
                    continue;
                ChromPeak peakNew = chromInfo.GetPeak(indexPeakBest);
                if (peakNew.IsEmpty)
                    continue;
                startMin = Math.Min(startMin, peakNew.StartTime);
                endMax = Math.Max(endMax, peakNew.EndTime);
            }
            // Update all transitions with the new information
            var listChildrenNew = new List<DocNode>();
            foreach (TransitionDocNode nodeTran in Children)
            {
                var chromInfoArray = chromGroupInfo.GetAllTransitionInfo(
                    (float)nodeTran.Mz, (float)mzMatchTolerance, regression);
                // Shouldn't need to update a transition with no chrom info
                if (chromInfoArray.Length == 0)
                    listChildrenNew.Add(nodeTran.RemovePeak(indexSet, fileId, userSet));
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
                        nodeTranNew = (TransitionDocNode) nodeTranNew.ChangePeak(
                                                              indexSet, fileId, step, peakNew, ratioCount, userSet);
                    }
                    listChildrenNew.Add(nodeTranNew);
                }
            }
            return ChangeChildrenChecked(listChildrenNew);
        }

        public DocNode ChangePeak(SrmSettings settings,
                                  ChromatogramGroupInfo chromGroupInfo,
                                  double mzMatchTolerance,
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
                throw new ArgumentException(string.Format(Resources.TransitionGroupDocNode_ChangePeak_Missing_Start_Time_in_Change_Peak));
            if (startTime != null && endTime == null)
                throw new ArgumentException(string.Format(Resources.TransitionGroupDocNode_ChangePeak_Missing_End_Time_In_Change_Peak));

            int ratioCount = settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;

            // Recalculate peaks based on new boundaries
            var listChildrenNew = new List<DocNode>();
            ChromPeak.FlagValues flags = 0;
            if (settings.MeasuredResults.IsTimeNormalArea)
                flags |= ChromPeak.FlagValues.time_normalized;
            if (identified != PeakIdentification.FALSE)
                flags |= ChromPeak.FlagValues.contains_id;
            if (identified == PeakIdentification.ALIGNED)
                flags |= ChromPeak.FlagValues.used_id_alignment;
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
                        if (existingChromInfos != null && existingChromInfos.All(chromInfo => chromInfo.IsEmpty))
                        {
                            listChildrenNew.Add(nodeTran);
                            continue;
                        }
                    }
                }
                var chromInfoArray = chromGroupInfo.GetAllTransitionInfo(
                    (float)nodeTran.Mz, (float)mzMatchTolerance, regression);

                // Shouldn't need to update a transition with no chrom info
                // Also if startTime is null, remove the peak
                if (chromInfoArray.Length == 0 || startTime==null)
                    listChildrenNew.Add(nodeTran.RemovePeak(indexSet, fileId, userSet));
                else
                {
                    // CONSIDER: Do this more efficiently?  Only when there is opimization
                    //           data will the loop execute more than once.
                    int startIndex = chromGroupInfo.IndexOfNearestTime((float)startTime);
                    int endIndex = chromGroupInfo.IndexOfNearestTime((float)endTime);
                    int numSteps = chromInfoArray.Length/2;
                    var nodeTranNew = nodeTran;
                    for (int i = 0; i < chromInfoArray.Length; i++)
                    {
                        var chromInfo = chromInfoArray[i];
                        int step = i - numSteps;
                        nodeTranNew = (TransitionDocNode) nodeTranNew.ChangePeak(indexSet, fileId, step,
                                                                                    chromInfo.CalcPeak(startIndex, endIndex, flags), ratioCount, userSet);
                    }
                    listChildrenNew.Add(nodeTranNew);
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

            var childrenNew = new List<DocNode>();
            foreach (TransitionDocNode nodeTran in nodeGroupSynch.Children)
            {
                var tranMatch = nodeTran.Transition;
                var tran = new Transition(TransitionGroup,
                                            tranMatch.IonType,
                                            tranMatch.CleavageOffset,
                                            tranMatch.MassIndex,
                                            tranMatch.Charge,
                                            tranMatch.DecoyMassShift,
                                            tranMatch.CustomIon);
                var losses = nodeTran.Losses;
                // m/z, isotope distribution and library info calculated later
                var nodeTranNew = new TransitionDocNode(tran, losses, 0, null, null);
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

            var dictFileIdToChromInfo = results.Where(l => l != null).SelectMany(l => l)
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
                        listChromInfo = new List<TransitionGroupChromInfo>();
                        if (chromInfoList != null)
                            listChromInfo.AddRange(chromInfoList);
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
            if (ArrayUtil.ReferencesEqual(listResults, Results))
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
                        Equals(obj.CustomIon, CustomIon) &&
                        Equals(obj.ExplicitValues, ExplicitValues);
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
                result = (result*397) ^ (CustomIon != null ? CustomIon.GetHashCode() : 0);
                return result;
            }
        }

        public override string ToString()
        {
            return TextUtil.SpaceSeparate(TransitionGroup.Peptide.ToString(), TransitionGroup.ToString());
        }

        #endregion

    }
}