/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Collections.Generic;
using System.Linq;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Splits items into batches of one peptide length, in ascending length order and input
    /// order within a length, the way peptdeep groups by <c>nAA</c> before slicing batches.
    /// </summary>
    internal static class LengthBatches
    {
        /// <summary>
        /// Index lists into <paramref name="items"/>, each at most <paramref name="batchSize"/>
        /// long and holding items of a single length.
        /// </summary>
        public static IEnumerable<int[]> Split<T>(IReadOnlyList<T> items, Func<T, int> getLength, int batchSize)
        {
            if (batchSize < 1)
                throw new ArgumentOutOfRangeException(nameof(batchSize));
            var byLength = new SortedDictionary<int, List<int>>();
            for (int i = 0; i < items.Count; i++)
            {
                int length = getLength(items[i]);
                if (!byLength.TryGetValue(length, out var indices))
                {
                    indices = new List<int>();
                    byLength.Add(length, indices);
                }
                indices.Add(i);
            }
            foreach (var group in byLength.Values)
            {
                for (int start = 0; start < group.Count; start += batchSize)
                    yield return group.Skip(start).Take(batchSize).ToArray();
            }
        }
    }
}
