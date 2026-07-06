/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Text.RegularExpressions;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Reconciles the searched library against the external entrapment pairing
    /// manifest, producing a clean target &lt;-&gt; entrapment pairing and identifying
    /// entrapment peptides that must be dropped because they have no target twin.
    ///
    /// The library legitimately contains peptide forms the manifest never had --
    /// notably N-terminal-Met-clipped variants generated at library-prediction time
    /// (Carafe <c>-clip_n_m</c>). Because the entrapment shuffle does not fix the
    /// N-terminus, that clip can fire on one side of a target/entrapment pair but
    /// not the other, leaving an entrapment with no target: a nonsensical artifact.
    /// FDRBench's paired estimator assumes every entrapment has a target and crashes
    /// on such a row, so we drop these here (with a warning) rather than emit them.
    ///
    /// Pairing for peptides the manifest already covers comes from the manifest.
    /// For peptides it does not (the extras), the pairing is reconstructed from the
    /// library protein accession's <c>_pep&lt;NNNNN&gt;</c> token, which a target and
    /// its entrapment/decoy quartet share (this token survives on the extras because
    /// the manifest's clean accessions -- which drop it -- are only substituted for
    /// covered peptides).
    /// </summary>
    public sealed class EntrapmentPairing
    {
        /// <summary>Peptide sequence -&gt; pair index (target and its entrapment share it).</summary>
        public readonly Dictionary<string, uint> PairIndexBySeq;

        /// <summary>
        /// Entrapment sequences excluded everywhere -- the emitted manifest, the
        /// FDRBench input, and the diagnostics classification -- because they have no
        /// target twin. Keeping all three consistent is the whole point: the HTML and
        /// FDRBench must see the same peptides.
        /// </summary>
        public readonly HashSet<string> ExcludedEntrapment;

        /// <summary>How many of <see cref="ExcludedEntrapment"/> are the known N-terminal-Met-clip artifact.</summary>
        public readonly int MetClipDroppedCount;

        /// <summary>
        /// The subset of <see cref="ExcludedEntrapment"/> with no target twin that is
        /// NOT explained by the Met-clip artifact -- surfaced (not silently dropped)
        /// so an unexplained pairing gap gets attention.
        /// </summary>
        public readonly IReadOnlyList<string> UnexplainedEntrapment;

        private EntrapmentPairing(Dictionary<string, uint> pairIndexBySeq,
            HashSet<string> excluded, int metClipDroppedCount, IReadOnlyList<string> unexplained)
        {
            PairIndexBySeq = pairIndexBySeq;
            ExcludedEntrapment = excluded;
            MetClipDroppedCount = metClipDroppedCount;
            UnexplainedEntrapment = unexplained;
        }

        /// <summary>Log the dropped-orphan summary (a warning for anything unexplained).</summary>
        public void LogSummary(Action<string> logInfo)
        {
            if (MetClipDroppedCount > 0)
                logInfo(string.Format(
                    @"[ENTRAPMENT] Dropped {0} unmatched entrapment peptides (N-terminal-Met-clip artifacts with no target pair); excluded from the FDRBench manifest, input, and diagnostics",
                    MetClipDroppedCount));
            if (UnexplainedEntrapment.Count > 0)
            {
                var examples = new List<string>();
                for (int i = 0; i < UnexplainedEntrapment.Count && i < 3; i++)
                    examples.Add(UnexplainedEntrapment[i]);
                logInfo(string.Format(
                    @"[ENTRAPMENT] WARNING: {0} entrapment peptides have no target pair and no known explanation (e.g. {1}); excluded -- investigate",
                    UnexplainedEntrapment.Count, string.Join(@", ", examples)));
            }
        }

        // Library accession -> shared quartet key: strip the decoy_/_p_target markers,
        // keep <accession>_pep<NNNNN>. Null when the token is absent.
        private static readonly Regex PepTokenRegex = new Regex(
            @"\|([A-Za-z0-9]+)_pep(\d+)\|", RegexOptions.Compiled);

        private static string ReconstructPairKey(IReadOnlyList<string> proteinIds)
        {
            if (proteinIds == null || proteinIds.Count == 0 || proteinIds[0] == null)
                return null;
            string p = proteinIds[0].Replace(@"decoy_", string.Empty).Replace(@"_p_target", string.Empty);
            var m = PepTokenRegex.Match(p);
            return m.Success ? m.Groups[1].Value + "_" + m.Groups[2].Value : null;
        }

        private struct ExtraInfo
        {
            public string PairKey;
            public bool IsEntrapment;
        }

        /// <summary>
        /// Build the reconciled pairing. <paramref name="externalManifestPath"/> may be
        /// null/absent; then every peptide is treated as an extra and paired purely
        /// from the library accessions.
        /// </summary>
        public static EntrapmentPairing Build(
            IReadOnlyDictionary<uint, LibraryEntry> libraryById, string externalManifestPath)
        {
            var pairIndexBySeq = new Dictionary<string, uint>(StringComparer.Ordinal);
            var manifestSeqs = new HashSet<string>(StringComparer.Ordinal);
            uint maxIdx = 0;
            if (!string.IsNullOrEmpty(externalManifestPath) && File.Exists(externalManifestPath))
            {
                var manifest = DecoyPairingManifest.FromTsv(externalManifestPath);
                foreach (var kv in manifest.PairIndices())
                {
                    pairIndexBySeq[kv.Key] = kv.Value;   // covered sequences
                    if (kv.Value > maxIdx) maxIdx = kv.Value;
                }
                foreach (var kv in manifest.Kinds())
                    manifestSeqs.Add(kv.Key);
            }

            // Collect the extras (library peptides the manifest does not cover), keyed
            // by their reconstructed quartet key, and note which keys have a target.
            var extras = new Dictionary<string, ExtraInfo>(StringComparer.Ordinal);
            var keyHasTarget = new HashSet<string>(StringComparer.Ordinal);
            foreach (var lib in libraryById.Values)
            {
                if (lib == null || lib.Sequence == null)
                    continue;
                if (EntrapmentLibraryClassifier.IsDecoySide(lib.ProteinIds))
                    continue;
                string seq = lib.Sequence;
                if (manifestSeqs.Contains(seq) || extras.ContainsKey(seq))
                    continue;
                string pk = ReconstructPairKey(lib.ProteinIds);
                bool ent = EntrapmentLibraryClassifier.IsEntrapment(lib.ProteinIds);
                extras[seq] = new ExtraInfo { PairKey = pk, IsEntrapment = ent };
                if (pk != null && !ent)
                    keyHasTarget.Add(pk);
            }

            // Assign a fresh, non-colliding pair index per distinct extra quartet key
            // (a matched target/entrapment pair shares it). Drop unmatched entrapment.
            var keyToIndex = new Dictionary<string, uint>(StringComparer.Ordinal);
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            var unexplained = new List<string>();
            int metClip = 0;
            uint nextIdx = maxIdx + 1;
            foreach (var kvp in extras)
            {
                string seq = kvp.Key;
                ExtraInfo info = kvp.Value;
                if (info.IsEntrapment && (info.PairKey == null || !keyHasTarget.Contains(info.PairKey)))
                {
                    // Unmatched entrapment: exclude it. If it is the known N-terminal-Met
                    // clip artifact (its M-prefixed form is a manifest peptide) drop it
                    // quietly; otherwise keep it in the unexplained list to surface.
                    excluded.Add(seq);
                    if (manifestSeqs.Contains("M" + seq))
                        metClip++;
                    else
                        unexplained.Add(seq);
                    continue;
                }
                if (info.PairKey != null)
                {
                    if (!keyToIndex.TryGetValue(info.PairKey, out uint idx))
                    {
                        idx = nextIdx++;
                        keyToIndex[info.PairKey] = idx;
                    }
                    pairIndexBySeq[seq] = idx;
                }
                else
                {
                    // A target with no reconstructable key: give it a standalone index
                    // (a target needs no pair; it only counts toward n_t).
                    pairIndexBySeq[seq] = nextIdx++;
                }
            }

            return new EntrapmentPairing(pairIndexBySeq, excluded, metClip, unexplained);
        }
    }
}
