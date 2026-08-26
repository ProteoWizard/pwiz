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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Implement to say WHY this object is not <see cref="object.Equals(object)"/> to another.
    ///
    /// <para>Exists because an assertion that two objects differ is useless when neither overrides
    /// ToString: the failure message renders both sides as the bare type name and carries no
    /// information. That has repeatedly cost a diagnosis - an intermittent failure reproduces once
    /// in hundreds of runs, reports nothing usable, and is thrown away. A type that knows what its
    /// Equals compares can say which member differed, and the difference is then obvious from the
    /// first occurrence.</para>
    ///
    /// <para>Deliberately declared next to the Equals it explains rather than discovered by
    /// reflection over properties. Equals implementations here compare private fields, which do not
    /// map onto public properties - <see cref="object.Equals(object)"/> on PeptideLibraries
    /// compares a _rankIdName with no public accessor at all - so a reflective differ can report
    /// "no difference found" while Equals says otherwise. Some property getters also assert on
    /// internal state, which a blind walk would trip precisely when things are already wrong.</para>
    /// </summary>
    public interface IExplainDiff
    {
        /// <summary>
        /// Returns a human readable account of how this object differs from
        /// <paramref name="other"/>, or null when no difference is found.
        ///
        /// <para>Takes object rather than a type parameter, and does its own type check, for the
        /// same reason <see cref="object.Equals(object)"/> does: callers hold the values loosely.
        /// Only called after Equals has already returned false, so returning null means this
        /// implementation has fallen out of step with Equals - callers report that rather than
        /// silently passing.</para>
        /// </summary>
        string ExplainDiff(object other);
    }
}
