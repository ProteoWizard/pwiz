using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class PeptideQuantifier
    {
        public PeptideQuantifier(PeptideGroupDocNode peptideGroup, PeptideDocNode peptideDocNode,
            QuantificationSettings quantificationSettings)
        {
            PeptideGroupDocNode = peptideGroup;
            PeptideDocNode = peptideDocNode;
            QuantificationSettings = quantificationSettings;

        }

        public static PeptideQuantifier GetPeptideQuantifier(SrmSettings srmSettings, PeptideGroupDocNode peptideGroup, PeptideDocNode peptide)
        {
            var mods = srmSettings.PeptideSettings.Modifications;
            // Quantify on all label types which are not internal standards.
            ICollection<IsotopeLabelType> labelTypes = ImmutableList.ValueOf(mods.GetModificationTypes()
                .Except(mods.InternalStandardTypes));
            return new PeptideQuantifier(peptideGroup, peptide, srmSettings.PeptideSettings.Quantification)
            {
                MeasuredLabelTypes = labelTypes
            };
        }

        public PeptideGroupDocNode PeptideGroupDocNode { get; private set; }
        public PeptideDocNode PeptideDocNode {get; private set; }
        public QuantificationSettings QuantificationSettings { get; private set; }
        public NormalizationMethod NormalizationMethod { get {return QuantificationSettings.NormalizationMethod;} }
        public ICollection<IsotopeLabelType> MeasuredLabelTypes { get; set; }
        public NormalizationData NormalizationData { get; set; }

        public IsotopeLabelType RatioLabelType
        {
            get
            {
                if (string.IsNullOrEmpty(NormalizationMethod.IsotopeLabelTypeName))
                {
                    return null;
                }
                return new IsotopeLabelType(NormalizationMethod.IsotopeLabelTypeName, 0);
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
            if (!string.IsNullOrEmpty(NormalizationMethod.IsotopeLabelTypeName))
            {
                if (Equals(NormalizationMethod.IsotopeLabelTypeName,
                    transitionGroupDocNode.TransitionGroup.LabelType.Name))
                {
                    return true;
                }
            }
            return false;
        }

        public bool SkipTransition(TransitionDocNode transitionDocNode)
        {
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
            var transitionsToNormalizeAgainst = GetTransitionsToNormalizeAgainst(PeptideDocNode, replicateIndex);
            foreach (var precursor in PeptideDocNode.TransitionGroups)
            {
                if (SkipTransitionGroup(precursor))
                {
                    continue;
                }
                foreach (var transition in precursor.Transitions)
                {
                    if (SkipTransition(transition))
                    {
                        continue;
                    }
                    var quantity = GetTransitionQuantity(srmSettings, transitionsToNormalizeAgainst, replicateIndex, precursor,
                        transition, treatMissingAsZero);
                    if (null != quantity)
                    {
                        IdentityPath transitionIdentityPath = new IdentityPath(PeptideGroupDocNode.PeptideGroup,
                            PeptideDocNode.Peptide, precursor.TransitionGroup, transition.Transition);
                        quantities.Add(transitionIdentityPath, quantity);
                    }
                }
            }
            return quantities;
        }

        private Quantity GetTransitionQuantity(
            SrmSettings srmSettings,
            IDictionary<PeptideDocNode.TransitionKey, TransitionChromInfo> peptideStandards,
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
            if (null == chromInfos)
            {
                return null;
            }
            var chromInfo = GetTransitionChromInfo(transition, replicateIndex);
            if (null == chromInfo)
            {
                return null;
            }
            double? normalizedArea = null;
            if (treatMissingAsZero && HasFalseQValue(transitionGroup, replicateIndex, chromInfo.FileId))
            {
                normalizedArea = 0;
            }
            if (!normalizedArea.HasValue)
            {
                if (chromInfo.IsEmpty)
                {
                    return null;
                }
                normalizedArea = chromInfo.Area;
            }

            double denominator = 1.0;

            if (null != peptideStandards)
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
            else
            {
                if (chromInfo.IsTruncated.GetValueOrDefault())
                {
                    return null;
                }
                if (Equals(NormalizationMethod, NormalizationMethod.GLOBAL_STANDARDS))
                {
                    denominator = srmSettings.CalcGlobalStandardArea(replicateIndex, chromInfo.FileId);
                }
                else if (Equals(NormalizationMethod, NormalizationMethod.EQUALIZE_MEDIANS))
                {
                    if (null == NormalizationData)
                    {
                        throw new InvalidOperationException(string.Format("Normalization method '{0}' is not supported here.", NormalizationMethod)); // Not L10N
                    }
                    double? median = NormalizationData.GetMedian(chromInfo.FileId, transitionGroup.TransitionGroup.LabelType);
                    if (!median.HasValue)
                    {
                        return null;
                    }
                    denominator = Math.Pow(2.0, median.Value);
                }
            }
            return new Quantity(normalizedArea.Value, denominator);
        }

        private TransitionChromInfo GetTransitionChromInfo(TransitionDocNode transitionDocNode, int replicateIndex)
        {
            if (null == transitionDocNode.Results || replicateIndex < 0 ||
                replicateIndex >= transitionDocNode.Results.Count)
            {
                return null;
            }
            var chromInfos = transitionDocNode.Results[replicateIndex];
            if (null == chromInfos)
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
            PeptideDocNode peptideDocNode, int replicateIndex)
        {
            if (string.IsNullOrEmpty(NormalizationMethod.IsotopeLabelTypeName))
            {
                return null;
            }
            var result = new Dictionary<PeptideDocNode.TransitionKey, TransitionChromInfo>();
            foreach (var transitionGroup in peptideDocNode.TransitionGroups)
            {
                if (!Equals(NormalizationMethod.IsotopeLabelTypeName, transitionGroup.TransitionGroup.LabelType.Name))
                {
                    continue;
                }
                foreach (var transition in transitionGroup.Transitions)
                {
                    if (null == transition.Results || transition.Results.Count <= replicateIndex)
                    {
                        continue;
                    }
                    var chromInfoList = transition.Results[replicateIndex];
                    if (null == chromInfoList)
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

        public static double? SumQuantities(IEnumerable<Quantity> quantities, NormalizationMethod normalizationMethod)
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
            if (!string.IsNullOrEmpty(normalizationMethod.IsotopeLabelTypeName))
            {
                return numerator/denominator;
            }
            return numerator/denominator*count;
        }

        public class Quantity
        {
            public Quantity(double intensity, double denominator)
            {
                Intensity = intensity;
                Denominator = denominator;
            }
            public double Intensity { get; private set; }
            public double Denominator { get; private set; }
        }

        public static bool HasFalseQValue(TransitionGroupDocNode transitionGroupDocNode, int replicateIndex,
            ChromFileInfoId fileId)
        {
            if (null == transitionGroupDocNode.Results)
            {
                return false;
            }
            var chromInfoList = transitionGroupDocNode.Results[replicateIndex];
            if (null == chromInfoList)
            {
                return false;
            }
            var chromInfo = chromInfoList.FirstOrDefault(ci => null != ci && ReferenceEquals(ci.FileId, fileId));
            if (chromInfo == null)
            {
                return false;
            }
            return HasFalseQValue(chromInfo);
        }
        
        public static bool HasFalseQValue(TransitionGroupChromInfo transitionGroupChromInfo)
        {
            // TODO: Make it possible to set the cut-off
            return transitionGroupChromInfo.QValue > .01;
        }
    }
}
