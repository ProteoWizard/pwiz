/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Linear-time medians by selection rather than sorting: Hoare's FIND, as in Skyline's
    /// <c>QNthItem</c> (pwiz.Skyline.Util), specialized for doubles in a span.
    ///
    /// <para>The median is a value, not a position, so it equals the median a full sort
    /// would give for the same values - the order the selection leaves them in does not
    /// matter. The one way two equal-comparing doubles can differ is the sign of zero; a
    /// caller whose values may hold both -0.0 and +0.0 could see either back.</para>
    ///
    /// <para>The values must not contain NaN, which no ordering can place.</para>
    /// </summary>
    public static class MedianMath
    {
        /// <summary>
        /// The median of <paramref name="values"/> - the middle value, or the mean of the two
        /// middle values for an even count - or NaN when there are none. Reorders the values.
        /// </summary>
        public static double MedianInPlace(Span<double> values)
        {
            int n = values.Length;
            if (n == 0)
                return double.NaN;
            int mid = n / 2;
            double upper = SelectInPlace(values, mid);
            if (n % 2 != 0)
                return upper;
            // The selection leaves every value below index mid at most values[mid], so the
            // lower middle value is the largest of them.
            double lower = values[0];
            for (int i = 1; i < mid; i++)
            {
                if (values[i] > lower)
                    lower = values[i];
            }
            return 0.5 * (lower + upper);
        }

        /// <summary>
        /// The value that would be at <paramref name="elementIndex"/> if
        /// <paramref name="values"/> were sorted ascending. Partitions the values around it in
        /// place: on return it is at that index, with no greater value before it and no lesser
        /// value after it.
        /// </summary>
        public static double SelectInPlace(Span<double> values, int elementIndex)
        {
            if (elementIndex < 0 || values.Length <= elementIndex)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));

            int left = 0;
            int right = values.Length - 1;
            while (left < right)
            {
                double pivot = values[elementIndex];
                int splitLeft = left, splitRight = right;
                Split(values, pivot, ref splitLeft, ref splitRight);
                if (splitRight < elementIndex)
                    left = splitLeft;
                if (elementIndex < splitLeft)
                    right = splitRight;
            }
            return values[elementIndex];
        }

        private static void Split(Span<double> values, double pivot, ref int left, ref int right)
        {
            // Scan in from both ends, swapping out-of-place pairs, until the pointers cross.
            do
            {
                while (values[left] < pivot)
                    left++;
                while (pivot < values[right])
                    right--;

                if (left <= right)
                {
                    (values[left], values[right]) = (values[right], values[left]);
                    left++;
                    right--;
                }
            } while (left <= right);
        }
    }
}
