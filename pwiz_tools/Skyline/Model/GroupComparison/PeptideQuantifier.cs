using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class PeptideQuantifier
    {
        private readonly Lazy<NormalizationData> _normalizationData;
        public PeptideQuantifier(Lazy<NormalizationData> normalizationData, PeptideGroup peptideGroup, PeptideDocNode peptideDocNode,
            QuantificationSettings quantificationSettings)
        {
            PeptideGroup = peptideGroup;
            PeptideDocNode = peptideDocNode;
            QuantificationSettings = quantificationSettings;
            _normalizationData = normalizationData;
        }

        public PeptideQuantifier(Lazy<NormalizationData> normalizationData,
            PeptideGroupDocNode peptideGroupDocNode, PeptideDocNode peptideDocNode,
            QuantificationSettings quantificationSettings) : this(normalizationData,
            peptideGroupDocNode.PeptideGroup, peptideDocNode, quantificationSettings)
        {
        }

        public static PeptideQuantifier GetPeptideQuantifier(Lazy<NormalizationData> getNormalizationDataFunc, SrmSettings srmSettings, PeptideGroup peptideGroup, PeptideDocNode peptide)
        {
            var mods = srmSettings.PeptideSettings.Modifications;
            // Quantify on all label types which are not internal standards.
            ICollection<IsotopeLabelType> labelTypes = ImmutableList.ValueOf(mods.GetModificationTypes()
                .Except(mods.InternalStandardTypes));
            return new PeptideQuantifier(getNormalizationDataFunc, peptideGroup, peptide, srmSettings.PeptideSettings.Quantification)
            {
                MeasuredLabelTypes = labelTypes,
                IncludeTruncatedPeaks = srmSettings.TransitionSettings.Instrument.TriggeredAcquisition
            };
        }

        public static PeptideQuantifier GetPeptideQuantifier(SrmDocument document, PeptideGroup peptideGroup,
            PeptideDocNode peptide)
        {

            return GetPeptideQuantifier(NormalizationData.LazyNormalizationData(document), document.Settings, peptideGroup, peptide);
        }
        public static PeptideQuantifier GetPeptideQuantifier(Lazy<NormalizationData> getNormalizationDataFunc, SrmSettings settings, PeptideGroupDocNode peptideGroupDocNode,
            PeptideDocNode peptide)
        {
            return GetPeptideQuantifier(getNormalizationDataFunc, settings, peptideGroupDocNode.PeptideGroup, peptide);
        }

        public static PeptideQuantifier GetPeptideQuantifier(SrmDocument document,
            PeptideGroupDocNode peptideGroupDocNode, PeptideDocNode peptideDocNode)
        {
            return GetPeptideQuantifier(document, peptideGroupDocNode.PeptideGroup, peptideDocNode);
        }

        public PeptideGroup  PeptideGroup  { get; private set; }
        public PeptideDocNode PeptideDocNode {get; private set; }
        public QuantificationSettings QuantificationSettings { get; private set; }

        public NormalizationMethod NormalizationMethod
        {
            get
            {
                return PeptideDocNode.NormalizationMethod ?? QuantificationSettings.NormalizationMethod;
            }
        }
        public ICollection<IsotopeLabelType> MeasuredLabelTypes { get; set; }

        public double? QValueCutoff { get; set; }

        public bool IncludeTruncatedPeaks { get; set; }

        public IsotopeLabelType RatioLabelType
        {
            get
            {
                NormalizationMethod.RatioToLabel ratioToLabel = NormalizationMethod as NormalizationMethod.RatioToLabel;
                if (ratioToLabel == null)
                {
                    return null;
                }
                return new IsotopeLabelType(ratioToLabel.IsotopeLabelTypeName, 0);
            }
        }

        public int? MsLevel { get { return QuantificationSettings.MsLevel; } }

        public bool SkipTransitionGroup(TransitionGroupDocNode transitionGroupDocNode)
        {
            if (transitionGroupDocNode.IsDecoy)
            {
                return true;
            }
            if (null != MeasuredLabelTypes)
            {
                if (!MeasuredLabelTypes.Contains(transitionGroupDocNode.TransitionGroup.LabelType))
                {
                    return true;
                }
            }
            if (NormalizationMethod is NormalizationMethod.RatioToLabel)
            {
                if (Equals(((NormalizationMethod.RatioToLabel) NormalizationMethod).IsotopeLabelTypeName,
                    transitionGroupDocNode.TransitionGroup.LabelType.Name))
                {
                    return true;
                }
            }
            return false;
        }

        public bool SkipTransition(SrmSettings settings, TransitionDocNode transitionDocNode)
        {
            if (!transitionDocNode.IsQuantitative(settings))
            {
                return true;
            }
            if (MsLevel.HasValue)
            {
                if (MsLevel == 1)
                {
                    return !transitionDocNode.IsMs1;
                }
                return transitionDocNode.IsMs1;
            }
            return false;
        }

        public IDictionary<IdentityPath, Quantity> GetTransitionIntensities(SrmSettings srmSettings, int replicateIndex, bool treatMissingAsZero)
        {
            var quantities = new Dictionary<IdentityPath, Quantity>();
            var transitionsToNormalizeAgainst = GetTransitionsToNormalizeAgainst(srmSettings, PeptideDocNode, replicateIndex);
            foreach (var precursor in PeptideDocNode.TransitionGroups)
            {
                if (SkipTransitionGroup(precursor))
                {
                    continue;
                }
                foreach (var transition in precursor.Transitions)
                {
                    if (SkipTransition(srmSettings, transition))
                    {
                        continue;
                    }
                    var quantity = GetTransitionQuantity(srmSettings, transitionsToNormalizeAgainst, NormalizationMethod, replicateIndex, precursor,
                        transition, treatMissingAsZero);
                    if (null != quantity)
                    {
                        IdentityPath transitionIdentityPath = new IdentityPath(PeptideGroup,
                            PeptideDocNode.Peptide, precursor.TransitionGroup, transition.Transition);
                        quantities.Add(transitionIdentityPath, quantity);
                    }
                }
            }
            return quantities;
        }

        public double GetIsotopologArea(SrmSettings settings, int replicateIndex, IsotopeLabelType labelType)
        {
            double totalArea = 0;
            var normalizationMethod = NormalizationMethod;
            if (normalizationMethod is NormalizationMethod.RatioToLabel)
            {
                normalizationMethod = NormalizationMethod.NONE;
            }
            foreach (var precursor in PeptideDocNode.TransitionGroups)
            {
                if (!Equals(labelType, precursor.LabelType))
                {
                    continue;
                }
                foreach (var transition in precursor.Transitions)
                {
                    if (SkipTransition(settings, transition))
                    {
                        continue;
                    }
                    var quantity = GetTransitionQuantity(settings, null, normalizationMethod, replicateIndex, precursor,
                        transition, false);
                    if (quantity != null)
                    {
                        totalArea += quantity.Intensity / quantity.Denominator;
                    }
                }
            }
            return totalArea;
        }

        public double? GetQualitativeIonRatio(SrmSettings settings, TransitionGroupDocNode precursor, int replicateIndex)
        {
            double numerator = 0;
            int numeratorCount = 0;
            double denominator = 0;
            int denominatorCount = 0;
            foreach (var transition in precursor.Transitions)
            {
                var quantity = GetTransitionQuantity(settings, null, NormalizationMethod.NONE, replicateIndex,
                    precursor, transition, false);
                if (false == quantity?.Truncated)
                {
                    double value = quantity.Intensity / quantity.Denominator;
                    if (transition.ExplicitQuantitative)
                    {
                        denominator += value;
                        denominatorCount++;
                    }
                    else
                    {
                        numerator += value;
                        numeratorCount++;
                    }
                }
            }

            if (numeratorCount == 0 || denominatorCount == 0)
            {
                return null;
            }

            return numerator / denominator;
        }

        public PeptideQuantifier WithQuantifiableTransitions(
            IEnumerable<IdentityPath> quantifiableTransitionIdentityPaths)
        {
            ICollection<IdentityPath> identityPathSet = quantifiableTransitionIdentityPaths as ICollection<IdentityPath> ??
                                                        quantifiableTransitionIdentityPaths.ToHashSet();
            if (identityPathSet.Count > 1 && !(identityPathSet is HashSet<IdentityPath>))
            {
                identityPathSet = identityPathSet.ToHashSet();
            }
            var newTransitionGroups = new List<TransitionGroupDocNode>();
            foreach (var transitionGroupDocNode in PeptideDocNode.TransitionGroups)
            {
                if (SkipTransitionGroup(transitionGroupDocNode))
                {
                    newTransitionGroups.Add(transitionGroupDocNode);
                    continue;
                }

                var newTransitions = new List<TransitionDocNode>();
                foreach (var transitionDocNode in transitionGroupDocNode.Transitions)
                {
                    var identityPath = new IdentityPath(PeptideGroup,
                        PeptideDocNode.Peptide, transitionGroupDocNode.TransitionGroup,
                        transitionDocNode.Transition);
                    newTransitions.Add(transitionDocNode.ChangeQuantitative(identityPathSet.Contains(identityPath)));
                }
                newTransitionGroups.Add((TransitionGroupDocNode) transitionGroupDocNode.ChangeChildren(newTransitions.ToArray()));
            }

            var newPeptideDocNode = (PeptideDocNode) PeptideDocNode.ChangeChildren(newTransitionGroups.ToArray());
            return new PeptideQuantifier(_normalizationData, PeptideGroup, newPeptideDocNode,
                QuantificationSettings);
        }

        public PeptideQuantifier WithQuantificationSettings(QuantificationSettings quantificationSettings)
        {
            return new PeptideQuantifier(_normalizationData, PeptideGroup, PeptideDocNode,
                quantificationSettings);
        }

        public PeptideQuantifier MakeAllTransitionsQuantitative()
        {
            var allTransitionIdentityPaths = PeptideDocNode.TransitionGroups.SelectMany(tg =>
                tg.Transitions.Select(t => new IdentityPath(PeptideGroup, PeptideDocNode.Peptide,
                    tg.TransitionGroup, t.Transition))).ToHashSet();
            return WithQuantifiableTransitions(allTransitionIdentityPaths);
        }

        private Quantity GetTransitionQuantity(
            SrmSettings srmSettings,
            IDictionary<PeptideDocNode.TransitionKey, TransitionChromInfo> peptideStandards,
            NormalizationMethod normalizationMethod,
            int replicateIndex,
            TransitionGroupDocNode transitionGroup, TransitionDocNode transition,
            bool treatMissingAsZero)
        {
            if (null == transition.Results)
            {
                return null;
            }
            if (replicateIndex >= transition.Results.Count)
            {
                return null;
            }
            var chromInfos = transition.Results[replicateIndex];
            if (chromInfos.IsEmpty)
            {
                return null;
            }
            var chromInfo = GetTransitionChromInfo(transition, replicateIndex);
            if (null == chromInfo)
            {
                return null;
            }
            double? normalizedArea = GetArea(treatMissingAsZero, QValueCutoff, true, transitionGroup, transition, replicateIndex, chromInfo);
            if (!normalizedArea.HasValue)
            {
                return null;
            }

            double denominator = 1.0;
            bool truncated = false;
            if (null != peptideStandards)
            {
                if (QuantificationSettings.SimpleRatios)
                {
                    if (peptideStandards.Count == 0)
                    {
                        return null;
                    }

                    denominator = peptideStandards.Values.Sum(value => value.Area);
                }
                else
                {
                    TransitionChromInfo chromInfoStandard;
                    if (!peptideStandards.TryGetValue(GetRatioTransitionKey(transitionGroup, transition), out chromInfoStandard))
                    {
                        return null;
                    }
                    else
                    {
                        denominator = chromInfoStandard.Area;
                    }
                }
            }
            else
            {
                truncated = chromInfo.IsTruncated.GetValueOrDefault() && !IncludeTruncatedPeaks;
                if (Equals(normalizationMethod, NormalizationMethod.GLOBAL_STANDARDS))
                {
                    var fileInfo = srmSettings.MeasuredResults.Chromatograms[replicateIndex]
                        .GetFileInfo(chromInfo.FileId);
                    if (fileInfo == null)
                    {
                        return null;
                    }
                    denominator = srmSettings.CalcGlobalStandardArea(replicateIndex, fileInfo);
                }
                else if (normalizationMethod is NormalizationMethod.RatioToSurrogate)
                {
                    denominator =  ((NormalizationMethod.RatioToSurrogate) NormalizationMethod)
                        .GetStandardArea(srmSettings, replicateIndex, chromInfo.FileId);
                }
                else if (Equals(normalizationMethod, NormalizationMethod.EQUALIZE_MEDIANS))
                {
                    var normalizationData = _normalizationData.Value;
                    if (null == normalizationData)
                    {
                        throw new InvalidOperationException(string.Format(@"Normalization method '{0}' is not supported here.", NormalizationMethod));
                    }
                    double? medianAdjustment = normalizationData.GetLog2Median(replicateIndex, chromInfo.FileId) 
                        - normalizationData.GetMedianLog2Median();
                    if (!medianAdjustment.HasValue)
                    {
                        return null;
                    }
                    normalizedArea /= Math.Pow(2.0, medianAdjustment.Value);
                }
                else if (Equals(normalizationMethod, NormalizationMethod.TIC))
                {
                    var factor = srmSettings.GetTicNormalizationDenominator(replicateIndex, chromInfo.FileId);
                    if (!factor.HasValue)
                    {
                        return null;
                    }
                    denominator = factor.Value;
                }
            }
            return new Quantity(normalizedArea.Value, denominator, truncated);
        }

        private TransitionChromInfo GetTransitionChromInfo(TransitionDocNode transitionDocNode, int replicateIndex)
        {
            if (null == transitionDocNode.Results || replicateIndex < 0 ||
                replicateIndex >= transitionDocNode.Results.Count)
            {
                return null;
            }
            var chromInfos = transitionDocNode.Results[replicateIndex];
            if (chromInfos.IsEmpty)
            {
                return null;
            }
            foreach (var chromInfo in chromInfos)
            {
                if (0 != chromInfo.OptimizationStep)
                {
                    continue;
                }
                return chromInfo;
            }
            return null;
        }

        private Dictionary<PeptideDocNode.TransitionKey, TransitionChromInfo> GetTransitionsToNormalizeAgainst(
            SrmSettings settings, PeptideDocNode peptideDocNode, int replicateIndex)
        {
            NormalizationMethod.RatioToLabel ratioToLabel = NormalizationMethod as NormalizationMethod.RatioToLabel;
            if (ratioToLabel == null)
            {
                return null;
            }
            var result = new Dictionary<PeptideDocNode.TransitionKey, TransitionChromInfo>();
            foreach (var transitionGroup in peptideDocNode.TransitionGroups)
            {
                if (!Equals(ratioToLabel.IsotopeLabelTypeName, transitionGroup.TransitionGroup.LabelType.Name))
                {
                    continue;
                }
                foreach (var transition in transitionGroup.Transitions)
                {
                    if (!transition.IsQuantitative(settings))
                    {
                        continue;
                    }
                    if (null == transition.Results || transition.Results.Count <= replicateIndex)
                    {
                        continue;
                    }
                    var chromInfoList = transition.Results[replicateIndex];
                    if (chromInfoList.IsEmpty)
                    {
                        continue;
                    }
                    var chromInfo = chromInfoList.FirstOrDefault(chrom => 0 == chrom.OptimizationStep);
                    if (null != chromInfo && !chromInfo.IsEmpty)
                    {
                        result[GetRatioTransitionKey(transitionGroup, transition)] = chromInfo;
                    }
                }
            }
            return result;
        }

        private PeptideDocNode.TransitionKey GetRatioTransitionKey(TransitionGroupDocNode transitionGroup, TransitionDocNode transitionDocNode)
        {
            return new PeptideDocNode.TransitionKey(transitionGroup, transitionDocNode.Key(transitionGroup), RatioLabelType);
        }

        public double? SumQuantities(IEnumerable<Quantity> quantities)
        {
            return SumQuantities(quantities, QuantificationSettings.ChangeNormalizationMethod(NormalizationMethod));
        }

        public AnnotatedDouble SumTransitionQuantities(ICollection<IdentityPath> completeTransitionSet,
            IDictionary<IdentityPath, Quantity> availableQuantities)
        {
            return SumTransitionQuantities(completeTransitionSet, availableQuantities,
                QuantificationSettings.ChangeNormalizationMethod(NormalizationMethod));
        }

        public static AnnotatedDouble SumTransitionQuantities(ICollection<IdentityPath> completeTransitionSet,
            IDictionary<IdentityPath, Quantity> availableQuantities, QuantificationSettings quantificationSettings)
        {
            var quantitiesToSum = availableQuantities.Where(entry => completeTransitionSet.Contains(entry.Key))
                .Select(kvp => kvp.Value).ToList();
            string error = null;
            if (quantitiesToSum.Count != completeTransitionSet.Count)
            {
                var missingTransitions =
                    completeTransitionSet.Where(idPath => !availableQuantities.ContainsKey(idPath)).ToList();
                if (missingTransitions.Count == 1)
                {
                    error = string.Format(GroupComparisonResources.PeptideQuantifier_SumTransitionQuantities_Transition___0___is_missing, missingTransitions.First().Child);
                }
                else
                {
                    error = string.Format(GroupComparisonResources.PeptideQuantifier_SumTransitionQuantities_Missing_values_for__0___1__transitions, missingTransitions.Count, completeTransitionSet.Count);
                }
            }
            else if (quantitiesToSum.Any(q => q.Truncated))
            {
                var truncatedTransitions = availableQuantities
                    .Where(kvp => completeTransitionSet.Contains(kvp.Key) && kvp.Value.Truncated).ToList();
                if (truncatedTransitions.Count > 0)
                {
                    if (truncatedTransitions.Count == 1)
                    {
                        error = string.Format(GroupComparisonResources.PeptideQuantifier_SumTransitionQuantities_Transition___0___is_truncated, truncatedTransitions[0].Key.Child);
                    }
                    else if(truncatedTransitions.Count == completeTransitionSet.Count)
                    {
                        error = string.Format(GroupComparisonResources.PeptideQuantifier_SumTransitionQuantities_All__0__peaks_are_truncated, truncatedTransitions.Count);
                    }
                    else 
                    {
                        error = string.Format(GroupComparisonResources.PeptideQuantifier_SumTransitionQuantities_Truncated_peaks_for__0___1__transitions, truncatedTransitions.Count, completeTransitionSet.Count);
                    }
                }
            }

            double? sum = SumQuantities(quantitiesToSum, quantificationSettings);
            if (sum.HasValue)
            {
                return AnnotatedDouble.WithMessage(sum.Value, error);
            }
            return null;
        }

        private static double? SumQuantities(IEnumerable<Quantity> quantities,
            QuantificationSettings quantificationSettings)
        {
            double numerator = 0;
            double denominator = 0;
            int count = 0;
            foreach (var quantity in quantities)
            {
                numerator += quantity.Intensity;
                denominator += quantity.Denominator;
                count++;
            }
            if (count == 0)
            {
                return null;
            }
            if (!quantificationSettings.SimpleRatios && quantificationSettings.NormalizationMethod is NormalizationMethod.RatioToLabel)
            {
                return numerator / denominator;
            }
            return numerator / denominator * count;

        }

        public class Quantity
        {
            public Quantity(double intensity, double denominator, bool truncated)
            {
                Intensity = intensity;
                Denominator = denominator;
                Truncated = truncated;
            }
            public double Intensity { get; private set; }
            public double Denominator { get; private set; }
            public bool Truncated { get; private set; }
        }

        public static double? GetArea(bool treatMissingAsZero, double? qValueCutoff, bool allowTruncated, TransitionGroupDocNode transitionGroup,
            TransitionDocNode transition, int replicateIndex, TransitionChromInfo chromInfo)
        {
            if (treatMissingAsZero && chromInfo.IsEmpty)
            {
                return 0;
            }
            if (chromInfo.IsEmpty)
            {
                return null;
            }

            if (!allowTruncated && chromInfo.IsTruncated.GetValueOrDefault())
            {
                return null;
            }

            if (qValueCutoff.HasValue)
            {
                TransitionGroupChromInfo transitionGroupChromInfo = FindTransitionGroupChromInfo(transitionGroup,
                    replicateIndex, chromInfo.FileId);
                if (transitionGroupChromInfo != null && transitionGroupChromInfo.QValue > qValueCutoff.Value)
                {
                    return treatMissingAsZero ? 0 : default(double?);
                }
            }
            return chromInfo.Area;
        }

        private static TransitionGroupChromInfo FindTransitionGroupChromInfo(TransitionGroupDocNode transitionGroup,
            int replicateIndex, ChromFileInfoId chromFileInfoId)
        {
            if (transitionGroup.Results == null || transitionGroup.Results.Count <= replicateIndex)
            {
                return null;
            }
            var chromInfoList = transitionGroup.Results[replicateIndex];
            if (chromInfoList.IsEmpty)
            {
                return null;
            }
            return chromInfoList.FirstOrDefault(
                chromInfo => chromInfo != null && ReferenceEquals(chromInfo.FileId, chromFileInfoId));
        }
    }
}
