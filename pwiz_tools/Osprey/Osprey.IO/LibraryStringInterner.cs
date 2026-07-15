/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Collapses duplicate per-entry strings of a resident spectral library to
    /// a single shared instance. A large library repeats the same protein
    /// accessions, gene names, stripped sequences, and modification names across
    /// millions of entries (one protein maps to many peptides; one stripped
    /// sequence spans many charge/modification states), so the distinct string
    /// count is far smaller than the total. Sharing one instance per distinct
    /// value drops the per-duplicate string object (header + chars) from the
    /// resident set.
    ///
    /// The intern pool is a plain (single-threaded) dictionary built and
    /// released within one load call -- unlike the concurrent per-observation
    /// interning that was a net loss on the FDR path, this runs once over the
    /// library and only the shared instances survive. Values are unchanged
    /// (only object identity), so output stays byte-identical.
    /// </summary>
    public static class LibraryStringInterner
    {
        /// <summary>
        /// Intern the repeated string fields of every entry in place. Logs a
        /// one-line distinct/total summary when <paramref name="logInfo"/> is set.
        /// </summary>
        public static void InternInPlace(IList<LibraryEntry> entries, Action<string> logInfo = null)
        {
            if (entries == null || entries.Count == 0)
                return;

            var pool = new Dictionary<string, string>(StringComparer.Ordinal);
            long totalRefs = 0;

            foreach (var entry in entries)
            {
                if (entry == null)
                    continue;
                entry.Sequence = Intern(pool, entry.Sequence, ref totalRefs);
                entry.ModifiedSequence = Intern(pool, entry.ModifiedSequence, ref totalRefs);
                InternList(pool, entry.ProteinIds, ref totalRefs);
                InternList(pool, entry.GeneNames, ref totalRefs);

                var mods = entry.Modifications;
                if (mods != null)
                {
                    for (int i = 0; i < mods.Count; i++)
                    {
                        var m = mods[i];
                        if (m != null && m.Name != null)
                            m.Name = Intern(pool, m.Name, ref totalRefs);
                    }
                }
            }

            if (logInfo != null)
            {
                long collapsed = totalRefs - pool.Count;
                double pct = totalRefs > 0 ? 100.0 * collapsed / totalRefs : 0.0;
                logInfo(string.Format(
                    "Interned library strings: {0} distinct / {1} total ({2:F1}% collapsed)",
                    pool.Count, totalRefs, pct));
            }
        }

        private static string Intern(Dictionary<string, string> pool, string s, ref long totalRefs)
        {
            if (s == null)
                return null;
            totalRefs++;
            string existing;
            if (pool.TryGetValue(s, out existing))
                return existing;
            pool[s] = s;
            return s;
        }

        private static void InternList(Dictionary<string, string> pool, List<string> list, ref long totalRefs)
        {
            if (list == null)
                return;
            for (int i = 0; i < list.Count; i++)
                list[i] = Intern(pool, list[i], ref totalRefs);
        }
    }
}
