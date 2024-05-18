using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
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

            
        }
        public static bool IsManualIntegrated(PeptideDocNode peptideDocNode, int replicateIndex,
            ChromFileInfoId fileId)
        {
            foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
            {
                foreach (var transitionGroupChromInfo in transitionGroupDocNode.GetSafeChromInfo(replicateIndex))
                {
                    if (ReferenceEquals(fileId, transitionGroupChromInfo.FileId) &&
                        transitionGroupChromInfo.UserSet == UserSet.TRUE)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        private class DataProducer : Producer<Parameters, PeakImputationData>
        {
            public override PeakImputationData ProduceResult(ProductionMonitor productionMonitor, Parameters parameter, IDictionary<WorkOrder, object> inputs)
            {
                ScoringResults scoringResults = ScoringResults.PRODUCER.GetResult(inputs, parameter.GetScoringResultsParameters());
                var consensusAlignment =
                    ConsensusAlignment.PRODUCER.GetResult(inputs, parameter.GetAlignmentParameters());
                var rows = ImmutableList.ValueOf(GetRows(productionMonitor.CancellationToken, parameter, scoringResults, consensusAlignment));
                return new PeakImputationData(parameter, consensusAlignment, scoringResults, rows);
            }
           

            public override IEnumerable<WorkOrder> GetInputs(Parameters parameter)
            {
                SrmDocument document = parameter.Document;
                if (document.MeasuredResults == null)
                {
                    yield break;
                }

                if (parameter.GetScoringResultsParameters() != null)
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

                                var peakFeatureStatistics = scoringResults?.ResultsHandler?.GetPeakFeatureStatistics(molecule.Peptide,
                                    peptideChromInfo.FileId);
                                var peak = new RatedPeak(peakResultFile, alignments?.GetAlignment(peakResultFile.ReplicateFileId), rawPeakBounds, peakFeatureStatistics?.BestScore,
                                    manuallyIntegrated);
                                peaks.Add(peak);
                            }
                        }

                        yield return new MoleculePeaks(peptideIdentityPath, RatePeaks(parameters, peaks));
                    }
                }
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
