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
        private NormalizationData _normalizationData;
        private Func<NormalizationData> _getNormalizationDataFunc;
        public PeptideQuantifier(Func<NormalizationData> getNormalizationDataFunc, PeptideGroupDocNode peptideGroup, PeptideDocNode peptideDocNode,
            QuantificationSettings quantificationSettings)
        {
            PeptideGroupDocNode = peptideGroup;
            PeptideDocNode = peptideDocNode;
            QuantificationSettings = quantificationSettings;
            _getNormalizationDataFunc = getNormalizationDataFunc;
        }

        public static PeptideQuantifier GetPeptideQuantifier(Func<NormalizationData> getNormalizationDataFunc, SrmSettings srmSettings, PeptideGroupDocNode peptideGroup, PeptideDocNode peptide)
        {
            var mods = srmSettings.PeptideSettings.Modifications;
            // Quantify on all label types which are not internal standards.
            ICollection<IsotopeLabelType> labelTypes = ImmutableList.ValueOf(mods.GetModificationTypes()
                .Except(mods.InternalStandardTypes));
            return new PeptideQuantifier(getNormalizationDataFunc, peptideGroup, peptide, srmSettings.PeptideSettings.Quantification)
            {
                MeasuredLabelTypes = labelTypes
            };
        }

        public static PeptideQuantifier GetPeptideQuantifier(SrmDocument document, PeptideGroupDocNode peptideGroup,
            PeptideDocNode peptide)
        {
            return GetPeptideQuantifier(() => NormalizationData.GetNormalizationData(document, false, null), 
                document.Settings, peptideGroup, peptide);
        }

        public PeptideGroupDocNode PeptideGroupDocNode { get; private set; }
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

        public NormalizationData GetNormalizationData()
        {
            if (_normalizationData == null)
            {
                _normalizationData = _getNormalizationDataFunc();
            }
            return _normalizationData;
        }
        public double? QValueCutoff { get; set; }

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
                        IdentityPath transitionIdentityPath = new IdentityPath(PeptideGroupDocNode.PeptideGroup,
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
            double? normalizedArea = GetArea(treatMissingAsZero, QValueCutoff, transitionGroup, transition, replicateIndex, chromInfo);
            if (!normalizedArea.HasValue)
            {
                return null;
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
                    var normalizationData = GetNormalizationData();
                    if (null == normalizationData)
                    {
                        throw new InvalidOperationException(string.Format(@"Normalization method '{0}' is not supported here.", NormalizationMethod));
                    }
                    double? medianAdjustment = normalizationData.GetMedian(chromInfo.FileId, transitionGroup.TransitionGroup.LabelType) 
                        - normalizationData.GetMedianMedian(srmSettings.MeasuredResults.Chromatograms[replicateIndex].SampleType, transitionGroup.TransitionGroup.LabelType);
                    if (!medianAdjustment.HasValue)
                    {
                        return null;
                    }
                    normalizedArea /= Math.Pow(2.0, medianAdjustment.Value);
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
            if (normalizationMethod is NormalizationMethod.RatioToLabel)
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

        public static double? GetArea(bool treatMissingAsZero, double? qValueCutoff, TransitionGroupDocNode transitionGroup,
            TransitionDocNode transition, int replicateIndex, TransitionChromInfo chromInfo)
        {
            if (treatMissingAsZero && chromInfo.IsEmpty)
            {
                return 0;
            }
            if (chromInfo.IsEmpty || chromInfo.IsTruncated.GetValueOrDefault())
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
