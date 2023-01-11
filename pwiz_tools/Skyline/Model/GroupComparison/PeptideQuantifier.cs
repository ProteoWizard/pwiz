using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
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
        private Dictionary<ChromFileInfoId, double> _globalStandardAreas;
        private double _medianGlobalStandardArea;
        public PeptideQuantifier(Func<NormalizationData> getNormalizationDataFunc, PeptideGroupDocNode peptideGroup, PeptideDocNode peptideDocNode,
            SrmSettings srmSettings)
        {
            PeptideGroupDocNode = peptideGroup;
            PeptideDocNode = peptideDocNode;
            SrmSettings = srmSettings;
            _getNormalizationDataFunc = getNormalizationDataFunc;
        }

        public static PeptideQuantifier GetPeptideQuantifier(Func<NormalizationData> getNormalizationDataFunc, SrmSettings srmSettings, PeptideGroupDocNode peptideGroup, PeptideDocNode peptide)
        {
            var mods = srmSettings.PeptideSettings.Modifications;
            // Quantify on all label types which are not internal standards.
            ICollection<IsotopeLabelType> labelTypes = ImmutableList.ValueOf(mods.GetModificationTypes()
                .Except(mods.InternalStandardTypes));
            return new PeptideQuantifier(getNormalizationDataFunc, peptideGroup, peptide, srmSettings)
            {
                MeasuredLabelTypes = labelTypes,
                IncludeTruncatedPeaks = srmSettings.TransitionSettings.Instrument.TriggeredAcquisition,
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
        public QuantificationSettings QuantificationSettings
        {
            get { return SrmSettings.PeptideSettings.Quantification; }
        }
        public SrmSettings SrmSettings { get; }

        public NormalizationMethod NormalizationMethod
        {
            get
            {
                return PeptideDocNode.NormalizationMethod ?? QuantificationSettings.NormalizationMethod;
            }
        }
        public ICollection<IsotopeLabelType> MeasuredLabelTypes { get; set; }
        /// <summary>
        /// When normalizing to global standards, whether to multiply the end result by the median global standard area,
        /// so the normalized value is close in magnitude to the original value.
        /// For backward compatibility reasons, we do not normally do this for global standard normalization.
        /// However, when doing group comparisons we need to do this, since group comparisons treat values which are less than 1
        /// as being equal to 1, because otherwise logarithms of negative or very small numbers mess up some calculations.
        /// </summary>
        public bool AlwaysMultiplyByMedianNormalizationFactor { get; set; }

        public double GetGlobalStandardNormalizationFactor(ChromFileInfoId chromFileInfoId)
        {
            if (_globalStandardAreas == null)
            {
                var dictionary =
                    new Dictionary<ChromFileInfoId, double>(new IdentityEqualityComparer<ChromFileInfoId>());
                for (int iResult = 0; iResult < SrmSettings.MeasuredResults?.Chromatograms.Count; iResult++)
                {
                    var chromatogramSet = SrmSettings.MeasuredResults.Chromatograms[iResult];
                    foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                    {
                        dictionary[chromFileInfo.FileId] = SrmSettings.CalcGlobalStandardArea(iResult, chromFileInfo);
                    }
                }

                if (dictionary.Count > 0)
                {
                    _medianGlobalStandardArea = dictionary.Values.Median();
                }
                _globalStandardAreas = dictionary;
            }

            _globalStandardAreas.TryGetValue(chromFileInfoId, out var globalStandardArea);
            if (AlwaysMultiplyByMedianNormalizationFactor && _medianGlobalStandardArea != 0)
            {
                globalStandardArea /= _medianGlobalStandardArea;
            }

            return globalStandardArea;
        }

        public NormalizationData GetNormalizationData()
        {
            if (_normalizationData == null)
            {
                _normalizationData = _getNormalizationDataFunc();
            }
            return _normalizationData;
        }
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

        public bool SkipTransition(TransitionDocNode transitionDocNode)
        {
            if (!transitionDocNode.IsQuantitative(SrmSettings))
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

        public IDictionary<IdentityPath, Quantity> GetTransitionIntensities(int replicateIndex, bool treatMissingAsZero)
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
                    var quantity = GetTransitionQuantity(transitionsToNormalizeAgainst, NormalizationMethod, replicateIndex, precursor,
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
                    if (SkipTransition(transition))
                    {
                        continue;
                    }
                    var quantity = GetTransitionQuantity(null, normalizationMethod, replicateIndex, precursor,
                        transition, false);
                    if (quantity != null)
                    {
                        totalArea += quantity.Intensity / quantity.Denominator;
                    }
                }
            }
            return totalArea;
        }

        public double? GetQualitativeIonRatio(TransitionGroupDocNode precursor, int replicateIndex)
        {
            double numerator = 0;
            int numeratorCount = 0;
            double denominator = 0;
            int denominatorCount = 0;
            foreach (var transition in precursor.Transitions)
            {
                var quantity = GetTransitionQuantity(null, NormalizationMethod.NONE, replicateIndex,
                    precursor, transition, false);
                if (quantity != null)
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

        private Quantity GetTransitionQuantity(
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
            double? normalizedArea = GetArea(treatMissingAsZero, QValueCutoff, IncludeTruncatedPeaks, transitionGroup, transition, replicateIndex, chromInfo);
            if (!normalizedArea.HasValue)
            {
                return null;
            }

            double denominator = 1.0;

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
                if (chromInfo.IsTruncated.GetValueOrDefault())
                {
                    return null;
                }
                if (Equals(normalizationMethod, NormalizationMethod.GLOBAL_STANDARDS))
                {

                    denominator = GetGlobalStandardNormalizationFactor(chromInfo.FileId);
                }
                else if (normalizationMethod is NormalizationMethod.RatioToSurrogate)
                {
                    denominator =  ((NormalizationMethod.RatioToSurrogate) NormalizationMethod)
                        .GetStandardArea(SrmSettings, replicateIndex, chromInfo.FileId);
                }
                else if (Equals(normalizationMethod, NormalizationMethod.EQUALIZE_MEDIANS))
                {
                    var normalizationData = GetNormalizationData();
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
                    var factor = SrmSettings.GetTicNormalizationDenominator(replicateIndex, chromInfo.FileId);
                    if (!factor.HasValue)
                    {
                        return null;
                    }
                    denominator = factor.Value;
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
            PeptideDocNode peptideDocNode, int replicateIndex)
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
                    if (!transition.IsQuantitative(SrmSettings))
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

        public static double? SumQuantities(IEnumerable<Quantity> quantities, NormalizationMethod normalizationMethod, bool simpleRatios)
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
            if (!simpleRatios && normalizationMethod is NormalizationMethod.RatioToLabel)
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
