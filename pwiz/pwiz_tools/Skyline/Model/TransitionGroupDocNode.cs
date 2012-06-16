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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class TransitionGroupDocNode : DocNodeParent
    {
        public const int MIN_DOT_PRODUCT_TRANSITIONS = 4;
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
                   null,
                   children ?? new TransitionDocNode[0],
                   children == null)
        {
        }

        public TransitionGroupDocNode(TransitionGroup id,
                                      Annotations annotations,
                                      SrmSettings settings,
                                      ExplicitMods mods,
                                      SpectrumHeaderInfo libInfo,
                                      Results<TransitionGroupChromInfo> results,
                                      TransitionDocNode[] children,
                                      bool autoManageChildren)
            : base(id, annotations, children, autoManageChildren)
        {
            if (settings != null)
            {
                IsotopeDistInfo isotopeDist;
                PrecursorMz = CalcPrecursorMZ(settings, mods, out isotopeDist);
                IsotopeDist = isotopeDist;
                RelativeRT = CalcRelativeRT(settings, mods);
            }
            LibInfo = libInfo;
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
        }

        public TransitionGroup TransitionGroup { get { return (TransitionGroup) Id; }}

        public IEnumerable<TransitionDocNode> Transitions { get { return Children.Cast<TransitionDocNode>(); } }

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

        public bool IsLight { get { return TransitionGroup.LabelType.IsLight; } }

        public RelativeRT RelativeRT { get; private set; }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.precursor; } }

        public double PrecursorMz { get; private set; }

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

        public class ScheduleTimes        
        {
            public float CenterTime { get; set; }
            public float Width { get; set; }
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
            SrmDocument document, ExportSchedulingAlgorithm algorithm, int? replicateNum)
        {
            var enumScheduleTimes = schedulingGroups.Select(nodeGroup =>
                    nodeGroup.GetSchedulingPeakTimes(document, algorithm, replicateNum))
                .Where(scheduleTimes => scheduleTimes != null)
                .ToArray();
            if (enumScheduleTimes.Length < 2)
                return enumScheduleTimes.FirstOrDefault();

            return new ScheduleTimes
                       {
                           CenterTime = enumScheduleTimes.Average(st => st.CenterTime)
                       };
        }

        public ScheduleTimes GetSchedulingPeakTimes(SrmDocument document, ExportSchedulingAlgorithm algorithm, int? replicateNum)
        {
            if (!HasResults)
                return null;

            int valCount = 0;
            double valTotal = 0;
            // Try to get a scheduling time from non-optimization data, unless this
            // document contains only optimization data.  This is because optimization
            // data may have been taken under completely different chromatographic
            // conditions.
            int valCountOpt = 0;
            double valTotalOpt = 0;
            ScheduleTimes scheduleTimes = new ScheduleTimes();

            // CONSIDER:  Need to set a width for algorithms other than trends?
            if (!replicateNum.HasValue || algorithm == ExportSchedulingAlgorithm.Average)
            {
                // Sum the center times for averaging
                for (int i = 0; i < Results.Count; i++)
                {
                    var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[i];
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
                if (valCount == 0)
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
                            deltaBest = deltaBestCompare;
                        }
                        else
                        {
                            valCountOpt = valCountTmp;
                            valTotalOpt = valTotalTmp;
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
                return scheduleTimes;
            }
            // If only optimization was found, then use it.
            else if (valTotalOpt != 0)
            {
                scheduleTimes.CenterTime = (float) (valTotalOpt/valCountOpt);
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
            var calc = settings.GetPrecursorCalc(labelType, mods);
            double massH = calc.GetPrecursorMass(seq);
            double mz = SequenceMassCalc.GetMZ(massH, charge);
            if (TransitionGroup.DecoyMassShift.HasValue)
                mz += TransitionGroup.DecoyMassShift.Value;

            isotopeDist = null;
            var fullScan = settings.TransitionSettings.FullScan;
            if (fullScan.IsHighResPrecursor)
            {
                var massDist = calc.GetMzDistribution(seq, charge, fullScan.IsotopeAbundances);
                isotopeDist = new IsotopeDistInfo(massDist, massH, charge,
                    settings.TransitionSettings.FullScan.GetPrecursorFilterWindow,
                    // Centering resolution must be inversely proportional to charge state
                    // High charge states can bring major peaks close enough toghether to
                    // cause them to be combined and centered in isotope distribution valleys
                    TransitionFullScan.ISOTOPE_PEAK_CENTERING_RES / (1 + charge/15.0),
                    TransitionFullScan.MIN_ISOTOPE_PEAK_ABUNDANCE);
            }
            return mz;
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

        public TransitionGroupDocNode ChangeSettings(SrmSettings settingsNew, ExplicitMods mods, SrmSettingsDiff diff)
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

            if (diff.DiffTransitionGroupProps || diff.DiffTransitions || diff.DiffTransitionProps)
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
                foreach (TransitionDocNode nodeTran in TransitionGroup.GetTransitions(settingsNew, mods,
                        precursorMz, isotopeDist, libInfo, transitionRanks, true))
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
                            var annotations = nodeTranResult.Annotations;
                            var losses = nodeTranResult.Losses;
                            double massH = settingsNew.GetFragmentMass(TransitionGroup.LabelType, mods, tran, isotopeDist);
                            var isotopeDistInfo = losses == null ? TransitionDocNode.GetIsotopeDistInfo(tran, isotopeDist) : null;
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
                        childrenNew.Add(nodeTranResult);
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
                        // Discard isotope transitions which are no longer valid
                        if (!TransitionDocNode.IsValidIsotopeTransition(tran, isotopeDist))
                            continue;
                        double massH = settingsNew.GetFragmentMass(TransitionGroup.LabelType, mods, tran, isotopeDist);
                        var isotopeDistInfo = losses == null ? TransitionDocNode.GetIsotopeDistInfo(tran, isotopeDist) : null;
                        var info = isotopeDistInfo == null ? TransitionDocNode.GetLibInfo(tran, Transition.CalcMass(massH, losses), transitionRanks) : null;
                        Helpers.AssignIfEquals(ref info, nodeTransition.LibInfo);
                        if (!ReferenceEquals(info, nodeTransition.LibInfo))
                            dotProductChange = true;

                        var nodeNew = new TransitionDocNode(tran, annotations, losses,
                            massH, isotopeDistInfo, info, results);

                        Helpers.AssignIfEquals(ref nodeNew, nodeTransition);
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
                result = (TransitionGroupDocNode) ChangeId(new TransitionGroup(parent.Peptide,
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
                if (!Equals(((TransitionDocNode)children1[i]).Key, ((TransitionDocNode)children2[i]).Key))
                    return false;
            }
            return true;
        }

        private Dictionary<TransitionLossKey, DocNode> CreateTransitionLossToChildMap()
        {
            return Children.ToDictionary(child => ((TransitionDocNode) child).Key);
        }

        public TransitionGroupDocNode UpdateResults(SrmSettings settingsNew, SrmSettingsDiff diff,
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
                        select ((TransitionDocNode)child).Key);
                }

                float mzMatchTolerance = (float)settingsNew.TransitionSettings.Instrument.MzMatchTolerance;
                var resultsCalc = new TransitionGroupResultsCalculator(settingsNew, this, dictChromIdIndex);
                var measuredResults = settingsNew.MeasuredResults;
                foreach (var chromatograms in measuredResults.Chromatograms)
                {
                    ChromatogramGroupInfo[] arrayChromInfo;
                    // Check if this object has existing results information
                    int iResultOld;
                    if (!dictChromIdIndex.TryGetValue(chromatograms.Id.GlobalIndex, out iResultOld))
                        iResultOld = -1;
                    else
                    {
                        Debug.Assert(settingsOld != null && settingsOld.HasResults);

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
                    Dictionary<int, TransitionChromInfo> dictUserSetInfoBest =
                        (setTranPrevious != null && iResultOld != -1)
                             ? FindBestUserSetInfo(nodePrevious, iResultOld)
                             : null;
                    
                    bool loadPoints = (dictUserSetInfoBest != null);
                    if (measuredResults.TryLoadChromatogram(chromatograms, this, mzMatchTolerance, loadPoints, out arrayChromInfo))
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
                        for (int j = 0; j < countGroupInfos; j++)
                            fileIds[j] = chromatograms.FindFile(arrayChromInfo[j]);

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
                                    bool userSet = false;
                                    var chromInfo = FindChromInfo(results, fileId, step);
                                    if (chromInfo == null || !chromInfo.UserSet)
                                    {
                                        ChromPeak peak = ChromPeak.EMPTY;
                                        if (info != null)
                                        {
                                            // If the peak boundaries have been set by the user, and this transition
                                            // was not present previously, make sure it gets the same peak boundaries.
                                            TransitionChromInfo chromInfoBest;
                                            if (dictUserSetInfoBest != null &&
                                                    dictUserSetInfoBest.TryGetValue(fileId.GlobalIndex, out chromInfoBest) &&
                                                    !setTranPrevious.Contains(nodeTran.Key))
                                            {
                                                int startIndex = info.IndexOfNearestTime(chromInfoBest.StartRetentionTime);
                                                int endIndex = info.IndexOfNearestTime(chromInfoBest.EndRetentionTime);
                                                ChromPeak.FlagValues flags = 0;
                                                if (settingsNew.MeasuredResults.IsTimeNormalArea)
                                                    flags = ChromPeak.FlagValues.time_normalized;
                                                peak = info.CalcPeak(startIndex, endIndex, flags);
                                                userSet = true;
                                            }
                                            // Otherwize use the best peak chosen at import time
                                            else
                                            {
                                                if (info.BestPeakIndex != -1)
                                                    peak = info.GetPeak(info.BestPeakIndex);
                                                if (!integrateAll && peak.IsForcedIntegration)
                                                    peak = ChromPeak.EMPTY;
                                            }
                                        }

                                        // Avoid creating new info objects that represent the same data
                                        // in use before.
                                        if (chromInfo == null || !chromInfo.Equivalent(fileId, step, peak))
                                        {
                                            int ratioCount = settingsNew.PeptideSettings.Modifications.InternalStandardTypes.Count;
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
                    else
                    {
                        for (int iTran = 0; iTran < Children.Count; iTran++)
                            resultsCalc.AddTransitionChromInfo(iTran, null);
                    }
                }

                return resultsCalc.UpdateTransitionGroupNode(this);
            }
        }

        private static TransitionChromInfo CreateTransionChromInfo(TransitionChromInfo chromInfo, ChromFileInfoId fileId,
                                                            int step, ChromPeak peak, int ratioCount, bool userSet)
        {
            // Use the old ratio for now, and it will be corrected by the peptide,
            // if it is incorrect.
            IList<float?> ratios = chromInfo != null ? chromInfo.Ratios : new float?[ratioCount];
            Annotations annotations = chromInfo != null ? chromInfo.Annotations : Annotations.EMPTY;

            return new TransitionChromInfo(fileId, step, peak, ratios, annotations, userSet);
        }

        /// <summary>
        /// Find the <see cref="TransitionChromInfo"/> set by the user with the largest
        /// peak area for each file represented in a specific result set.
        /// </summary>
        private static Dictionary<int, TransitionChromInfo> FindBestUserSetInfo(TransitionGroupDocNode nodeGroup, int indexResult)
        {
            Dictionary<int, TransitionChromInfo> dictInfo = null;

            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                if (!nodeTran.HasResults)
                    continue;

                var chromInfoList = nodeTran.Results[indexResult];
                if (chromInfoList == null)
                    continue;

                foreach (var chromInfo in chromInfoList)
                {
                    if (chromInfo == null || !chromInfo.UserSet || chromInfo.OptimizationStep != 0)
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

// ReSharper disable UnusedMember.Local
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
// ReSharper restore UnusedMember.Local

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
            private readonly List<IList<TransitionChromInfo>>[] _arrayChromInfoSets;
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
                Debug.Assert(countTransitions > 0);
                _listResultCalcs = new List<TransitionGroupChromInfoListCalculator>();
                _arrayChromInfoSets = new List<IList<TransitionChromInfo>>[countTransitions];
                for (int iTran = 0; iTran < countTransitions; iTran++)
                    _arrayChromInfoSets[iTran] = new List<IList<TransitionChromInfo>>();
            }

            private SrmSettings Settings { get; set; }

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
                    _listResultCalcs.Add(new TransitionGroupChromInfoListCalculator(Settings,
                        iResult, transitionCount, listChromInfo));
                }
                // Add the iNext entry
                _listResultCalcs[iNext].AddChromInfoList(info);
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
                var listChromInfoLists = _arrayChromInfoSets[iTran];
                var results = Results<TransitionChromInfo>.Merge(nodeTran.Results, listChromInfoLists);
                if (Results<TransitionChromInfo>.EqualsDeep(results, nodeTran.Results))
                    return nodeTran;
                return nodeTran.ChangeResults(results);
            }

            private void RankAndCorrelateTransitions(TransitionGroupDocNode nodeGroup)
            {
                int countTransitions = _arrayChromInfoSets.Length;
                var arrayRanked = new KeyValuePair<int, TransitionChromInfo>[countTransitions];
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
                for (int i = 0; i < _listResultCalcs.Count; i++)
                {
                    for (int iInfo = 0; /* internal break */ ; iInfo++)
                    {
                        int countInfo = 0, countLibTrans = 0, countIsoTrans = 0;
                        ChromFileInfoId fileId = null;
                        int optStep = 0;
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
                                fileId = chromInfo.FileId;
                                optStep = chromInfo.OptimizationStep;
                            }

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
                            _listResultCalcs[i].SetLibInfo(fileId, optStep, peakAreas, libIntensities);
                        if (peakAreasMs != null)
                            _listResultCalcs[i].SetIsotopeDistInfo(fileId, optStep, peakAreasMs, isoProportionsMs);

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

            private static float GetSafeLibIntensity(TransitionDocNode nodeTran)
            {
                return (nodeTran.HasLibInfo ? nodeTran.LibInfo.Intensity : 0);
            }

            private static float GetSafeIsotopeDistProportion(TransitionDocNode nodeTran)
            {
                return (nodeTran.HasDistInfo ? nodeTran.IsotopeDistInfo.Proportion : 0);
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

            public void AddChromInfoList(IEnumerable<TransitionChromInfo> listInfo)
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
                        throw new InvalidDataException("Attempt to add integration information for missing file.");
                    int step = chromInfo.OptimizationStep;
                    int i = IndexOfCalc(fileOrder, step);
                    if (i >= 0)
                        Calculators[i].AddChromInfo(chromInfo);
                    else
                    {
                        var chromInfoGroup = FindChromInfo(fileId, step);
                        var calc = new TransitionGroupChromInfoCalculator(Settings,
                                                                          ResultsIndex,
                                                                          fileId,
                                                                          step,
                                                                          TransitionCount,
                                                                          chromInfo.Ratios.Count,
                                                                          chromInfoGroup);
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
                                                      TransitionGroupChromInfo chromInfo)
            {
                Settings = settings;
                ResultsIndex = resultsIndex;
                FileId = fileId;
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
            private float? BackgroundArea { get; set; }
            private int? Truncated { get; set; }
            private bool Identified { get; set; }
            private float? LibraryDotProduct { get; set; }
            private float? IsotopeDotProduct { get; set; }
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

                Debug.Assert(ReferenceEquals(info.FileId, FileId),
                             string.Format("Grouping transitions from file {0} with file {1}", info.FileIndex, FileId.GlobalIndex));
                FileId = info.FileId;
                FileOrder = Settings.MeasuredResults.Chromatograms[ResultsIndex].IndexOfId(FileId);

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
                    if (info.IsTruncated.HasValue)
                    {
                        if (!Truncated.HasValue)
                            Truncated = 0;
                        if (info.IsTruncated.Value)
                            Truncated++;
                    }
                    if (info.IsIdentified)
                        Identified = true;
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

            public void SetIsotopeDistInfo(double[] peakAreas, double[] isotopeDistProportions)
            {
                // Only do this once.
                if (IsotopeDotProduct.HasValue)
                    return;

                var statPeakAreas = new Statistics(peakAreas);
                var statIsotopeDistProportions = new Statistics(isotopeDistProportions);
                IsotopeDotProduct = (float)statPeakAreas.AngleSqrt(statIsotopeDistProportions);
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
                                                    Area,
                                                    BackgroundArea,
                                                    Ratios,
                                                    RatioStdevs,
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
                                  ChromatogramGroupInfo chromGroupInfo,
                                  double mzMatchTolerance,
                                  int indexSet,
                                  ChromFileInfoId fileId,
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
                var chromInfo = chromGroupInfo.GetTransitionInfo((float)nodeTran.Mz, (float)mzMatchTolerance);
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
                var chromInfo = chromGroupInfo.GetTransitionInfo((float)nodeTran.Mz, (float)mzMatchTolerance);
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
                var chromInfoArray = chromGroupInfo.GetAllTransitionInfo(
                    (float)nodeTran.Mz, (float)mzMatchTolerance, regression);
                // Shouldn't need to update a transition with no chrom info
                if (chromInfoArray.Length == 0)
                    listChildrenNew.Add(nodeTran.RemovePeak(indexSet, fileId));
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
                                ReferenceEquals(info.FileId, fileId) && info.OptimizationStep == step);
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
                                                              indexSet, fileId, step, peakNew, ratioCount);
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
                                  double startTime,
                                  double endTime,
                                  bool identified)
        {
            int ratioCount = settings.PeptideSettings.Modifications.InternalStandardTypes.Count;

            // Recalculate peaks based on new boundaries
            var listChildrenNew = new List<DocNode>();
            int startIndex = chromGroupInfo.IndexOfNearestTime((float)startTime);
            int endIndex = chromGroupInfo.IndexOfNearestTime((float)endTime);
            ChromPeak.FlagValues flags = 0;
            if (settings.MeasuredResults.IsTimeNormalArea)
                flags |= ChromPeak.FlagValues.time_normalized;
            if (identified)
                flags |= ChromPeak.FlagValues.contains_id;
            foreach (TransitionDocNode nodeTran in Children)
            {
                if (transition != null && !ReferenceEquals(transition, nodeTran.Transition))
                    listChildrenNew.Add(nodeTran);
                else
                {
                    var chromInfoArray = chromGroupInfo.GetAllTransitionInfo(
                        (float)nodeTran.Mz, (float)mzMatchTolerance, regression);

                    // Shouldn't need to update a transition with no chrom info
                    if (chromInfoArray.Length == 0)
                        listChildrenNew.Add(nodeTran.RemovePeak(indexSet, fileId));
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
                            nodeTranNew = (TransitionDocNode) nodeTranNew.ChangePeak(indexSet, fileId, step,
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
                    var tran = new Transition(TransitionGroup,
                                              tranMatch.IonType,
                                              tranMatch.CleavageOffset,
                                              tranMatch.MassIndex,
                                              tranMatch.Charge);
                    var losses = nodeTran.Losses;
                    // m/z, isotope distribution and library info calculated later
                    childrenNew.Add(new TransitionDocNode(tran, losses, 0, null, null));
                }
                nodeResult = (TransitionGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
            }

            // Change settings to creat auto-manage children, or calculate
            // mz values, library ranks and result matching
            return nodeResult.ChangeSettings(settings, nodePep.ExplicitMods, SrmSettingsDiff.ALL);
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
                var key = childrenNew[i].Key;
                if (!dictPepIndex.ContainsKey(key))
                    dictPepIndex[key] = i;
            }
            // Add the new children to the end, or merge when the node is already present
            foreach (TransitionDocNode nodeTran in nodeGroupMerge.Children)
            {
                int i;
                if (!dictPepIndex.TryGetValue(nodeTran.Key, out i))
                    childrenNew.Add(nodeTran);
                else if (mergeMatch != null)
                    childrenNew[i] = mergeMatch(childrenNew[i], nodeTran);
            }
            childrenNew.Sort(TransitionGroup.CompareTransitions);
            return (TransitionGroupDocNode)ChangeChildrenChecked(childrenNew.Cast<DocNode>().ToArray());
        }

        public TransitionGroupDocNode MergeUserInfo(TransitionGroupDocNode nodeGroupMerge,
            SrmSettings settings, SrmSettingsDiff diff)
        {
            var result = Merge(nodeGroupMerge, (n, nMerge) => n.MergeUserInfo(settings, nMerge));
            var annotations = Annotations.Merge(nodeGroupMerge.Annotations);
            if (!ReferenceEquals(annotations, Annotations))
                result = (TransitionGroupDocNode)result.ChangeAnnotations(annotations);
            var resultsInfo = MergeResultsUserInfo(settings, nodeGroupMerge.Results);
            if (!ReferenceEquals(resultsInfo, Results))
                result = result.ChangeResults(resultsInfo);
            return result.UpdateResults(settings, diff, this);
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
            return base.Equals(obj) &&
                   obj.PrecursorMz == PrecursorMz &&
                   Equals(obj.IsotopeDist, IsotopeDist) &&
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
                result = (result*397) ^ (IsotopeDist != null ? IsotopeDist.GetHashCode() : 0);
                result = (result*397) ^ (LibInfo != null ? LibInfo.GetHashCode() : 0);
                result = (result*397) ^ (Results != null ? Results.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }
}