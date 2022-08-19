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
using System.IO;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    /// <summary>
    /// A set of transition groups containing scored peaks.
    /// </summary>
    public class ScoredGroupPeaksSet
    {
        private List<ScoredGroupPeaks> _scoredGroupPeaksList;

        public ScoredGroupPeaksSet()
        {
            _scoredGroupPeaksList = new List<ScoredGroupPeaks>();
        }

        public ScoredGroupPeaksSet(IEnumerable<IList<FeatureScores>> groupList, int capacity)
        {
            _scoredGroupPeaksList = new List<ScoredGroupPeaks>(capacity);
            foreach (var group in groupList)
            {
                var scoredGroupPeaks = new ScoredGroupPeaks(group.Count);
                foreach (var features in group)
                    scoredGroupPeaks.Add(new ScoredPeak(features));
                _scoredGroupPeaksList.Add(scoredGroupPeaks);
            }
        }

        public double Mean { get; private set; }
        public double Stdev { get; private set; }

        public List<ScoredGroupPeaks> ScoredGroupPeaksList
        {
            get { return _scoredGroupPeaksList; }
        }

        public int Count
        {
            get { return _scoredGroupPeaksList.Count; }
        }

        public void Add(ScoredGroupPeaks scoredGroupPeaks)
        {
            _scoredGroupPeaksList.Add(scoredGroupPeaks);
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
                _scoredGroupPeaksList = new List<ScoredGroupPeaks>(_scoredGroupPeaksList.OrderBy(key => key.Id));
                _scoredGroupPeaksList.RemoveRange(newCount, Count - newCount);
            }
            else
            {
                // Randomly select half the transition groups to keep.
                var oldScoredGroupPeaks = _scoredGroupPeaksList;
                _scoredGroupPeaksList = new List<ScoredGroupPeaks>(newCount);
                for (int i = 0; i < newCount; i++)
                {
                    var randomIndex = random.Next(oldScoredGroupPeaks.Count);
                    _scoredGroupPeaksList.Add(oldScoredGroupPeaks[randomIndex]);
                    oldScoredGroupPeaks.RemoveAt(randomIndex);
                }
            }
        }

        /// <summary>
        /// Return a list of peaks, where each peak has the maximum score in its transition group,
        /// and its q-value is less than the cutoff value.
        /// </summary>
        /// <param name="decoyScoredGroupPeaks">Decoy transition groups.</param>
        /// <param name="qValueCutoff">Cutoff q-value.</param>
        /// <param name="lambda">Optional p-value cutoff for calculating Pi-zero.</param>
        /// <param name="nonParametric">Non-parametric p value calculation if true and based on normal distribution if false</param>
        /// <returns>List of peaks the meet the criteria.</returns>
        public List<ScoredPeak> SelectTruePeaks(ScoredGroupPeaksSet decoyScoredGroupPeaks, double qValueCutoff, double? lambda, bool nonParametric)
        {
            var pvalues = CalcPValues(decoyScoredGroupPeaks, nonParametric);
            var qvalues = new Statistics(pvalues).Qvalues(lambda, MProphetPeakScoringModel.PI_ZERO_MIN);

            // Select max peak with q value less than the cutoff from each target group.
            var truePeaks = new List<ScoredPeak>(_scoredGroupPeaksList.Count/5);
            for (int i = 0; i < _scoredGroupPeaksList.Count; i++)
            {
                if (qvalues[i] <= qValueCutoff)
                    truePeaks.Add(_scoredGroupPeaksList[i].MaxPeak);
            }
            return truePeaks;
        }

        private double[] CalcPValues(ScoredGroupPeaksSet decoyScoredGroupPeaks, bool nonParametric = false)
        {
            // Get max peak score for each transition group.
            var targetScores = GetMaxScores();
            var decoyScores = decoyScoredGroupPeaks.GetMaxScores();

            // Calculate statistics for each set of scores.
            var statDecoys = new Statistics(decoyScores);
            var statTarget = new Statistics(targetScores);

            return nonParametric
                ? statDecoys.PvaluesNull(statTarget)
                : statDecoys.PvaluesNorm(statTarget);
        }

        /// <summary>
        /// Return a list of peaks that have the highest score in each transition group.
        /// </summary>
        /// <returns>List of highest scoring peaks.</returns>
        public List<ScoredPeak> SelectMaxPeaks()
        {
            return _scoredGroupPeaksList.Select(t => t.MaxPeak).ToList();
        }

        public void SelectTargetsAndDecoys(out ScoredGroupPeaksSet targetScoredGroupPeaksSet, out ScoredGroupPeaksSet decoyScoredGroupPeaksSet)
        {
            targetScoredGroupPeaksSet = new ScoredGroupPeaksSet();
            decoyScoredGroupPeaksSet = new ScoredGroupPeaksSet();
            foreach (var scoredGroupPeaks in _scoredGroupPeaksList)
            {
                var secondHighestPeak = scoredGroupPeaks.SecondHighestPeak;
                if (secondHighestPeak != null)
                {
                    var decoyScoredGroupPeaks = new ScoredGroupPeaks();
                    decoyScoredGroupPeaks.Add(secondHighestPeak);
                    decoyScoredGroupPeaksSet.Add(decoyScoredGroupPeaks);
                }

                // Copy all other peaks to target.
                var targetScoredGroupPeaks = new ScoredGroupPeaks();
                foreach (var peak in scoredGroupPeaks.ScoredPeaks)
                {
                    if (!ReferenceEquals(peak, secondHighestPeak))
                        targetScoredGroupPeaks.Add(peak);
                }
                targetScoredGroupPeaksSet.Add(targetScoredGroupPeaks);
            }
        }

        public ScoredPeak FirstPeak
        {
            get { return _scoredGroupPeaksList[0].ScoredPeaks[0]; }
        }

        private double[] GetMaxScores()
        {
            var maxScores = new double[_scoredGroupPeaksList.Count];
            ParallelEx.For(0, maxScores.Length, i =>
            {
                var maxPeak = _scoredGroupPeaksList[i].MaxPeak;
                maxScores[i] = maxPeak?.Score ?? double.NaN;
            });
                
            return maxScores;
        }

        public void WriteBest(string filePath)
        {
            using (var saver = new FileSaver(filePath))
            using (var writer = new StreamWriter(saver.SafeName))
            {
                foreach (var peaks in _scoredGroupPeaksList)
                {
                    var maxPeak = peaks.MaxPeak;
                    writer.WriteLine(maxPeak?.Score ?? double.NaN);
                }
                writer.Close();
                saver.Commit();
            }
        }

        /// <summary>
        /// Recalculate the scores of each peak by applying the given feature weighting factors.
        /// </summary>
        /// <param name="weights">Array of weight factors applied to each feature.</param>
        /// <param name="replaceUnknownFeatureScores">Whether feature scores which are NaN or infinity should be replaced with zero</param>
        /// <returns>Mean peak score.</returns>
        public void ScorePeaks(IList<double> weights, bool replaceUnknownFeatureScores)
        {
            ParallelEx.For(0, _scoredGroupPeaksList.Count, i =>
            {
                var scoredPeaks = _scoredGroupPeaksList[i].ScoredPeaks;
                for (int j = 0; j < scoredPeaks.Count; j++)
                {
                    scoredPeaks[j] = scoredPeaks[j].CalcScore(weights, replaceUnknownFeatureScores);
                }
            });

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
        public List<IList<FeatureScores>> ToList()
        {
            var list = new List<IList<FeatureScores>>(_scoredGroupPeaksList.Count);
            foreach (var scoredGroupPeaks in _scoredGroupPeaksList)
                list.Add(scoredGroupPeaks.ToList());
            return list;
        }
        #endregion
    }
}