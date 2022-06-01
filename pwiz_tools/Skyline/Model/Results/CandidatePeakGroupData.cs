/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results
{
    public class CandidatePeakGroupData
    {
        public CandidatePeakGroupData(int? peakIndex, double retentionTime, double minStartTime, double maxEndTime,
            bool chosen, PeakGroupScore score, bool originallyBestPeak)
        {
            PeakIndex = peakIndex;
            RetentionTime = retentionTime;
            MinStartTime = minStartTime;
            MaxEndTime = maxEndTime;
            Chosen = chosen;
            Score = score;
            OriginallyBestPeak = originallyBestPeak;
        }

        public int? PeakIndex { get; }
        public double RetentionTime { get; }
        public double MinStartTime { get; }
        public double MaxEndTime { get; }
        public bool Chosen { get; }
        public PeakGroupScore Score { get; }
        public bool OriginallyBestPeak { get; }

        public static CandidatePeakGroupData CustomPeak(double retentionTime, double minStartTime, double maxEndTime,
            PeakGroupScore score)
        {
            return new CandidatePeakGroupData(null, retentionTime, minStartTime, maxEndTime, true, score, false);
        }

        public static CandidatePeakGroupData FoundPeak(int peakIndex, double retentionTime, double minStartTime,
            double maxEndTime, bool chosen, PeakGroupScore score, bool originallyBestPeak)
        {
            return new CandidatePeakGroupData(peakIndex, retentionTime, minStartTime, maxEndTime, chosen, score,
                originallyBestPeak);
        }
    }
}
