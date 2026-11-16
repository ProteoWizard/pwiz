/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Turns "these two objects are not equal" into a statement of WHICH member differs, for
    /// assertion messages.
    ///
    /// <para>Prefers the type's own <see cref="IExplainDiff"/>, which is declared next to the
    /// Equals it explains and so can reach private fields. Falls back to a best-effort walk of
    /// public properties for types that have not adopted the interface, so an unfamiliar failure
    /// still says something useful without anyone having to add code first.</para>
    /// </summary>
    public static class EqualityExplainer
    {
        /// <summary>
        /// Longer than this and the reader is scrolling rather than diagnosing.
        /// </summary>
        private const int MAX_DIFFERENCES = 10;

        /// <summary>
        /// Returns a description of how <paramref name="expected"/> and <paramref name="actual"/>
        /// differ, or null when they are equal. Safe to call on anything: it never throws for
        /// reasons intrinsic to the values being compared.
        /// </summary>
        public static string Explain(object expected, object actual)
        {
            if (Equals(expected, actual))
                return null;
            if (expected == null || actual == null)
                return string.Format(@"{0} vs {1}", Describe(expected), Describe(actual));

            var explained = expected is IExplainDiff explainer ? Safely(() => explainer.ExplainDiff(actual)) : null;
            if (!string.IsNullOrEmpty(explained))
                return explained;

            // Collections are the common case for document comparisons - Results<T> holds
            // ChromInfoLists which hold the chrom infos that know how to explain themselves - so
            // recurse to reach the element that actually differs rather than reporting that two
            // lists are not equal.
            var elements = ExplainByElements(expected, actual);
            if (!string.IsNullOrEmpty(elements))
                return elements;

            var reflected = ExplainByPublicProperties(expected, actual);
            if (!string.IsNullOrEmpty(reflected))
                return reflected;

            // Equals said unequal and nothing could say why. For an IExplainDiff type that means
            // the implementation has drifted from Equals - worth saying out loud, because the
            // alternative is a message that silently claims there is nothing to see.
            return string.Format(
                expected is IExplainDiff
                    ? @"{0} and {1} are not equal, but ExplainDiff found no difference - it may be out of date with Equals"
                    : @"{0} and {1} are not equal; no public property differs, so the difference is in state this type does not expose. Implement IExplainDiff on it to say which member.",
                Describe(expected), Describe(actual));
        }

        /// <summary>
        /// Fails with <see cref="AssertEx.Fail(string)"/> when the two differ, leading with
        /// <paramref name="context"/> so the reader knows what was being compared.
        /// </summary>
        public static void AssertEqual(object expected, object actual, string context)
        {
            var why = Explain(expected, actual);
            if (why != null)
                AssertEx.Fail(TextUtil.LineSeparate(context, string.Empty, why));
        }

        private static string ExplainByElements(object expected, object actual)
        {
            if (expected is string || !(expected is System.Collections.IEnumerable left) ||
                !(actual is System.Collections.IEnumerable right))
                return null;

            var mine = left.Cast<object>().ToList();
            var theirs = right.Cast<object>().ToList();
            if (mine.Count != theirs.Count)
                return string.Format(@"count {0} vs {1}", mine.Count, theirs.Count);

            var differences = new List<string>();
            for (int i = 0; i < mine.Count && differences.Count < MAX_DIFFERENCES; i++)
            {
                var why = Explain(mine[i], theirs[i]);
                if (why != null)
                    differences.Add(string.Format(@"[{0}] {1}", i, why.Replace(Environment.NewLine, @"; ")));
            }
            return differences.Count == 0 ? null : TextUtil.LineSeparate(differences);
        }

        private static string ExplainByPublicProperties(object expected, object actual)
        {
            if (expected.GetType() != actual.GetType())
                return string.Format(@"types differ: {0} vs {1}", expected.GetType().Name, actual.GetType().Name);

            var differences = new List<string>();
            foreach (var property in expected.GetType().GetProperties())
            {
                if (differences.Count >= MAX_DIFFERENCES || property.GetIndexParameters().Length != 0 || !property.CanRead)
                    continue;
                // Getters here can assert on internal state - PeptideLibraries has several that
                // Assume.IsTrue(IsLoaded) - and this runs precisely when state is already suspect,
                // so a throwing property is skipped rather than allowed to mask the real failure.
                var mine = Safely(() => property.GetValue(expected));
                var theirs = Safely(() => property.GetValue(actual));
                if (mine is Exception || theirs is Exception || Equals(mine, theirs))
                    continue;
                differences.Add(string.Format(@"{0} {1} vs {2}", property.Name, Describe(mine), Describe(theirs)));
            }
            return differences.Count == 0 ? null : TextUtil.LineSeparate(differences);
        }

        private static string Describe(object value)
        {
            if (value == null)
                return @"(null)";
            var text = value.ToString();
            // A type whose ToString is just its type name tells the reader nothing, which is the
            // whole problem this class exists to solve. Say so rather than print it twice.
            return text == value.GetType().ToString() ? string.Format(@"<{0}>", value.GetType().Name) : text;
        }

        private static T Safely<T>(Func<T> func) where T : class
        {
            try
            {
                return func();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object Safely(Func<object> func)
        {
            try
            {
                return func();
            }
            catch (Exception x)
            {
                return x;
            }
        }
    }
}
