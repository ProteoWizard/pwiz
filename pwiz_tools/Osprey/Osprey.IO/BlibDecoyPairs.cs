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

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using pwiz.BiblioSpec.IO;
using pwiz.CarafeSharp.Contracts;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// The <c>DecoyPairs</c> table of a blib that supplies its own decoys (CarafeSharp writes
    /// it, <c>BlibSchema.CreateDecoyPairs</c> defines it, <see cref="BlibReader.ReadDecoyPairs"/>
    /// reads it): which spectra are decoys, which are entrapment, and which target each decoy
    /// was made from. A blib searched with its own decoys must carry the table, and Osprey pairs
    /// the decoys from it alone - no manifest, no composition fallback, both of which stay for
    /// TSV libraries. Read by RefSpectra id, which is a <see cref="LibraryEntry.Id"/> only until
    /// the deduplicator renumbers the library, so the rows are carried by entry object
    /// (<see cref="ByEntry"/>) into the pairing that runs after it.
    /// </summary>
    public static class BlibDecoyPairs
    {
        /// <summary>True when the library claims the Osprey library contract in its metadata, which makes its table part of the contract check.</summary>
        public static bool ClaimsContract(string path)
        {
            return BlibMetadata.Read(path, LibraryContract.METADATA_KEY) == LibraryContract.METADATA_VALUE;
        }

        /// <summary>
        /// The table's rows by RefSpectra id, or null when the library has no such table. A
        /// table Osprey cannot read (another writer's columns) is a library fault, reported as
        /// one rather than as a defect.
        /// </summary>
        public static IReadOnlyDictionary<long, BlibDecoyPair> Read(string path)
        {
            using (var reader = new BlibReader(path))
            {
                try
                {
                    return reader.ReadDecoyPairs();
                }
                catch (Exception ex) when (ex is SQLiteException || ex is InvalidCastException)
                {
                    throw new InvalidDataException(string.Format(
                        OspreyIOResources.BlibDecoyPairs_Read_The_library__0__has_a_DecoyPairs_table_Osprey_cannot_read___1_,
                        path, ex.Message), ex);
                }
            }
        }

        /// <summary>
        /// The rows of <paramref name="table"/> keyed by the entry they describe, taken while
        /// <see cref="LibraryEntry.Id"/> is still the RefSpectra id. The entries survive
        /// deduplication as the same objects, so the map still addresses them afterwards.
        /// </summary>
        public static IReadOnlyDictionary<LibraryEntry, BlibDecoyPair> ByEntry(IEnumerable<LibraryEntry> entries,
            IReadOnlyDictionary<long, BlibDecoyPair> table)
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
        /// entries it changed, each given or stripped of the decoy bit as the marker would), and
        /// each pair id naming exactly one target and one decoy at one charge gives the decoy
        /// its target's id with the decoy bit, as the manifest and composition pairings do.
        /// Returns the pairs made. A pair id with a side missing (the deduplicator dropped it,
        /// or the writer left it out) or with more than two rows is malformed and pairs nothing,
        /// the same rule the library contract's rule 6 applies, so its rows count as unpaired in
        /// the summary.
        /// </summary>
        public static int Pair(IList<LibraryEntry> library, IReadOnlyDictionary<LibraryEntry, BlibDecoyPair> pairs,
            PairingState state, out int newlyMarked)
        {
            newlyMarked = 0;
            var groups = new Dictionary<int, List<int>>();
            for (int idx = 0; idx < library.Count; idx++)
            {
                var entry = library[idx];
                if (!pairs.TryGetValue(entry, out var pair))
                    continue;
                if (entry.IsDecoy != pair.IsDecoy)
                {
                    entry.IsDecoy = pair.IsDecoy;
                    entry.Id = pair.IsDecoy ? entry.Id | LibraryEntry.DECOY_ID_BIT : entry.Id & ~LibraryEntry.DECOY_ID_BIT;
                    newlyMarked++;
                }
                if (!groups.TryGetValue(pair.PairId, out var group))
                    groups.Add(pair.PairId, group = new List<int>(2));
                group.Add(idx);
            }
            int paired = 0;
            // Pair-id order, so the ids a library gets do not depend on dictionary order.
            foreach (int pairId in groups.Keys.OrderBy(id => id))
            {
                var group = groups[pairId];
                if (group.Count != 2)
                    continue;
                int targetIdx = library[group[0]].IsDecoy ? group[1] : group[0];
                int decoyIdx = library[group[0]].IsDecoy ? group[0] : group[1];
                var target = library[targetIdx];
                var decoy = library[decoyIdx];
                if (target.IsDecoy || !decoy.IsDecoy || target.Charge != decoy.Charge ||
                    state.ClaimedTargets.Contains(targetIdx) || state.PairedDecoys.Contains(decoyIdx))
                {
                    continue;
                }
                decoy.Id = target.Id | LibraryEntry.DECOY_ID_BIT;
                state.ClaimedTargets.Add(targetIdx);
                state.PairedDecoys.Add(decoyIdx);
                paired++;
            }
            return paired;
        }
    }
}
