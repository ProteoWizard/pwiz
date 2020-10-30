using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model.Results
{
    public class RatioCalculator
    {
        private readonly Lazy<NormalizationData> _normalizationData;
        private readonly Dictionary<ChromFileInfoId, FileInfo> _fileInfos;
        public RatioCalculator(SrmDocument document)
        {
            Document = document;
            _normalizationData = new Lazy<NormalizationData>(()=>NormalizationData.GetNormalizationData(document, false, null));
            _fileInfos = new Dictionary<ChromFileInfoId, FileInfo>(new IdentityEqualityComparer<ChromFileInfoId>());
            if (document.MeasuredResults != null)
            {
                var chromatograms = document.Settings.MeasuredResults.Chromatograms;
                for (int resultsIndex = 0; resultsIndex < chromatograms.Count; resultsIndex++)
                {
                    foreach (var chromFileInfo in chromatograms[resultsIndex].MSDataFileInfos)
                    {
                        _fileInfos.Add(chromFileInfo.FileId, new FileInfo(chromFileInfo, resultsIndex, document.Settings));
                    }
                }
            }
        }

        public bool BugCompatibility { get; set; }

        public SrmDocument Document { get; private set; }

        public bool SimpleRatios
        {
            get { return Document.Settings.PeptideSettings.Quantification.SimpleRatios; }
        }

        public double? GetTransitionValue(NormalizationMethod normalizationMethod, PeptideDocNode peptideDocNode,
            TransitionDocNode transitionDocNode, TransitionChromInfo transitionChromInfo)
        {
            if (peptideDocNode == null)
            {
                return null;
            }
            var transitionGroupDocNode = (TransitionGroupDocNode) peptideDocNode.FindNode(transitionDocNode.Transition.Group);
            return GetTransitionValue(normalizationMethod, peptideDocNode, transitionGroupDocNode, transitionDocNode,
                transitionChromInfo);
        }

        public double? GetTransitionValue(NormalizationMethod normalizationMethod, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode, TransitionDocNode transitionDocNode, TransitionChromInfo transitionChromInfo)
        {
            if (!transitionDocNode.IsQuantitative(Document.Settings))
            {
                return null;
            }
            if (transitionChromInfo == null || transitionChromInfo.IsEmpty)
            {
                return null;
            }

            if (TryGetDenominator(normalizationMethod, transitionGroupDocNode.LabelType, transitionChromInfo.FileId,
                out double? denominator))
            {
                return transitionChromInfo.Area / denominator;
            }

            if (normalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
            {
                if (ratioToLabel.IsotopeLabelTypeName == transitionGroupDocNode.LabelType.Name)
                {
                    return null;
                }
                if (SimpleRatios)
                {
                    return null;
                }
                var otherTransitionGroup =
                    FindMatchingTransitionGroup(ratioToLabel, peptideDocNode, transitionGroupDocNode);
                if (otherTransitionGroup == null)
                {
                    return null;
                }

                var otherTransition = FindMatchingTransition(transitionGroupDocNode, transitionDocNode, otherTransitionGroup);
                if (otherTransition == null)
                {
                    return null;
                }

                if (!otherTransition.IsQuantitative(Document.Settings))
                {
                    return null;
                }

                var otherChrominfo = FindMatchingTransitionChromInfo(transitionChromInfo, otherTransition);
                if (otherChrominfo == null || otherChrominfo.IsEmpty)
                {
                    return null;
                }

                return transitionChromInfo.Area / otherChrominfo.Area;
            }

            return null;
        }

        public double? GetTransitionDataValue(RatioIndex ratioIndex, TransitionChromInfoData transitionChromInfoData)
        {
            var normalizationMethod = RatioIndexToNormalizationMethod(transitionChromInfoData.PeptideDocNode,
                transitionChromInfoData.TransitionGroupDocNode, ratioIndex);
            return GetTransitionValue(normalizationMethod, transitionChromInfoData.PeptideDocNode,
                transitionChromInfoData.TransitionGroupDocNode, transitionChromInfoData.TransitionDocNode,
                transitionChromInfoData.ChromInfo);
        }

        public double? GetTransitionGroupDataValue(RatioIndex ratioIndex,
            TransitionGroupChromInfoData transitionGroupChromInfoData)
        {
            var normalizationMethod = RatioIndexToNormalizationMethod(transitionGroupChromInfoData.PeptideDocNode,
                transitionGroupChromInfoData.TransitionGroupDocNode, ratioIndex);
            return GetTransitionGroupValue(normalizationMethod, transitionGroupChromInfoData.PeptideDocNode,
                transitionGroupChromInfoData.TransitionGroupDocNode,
                transitionGroupChromInfoData.ChromInfo);

        }

        public double? GetTransitionGroupValue(NormalizationMethod normalizationMethod, PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode, TransitionGroupChromInfo transitionGroupChromInfo)
        {
            if (transitionGroupChromInfo == null)
            {
                return null;
            }

            if (TryGetDenominator(normalizationMethod, transitionGroupDocNode.LabelType,
                transitionGroupChromInfo.FileId, out double? denominator))
            {
                return transitionGroupChromInfo.Area / denominator;
            }

            if (normalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
            {
                return GetTransitionGroupRatioValue(ratioToLabel, peptideDocNode, transitionGroupDocNode, transitionGroupChromInfo)?.Ratio;
            }

            return null;
        }

        public RatioValue GetTransitionGroupRatioValue(NormalizationMethod.RatioToLabel ratioToLabel,
            PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode, TransitionGroupChromInfo transitionGroupChromInfo)
        {
            if (peptideDocNode == null)
            {
                return null;
            }
            if (transitionGroupDocNode.LabelType.Name == ratioToLabel.IsotopeLabelTypeName)
            {
                return null;
            }
            var otherTransitionGroup =
                FindMatchingTransitionGroup(ratioToLabel, peptideDocNode, transitionGroupDocNode);
            if (otherTransitionGroup == null)
            {
                return null;
            }

            var numerators = new List<double>();
            var denominators = new List<double>();
            if (SimpleRatios)
            {
                // var otherTransitionGroupChromInfo =
                //     FindMatchingTransitionGroupChromInfo(transitionGroupChromInfo, otherTransitionGroup);
                // if (otherTransitionGroupChromInfo == null)
                // {
                //     return null;
                // }
                //
                // return transitionGroupChromInfo.Area / otherTransitionGroupChromInfo.Area;
                foreach (var tran in transitionGroupDocNode.Transitions.Where(tran =>
                    tran.IsQuantitative(Document.Settings)))
                {
                    var chromInfo = FindMatchingTransitionChromInfo(transitionGroupChromInfo.FileId,
                        transitionGroupChromInfo.OptimizationStep, tran);
                    if (chromInfo != null)
                    {
                        numerators.Add(chromInfo.Area);
                    }
                }
                foreach (var tran in otherTransitionGroup.Transitions.Where(tran =>
                    tran.IsQuantitative(Document.Settings)))
                {
                    var chromInfo = FindMatchingTransitionChromInfo(transitionGroupChromInfo.FileId,
                        transitionGroupChromInfo.OptimizationStep, tran);
                    if (chromInfo != null)
                    {
                        denominators.Add(chromInfo.Area);
                    }
                }

                if (numerators.Count == 0 || denominators.Count == 0)
                {
                    return null;
                }

                return RatioValue.ValueOf(numerators.Sum() / denominators.Sum());
            }

            var transitionMap = GetTransitionMap(otherTransitionGroup);
            foreach (var transition in transitionGroupDocNode.Transitions)
            {
                if (!transition.IsQuantitative(Document.Settings))
                {
                    continue;
                }
                var targetKey = new PeptideDocNode.TransitionKey(transitionGroupDocNode, new TransitionLossKey(transitionGroupDocNode, transition, transition.Losses), otherTransitionGroup.LabelType);
                if (!transitionMap.TryGetValue(
                    targetKey,
                    out TransitionDocNode otherTransition))
                {
                    continue;
                }

                if (!otherTransition.IsQuantitative(Document.Settings))
                {
                    continue;
                }

                var transitionChromInfo = FindMatchingTransitionChromInfo(transitionGroupChromInfo.FileId,
                    transitionGroupChromInfo.OptimizationStep, transition);
                var otherTransitionChromInfo = FindMatchingTransitionChromInfo(transitionGroupChromInfo.FileId,
                    transitionGroupChromInfo.OptimizationStep, otherTransition);
                if (transitionChromInfo == null || transitionChromInfo.IsEmpty || otherTransitionChromInfo == null || otherTransitionChromInfo.IsEmpty)
                {
                    continue;
                }
                numerators.Add(transitionChromInfo.Area);
                denominators.Add(otherTransitionChromInfo.Area);
            }

            return RatioValue.Calculate(numerators, denominators);
        }

        public TransitionDocNode FindMatchingTransition(TransitionGroupDocNode transitionGroup, TransitionDocNode transitionDocNode,
            TransitionGroupDocNode otherTransitionGroup)
        {
            var transitionKey = new PeptideDocNode.TransitionKey(transitionGroup, new TransitionLossKey(transitionGroup, transitionDocNode, transitionDocNode.Losses), otherTransitionGroup.LabelType);
            foreach (var otherTransition in otherTransitionGroup.Transitions)
            {
                var otherTransitionKey = new PeptideDocNode.TransitionKey(otherTransitionGroup, new TransitionLossKey(otherTransitionGroup, otherTransition, otherTransition.Losses), otherTransitionGroup.LabelType);
                if (transitionKey.Equals(otherTransitionKey))
                {
                    return otherTransition;
                }
            }

            return null;
        }

        public IDictionary<PeptideDocNode.TransitionKey, TransitionDocNode> GetTransitionMap(
            TransitionGroupDocNode transitionGroupDocNode)
        {
            return transitionGroupDocNode.Transitions.ToDictionary(transition =>
                new PeptideDocNode.TransitionKey(transitionGroupDocNode,
                    new TransitionLossKey(transitionGroupDocNode, transition, transition.Losses),
                    transitionGroupDocNode.LabelType)

            );
        }


        public TransitionChromInfo FindMatchingTransitionChromInfo(TransitionChromInfo transitionChromInfo,
            TransitionDocNode otherTransition)
        {
            return FindMatchingTransitionChromInfo(transitionChromInfo.FileId, transitionChromInfo.OptimizationStep,
                otherTransition);
        }
        public TransitionChromInfo FindMatchingTransitionChromInfo(ChromFileInfoId fileId, int optimizationStep, TransitionDocNode otherTransition) 
        {
            if (otherTransition.Results == null)
            {
                return null;
            }

            FileInfo fileInfo;
            if (!_fileInfos.TryGetValue(fileId, out fileInfo))
            {
                return null;
            }

            if (otherTransition.Results.Count <= fileInfo.ResultsIndex)
            {
                return null;
            }

            foreach (var otherChromInfo in otherTransition.Results[fileInfo.ResultsIndex])
            {
                if (otherChromInfo == null)
                {
                    continue;
                }

                if (!ReferenceEquals(otherChromInfo.FileId, fileId))
                {
                    continue;
                }

                if (otherChromInfo.OptimizationStep != 0)
                {
                    continue;
                }

                return otherChromInfo;
            }

            return null;
        }

        public TransitionGroupChromInfo FindMatchingTransitionGroupChromInfo(
            TransitionGroupChromInfo transitionGroupChromInfo, TransitionGroupDocNode otherTransitionGroup)
        {
            if (otherTransitionGroup.Results == null)
            {
                return null;
            }

            FileInfo fileInfo;
            if (!_fileInfos.TryGetValue(transitionGroupChromInfo.FileId, out fileInfo))
            {
                return null;
            }

            if (otherTransitionGroup.Results.Count <= fileInfo.ResultsIndex)
            {
                return null;
            }

            foreach (var otherChromInfo in otherTransitionGroup.Results[fileInfo.ResultsIndex])
            {
                if (otherChromInfo == null)
                {
                    continue;
                }

                if (!ReferenceEquals(otherChromInfo.FileId, transitionGroupChromInfo.FileId))
                {
                    continue;
                }

                if (otherChromInfo.OptimizationStep != transitionGroupChromInfo.OptimizationStep)
                {
                    continue;
                }

                return otherChromInfo;
            }

            return null;

        }

        public TransitionGroupDocNode FindMatchingTransitionGroup(NormalizationMethod.RatioToLabel ratioToLabel,
            PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode)
        {
            foreach (var otherTransitionGroup in peptideDocNode.TransitionGroups)
            {
                if (ratioToLabel.IsotopeLabelTypeName != otherTransitionGroup.LabelType.Name)
                {
                    continue;
                }

                if (!Equals(otherTransitionGroup.PrecursorAdduct.Unlabeled, transitionGroupDocNode.PrecursorAdduct.Unlabeled))
                {
                    continue;
                }

                return otherTransitionGroup;
            }

            return null;
        }

        public IList<IsotopeLabelType> RatioInternalStandardTypes
        {
            get { return Document.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes; }
        }

        public bool TryGetDenominator(NormalizationMethod normalizationMethod, IsotopeLabelType labelType, ChromFileInfoId fileId, out double? denominator)
        {
            if (Equals(normalizationMethod, NormalizationMethod.NONE))
            {
                denominator = 1;
                return true;
            }

            FileInfo fileInfo;
            if (!_fileInfos.TryGetValue(fileId, out fileInfo))
            {
                denominator = null;
                return true;
            }
            if (Equals(normalizationMethod, NormalizationMethod.GLOBAL_STANDARDS))
            {
                denominator = fileInfo.GlobalStandardArea;
                return true;
            }

            if (Equals(normalizationMethod, NormalizationMethod.EQUALIZE_MEDIANS))
            {
                var normalizationData = _normalizationData.Value;
                var medianAdjustment = normalizationData.GetMedian(fileId, labelType) - normalizationData.GetMedianMedian(fileInfo.SampleType, labelType);
                if (!medianAdjustment.HasValue)
                {
                    denominator = null;
                    return true;
                }

                denominator = 1 / Math.Pow(2, medianAdjustment.Value);
                return true;
            }

            if (normalizationMethod is NormalizationMethod.RatioToSurrogate ratioToSurrogate)
            {
                denominator = fileInfo.GetSurrogateStandardArea(ratioToSurrogate);
                return true;
            }

            denominator = null;
            return false;
        }

        public NormalizationMethod RatioIndexToNormalizationMethod(PeptideDocNode peptideDocNode, RatioIndex ratioIndex)
        {
            if (ratioIndex == RatioIndex.CALIBRATED)
            {
                ratioIndex = RatioIndex.NORMALIZED;
            }

            if (ratioIndex == RatioIndex.NORMALIZED)
            {
                return peptideDocNode.NormalizationMethod ??
                       Document.Settings.PeptideSettings.Quantification.NormalizationMethod;
            }

            if (ratioIndex.InternalStandardIndex.HasValue)
            {
                if (ratioIndex.InternalStandardIndex >=
                    RatioInternalStandardTypes.Count)
                {
                    return NormalizationMethod.NONE;
                }

                return new NormalizationMethod.RatioToLabel(
                    RatioInternalStandardTypes[ratioIndex.InternalStandardIndex.Value]);
            }

            if (ratioIndex == RatioIndex.GLOBAL_STANDARD)
            {
                return NormalizationMethod.GLOBAL_STANDARDS;
            }

            return NormalizationMethod.NONE;
        }

        public NormalizationMethod RatioIndexToNormalizationMethod(PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroup, RatioIndex ratioIndex)
        {
            var normalizationMethod = RatioIndexToNormalizationMethod(peptideDocNode, ratioIndex);
            if (normalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
            {
                if (ratioToLabel.IsotopeLabelTypeName == transitionGroup.LabelType.Name)
                {
                    return NormalizationMethod.NONE;
                }
            }

            return normalizationMethod;
        }

        private class FileInfo
        {
            private Dictionary<NormalizationMethod.RatioToSurrogate, double> _surrogateStandardAreas;
            public FileInfo(ChromFileInfo fileInfo, int resultsIndex, SrmSettings settings)
            {
                ChromFileInfo = fileInfo;
                ResultsIndex = resultsIndex;
                GlobalStandardArea = settings.CalcGlobalStandardArea(resultsIndex, fileInfo);
                _surrogateStandardAreas = new Dictionary<NormalizationMethod.RatioToSurrogate, double>();
            }

            public SrmSettings SrmSettings { get; private set; }

            public ChromFileInfo ChromFileInfo { get; private set; }
            public int ResultsIndex { get; private set; }

            public double GlobalStandardArea { get; private set; }

            public SampleType SampleType
            {
                get { return SrmSettings.MeasuredResults.Chromatograms[ResultsIndex].SampleType; }
            }

            public double GetSurrogateStandardArea(NormalizationMethod.RatioToSurrogate ratioToSurrogate)
            {
                lock (_surrogateStandardAreas)
                {
                    double value;
                    if (_surrogateStandardAreas.TryGetValue(ratioToSurrogate, out value))
                    {
                        return value;
                    }

                    value = ratioToSurrogate.GetStandardArea(SrmSettings, ResultsIndex, ChromFileInfo.FileId);
                    _surrogateStandardAreas.Add(ratioToSurrogate, value);
                    return value;
                }
            }
        }
    }
}
