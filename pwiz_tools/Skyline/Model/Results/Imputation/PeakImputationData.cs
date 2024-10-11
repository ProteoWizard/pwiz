using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EnvDTE;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class PeakImputationData
    {
        public static readonly Producer<Parameters, PeakImputationData> PRODUCER = new DataProducer();
        public PeakImputationData(Parameters parameters, AlignmentResults alignmentResults, ChromatogramTimeRanges chromatogramTimeRanges, IList<MoleculePeaks> moleculePeaksList)
        {
            Params = parameters;
            Alignments = alignmentResults;
            var sortedScores = ImmutableList.ValueOf(moleculePeaksList
                .SelectMany(molecule => molecule.Peaks.Select(peak => peak.Score)).OfType<double>()
                .Select(score => (float)score));

            ScoreConversionData = new ScoreConversionData(sortedScores);
            ChromatogramTimeRanges = chromatogramTimeRanges;

            var ratedMoleculePeaks = new List<MoleculePeaks>();
            foreach (var moleculePeaks in moleculePeaksList)
            {
                ratedMoleculePeaks.Add(RatePeaks(parameters, moleculePeaks));
            }
            MoleculePeaks = ImmutableList.ValueOf(ratedMoleculePeaks);
            MeanRtStdDev = GetMeanRtStandardDeviation(parameters.Document, alignmentResults);
        }

        public Parameters Params { get; }
        public AlignmentResults Alignments { get; }
        public ChromatogramTimeRanges ChromatogramTimeRanges { get; }

        public ScoreConversionData ScoreConversionData { get; }
        public ImmutableList<MoleculePeaks> MoleculePeaks { get; }
        public double? MeanRtStdDev { get; }

        public RatedPeak FillInScores(RatedPeak peak)
        {
            if (peak.Score.HasValue)
            {
                peak = peak.ChangePercentile(ScoreConversionData.GetPercentileOfScore(peak.Score.Value));
                // peak = peak.ChangeQValue(ScoringResults.ScoreQValueMap?.GetQValue(peak.Score));
                if (!Equals(Params.ScoringModel, LegacyScoringModel.DEFAULT_MODEL))
                {
                    peak = peak.ChangePValue(CutoffScoreType.PVALUE.FromRawScore(ScoreConversionData, peak.Score.Value));
                }
            }

            return peak;
        }

        public class Parameters : Immutable
        {
            public Parameters(SrmDocument document)
            {
                Document = document;
            }

            public SrmDocument Document { get; }

            public bool OverwriteManualPeaks { get; private set; }

            public Parameters ChangeOverwriteManualPeaks(bool value)
            {
                return ChangeProp(ImClone(this), im => im.OverwriteManualPeaks = value);
            }

            public double? CutoffScore { get; private set; }

            public CutoffScoreType CutoffScoreType { get; private set; }

            public Parameters ChangeCutoffScore(CutoffScoreType type, double? value)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.CutoffScoreType = type;
                    im.CutoffScore = value;
                });
            }

            public PeakScoringModelSpec ScoringModel { get; private set; }

            public Parameters ChangeScoringModel(PeakScoringModelSpec value)
            {
                return ChangeProp(ImClone(this), im => im.ScoringModel = value);
            }

            public RtValueType RtValueType { get; private set; }

            public Parameters ChangeRtValueType(RtValueType value)
            {
                return ChangeProp(ImClone(this), im => im.RtValueType = value);
            }

            public AlignmentType AlignmentType { get; private set; }

            public Parameters ChangeAlignmentType(AlignmentType value)
            {
                return ChangeProp(ImClone(this), im => im.AlignmentType = value);
            }

            public double? MaxPeakWidthVariation { get; private set; }

            public Parameters ChangeMaxPeakWidthVariation(double? value)
            {
                return ChangeProp(ImClone(this), im => im.MaxPeakWidthVariation = value);
            }

            public ImmutableList<IdentityPath> PeptideIdentityPaths { get; private set; }

            public Parameters ChangePeptideIdentityPaths(ImmutableList<IdentityPath> value)
            {
                return ChangeProp(ImClone(this), im => im.PeptideIdentityPaths = value);
            }

            public AlignmentParameters GetAlignmentParameters()
            {
                if (RtValueType == null || AlignmentType == null)
                {
                    return null;
                }

                return new AlignmentParameters(Document, RtValueType, AlignmentType);
            }

            public double? AllowableRtShift { get; private set; }

            public Parameters ChangeAllowableRtShift(double? value)
            {
                return ChangeProp(ImClone(this), im => im.AllowableRtShift = value);
            }

            protected bool Equals(Parameters other)
            {
                return ReferenceEquals(Document, other.Document) && OverwriteManualPeaks == other.OverwriteManualPeaks &&
                       Nullable.Equals(CutoffScore, other.CutoffScore) &&
                       Equals(CutoffScoreType, other.CutoffScoreType) && Equals(ScoringModel, other.ScoringModel) &&
                       Equals(RtValueType, other.RtValueType) && 
                       Equals(AlignmentType, other.AlignmentType) &&
                       Nullable.Equals(AllowableRtShift, other.AllowableRtShift) && 
                       Equals(PeptideIdentityPaths, other.PeptideIdentityPaths) &&
                       Equals(MaxPeakWidthVariation, other.MaxPeakWidthVariation);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Parameters)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Document != null ? RuntimeHelpers.GetHashCode(Document) : 0;
                    hashCode = (hashCode * 397) ^ OverwriteManualPeaks.GetHashCode();
                    hashCode = (hashCode * 397) ^ CutoffScore.GetHashCode();
                    hashCode = (hashCode * 397) ^ (CutoffScoreType != null ? CutoffScoreType.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (ScoringModel != null ? ScoringModel.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (AlignmentType != null ? AlignmentType.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ AllowableRtShift.GetHashCode();
                    hashCode = (hashCode * 397) ^
                               (PeptideIdentityPaths != null ? PeptideIdentityPaths.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ MaxPeakWidthVariation.GetHashCode();
                    return hashCode;
                }
            }
        }
        public static bool IsManualIntegrated(PeptideDocNode peptideDocNode, int replicateIndex,
            ChromFileInfoId fileId)
        {
            return EnumerateTransitionGroupChromInfos(peptideDocNode, replicateIndex, fileId)
                .Any(transitionGroupChromInfo => transitionGroupChromInfo.UserSet == UserSet.TRUE);
        }

        public static float? GetQValue(PeptideDocNode peptideDocNode, int replicateIndex, ChromFileInfoId fileId)
        {
            return EnumerateTransitionGroupChromInfos(peptideDocNode, replicateIndex, fileId)
                .Select(transitionGroupInfo=>transitionGroupInfo.QValue).FirstOrDefault();
        }

        private static IEnumerable<TransitionGroupChromInfo> EnumerateTransitionGroupChromInfos(
            PeptideDocNode peptideDocNode, int replicateIndex,
            ChromFileInfoId fileId)
        {
            return peptideDocNode.TransitionGroups.SelectMany(tg => tg.GetSafeChromInfo(replicateIndex))
                .Where(tgci => ReferenceEquals(tgci.FileId, fileId));
        }
        private class DataProducer : Producer<Parameters, PeakImputationData>
        {
            public override PeakImputationData ProduceResult(ProductionMonitor productionMonitor, Parameters parameter, IDictionary<WorkOrder, object> inputs)
            {
                var consensusAlignment =
                    AlignmentParameters.ALIGNMENT_PRODUCER.GetResult(inputs, parameter.GetAlignmentParameters());
                var chromatogramTimeRanges = ChromatogramTimeRanges.PRODUCER.GetResult(inputs,
                    new ChromatogramTimeRanges.Parameter(parameter.Document.MeasuredResults, true));
                var rows = ImmutableList.ValueOf(GetRows(productionMonitor, parameter, consensusAlignment, chromatogramTimeRanges));
                // rows = EnsureScores(rows);
                return new PeakImputationData(parameter, consensusAlignment, chromatogramTimeRanges, rows);
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameters parameter)
            {
                SrmDocument document = parameter.Document;
                if (document.MeasuredResults == null)
                {
                    yield break;
                }

                yield return Imputation.ChromatogramTimeRanges.PRODUCER.MakeWorkOrder(
                    new ChromatogramTimeRanges.Parameter(document.MeasuredResults, true));
                if (parameter.GetAlignmentParameters() != null)
                {
                    yield return parameter.GetAlignmentParameters().MakeWorkOrder();
                }
            }

            private IEnumerable<MoleculePeaks> GetRows(ProductionMonitor productionMonitor, Parameters parameters, AlignmentResults alignments, ChromatogramTimeRanges chromatogramTimeRanges)
            {
                var peptideIdentityPaths = parameters.PeptideIdentityPaths?.ToHashSet();
                var document = parameters.Document;
                var measuredResults = document.MeasuredResults;
                if (measuredResults == null)
                {
                    yield break;
                }

                Dictionary<Target, double> standardTimes = null;
                if (alignments?.StandardTimes != null)
                {
                    standardTimes = CollectionUtil.SafeToDictionary(alignments.StandardTimes);
                }
                var resultFileInfos = ReplicateFileInfo.List(document.MeasuredResults);
                var resultFileInfoDict =
                    resultFileInfos.ToDictionary(resultFileInfo => ReferenceValue.Of(resultFileInfo.ReplicateFileId.FileId));
                int moleculeIndex = 0;
                int moleculeCount = peptideIdentityPaths?.Count ?? document.MoleculeCount;
                foreach (var moleculeGroup in document.MoleculeGroups)
                {
                    foreach (var molecule in moleculeGroup.Molecules)
                    {
                        productionMonitor.CancellationToken.ThrowIfCancellationRequested();
                        var timeRanges = chromatogramTimeRanges?.GetTimeRanges(molecule);
                        var peptideIdentityPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
                        if (false == peptideIdentityPaths?.Contains(peptideIdentityPath))
                        {
                            continue;
                        }
                        productionMonitor.SetProgress(moleculeIndex * 100 / moleculeCount);
                        moleculeIndex++;
                        if (peptideIdentityPaths == null && molecule.GlobalStandardType != null)
                        {
                            continue;
                        }

                        if (molecule.Children.Count == 0)
                        {
                            continue;
                        }
                        var peaks = new List<RatedPeak>();
                        for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
                        {
                            foreach (var peptideChromInfo in molecule.GetSafeChromInfo(replicateIndex))
                            {
                                productionMonitor.CancellationToken.ThrowIfCancellationRequested();
                                if (!resultFileInfoDict.TryGetValue(peptideChromInfo.FileId, out var peakResultFile))
                                {
                                    // Shouldn't happen
                                    continue;
                                }

                                var chromFileInfo = document.MeasuredResults.Chromatograms[replicateIndex]
                                    .GetFileInfo(peptideChromInfo.FileId);
                                PeptideDocNode scoredMolecule = molecule;
                                bool manuallyIntegrated = IsManualIntegrated(molecule, replicateIndex, peptideChromInfo.FileId);
                                if (manuallyIntegrated)
                                {
                                    if (!parameters.OverwriteManualPeaks)
                                    {
                                        continue;
                                    }
                                }
                                
                                var rawPeakBounds = GetRawPeakBounds(scoredMolecule,
                                    replicateIndex,
                                    peptideChromInfo.FileId);
                                var onDemandFeatureCalculator = new OnDemandFeatureCalculator(
                                    parameters.ScoringModel.PeakFeatureCalculators, parameters.Document.Settings,
                                    molecule, replicateIndex, chromFileInfo);
                                var candidatePeakGroups = molecule.TransitionGroups.SelectMany(tg =>
                                    onDemandFeatureCalculator.GetCandidatePeakGroups(tg.TransitionGroup)).ToList();
                                CandidatePeakGroupData matchingPeakGroup = null;
                                if (manuallyIntegrated)
                                {
                                    matchingPeakGroup = candidatePeakGroups
                                        .OrderByDescending(group => group.Score.ModelScore).FirstOrDefault();
                                }
                                else
                                {
                                    if (rawPeakBounds != null)
                                    {
                                        matchingPeakGroup = candidatePeakGroups.FirstOrDefault(peakGroupData =>
                                            peakGroupData.MinStartTime == rawPeakBounds.StartTime &&
                                            peakGroupData.MaxEndTime == rawPeakBounds.EndTime);
                                    }
                                    if (matchingPeakGroup == null)
                                    {
                                        matchingPeakGroup =
                                            onDemandFeatureCalculator.GetChosenPeakGroupData(molecule.TransitionGroups
                                                .First().TransitionGroup);
                                    }
                                }
                                double? score = matchingPeakGroup?.Score.ModelScore;
                                var timeIntervals = timeRanges?.GetTimeIntervals(peakResultFile.MsDataFileUri);
                                var peak = new RatedPeak(peakResultFile, alignments?.GetAlignment(peakResultFile.ReplicateFileId), timeIntervals, rawPeakBounds, score,
                                    manuallyIntegrated);
                                var explicitPeakBounds =
                                    document.Settings.GetExplicitPeakBounds(molecule, chromFileInfo.FilePath);
                                if (rawPeakBounds != null && explicitPeakBounds?.Score != null && explicitPeakBounds.StartTime < rawPeakBounds.EndTime && explicitPeakBounds.EndTime > rawPeakBounds.StartTime)
                                {
                                    peak = peak.ChangeQValue(explicitPeakBounds.Score);
                                }

                                peaks.Add(peak);
                            }
                        }
                        var moleculePeaks = new MoleculePeaks(peptideIdentityPath, peaks);
                        if (true == standardTimes?.TryGetValue(molecule.ModifiedTarget, out var standardTime))
                        {
                            moleculePeaks = moleculePeaks.ChangeAlignmentStandardTime(standardTime);
                        }
                        yield return moleculePeaks;
                    }
                }
            }
        }

        public static RatedPeak.PeakBounds GetRawPeakBounds(PeptideDocNode peptideDocNode, int replicateIndex,
            ChromFileInfoId chromFileInfoId)
        {
            var peptideChromInfo = peptideDocNode.GetSafeChromInfo(replicateIndex)
                .FirstOrDefault(chromInfo => ReferenceEquals(chromInfo.FileId, chromFileInfoId));
            if (peptideChromInfo?.RetentionTime == null)
            {
                return null;
            }

            double apexTime = peptideChromInfo.RetentionTime.Value;
            double startTime = apexTime;
            double endTime = apexTime;
            foreach (var transitionGroup in peptideDocNode.TransitionGroups)
            {
                foreach (var chromInfo in transitionGroup.GetSafeChromInfo(replicateIndex))
                {
                    if (!ReferenceEquals(chromFileInfoId, chromInfo.FileId))
                    {
                        continue;
                    }

                    if (chromInfo.StartRetentionTime.HasValue)
                    {
                        startTime = Math.Min(startTime, chromInfo.StartRetentionTime.Value);
                    }

                    if (chromInfo.EndRetentionTime.HasValue)
                    {
                        endTime = Math.Max(endTime, chromInfo.EndRetentionTime.Value);
                    }
                }
            }

            return new RatedPeak.PeakBounds(startTime, endTime);
        }

        private ChromatogramTimeRanges.TimeRangeDict GetTimeIntervals(MoleculePeaks moleculePeaks)
        {
            if (ChromatogramTimeRanges == null)
            {
                return null;
            }
            var peptideDocNode = (PeptideDocNode) Params.Document.FindNode(moleculePeaks.PeptideIdentityPath);
            if (peptideDocNode == null)
            {
                return null;
            }

            return ChromatogramTimeRanges.GetTimeRanges(peptideDocNode);
        }

        private MoleculePeaks RatePeaks(Parameters parameters, MoleculePeaks moleculePeaks)
        {
            var peaks = new List<RatedPeak>();
            var timeRanges = GetTimeIntervals(moleculePeaks);
            peaks.AddRange(MarkExemplaryPeaks(parameters, moleculePeaks.Peaks.Select(FillInScores).ToList()));
            var exemplaryPeaks = peaks.Where(peak => peak.PeakVerdict == RatedPeak.Verdict.Exemplary).ToList();
            if (exemplaryPeaks.Count == 0)
            {
                return moleculePeaks.ChangePeaks(peaks.Select(peak=>peak.ChangeVerdict(RatedPeak.Verdict.Unknown, "No exemplary peaks")), null, null);
            }
            
            var bestPeak = exemplaryPeaks.First();
            var exemplaryPeakBounds = GetExemplaryPeakBounds(exemplaryPeaks);
            var peptideDocNode = (PeptideDocNode) parameters.Document.FindNode(moleculePeaks.PeptideIdentityPath);
            peaks = peaks.Select(peak => MarkAcceptedPeak(parameters, peptideDocNode, exemplaryPeakBounds.ReverseAlignPreservingWidth(peak.AlignmentFunction), peak)).ToList();
            var peaksByFile = peaks.ToLookup(peak => peak.ReplicateFileInfo.ReplicateFileId);
            var peaksInOriginalOrder = moleculePeaks.Peaks.GroupBy(peak => peak.ReplicateFileInfo.ReplicateFileId)
                .SelectMany(group => peaksByFile[group.Key]);
            return moleculePeaks.ChangePeaks(peaksInOriginalOrder, bestPeak, exemplaryPeakBounds);
        }

        private IEnumerable<RatedPeak> MarkExemplaryPeaks(Parameters parameters, IList<RatedPeak> peaks)
        {
            bool firstExemplary = true;
            IEnumerable<RatedPeak> orderedPeaks;
            if (parameters.CutoffScoreType == CutoffScoreType.QVALUE)
            {
                orderedPeaks = peaks.OrderBy(peak => Tuple.Create(peak.QValue ?? double.PositiveInfinity, -peak.Score));
            }
            else
            {
                orderedPeaks = peaks.OrderByDescending(peak => peak.Score);
            }
            foreach (var peak in orderedPeaks)
            {
                if (peak.AlignedPeakBounds == null || !peak.Score.HasValue)
                {
                    yield return peak;
                    continue;
                }

                if (firstExemplary)
                {
                    firstExemplary = false;
                    string opinion;
                    if (parameters.CutoffScoreType == CutoffScoreType.QVALUE)
                    {
                        if (peaks.Select(p => p.QValue).Distinct().Count() == 1 && peaks.Select(p=>p.Score).Distinct().Count() > 1)
                        {
                            opinion =
                                string.Format("All of the q-values were the same but this one had a better {0} score.", parameters.ScoringModel.Name);
                        }
                        else
                        {
                            opinion = string.Format("Peak q-value {0} is lower than all other replicates", peak.QValue);
                        }
                    }
                    else
                    {
                        opinion = string.Format("Peak score {0} is higher than all other replicates",
                            peak.Score?.ToString(Formats.PEAK_SCORE));
                    }

                    yield return peak.ChangeVerdict(RatedPeak.Verdict.Exemplary, opinion);
                    continue;
                }

                yield return MarkExemplary(parameters, peak);
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private RatedPeak MarkAcceptedPeak(Parameters parameters, PeptideDocNode peptideDocNode, 
            RatedPeak.PeakBounds exemplaryPeakBounds,
            RatedPeak peak)
        {
            if (exemplaryPeakBounds == null)
            {
                return peak.ChangeVerdict(RatedPeak.Verdict.Accepted,
                    "Adjustment not possible because no exemplary peaks found.");
            }

            peak = peak.ChangeRtShift(peak.RawPeakBounds?.MidTime - exemplaryPeakBounds.MidTime);
            if (peak.PeakVerdict != RatedPeak.Verdict.Unknown)
            {
                return peak;
            }

            bool needsMoving = !peak.RtShift.HasValue || Math.Abs(peak.RtShift.Value) > parameters.AllowableRtShift;
            bool needsWidthChanged =
                !needsMoving && parameters.MaxPeakWidthVariation.HasValue &&
                Math.Abs(exemplaryPeakBounds.Width - peak.RawPeakBounds.Width) >
                parameters.MaxPeakWidthVariation * exemplaryPeakBounds.Width;
            if (!needsMoving && !needsWidthChanged)
            {
                return peak.ChangeVerdict(RatedPeak.Verdict.Accepted,
                    string.Format("Retention time {0} is within {1} of {2}", peak.RawPeakBounds.MidTime.ToString(Formats.RETENTION_TIME),
                        parameters.AllowableRtShift, exemplaryPeakBounds.MidTime.ToString(Formats.RETENTION_TIME)));
            }

            if (!peak.TimeIntervals.ContainsTime((float) exemplaryPeakBounds.MidTime))
            {
                var opinion = string.Format("Imputed retention time {0} is outside the chromatogram.", exemplaryPeakBounds.MidTime.ToString(Formats.RETENTION_TIME));
                if (peak.RawPeakBounds == null)
                {
                    return peak.ChangeVerdict(RatedPeak.Verdict.Accepted, opinion);
                }

                return peak.ChangeVerdict(RatedPeak.Verdict.NeedsRemoval, opinion);
            }

            if (needsWidthChanged)
            {
                var opinion = string.Format("Width {0} should be changed because more than {1} different from {2}",
                    peak.RawPeakBounds.Width.ToString(Formats.RETENTION_TIME),
                    parameters.MaxPeakWidthVariation.Value.ToString(Formats.Percent),
                    exemplaryPeakBounds.Width.ToString(Formats.RETENTION_TIME));
                return peak.ChangeVerdict(RatedPeak.Verdict.NeedsAdjustment, opinion);
            }
            return peak.ChangeVerdict(RatedPeak.Verdict.NeedsAdjustment,
                string.Format("Peak should be moved to {0}", exemplaryPeakBounds.MidTime.ToString(Formats.RETENTION_TIME)));
        }

        private RatedPeak.PeakBounds GetExemplaryPeakBounds(IList<RatedPeak> exemplaryPeaks)
        {
            if (exemplaryPeaks.Count == 0)
            {
                return null;
            }

            var alignedPeakBounds =
                exemplaryPeaks.Select(peak => peak.RawPeakBounds.AlignPreservingWidth(peak.AlignmentFunction)).ToList();
            if (alignedPeakBounds.Count == 0)
            {
                return null;
            }

            return new RatedPeak.PeakBounds(alignedPeakBounds.Average(peak => peak.StartTime),
                alignedPeakBounds.Average(peak => peak.EndTime));
        }

        private RatedPeak MarkExemplary(Parameters parameters, RatedPeak peak)
        {
            if (parameters.CutoffScoreType == CutoffScoreType.RAW && peak.Score >= parameters.CutoffScore)
            {
                return peak.ChangeVerdict(RatedPeak.Verdict.Exemplary,
                    string.Format("Score {0} is above cutoff {1}", peak.Score.Value.ToString(Formats.PEAK_SCORE), parameters.CutoffScore));
            }

            if (parameters.CutoffScoreType == CutoffScoreType.PERCENTILE &&
                peak.Percentile >= parameters.CutoffScore)
            {
                return peak.ChangeVerdict(RatedPeak.Verdict.Exemplary,
                    string.Format("Percentile {0} is above cutoff {1}", peak.Percentile.Value.ToString(@"P"), parameters.CutoffScore.Value.ToString(@"P")));
            }

            if (parameters.CutoffScoreType == CutoffScoreType.PVALUE && peak.PValue <= parameters.CutoffScore)
            {
                return peak.ChangeVerdict(RatedPeak.Verdict.Exemplary,
                    string.Format("P-value {0} is below cutoff {1}", peak.PValue.Value.ToString(Formats.PValue), parameters.CutoffScore));
            }

            if (parameters.CutoffScoreType == CutoffScoreType.QVALUE && peak.QValue <= parameters.CutoffScore)
            {
                return peak.ChangeVerdict(RatedPeak.Verdict.Exemplary,
                    string.Format("Q-value {0} is below cutoff {1}", peak.QValue.Value.ToString(Formats.PValue), parameters.CutoffScore));
            }

            return peak;
        }

        public static double? GetMeanRtStandardDeviation(SrmDocument document, AlignmentResults alignmentResults)
        {
            var standardDeviations = new List<double>();
            foreach (var molecule in document.Molecules)
            {
                if (!molecule.HasResults)
                {
                    continue;
                }

                var times = new List<double>();
                for (int i = 0; i < molecule.Results.Count; i++)
                {
                    foreach (var fileGroup in molecule.GetSafeChromInfo(i)
                                 .GroupBy(peptideChromInfo => ReferenceValue.Of(peptideChromInfo.FileId)))
                    {
                        AlignmentFunction alignmentFunction = AlignmentFunction.IDENTITY;
                        if (alignmentResults != null)
                        {
                            alignmentFunction = alignmentResults.GetAlignment(
                                new ReplicateFileId((ChromatogramSetId)document.MeasuredResults.Chromatograms[i].Id,
                                    fileGroup.Key));
                        }
                        var fileTimes = fileGroup.Select(peptideChromInfo => 
                                (double?)peptideChromInfo.RetentionTime)
                            .OfType<double>().Select(alignmentFunction.GetY).ToList();
                        if (fileTimes.Count > 0)
                        {
                            times.Add(fileTimes.Average());
                        }
                    }
                }

                if (times.Count > 1)
                {
                    var standardDeviation = times.StandardDeviation();
                    if (!double.IsNaN(standardDeviation))
                    {
                        standardDeviations.Add(standardDeviation);
                    }
                }
            }

            if (standardDeviations.Count == 0)
            {
                return null;
            }

            return standardDeviations.Average();
        }

        public static double? GetAveragePeakWidthCV(SrmDocument document)
        {
            var cvs= new List<double>();
            foreach (var molecule in document.Molecules)
            {
                if (!molecule.HasResults)
                {
                    continue;
                }

                var widths = new List<double>();
                for (int i = 0; i < molecule.Results.Count; i++)
                {
                    foreach (var group in molecule.TransitionGroups.SelectMany(tg => tg.GetSafeChromInfo(i))
                                 .GroupBy(chromInfo => ReferenceValue.Of(chromInfo.FileId)))
                    {
                        var fileWidths = group
                            .Select(chromInfo => (double?)chromInfo.EndRetentionTime - chromInfo.StartRetentionTime)
                            .OfType<double>().ToList();
                        if (fileWidths.Count > 0)
                        {
                            widths.Add(fileWidths.Average());
                        }
                    }
                }

                if (widths.Count > 1)
                {
                    var cv = widths.Variance() / widths.Mean();

                    if (!double.IsNaN(cv))
                    {
                        cvs.Add(cv);
                    }
                }
            }

            if (cvs.Count == 0)
            {
                return null;
            }

            return cvs.Average();

        }
    }
}
