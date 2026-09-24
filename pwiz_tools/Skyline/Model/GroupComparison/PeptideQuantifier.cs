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
        private readonly NormalizedValueCalculator _normalizationData;
        public PeptideQuantifier(NormalizedValueCalculator normalizationData, PeptideGroup peptideGroup, PeptideDocNode peptideDocNode,
            QuantificationSettings quantificationSettings)
        {
            PeptideGroup = peptideGroup;
            PeptideDocNode = peptideDocNode;
            QuantificationSettings = quantificationSettings;
            _normalizationData = normalizationData;
        }


        public static PeptideQuantifier GetPeptideQuantifier(NormalizedValueCalculator normalizedValueCalculator, SrmSettings srmSettings, PeptideGroup peptideGroup, PeptideDocNode peptide)
        {
            var mods = srmSettings.PeptideSettings.Modifications;
            // Quantify on all label types which are not internal standards.
            ICollection<IsotopeLabelType> labelTypes = ImmutableList.ValueOf(mods.GetModificationTypes()
                .Except(mods.InternalStandardTypes));
            return new PeptideQuantifier(normalizedValueCalculator, peptideGroup, peptide, srmSettings.PeptideSettings.Quantification)
            {
                MeasuredLabelTypes = labelTypes,
                IncludeTruncatedPeaks = srmSettings.TransitionSettings.Instrument.TriggeredAcquisition
            };
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

        /// <summary>
        /// When true, transitions that are missing or have a non-positive area in a replicate
        /// are imputed with a small positive value (max(0.5 * 1st percentile of positive
        /// intensities, 1.0)) before the transitions are summarized into a peptide abundance.
        /// This mirrors skyline-prism, which imputes missing/zero transitions before every
        /// rollup. When false, such transitions are simply omitted from the summarization.
        /// </summary>
        public bool ImputeMissingValues { get; set; }

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
            return GetTransitionIntensities(srmSettings, replicateIndex, treatMissingAsZero, NormalizationMethod);
        }

        public IDictionary<IdentityPath, Quantity> GetTransitionIntensities(SrmSettings srmSettings, int replicateIndex,
            bool treatMissingAsZero, NormalizationMethod normalizationMethod)
        {
            var quantities = new Dictionary<IdentityPath, Quantity>();
            var transitionsToNormalizeAgainst = normalizationMethod is NormalizationMethod.RatioToLabel
                ? GetTransitionsToNormalizeAgainst(srmSettings, PeptideDocNode, replicateIndex)
                : null;
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
                    var quantity = GetTransitionQuantity(srmSettings, transitionsToNormalizeAgainst, normalizationMethod, replicateIndex, precursor,
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

        /// <summary>
        /// Returns a single retention time for this peptide: the mean of the per-precursor peak
        /// retention times over every measured (precursor, replicate, file). This is one RT per
        /// peptide, used for RT_LOESS normalization so the correction is evaluated at the
        /// peptide's characteristic RT in every replicate - including replicates where the
        /// peptide was not measured. It approximates skyline-prism's per-peptide "mean_rt"
        /// (the mean transition RetentionTime across replicates); the small per-transition vs
        /// per-precursor difference is immaterial to the smooth LOESS correction. Returns NaN
        /// if no retention time is available.
        /// </summary>
        public double GetPeptideMeanRetentionTime(SrmSettings settings)
        {
            if (settings.MeasuredResults == null)
            {
                return double.NaN;
            }
            int replicateCount = settings.MeasuredResults.Chromatograms.Count;
            double sum = 0;
            int count = 0;
            foreach (var precursor in PeptideDocNode.TransitionGroups)
            {
                if (SkipTransitionGroup(precursor) || precursor.Results == null)
                {
                    continue;
                }
                for (int iReplicate = 0; iReplicate < replicateCount && iReplicate < precursor.Results.Count; iReplicate++)
                {
                    var chromInfoList = precursor.Results[iReplicate];
                    if (chromInfoList.IsEmpty)
                    {
                        continue;
                    }
                    foreach (var chromInfo in chromInfoList)
                    {
                        if (chromInfo == null || chromInfo.OptimizationStep != 0)
                        {
                            continue;
                        }
                        if (chromInfo.RetentionTime.HasValue)
                        {
                            sum += chromInfo.RetentionTime.Value;
                            count++;
                        }
                    }
                }
            }
            return count > 0 ? sum / count : double.NaN;
        }

        public static bool IncludeInMedianPolish(SampleType sampleType)
        {
            return SampleType.STANDARD.Equals(sampleType) || SampleType.QC.Equals(sampleType) ||
                   SampleType.UNKNOWN.Equals(sampleType);
        }

        public static HashSet<int> GetMedianPolishReplicates(SrmSettings settings)
        {
            return Enumerable.Range(0, settings.MeasuredResults?.Chromatograms.Count ?? 0)
                .Where(i => IncludeInMedianPolish(settings.MeasuredResults.Chromatograms[i].SampleType))
                .ToHashSet();
        }

        public double?[] GetPeptideLog2Abundances(SrmSettings settings, HashSet<int> replicateIndexes,
            SummarizationMethod peptideSummarizationMethod)
        {
            return GetPeptideLog2Abundances(settings, replicateIndexes, peptideSummarizationMethod,
                NormalizationMethod);
        }

        /// <summary>
        /// Returns per-replicate log2 peptide abundance using the chosen peptide-level
        /// summarization method. For MEDIANPOLISH, returns the polish row effect plus
        /// scale factor (same as <see cref="GetMedianPolishQuantities"/>); for AVERAGING,
        /// returns log2 of the per-replicate transition-area sum.
        /// </summary>
        public double?[] GetPeptideLog2Abundances(SrmSettings settings, HashSet<int> replicateIndexes,
            SummarizationMethod peptideSummarizationMethod, NormalizationMethod normalizationMethod)
        {
            if (Equals(peptideSummarizationMethod, SummarizationMethod.MEDIANPOLISH))
            {
                return GetMedianPolishQuantities(settings, replicateIndexes);
            }
            int replicateCount = settings.MeasuredResults?.Chromatograms?.Count ?? 0;
            var result = new double?[replicateCount];
            if (ImputeMissingValues)
            {
                // Sum the same imputed transition matrix the median polish uses, so the summed
                // result matches skyline-prism's "sum" rollup (which also imputes missing/zero
                // transitions before aggregating).
                var replicateValues = GetTransitionLog2Abundances(settings, replicateCount, normalizationMethod);
                for (int i = 0; i < replicateCount; i++)
                {
                    double sum = 0;
                    bool any = false;
                    foreach (var log2Abundance in replicateValues[i].Values)
                    {
                        sum += Math.Pow(2, log2Abundance);
                        any = true;
                    }
                    if (any && sum > 0)
                    {
                        result[i] = Math.Log(sum, 2);
                    }
                }
                return result;
            }

            var quantificationSettings = QuantificationSettings.ChangeNormalizationMethod(normalizationMethod);
            for (int i = 0; i < replicateCount; i++)
            {
                var transitionIntensities = GetTransitionIntensities(settings, i, false);
                if (transitionIntensities.Count == 0)
                {
                    continue;
                }
                var transitionKeys = transitionIntensities.Keys.ToHashSet();
                var sum = SumTransitionQuantities(transitionKeys, transitionIntensities, quantificationSettings);
                if (sum != null && sum.Raw > 0)
                {
                    result[i] = Math.Log(sum.Raw, 2);
                }
            }
            return result;
        }

        public double?[] GetMedianPolishQuantities(SrmSettings settings, HashSet<int> replicateIndexes)
        {
            // For NormalizationMethod=EQUALIZE_MEDIANS or RT_LOESS combined with
            // PeptideSummarizationMethod=MEDIANPOLISH, the normalization factor must be derived
            // from the post-rollup peptide abundances, not from the raw transition values.
            // (This matches skyline-prism's pipeline, where median normalization and
            // RT-lowess normalization run after the transition-to-peptide rollup.)
            // To do that, polish on un-normalized transition areas first, then subtract a
            // per-replicate adjustment computed from all peptides' polished values.
            var nm = NormalizationMethod;
            bool peptideLevelAdjustment = Equals(nm, NormalizationMethod.EQUALIZE_MEDIANS)
                                          || Equals(nm, NormalizationMethod.RT_LOESS);
            var nmForPolish = peptideLevelAdjustment ? NormalizationMethod.NONE : nm;

            var polished = MedianPolishWithMethod(settings, replicateIndexes, nmForPolish);
            if (peptideLevelAdjustment)
            {
                ApplyPeptideLevelAdjustment(settings, polished, nm);
            }
            return polished;
        }

        public double?[] PolishUnnormalizedTransitions(SrmSettings settings, HashSet<int> replicateIndexes)
        {
            return MedianPolishWithMethod(settings, replicateIndexes, NormalizationMethod.NONE);
        }

        private double?[] MedianPolishWithMethod(SrmSettings settings, HashSet<int> replicateIndexes,
            NormalizationMethod normalizationMethod)
        {
            if (settings.MeasuredResults == null)
            {
                return Array.Empty<double?>();
            }

            int replicateCount = settings.MeasuredResults.Chromatograms.Count;
            var replicateValues = GetTransitionLog2Abundances(settings, replicateCount, normalizationMethod);
            return new MedianPolisher() { IncludeScaleFactor = true, IterateToConvergence = true }
                .Polish(replicateValues, replicateIndexes);
        }

        /// <summary>
        /// Builds the per-replicate transition log2 abundances that get summarized into a
        /// peptide-level abundance, returning one dictionary per replicate mapping transition
        /// identity to its log2 abundance.
        ///
        /// When <see cref="ImputeMissingValues"/> is true, every transition observed in any
        /// replicate contributes to every replicate; cells with no observation or a
        /// non-positive area are filled with an imputed low value
        /// (max(0.5 * 1st percentile of positive intensities, 1.0)), matching how
        /// skyline-prism builds its transition-by-sample matrix before a rollup. When false,
        /// only observed positive-area transitions are included.
        /// </summary>
        private List<IDictionary<IdentityPath, double>> GetTransitionLog2Abundances(SrmSettings settings,
            int replicateCount, NormalizationMethod normalizationMethod)
        {
            // Pass 1: collect per-replicate transition quantities, all observed transition
            // keys, and the positive raw intensities used to derive an imputation value.
            var perReplicateQuantities = new List<Dictionary<IdentityPath, Quantity>>(replicateCount);
            var allKeys = new HashSet<IdentityPath>();
            var positiveIntensities = new List<double>();
            for (int iReplicate = 0; iReplicate < replicateCount; iReplicate++)
            {
                var transitionIntensities = GetTransitionIntensities(settings, iReplicate, false, normalizationMethod);
                var quantities = new Dictionary<IdentityPath, Quantity>();
                foreach (var entry in transitionIntensities)
                {
                    if (entry.Value.Truncated)
                    {
                        continue;
                    }
                    quantities[entry.Key] = entry.Value;
                    allKeys.Add(entry.Key);
                    if (entry.Value.Intensity > 0)
                    {
                        positiveIntensities.Add(entry.Value.Intensity);
                    }
                }
                perReplicateQuantities.Add(quantities);
            }

            // Per-peptide imputation value for missing or zero cells. Mirrors skyline-prism:
            // impute = max(0.5 * P1(positive intensities for this peptide), 1.0). Filling
            // every gap with the same low-positive value keeps the polish from treating
            // missing measurements as outliers, and matches how skyline-prism builds the
            // transition-by-sample matrix before its rollup.
            double imputeIntensity = 1.0;
            if (positiveIntensities.Count > 0)
            {
                double p1 = new Util.Statistics(positiveIntensities).Percentile(0.01);
                imputeIntensity = Math.Max(p1 * 0.5, 1.0);
            }

            // Pass 2: build the log2 abundance per replicate. When imputing, every transition
            // key contributes to every replicate; cells with no observation or zero area are
            // filled with imputeIntensity (paired with the denominator from any other
            // replicate's observation of the same key, or 1.0 if the key was never observed
            // with a valid denominator). When not imputing, missing/zero cells are omitted.
            var replicateValues = new List<IDictionary<IdentityPath, double>>(replicateCount);
            for (int iReplicate = 0; iReplicate < replicateCount; iReplicate++)
            {
                var quantities = perReplicateQuantities[iReplicate];
                var abundances = new Dictionary<IdentityPath, double>();
                foreach (var key in allKeys)
                {
                    quantities.TryGetValue(key, out var q);
                    double intensity;
                    double denominator;
                    if (q != null && q.Intensity > 0)
                    {
                        intensity = q.Intensity;
                        denominator = q.Denominator;
                    }
                    else
                    {
                        if (!ImputeMissingValues)
                        {
                            continue;
                        }
                        intensity = imputeIntensity;
                        denominator = q?.Denominator ?? 1.0;
                    }
                    double log2Abundance = GroupComparer.CalcLog2Abundance(intensity, denominator);
                    if (!double.IsNaN(log2Abundance) && !double.IsInfinity(log2Abundance))
                    {
                        abundances[key] = log2Abundance;
                    }
                }
                replicateValues.Add(abundances);
            }
            return replicateValues;
        }

        private void ApplyPeptideLevelAdjustment(SrmSettings settings, double?[] polished,
            NormalizationMethod normalizationMethod)
        {
            var polishedAbundances = _normalizationData.GetPolishedPeptideAbundances();
            if (polishedAbundances == null || !polishedAbundances.HasData)
            {
                return;
            }
            bool useRtLoess = Equals(normalizationMethod, NormalizationMethod.RT_LOESS);
            // RT_LOESS uses a single mean retention time per peptide for every replicate
            // (matching skyline-prism's per-peptide mean_rt), so peptides not measured in a
            // replicate still get the correction at the peptide's characteristic RT.
            double peptideRt = useRtLoess ? GetPeptideMeanRetentionTime(settings) : double.NaN;
            var measuredResults = settings.MeasuredResults;
            for (int iReplicate = 0; iReplicate < polished.Length; iReplicate++)
            {
                if (!polished[iReplicate].HasValue)
                {
                    continue;
                }
                if (measuredResults == null || iReplicate >= measuredResults.Chromatograms.Count)
                {
                    continue;
                }
                if (useRtLoess && double.IsNaN(peptideRt))
                {
                    polished[iReplicate] = null;
                    continue;
                }
                var chromatogramSet = measuredResults.Chromatograms[iReplicate];
                double? totalAdjustment = null;
                int adjustmentCount = 0;
                foreach (var fileInfo in chromatogramSet.MSDataFileInfos)
                {
                    double? adj = useRtLoess
                        ? polishedAbundances.GetRtLoessAdjustment(iReplicate, fileInfo.FileId, peptideRt)
                        : polishedAbundances.GetMedianAdjustment(iReplicate, fileInfo.FileId);
                    if (adj.HasValue)
                    {
                        totalAdjustment = (totalAdjustment ?? 0) + adj.Value;
                        adjustmentCount++;
                    }
                }
                if (adjustmentCount == 0)
                {
                    polished[iReplicate] = null;
                    continue;
                }
                polished[iReplicate] -= totalAdjustment.Value / adjustmentCount;
            }
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
                    var normalizationData = _normalizationData.GetNormalizationData();
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
                else if (Equals(normalizationMethod, NormalizationMethod.RT_LOESS))
                {
                    var rtLoessCurves = _normalizationData.GetRtLoessCurves();
                    if (null == rtLoessCurves)
                    {
                        throw new InvalidOperationException(string.Format(@"Normalization method '{0}' is not supported here.", NormalizationMethod));
                    }
                    double? rtLoessAdjustment = rtLoessCurves.GetAdjustment(
                        replicateIndex, chromInfo.FileId, chromInfo.RetentionTime);
                    if (!rtLoessAdjustment.HasValue)
                    {
                        return null;
                    }
                    normalizedArea /= Math.Pow(2.0, rtLoessAdjustment.Value);
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
