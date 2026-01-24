/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Helper class for efficiently finding matching transitions.
    /// Identifies sorted regions (where Source is constant and m/z is non-decreasing)
    /// and uses binary search within each region.
    /// </summary>
    public class ChromTransitionMatcher
    {
        private readonly IList<ChromTransition> _transitions;

        // Region boundary indexes (excluding 0 which is always the first region start)
        // A region boundary occurs when Source changes or m/z decreases
        private readonly List<int> _regionBoundaries;

        public ChromTransitionMatcher(IList<ChromTransition> transitions)
        {
            _transitions = transitions;
            _regionBoundaries = Enumerable.Range(1, transitions.Count - 1)
                .Where(i => transitions[i].Source != transitions[i - 1].Source ||
                            transitions[i].Product < transitions[i - 1].Product)
                .ToList();
        }

        /// <summary>
        /// Find the best matching transition index for the given product m/z.
        /// </summary>
        /// <param name="productMz">Product m/z to match</param>
        /// <param name="tolerance">m/z tolerance for matching</param>
        /// <param name="isMs1">True for MS1 transition, false for fragment, null to search all sources</param>
        /// <returns>Index of best match, or null if no match found</returns>
        public int? GetBestProductIndex(double productMz, float tolerance, bool? isMs1)
        {
            int? iNearest = null;
            double maxMz = productMz + tolerance;
            double deltaNearestMz = double.MaxValue;

            int regionStart = 0;
            foreach (var regionEnd in _regionBoundaries.Append(_transitions.Count))
            {
                // Check if this region's source can match
                var regionSource = _transitions[regionStart].Source;
                if (!SourceMatches(regionSource, isMs1))
                {
                    regionStart = regionEnd;
                    continue;
                }

                double minMz = productMz - (iNearest.HasValue ? deltaNearestMz : tolerance);

                // Binary search within this region to find first element >= minMz
                int lo = regionStart;
                int hi = regionEnd;
                while (lo < hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    if (_transitions[mid].Product < minMz)
                        lo = mid + 1;
                    else
                        hi = mid;
                }

                // Scan candidates within tolerance range
                for (int i = lo; i < regionEnd; i++)
                {
                    var chromTransition = _transitions[i];
                    double product = chromTransition.Product;

                    if (product > maxMz)
                        break;

                    if (chromTransition.OptimizationStep != 0)
                        continue;

                    double deltaMz = Math.Abs(productMz - product);
                    if (deltaMz < deltaNearestMz)
                    {
                        iNearest = i;
                        deltaNearestMz = deltaMz;
                        maxMz = productMz + deltaNearestMz;
                    }
                }

                regionStart = regionEnd;
            }

            return iNearest;
        }

        /// <summary>
        /// Check if a chromatogram source can match a transition with the given isMs1 flag.
        /// MS1 sources (sim, ms1) can only be matched by MS1 transitions.
        /// Fragment and unknown sources can be matched by any transition.
        /// </summary>
        private static bool SourceMatches(ChromSource source, bool? isMs1)
        {
            if (isMs1 == null)
                return true; // No filter, match any source

            bool isMs1Source = source == ChromSource.ms1 || source == ChromSource.sim;
            if (isMs1Source && isMs1 == false)
                return false; // MS1 source cannot match fragment transition

            return true;
        }
    }
}
