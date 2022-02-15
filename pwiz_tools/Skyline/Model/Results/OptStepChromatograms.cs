/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// A list of chromatograms associated with a particular Transition corresponding to optimization steps ranging from
    /// -StepCount to +StepCount.
    /// </summary>
    public class OptStepChromatograms
    {
        public static readonly OptStepChromatograms EMPTY =
            new OptStepChromatograms(SignedMz.ZERO, ImmutableList.Empty<ChromatogramInfo>(), 0);
        private readonly int _centerIndex;
        private ImmutableList<ChromatogramInfo> _chromatogramInfos;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="productMz">The m/z of the transition that these chromatograms are for</param>
        /// <param name="chromatograms">List chromatograms </param>
        /// <param name="stepCount"></param>
        public OptStepChromatograms(SignedMz productMz, IEnumerable<ChromatogramInfo> chromatograms, int stepCount)
        {
            _chromatogramInfos = ImmutableList.ValueOf(chromatograms);
            StepCount = stepCount;
            _centerIndex = IndexOfCenter(productMz, _chromatogramInfos.Select(c => c.ProductMz), stepCount);
        }

        public static OptStepChromatograms FromChromatogram(ChromatogramInfo chromatogramInfo)
        {
            if (chromatogramInfo == null)
            {
                return EMPTY;
            }
            return new OptStepChromatograms(chromatogramInfo.ProductMz, ImmutableList.Singleton(chromatogramInfo), 0);
        }

        /// <summary>
        /// The number of chromatogram steps, typically equal to <see cref="OptimizableRegression.StepCount"/>.
        /// </summary>
        public int StepCount { get; }

        /// <summary>
        /// True if all of the chromatograms in this list are null.
        /// </summary>
        public bool IsEmpty => _chromatogramInfos.Count == 0;

        /// <summary>
        /// Returns the ChromatogramInfo for the specified step number, or null.
        /// </summary>
        public ChromatogramInfo GetChromatogramForStep(int step)
        {
            int index = _centerIndex + step;
            if (index < 0 || index >= _chromatogramInfos.Count)
            {
                return null;
            }

            return _chromatogramInfos[index];
        }

        /// <summary>
        /// Given a list of m/z's, return the index of the one which should be associated with "step zero".
        /// If the number of m/z's exactly matches the expected number for the specified stepCount, then
        /// assume that the middle number is step 0. Otherwise, return the index of the productMz which is closest
        /// to the target m/z.
        /// </summary>
        public static int IndexOfCenter(SignedMz targetMz, IEnumerable<SignedMz> productMzs, int stepCount)
        {
            var list = productMzs.ToList();
            if (list.Count == stepCount * 2 + 1)
            {
                return stepCount;
            }

            if (list.Count <= 1)
            {
                return 0;
            }
            double minDelta = Math.Abs(list[0] - targetMz);
            int closestIndex = 0;
            for (int i = 1; i < list.Count; i++)
            {
                double delta = Math.Abs(list[i] - targetMz);
                if (delta < minDelta)
                {
                    closestIndex = i;
                    minDelta = delta;
                }
            }
            return closestIndex;
        }
    }
}
