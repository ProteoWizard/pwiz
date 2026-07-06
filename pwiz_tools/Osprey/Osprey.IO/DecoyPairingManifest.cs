/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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
using System.IO;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Stats returned by <see cref="DecoyPairingManifest.ApplyToLibrary"/>.
    /// Maps to Rust <c>osprey_io::pairing::ManifestApplyStats</c>.
    /// </summary>
    public class ManifestApplyStats
    {
        /// <summary>
        /// Decoys paired with a target on this call (decoy.Id rewritten so
        /// its base_id matches the target's id).
        /// </summary>
        public int NPaired { get; set; }

        /// <summary>
        /// Library entries whose <see cref="LibraryEntry.IsDecoy"/> was
        /// flipped from false to true because the manifest classified
        /// them as <c>decoy</c> or <c>p_decoy</c>. The protein-prefix
        /// scan in <c>LibraryDecoyMarker</c> misses these when the
        /// library predictor strips the decoy prefix from protein
        /// accessions; the manifest's sequence-based classification is
        /// authoritative.
        /// </summary>
        public int NNewlyMarkedDecoy { get; set; }

        /// <summary>
        /// Library entries whose <see cref="LibraryEntry.ProteinIds"/>
        /// were replaced from the manifest's <c>proteins</c> column.
        /// Carafe and similar library predictors may stamp a
        /// per-peptide suffix into ProteinID (e.g.
        /// <c>sp|P12345_pep00001|GENE_A</c>) which breaks protein
        /// parsimony / picked-protein FDR -- the manifest carries the
        /// clean, un-suffixed source accessions in its
        /// semicolon-separated <c>proteins</c> column and is the
        /// authoritative source of protein info when present.
        /// </summary>
        public int NProteinsReplaced { get; set; }
    }

    /// <summary>
    /// One of the four peptide kinds in an FDRBench manifest row.
    /// Maps to Rust <c>osprey_io::pairing::PeptideKind</c>.
    /// </summary>
    public enum PeptideKind
    {
        /// <summary>Original target peptide from a real protein.</summary>
        Target,
        /// <summary>Decoy of a real-protein target (randomized sequence).</summary>
        Decoy,
        /// <summary>Entrapment target peptide (a synthetic target).</summary>
        PTarget,
        /// <summary>Decoy of an entrapment target.</summary>
        PDecoy,
    }

    /// <summary>
    /// In-memory representation of a FDRBench pairing manifest. FDRBench
    /// emits a 5-column TSV (<c>sequence, decoy, proteins, peptide_type,
    /// peptide_pair_index</c>) when it generates an entrapment library.
    /// Each <c>peptide_pair_index</c> group contains exactly four entries:
    /// target, p_target, decoy (of target), p_decoy (of p_target).
    /// From this we recover two target-decoy pairs per pair_index:
    /// <c>(target &lt;-&gt; decoy)</c> and <c>(p_target &lt;-&gt; p_decoy)</c>.
    /// Maps to Rust <c>osprey_io::pairing::DecoyPairingManifest</c>.
    /// </summary>
    public class DecoyPairingManifest
    {
        private readonly Dictionary<string, ManifestEntryInfo> _seqToInfo;

        private DecoyPairingManifest(Dictionary<string, ManifestEntryInfo> seqToInfo)
        {
            _seqToInfo = seqToInfo;
        }

        /// <summary>Number of indexed sequences.</summary>
        public int Count { get { return _seqToInfo.Count; } }

        /// <summary>True when no sequences are indexed.</summary>
        public bool IsEmpty { get { return _seqToInfo.Count == 0; } }

        /// <summary>
        /// Enumerate the manifest's per-sequence classification (sequence -&gt;
        /// target / decoy / p_target / p_decoy). Read-only view used by the
        /// model-diagnostics report to classify each precursor by its
        /// modified sequence; pairing/library-rewrite consumers use
        /// <see cref="ApplyToLibrary"/> instead.
        /// </summary>
        public IEnumerable<KeyValuePair<string, PeptideKind>> Kinds()
        {
            foreach (var kv in _seqToInfo)
                yield return new KeyValuePair<string, PeptideKind>(kv.Key, kv.Value.Kind);
        }

        /// <summary>
        /// Enumerate the manifest's per-sequence <c>peptide_pair_index</c>
        /// (sequence -&gt; pair index). Within a pair index the target and its
        /// entrapment (p_target) share the value, which the model-diagnostics
        /// paired-FDP estimator uses to compare an entrapment hit against its
        /// paired target's score.
        /// </summary>
        public IEnumerable<KeyValuePair<string, uint>> PairIndices()
        {
            foreach (var kv in _seqToInfo)
                yield return new KeyValuePair<string, uint>(kv.Key, kv.Value.PairIndex);
        }

        /// <summary>
        /// Parse a FDRBench-style pairing manifest from disk. Expected
        /// header (tab-separated, in any column order, but the three
        /// required columns must all be present):
        /// <c>sequence  decoy  proteins  peptide_type  peptide_pair_index</c>.
        /// Rows with an unknown <c>peptide_type</c> or non-numeric
        /// <c>peptide_pair_index</c> are skipped.
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// When the header is missing one of the required columns.
        /// </exception>
        public static DecoyPairingManifest FromTsv(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException(@"path is required", nameof(path));
            using (var reader = new StreamReader(path))
            {
                string header = reader.ReadLine();
                if (header == null)
                    throw new InvalidDataException(@"FDRBench manifest is empty");
                var cols = header.Split('\t');
                int iSeq = -1, iType = -1, iPair = -1, iProteins = -1;
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] == @"sequence")
                        iSeq = i;
                    else if (cols[i] == @"peptide_type")
                        iType = i;
                    else if (cols[i] == @"peptide_pair_index")
                        iPair = i;
                    else if (cols[i] == @"proteins")
                        iProteins = i;
                }
                if (iSeq < 0 || iType < 0 || iPair < 0)
                {
                    throw new InvalidDataException(string.Format(
                        @"FDRBench manifest header missing required columns " +
                        @"(need sequence, peptide_type, peptide_pair_index). Got: {0}",
                        header));
                }
                // `proteins` is optional -- older manifests without it still
                // parse fine; ApplyToLibrary simply won't replace
                // protein_ids when this column is absent.
                // When the proteins column IS present in the header, rows
                // truncated to drop it are treated as malformed (skipped
                // and counted in nSkipped). This matches Rust 0c3a73e
                // `max_needed = i_proteins.unwrap_or(0).max(...)`:
                // strict for cross-impl byte parity. The body's
                // `iProteins < fields.Length` guard is belt-and-suspenders
                // against the early skip, not a design intent for lenient
                // parsing of short rows.
                int maxNeeded = Math.Max(iSeq, Math.Max(iType, iPair));
                if (iProteins >= 0 && iProteins > maxNeeded)
                    maxNeeded = iProteins;
                int minRequiredCols = maxNeeded + 1;
                var map = new Dictionary<string, ManifestEntryInfo>(StringComparer.Ordinal);
                int nSkipped = 0;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0)
                        continue;
                    var fields = line.Split('\t');
                    if (fields.Length < minRequiredCols)
                    {
                        nSkipped++;
                        continue;
                    }
                    if (!TryParsePeptideKind(fields[iType], out var kind))
                    {
                        nSkipped++;
                        continue;
                    }
                    if (!uint.TryParse(fields[iPair],
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out uint pairIndex))
                    {
                        nSkipped++;
                        continue;
                    }
                    // Parse the optional semicolon-separated proteins
                    // column. Empty / "-" / single-dash -> empty list,
                    // signalling "don't override library protein_ids."
                    var proteins = new List<string>();
                    if (iProteins >= 0 && iProteins < fields.Length)
                    {
                        string raw = fields[iProteins].Trim();
                        if (raw.Length > 0 && raw != @"-")
                        {
                            foreach (var p in raw.Split(';'))
                            {
                                string pt = p.Trim();
                                if (pt.Length > 0)
                                    proteins.Add(pt);
                            }
                        }
                    }
                    map[fields[iSeq]] = new ManifestEntryInfo(kind, pairIndex, proteins);
                }
                return new DecoyPairingManifest(map);
            }
        }

        /// <summary>
        /// Apply this manifest to <paramref name="library"/>: for each
        /// <c>(pair_index, partition, charge)</c> bucket, identify
        /// target-side and decoy-side entries and rewrite each decoy's
        /// <see cref="LibraryEntry.Id"/> so its <c>base_id</c> matches a
        /// target's id. Returns <see cref="ManifestApplyStats"/> with the
        /// pair count and the count of newly-marked decoys.
        ///
        /// <paramref name="state"/> carries already-claimed targets and
        /// already-paired decoys so this can be chained with a
        /// composition-based fallback pass without claiming the same
        /// target twice. New pairings made here are added to
        /// <paramref name="state"/>.
        ///
        /// <b>Authoritative classification by manifest.</b> Any library
        /// entry whose sequence appears in the manifest as <c>decoy</c>
        /// or <c>p_decoy</c> gets <see cref="LibraryEntry.IsDecoy"/> =
        /// true and <see cref="LibraryEntry.DECOY_ID_BIT"/> set on its
        /// id, even if it doesn't pair. This catches the common case
        /// where a library predictor (e.g. Carafe) stripped the
        /// protein-accession decoy prefix during processing, leaving
        /// Osprey's prefix scan unable to recognise the decoys. The
        /// manifest's <c>peptide_type</c> column is taken as the source
        /// of truth.
        /// </summary>
        public ManifestApplyStats ApplyToLibrary(IList<LibraryEntry> library, PairingState state)
        {
            var stats = new ManifestApplyStats();
            if (library == null || library.Count == 0 || state == null)
                return stats;

            // Bucket key: (pair_index, partition, charge, is_target_side).
            // Skip entries already paired (decoys) or claimed (targets).
            // Also collect decoy-side indices so we can stamp IsDecoy +
            // DECOY_ID_BIT on them even when their predictor stripped the
            // prefix from the protein accession.
            var buckets = new Dictionary<BucketKey, List<int>>(
                BucketKeyComparer.Instance);
            var decoySideIndices = new List<int>();
            // Library indices whose ProteinIds should be replaced with
            // the manifest's clean accessions (collected during the
            // read-only scan; applied below). Carafe-style libraries
            // stamp a per-peptide suffix into ProteinID that breaks
            // protein parsimony; the manifest's `proteins` column
            // carries the clean, un-suffixed source accessions and is
            // the authoritative source of protein info when present.
            var proteinOverride = new List<KeyValuePair<int, List<string>>>();
            for (int idx = 0; idx < library.Count; idx++)
            {
                var entry = library[idx];
                if (entry == null)
                    continue;
                if (entry.IsDecoy && state.PairedDecoys.Contains(idx))
                    continue;
                if (!entry.IsDecoy && state.ClaimedTargets.Contains(idx))
                    continue;
                if (!_seqToInfo.TryGetValue(entry.Sequence, out var info))
                    continue;
                bool isTargetSide = IsTargetSideOf(info.Kind);
                var key = new BucketKey(info.PairIndex, PartitionOf(info.Kind),
                    entry.Charge, isTargetSide);
                if (!buckets.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    buckets[key] = list;
                }
                list.Add(idx);
                if (!isTargetSide)
                    decoySideIndices.Add(idx);
                // Manifest has clean source accessions for this sequence,
                // and the library's stored ProteinIds differ -> queue the
                // override. Empty Proteins (column absent / value was
                // "-") is a no-op: the library wins.
                if (info.Proteins != null && info.Proteins.Count > 0 &&
                    !ProteinListsEqual(entry.ProteinIds, info.Proteins))
                {
                    proteinOverride.Add(
                        new KeyValuePair<int, List<string>>(idx, info.Proteins));
                }
            }

            // Mark manifest-identified decoys BEFORE pairing. Idempotent:
            // entries already flagged keep their flag and high bit.
            // Pairing will overwrite the low 31 bits to the target's
            // base_id for paired decoys; unpaired decoys keep their
            // original id with just the high bit set.
            foreach (var idx in decoySideIndices)
            {
                if (!library[idx].IsDecoy)
                {
                    library[idx].IsDecoy = true;
                    stats.NNewlyMarkedDecoy++;
                }
                if ((library[idx].Id & LibraryEntry.DECOY_ID_BIT) == 0u)
                    library[idx].Id |= LibraryEntry.DECOY_ID_BIT;
            }

            // Replace library ProteinIds with the manifest's clean
            // accessions for every sequence the manifest covers and
            // whose stored ProteinIds disagree.
            stats.NProteinsReplaced = proteinOverride.Count;
            foreach (var kv in proteinOverride)
            {
                library[kv.Key].ProteinIds = new List<string>(kv.Value);
            }

            // Walk every target-side bucket; pair with the matching
            // decoy-side bucket in deterministic order.
            var pairings = new List<KeyValuePair<int, int>>();
            var targetKeys = new List<BucketKey>();
            foreach (var k in buckets.Keys)
            {
                if (k.IsTargetSide)
                    targetKeys.Add(k);
            }
            targetKeys.Sort(BucketKeyOrderComparer.Instance); // Array.Sort OK: targetKeys are distinct bucket dictionary keys, so the comparer never ties
            foreach (var tKey in targetKeys)
            {
                var dKey = new BucketKey(tKey.PairIndex, tKey.Partition,
                    tKey.Charge, false);
                if (!buckets.TryGetValue(dKey, out var dIndices))
                    continue;
                var tIndices = buckets[tKey];
                var tSorted = new List<int>(tIndices);
                var dSorted = new List<int>(dIndices);
                // Array.Sort OK (both sorts): the secondary key is the unique library entry Id,
                // so the comparator never returns 0 and the unstable-sort tie path is unreachable.
                tSorted.Sort((a, b) => // Array.Sort OK: (see above) secondary key is unique library entry Id, comparator never ties
                {
                    int c = string.CompareOrdinal(library[a].Sequence, library[b].Sequence);
                    if (c != 0)
                        return c;
                    return library[a].Id.CompareTo(library[b].Id);
                });
                dSorted.Sort((a, b) => // Array.Sort OK: (see above) secondary key is unique library entry Id, comparator never ties
                {
                    int c = string.CompareOrdinal(library[a].Sequence, library[b].Sequence);
                    if (c != 0)
                        return c;
                    return library[a].Id.CompareTo(library[b].Id);
                });
                int n = Math.Min(tSorted.Count, dSorted.Count);
                for (int i = 0; i < n; i++)
                {
                    int tIdx = tSorted[i];
                    int dIdx = dSorted[i];
                    pairings.Add(new KeyValuePair<int, int>(dIdx, tIdx));
                    state.ClaimedTargets.Add(tIdx);
                    state.PairedDecoys.Add(dIdx);
                }
            }

            // Apply pairings. For each paired decoy, set its id so that
            // base_id matches the target. Unpaired decoy-side entries
            // keep the high bit set above without an id rewrite.
            for (int i = 0; i < pairings.Count; i++)
            {
                int decoyIdx = pairings[i].Key;
                int targetIdx = pairings[i].Value;
                uint targetId = library[targetIdx].Id;
                library[decoyIdx].Id = targetId | LibraryEntry.DECOY_ID_BIT;
            }
            stats.NPaired = pairings.Count;
            return stats;
        }

        private static bool TryParsePeptideKind(string s, out PeptideKind kind)
        {
            switch (s)
            {
                case @"target":   kind = PeptideKind.Target;   return true;
                case @"decoy":    kind = PeptideKind.Decoy;    return true;
                case @"p_target": kind = PeptideKind.PTarget;  return true;
                case @"p_decoy":  kind = PeptideKind.PDecoy;   return true;
                default:          kind = PeptideKind.Target;   return false;
            }
        }

        // Which "side" of a pair this kind sits on (true = target-like).
        private static bool IsTargetSideOf(PeptideKind k)
        {
            return k == PeptideKind.Target || k == PeptideKind.PTarget;
        }

        // Partition: target<->decoy and p_target<->p_decoy are independent
        // pairings within the same peptide_pair_index.
        private static byte PartitionOf(PeptideKind k)
        {
            return (byte)(k == PeptideKind.Target || k == PeptideKind.Decoy ? 0 : 1);
        }

        // Cheap ordinal equality on two protein-ID lists. Used to decide
        // whether the manifest's clean accessions would actually change
        // the library entry (skip the rewrite when they match).
        private static bool ProteinListsEqual(IList<string> a, IList<string> b)
        {
            if (a == null)
                return b == null || b.Count == 0;
            if (b == null)
                return a.Count == 0;
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        // Per-sequence manifest metadata: peptide kind, pair index, and
        // (optionally) the clean source proteins from the `proteins`
        // column. When Proteins is non-empty, ApplyToLibrary replaces the
        // matching library entry's ProteinIds; this is the authoritative
        // protein source for Carafe-built libraries that stamped a
        // per-peptide suffix into ProteinID.
        private class ManifestEntryInfo
        {
            public readonly PeptideKind Kind;
            public readonly uint PairIndex;
            public readonly List<string> Proteins;

            public ManifestEntryInfo(PeptideKind kind, uint pairIndex, List<string> proteins)
            {
                Kind = kind;
                PairIndex = pairIndex;
                Proteins = proteins ?? new List<string>();
            }
        }

        private struct BucketKey : IEquatable<BucketKey>
        {
            public readonly uint PairIndex;
            public readonly byte Partition;
            public readonly byte Charge;
            public readonly bool IsTargetSide;

            public BucketKey(uint pairIndex, byte partition, byte charge, bool isTargetSide)
            {
                PairIndex = pairIndex;
                Partition = partition;
                Charge = charge;
                IsTargetSide = isTargetSide;
            }

            public bool Equals(BucketKey other)
            {
                return PairIndex == other.PairIndex &&
                       Partition == other.Partition &&
                       Charge == other.Charge &&
                       IsTargetSide == other.IsTargetSide;
            }

            public override bool Equals(object obj)
            {
                return obj is BucketKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = (int)PairIndex;
                    h = (h * 397) ^ Partition;
                    h = (h * 397) ^ Charge;
                    h = (h * 397) ^ (IsTargetSide ? 1 : 0);
                    return h;
                }
            }
        }

        private sealed class BucketKeyComparer : IEqualityComparer<BucketKey>
        {
            public static readonly BucketKeyComparer Instance = new BucketKeyComparer();
            public bool Equals(BucketKey x, BucketKey y) { return x.Equals(y); }
            public int GetHashCode(BucketKey obj) { return obj.GetHashCode(); }
        }

        // Deterministic ordering on (pair_index, partition, charge, side)
        // so the manifest pairing pass is order-independent. Matches Rust's
        // tuple ordering on the same fields.
        private sealed class BucketKeyOrderComparer : IComparer<BucketKey>
        {
            public static readonly BucketKeyOrderComparer Instance =
                new BucketKeyOrderComparer();
            public int Compare(BucketKey x, BucketKey y)
            {
                int c = x.PairIndex.CompareTo(y.PairIndex);
                if (c != 0)
                    return c;
                c = x.Partition.CompareTo(y.Partition);
                if (c != 0)
                    return c;
                c = x.Charge.CompareTo(y.Charge);
                if (c != 0)
                    return c;
                return x.IsTargetSide.CompareTo(y.IsTargetSide);
            }
        }
    }
}
