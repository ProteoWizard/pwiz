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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>Summary statistics shared by the training, alignment and metrics code.</summary>
    public static class Statistics
    {
        /// <summary>
        /// The median of <paramref name="values"/>, the mean of the two middle ones for an even count, as numpy's
        /// <c>median</c> takes it; NaN for none. The values are copied, not reordered; callers that may hold NaN filter it
        /// first.
        /// </summary>
        public static double Median(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
                return double.NaN;
            var sorted = new double[values.Count];
            for (int i = 0; i < sorted.Length; i++)
                sorted[i] = values[i];
            Array.Sort(sorted);
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }
    }
}
