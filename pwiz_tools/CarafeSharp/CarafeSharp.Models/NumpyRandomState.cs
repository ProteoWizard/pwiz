/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on numpy's legacy RandomState (mt19937 and random_interval, BSD-3-Clause) and
 *   pandas DataFrame.sample (BSD-3-Clause), as Carafe's ai.py and models.py use them
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

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// numpy's legacy <c>RandomState</c> (the Mersenne Twister behind <c>np.random.seed</c>,
    /// <c>np.random.permutation</c> and pandas' <c>DataFrame.sample</c>), reproduced draw for
    /// draw so CarafeSharp selects exactly the training and test PSMs Carafe does.
    /// </summary>
    public sealed class NumpyRandomState
    {
        private const int N = 624;
        private const int M = 397;
        private const uint MATRIX_A = 0x9908b0dfU;
        private const uint UPPER_MASK = 0x80000000U;
        private const uint LOWER_MASK = 0x7fffffffU;

        private readonly uint[] _mt = new uint[N];
        private int _pos;

        /// <summary>
        /// <c>np.random.RandomState(seed)</c> / <c>np.random.seed(seed)</c> for an integer seed:
        /// numpy's <c>mt19937_seed</c>.
        /// </summary>
        public NumpyRandomState(uint seed)
        {
            for (int pos = 0; pos < N; pos++)
            {
                _mt[pos] = seed;
                seed = unchecked(1812433253U * (seed ^ (seed >> 30)) + (uint)pos + 1);
            }
            _pos = N;
        }

        /// <summary>The next 32-bit output of the generator.</summary>
        public uint NextUInt32()
        {
            if (_pos == N)
                Generate();
            uint y = _mt[_pos++];
            y ^= y >> 11;
            y ^= (y << 7) & 0x9d2c5680U;
            y ^= (y << 15) & 0xefc60000U;
            y ^= y >> 18;
            return y;
        }

        /// <summary>
        /// numpy's <c>random_interval(max)</c>: a uniform integer in [0, max] by masked rejection.
        /// </summary>
        public long RandomInterval(long max)
        {
            if (max == 0)
                return 0;
            ulong mask = (ulong)max;
            mask |= mask >> 1;
            mask |= mask >> 2;
            mask |= mask >> 4;
            mask |= mask >> 8;
            mask |= mask >> 16;
            mask |= mask >> 32;
            if ((ulong)max <= 0xffffffffUL)
            {
                ulong value;
                while ((value = NextUInt32() & mask) > (ulong)max)
                {
                }
                return (long)value;
            }
            ulong wide;
            while ((wide = NextUInt64() & mask) > (ulong)max)
            {
            }
            return (long)wide;
        }

        /// <summary>
        /// <c>RandomState.permutation(n)</c>: <c>arange(n)</c> shuffled by the legacy Fisher-Yates
        /// loop (<c>for i in reversed(range(1, n)): j = random_interval(i)</c>).
        /// </summary>
        public int[] Permutation(int n)
        {
            var result = new int[n];
            for (int i = 0; i < n; i++)
                result[i] = i;
            for (int i = n - 1; i >= 1; i--)
            {
                int j = (int)RandomInterval(i);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }

        /// <summary>
        /// <c>RandomState.choice(n, size, replace=False)</c> with no weights:
        /// <c>permutation(n)[:size]</c>. This is also what pandas' <c>DataFrame.sample(size)</c>
        /// draws, returning rows in the drawn order.
        /// </summary>
        public int[] ChooseWithoutReplacement(int n, int size)
        {
            if (size < 0 || size > n)
                throw new ArgumentOutOfRangeException(nameof(size));
            var permutation = Permutation(n);
            var result = new int[size];
            Array.Copy(permutation, result, size);
            return result;
        }

        private ulong NextUInt64()
        {
            ulong upper = NextUInt32();
            return (upper << 32) | NextUInt32();
        }

        private void Generate()
        {
            int kk;
            uint y;
            for (kk = 0; kk < N - M; kk++)
            {
                y = (_mt[kk] & UPPER_MASK) | (_mt[kk + 1] & LOWER_MASK);
                _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ ((y & 1) != 0 ? MATRIX_A : 0);
            }
            for (; kk < N - 1; kk++)
            {
                y = (_mt[kk] & UPPER_MASK) | (_mt[kk + 1] & LOWER_MASK);
                _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ ((y & 1) != 0 ? MATRIX_A : 0);
            }
            y = (_mt[N - 1] & UPPER_MASK) | (_mt[0] & LOWER_MASK);
            _mt[N - 1] = _mt[M - 1] ^ (y >> 1) ^ ((y & 1) != 0 ? MATRIX_A : 0);
            _pos = 0;
        }
    }
}
