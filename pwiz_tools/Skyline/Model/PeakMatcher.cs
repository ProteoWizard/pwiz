/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public static class PeakMatcher
    {
        private const double ALIGN_AREA_MIN            = 0.10; // Peak must contain at least this percentage of entire chromatogram area to be considered for alignment
        private const double ALIGN_DOT_MIN             = 0.85; // Peaks must have a dot product equal to or greater than this value for alignment
        private const double INTEGRATE_SCORE_THRESHOLD = 0.70; // Integration will be favored over peaks with match scores below this value
        private const double SCORE_DOT_WEIGHT          = 0.50;
        private const double SCORE_RT_WEIGHT           = 0.35;
        private const double SCORE_AREA_WEIGHT         = 0.15;

        private static void GetReferenceData(SrmDocument doc, PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup, int resultsIndex, ChromFileInfoId resultsFile,
            out PeakMatchData referenceTarget, out PeakMatchData[] referenceMatchData, out DateTime? runTime)
        {
            referenceTarget = null;
            referenceMatchData = new PeakMatchData[0];
            runTime = null;

            var referenceMatchDataList = new List<PeakMatchData>();

            var mzMatchTolerance = (float) doc.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                
            if (!nodeTranGroup.HasResults || resultsIndex < 0 || resultsIndex >= nodeTranGroup.Results.Count)
                return;

            var tranGroupChromInfo = nodeTranGroup.GetChromInfo(resultsIndex, resultsFile);
            if (tranGroupChromInfo == null)
                return;

            var chromSet = doc.Settings.MeasuredResults.Chromatograms[resultsIndex];
            if (!doc.Settings.MeasuredResults.TryLoadChromatogram(chromSet, nodePep, nodeTranGroup, mzMatchTolerance, out var chromGroupInfos))
                return;

            var chromGroupInfo = chromGroupInfos.FirstOrDefault(info => Equals(chromSet.GetFileInfo(tranGroupChromInfo.FileId).FilePath, info.FilePath));
            if (chromGroupInfo == null || chromGroupInfo.NumPeaks == 0 || !chromGroupInfo.TimeIntensitiesGroup.HasAnyPoints)
                return;

            runTime = chromGroupInfo.RunStartTime;

            if (!tranGroupChromInfo.RetentionTime.HasValue || !tranGroupChromInfo.StartRetentionTime.HasValue || !tranGroupChromInfo.EndRetentionTime.HasValue)
                return;

            int peakIndex = -1;
            foreach (var transition in chromGroupInfo.TransitionPointSets)
            {
                peakIndex = transition.IndexOfPeak(tranGroupChromInfo.RetentionTime.Value);
                if (peakIndex != -1)
                    break;
            }
                    
            // Get time information
            float timeMin = chromGroupInfo.TimeIntensitiesGroup.MinTime;
            float timeMax = chromGroupInfo.TimeIntensitiesGroup.MaxTime;

            float totalArea = chromGroupInfo.TransitionPointSets.Sum(chromInfo => chromInfo.Peaks.Sum(peak => peak.Area));
            for (int i = 0; i < chromGroupInfo.NumPeaks; i++)
                referenceMatchDataList.Add(new PeakMatchData(nodeTranGroup, chromGroupInfo, mzMatchTolerance, i, totalArea));

            // Get ion abundance information
            var abundances = new IonAbundances();
            foreach (var nodeTran in nodeTranGroup.Transitions)
            {
                var chromInfoCached = chromGroupInfo.GetTransitionInfo(nodeTran, mzMatchTolerance);
                if (chromInfoCached == null)
                    continue;

                var tranChromInfo = nodeTran.Results[resultsIndex].First(r => r.FileIndex == tranGroupChromInfo.FileIndex);

                float area;
                if (peakIndex != -1)
                {
                    // A peak exists here
                    var cachedPeak = chromInfoCached.GetPeak(peakIndex);
                    area = cachedPeak.Area;

                    if (cachedPeak.RetentionTime.Equals(tranGroupChromInfo.RetentionTime.Value))
                    {
                        var range = cachedPeak.EndTime - cachedPeak.StartTime;
                        referenceMatchDataList[peakIndex].ShiftLeft = (tranChromInfo.StartRetentionTime - cachedPeak.StartTime)/range;
                        referenceMatchDataList[peakIndex].ShiftRight = (tranChromInfo.EndRetentionTime - cachedPeak.EndTime)/range;
                    }
                }
                else
                {
                    area = tranChromInfo.Area;
                }
                abundances.Add(nodeTran, area);
            }

            referenceTarget = peakIndex != -1
                ? referenceMatchDataList[peakIndex]
                : new PeakMatchData(abundances, abundances.Sum()/totalArea, tranGroupChromInfo.RetentionTime.Value,
                    tranGroupChromInfo.StartRetentionTime.Value, tranGroupChromInfo.EndRetentionTime.Value, timeMin, timeMax);

            referenceMatchData = referenceMatchDataList.ToArray();
        }

        public static SrmDocument ApplyPeak(IProgressMonitor progressMonitor, IProgressStatus progressStatus, SrmDocument doc, PeptideGroup peptideGroup, PeptideDocNode peptideDocNode, TransitionGroupDocNode nodeTranGroup,
            int resultsIndex, ChromFileInfoId resultsFile, bool subsequent, ReplicateValue groupBy, object groupByValue)
        {
            nodeTranGroup = nodeTranGroup ?? PickTransitionGroup(doc, peptideDocNode, resultsIndex);
            GetReferenceData(doc, peptideDocNode, nodeTranGroup, resultsIndex, resultsFile, out var referenceTarget, out var referenceMatchData, out var runTime);

            var annotationCalculator = new AnnotationCalculator(doc);
            var chromatograms = doc.Settings.MeasuredResults.Chromatograms;
            for (var i = 0; i < chromatograms.Count; i++)
            {
                if (progressMonitor.IsCanceled)
                {
                    return null;
                }
                progressMonitor.UpdateProgress(progressStatus =
                    progressStatus.ChangePercentComplete(i * 100 / chromatograms.Count));
                var chromSet = chromatograms[i];

                if (groupBy != null)
                {
                    if (!Equals(groupByValue, groupBy.GetValue(annotationCalculator, chromSet)))
                    {
                        continue;
                    }
                }

                for (var j = 0; j < chromSet.MSDataFileInfos.Count; j++)
                {
                    var fileInfo = chromSet.MSDataFileInfos[j];
                    if ((i == resultsIndex && (resultsFile == null || ReferenceEquals(resultsFile, fileInfo.FileId))) ||
                        (subsequent && runTime != null && fileInfo.RunStartTime < runTime))
                    {
                        continue;
                    }

                    var bestMatch = GetPeakMatch(doc, chromSet, fileInfo, nodeTranGroup, referenceTarget, referenceMatchData);
                    if (bestMatch != null)
                        doc = bestMatch.ChangePeak(doc, peptideGroup, peptideDocNode, nodeTranGroup, chromSet.Name, fileInfo.FilePath);
                }
            }
            return doc;
        }

        public static TransitionGroupDocNode PickTransitionGroup(SrmDocument doc, PeptideDocNode peptideDocNode, int resultsIndex)
        {
            // Determine which transition group to use
            if (peptideDocNode.Children.Count == 0)
                return null;

            if (peptideDocNode.Children.Count == 1)
                return peptideDocNode.TransitionGroups.First();

            var standards = doc.Settings.PeptideSettings.Modifications.InternalStandardTypes;
            var nodeTranGroups = peptideDocNode.TransitionGroups;
            var standardList = peptideDocNode.TransitionGroups.Where(tranGroup => standards.Contains(tranGroup.TransitionGroup.LabelType)).ToArray();

            if (standardList.Length == 1)
                return standardList.First();
            
            if (standardList.Length > 1)
                nodeTranGroups = standardList;

            // Still not sure, pick the one with the most peak area
            TransitionGroupDocNode best = null;
            float mzMatchTolerance = (float) doc.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            float highestAreaSum = 0;
            foreach (var tranGroup in nodeTranGroups)
            {
                ChromatogramSet chromSet = doc.Settings.MeasuredResults.Chromatograms[resultsIndex];
                ChromatogramGroupInfo[] chromGroupInfos;
                if (!doc.Settings.MeasuredResults.TryLoadChromatogram(chromSet, peptideDocNode, tranGroup, mzMatchTolerance, out chromGroupInfos))
                    continue;

                float areaSum = chromGroupInfos.Where(info => info != null && info.TransitionPointSets != null)
                    .Sum(chromGroupInfo => chromGroupInfo.TransitionPointSets.Sum(chromInfo => chromInfo.Peaks.Sum(peak => peak.Area)));
                if (areaSum > highestAreaSum)
                {
                    best = tranGroup;
                    highestAreaSum = areaSum;
                }
            }
            return best;
        }

        private static PeakMatch GetPeakMatch(SrmDocument doc, ChromatogramSet chromSet, IPathContainer fileInfo, TransitionGroupDocNode nodeTranGroup,
            PeakMatchData referenceTarget, IEnumerable<PeakMatchData> referenceMatchData)
        {
            if (referenceTarget == null)
                return new PeakMatch(0, 0);

            var mzMatchTolerance = (float) doc.Settings.TransitionSettings.Instrument.MzMatchTolerance;

            ChromatogramGroupInfo[] loadInfos;
            if (!nodeTranGroup.HasResults || !doc.Settings.MeasuredResults.TryLoadChromatogram(chromSet, null, nodeTranGroup, mzMatchTolerance, out loadInfos))
                return null;

            var chromGroupInfo = loadInfos.FirstOrDefault(info => Equals(info.FilePath, fileInfo.FilePath));
            if (chromGroupInfo == null || chromGroupInfo.NumPeaks == 0 || !chromGroupInfo.TimeIntensitiesGroup.HasAnyPoints)
                return null;

            var matchData = new List<PeakMatchData>();
            double totalArea = chromGroupInfo.TransitionPointSets.Sum(chromInfo => chromInfo.Peaks.Sum(peak => peak.Area));
            for (int i = 0; i < chromGroupInfo.NumPeaks; i++)
                matchData.Add(new PeakMatchData(nodeTranGroup, chromGroupInfo, mzMatchTolerance, i, totalArea));

            // TODO: Try to improve this. Align peaks in area descending order until peaks do not match
            var alignments = new List<PeakAlignment>();
            bool referenceAligned = false;
            using (var referenceIter = referenceMatchData.OrderByDescending(d => d.PercentArea).GetEnumerator())
            using (var curIter = matchData.OrderByDescending(d => d.PercentArea).GetEnumerator())
            {
                while (referenceIter.MoveNext() && curIter.MoveNext())
                {
                    var alignAttempt = new PeakAlignment(referenceIter.Current, curIter.Current);
                    if (!TryInsertPeakAlignment(alignments, alignAttempt))
                        break;

                    if (referenceTarget == alignAttempt.ReferencePeak)
                    {
                        // Reference target aligned
                        referenceAligned = true;
                        matchData = new List<PeakMatchData> { alignAttempt.AlignedPeak };
                        break;
                    }
                }
            }

            PeakMatch manualMatch = null;

            if (!referenceAligned)
            {
                PeakAlignment prev, next;
                GetSurroundingAlignments(alignments, referenceTarget, out prev, out next);
                if (prev != null || next != null)
                {
                    // At least one alignment occurred
                    var chromGroupMinTime = chromGroupInfo.TimeIntensitiesGroup.MinTime;
                    var chromGroupMaxTime = chromGroupInfo.TimeIntensitiesGroup.MaxTime;

                    float scale = (chromGroupMaxTime - chromGroupMinTime)/(referenceTarget.MaxTime - referenceTarget.MinTime);
                    manualMatch = MakePeakMatchBetween(scale, referenceTarget, prev, next);
                    if (chromGroupMinTime >= manualMatch.EndTime || manualMatch.StartTime >= chromGroupMaxTime ||
                        manualMatch.EndTime <= manualMatch.StartTime)
                        manualMatch = null;

                    float curMinTime = prev == null ? chromGroupMinTime : prev.AlignedPeak.RetentionTime;
                    float curMaxTime = next == null ? chromGroupMaxTime : next.AlignedPeak.RetentionTime;

                    matchData = matchData.Where(d => curMinTime < d.RetentionTime && d.RetentionTime < curMaxTime).ToList();
                }
            }

            PeakMatchData bestMatch = null;
            double bestScore = double.MinValue;
            foreach (var other in matchData)
            {
                var score = referenceTarget.GetMatchScore(other);
                if (bestMatch == null || score > bestScore)
                {
                    bestScore = score;
                    bestMatch = other;
                }
            }

            // If no matching peak with high enough score was found, but there is a matching
            // peak or some peak with a low score.
            if (bestMatch == null || (!referenceAligned && bestScore < INTEGRATE_SCORE_THRESHOLD && manualMatch != null))
                return manualMatch;

            if (referenceTarget.ShiftLeft == 0 && referenceTarget.ShiftRight == 0)
                return new PeakMatch(bestMatch.RetentionTime);

            var range = bestMatch.EndTime - bestMatch.StartTime;
            var startTime = bestMatch.StartTime + (referenceTarget.ShiftLeft*range);
            var endTime = bestMatch.EndTime + (referenceTarget.ShiftRight*range);
            return startTime <= bestMatch.RetentionTime && bestMatch.RetentionTime <= endTime
                ? new PeakMatch(startTime, endTime)
                : new PeakMatch(bestMatch.RetentionTime); // If the shifted boundaries exclude the peak itself, don't change the boundaries
        }

        private static ChromPeak? GetLargestPeak(TransitionGroupDocNode nodeTranGroup,
            ChromatogramGroupInfo chromGroupInfo, int peakIndex, float mzMatchTolerance)
        {
            ChromPeak? largestPeak = null;
            foreach (var peak in
                     from transitionDocNode in nodeTranGroup.Transitions
                     select chromGroupInfo.GetTransitionInfo(transitionDocNode, mzMatchTolerance)
                     into chromInfo where chromInfo != null
                     select chromInfo.GetPeak(peakIndex))
            {
                if (largestPeak == null || peak.Height > largestPeak.Value.Height)
                    largestPeak = peak;
            }
            return largestPeak;
        }

        private static PeakMatch MakePeakMatchBetween(float scale, PeakMatchData referenceTarget, PeakAlignment prev, PeakAlignment next)
        {
            if (prev == null && next == null)
                return null;

            float startTime, endTime;
            var range = scale * (referenceTarget.EndTime - referenceTarget.StartTime);
            if (prev != null && next != null)
            {
                startTime = prev.AlignedPeak.RetentionTime + scale*(referenceTarget.StartTime - prev.ReferencePeak.RetentionTime);
                endTime = next.AlignedPeak.RetentionTime - scale*(next.ReferencePeak.RetentionTime - referenceTarget.EndTime);
            }
            else if (next != null)
            {
                endTime = next.AlignedPeak.StartTime - scale*(next.ReferencePeak.StartTime - referenceTarget.EndTime);
                startTime = endTime - range;
            }
            else
            {
                startTime = prev.AlignedPeak.EndTime + scale*(referenceTarget.StartTime - prev.ReferencePeak.EndTime);
                endTime = startTime + range;
            }
            return new PeakMatch(startTime + (referenceTarget.ShiftLeft*range), endTime + (referenceTarget.ShiftRight*range));
        }

        private static bool TryInsertPeakAlignment(IList<PeakAlignment> alignList, PeakAlignment alignment)
        {
            var reference = alignment.ReferencePeak;
            var other = alignment.AlignedPeak;

            if (Math.Min(reference.PercentArea, other.PercentArea) < ALIGN_AREA_MIN || reference.Abundances.Dot(other.Abundances) < ALIGN_DOT_MIN)
                return false;

            PeakAlignment prev, next;
            var insertPos = GetSurroundingAlignments(alignList, reference, out prev, out next);
            if ((prev != null && other.RetentionTime < prev.AlignedPeak.RetentionTime) ||
                (next != null && other.RetentionTime > next.AlignedPeak.RetentionTime))
            {
                return false;
            }

            alignList.Insert(insertPos, alignment);
            return true;
        }

        private static int GetSurroundingAlignments(IList<PeakAlignment> alignList, PeakMatchData target, out PeakAlignment prev, out PeakAlignment next)
        {
            prev = next = null;
            if (!alignList.Any())
                return 0;

            var insertPos = alignList.Select(align => align.ReferencePeak).ToList().BinarySearch(target, RT_COMPARER);
            if (insertPos < 0)
                insertPos = ~insertPos;

            if (insertPos == 0)
            {
                next = alignList.First();
            }
            else if (insertPos != alignList.Count)
            {
                prev = alignList[insertPos - 1];
                next = alignList[insertPos];
            }
            else
            {
                prev = alignList.Last();
            }
            return insertPos;
        }

        private class PeakAlignment
        {
            public PeakMatchData ReferencePeak { get; private set; }
            public PeakMatchData AlignedPeak { get; private set; }

            public PeakAlignment(PeakMatchData referencePeak, PeakMatchData alignedPeak)
            {
                ReferencePeak = referencePeak;
                AlignedPeak = alignedPeak;
            }
        }

        private class PeakMatch
        {
            private readonly float? _retentionTime;
            public float? StartTime { get; private set; }
            public float? EndTime { get; private set; }

            public PeakMatch(float rt)
            {
                _retentionTime = rt;
            }

            public PeakMatch(float startTime, float endTime)
            {
                StartTime = startTime;
                EndTime = endTime;
            }

            public SrmDocument ChangePeak(SrmDocument doc, PeptideGroup peptideGroup, PeptideDocNode peptideDocNode, TransitionGroupDocNode nodeTranGroup, string nameSet, MsDataFileUri filePath)
            {
                if ((_retentionTime ?? StartTime) == null)
                    return doc;

                var groupPath = new IdentityPath(peptideGroup, peptideDocNode.Peptide, nodeTranGroup.Id);

                doc = _retentionTime.HasValue
                    ? doc.ChangePeak(groupPath, nameSet, filePath, null, _retentionTime.Value, UserSet.TRUE)
                    : doc.ChangePeak(groupPath, nameSet, filePath, null, StartTime, EndTime, UserSet.TRUE, null, false);

                var activeTransitionGroup = (TransitionGroupDocNode) doc.FindNode(groupPath);
                if (activeTransitionGroup.RelativeRT != RelativeRT.Matching)
                    return doc;

                var activeChromInfo = SkylineWindow.FindChromInfo(doc, activeTransitionGroup, nameSet, filePath);
                var peptide = (PeptideDocNode) doc.FindNode(groupPath.Parent);
                // See if there are any other transition groups that should have their peak bounds set to the same value
                foreach (var tranGroup in peptide.TransitionGroups.Where(tranGroup => tranGroup.RelativeRT == RelativeRT.Matching))
                {
                    var otherGroupPath = new IdentityPath(groupPath.Parent, tranGroup.TransitionGroup);
                    if (Equals(groupPath, otherGroupPath) || SkylineWindow.FindChromInfo(doc, tranGroup, nameSet, filePath) == null)
                        continue;

                    doc = doc.ChangePeak(otherGroupPath, nameSet, filePath, null,
                        activeChromInfo.StartRetentionTime, activeChromInfo.EndRetentionTime, UserSet.TRUE, activeChromInfo.Identified, false);
                }
                return doc;
            }
        }

        private class PeakMatchData
        {
            public IonAbundances Abundances { get; private set; }
            public double PercentArea { get; private set; }
            public float RetentionTime { get; private set; }
            public float StartTime { get; private set; }
            public float EndTime { get; private set; }
            public float ShiftLeft { get; set; }
            public float ShiftRight { get; set; }
            public float MinTime { get; private set; }
            public float MaxTime { get; private set; }

            public PeakMatchData(IonAbundances abundances, double percentArea,
                float rt, float rtStart, float rtEnd, float rtMin, float rtMax)
            {
                Abundances = abundances;
                PercentArea = percentArea;
                RetentionTime = rt;
                StartTime = rtStart;
                EndTime = rtEnd;
                ShiftLeft = 0;
                ShiftRight = 0;
                MinTime = rtMin;
                MaxTime = rtMax;
            }

            public PeakMatchData(TransitionGroupDocNode nodeTranGroup, ChromatogramGroupInfo chromGroupInfo,
                float mzMatchTolerance, int peakIndex, double totalChromArea)
            {
                Abundances = new IonAbundances(nodeTranGroup, chromGroupInfo, mzMatchTolerance, peakIndex);
                PercentArea = Abundances.Sum()/totalChromArea;
                var peak = GetLargestPeak(nodeTranGroup, chromGroupInfo, peakIndex, mzMatchTolerance);
                Assume.IsNotNull(peak);
                RetentionTime = peak.Value.RetentionTime;
                StartTime = peak.Value.StartTime;
                EndTime = peak.Value.EndTime;
                ShiftLeft = 0;
                ShiftRight = 0;
                MinTime = chromGroupInfo.TimeIntensitiesGroup.MinTime;
                MaxTime = chromGroupInfo.TimeIntensitiesGroup.MaxTime;
            }

            public double GetMatchScore(PeakMatchData other)
            {
                var timeMin = Math.Min(MinTime, other.MinTime);
                var timeMax = Math.Max(MaxTime, other.MaxTime);
                return SCORE_DOT_WEIGHT * Abundances.Dot(other.Abundances) +
                       SCORE_RT_WEIGHT * (1 - Math.Abs((RetentionTime - timeMin) - (other.RetentionTime - timeMin)) / (timeMax - timeMin)) +
                       SCORE_AREA_WEIGHT * (1 - Math.Abs(PercentArea - other.PercentArea));
            }
        }

        private static readonly Comparer<PeakMatchData> RT_COMPARER =
            Comparer<PeakMatchData>.Create((x, y) => x.RetentionTime.CompareTo(y.RetentionTime));

        private class IonAbundances
        {
            private readonly Dictionary<string, float> _abundances;

            public IonAbundances()
            {
                _abundances = new Dictionary<string, float>();
            }

            public IonAbundances(TransitionGroupDocNode nodeTranGroup, ChromatogramGroupInfo chromGroupInfo,
                float mzMatchTolerance, int peakIndex) : this()
            {
                foreach (var nodeTran in nodeTranGroup.Transitions)
                {
                    var chromInfoCached = chromGroupInfo.GetTransitionInfo(nodeTran, mzMatchTolerance);
                    if (chromInfoCached == null)
                        continue;

                    Add(nodeTran, chromInfoCached.GetPeak(peakIndex).Area);
                }
            }

            public void Add(TransitionDocNode nodeTran, float abundance)
            {
                string ion = nodeTran.FragmentIonName;
                float current = !_abundances.ContainsKey(ion) ? 0 : _abundances[ion];
                _abundances[ion] = current + abundance;
            }

            public double Sum()
            {
                return _abundances.Sum(a => a.Value);
            }

            public double Dot(IonAbundances other)
            {
                var abundancesThis = new List<double>();
                var abundancesOther = new List<double>();
                foreach (var fragment in _abundances.Keys.Union(other._abundances.Keys))
                {
                    abundancesThis.Add(_abundances.TryGetValue(fragment, out var abundance) ? abundance : 0);
                    abundancesOther.Add(other._abundances.TryGetValue(fragment, out var otherAbundance) ? otherAbundance : 0);
                }
                var statisticsThis = new Statistics(abundancesThis);
                var statisticsOther = new Statistics(abundancesOther);
                return statisticsThis.Angle(statisticsOther);
            }
        }
    }
}
