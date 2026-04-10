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
