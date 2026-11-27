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

namespace pwiz.Quasar.Test
{
    /// <summary>Counting noise for synthetic traces, reproducible from a seeded <see cref="Random"/>.</summary>
    internal static class SyntheticCounts
    {
        /// <summary>A Poisson draw: Knuth's method for small means, a rounded normal for large ones.</summary>
        public static int Poisson(Random random, double mean)
        {
            if (mean <= 0.0)
                return 0;
            if (mean > 30.0)
            {
                double u1 = 1.0 - random.NextDouble();
                double u2 = random.NextDouble();
                double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                return Math.Max(0, (int)Math.Round(mean + Math.Sqrt(mean) * normal));
            }

            double limit = Math.Exp(-mean);
            double product = random.NextDouble();
            int count = 0;
            while (product > limit)
            {
                product *= random.NextDouble();
                count++;
            }

            return count;
        }
    }
}
