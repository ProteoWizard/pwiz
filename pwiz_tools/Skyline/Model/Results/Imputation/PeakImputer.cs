using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class PeakImputer
    {
        public double? CutoffScore { get; set; }
        public double? MaxRtShift { get; set; }
        public bool OverwriteManualPeaks { get; set; }

        public PeakScoringModelSpec ScoringModel { get; set; }

        public SrmDocument ImputeBoundaries(SrmDocument document, IdentityPath peptideIdentityPath, RatedPeak bestPeak,
            IEnumerable<RatedPeak> rejectedPeaks)
        {
            if (bestPeak?.AlignedPeakBounds == null)
            {
                return document;
            }

            foreach (var outlierPeak in rejectedPeaks)
            {
                var bestPeakBounds = bestPeak.AlignedPeakBounds.ReverseAlign(outlierPeak.AlignmentFunction);
                if (bestPeakBounds == null)
                {
                    continue;
                }

                var replicateFileInfo = outlierPeak.ReplicateFileInfo;
                var peptideDocNode = (PeptideDocNode)document.FindNode(peptideIdentityPath);
                var newPeakBounds = GetNewPeakBounds(document, peptideDocNode, replicateFileInfo,
                    bestPeakBounds);
                foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
                {
                    var identityPath =
                        new IdentityPath(peptideIdentityPath, transitionGroupDocNode.TransitionGroup);
                    var chromatogramSet =
                        document.MeasuredResults.Chromatograms[replicateFileInfo.ReplicateIndex];
                    document = document.ChangePeak(identityPath, chromatogramSet.Name,
                        replicateFileInfo.MsDataFileUri, null, newPeakBounds?.StartTime ?? 0, newPeakBounds?.EndTime ?? 0,
                        UserSet.MATCHED, null,
                        false);
                }
            }

            return document;
        }

        public RatedPeak.PeakBounds GetNewPeakBounds(SrmDocument document, PeptideDocNode peptideDocNode,
            ReplicateFileInfo replicateFileInfo, RatedPeak.PeakBounds bestPeakBounds)
        {
            var chromatogramGroupInfos = LoadChromatogramGroupInfos(document, peptideDocNode, replicateFileInfo).ToList();

            var candidateBoundaries = GetCandidatePeakBounds(chromatogramGroupInfos).OrderBy(peakBounds=>Math.Abs(peakBounds.MidTime - bestPeakBounds.MidTime));
            foreach (var candidateBoundary in candidateBoundaries)
            {
                if (MaxRtShift.HasValue)
                {
                    var rtShift = Math.Abs(candidateBoundary.MidTime - bestPeakBounds.MidTime);
                    if (rtShift > MaxRtShift)
                    {
                        break;
                    }
                }

                if (!IsValidPeakBounds(chromatogramGroupInfos, candidateBoundary))
                {
                    continue;
                }
                return candidateBoundary;
            }

            if (IsValidPeakBounds(chromatogramGroupInfos, bestPeakBounds))
            {
                return bestPeakBounds;
            }
            return null;
        }

        private IEnumerable<RatedPeak.PeakBounds> GetCandidatePeakBounds(IEnumerable<ChromatogramGroupInfo> chromatogramGroupInfos)
        {
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
            PeptideDocNode peptideDocNode, ReplicateFileInfo replicateFileInfo)
        {
            if (!document.MeasuredResults.TryLoadChromatogram(replicateFileInfo.ReplicateIndex, peptideDocNode, null,
                    (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance,
                    out var chromatogramGroupInfos))
            {
                return Array.Empty<ChromatogramGroupInfo>();
            }

            return chromatogramGroupInfos.Where(chromatogramGroupInfo =>
                Equals(chromatogramGroupInfo.FilePath, replicateFileInfo.MsDataFileUri));
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
    }
}
