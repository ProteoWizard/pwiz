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

        // N-terminal methionine, prepended to reverse a Met clip.
        private const string MetPrefix = @"M";

        /// <summary>
        /// Build the reconciled pairing, anchored on the external manifest.
        /// <paramref name="externalManifestPath"/> may be null/absent; then there is
        /// no manifest to reconcile against and every library peptide is kept unpaired.
        ///
        /// A library peptide the manifest does NOT contain is a Met-clip form: its
        /// <c>M</c>-prefixed sequence is a manifest peptide (verified 100% on real
        /// data). Its pair is the Met-clip of the manifest partner: a clipped
        /// entrapment pairs with the clip of its manifest target, which exists only
        /// when that target itself starts with M. When it cannot (target has no
        /// N-terminal M, or its clipped form was not predicted into the library), the
        /// clipped entrapment is inherently unpaired -- the artifact we drop. A
        /// library extra whose M-prefixed form is NOT a manifest peptide is not a
        /// Met-clip form; such an entrapment is surfaced as unexplained.
        /// </summary>
        public static EntrapmentPairing Build(
            IReadOnlyDictionary<uint, LibraryEntry> libraryById, string externalManifestPath)
        {
            var pairIndexBySeq = new Dictionary<string, uint>(StringComparer.Ordinal);
            var manifestKind = new Dictionary<string, PeptideKind>(StringComparer.Ordinal);
            var manifestPairIdx = new Dictionary<string, uint>(StringComparer.Ordinal);
            var targetByPairIdx = new Dictionary<uint, string>();
            uint maxIdx = 0;
            if (!string.IsNullOrEmpty(externalManifestPath) && File.Exists(externalManifestPath))
            {
                var manifest = DecoyPairingManifest.FromTsv(externalManifestPath);
                foreach (var kv in manifest.Kinds())
                    manifestKind[kv.Key] = kv.Value;
                foreach (var kv in manifest.PairIndices())
                {
                    manifestPairIdx[kv.Key] = kv.Value;
                    if (kv.Value > maxIdx) maxIdx = kv.Value;
                }
                foreach (var kv in manifestKind)
                {
                    if (kv.Value == PeptideKind.Target && manifestPairIdx.TryGetValue(kv.Key, out uint idx))
                        targetByPairIdx[idx] = kv.Key;
                }
            }
            // Clip-quartet indices live above the manifest's range so they never
            // collide with it or with each other (one per original pair index).
            bool haveManifest = manifestKind.Count > 0;
            uint clipBase = maxIdx + 1;
            uint nextStandalone = clipBase + maxIdx + 1;

            // Library non-decoy sequences, for the "is the clipped-target in the library?" test.
            var libSeqs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var lib in libraryById.Values)
            {
                if (lib != null && lib.Sequence != null &&
                    !EntrapmentLibraryClassifier.IsDecoySide(lib.ProteinIds))
                    libSeqs.Add(lib.Sequence);
            }

            var excluded = new HashSet<string>(StringComparer.Ordinal);
            var unexplained = new List<string>();
            int metClip = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var lib in libraryById.Values)
            {
                if (lib == null || lib.Sequence == null)
                    continue;
                if (EntrapmentLibraryClassifier.IsDecoySide(lib.ProteinIds))
                    continue;
                string seq = lib.Sequence;
                if (!seen.Add(seq))
                    continue;

                // Covered by the manifest: use its pairing directly.
                if (manifestPairIdx.TryGetValue(seq, out uint mi))
                {
                    pairIndexBySeq[seq] = mi;
                    continue;
                }

                // With no manifest there is nothing to reconcile against: keep every
                // peptide, exclude none (Met-clip artifacts are undetectable).
                if (!haveManifest)
                {
                    pairIndexBySeq[seq] = nextStandalone++;
                    continue;
                }

                bool ent = EntrapmentLibraryClassifier.IsEntrapment(lib.ProteinIds);
                // Extra: it should be a Met-clip form of a manifest peptide.
                if (!manifestKind.TryGetValue(MetPrefix + seq, out var mkind))
                {
                    if (ent)
                    {
                        // Not a Met-clip form and unpaired -> surface, don't silently drop.
                        excluded.Add(seq);
                        unexplained.Add(seq);
                    }
                    else
                    {
                        pairIndexBySeq[seq] = nextStandalone++; // extra target: only adds to n_t
                    }
                    continue;
                }

                uint origIdx = manifestPairIdx[MetPrefix + seq];
                if (ent)
                {
                    // Clipped entrapment. Its only valid pair is the clip of the manifest
                    // target at this pair index, which exists only if that target starts
                    // with M and its clipped form was predicted into the library.
                    string t = targetByPairIdx.TryGetValue(origIdx, out var tt) ? tt : null;
                    // The clipped target must itself be an EXTRA in the library: only
                    // then does it land on the shared clip index. If its sequence is a
                    // manifest peptide (covered), it keeps that manifest index instead,
                    // leaving this entrapment unpaired -- so it is still an orphan.
                    string clippedTarget = t != null && t.StartsWith(MetPrefix, StringComparison.Ordinal)
                        ? t.Substring(1) : null;
                    bool clippedTargetExists = clippedTarget != null
                        && libSeqs.Contains(clippedTarget) && !manifestKind.ContainsKey(clippedTarget);
                    if (!clippedTargetExists)
                    {
                        excluded.Add(seq);   // the understood Met-clip orphan entrapment
                        metClip++;
                    }
                    else
                    {
                        pairIndexBySeq[seq] = clipBase + origIdx;
                    }
                }
                else
                {
                    // Clipped target: keep it in the clip-quartet index (shares it with
                    // its clipped entrapment, which resolves to the same origIdx).
                    pairIndexBySeq[seq] = clipBase + origIdx;
                }
            }

            return new EntrapmentPairing(pairIndexBySeq, excluded, metClip, unexplained);
        }
    }
}
