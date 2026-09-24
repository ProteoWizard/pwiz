/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/ai.py
 *   (train_ms2, train_rt) and models.py (psm_sampling_with_important_mods, count_mods)
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

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Carafe's split of training data into the rows a model trains on and the rows it is
    /// scored on, reproduced exactly (including pandas' seeded sampling) so a CarafeSharp
    /// fine-tune is judged on the same test set as Carafe's:
    /// <list type="number">
    /// <item><c>n_test = max(1, min(maxTest, ceil(0.1 N) - 10))</c>, <c>n_train = N - n_test</c>.</item>
    /// <item>Training rows: <c>n_train</c> rows drawn with seed 1337, then for each of the 10
    /// most frequent modifications, up to 50 more rows carrying it (also seed 1337, so rows
    /// can repeat).</item>
    /// <item>Test rows: the rows whose sequence never occurs in training, down-sampled to
    /// <c>n_test</c> with seed 1337; all rows when none are left.</item>
    /// </list>
    /// Returned indices are in the order pandas would produce the rows, which decides the
    /// training shuffles.
    /// </summary>
    public static class TrainingSplit
    {
        public const uint SAMPLING_SEED = 1337;
        public const int DEFAULT_MAX_TEST = 1000;
        public const int TOP_N_MODS = 10;
        public const int ROWS_PER_MOD = 50;

        /// <summary>Carafe's <c>n_test</c> for <paramref name="rowCount"/> rows.</summary>
        public static int TestCount(int rowCount, int maxTest = DEFAULT_MAX_TEST)
        {
            return Math.Max(1, Math.Min(maxTest, (int)Math.Ceiling(rowCount * 0.1) - 10));
        }

        /// <summary>
        /// Splits rows described by their sequences and alphabase <c>mods</c> strings.
        /// <paramref name="trainCount"/> is normally <c>rowCount - TestCount(rowCount)</c>; RT
        /// passes counts computed before its rows were collapsed, as Carafe does.
        /// </summary>
        public static (int[] Train, int[] Test) Split(IReadOnlyList<string> sequences, IReadOnlyList<string> mods,
            int trainCount, int testCount)
        {
            int n = sequences.Count;
            var train = new List<int>(SampleRows(Enumerable.Range(0, n).ToArray(), trainCount));
            foreach (string mod in TopModifications(mods))
            {
                var carrying = Enumerable.Range(0, n).Where(i => mods[i].Contains(mod, StringComparison.Ordinal)).ToArray();
                train.AddRange(SampleRows(carrying, ROWS_PER_MOD));
            }

            var trainSequences = new HashSet<string>(train.Select(i => sequences[i]), StringComparer.Ordinal);
            var candidates = Enumerable.Range(0, n).Where(i => !trainSequences.Contains(sequences[i])).ToArray();
            int[] test;
            if (candidates.Length > testCount)
                test = SampleRows(candidates, testCount);
            else if (candidates.Length == 0)
                test = Enumerable.Range(0, n).ToArray();
            else
                test = candidates;
            return (train.ToArray(), test);
        }

        /// <summary>
        /// pandas <c>df.sample(count, random_state=1337)</c> over <paramref name="rows"/>, or every
        /// row when <paramref name="count"/> is not smaller than their number.
        /// </summary>
        private static int[] SampleRows(int[] rows, int count)
        {
            if (count >= rows.Length)
                return rows;
            var random = new NumpyRandomState(SAMPLING_SEED);
            return random.ChooseWithoutReplacement(rows.Length, count).Select(i => rows[i]).ToArray();
        }

        /// <summary>
        /// Carafe's <c>count_mods</c>: modifications by number of rows carrying them, most
        /// frequent first, keeping the <see cref="TOP_N_MODS"/> most frequent. Ties keep the
        /// order of first appearance (Carafe's order there follows Python set iteration, which
        /// is not stable across processes; with fewer than 17 modifications numpy's sort keeps
        /// insertion order, so this matches whenever Carafe's own order is reproducible).
        /// </summary>
        private static IEnumerable<string> TopModifications(IReadOnlyList<string> mods)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (string text in mods)
            {
                if (string.IsNullOrEmpty(text))
                    continue;
                foreach (string mod in text.Split(';').Distinct(StringComparer.Ordinal))
                {
                    if (IsMutation(mod))
                        continue;
                    if (counts.TryGetValue(mod, out int count))
                    {
                        counts[mod] = count + 1;
                    }
                    else
                    {
                        counts.Add(mod, 1);
                        order.Add(mod);
                    }
                }
            }
            return order.Select((mod, index) => (mod, index))
                .OrderByDescending(p => counts[p.mod]).ThenBy(p => p.index)
                .Take(TOP_N_MODS).Select(p => p.mod);
        }

        /// <summary>alphabase's mutation notation, <c>Xaa-&gt;Yyyyy</c> style names.</summary>
        private static bool IsMutation(string mod)
        {
            var parts = mod.Split(new[] { @"->" }, StringSplitOptions.None);
            return parts.Length == 2 && parts[0].Length == 3 && parts[1].Length == 5;
        }
    }
}
