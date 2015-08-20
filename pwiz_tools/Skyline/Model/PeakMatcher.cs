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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class PeakMatcher
    {
        private const double ALIGN_AREA_MIN            = 0.10; // Peak must contain at least this percentage of entire chromatogram area to be considered for alignment
        private const double ALIGN_DOT_MIN             = 0.85; // Peaks must have a dot product equal to or greater than this value for alignment
        private const double INTEGRATE_SCORE_THRESHOLD = 0.70; // Integration will be favored over peaks with match scores below this value
        private const double SCORE_DOT_WEIGHT          = 0.50;
        private const double SCORE_RT_WEIGHT           = 0.35;
        private const double SCORE_AREA_WEIGHT         = 0.15;

        private readonly TransitionGroupDocNode _nodeTranGroup;
        private readonly IdentityPath _groupPath;
        private readonly TransitionGroupChromInfo _tranGroupChromInfo;
        private readonly DateTime? _runTime;
        
        private readonly List<PeakMatchData> _peakMatchData;
        private readonly PeakMatchData _origin;
        private readonly float _timeMin, _timeMax;
        private readonly float _shiftLeft, _shiftRight;

        public PeakMatcher(SrmDocument document, PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup,
            IdentityPath groupPath, int resultsIndex)
        {
            _nodeTranGroup = nodeTranGroup;
            _groupPath = groupPath;

            if (!nodeTranGroup.HasResults || resultsIndex < 0 || resultsIndex >= nodeTranGroup.Results.Count)
                return;

            var listChromInfo = _nodeTranGroup.Results[resultsIndex];
            // CONSIDER: What about multiple files and optimization steps?
            _tranGroupChromInfo = listChromInfo[0];

            var chromSet = document.Settings.MeasuredResults.Chromatograms[resultsIndex];
            _runTime = chromSet.MSDataFileInfos[chromSet.IndexOfId(_tranGroupChromInfo.FileId)].RunStartTime;

            if (_tranGroupChromInfo == null || !_tranGroupChromInfo.RetentionTime.HasValue ||
                !_tranGroupChromInfo.StartRetentionTime.HasValue || !_tranGroupChromInfo.EndRetentionTime.HasValue)
                return;

            var mzMatchTolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;

            ChromatogramGroupInfo[] chromSetInfos;
            if (!document.Settings.MeasuredResults.TryLoadChromatogram(chromSet, nodePep, nodeTranGroup, mzMatchTolerance, true, out chromSetInfos))
                return;

            var chromGroupInfo = GetChromGroupInfo(chromSetInfos, chromSet.GetFileInfo(_tranGroupChromInfo.FileId).FilePath);
            if (chromGroupInfo == null || chromGroupInfo.Times.Count() == 0)
                return;

            int peakIndex = -1;
            foreach (var transition in chromGroupInfo.TransitionPointSets)
            {
                peakIndex = transition.IndexOfPeak(_tranGroupChromInfo.RetentionTime.Value);
                if (peakIndex != -1)
                    break;
            }

            // Get time information
            _timeMin = chromGroupInfo.Times.First();
            _timeMax = chromGroupInfo.Times.Last();
            
            // Get ion abundance information
            var abundances = new IonAbundances();
            foreach (var nodeTran in _nodeTranGroup.Transitions)
            {
                var chromInfoCached = chromGroupInfo.GetTransitionInfo((float)nodeTran.Mz, mzMatchTolerance);
                if (chromInfoCached == null)
                    continue;

                var tranChromInfo = nodeTran.Results[resultsIndex].First(r => r.FileIndex == _tranGroupChromInfo.FileIndex);

                float area;
                if (peakIndex != -1)
                {
                    // A peak exists here
                    var cachedPeak = chromInfoCached.GetPeak(peakIndex);
                    area = cachedPeak.Area;

                    if (cachedPeak.RetentionTime.Equals(_tranGroupChromInfo.RetentionTime.Value))
                    {
                        var range = cachedPeak.EndTime - cachedPeak.StartTime;
                        _shiftLeft = (tranChromInfo.StartRetentionTime - cachedPeak.StartTime)/range;
                        _shiftRight = (tranChromInfo.EndRetentionTime - cachedPeak.EndTime)/range;
                    }
                }
                else
                {
                    area = tranChromInfo.Area;
                }
                abundances.Add(nodeTran, area);
            }

            // Calculate area of this peak versus area of all found peaks
            var totalArea = chromGroupInfo.TransitionPointSets.Sum(chromInfo => chromInfo.Peaks.Sum((peak => peak.Area)));
            var percentArea = abundances.Sum() / totalArea;

            _peakMatchData = new List<PeakMatchData>();
            for (int i = 0; i < chromGroupInfo.NumPeaks; i++)
                _peakMatchData.Add(new PeakMatchData(_nodeTranGroup, chromGroupInfo, mzMatchTolerance, i, totalArea));

            _origin = peakIndex != -1
                ? _peakMatchData[peakIndex]
                : new PeakMatchData(abundances, percentArea, _tranGroupChromInfo.RetentionTime.Value,
                    _tranGroupChromInfo.StartRetentionTime.Value, _tranGroupChromInfo.EndRetentionTime.Value);

            // Sort peak match data by retention times
            _peakMatchData.Sort(RT_COMPARER);
        }

        public SrmDocument ApplyPeak(SrmDocument document, bool subsequent)
        {
            if (_origin == null)
                return document;

            var mzMatchTolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;

            foreach (var chromSet in document.Settings.MeasuredResults.Chromatograms)
            {
                ChromatogramGroupInfo[] chromGroupInfos;
                if (!document.Settings.MeasuredResults.TryLoadChromatogram(chromSet, null, _nodeTranGroup, mzMatchTolerance, true, out chromGroupInfos))
                    continue;

                foreach (var fileInfo in chromSet.MSDataFileInfos)
                {
                    var runTime = fileInfo.RunStartTime;
                    if (ReferenceEquals(_tranGroupChromInfo.FileId, fileInfo.FileId) ||
                        (subsequent && _runTime.HasValue && runTime.HasValue && _runTime.Value >= runTime.Value))
                    {
                        continue;
                    }

                    var chromGroupInfo = GetChromGroupInfo(chromGroupInfos, fileInfo.FilePath);
                    if (chromGroupInfo == null)
                        continue;

                    var bestMatch = GetPeakMatch(document, chromGroupInfo);
                    if (bestMatch != null)
                    {
                        document = bestMatch.ChangePeak(document, _groupPath, chromSet.Name, fileInfo.FilePath);
                    }
                }
            }
            return document;
        }

        private PeakMatch GetPeakMatch(SrmDocument document, ChromatogramGroupInfo chromGroupInfo)
        {
            if (_origin == null || chromGroupInfo.NumPeaks == 0 || chromGroupInfo.Times.Count() == 0)
                return null;

            float mzMatchTolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double totalArea = chromGroupInfo.TransitionPointSets.Sum(chromInfo => chromInfo.Peaks.Sum(peak => peak.Area));

            var matchData = new List<PeakMatchData>();
            for (int i = 0; i < chromGroupInfo.NumPeaks; i++)
                matchData.Add(new PeakMatchData(_nodeTranGroup, chromGroupInfo, mzMatchTolerance, i, totalArea));

            PeakMatchData bestMatch = matchData.First();
            // Align all peaks in apply to data chromatograms with current chromatograms
            var alignments = GetPeakAlignments(matchData);
            var originAlign = alignments.FirstOrDefault(align => align.OriginPeak == _origin);
            if (originAlign != null)
            {
                // Origin peak aligned, just use the peak it aligned with
                bestMatch = originAlign.AlignedPeak;
            }
            else
            {
                PeakAlignment prev, next;
                GetSurroundingAlignments(alignments, _origin, out prev, out next);
                float curMinTime = chromGroupInfo.Times.First();
                float curMaxTime = chromGroupInfo.Times.Last();
                var minTime = Math.Min(curMinTime, _timeMin);
                var maxTime = Math.Max(curMaxTime, _timeMax);
                if (prev != null || next != null)
                {
                    minTime = (prev != null) ? prev.AlignedPeak.RetentionTime : minTime;
                    maxTime = (next != null) ? next.AlignedPeak.RetentionTime : maxTime;
                    matchData = matchData.Where(data => minTime < data.RetentionTime && data.RetentionTime < maxTime).ToList();
                }

                double bestScore = double.MinValue;
                foreach (var other in matchData)
                {
                    var score = _origin.GetMatchScore(other, minTime, maxTime);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatch = other;
                    }
                }

                // If no matching peak with high enough score was found, but there is a matching
                // peak or some peak with a low score.
                if (bestScore < INTEGRATE_SCORE_THRESHOLD && (prev != null || next != null))
                {
                    float scale = (curMaxTime - curMinTime) / (_timeMax - _timeMin);
                    var manualMatch = MakePeakMatchBetween(scale, prev, next);
                    if (curMinTime < manualMatch.EndTime && manualMatch.StartTime < curMaxTime)
                        return manualMatch;
                }
            }

            if (_shiftLeft == 0 && _shiftRight == 0)
                return new PeakMatch(bestMatch.RetentionTime);

            var range = bestMatch.EndTime - bestMatch.StartTime;
            var startTime = bestMatch.StartTime + (_shiftLeft*range);
            var endTime = bestMatch.EndTime + (_shiftRight*range);
            return startTime <= bestMatch.RetentionTime && bestMatch.RetentionTime <= endTime
                ? new PeakMatch(startTime, endTime)
                : new PeakMatch(bestMatch.RetentionTime); // If the shifted boundaries exclude the peak itself, don't change the boundaries
        }

        private PeakMatch MakePeakMatchBetween(float scale, PeakAlignment prev, PeakAlignment next)
        {
            if (prev == null && next == null)
                return null;

            float startTime, endTime;
            var range = scale*(_origin.EndTime - _origin.StartTime);
            if (prev != null && next != null)
            {
                startTime = prev.AlignedPeak.RetentionTime + scale*(_origin.StartTime - prev.OriginPeak.RetentionTime);
                endTime = next.AlignedPeak.RetentionTime - scale*(next.OriginPeak.RetentionTime - _origin.EndTime);
            }
            else if (next != null)
            {
                endTime = next.AlignedPeak.StartTime - scale*(next.OriginPeak.StartTime - _origin.EndTime);
                startTime = endTime - range;
            }
            else
            {
                startTime = prev.AlignedPeak.EndTime + scale*(_origin.StartTime - prev.OriginPeak.EndTime);
                endTime = startTime + range;
            }
            return new PeakMatch(startTime + (_shiftLeft*range), endTime + (_shiftRight*range));
        }

        private List<PeakAlignment> GetPeakAlignments(IEnumerable<PeakMatchData> otherMatchData)
        {
            // TODO: Try to improve this. Aligns peaks in area descending order until peaks do not match
            var alignments = new List<PeakAlignment>();
            var origin = _peakMatchData.OrderByDescending(data => data.PercentArea).GetEnumerator();
            var other = otherMatchData.OrderByDescending(data => data.PercentArea).GetEnumerator();
            while (origin.MoveNext() && other.MoveNext())
            {
                if (!TryInsertPeakAlignment(alignments, new PeakAlignment(origin.Current, other.Current)))
                    break;
            }
            return alignments;
        }

        private static ChromatogramGroupInfo GetChromGroupInfo(IEnumerable<ChromatogramGroupInfo> chromSetInfos, MsDataFileUri filePath)
        {
            return chromSetInfos.FirstOrDefault(info => Equals(filePath, info.FilePath));
        }

        private static bool TryInsertPeakAlignment(IList<PeakAlignment> alignList, PeakAlignment alignment)
        {
            var origin = alignment.OriginPeak;
            var other = alignment.AlignedPeak;

            if (Math.Min(origin.PercentArea, other.PercentArea) < ALIGN_AREA_MIN || origin.Abundances.Dot(other.Abundances) < ALIGN_DOT_MIN)
                return false;

            PeakAlignment prev, next;
            var insertPos = GetSurroundingAlignments(alignList, origin, out prev, out next);
            if ((prev != null && other.RetentionTime < prev.AlignedPeak.RetentionTime) ||
                (next != null && other.RetentionTime > next.AlignedPeak.RetentionTime))
            {
                return false;
            }

            alignList.Insert(insertPos, alignment);
            return true;
        }

        private static int GetSurroundingAlignments(IList<PeakAlignment> alignList, PeakMatchData origin,
            out PeakAlignment prev, out PeakAlignment next)
        {
            prev = next = null;
            if (!alignList.Any())
                return 0;

            var insertPos = alignList.Select(align => align.OriginPeak).ToList().BinarySearch(origin, RT_COMPARER);
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

            public SrmDocument ChangePeak(SrmDocument document, IdentityPath groupPath, string nameSet, MsDataFileUri filePath)
            {
                if ((_retentionTime ?? StartTime) == null)
                    return document;

                return _retentionTime.HasValue
                    ? document.ChangePeak(groupPath, nameSet, filePath, null, _retentionTime.Value, UserSet.TRUE)
                    : document.ChangePeak(groupPath, nameSet, filePath, null, StartTime, EndTime, UserSet.TRUE, null, false);
            }
        }

        private class PeakMatchData
        {
            public IonAbundances Abundances { get; private set; }
            public double PercentArea { get; private set; }
            public float RetentionTime { get; private set; }
            public float StartTime { get; private set; }
            public float EndTime { get; private set; }

            public PeakMatchData(IonAbundances abundances, double percentArea, float rt, float rtStart, float rtEnd)
            {
                Abundances = abundances;
                PercentArea = percentArea;
                RetentionTime = rt;
                StartTime = rtStart;
                EndTime = rtEnd;
            }

            public PeakMatchData(TransitionGroupDocNode nodeTranGroup, ChromatogramGroupInfo chromGroupInfo,
                float mzMatchTolerance, int peakIndex, double totalChromArea)
            {
                Abundances = new IonAbundances(nodeTranGroup, chromGroupInfo, mzMatchTolerance, peakIndex);
                PercentArea = Abundances.Sum()/totalChromArea;
                var peak = GetLargestPeak(nodeTranGroup, chromGroupInfo, peakIndex, mzMatchTolerance);
                RetentionTime = peak.RetentionTime;
                StartTime = peak.StartTime;
                EndTime = peak.EndTime;
            }

            public double GetMatchScore(PeakMatchData other, float timeMin, float timeMax)
            {
                return SCORE_DOT_WEIGHT * Abundances.Dot(other.Abundances) +
                       SCORE_RT_WEIGHT * (1 - Math.Abs((RetentionTime - timeMin) - (other.RetentionTime - timeMin)) / (timeMax - timeMin)) +
                       SCORE_AREA_WEIGHT * (1 - Math.Abs(PercentArea - other.PercentArea));
            }

            private static ChromPeak GetLargestPeak(TransitionGroupDocNode nodeTranGroup,
                ChromatogramGroupInfo chromGroupInfo, int peakIndex, float mzMatchTolerance)
            {
                ChromPeak largestPeak = ChromPeak.EMPTY;
                foreach (TransitionDocNode transitionDocNode in nodeTranGroup.Transitions)
                {
                    ChromatogramInfo chromInfo = chromGroupInfo.GetTransitionInfo((float)transitionDocNode.Mz, mzMatchTolerance);
                    if (chromInfo == null)
                        continue;

                    var peak = chromInfo.GetPeak(peakIndex);
                    if (peak.Height > largestPeak.Height)
                        largestPeak = peak;
                }
                return largestPeak;
            }
        }

        private static readonly Comparer<PeakMatchData> RT_COMPARER =
            Comparer<PeakMatchData>.Create((x, y) => x.RetentionTime.CompareTo(y.RetentionTime));

        private class PeakAlignment
        {
            public PeakMatchData OriginPeak { get; private set; }
            public PeakMatchData AlignedPeak { get; private set; }
            
            public PeakAlignment(PeakMatchData originPeak, PeakMatchData alignedPeak)
            {
                OriginPeak = originPeak;
                AlignedPeak = alignedPeak;
            }
        }

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
                foreach (TransitionDocNode nodeTran in nodeTranGroup.Children)
                {
                    var chromInfoCached = chromGroupInfo.GetTransitionInfo((float) nodeTran.Mz, mzMatchTolerance);
                    if (chromInfoCached == null)
                        continue;

                    Add(nodeTran, chromInfoCached.GetPeak(peakIndex).Area);
                }
            }

            public void Add(TransitionDocNode nodeTran, float abundance)
            {
                _abundances[nodeTran.FragmentIonName] = abundance;
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
                    abundancesThis.Add(_abundances.ContainsKey(fragment) ? _abundances[fragment] : 0);
                    abundancesOther.Add(other._abundances.ContainsKey(fragment) ? other._abundances[fragment] : 0);
                }
                var statisticsThis = new Statistics(abundancesThis);
                var statisticsOther = new Statistics(abundancesOther);
                return statisticsThis.Angle(statisticsOther);
            }
        }
    }
}
