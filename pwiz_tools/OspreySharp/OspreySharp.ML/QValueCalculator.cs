/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
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

// Q-value calculation using target-decoy competition
//
// Originally from Sage (https://github.com/lazear/sage)
// Copyright (c) 2022 Michael Lazear
// Licensed under the MIT License

using System;

namespace pwiz.OspreySharp.ML
{
    /// <summary>
    /// Q-value calculator using target-decoy competition.
    /// Port of qvalue.rs.
    ///
    /// Input must be sorted in descending order by score (best matches first).
    /// </summary>
    public static class QValueCalculator
    {
        /// <summary>
        /// Calculate q-values for a sorted list of target and decoy matches.
        /// </summary>
        /// <param name="isDecoy">Boolean array where true indicates a decoy match.
        /// Must be sorted by score descending.</param>
        /// <param name="qValues">Output array to store calculated q-values (same length as isDecoy).</param>
        /// <returns>Number of matches passing 1% FDR threshold.</returns>
        public static int ComputeQValues(bool[] isDecoy, double[] qValues)
        {
            if (isDecoy.Length != qValues.Length)
                throw new ArgumentException("isDecoy and qValues must have same length");

            int decoy = 0;
            int target = 0;

            // Forward pass: calculate FDR at each position
            for (int i = 0; i < isDecoy.Length; i++)
            {
                if (isDecoy[i])
                    decoy++;
                else
                    target++;
                qValues[i] = (double)decoy / Math.Max(1, target);
            }

            // Reverse pass: calculate cumulative minimum (q-value)
            double qMin = 1.0;
            int passing = 0;
            for (int i = qValues.Length - 1; i >= 0; i--)
            {
                qMin = Math.Min(qMin, qValues[i]);
                qValues[i] = qMin;
                if (qMin <= 0.01)
                    passing++;
            }

            return passing;
        }

        /// <summary>
        /// Convenience overload that allocates and returns the q-value array.
        /// </summary>
        /// <param name="isDecoy">Boolean array where true indicates a decoy match.
        /// Must be sorted by score descending.</param>
        /// <param name="passing">Number of matches passing 1% FDR threshold.</param>
        /// <returns>Computed q-values.</returns>
        public static double[] ComputeQValues(bool[] isDecoy, out int passing)
        {
            var qValues = new double[isDecoy.Length];
            passing = ComputeQValues(isDecoy, qValues);
            return qValues;
        }
    }
}
