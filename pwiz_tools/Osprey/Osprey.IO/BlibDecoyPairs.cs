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
        /// The rows of <paramref name="table"/> keyed by the entry they describe, through
        /// <paramref name="refSpectraIds"/>, the RefSpectra id of each entry in order (the
        /// loader's <see cref="BlibLoader.RefSpectraIds"/>). The entries survive deduplication
        /// as the same objects, so the map still addresses them afterwards.
        /// </summary>
        public static IReadOnlyDictionary<LibraryEntry, BlibDecoyPair> ByEntry(IReadOnlyList<LibraryEntry> entries,
            IReadOnlyList<long> refSpectraIds, IReadOnlyDictionary<long, BlibDecoyPair> table)
        {
            if (entries.Count != refSpectraIds.Count)
                throw new ArgumentException($@"{entries.Count} entries but {refSpectraIds.Count} RefSpectra ids: not the loader's list");
            var byEntry = new Dictionary<LibraryEntry, BlibDecoyPair>(ReferenceEqualityComparer.Instance);
            for (int i = 0; i < entries.Count; i++)
            {
                if (table.TryGetValue(refSpectraIds[i], out var pair))
                    byEntry.Add(entries[i], pair);
            }
            return byEntry;
        }

        /// <summary>
        /// Marks and pairs <paramref name="library"/> from its table: a row's side is
        /// authoritative over the accession prefixes (<paramref name="newlyMarked"/> counts the
        /// entries it changed, each given or stripped of the decoy bit as the marker would), and
        /// each decoy row naming a target at its charge gives the decoy that target's id with the
        /// decoy bit, as the manifest and composition pairings do. Returns the pairs made. A
        /// decoy row naming no target, a target the deduplicator dropped, a decoy, or a target at
        /// another charge pairs nothing, the same rule the library contract's rule 6 applies, so
        /// it counts as unpaired in the summary. The rows' pairing ids are
        /// <see cref="PairingIds.Assign"/>'s to give, after this.
        /// </summary>
        public static int Pair(IList<LibraryEntry> library, IReadOnlyDictionary<LibraryEntry, BlibDecoyPair> pairs,
            IReadOnlyDictionary<long, LibraryEntry> entriesByRefSpectraId, PairingState state, out int newlyMarked)
        {
            newlyMarked = 0;
            var indexByEntry = new Dictionary<LibraryEntry, int>(ReferenceEqualityComparer.Instance);
            for (int idx = 0; idx < library.Count; idx++)
            {
                var entry = library[idx];
                indexByEntry[entry] = idx;
                if (!pairs.TryGetValue(entry, out var pair))
                    continue;
                if (entry.IsDecoy != pair.IsDecoy)
                {
                    entry.IsDecoy = pair.IsDecoy;
                    entry.Id = pair.IsDecoy ? entry.Id | LibraryEntry.DECOY_ID_BIT : entry.Id & ~LibraryEntry.DECOY_ID_BIT;
                    newlyMarked++;
                }
            }
            int paired = 0;
            // Library order, so the ids a library gets do not depend on dictionary order.
            for (int decoyIdx = 0; decoyIdx < library.Count; decoyIdx++)
            {
                var decoy = library[decoyIdx];
                if (!pairs.TryGetValue(decoy, out var pair) || !pair.IsDecoy || !pair.TargetRefSpectraId.HasValue ||
                    !entriesByRefSpectraId.TryGetValue(pair.TargetRefSpectraId.Value, out var target) ||
                    !indexByEntry.TryGetValue(target, out int targetIdx))
                {
                    continue;
                }
                if (target.IsDecoy || target.Charge != decoy.Charge ||
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

        /// <summary>
        /// The entries of <paramref name="entries"/> by RefSpectra id, through
        /// <paramref name="refSpectraIds"/> (the loader's list, in the entries' order): what a
        /// row's <see cref="BlibDecoyPair.TargetRefSpectraId"/> names.
        /// </summary>
        public static IReadOnlyDictionary<long, LibraryEntry> ByRefSpectraId(IReadOnlyList<LibraryEntry> entries,
            IReadOnlyList<long> refSpectraIds)
        {
            if (entries.Count != refSpectraIds.Count)
                throw new ArgumentException($@"{entries.Count} entries but {refSpectraIds.Count} RefSpectra ids: not the loader's list");
            var byId = new Dictionary<long, LibraryEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                byId[refSpectraIds[i]] = entries[i];
            return byId;
        }
    }
}
