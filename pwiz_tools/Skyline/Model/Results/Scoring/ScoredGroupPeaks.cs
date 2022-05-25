/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Results.Scoring
{
    /// <summary>
    /// The calculated features and scores for the peaks in a transition group.
    /// </summary>
    public class ScoredGroupPeaks
    {
        public string Id { get; set; }    // used for debugging
        public List<ScoredPeak> ScoredPeaks { get; private set; }

        public ScoredGroupPeaks(int? capacity = null)
        {
            ScoredPeaks = capacity.HasValue
                ? new List<ScoredPeak>(capacity.Value)
                : new List<ScoredPeak>();
        }

        /// <summary>
        /// Add a peak.
        /// </summary>
        /// <param name="peak">Peak to add to this group.</param>
        public void Add(ScoredPeak peak)
        {
            ScoredPeaks.Add(peak);
        }

        /// <summary>
        /// Find the peak with the maximum score.
        /// </summary>
        public ScoredPeak MaxPeak
        {
            get
            {
                if (ScoredPeaks.Count == 0)
                    return null;
                var maxPeak = ScoredPeaks[0];
                var maxScore = maxPeak.Score;
                for (int i = 1; i < ScoredPeaks.Count; i++)
                {
                    var peak = ScoredPeaks[i];
                    if (maxScore < peak.Score)
                    {
                        maxScore = peak.Score;
                        maxPeak = peak;
                    }
                }
                return maxPeak;
            }
        }

        public ScoredPeak SecondHighestPeak
        {
            get
            {
                if (ScoredPeaks.Count == 0)
                    return null;
                var maxPeak = ScoredPeaks[0];
                double maxScore = maxPeak.Score;
                ScoredPeak max2Peak = null;
                double max2Score = Double.MinValue;
                for (int i = 1; i < ScoredPeaks.Count; i++)
                {
                    var peak = ScoredPeaks[i];
                    if (max2Score < peak.Score)
                    {
                        if (maxScore < peak.Score)
                        {
                            max2Score = maxScore;
                            max2Peak = maxPeak;
                            maxScore = peak.Score;
                            maxPeak = peak;
                        }
                        else
                        {
                            max2Score = peak.Score;
                            max2Peak = peak;
                        }
                    }
                }
                return max2Peak;
            }
        }

        #region Functional Test Support
        /// <summary>
        /// Return a list of peak feature values.
        /// </summary>
        /// <returns></returns>
        public List<FeatureScores> ToList()
        {
            return ScoredPeaks.Select(peak => peak.FeatureScores).ToList();
        }

        public override string ToString()
        {
            return string.Format(@"{0}: {1}", Id, MaxPeak);
        }
        #endregion
    }
}
