using System.Collections.Generic;
using System.Linq;
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
            NormalizationMethod normalizationMethod = srmSettings.PeptideSettings.Quantification.NormalizationMethod;
            var mods = srmSettings.PeptideSettings.Modifications;
            IsotopeLabelType labelType;
            if (string.IsNullOrEmpty(normalizationMethod.IsotopeLabelTypeName))
            {
                labelType = mods.GetModificationTypes()
                    .Except(mods.InternalStandardTypes.Take(1)).FirstOrDefault();
            }
            else
            {
                labelType = mods.GetModificationTypes()
                    .First(mod => mod.Name != normalizationMethod.IsotopeLabelTypeName);
            }
            return new PeptideQuantifier(peptideGroup, peptide, srmSettings.PeptideSettings.Quantification)
            {
                MeasuredLabelType = labelType,
            };
        }

        public PeptideGroupDocNode PeptideGroupDocNode { get; private set; }
        public PeptideDocNode PeptideDocNode {get; private set; }
        public QuantificationSettings QuantificationSettings { get; private set; }
        public NormalizationMethod NormalizationMethod { get {return QuantificationSettings.NormalizationMethod;} }
        public IsotopeLabelType MeasuredLabelType { get; set; }

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
            if (null != MeasuredLabelType)
            {
                if (!Equals(MeasuredLabelType, transitionGroupDocNode.TransitionGroup.LabelType))
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

        public IDictionary<IdentityPath, Quantity> GetTransitionIntensities(SrmSettings srmSettings, int replicateIndex)
        {
            var quantities = new Dictionary<IdentityPath, Quantity>();
            var transitionsToNormalizeAgainst = GetTransitionsForLabel(
                PeptideDocNode, replicateIndex, NormalizationMethod.IsotopeLabelTypeName);
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
                        transition);
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
            IDictionary<TransitionLossEquivalentKey, TransitionChromInfo> peptideStandards,
            int replicateIndex,
            TransitionGroupDocNode transitionGroup, TransitionDocNode transition)
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
            if (null == chromInfo || chromInfo.IsEmpty)
            {
                return null;
            }
            double normalizedArea = chromInfo.Area;
            double denominator = 1.0;

            if (null != peptideStandards)
            {
                TransitionChromInfo chromInfoStandard;
                if (!peptideStandards.TryGetValue(transition.EquivalentKey(transitionGroup), out chromInfoStandard))
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
            }
            return new Quantity(normalizedArea, denominator);
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
                if (chromInfo.IsEmpty)
                {
                    continue;
                }
                return chromInfo;
            }
            return null;
        }

        private Dictionary<TransitionLossEquivalentKey, TransitionChromInfo> GetTransitionsForLabel(
            PeptideDocNode peptideDocNode, int replicateIndex, string isotopeLabelName)
        {
            if (string.IsNullOrEmpty(isotopeLabelName))
            {
                return null;
            }
            var result = new Dictionary<TransitionLossEquivalentKey, TransitionChromInfo>();
            foreach (var transitionGroup in peptideDocNode.TransitionGroups)
            {
                if (!Equals(isotopeLabelName, transitionGroup.TransitionGroup.LabelType.Name))
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
                        result[transition.EquivalentKey(transitionGroup)] = chromInfo;
                    }
                }
            }
            return result;
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
    }
}
