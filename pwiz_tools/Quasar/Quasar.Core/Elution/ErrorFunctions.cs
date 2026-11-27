/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on datum (https://github.com/maccoss/datum)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// The complementary error function and its scaled form, as the EMG density needs them.
    /// </summary>
    /// <remarks>
    /// Uses the Numerical Recipes <c>erfcc</c> rational approximation (fractional error below
    /// 1.2e-7 everywhere). Its error is relative rather than absolute in the tails, so
    /// <see cref="Erfcx"/> stays accurate for large arguments, where computing
    /// <c>exp(x^2) * erfc(x)</c> directly would multiply an overflow by an underflow.
    /// Ported from datum's <c>MathFunctions</c> with the arithmetic unchanged.
    /// </remarks>
    public static class ErrorFunctions
    {
        /// <summary>Complementary error function, erfc(x) = 1 - erf(x).</summary>
        public static double Erfc(double x)
        {
            double z = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.5 * z);
            double ans = t * Math.Exp(-z * z - 1.26551223 + Poly(t));
            return x >= 0.0 ? ans : 2.0 - ans;
        }

        /// <summary>
        /// Scaled complementary error function, erfcx(x) = exp(x^2) * erfc(x). Finite for large
        /// positive x, where erfc(x) itself underflows.
        /// </summary>
        public static double Erfcx(double x)
        {
            double z = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.5 * z);
            // erfcc folds an exp(-z^2) into its result; leaving it out gives erfcx(z) for z >= 0.
            double erfcxPos = t * Math.Exp(-1.26551223 + Poly(t));
            if (x >= 0.0)
                return erfcxPos;

            // erfcx(-z) = 2 * exp(z^2) - erfcx(z).
            return 2.0 * Math.Exp(x * x) - erfcxPos;
        }

        /// <summary>The Numerical Recipes erfcc polynomial in t.</summary>
        private static double Poly(double t)
        {
            return t * (1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806 +
                t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 +
                t * 0.17087277))))))));
        }
    }
}
