using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
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

            SortedScores = ScoringResults.SortedScores;
            if (SortedScores == null)
            {
                SortedScores = ImmutableList.ValueOf(moleculePeaksList
                    .SelectMany(molecule => molecule.Peaks.Select(peak => peak.Score)).OfType<double>()
                    .Select(score => (float)score));
            }

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

        public ImmutableList<float> SortedScores { get; }

        public ImmutableList<MoleculePeaks> MoleculePeaks { get; }

        public RatedPeak FillInScores(RatedPeak peak)
        {
            if (peak.Score.HasValue)
            {
                peak = peak.ChangePercentile(GetPercentileOfScore(peak.Score.Value));
                peak = peak.ChangeQValue(ScoringResults.ScoreQValueMap?.GetQValue(peak.Score));
                if (CutoffScoreType.PVALUE.IsEnabled(Params.ScoringModel))
                {
                    peak = peak.ChangePValue(CutoffScoreType.PVALUE.FromRawScore(this, peak.Score.Value));
                }
            }

            return peak;
        }

        public bool HasAnyScores()
        {
            return MoleculePeaks.SelectMany(peaks => peaks.Peaks).Any(peak => peak.Score.HasValue);
        }

        public bool HasAnyQValues()
        {
            return MoleculePeaks.SelectMany(peaks => peaks.Peaks).Any(peak => peak.QValue.HasValue);
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

            public ScoringResults.Parameters GetScoringResultsParameters()
            {
                if (ScoringModel == null)
                {
                    return null;
                }

                return new ScoringResults.Parameters(Document, ScoringModel, OverwriteManualPeaks);
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
                       Nullable.Equals(AllowableRtShift, other.AllowableRtShift);
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
                    var hashCode = (Document != null ? RuntimeHelpers.GetHashCode(Document) : 0);
                    hashCode = (hashCode * 397) ^ OverwriteManualPeaks.GetHashCode();
                    hashCode = (hashCode * 397) ^ CutoffScore.GetHashCode();
                    hashCode = (hashCode * 397) ^ (CutoffScoreType != null ? CutoffScoreType.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (ScoringModel != null ? ScoringModel.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (AlignmentType != null ? AlignmentType.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ AllowableRtShift.GetHashCode();
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

            // private ImmutableList<MoleculePeaks> EnsureScores(ImmutableList<MoleculePeaks> rows)
            // {
            //     if (rows.Any(row => row.Peaks.Any(peak => peak.Score.HasValue)))
            //     {
            //         return rows;
            //     }
            //
            //     if (!rows.Any(row => row.Peaks.Any(peak => peak.QValue.HasValue)))
            //     {
            //         return rows;
            //     }
            //
            //     return ImmutableList.ValueOf(rows.Select(row => new MoleculePeaks(row.PeptideIdentityPath,
            //         row.Peaks.Select(peak => peak.ChangeScore(-peak.QValue)))));
            // }

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
                        new ScoringResults.Parameters(parameter.Document, parameter.ScoringModel, parameter.OverwriteManualPeaks));
                }
            }

            private IEnumerable<MoleculePeaks> GetRows(ProductionMonitor productionMonitor, Parameters parameters, ScoringResults scoringResults, ConsensusAlignment alignments)
            {
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
                foreach (var moleculeGroup in document.MoleculeGroups)
                {
                    foreach (var molecule in moleculeGroup.Molecules)
                    {
                        productionMonitor.CancellationToken.ThrowIfCancellationRequested();
                        productionMonitor.SetProgress(moleculeIndex * 100 / document.MoleculeCount);
                        moleculeIndex++;
                        var peptideIdentityPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
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

            private bool HasAnyScores(SrmDocument document)
            {
                return document.MoleculeTransitionGroups
                    .Where(tg => tg.Results != null)
                    .SelectMany(tg => tg.Results)
                    .SelectMany(chromInfoList => chromInfoList)
                    .Any(transitionGroupChromInfo =>
                        transitionGroupChromInfo.ZScore.HasValue);
            }

            private bool HasAnyQValues(SrmDocument document)
            {
                return document.MoleculeTransitionGroups.Where(tg => tg.Results != null)
                    .SelectMany(tg => tg.Results)
                    .SelectMany(chromInfoList => chromInfoList)
                    .Any(transitionGroupChromInfo =>
                        transitionGroupChromInfo.QValue.HasValue);
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

        public static double? GetValueAtPercentile(double percentile, IList<float> list)
        {
            if (list.Count == 0)
            {
                return null;
            }

            double doubleIndex = percentile * list.Count;
            if (doubleIndex <= 0)
            {
                return list[0];
            }

            if (doubleIndex >= list.Count - 1)
            {
                return list[list.Count - 1];
            }

            int prevIndex = (int)Math.Floor(doubleIndex);
            int nextIndex = (int)Math.Ceiling(doubleIndex);
            var prevValue = list[prevIndex];
            if (prevIndex == nextIndex)
            {
                return prevValue;
            }
            var nextValue = list[nextIndex];
            return prevValue * (nextIndex - doubleIndex) + nextValue * (doubleIndex - prevIndex);
        }
        public static double? GetPercentileOfValue(double value, IList<float> list)
        {
            if (list.Count == 0)
            {
                return null;
            }
            var index = CollectionUtil.BinarySearch(list, (float)value);
            if (index >= 0)
            {
                return (double)index / list.Count;
            }
            index = ~index;

            if (index <= 0)
            {
                return list[0];
            }

            if (index >= list.Count - 1)
            {
                return list[list.Count - 1];
            }

            double prev = list[index];
            double next = list[index + 1];
            return (index + (value - prev) / (next - prev)) / list.Count;
        }

        public double? GetPercentileOfScore(double score)
        {
            return GetPercentileOfValue(score, SortedScores);
        }

        public double? GetScoreAtPercentile(double percentile)
        {
            return GetValueAtPercentile(percentile, SortedScores);
        }
        private MoleculePeaks RatePeaks(Parameters parameters, MoleculePeaks moleculePeaks)
        {
            var exemplaryPeaks = FindExemplaryPeaks(parameters, moleculePeaks.Peaks.Select(FillInScores), out var remainingPeaks)
                .Select(peak => peak.ChangeVerdict(RatedPeak.Verdict.Exemplary)).ToList();
            if (exemplaryPeaks.Count == 0)
            {
                return moleculePeaks;
            }

            var bestPeak = exemplaryPeaks.First();
            var exemplaryPeakBounds = GetExemplaryPeakBounds(exemplaryPeaks);
            var acceptedPeaks = FindAcceptedPeaks(parameters, exemplaryPeakBounds, remainingPeaks, out remainingPeaks)
                .Select(peak => peak.ChangeVerdict(RatedPeak.Verdict.Accepted))
                .ToList();
            var exemplaryMidTime = exemplaryPeakBounds?.MidTime;
            var ratedPeaks = exemplaryPeaks.Concat(acceptedPeaks).Concat(remainingPeaks).Select(peak =>
                peak.ChangeRtShift(peak.AlignedPeakBounds?.MidTime - exemplaryMidTime));
            return moleculePeaks.ChangePeaks(ratedPeaks, bestPeak, exemplaryPeakBounds);
        }

        private IEnumerable<RatedPeak> FindExemplaryPeaks(Parameters parameters, IEnumerable<RatedPeak> peaks,
            out IList<RatedPeak> remainingPeaks)
        {
            List<RatedPeak> exemplaryPeaks = new List<RatedPeak>();
            remainingPeaks = new List<RatedPeak>();
            foreach (var peak in peaks.OrderByDescending(peak => peak.Score))
            {
                if (peak.AlignedPeakBounds == null)
                {
                    remainingPeaks.Add(peak);
                    continue;
                }

                if (exemplaryPeaks.Count == 0)
                {
                    exemplaryPeaks.Add(peak);
                    continue;
                }

                if (IsExemplary(parameters, peak))
                {
                    exemplaryPeaks.Add(peak);
                    continue;
                }
                remainingPeaks.Add(peak);
            }

            return exemplaryPeaks;
        }

        private IEnumerable<RatedPeak> FindAcceptedPeaks(Parameters parameters, RatedPeak.PeakBounds exemplaryPeakBounds,
            IEnumerable<RatedPeak> peaks,
            out IList<RatedPeak> remainingPeaks)
        {
            var acceptedPeaks = new List<RatedPeak>();
            remainingPeaks = new List<RatedPeak>();
            if (exemplaryPeakBounds == null)
            {
                remainingPeaks.AddRange(peaks);
                return Array.Empty<RatedPeak>();
            }
            foreach (var peak in peaks)
            {
                if (parameters.AllowableRtShift.HasValue)
                {
                    var rtShift = exemplaryPeakBounds.MidTime - peak.AlignedPeakBounds?.MidTime;
                    if (rtShift.HasValue && Math.Abs(rtShift.Value) <= parameters.AllowableRtShift)
                    {
                        acceptedPeaks.Add(peak);
                        continue;
                    }
                }
                remainingPeaks.Add(peak);
            }

            return acceptedPeaks;
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

        private bool IsAccepted(Parameters parameters, RatedPeak bestPeak, RatedPeak peak)
        {
            if (peak.AlignedPeakBounds == null)
            {
                return false;
            }
            if (parameters.CutoffScoreType == CutoffScoreType.RAW && peak.Score < parameters.CutoffScore)
            {
                return false;
            }

            if (parameters.CutoffScoreType == CutoffScoreType.PERCENTILE &&
                peak.Percentile < parameters.CutoffScore)
            {
                return false;
            }

            if (parameters.CutoffScoreType == CutoffScoreType.PVALUE && peak.PValue > parameters.CutoffScore)
            {
                return false;
            }

            if (parameters.CutoffScoreType == CutoffScoreType.QVALUE && peak.QValue > parameters.CutoffScore)
            {
                return false;
            }

            if (peak.AlignedPeakBounds.MidTime - bestPeak.AlignedPeakBounds.MidTime > parameters.AllowableRtShift)
            {
                return false;
            }

            return true;
        }

        private bool IsExemplary(Parameters parameters, RatedPeak peak)
        {
            if (parameters.CutoffScoreType == CutoffScoreType.RAW && peak.Score >= parameters.CutoffScore)
            {
                return true;
            }

            if (parameters.CutoffScoreType == CutoffScoreType.PERCENTILE &&
                peak.Percentile >= parameters.CutoffScore)
            {
                return true;
            }

            if (parameters.CutoffScoreType == CutoffScoreType.PVALUE && peak.PValue <= parameters.CutoffScore)
            {
                return true;
            }

            if (parameters.CutoffScoreType == CutoffScoreType.QVALUE && peak.QValue <= parameters.CutoffScore)
            {
                return true;
            }

            return false;
        }



    }
}
