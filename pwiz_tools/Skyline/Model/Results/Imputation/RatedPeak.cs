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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class RatedPeak : Immutable
    {
        public RatedPeak(ReplicateFileInfo resultFileInfo, AlignmentFunction alignmentFunction, TimeIntervals chromatogramTimeIntervals, FormattablePeakBounds rawPeakBounds, double? score, bool manuallyIntegrated)
        {
            ReplicateFileInfo = resultFileInfo;
            AlignmentFunction = alignmentFunction;
            TimeIntervals = chromatogramTimeIntervals;
            RawPeakBounds = MakeValidPeakBounds(TimeIntervals, rawPeakBounds);
            AlignedPeakBounds = RawPeakBounds?.Align(alignmentFunction);
            ManuallyIntegrated = manuallyIntegrated;
            Score = score;
        }

        public ReplicateFileInfo ReplicateFileInfo { get; }
        public FormattablePeakBounds RawPeakBounds { get; }

        public FormattablePeakBounds AlignedPeakBounds { get; private set; }

        public TimeIntervals TimeIntervals { get; }

        public double? Score { get; private set; }

        public RatedPeak ChangeScore(double? value)
        {
            return ChangeProp(ImClone(this), im => im.Score = value);
        }

        public bool ManuallyIntegrated { get; }
        public Verdict PeakVerdict { get; private set; }

        public string Opinion { get; private set; }

        public RatedPeak ChangeVerdict(Verdict verdict, string opinion)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.PeakVerdict = verdict;
                im.Opinion = opinion;
            });
        }

        public double? RtShift { get; private set; }

        public RatedPeak ChangeRtShift(double? value)
        {
            return ChangeProp(ImClone(this), im => im.RtShift = value);
        }

        public AlignmentFunction AlignmentFunction { get; }

        public enum Verdict
        {
            Unknown,
            NeedsRemoval,
            NeedsAdjustment,
            Accepted,
            Exemplary
        }

        public static FormattablePeakBounds MakeValidPeakBounds(TimeIntervals timeIntervals, FormattablePeakBounds peakBounds)
        {
            if (timeIntervals == null)
            {
                return peakBounds;
            }
            if (peakBounds == null)
            {
                return null;
            }

            int? intervalIndex = timeIntervals.IndexOfIntervalContaining((float)peakBounds.MidTime);
            if (!intervalIndex.HasValue)
            {
                return null;
            }
            var newStartTime = Math.Max(peakBounds.StartTime, timeIntervals.Starts[intervalIndex.Value]);
            var newEndTime = Math.Min(peakBounds.EndTime, timeIntervals.Ends[intervalIndex.Value]);
            if (newStartTime >= newEndTime)
            {
                return null;
            }

            if (newEndTime - newStartTime < Math.Max(0, peakBounds.Width / 2))
            {
                return null;
            }
            return new FormattablePeakBounds(newStartTime, newEndTime);
        }
    }
}