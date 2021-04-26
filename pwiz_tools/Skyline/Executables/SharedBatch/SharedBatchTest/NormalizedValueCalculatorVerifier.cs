/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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

namespace SharedBatchTest
{
    public static class NormalizedValueCalculatorVerifier
    {
        

        public static void AssertNumbersSame(double? value1, double? value2)
        {
            if (Equals(value1, value2))
            {
                return;
            }
            AssertEx.AreEqual(value1.HasValue, value2.HasValue);
            if (!value1.HasValue || !value2.HasValue)
            {
                return;
            }

            var delta = Math.Min(Math.Abs(value1.Value), Math.Abs(value2.Value)) / 1e6;
            AssertEx.AreEqual(value1.Value, value2.Value, delta);
        }
    }
}
