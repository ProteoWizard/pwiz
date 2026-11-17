/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Turns the collision energies of waters_connect CE optimization chromatograms into the product m/z
    /// spacing that <see cref="ChromatogramDataProvider"/> uses to tell optimization steps apart.
    ///
    /// A waters_connect method acquires every CE step of a transition at its real product m/z, so the
    /// chromatograms of one transition differ only in collision energy. Spacing their product m/z by one
    /// <see cref="ChromatogramInfo.OPTIMIZE_SHIFT_SIZE"/> per step, in CE order, lets them be loaded exactly
    /// like data acquired from a method that stepped the product m/z itself. Data acquired that way already
    /// has a unique product m/z per chromatogram and is left alone.
    /// </summary>
    public static class WatersConnectCeSteps
    {
        /// <summary>
        /// Spaces the product m/z of each series of chromatograms that share a precursor and product m/z and
        /// differ only in collision energy. The center step keeps the real product m/z, so the loader matches
        /// it to the document transition. Chromatograms outside such a series are left untouched.
        /// </summary>
        /// <param name="chromIds">The chromatogram keys to renumber, modified in place</param>
        /// <param name="stepCount">Optimization steps either side of center, from the replicate's
        /// optimization function. When a series is short because the export dropped steps whose collision
        /// energy would not have been positive, the highest collision energy is taken to be the last step.</param>
        public static void SpaceProductMzBySteps(IList<ChromKeyProviderIdPair> chromIds, int stepCount)
        {
            foreach (var series in GetSeries(chromIds))
            {
                int centerIndex = GetCenterIndex(series.Count, stepCount);
                for (int i = 0; i < series.Count; i++)
                {
                    var chromId = chromIds[series[i]];
                    var productMz = chromId.Key.Product + (i - centerIndex) * ChromatogramInfo.OPTIMIZE_SHIFT_SIZE;
                    chromIds[series[i]] = new ChromKeyProviderIdPair(
                        chromId.Key.ChangeOptimizationStep(chromId.Key.OptimizationStep, productMz), chromId.ProviderId);
                }
            }
        }

        /// <summary>
        /// Returns the indexes of each series of chromatograms with the same precursor and product m/z and
        /// distinct collision energies, ordered by collision energy. A series needs at least 3 chromatograms,
        /// because a single optimization step either side of center is the smallest set Skyline can export.
        /// </summary>
        private static IEnumerable<IList<int>> GetSeries(IList<ChromKeyProviderIdPair> chromIds)
        {
            var seriesByTransition = new Dictionary<KeyValuePair<SignedMz, SignedMz>, List<int>>();
            for (int i = 0; i < chromIds.Count; i++)
            {
                var key = chromIds[i].Key;
                if (key.CollisionEnergy <= 0)
                    continue;
                var transition = new KeyValuePair<SignedMz, SignedMz>(key.Precursor, key.Product);
                if (!seriesByTransition.TryGetValue(transition, out var series))
                {
                    series = new List<int>();
                    seriesByTransition.Add(transition, series);
                }
                series.Add(i);
            }

            foreach (var series in seriesByTransition.Values)
            {
                if (series.Count < 3)
                    continue;
                series.Sort((index1, index2) =>
                    chromIds[index1].Key.CollisionEnergy.CompareTo(chromIds[index2].Key.CollisionEnergy));
                if (HasRepeatedCollisionEnergy(chromIds, series))
                    continue;
                yield return series;
            }
        }

        private static bool HasRepeatedCollisionEnergy(IList<ChromKeyProviderIdPair> chromIds, IList<int> series)
        {
            return series.Select(index => chromIds[index].Key.CollisionEnergy).Distinct().Count() != series.Count;
        }

        /// <summary>
        /// The series member that keeps the real product m/z: the middle one for a full set of steps, and
        /// otherwise the one that makes the highest collision energy the last step, since the export drops
        /// steps from the low end.
        /// </summary>
        private static int GetCenterIndex(int seriesCount, int stepCount)
        {
            int centerIndex = seriesCount - 1 - stepCount;
            if (seriesCount > stepCount * 2 || centerIndex < 0)
                centerIndex = (seriesCount + 1) / 2 - 1;    // Same rule as ChromatogramDataProvider uses
            return centerIndex;
        }
    }
}
