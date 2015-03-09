/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.Common.DataAnalysis
{
    public static class PValues
    {
        /// <summary>
        /// Adjusts the p-values for multiple comparisons using the BH method.
        /// This is equivalent to calling the function in R "p.adjust".
        /// </summary>
        /// <returns>an array of adjusted pValues.  They will be in the same order as the pValues that were passed in</returns>
        public static double[] AdjustPValues(IEnumerable<double> pValues)
        {
            var entries = pValues.Select((pValue, index) => new Tuple<double, int>(pValue, index)).ToArray();
            Array.Sort(entries);
            double[] cumulativeMins = new double[entries.Length];
            double currentMin = 1.0;
            var result = new double[entries.Length];
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                double value = entries[i].Item1*entries.Length/(i + 1);
                currentMin = Math.Min(value, currentMin);
                cumulativeMins[i] = currentMin;
                result[entries[i].Item2] = currentMin;
            }
            return result;
        }
    }
}
