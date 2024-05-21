using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class PeakImputer
    {
        private OnDemandFeatureCalculator _onDemandFeatureCalculator;
        public PeakImputer(SrmDocument document, IdentityPath peptideIdentityPath, PeakScoringModelSpec scoringModel, ReplicateFileInfo replicateFileInfo)
        {
            SrmSettings  = document.Settings;
            ReplicateFileInfo = replicateFileInfo;
            PeptideIdentityPath = peptideIdentityPath;
            ScoringModel = scoringModel;
            var chromFileInfo = document.MeasuredResults.Chromatograms[replicateFileInfo.ReplicateIndex]
                .GetFileInfo(replicateFileInfo.ReplicateFileId.FileId);
            PeptideDocNode = (PeptideDocNode)document.FindNode(peptideIdentityPath);
            _onDemandFeatureCalculator = new OnDemandFeatureCalculator(scoringModel.PeakFeatureCalculators,
                document.Settings, PeptideDocNode, replicateFileInfo.ReplicateIndex, chromFileInfo);
        }

        public SrmSettings SrmSettings { get; }
        private ReplicateFileInfo ReplicateFileInfo { get; }
        public IdentityPath PeptideIdentityPath { get; }
        public PeptideDocNode PeptideDocNode { get; }
        public double? CutoffScore { get; set; }
        public double? MaxRtShift { get; set; }

        public PeakScoringModelSpec ScoringModel { get; }

        public SrmDocument ImputeBoundaries(SrmDocument document, RatedPeak.PeakBounds bestPeakBounds)
        {
            var peptideDocNode = (PeptideDocNode)document.FindNode(PeptideIdentityPath);
            var newPeakBounds = GetNewPeakBounds(document, peptideDocNode, bestPeakBounds);
            foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
            {
                var identityPath =
                    new IdentityPath(PeptideIdentityPath, transitionGroupDocNode.TransitionGroup);
                var chromatogramSet =
                    document.MeasuredResults.Chromatograms[ReplicateFileInfo.ReplicateIndex];
                document = document.ChangePeak(identityPath, chromatogramSet.Name,
                    ReplicateFileInfo.MsDataFileUri, null, newPeakBounds?.StartTime ?? 0, newPeakBounds?.EndTime ?? 0,
                    UserSet.IMPUTED, null,
                    false);
            }

            return document;
        }

        public RatedPeak.PeakBounds GetNewPeakBounds(SrmDocument document, PeptideDocNode peptideDocNode,
            RatedPeak.PeakBounds bestPeakBounds)
        {
            var chromatogramGroupInfos = LoadChromatogramGroupInfos(document, peptideDocNode).ToList();
            if (!MaxRtShift.HasValue)
            {
                if (IsValidPeakBounds(chromatogramGroupInfos, bestPeakBounds))
                {
                    return bestPeakBounds;
                }

                return null;
            }

            var candidateBoundaries = GetCandidatePeakBounds(chromatogramGroupInfos).Where(peak=>peak.MidTime >= bestPeakBounds.StartTime && peak.MidTime <= bestPeakBounds.EndTime).ToList();
            if (candidateBoundaries.Any())
            {
                var peakBounds = new RatedPeak.PeakBounds(candidateBoundaries.Min(peak => peak.StartTime),
                    candidateBoundaries.Max(peak => peak.EndTime));
                if (peakBounds.Width < bestPeakBounds.Width)
                {
                    var halfDifference = (bestPeakBounds.Width - peakBounds.Width) / 2;
                    peakBounds = new RatedPeak.PeakBounds(peakBounds.StartTime - halfDifference,
                        peakBounds.EndTime + halfDifference);
                }

                if (Math.Abs(peakBounds.MidTime - bestPeakBounds.MidTime) <= MaxRtShift.Value && IsValidPeakBounds(chromatogramGroupInfos, peakBounds))
                {
                    return peakBounds;
                }
            }

            if (IsValidPeakBounds(chromatogramGroupInfos, bestPeakBounds))
            {
                return bestPeakBounds;
            }
            return null;
        }

        private IEnumerable<RatedPeak.PeakBounds> GetCandidatePeakBounds(IList<ChromatogramGroupInfo> chromatogramGroupInfos)
        {
            if (NeedToPickPeaksAgain(chromatogramGroupInfos))
            {
                PeptideChromDataSets peptideChromDataSets = _onDemandFeatureCalculator.MakePeptideChromDataSets();
                peptideChromDataSets.PickChromatogramPeaks(null);
                chromatogramGroupInfos = peptideChromDataSets.MakeChromatogramGroupInfos().ToList();
            }
            foreach (var chromatogramGroupInfo in chromatogramGroupInfos)
            {
                for (int iPeak = 0; iPeak < chromatogramGroupInfo.NumPeaks; iPeak++)
                {
                    for (int iTransition = 0; iTransition < chromatogramGroupInfo.NumTransitions; iTransition++)
                    {
                        var peak = chromatogramGroupInfo.GetTransitionPeak(iTransition, iPeak);
                        if (!peak.IsEmpty)
                        {
                            yield return new RatedPeak.PeakBounds(peak.StartTime, peak.EndTime);
                            break;
                        }
                    }
                }
            }
        }

        private IEnumerable<ChromatogramGroupInfo> LoadChromatogramGroupInfos(SrmDocument document,
            PeptideDocNode peptideDocNode)
        {
            return peptideDocNode.TransitionGroups.Select(tg =>
                _onDemandFeatureCalculator.GetChromatogramGroupInfo(tg.TransitionGroup));
        }

        private bool IsValidPeakBounds(IEnumerable<ChromatogramGroupInfo> chromatogramGroupInfos, RatedPeak.PeakBounds peakBounds)
        {
            foreach (var chromatogramGroupInfo in chromatogramGroupInfos)
            {
                if (chromatogramGroupInfo.Header.StartTime > peakBounds.MidTime)
                {
                    continue;
                }

                if (chromatogramGroupInfo.Header.EndTime < peakBounds.MidTime)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool NeedToPickPeaksAgain(IEnumerable<ChromatogramGroupInfo> chromatogramGroupInfos)
        {
            if (chromatogramGroupInfos.Any(info => info.NumPeaks > 1))
            {
                return false;
            }

            if (null != SrmSettings.GetExplicitPeakBounds(PeptideDocNode, ReplicateFileInfo.MsDataFileUri))
            {
                return true;
            }

            return false;
        }
    }
}
