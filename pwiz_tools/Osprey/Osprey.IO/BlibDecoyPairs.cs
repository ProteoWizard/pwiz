/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Data.SQLite;
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>One row of a blib's <c>DecoyPairs</c> table: what the library says about a spectrum's side and pair.</summary>
    public readonly struct BlibDecoyPair
    {
        public BlibDecoyPair(bool isDecoy, bool isEntrapment, int pairId)
        {
            IsDecoy = isDecoy;
            IsEntrapment = isEntrapment;
            PairId = pairId;
        }

        public bool IsDecoy { get; }
        /// <summary>True for an entrapment target or entrapment decoy (FDRBench's p_target / p_decoy).</summary>
        public bool IsEntrapment { get; }
        /// <summary>Shared by exactly two rows: a target and its decoy, at one charge.</summary>
        public int PairId { get; }
    }

    /// <summary>
    /// The <c>DecoyPairs</c> table of a blib that supplies its own decoys (CarafeSharp writes
    /// it, <c>BlibSchema.CreateDecoyPairs</c> defines it): which spectra are decoys, which are
    /// entrapment, and which target each decoy was made from. A blib searched with its own
    /// decoys must carry the table, and Osprey pairs the decoys from it alone - no manifest, no
    /// composition fallback, both of which stay for TSV libraries. Read by RefSpectra id, which
    /// is a <see cref="LibraryEntry.Id"/> only until the deduplicator renumbers the library, so
    /// the rows are carried by entry object (<see cref="ByEntry"/>) into the pairing that runs
    /// after it.
    /// </summary>
    public static class BlibDecoyPairs
    {
        public const string TABLE = @"DecoyPairs";

        /// <summary>
        /// The table's rows by RefSpectra id, or null when the library has no such table.
        /// </summary>
        public static Dictionary<uint, BlibDecoyPair> Read(string path)
        {
            // A ';' in the path must not end the connection string early: the builder quotes it.
            var connectionString = new SQLiteConnectionStringBuilder { DataSource = path, Version = 3, ReadOnly = true };
            using (var connection = new SQLiteConnection(connectionString.ToString()))
            {
                connection.Open();
                using (var exists = new SQLiteCommand(@"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name", connection))
                {
                    exists.Parameters.AddWithValue(@"@name", TABLE);
                    if ((long)(exists.ExecuteScalar() ?? 0L) == 0)
                        return null;
                }
                var rows = new Dictionary<uint, BlibDecoyPair>();
                using (var command = new SQLiteCommand(@"SELECT RefSpectraID, IsDecoy, IsEntrapment, PairID FROM DecoyPairs", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows[(uint)reader.GetInt64(0)] = new BlibDecoyPair(reader.GetInt64(1) != 0, reader.GetInt64(2) != 0,
                            (int)reader.GetInt64(3));
                    }
                }
                return rows;
            }
        }

        /// <summary>
        /// The rows of <paramref name="table"/> keyed by the entry they describe, taken while
        /// <see cref="LibraryEntry.Id"/> is still the RefSpectra id. The entries survive
        /// deduplication as the same objects, so the map still addresses them afterwards.
        /// </summary>
        public static IReadOnlyDictionary<LibraryEntry, BlibDecoyPair> ByEntry(IEnumerable<LibraryEntry> entries,
            IReadOnlyDictionary<uint, BlibDecoyPair> table)
        {
            var byEntry = new Dictionary<LibraryEntry, BlibDecoyPair>(ReferenceEqualityComparer.Instance);
            foreach (var entry in entries)
            {
                if (table.TryGetValue(entry.Id, out var pair))
                    byEntry.Add(entry, pair);
            }
            return byEntry;
        }

        /// <summary>
        /// Marks and pairs <paramref name="library"/> from its table: a row's side is
        /// authoritative over the accession prefixes (<paramref name="newlyMarked"/> counts the
        /// entries it changed), and each pair whose target and decoy are both in the library at
        /// one charge gives the decoy its target's id with the decoy bit, as the manifest and
        /// composition pairings do. Returns the pairs made. A pair with one side missing (the
        /// deduplicator dropped it, or the writer left it out) pairs nothing, and counts as an
        /// unpaired decoy or target in the summary.
        /// </summary>
        public static int Pair(IList<LibraryEntry> library, IReadOnlyDictionary<LibraryEntry, BlibDecoyPair> pairs,
            PairingState state, out int newlyMarked)
        {
            newlyMarked = 0;
            var sides = new Dictionary<int, (int Target, int Decoy)>();
            for (int idx = 0; idx < library.Count; idx++)
            {
                var entry = library[idx];
                if (!pairs.TryGetValue(entry, out var pair))
                    continue;
                if (entry.IsDecoy != pair.IsDecoy)
                {
                    entry.IsDecoy = pair.IsDecoy;
                    newlyMarked++;
                }
                sides.TryGetValue(pair.PairId, out var side);
                if (!sides.ContainsKey(pair.PairId))
                    side = (-1, -1);
                // The first row of each side wins; a second is a malformed pair and stays unpaired.
                if (pair.IsDecoy && side.Decoy < 0)
                    side.Decoy = idx;
                else if (!pair.IsDecoy && side.Target < 0)
                    side.Target = idx;
                sides[pair.PairId] = side;
            }
            int paired = 0;
            // Pair-id order, so the ids a library gets do not depend on dictionary order.
            foreach (int pairId in sides.Keys.OrderBy(id => id))
            {
                var side = sides[pairId];
                if (side.Target < 0 || side.Decoy < 0)
                    continue;
                var target = library[side.Target];
                var decoy = library[side.Decoy];
                if (target.Charge != decoy.Charge || state.ClaimedTargets.Contains(side.Target) || state.PairedDecoys.Contains(side.Decoy))
                    continue;
                decoy.Id = target.Id | LibraryEntry.DECOY_ID_BIT;
                state.ClaimedTargets.Add(side.Target);
                state.PairedDecoys.Add(side.Decoy);
                paired++;
            }
            return paired;
        }
    }
}
