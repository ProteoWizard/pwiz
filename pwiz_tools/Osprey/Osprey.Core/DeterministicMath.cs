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

// Exp and Log are ports of fdlibm 5.3 e_exp.c and e_log.c, which carry this notice:
//
//   Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.
//
//   Developed at SunSoft, a Sun Microsystems, Inc. business.
//   Permission to use, copy, modify, and distribute this
//   software is freely granted, provided that this notice
//   is preserved.

using System;
using System.Runtime.CompilerServices;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Transcendental functions that return the same bits on every platform, framework and
    /// thread count.
    ///
    /// <para><see cref="Math.Exp"/> and <see cref="Math.Log(double)"/> are not computed by .NET: the
    /// runtime calls the operating system's C library (the Universal CRT on Windows, glibc on
    /// Linux), and those disagree in the last bit for a few inputs in every thousand. A model
    /// trained through them is reproducible on one OS but not across OSes. The functions here
    /// use only IEEE 754 addition, subtraction, multiplication and division (each correctly
    /// rounded, so identical everywhere) plus integer manipulation of the bit pattern, so a
    /// result can differ between machines only if the arithmetic itself does.</para>
    ///
    /// <para>The algorithms are fdlibm's (Sun Microsystems, 1993; notice at the top of this
    /// file), the same ones Java's StrictMath specifies for bit-exact results. fdlibm bounds
    /// the error of both below 1 ulp; measured against a correctly rounded reference, almost
    /// every result is correctly rounded and the rest are off by one ulp.</para>
    ///
    /// <para>Expression contraction: a fused multiply-add rounds once where the source rounds
    /// twice, so a compiler that fused <c>a * b + c</c> on its own would change bits. The .NET
    /// JIT does not: it emits an FMA only for an explicit <c>Math.FusedMultiplyAdd</c> or FMA
    /// intrinsic call, on every architecture it supports. That is a property of the JIT rather
    /// than of this source, so the test pins a hash of the result bits over a large input
    /// grid, and a JIT or platform that contracted would fail it.</para>
    ///
    /// <para>These are not faster than <see cref="Math"/> and are not meant to replace it
    /// generally; use them where a result feeds an iterative fit whose output must be
    /// exactly reproducible.</para>
    ///
    /// <para>OSPREY_DETERMINISTIC_MATH=0 (<see cref="OspreyEnvironment.UseDeterministicMath"/>)
    /// sends <see cref="Exp(double)"/> and <see cref="Log(double)"/> to <see cref="Math"/>
    /// instead, so a run can be compared against the platform functions it replaced. The
    /// environment is read once into a static and cannot be flipped at run time, so the arm
    /// can also be passed explicitly (<see cref="Exp(double, bool)"/>,
    /// <see cref="Log(double, bool)"/>) - a test would otherwise be able to assert only
    /// whichever arm its process happens to be running under.</para>
    /// </summary>
    public static class DeterministicMath
    {
        // e_exp.c constants. The high halves of ln2 have trailing zero bits, so k * LN2_HI
        // is exact for every k the reduction can produce.
        private const double LN2_HI = 6.93147180369123816490e-01;     // 0x3FE62E42FEE00000
        private const double LN2_LO = 1.90821492927058770002e-10;     // 0x3DEA39EF35793C76
        private const double INV_LN2 = 1.44269504088896338700e+00;    // 0x3FF71547652B82FE
        private const double EXP_OVERFLOW = 7.09782712893383973096e+02;   // 0x40862E42FEFA39EF
        private const double EXP_UNDERFLOW = -7.45133219101941108420e+02; // 0xC0874910D52D3051
        private const double TWO_M1000 = 9.33263618503218878990e-302; // 2^-1000
        private const double P1 = 1.66666666666666019037e-01;
        private const double P2 = -2.77777777770155933842e-03;
        private const double P3 = 6.61375632143793436117e-05;
        private const double P4 = -1.65339022054652515390e-06;
        private const double P5 = 4.13813679705723846039e-08;

        // e_log.c constants.
        private const double TWO_54 = 1.80143985094819840000e+16;     // 2^54
        private const double LG1 = 6.666666666666735130e-01;
        private const double LG2 = 3.999999999940941908e-01;
        private const double LG3 = 2.857142874366239149e-01;
        private const double LG4 = 2.222219843214978396e-01;
        private const double LG5 = 1.818357216161805012e-01;
        private const double LG6 = 1.531383769920937332e-01;
        private const double LG7 = 1.479819860511658591e-01;

        /// <summary>
        /// e raised to <paramref name="x"/>, from the arm
        /// <see cref="OspreyEnvironment.UseDeterministicMath"/> selects.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exp(double x)
        {
            return Exp(x, OspreyEnvironment.UseDeterministicMath);
        }

        /// <summary>
        /// e raised to <paramref name="x"/>: the fdlibm arm when <paramref name="deterministic"/>
        /// is true, otherwise <see cref="Math.Exp"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exp(double x, bool deterministic)
        {
            return deterministic ? ExpFdlibm(x) : Math.Exp(x);
        }

        /// <summary>
        /// Natural logarithm of <paramref name="x"/>, from the arm
        /// <see cref="OspreyEnvironment.UseDeterministicMath"/> selects.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log(double x)
        {
            return Log(x, OspreyEnvironment.UseDeterministicMath);
        }

        /// <summary>
        /// Natural logarithm of <paramref name="x"/>: the fdlibm arm when
        /// <paramref name="deterministic"/> is true, otherwise <see cref="Math.Log(double)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log(double x, bool deterministic)
        {
            return deterministic ? LogFdlibm(x) : Math.Log(x);
        }

        /// <summary>
        /// The fdlibm e_exp.c arm: error below 1 ulp, and the same special values as
        /// <see cref="Math.Exp"/>: NaN for NaN, +inf for +inf and for overflow
        /// (x &gt; 709.78), 0 for -inf and for underflow (x &lt; -745.13), and subnormal
        /// results in between.
        /// </summary>
        private static double ExpFdlibm(double x)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            int hx = (int)(bits >> 32);
            int negative = (hx >> 31) & 1;
            hx &= 0x7fffffff;

            // Non-finite, overflow and underflow: |x| >= 709.78.
            if (hx >= 0x40862E42)
            {
                if (hx >= 0x7ff00000)
                {
                    if (double.IsNaN(x))
                        return x;
                    return negative == 0 ? x : 0.0;
                }
                if (x > EXP_OVERFLOW)
                    return double.PositiveInfinity;
                if (x < EXP_UNDERFLOW)
                    return 0.0;
            }

            // Argument reduction: x = k * ln2 + r with |r| <= 0.5 * ln2, r carried as hi - lo.
            double hi = 0, lo = 0;
            int k = 0;
            if (hx > 0x3fd62e42)
            {
                // |x| > 0.5 * ln2
                if (hx < 0x3FF0A2B2)
                {
                    // and |x| < 1.5 * ln2
                    hi = negative == 0 ? x - LN2_HI : x + LN2_HI;
                    lo = negative == 0 ? LN2_LO : -LN2_LO;
                    k = 1 - negative - negative;
                }
                else
                {
                    k = (int)(INV_LN2 * x + (negative == 0 ? 0.5 : -0.5));
                    double t = k;
                    hi = x - t * LN2_HI;
                    lo = t * LN2_LO;
                }
                x = hi - lo;
            }
            else if (hx < 0x3e300000)
            {
                // |x| < 2^-28, where 1 + x is already the correctly rounded result.
                return 1.0 + x;
            }

            // x is now in the primary range.
            double z = x * x;
            double c = x - z * (P1 + z * (P2 + z * (P3 + z * (P4 + z * P5))));
            if (k == 0)
                return 1.0 - ((x * c) / (c - 2.0) - x);
            double y = 1.0 - ((lo - (x * c) / (2.0 - c)) - hi);

            // Scale by 2^k by adding k to the exponent field. Below -1021 the scaled value
            // is subnormal, so scale by 2^(k+1000) first and let one multiply round it.
            long yBits = BitConverter.DoubleToInt64Bits(y);
            if (k >= -1021)
                return BitConverter.Int64BitsToDouble(yBits + ((long)k << 52));
            return BitConverter.Int64BitsToDouble(yBits + ((long)(k + 1000) << 52)) * TWO_M1000;
        }

        /// <summary>
        /// The fdlibm e_log.c arm: error below 1 ulp, and the same special values as
        /// <see cref="Math.Log(double)"/>: -inf for +/-0, NaN for negative numbers, -inf and
        /// NaN, +inf for +inf, and full accuracy for subnormal inputs.
        /// </summary>
        private static double LogFdlibm(double x)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            int hx = (int)(bits >> 32);
            uint lx = (uint)bits;

            int k = 0;
            if (hx < 0x00100000)
            {
                // x < 2^-1022, including every negative x.
                if (((hx & 0x7fffffff) | (int)lx) == 0)
                    return double.NegativeInfinity;
                if (hx < 0)
                    return double.NaN;

                // Subnormal: scale into the normal range.
                k -= 54;
                x *= TWO_54;
                bits = BitConverter.DoubleToInt64Bits(x);
                hx = (int)(bits >> 32);
                lx = (uint)bits;
            }
            if (hx >= 0x7ff00000)
                return x + x;

            // x = 2^k * m with m in [sqrt(2)/2, sqrt(2)), and f = m - 1.
            k += (hx >> 20) - 1023;
            hx &= 0x000fffff;
            int i = (hx + 0x95f64) & 0x100000;
            x = BitConverter.Int64BitsToDouble(((long)(hx | (i ^ 0x3ff00000)) << 32) | lx);
            k += i >> 20;
            double f = x - 1.0;
            double dk;

            if ((0x000fffff & (2 + hx)) < 3)
            {
                // |f| < 2^-20
                if (f == 0)
                {
                    if (k == 0)
                        return 0.0;
                    dk = k;
                    return dk * LN2_HI + dk * LN2_LO;
                }
                double r = f * f * (0.5 - 0.33333333333333333 * f);
                if (k == 0)
                    return f - r;
                dk = k;
                return dk * LN2_HI - ((r - dk * LN2_LO) - f);
            }

            double s = f / (2.0 + f);
            dk = k;
            double z = s * s;
            int ii = hx - 0x6147a;
            double w = z * z;
            int jj = 0x6b851 - hx;
            double t1 = w * (LG2 + w * (LG4 + w * LG6));
            double t2 = z * (LG1 + w * (LG3 + w * (LG5 + w * LG7)));
            ii |= jj;
            double rr = t2 + t1;
            if (ii > 0)
            {
                double hfsq = 0.5 * f * f;
                if (k == 0)
                    return f - (hfsq - s * (hfsq + rr));
                return dk * LN2_HI - ((hfsq - (s * (hfsq + rr) + dk * LN2_LO)) - f);
            }
            if (k == 0)
                return f - s * (f - rr);
            return dk * LN2_HI - ((s * (f - rr) - dk * LN2_LO) - f);
        }
    }
}
