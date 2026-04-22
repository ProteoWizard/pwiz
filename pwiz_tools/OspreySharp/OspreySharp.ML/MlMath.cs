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

// Machine Learning math utilities for Osprey
// Port of osprey-ml/src/lib.rs utility functions

using System;

namespace pwiz.OspreySharp.ML
{
    /// <summary>
    /// Shared math utilities for the ML module.
    /// </summary>
    public static class MlMath
    {
        /// <summary>
        /// L2 norm of a vector.
        /// </summary>
        public static double Norm(double[] values)
        {
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i] * values[i];
            return Math.Sqrt(sum);
        }

        /// <summary>
        /// Mean of a vector.
        /// </summary>
        public static double Mean(double[] values)
        {
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];
            return sum / values.Length;
        }

        /// <summary>
        /// Population standard deviation of a vector.
        /// </summary>
        public static double Std(double[] values)
        {
            double mean = Mean(values);
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
            {
                double diff = values[i] - mean;
                sum += diff * diff;
            }
            return Math.Sqrt(sum / values.Length);
        }

        /// <summary>
        /// Check if two arrays are element-wise close within epsilon.
        /// </summary>
        public static bool AllClose(double[] lhs, double[] rhs, double eps)
        {
            if (lhs.Length != rhs.Length)
                return false;
            for (int i = 0; i < lhs.Length; i++)
            {
                if (Math.Abs(lhs[i] - rhs[i]) > eps)
                    return false;
            }
            return true;
        }
    }
}
