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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// The assertions the feature-finding tests ported from skyline-glimpse (xUnit) need that MSTest does not
    /// have in the same form. Each keeps xUnit's meaning, so the ported expected values test what they tested.
    /// </summary>
    internal static class FeatureAssert
    {
        /// <summary>
        /// xUnit's <c>Assert.Equal(double, double, int precision)</c>: both values rounded to
        /// <paramref name="digits"/> decimal places must be equal.
        /// </summary>
        public static void AreEqualRounded(double expected, double actual, int digits)
        {
            var expectedRounded = Math.Round(expected, digits);
            var actualRounded = Math.Round(actual, digits);
            Assert.IsTrue(Equals(expectedRounded, actualRounded),
                string.Format(@"Expected {0:R} (rounded {1:R}), actual {2:R} (rounded {3:R}) to {4} digits.",
                    expected, expectedRounded, actual, actualRounded, digits));
        }

        /// <summary>The same elements in the same order (xUnit's <c>Assert.Equal</c> on two sequences).</summary>
        public static void AreSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            CollectionAssert.AreEqual(expected.ToArray(), actual.ToArray());
        }

        /// <summary>The same arrays, element by element, in the same order (xUnit compares nested sequences deeply).</summary>
        public static void AreJaggedEqual<T>(IEnumerable<T[]> expected, IEnumerable<T[]> actual)
        {
            var e = expected.ToArray();
            var a = actual.ToArray();
            Assert.AreEqual(e.Length, a.Length);
            for (var i = 0; i < e.Length; i++)
                CollectionAssert.AreEqual(e[i], a[i], @"Sequence " + i);
        }

        /// <summary>xUnit's <c>Assert.Single</c>: exactly one element, which is returned.</summary>
        public static T Single<T>(IEnumerable<T> items)
        {
            var all = items.ToArray();
            Assert.AreEqual(1, all.Length);
            return all[0];
        }

        /// <summary>xUnit's <c>Assert.Empty</c>.</summary>
        public static void IsEmpty<T>(IEnumerable<T> items)
        {
            Assert.AreEqual(0, items.Count());
        }

        /// <summary>xUnit's <c>Assert.InRange</c>: <paramref name="low"/> &lt;= actual &lt;= <paramref name="high"/>.</summary>
        public static void InRange(double actual, double low, double high)
        {
            Assert.IsTrue(actual >= low && actual <= high, string.Format(@"{0:R} is not in [{1:R}, {2:R}].", actual, low, high));
        }
    }
}
