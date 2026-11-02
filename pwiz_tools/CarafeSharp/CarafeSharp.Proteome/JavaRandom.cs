/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on the java.util.Random algorithm that Carafe (https://github.com/maccoss/carafe)
 *   src/main/java/db/EntrapmentFastaGear.java shuffles with
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The 48-bit linear congruential generator of <c>java.util.Random</c>, reproduced bit for
    /// bit so a shuffle seeded the way Carafe seeds it yields the same permutation. Only the
    /// members Carafe's entrapment generation calls are ported.
    /// </summary>
    public sealed class JavaRandom
    {
        private const long MULTIPLIER = 0x5DEECE66DL;
        private const long ADDEND = 0xBL;
        private const long MASK = (1L << 48) - 1;

        private long _seed;

        /// <summary>Same as <c>new java.util.Random(seed)</c>.</summary>
        public JavaRandom(long seed)
        {
            _seed = (seed ^ MULTIPLIER) & MASK;
        }

        /// <summary>
        /// Same as <c>java.util.Random.nextInt(bound)</c>: uniform in [0, bound), with the
        /// power-of-two fast path and the rejection loop that keeps other bounds unbiased.
        /// </summary>
        public int NextInt(int bound)
        {
            if (bound <= 0)
                throw new ArgumentOutOfRangeException(nameof(bound), bound, @"bound must be positive");

            int u = Next(31);
            int m = bound - 1;
            if ((bound & m) == 0)
                return (int)((bound * (long)u) >> 31);

            // Java relies on int overflow here: u - r + m goes negative when u falls in the
            // final, partial copy of [0, bound), and that draw is rejected.
            for (;; u = Next(31))
            {
                int r = u % bound;
                if (unchecked(u - r + m) >= 0)
                    return r;
            }
        }

        /// <summary>Same as the protected <c>java.util.Random.next(bits)</c>.</summary>
        private int Next(int bits)
        {
            _seed = unchecked(_seed * MULTIPLIER + ADDEND) & MASK;
            return unchecked((int)((ulong)_seed >> (48 - bits)));
        }
    }
}
