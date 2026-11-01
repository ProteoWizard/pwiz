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

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// exp and log from IEEE double arithmetic alone (addition, multiplication, division and exact scaling by powers of
    /// two), so they give the same bits on every operating system. <see cref="Math.Exp"/> and <see cref="Math.Log(double)"/> call
    /// the operating system's C library, the UCRT on Windows and glibc on Linux, and the two differ in the last bit or two
    /// for about 0.5% of arguments; a demultiplexing solve that iterates to a tolerance turns that into differences of
    /// 1e-5 in its output, and a log taken for a bucket key can put a fragment in a different channel. Accurate to within
    /// a few units in the last place, far below anything a demultiplexing result can show.
    /// </summary>
    public static class PortableMath
    {
        // ln 2 split so that k * LN2_HI is exact for every |k| < 2^11: LN2_HI has its low 21 bits zero.
        private const double LN2_HI = 6.93147180369123816490e-01;
        private const double LN2_LO = 1.90821492927058770002e-10;
        private const double INV_LN2 = 1.44269504088896338700e+00;
        // exp overflows above ln(MaxValue) and is 0 below ln(Epsilon / 2).
        private const double EXP_MAX = 709.782712893383973096;
        private const double EXP_MIN = -745.13321910194110842;
        private const double SQRT2 = 1.41421356237309504880;
        private const double TWO54 = 18014398509481984.0;

        /// <summary>e to the power <paramref name="x"/>.</summary>
        public static double Exp(double x)
        {
            if (double.IsNaN(x))
                return x;
            if (x > EXP_MAX)
                return double.PositiveInfinity;
            if (x < EXP_MIN)
                return 0;
            // x = k ln 2 + r with |r| <= ln 2 / 2, so exp(x) = 2^k exp(r). hi = x - k * LN2_HI is exact, and r = hi - lo
            // is kept as both parts.
            double k = Math.Round(x * INV_LN2);
            double hi = x - k * LN2_HI;
            double lo = k * LN2_LO;
            double r = hi - lo;
            // r coth(r / 2) = 2 + r^2/6 - r^4/360 + ... (Bernoulli numbers), so with c = r - (r coth(r / 2) - 2),
            // exp(r) = 1 + r + r c / (2 - c). Through r^16, whose next term changes exp(r) by under 2^-65 for
            // |r| <= 0.35.
            double z = r * r;
            double t = 7.0 / 261534873600;
            t = -691.0 / 653837184000 + z * t;
            t = 1.0 / 23950080 + z * t;
            t = -1.0 / 604800 + z * t;
            t = 1.0 / 15120 + z * t;
            t = -1.0 / 360 + z * t;
            t = 1.0 / 6 + z * t;
            double c = r - z * t;
            double p = 1 - ((lo - r * c / (2 - c)) - hi);
            return Scale(p, (int)k);
        }

        /// <summary>The natural logarithm of <paramref name="x"/>.</summary>
        public static double Log(double x)
        {
            if (double.IsNaN(x) || x < 0)
                return double.NaN;
            if (x == 0)
                return double.NegativeInfinity;
            if (double.IsPositiveInfinity(x))
                return x;
            // x = 2^e m with m in [sqrt(1/2), sqrt(2)), so log(x) = e ln 2 + log(m).
            int e = 0;
            long bits = BitConverter.DoubleToInt64Bits(x);
            if (bits >> 52 == 0)
            {
                // Subnormal: scale into the normal range first.
                x *= TWO54;
                e = -54;
                bits = BitConverter.DoubleToInt64Bits(x);
            }
            e += (int)(bits >> 52) - 1023;
            double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
            if (m >= SQRT2)
            {
                m *= 0.5;
                e++;
            }
            // log(1 + f) = 2 atanh(s) = 2s + s R, with s = f / (2 + f), |s| <= 0.172, and R = 2s^2/3 + 2s^4/5 + ...;
            // f = m - 1 is exact. Since 2s = f - f^2/2 + s f^2/2, log(1 + f) = f - (f^2/2 - s (f^2/2 + R)), whose
            // rounding errors all fall on the small correction to f.
            double f = m - 1;
            double halfSquare = 0.5 * f * f;
            double s = f / (2 + f);
            double z = s * s;
            double q = 2.0 / 25;
            q = 2.0 / 23 + z * q;
            q = 2.0 / 21 + z * q;
            q = 2.0 / 19 + z * q;
            q = 2.0 / 17 + z * q;
            q = 2.0 / 15 + z * q;
            q = 2.0 / 13 + z * q;
            q = 2.0 / 11 + z * q;
            q = 2.0 / 9 + z * q;
            q = 2.0 / 7 + z * q;
            q = 2.0 / 5 + z * q;
            q = 2.0 / 3 + z * q;
            double rTerm = z * q;
            return e * LN2_HI - ((halfSquare - (s * (halfSquare + rTerm) + e * LN2_LO)) - f);
        }

        /// <summary><paramref name="p"/> times 2^<paramref name="k"/>, exact unless the result is subnormal.</summary>
        private static double Scale(double p, int k)
        {
            if (k > 1023)
                return p * PowerOfTwo(1023) * PowerOfTwo(k - 1023);
            if (k < -1022)
                return p * PowerOfTwo(k + 600) * PowerOfTwo(-600);
            return p * PowerOfTwo(k);
        }

        private static double PowerOfTwo(int k)
        {
            return BitConverter.Int64BitsToDouble((long)(k + 1023) << 52);
        }
    }
}
