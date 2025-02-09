/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class PeakImputer
    {
        private OnDemandFeatureCalculator _onDemandFeatureCalculator;
        public PeakImputer(SrmDocument document, ChromatogramTimeRanges chromatogramTimeRanges, IdentityPath peptideIdentityPath, PeakScoringModelSpec scoringModel, ReplicateFileInfo replicateFileInfo)
        {
            SrmSettings  = document.Settings;
            ChromatogramTimeRanges = chromatogramTimeRanges;
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
        public ChromatogramTimeRanges ChromatogramTimeRanges { get; }
        private ReplicateFileInfo ReplicateFileInfo { get; }
        public IdentityPath PeptideIdentityPath { get; }
        public PeptideDocNode PeptideDocNode { get; }
        public double? CutoffScore { get; set; }
        public double? MaxRtShift { get; set; }

        public PeakScoringModelSpec ScoringModel { get; }

        public SrmDocument ImputeBoundaries(SrmDocument document, FormattablePeakBounds bestPeakBounds)
        {
            var peptideDocNode = (PeptideDocNode)document.FindNode(PeptideIdentityPath);
            var newPeakBounds = GetNewPeakBounds(document, peptideDocNode, bestPeakBounds);
            foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
            {
                var identityPath =
                    new IdentityPath(PeptideIdentityPath, transitionGroupDocNode.TransitionGroup);
                var chromatogramSet =
                    document.MeasuredResults.Chromatograms[ReplicateFileInfo.ReplicateIndex];
                try
                {
                    document = document.ChangePeak(identityPath, chromatogramSet.Name,
                        ReplicateFileInfo.MsDataFileUri, null, newPeakBounds?.StartTime ?? 0,
                        newPeakBounds?.EndTime ?? 0,
                        UserSet.IMPUTED, PeakIdentification.FALSE,
                        false);
                }
                catch (ArgumentException)
                {
                    // No results: ignore
                }
            }

            return document;
        }

        public FormattablePeakBounds GetNewPeakBounds(SrmDocument document, PeptideDocNode peptideDocNode,
            FormattablePeakBounds bestPeakBounds)
        {
            var chromatogramGroupInfos = LoadChromatogramGroupInfos(document, peptideDocNode).ToList();
            var timeRanges = ChromatogramTimeRanges?.GetTimeRanges(peptideDocNode);
            var timeIntervals = timeRanges?.GetTimeIntervals(ReplicateFileInfo.MsDataFileUri);
            if (!MaxRtShift.HasValue)
            {
                return RatedPeak.MakeValidPeakBounds(timeIntervals, bestPeakBounds);
            }

            var candidateBoundaries = GetCandidatePeakBounds(chromatogramGroupInfos).Where(peak=>peak.MidTime >= bestPeakBounds.StartTime && peak.MidTime <= bestPeakBounds.EndTime).ToList();
            if (candidateBoundaries.Any())
            {
                var peakBounds = new FormattablePeakBounds(candidateBoundaries.Min(peak => peak.StartTime),
                    candidateBoundaries.Max(peak => peak.EndTime));
                if (peakBounds.Width < bestPeakBounds.Width)
                {
                    var halfDifference = (bestPeakBounds.Width - peakBounds.Width) / 2;
                    peakBounds = new FormattablePeakBounds(peakBounds.StartTime - halfDifference,
                        peakBounds.EndTime + halfDifference);
                }

                if (Math.Abs(peakBounds.MidTime - bestPeakBounds.MidTime) <= MaxRtShift.Value)
                {
                    var validPeakBounds = RatedPeak.MakeValidPeakBounds(timeIntervals, peakBounds);
                    if (validPeakBounds != null)
                    {
                        return validPeakBounds;
                    }
                }
            }

            return RatedPeak.MakeValidPeakBounds(timeIntervals, bestPeakBounds);
        }

        private IEnumerable<FormattablePeakBounds> GetCandidatePeakBounds(IList<ChromatogramGroupInfo> chromatogramGroupInfos)
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
                            yield return new FormattablePeakBounds(peak.StartTime, peak.EndTime);
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
