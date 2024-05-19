using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class PeakImputationData
    {
        public static readonly Producer<Parameters, PeakImputationData> PRODUCER = new DataProducer();
        public PeakImputationData(Parameters parameters, ConsensusAlignment consensusAlignment, ScoringResults scoringResults, IList<MoleculePeaks> moleculePeaksList)
        {
            Params = parameters;
            ConsensusAlignment = consensusAlignment;
            ScoringResults = scoringResults;
            var sortedScores = ScoringResults.SortedScores;
            if (sortedScores == null)
            {
                sortedScores = ImmutableList.ValueOf(moleculePeaksList
                    .SelectMany(molecule => molecule.Peaks.Select(peak => peak.Score)).OfType<double>()
                    .Select(score => (float)score));
            }

            ScoreConversionData = new ScoreConversionData(ScoringResults.ScoreQValueMap, sortedScores);

            var ratedMoleculePeaks = new List<MoleculePeaks>();
            foreach (var moleculePeaks in moleculePeaksList)
            {
                ratedMoleculePeaks.Add(RatePeaks(parameters, moleculePeaks));
            }
            MoleculePeaks = ImmutableList.ValueOf(ratedMoleculePeaks);
        }

        public Parameters Params { get; }
        public ConsensusAlignment ConsensusAlignment { get; }
        public ScoringResults ScoringResults { get; }

        public ScoreConversionData ScoreConversionData { get; }
        public ImmutableList<MoleculePeaks> MoleculePeaks { get; }

        public RatedPeak FillInScores(RatedPeak peak)
        {
            if (peak.Score.HasValue)
            {
                peak = peak.ChangePercentile(ScoreConversionData.GetPercentileOfScore(peak.Score.Value));
                peak = peak.ChangeQValue(ScoringResults.ScoreQValueMap?.GetQValue(peak.Score));
                if (CutoffScoreType.PVALUE.IsEnabled(Params.ScoringModel))
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

            public RtValueType AlignmentType { get; private set; }

            public Parameters ChangeAlignmentType(RtValueType value)
            {
                return ChangeProp(ImClone(this), im => im.AlignmentType = value);
            }

            public ImmutableList<IdentityPath> PeptideIdentityPaths { get; private set; }

            public Parameters ChangePeptideIdentityPaths(ImmutableList<IdentityPath> value)
            {
                return ChangeProp(ImClone(this), im => im.PeptideIdentityPaths = value);
            }

            public ScoringResults.Parameters GetScoringResultsParameters()
            {
                if (ScoringModel == null)
                {
                    return null;
                }

                return new ScoringResults.Parameters(Document, ScoringModel, OverwriteManualPeaks, PeptideIdentityPaths);
            }

            public ConsensusAlignment.Parameters GetAlignmentParameters()
            {
                if (AlignmentType == null)
                {
                    return null;
                }

                return new ConsensusAlignment.Parameters(Document, AlignmentType);
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
                       Equals(AlignmentType, other.AlignmentType) &&
                       Nullable.Equals(AllowableRtShift, other.AllowableRtShift) && 
                       Equals(PeptideIdentityPaths, other.PeptideIdentityPaths);
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
                ScoringResults scoringResults = ScoringResults.PRODUCER.GetResult(inputs, parameter.GetScoringResultsParameters());
                var consensusAlignment =
                    ConsensusAlignment.PRODUCER.GetResult(inputs, parameter.GetAlignmentParameters());
                var rows = ImmutableList.ValueOf(GetRows(productionMonitor, parameter, scoringResults, consensusAlignment));
                // rows = EnsureScores(rows);
                return new PeakImputationData(parameter, consensusAlignment, scoringResults, rows);
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameters parameter)
            {
                SrmDocument document = parameter.Document;
                if (document.MeasuredResults == null)
                {
                    yield break;
                }

                if (parameter.AlignmentType != null)
                {
                    yield return ConsensusAlignment.PRODUCER.MakeWorkOrder(
                        new ConsensusAlignment.Parameters(document, parameter.AlignmentType));
                }

                if (parameter.ScoringModel != null)
                {
                    yield return ScoringResults.PRODUCER.MakeWorkOrder(
                        new ScoringResults.Parameters(parameter.Document, parameter.ScoringModel, parameter.OverwriteManualPeaks, parameter.PeptideIdentityPaths));
                }
            }

            private IEnumerable<MoleculePeaks> GetRows(ProductionMonitor productionMonitor, Parameters parameters, ScoringResults scoringResults, ConsensusAlignment alignments)
            {
                var peptideIdentityPaths = parameters.PeptideIdentityPaths?.ToHashSet();
                var document = scoringResults?.ReintegratedDocument ?? parameters.Document;
                var measuredResults = document.MeasuredResults;
                if (measuredResults == null)
                {
                    yield break;
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
                                bool manuallyIntegrated = IsManualIntegrated(molecule, replicateIndex, peptideChromInfo.FileId);

                                if (manuallyIntegrated)
                                {
                                    if (!parameters.OverwriteManualPeaks)
                                    {
                                        continue;
                                    }
                                }

                                var rawPeakBounds = GetRawPeakBounds(molecule,
                                    replicateIndex,
                                    peptideChromInfo.FileId);
                                double? score = EnumerateTransitionGroupChromInfos(molecule, replicateIndex,
                                        peptideChromInfo.FileId).Select(chromInfo => chromInfo.ZScore)
                                    .FirstOrDefault(value => value.HasValue);
                                if (score == null)
                                {
                                    score = -EnumerateTransitionGroupChromInfos(molecule, replicateIndex,
                                            peptideChromInfo.FileId).Select(chromInfo => chromInfo.QValue)
                                        .FirstOrDefault(value => value.HasValue);
                                }
                                var peak = new RatedPeak(peakResultFile, alignments?.GetAlignment(peakResultFile.ReplicateFileId), rawPeakBounds, score,
                                    manuallyIntegrated);
                                peaks.Add(peak);
                            }
                        }
                        yield return new MoleculePeaks(peptideIdentityPath, peaks);
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
                        endTime = Math.Min(endTime, chromInfo.EndRetentionTime.Value);
                    }
                }
            }

            return new RatedPeak.PeakBounds(startTime, endTime);
        }

        private MoleculePeaks RatePeaks(Parameters parameters, MoleculePeaks moleculePeaks)
        {
            var peaks = new List<RatedPeak>();
            peaks.AddRange(MarkExemplaryPeaks(parameters, moleculePeaks.Peaks.Select(FillInScores)));
            var exemplaryPeaks = peaks.Where(peak => peak.PeakVerdict == RatedPeak.Verdict.Exemplary).ToList();
            if (exemplaryPeaks.Count == 0)
            {
                return moleculePeaks.ChangePeaks(peaks.Select(peak=>peak.ChangeVerdict(RatedPeak.Verdict.Unknown, "No exemplary peaks")), null, null);
            }
            
            var bestPeak = exemplaryPeaks.First();
            var exemplaryPeakBounds = GetExemplaryPeakBounds(exemplaryPeaks);
            peaks = peaks.Select(peak => MarkAcceptedPeak(parameters, exemplaryPeakBounds, peak)).ToList();
            return moleculePeaks.ChangePeaks(peaks, bestPeak, exemplaryPeakBounds);
        }

        private IEnumerable<RatedPeak> MarkExemplaryPeaks(Parameters parameters, IEnumerable<RatedPeak> peaks)
        {
            bool firstExemplary = true;
            foreach (var peak in peaks.OrderByDescending(peak => peak.Score))
            {
                if (peak.AlignedPeakBounds == null || !peak.Score.HasValue)
                {
                    yield return peak;
                    continue;
                }

                if (firstExemplary)
                {
                    firstExemplary = false;
                    yield return peak.ChangeVerdict(RatedPeak.Verdict.Exemplary,
                        string.Format("Peak score {0} is higher than all other replicates", peak.Score?.ToString(Formats.PEAK_SCORE)));
                    continue;
                }

                yield return MarkExemplary(parameters, peak);
            }
        }

        private RatedPeak MarkAcceptedPeak(Parameters parameters, RatedPeak.PeakBounds exemplaryPeakBounds,
            RatedPeak peak)
        {
            peak = peak.ChangeRtShift(peak.AlignedPeakBounds?.MidTime - exemplaryPeakBounds?.MidTime);
            if (peak.PeakVerdict != RatedPeak.Verdict.Unknown)
            {
                return peak;
            }

            if (peak.AlignedPeakBounds == null)
            {
                return peak.ChangeVerdict(RatedPeak.Verdict.Rejected, "Missing peak");
            }

            if (parameters.AllowableRtShift.HasValue)
            {
                if (peak.RtShift.HasValue)
                {
                    if (Math.Abs(peak.RtShift.Value) <= parameters.AllowableRtShift)
                    {
                        return peak.ChangeVerdict(RatedPeak.Verdict.Accepted,
                            string.Format("Retention time {0} is within {1} of {2}", peak.AlignedPeakBounds.MidTime.ToString(Formats.RETENTION_TIME),
                                parameters.AllowableRtShift, exemplaryPeakBounds?.MidTime.ToString(Formats.RETENTION_TIME)));
                    }
                    else
                    {
                        return peak.ChangeVerdict(RatedPeak.Verdict.Rejected,
                            string.Format("Retention time {0} is not within {1} of {2}",
                                peak.AlignedPeakBounds.MidTime.ToString(Formats.RETENTION_TIME),
                                parameters.AllowableRtShift, exemplaryPeakBounds?.MidTime.ToString(Formats.RETENTION_TIME)));
                    }
                }
            }

            if (parameters.CutoffScore.HasValue)
            {
                return peak.ChangeVerdict(RatedPeak.Verdict.Rejected, "Peak was not good enough");
            }

            return peak.ChangeVerdict(RatedPeak.Verdict.Rejected, "There can be only one exemplary peak");
        }

        private RatedPeak.PeakBounds GetExemplaryPeakBounds(IList<RatedPeak> exemplaryPeaks)
        {
            if (exemplaryPeaks.Count == 0)
            {
                return null;
            }

            return new RatedPeak.PeakBounds(exemplaryPeaks.Average(peak => peak.AlignedPeakBounds.StartTime),
                exemplaryPeaks.Average(peak => peak.AlignedPeakBounds.EndTime));
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
    }
}
