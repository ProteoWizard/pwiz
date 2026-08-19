/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results
{
    public class NormalizedValueCalculator
    {
        public static readonly NormalizedValueCalculator DEFAULT =
            new NormalizedValueCalculator(new SrmDocument(SrmSettingsList.GetDefault()));
        private readonly Lazy<NormalizationData> _normalizationData;
        private readonly Dictionary<ReferenceValue<ChromFileInfoId>, FileInfo> _fileInfos;

        public NormalizedValueCalculator(SrmDocument document, NormalizationData normalizationData = null)
        {
            Document = document;
            if (normalizationData == null)
            {
                _normalizationData = new Lazy<NormalizationData>(() =>
                    NormalizationData.GetNormalizationData(document, false, null));
            }
            else
            {
                _normalizationData = new Lazy<NormalizationData>(()=>normalizationData);
            }
            _fileInfos = new Dictionary<ReferenceValue<ChromFileInfoId>, FileInfo>();
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

        public SrmDocument Document { get; private set; }

        public bool SimpleRatios
        {
            get { return Document.Settings.PeptideSettings.Quantification.SimpleRatios; }
        }

        public double? GetTransitionValue(NormalizationMethod normalizationMethod, PeptideDocNode peptideDocNode,
            TransitionDocNode transitionDocNode, int replicateIndex, TransitionChromInfo transitionChromInfo)
        {
            if (peptideDocNode == null)
            {
                return null;
            }
            var transitionGroupDocNode = (TransitionGroupDocNode) peptideDocNode.FindNode(transitionDocNode.Transition.Group);
            if (transitionGroupDocNode == null)
            {
                return null;
            }
            return GetTransitionValue(normalizationMethod, peptideDocNode, transitionGroupDocNode, transitionDocNode, replicateIndex, 
                transitionChromInfo);
        }

        public double? GetTransitionValue(NormalizationMethod normalizationMethod, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode, TransitionDocNode transitionDocNode, int replicateIndex, TransitionChromInfo transitionChromInfo)
        {
            if (transitionChromInfo == null || transitionChromInfo.IsEmpty)
            {
                return null;
            }

            return GetTransitionAreaValue(normalizationMethod, peptideDocNode, transitionGroupDocNode,
                transitionDocNode, replicateIndex, transitionChromInfo.FileId, transitionChromInfo.Area);
        }

        /// <summary>
        /// The same value from the area and the file alone, which is all of a peak this needs and
        /// all of it the columnar results hold. This is how a caller which has no reason to read a
        /// chromatogram - the targets tree, which asks for every node it paints - gets one.
        /// </summary>
        public double? GetTransitionAreaValue(NormalizationMethod normalizationMethod, PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode, TransitionDocNode transitionDocNode, int replicateIndex,
            ChromFileInfoId fileId, float area)
        {
            if (TryGetDenominator(normalizationMethod, replicateIndex, fileId, out double? denominator))
            {
                return area / denominator;
            }

            if (normalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
            {
                if (!transitionDocNode.IsQuantitative(Document.Settings))
                {
                    return null;
                }
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

                var otherPeak = FindMatchingTransitionPeak(fileId, otherTransitionGroup, otherTransition);
                if (!otherPeak.HasValue || otherPeak.Value.IsEmpty)
                {
                    return null;
                }

                return area / otherPeak.Value.Area;
            }

            return null;
        }

        public double? GetTransitionDataValue(NormalizeOption ratioIndex, TransitionChromInfoData transitionChromInfoData)
        {
            var normalizationMethod = NormalizationMethodForPrecursor(transitionChromInfoData.PeptideDocNode,
                transitionChromInfoData.TransitionGroupDocNode, ratioIndex);
            return GetTransitionValue(normalizationMethod, transitionChromInfoData.PeptideDocNode,
                transitionChromInfoData.TransitionGroupDocNode, transitionChromInfoData.TransitionDocNode,
                transitionChromInfoData.ReplicateIndex,
                transitionChromInfoData.ChromInfo);
        }

        public double? GetTransitionGroupDataValue(NormalizeOption ratioIndex,
            TransitionGroupChromInfoData transitionGroupChromInfoData)
        {
            var normalizationMethod = NormalizationMethodForPrecursor(transitionGroupChromInfoData.PeptideDocNode,
                transitionGroupChromInfoData.TransitionGroupDocNode, ratioIndex);
            return GetTransitionGroupValue(normalizationMethod, transitionGroupChromInfoData.PeptideDocNode,
                transitionGroupChromInfoData.TransitionGroupDocNode,
                transitionGroupChromInfoData.ReplicateIndex,
                transitionGroupChromInfoData.ChromInfo);

        }

        public double? GetTransitionGroupValue(NormalizationMethod normalizationMethod, PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode, int replicateIndex, TransitionGroupChromInfo transitionGroupChromInfo)
        {
            if (transitionGroupChromInfo == null)
            {
                return null;
            }

            if (TryGetDenominator(normalizationMethod, replicateIndex, transitionGroupChromInfo.FileId, out double? denominator))
            {
                return transitionGroupChromInfo.Area / denominator;
            }

            if (normalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
            {
                var ratioValue = GetTransitionGroupRatioValue(ratioToLabel, peptideDocNode, transitionGroupDocNode, transitionGroupChromInfo);
                return ratioValue?.Ratio;
            }

            return null;
        }

        public RatioValue GetTransitionGroupRatioValue(NormalizationMethod.RatioToLabel ratioToLabel,
            PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode, TransitionGroupChromInfo transitionGroupChromInfo)
        {
            return transitionGroupChromInfo == null
                ? null
                : GetTransitionGroupRatioValue(ratioToLabel, peptideDocNode, transitionGroupDocNode,
                    transitionGroupChromInfo.FileId);
        }

        /// <summary>
        /// The ratio of a precursor's areas to its label match's in one file. The file is all the
        /// overload which takes a <see cref="TransitionGroupChromInfo"/> ever took from one, and a
        /// caller reading the columnar results has the file without rebuilding a chrom info.
        /// </summary>
        public RatioValue GetTransitionGroupRatioValue(NormalizationMethod.RatioToLabel ratioToLabel,
            PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode, ChromFileInfoId fileId)
        {
            if (peptideDocNode == null || fileId == null)
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
                foreach (var tran in transitionGroupDocNode.Transitions.Where(tran =>
                    tran.IsQuantitative(Document.Settings)))
                {
                    var peak = FindMatchingTransitionPeak(fileId, transitionGroupDocNode,
                        tran);
                    if (peak.HasValue)
                    {
                        numerators.Add(peak.Value.Area);
                    }
                }
                foreach (var tran in otherTransitionGroup.Transitions.Where(tran =>
                    tran.IsQuantitative(Document.Settings)))
                {
                    var peak = FindMatchingTransitionPeak(fileId, otherTransitionGroup, tran);
                    if (peak.HasValue)
                    {
                        denominators.Add(peak.Value.Area);
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

                var peak = FindMatchingTransitionPeak(fileId, transitionGroupDocNode,
                    transition);
                var otherPeak = FindMatchingTransitionPeak(fileId, otherTransitionGroup,
                    otherTransition);
                if (!peak.HasValue || peak.Value.IsEmpty || !otherPeak.HasValue || otherPeak.Value.IsEmpty)
                {
                    continue;
                }
                numerators.Add(peak.Value.Area);
                denominators.Add(otherPeak.Value.Area);
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

        /// <summary>
        /// Returns the ratio that should be displayed by default in the Targets tree
        /// if the user has not yet selected a ratio to display.
        /// </summary>
        public NormalizationMethod GetFirstRatioNormalizationMethod()
        {
            var firstInternalStandardType = RatioInternalStandardTypes.FirstOrDefault();
            if (firstInternalStandardType != null)
            {
                return new NormalizationMethod.RatioToLabel(firstInternalStandardType);
            }

            if (Document.Settings.HasGlobalStandardArea)
            {
                return NormalizationMethod.GLOBAL_STANDARDS;
            }

            return null;
        }

        public IDictionary<PeptideDocNode.TransitionKey, TransitionDocNode> GetTransitionMap(
            TransitionGroupDocNode transitionGroupDocNode)
        {
            return CollectionUtil.SafeToDictionary(transitionGroupDocNode.Transitions.Select(transition =>
                new KeyValuePair<PeptideDocNode.TransitionKey, TransitionDocNode>(
                    new PeptideDocNode.TransitionKey(transitionGroupDocNode,
                        new TransitionLossKey(transitionGroupDocNode, transition, transition.Losses),
                        transitionGroupDocNode.LabelType), transition))
            );
        }


        /// <summary>
        /// One transition's peak in one file, or null when there is no peak there.
        /// <para>
        /// From the columnar results, which is where a peak lives now: a
        /// <see cref="TransitionDocNode"/> reports no chrom infos at all, so the precursor which
        /// owns the transition has to be named before its peak can be found. Optimization step
        /// zero, which is all the columnar results keep and all this ever accepted.
        /// </para>
        /// </summary>
        private TransitionPeak? FindMatchingTransitionPeak(ChromFileInfoId fileId,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            var groupResults = nodeGroup.AbbreviatedResults;
            if (groupResults == null || !_fileInfos.TryGetValue(fileId, out var fileInfo))
            {
                return null;
            }

            int transitionIndex = nodeGroup.FindNodeIndex(nodeTran.Transition);
            if (transitionIndex < 0 ||
                !groupResults.TryGetTransitionPeak(nodeTran.Transition, fileInfo.ResultsIndex, fileId, out var peak))
            {
                return null;
            }

            return peak;
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

        public bool TryGetDenominator(NormalizationMethod normalizationMethod, int replicateIndex, ChromFileInfoId fileId, out double? denominator)
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
                var medianAdjustment = normalizationData.GetLog2Median(replicateIndex, fileId) - normalizationData.GetMedianLog2Median();
                if (!medianAdjustment.HasValue)
                {
                    denominator = null;
                    return true;
                }

                denominator = Math.Pow(2, medianAdjustment.Value);
                return true;
            }

            if (Equals(normalizationMethod, NormalizationMethod.TIC))
            {
                denominator = Document.Settings.GetTicNormalizationDenominator(fileInfo.ResultsIndex, fileId);
                return denominator.HasValue;
            }

            if (normalizationMethod is NormalizationMethod.RatioToSurrogate ratioToSurrogate)
            {
                denominator = fileInfo.GetSurrogateStandardArea(ratioToSurrogate);
                return true;
            }

            denominator = null;
            return false;
        }

        public Lazy<NormalizationData> LazyNormalizationData
        {
            get
            {
                return _normalizationData;
            }
        }

        public NormalizationMethod NormalizationMethodForMolecule(PeptideDocNode peptideDocNode, NormalizeOption normalizeOption)
        {
            if(peptideDocNode == null)
                return NormalizationMethod.NONE;
            
            if (normalizeOption.NormalizationMethod != null)
            {
                return normalizeOption.NormalizationMethod;
            }
            if (normalizeOption == NormalizeOption.CALIBRATED)
            {
                normalizeOption = NormalizeOption.DEFAULT;
            }

            if (normalizeOption == NormalizeOption.DEFAULT)
            {
                return peptideDocNode.NormalizationMethod ??
                       Document.Settings.PeptideSettings.Quantification.NormalizationMethod;
            }

            return NormalizationMethod.NONE;
        }

        public NormalizationMethod NormalizationMethodForPrecursor(PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroup, NormalizeOption normalizeOption)
        {
            var normalizationMethod = NormalizationMethodForMolecule(peptideDocNode, normalizeOption);
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
                SrmSettings = settings;
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

        public static readonly Producer<Params,
            NormalizedValueCalculator> PRODUCER = new NormalizedValueCalculatorProducer();

        public static WorkOrder MakeWorkOrder(SrmDocument document, NormalizeOption normalizeOption)
        {
            return PRODUCER.MakeWorkOrder(new Params(document, normalizeOption));
        }
            
        private class NormalizedValueCalculatorProducer : Producer<Params,
            NormalizedValueCalculator>
        {
            public override NormalizedValueCalculator ProduceResult(ProductionMonitor productionMonitor, Params parameter, IDictionary<WorkOrder, object> dependencies)
            {
                var document = parameter.Document;
                return new NormalizedValueCalculator(document,
                    NormalizationData.PRODUCER.GetResult(dependencies, new NormalizationData.Parameters(document)));
            }

            public override IEnumerable<WorkOrder> GetInputs(Params workParameter)
            {
                var normalizeOption = workParameter.NormalizeOption;
                SrmDocument document = workParameter.Document;
                
                if (Equals(NormalizationMethod.EQUALIZE_MEDIANS, normalizeOption?.NormalizationMethod) || Equals(NormalizationMethod.EQUALIZE_MEDIANS, document.Settings.PeptideSettings.Quantification.NormalizationMethod))
                {
                    return ImmutableList.Singleton(NormalizationData.PRODUCER.MakeWorkOrder(new NormalizationData.Parameters(document)));
                }

                return Array.Empty<WorkOrder>();
            }
        }

        public class Params
        {
            public Params(SrmDocument document, NormalizeOption normalizeOption)
            {
                Document = document;
                NormalizeOption = normalizeOption;
            }
            
            public SrmDocument Document { get; }
            public NormalizeOption NormalizeOption { get; }

            protected bool Equals(Params other)
            {
                return ReferenceEquals(Document, other.Document) && NormalizeOption.Equals(other.NormalizeOption);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Params)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(Document) * 397) ^ NormalizeOption.GetHashCode();
                }
            }
        }
    }
}
