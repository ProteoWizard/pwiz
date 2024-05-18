using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class PeakImputationData
    {
        public static readonly Producer<Parameters, PeakImputationData> PRODUCER = new DataProducer();
        public PeakImputationData(Parameters parameters, ConsensusAlignment consensusAlignment, ScoringResults scoringResults, IEnumerable<MoleculePeaks> moleculePeaks)
        {
            Params = parameters;
            ConsensusAlignment = consensusAlignment;
            ScoringResults = scoringResults;
            MoleculePeaks = ImmutableList.ValueOf(moleculePeaks.Select(FillInScores));
        }

        public Parameters Params { get; }
        public ConsensusAlignment ConsensusAlignment { get; }
        public ScoringResults ScoringResults { get; }

        public ImmutableList<MoleculePeaks> MoleculePeaks { get; }

        public MoleculePeaks FillInScores(MoleculePeaks row)
        {
            if (ScoringResults == null)
            {
                return row;
            }
            var newPeaks = row.Peaks.ToList();
            for (int iPeak = 0; iPeak < newPeaks.Count; iPeak++)
            {
                var peak = newPeaks[iPeak];
                if (!peak.Score.HasValue)
                {
                    continue;
                }

                peak = peak.ChangePercentile(ScoringResults.GetPercentileOfScore(peak.Score.Value));
                peak = peak.ChangeQValue(ScoringResults.ScoreQValueMap?.GetQValue(peak.Score));
                if (CutoffScoreType.PVALUE.IsEnabled(Params.ScoringModel))
                {
                    peak = peak.ChangePValue(CutoffScoreType.PVALUE.FromRawScore(ScoringResults, peak.Score.Value));
                }
                newPeaks[iPeak] = peak;
            }

            if (ArrayUtil.ReferencesEqual(newPeaks, row.Peaks))
            {
                return row;
            }

            return new MoleculePeaks(row.PeptideIdentityPath, newPeaks);
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
                return ChangeProp(ImClone(this), im => im.CutoffScore = value);
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
                var rows = ImmutableList.ValueOf(GetRows(productionMonitor.CancellationToken, parameter, scoringResults, consensusAlignment));
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

            private IEnumerable<MoleculePeaks> GetRows(CancellationToken cancellationToken, Parameters parameters, ScoringResults scoringResults, ConsensusAlignment alignments)
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
                bool useQValuesAsScores = scoringResults?.ResultsHandler == null && !HasAnyScores(document) &&
                                          HasAnyQValues(document);
                foreach (var moleculeGroup in document.MoleculeGroups)
                {
                    foreach (var molecule in moleculeGroup.Molecules)
                    {
                        if (molecule.GlobalStandardType != null)
                        {
                            continue;
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        var peptideIdentityPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
                        var peaks = new List<RatedPeak>();
                        for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
                        {
                            foreach (var peptideChromInfo in molecule.GetSafeChromInfo(replicateIndex))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (!resultFileInfoDict.TryGetValue(peptideChromInfo.FileId
                                        ,
                                        out var peakResultFile))
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
                                double? score;
                                if (useQValuesAsScores)
                                {
                                    score = -GetQValue(molecule, replicateIndex, peptideChromInfo.FileId);
                                }
                                else
                                {
                                    var peakFeatureStatistics = scoringResults?.ResultsHandler?.GetPeakFeatureStatistics(molecule.Peptide,
                                        peptideChromInfo.FileId);
                                    score = peakFeatureStatistics?.BestScore;
                                }
                                var peak = new RatedPeak(peakResultFile, alignments?.GetAlignment(peakResultFile.ReplicateFileId), rawPeakBounds, score,
                                    manuallyIntegrated);
                                peaks.Add(peak);
                            }
                        }

                        yield return new MoleculePeaks(peptideIdentityPath, RatePeaks(parameters, peaks));
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

            private IEnumerable<RatedPeak> RatePeaks(Parameters parameters, IEnumerable<RatedPeak> peaks)
            {
                var list = peaks.OrderByDescending(peak=>peak.Score).ToList();
                if (list.Count == 0)
                {
                    return list;
                }

                list[0] = list[0].ChangeBest(true).ChangeAccepted(true);
                var bestPeak = list[0];
                for (int i = 1; i < list.Count; i++)
                {
                    var peak = list[i];
                    peak = peak.ChangeRtShift(peak.AlignedPeakBounds.MidTime - bestPeak.AlignedPeakBounds.MidTime);
                    bool accepted = Math.Abs(peak.RtShift) > parameters.AllowableRtShift;
                    if (parameters.CutoffScoreType == CutoffScoreType.RAW && peak.Score < parameters.CutoffScore)
                    {
                        accepted = false;
                    }

                    if (parameters.CutoffScoreType == CutoffScoreType.PERCENTILE &&
                        peak.Percentile < parameters.CutoffScore)
                    {
                        accepted = false;
                    }

                    if (parameters.CutoffScoreType == CutoffScoreType.PVALUE && peak.PValue > parameters.CutoffScore)
                    {
                        accepted = false;
                    }

                    if (parameters.CutoffScoreType == CutoffScoreType.QVALUE && peak.QValue > parameters.CutoffScore)
                    {
                        accepted = false;
                    }
                    peak = peak.ChangeAccepted(accepted);
                    list[i] = peak;
                }

                return list;
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


    }
}
