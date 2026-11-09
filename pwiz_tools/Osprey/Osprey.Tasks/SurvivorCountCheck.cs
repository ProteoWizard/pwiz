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

using System.Collections.Generic;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Holds every run's Stage 4 survivor load to two invariants: a run reloaded gives the count
    /// it gave the first time, and once every run has loaded, the counts sum to the survivor
    /// count the compaction line reported (<see cref="Expected"/>). One int per run.
    ///
    /// <para>Nothing materializes every run's survivors at once any more, so this is where "the
    /// reported compaction boundary is what the rebuild produces" is checked (#4544). Thread-safe:
    /// <see cref="FirstPassSurvivorLoader"/> loads runs from inside parallel file loops.</para>
    /// </summary>
    internal sealed class SurvivorCountCheck
    {
        private readonly int _runCount;
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private long _total;

        /// <param name="runCount">How many distinct runs the loader can load; the sum is
        /// compared once that many have loaded.</param>
        internal SurvivorCountCheck(int runCount)
        {
            _runCount = runCount;
        }

        /// <summary>
        /// The compaction's reported survivor count, or null when there is nothing to check the
        /// sum against.
        /// </summary>
        internal long? Expected { get; set; }

        /// <summary>
        /// Record one run's Stage 4 load. Returns the violation, or null. Returned rather than
        /// thrown, because the callers run inside parallel file loops, where a thrown message is
        /// lost. The text names an internal invariant, not an operator action, so it is not a
        /// resource string.
        /// </summary>
        internal string Check(string runName, int count)
        {
            lock (_counts)
            {
                if (_counts.TryGetValue(runName, out int firstCount))
                {
                    if (count == firstCount)
                        return null;
                    return string.Format(
                        @"Reloading the kept candidates of {0} produced {1} survivors where the " +
                        @"first load produced {2}.", runName, count, firstCount);
                }
                _counts.Add(runName, count);
                _total += count;
                if (Expected.HasValue && _counts.Count == _runCount && _total != Expected.Value)
                {
                    return string.Format(
                        @"First-pass compaction counted {0} survivors from the passing base_id set " +
                        @"but loading every file produced {1}. The reported compaction boundary " +
                        @"does not describe what re-scoring reads.",
                        Expected.Value, _total);
                }
                return null;
            }
        }
    }
}
