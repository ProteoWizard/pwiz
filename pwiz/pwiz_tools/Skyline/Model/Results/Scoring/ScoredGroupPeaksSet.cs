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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    /// <summary>
    /// A set of transition groups containing scored peaks.
    /// </summary>
    public class ScoredGroupPeaksSet
    {
        private List<ScoredGroupPeaks> _transitionGroups = new List<ScoredGroupPeaks>();

        public ScoredGroupPeaksSet()
        {
        }

        public ScoredGroupPeaksSet(IEnumerable<IList<double[]>> groupList)
        {
            foreach (var group in groupList)
            {
                var transitionGroup = new ScoredGroupPeaks();
                foreach (var features in group)
                    transitionGroup.Add(new ScoredPeak(features));
                _transitionGroups.Add(transitionGroup);
            }
        }

        public double Mean { get; private set; }
        public double Stdev { get; private set; }

        public int Count
        {
            get { return _transitionGroups.Count; }
        }

        public void Add(ScoredGroupPeaks transitionGroup)
        {
            _transitionGroups.Add(transitionGroup);
        }

        /// <summary>
        /// Discard half the transition groups (usually to save them for testing).  If a random
        /// number generator is specified, the discarded groups are chosen randomly.  Otherwise,
        /// the groups are sorted alphabetically by id, and the last half are discarded.
        /// </summary>
        /// <param name="random">Optional random number generator to discard random groups.</param>
        public void DiscardHalf(Random random = null)
        {
            var newCount = (int)Math.Round(Count / 2.0, MidpointRounding.ToEven);    // Emulate R rounding.

            if (random == null)
            {
                // Sort by transition group name, then discard upper half of list.
                _transitionGroups = new List<ScoredGroupPeaks>(_transitionGroups.OrderBy(key => key.Id));
                _transitionGroups.RemoveRange(newCount, Count - newCount);
            }
            else
            {
                // Randomly select half the transition groups to keep.
                var oldTransitionGroups = _transitionGroups;
                _transitionGroups = new List<ScoredGroupPeaks>(newCount);
                for (int i = 0; i < newCount; i++)
                {
                    var randomIndex = random.Next(oldTransitionGroups.Count);
                    _transitionGroups.Add(oldTransitionGroups[randomIndex]);
                    oldTransitionGroups.RemoveAt(randomIndex);
                }
            }
        }

        /// <summary>
        /// Return a list of peaks, where each peak has the maximum score in its transition group,
        /// and its q-value is less than the cutoff value.
        /// </summary>
        /// <param name="qValueCutoff">Cutoff q-value.</param>
        /// <param name="lambda">Optional p-value cutoff for calculating Pi-zero.</param>
        /// <param name="decoyTransitionGroups">Decoy transition groups.</param>
        /// <returns>List of peaks the meet the criteria.</returns>
        public List<ScoredPeak> SelectTruePeaks(double qValueCutoff, double? lambda, ScoredGroupPeaksSet decoyTransitionGroups)
        {
            // Get max peak score for each transition group.
            var targetScores = GetMaxScores();
            var decoyScores = decoyTransitionGroups.GetMaxScores();

            // Calculate statistics for each set of scores.
            var statDecoys = new Statistics(decoyScores);
            var statTarget = new Statistics(targetScores);

            // Calculate q values from decoy set.
            var pvalues = statDecoys.PvaluesNorm(statTarget);
            var qvalues = new Statistics(pvalues).Qvalues(lambda);

            // Select max peak with q value less than the cutoff from each target group.
            var truePeaks = new List<ScoredPeak>(_transitionGroups.Count);
            for (int i = 0; i < _transitionGroups.Count; i++)
            {
                if (qvalues[i] <= qValueCutoff)
                    truePeaks.Add(_transitionGroups[i].MaxPeak);
            }
            return truePeaks;
        }

        /// <summary>
        /// Return a list of peaks that have the highest score in each transition group.
        /// </summary>
        /// <returns>List of highest scoring peaks.</returns>
        public List<ScoredPeak> SelectMaxPeaks()
        {
            return _transitionGroups.Select(t => t.MaxPeak).ToList();
        }

        public void SelectTargetsAndDecoys(out ScoredGroupPeaksSet targetTransitionGroups, out ScoredGroupPeaksSet decoyTransitionGroups)
        {
            targetTransitionGroups = new ScoredGroupPeaksSet();
            decoyTransitionGroups = new ScoredGroupPeaksSet();
            foreach (var transitionGroup in _transitionGroups)
            {
                var secondHighestPeak = transitionGroup.SecondHighestPeak;
                if (secondHighestPeak != null)
                {
                    var decoyTransitionGroup = new ScoredGroupPeaks();
                    decoyTransitionGroup.Add(secondHighestPeak);
                    decoyTransitionGroups.Add(decoyTransitionGroup);
                }

                // Copy all other peaks to target.
                var targetTransitionGroup = new ScoredGroupPeaks();
                foreach (var peak in transitionGroup.ScoredPeaks)
                {
                    if (peak != secondHighestPeak)
                        targetTransitionGroup.Add(peak);
                }
                targetTransitionGroups.Add(targetTransitionGroup);
            }
        }

        public ScoredPeak FirstPeak
        {
            get { return _transitionGroups[0].ScoredPeaks[0]; }
        }

        private double[] GetMaxScores()
        {
            var maxScores = new double[_transitionGroups.Count];
            for (int i = 0; i < maxScores.Length; i++)
                maxScores[i] = _transitionGroups[i].MaxPeak.Score;
            return maxScores;
        }

        /// <summary>
        /// Recalculate the scores of each peak by applying the given feature weighting factors.
        /// </summary>
        /// <param name="weights">Array of weight factors applied to each feature.</param>
        /// <returns>Mean peak score.</returns>
        public void ScorePeaks(IList<double> weights)
        {
            foreach (var peak in _transitionGroups.SelectMany(transitionGroup => transitionGroup.ScoredPeaks))
            {
                peak.Score = 0;
                for (int i = 0; i < weights.Count; i++)
                {
                    if (!Double.IsNaN(weights[i]))
                        peak.Score += weights[i] * peak.Features[i];
                }
            }

            // Calculate mean and stdev for top-scoring peaks in each transition group.
            var scores = GetMaxScores();
            var stats = new Statistics(scores);
            Mean = stats.Mean();
            Stdev = stats.StdDev();
        }

        #region Functional Test Support
        /// <summary>
        /// Return a list of transition groups, each containing peak feature values.
        /// </summary>
        /// <returns></returns>
        public List<IList<double[]>> ToList()
        {
            var list = new List<IList<double[]>>(_transitionGroups.Count);
            foreach (var transitionGroup in _transitionGroups)
                list.Add(transitionGroup.ToList());
            return list;
        }
        #endregion
    }
}