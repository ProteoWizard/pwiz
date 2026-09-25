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
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Loads a spectral library from its configured source, picking the
    /// format-specific loader and using the binary <c>.libcache</c> for
    /// fast reload. Relocated out of <c>AbstractScoringTask</c> — all of
    /// its collaborators (<see cref="LibraryCache"/>, the format loaders,
    /// <see cref="LibraryDeduplicator"/>) already live in this assembly.
    ///
    /// Logging is injected via callbacks rather than read from a pipeline
    /// context, so this stays in the I/O layer with no dependency on the
    /// task framework. The logic is otherwise unchanged from the original.
    /// </summary>
    public static class LibraryLoader
    {
        /// <summary>
        /// Load spectral library from the configured source, using binary cache
        /// when available. Matches Rust's .libcache mechanism for fast reload.
        /// </summary>
        public static List<LibraryEntry> Load(OspreyConfig config,
            IOspreyLog log, Action<string> logWarning)
        {
            return Load(config, LibraryLoadOptions.Default, log, logWarning);
        }

        /// <summary>
        /// Load spectral library from the configured source, using binary cache
        /// when available. Matches Rust's .libcache mechanism for fast reload.
        ///
        /// With <see cref="LibraryLoadOptions.OmitFragments"/>, the returned
        /// entries carry their six identity scalars but no fragment peaks -- the
        /// dead weight for a FirstPassFDR / <c>StopAfterStage5</c> worker. The
        /// cache-read path skips retaining the fragment blocks; the source-parse
        /// path loads fragments in full (the min-fragment filter and
        /// <see cref="LibraryDeduplicator"/> both need the counts), writes the
        /// shared .libcache in full, then drops the retained arrays. So the
        /// returned library is lean exactly when <see cref="LibraryLoadOptions.OmitFragments"/>
        /// is set, and the on-disk cache is unaffected.
        /// </summary>
        public static List<LibraryEntry> Load(OspreyConfig config, LibraryLoadOptions options,
            IOspreyLog log, Action<string> logWarning)
        {
            var loaded = Load(config, options, log, logWarning, out string error);
            if (error != null)
                throw new InvalidDataException(error);
            return loaded;
        }

        /// <summary>
        /// Load the library AND finish it: for a supplied-decoy library that means marking the
        /// decoys and pairing each to its target BEFORE the <c>.libcache</c> is written, so the
        /// cache holds the library the rest of the pipeline actually uses (issue #4650).
        ///
        /// <para>That is the whole point of the encapsulation. Pairing rewrites a decoy's Id to
        /// <c>target_id | DECOY_ID_BIT</c> and marking completes <c>IsDecoy</c>; while both ran
        /// at the CALLER, after this method returned, the cache stored neither and every caller
        /// had to finish the library the same way. A consumer keyed on a final base_id - the
        /// retained-set skip - could not address the decoy rows at all, because the ids it was
        /// filtering were parse-order ids that pairing had not reached yet.</para>
        ///
        /// <para><paramref name="error"/> is the failure channel the move requires. The two
        /// pairing faults (no decoys matched; paired fraction under the threshold) used to set
        /// <c>ExitCode = 1</c> at the caller, and they must still stop the run rather than
        /// degrade - so they are returned here, with the same messages, for the caller to report
        /// exactly as before. A null return with a null error means the library was empty.</para>
        /// </summary>
        public static List<LibraryEntry> Load(OspreyConfig config, LibraryLoadOptions options,
            IOspreyLog log, Action<string> logWarning, out string error)
        {
            error = null;
            // A null carrier means "no special handling" (a full load).
            options = options ?? LibraryLoadOptions.Default;
            // Default the injected log callbacks to no-ops so a null delegate
            // cannot NRE this public API (matches PipelineContext's pattern).
            log = log ?? OspreyLog.None;
            logWarning = logWarning ?? (_ => { });

            string path = config.LibrarySource.Path;
            // Route the library cache through ArtifactPaths (like the spectra
            // cache) so it follows --cache-dir / --work-dir and need not be
            // written beside a read-only library. Filename form preserved
            // ("<library-leaf>.libcache"); only the directory is redirected.
            string cachePath = Path.Combine(
                ArtifactPaths.ResolveCacheDir(path),
                Path.GetFileName(path) + ".libcache");

            // Reuse the binary cache only when it was built from the SAME
            // version of the source library. The library's identity hash
            // (file name + size + mtime, the same recipe .scores.parquet uses)
            // is stamped into the cache header on write and checked here on
            // read. A cache built from a different build at this path -- an
            // in-place rebuild, or a timestamp-preserving swap that a mtime
            // check would miss -- is ignored and rebuilt, since its decoys and
            // pairing no longer match the current manifest. Compute the hash
            // once and reuse it for both the read check and the save below.
            // A null hash (source missing) skips the check and trusts the
            // cache, since it is then the only copy available.
            bool sourceExists = !string.IsNullOrEmpty(path) && File.Exists(path);
            // COMPOSITION, not just the source file. The cache now holds a FINISHED library -
            // decoys marked, paired, and their protein_ids rewritten from the pairing manifest -
            // so the things that decide those bytes have to be in the key that admits the cache.
            // Keyed on the library alone, a cache built with one manifest would be reused under
            // another and silently supply the first manifest's accessions to protein parsimony
            // and protein FDR: a wrong answer with nothing in the run to show for it. This is
            // the same failure the library-identity check already existed to prevent, one input
            // wider. An old v2 cache also hashes differently here and is rebuilt, which is the
            // intended outcome - its contents are a half-built library.
            string libraryHash = sourceExists ? LibraryCompositionHash(config) : null;
            if (File.Exists(cachePath))
            {
                try
                {
                    LibraryCache.LibraryCacheStatus status;
                    // The cache reader interns the repeated strings as it builds
                    // each entry's arrays and logs a one-line collapse summary.
                    // OmitFragments has it read-and-discard the fragment blocks so
                    // the returned entries stay lean (no ~3.2 GB of peak arrays).
                    var cached = LibraryCache.LoadCache(
                        cachePath, libraryHash, options.OmitFragments, log.LogInfo, out status,
                        options.RetainFragmentsFor);
                    if (cached != null && cached.Count > 0)
                    {
                        log.LogInfo(string.Format(
                            "Loaded {0:N0} library precursors from cache '{1}'",
                            cached.Count, cachePath));
                        // Caches written before the normalizer existed still hold Carafe's
                        // per-peptide accessions, and the cache is keyed on the SOURCE hash, so
                        // nothing else would invalidate them. Normalizing here keeps a cached
                        // load identical to a fresh parse instead of making the protein report
                        // depend on whether a cache happened to be present.
                        CarafeProteinIdNormalizer.Normalize(cached, logWarning);
                        // Said out loud, because otherwise the saving is invisible. The Stage 7
                        // library-fragment release that used to report it now correctly reports
                        // 0 - it freed nothing because nothing was allocated - and that zero is
                        // indistinguishable in a log from the zero of a broken call site, which
                        // is exactly what the gate's -RequireFreed exists to catch. So the leg
                        // that took the saving at load has to claim it (issue #4650).
                        if (options.RetainFragmentsFor != null)
                        {
                            int skipped = 0;
                            foreach (var entry in cached)
                            {
                                if (entry.IsSpectrumReleased)
                                    skipped++;
                            }
                            if (OspreyEnvironment.LogMemory)
                            {
                                log.LogInfo(string.Format(
                                    @"Skipped library fragments for {0} of {1} entries at load " +
                                    @"({2} base_ids retained for the 1st-pass retained set)",
                                    skipped, cached.Count, options.RetainFragmentsFor.Count));
                            }
                            log.LogInfo(LogTag.COUNT, LogKey.Format(LogKey.COUNT_LIBRARY_FRAGMENTS_SKIPPED,
                                @"skipped={0} entries={1} retained={2}",
                                skipped, cached.Count, options.RetainFragmentsFor.Count));
                        }
                        // ALREADY FINISHED. A v3 cache was written after marking and pairing, so
                        // re-running them here would redo work whose result is in the bytes -
                        // and, for the manifest arm, re-read a file the composition hash has
                        // already proven unchanged. The summary is still reported, recovered
                        // from the finished library, so a cached run is not silent about the
                        // pairing fraction (issue #4650).
                        if (LibrarySuppliesDecoys(config))
                            LogCachedPairingSummary(RecoverPairingStats(cached), log);
                        return cached;
                    }
                    if (status == LibraryCache.LibraryCacheStatus.IdentityMismatch)
                    {
                        log.LogInfo(string.Format(
                            "Library cache '{0}' was built from a different version of the " +
                            "source library; ignoring the stale cache and rebuilding from source.",
                            cachePath));
                    }
                }
                catch (Exception ex)
                {
                    logWarning(string.Format(
                        "Failed to load library cache: {0}. Falling back to source.", ex.Message));
                }
            }

            // Parse from source
            log.LogInfo(string.Format("Loading spectral library from {0}...", path));

            List<LibraryEntry> entries;

            switch (config.LibrarySource.Format)
            {
                case LibraryFormat.DiannTsv:
                    var tsvLoader = new DiannTsvLoader();
                    entries = tsvLoader.Load(path, log.LogInfo);
                    break;

                case LibraryFormat.Blib:
                    var blibLoader = new BlibLoader();
                    entries = blibLoader.Load(path, log.LogInfo);
                    break;

                default:
                    throw new NotSupportedException(string.Format(
                        "Unsupported library format: {0}", config.LibrarySource.Format));
            }

            // Strip Carafe's per-peptide "_pepNNNNN" pseudo-protein accessions BEFORE dedup:
            // dedup unions each (modified sequence, charge) group's accessions through a
            // SortedSet, so cleaning first lets that union collapse the variants instead of
            // carrying duplicates. This ordering is also what pre-stripping the source TSV
            // produces, which is how the regression goldens were captured.
            CarafeProteinIdNormalizer.Normalize(entries, logWarning);

            // Deduplicate library entries. The format loaders already interned
            // their strings during construction (and logged a collapse summary),
            // so entries carry shared instances by the time they get here.
            entries = LibraryDeduplicator.DeduplicateLibrary(entries);

            log.LogInfo(string.Format("Loaded {0:N0} library precursors", entries.Count));

            // Peak-less entries (0 fragments) are a BiblioSpec MS1-feature-finding artifact and are
            // not valid for DIA search; fail fast at load (issue #4355 / PR #4434 review), before the
            // cache save, so a bad entry never reaches the cache, decoy generation (which would
            // silently exclude it), or a lean OmitFragments load (which would retain a phantom and
            // diverge the FirstPassFDR reconciliation bytes).
            foreach (var entry in entries)
                if (entry.Fragments.Count == 0)
                    throw new InvalidDataException(string.Format(
                        "Library entry {0} ({1}) has no fragment peaks; peak-less entries support " +
                        "BiblioSpec MS1 feature finding and are not valid for DIA search.",
                        entry.Id, entry.ModifiedSequence));

            // FINISH the library before it is cached. Marking and pairing used to run at the
            // caller, after this method returned, so the cache stored a half-built library and
            // every caller had to complete it the same way (issue #4650). Here, ahead of the
            // save, the cached bytes ARE the library: decoys marked, Ids paired, protein_ids
            // rewritten from the manifest. Ordering against Normalize and Deduplicate above is
            // unchanged - both still run first, which is what the cross-impl byte parity rests
            // on.
            if (LibrarySuppliesDecoys(config) &&
                !TryFinishSuppliedDecoys(entries, config, log, out error))
            {
                return null;
            }

            // Save binary cache for next run
            try
            {
                // libraryHash is non-null here: the source existed to parse.
                LibraryCache.SaveCache(cachePath, entries, libraryHash);
                log.LogInfo(string.Format(
                    "Saved library cache ({0:N0} precursors) to '{1}'",
                    entries.Count, cachePath));
            }
            catch (Exception ex)
            {
                logWarning(string.Format("Failed to save library cache: {0}", ex.Message));
            }

            // Drop the retained fragment arrays now that every fragment-count
            // dependency is satisfied (the min-fragment filter in the loaders,
            // LibraryDeduplicator's fragment-count tie-break, and the full cache
            // save above). The six identity scalars are untouched, so a
            // StopAfterStage5 worker gets the resident-memory win with output
            // unchanged. Done here rather than in each loader so the source-parse
            // path returns a library as lean as the cache-read path.
            if (options.OmitFragments)
            {
                foreach (var entry in entries)
                    entry.Fragments = Array.Empty<LibraryFragment>();
            }
            // RetainFragmentsFor is deliberately NOT applied here. It is a read-time skip, and
            // this path has already paid for every fragment by the time it gets here, so there
            // is nothing left to save. More importantly the ids are not final yet: pairing runs
            // after Load returns (PerFileScoringTask.LoadLibraryAndDecoys ->
            // LibraryDecoyPairing), and it REWRITES a supplied decoy's Id to
            // target_id | DECOY_ID_BIT - so filtering on entry.Id here would test a parse-order
            // id against a set expressed in final base_ids and release the wrong entries. The
            // cache arm has no such problem: the ids it reads are the ones that were written
            // after pairing. A source-parsed library therefore stays fat until the Stage 7
            // library-fragment release drops the same set, by which point the ids ARE final,
            // and the two paths converge on the state every later reader sees.

            return entries;
        }
        /// <summary>
        /// Finish a supplied-decoy library: mark the decoys, then pair each to its target.
        /// Returns false with <paramref name="error"/> set on the two faults that make the
        /// library unusable - no decoys matched at all, and a paired fraction below the
        /// configured threshold.
        ///
        /// <para>INSIDE the load, and ahead of the cache write, deliberately (issue #4650).
        /// Pairing REWRITES a decoy's Id to <c>target_id | DECOY_ID_BIT</c>, and marking
        /// completes <c>IsDecoy</c> for the rows whose protein accessions carry a decoy prefix
        /// but no Decoy column. While both ran at the CALLER, a <c>.libcache</c> held neither -
        /// so the cached ids were parse-order ids and the cached <c>IsDecoy</c> was incomplete,
        /// and "the library cache" was a cache of a half-built library that every caller had to
        /// finish the same way. Anything keyed on a FINAL base_id - the retained-set skip this
        /// issue exists to enable - could not address those rows at all.</para>
        ///
        /// <para>The caller keeps the failure semantics it always had: these two faults are
        /// errors that stop the run, not warnings, because FDR estimates without proper
        /// target-decoy competition are not worth producing.</para>
        /// </summary>
        private static bool TryFinishSuppliedDecoys(
            List<LibraryEntry> library, OspreyConfig config, IOspreyLog log,
            out string error)
        {
            error = null;

            LibraryDecoyMarker.ApplyLibraryDecoyMarking(
                library, config.DecoyPrefixes, out var markingStats);
            log.LogInfo(string.Format(
                @"Library-decoy mode: matched prefixes {0}",
                FormatPrefixList(config.DecoyPrefixes)));
            log.LogInfo(LogTag.COUNT, string.Format(
                @"Library-decoy mode: {0} flagged ({1} via Decoy column, {2} via protein-accession prefix)",
                markingStats.NMarked, markingStats.NViaColumn, markingStats.NViaPrefix));

            int nLibraryTargets = 0;
            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                    nLibraryTargets++;
            }

            // Match Rust pipeline.rs at v26.6.0 (bcd7249): the "no decoys at all" check runs
            // BEFORE manifest application. The manifest CAN flip predictor-stripped entries to
            // IsDecoy=true (the Carafe failure mode commit d23d496 was built for), so this
            // ordering means a manifest cannot rescue a load that the prefix scan misses
            // entirely. Ordering preserved through the move for byte parity on the cross-impl
            // gate.
            int nLibraryDecoys = library.Count - nLibraryTargets;
            if (nLibraryDecoys == 0)
            {
                error = string.Format(
                    @"decoys_in_library mode requested but no library entries match prefixes {0}. " +
                    @"Check that the library actually contains decoys with one of these prefixes on " +
                    @"a protein accession, or unset decoys_in_library so Osprey generates decoys.",
                    FormatPrefixList(config.DecoyPrefixes));
                return false;
            }

            // Hybrid pairing. Net result on real Carafe-generated entrapment libraries:
            // ~30% via manifest, ~70% via composition, >99% total.
            var pairingState = new PairingState();
            LibraryDecoyPairing.CountTargetsAndDecoys(library,
                out int nTargetsForStats, out int nDecoysForStats);
            var pairingStats = new PairingStats
            {
                NTargets = nTargetsForStats,
                NDecoys = nDecoysForStats,
            };
            if (!string.IsNullOrEmpty(config.DecoyPairingManifestPath))
            {
                log.LogInfo(string.Format(
                    @"Loading decoy pairing manifest from {0}",
                    config.DecoyPairingManifestPath));
                DecoyPairingManifest manifest;
                try
                {
                    manifest = DecoyPairingManifest.FromTsv(config.DecoyPairingManifestPath);
                }
                catch (Exception ex)
                {
                    error = string.Format(
                        @"Failed to read decoy pairing manifest {0}: {1}",
                        config.DecoyPairingManifestPath, ex.Message);
                    return false;
                }
                var manifestStats = manifest.ApplyToLibrary(library, pairingState, log.LogInfo);
                pairingStats.NPairedViaManifest = manifestStats.NPaired;
                if (manifestStats.NProteinsReplaced > 0)
                {
                    log.LogInfo(string.Format(
                        @"Library-decoy mode: manifest replaced protein_ids on {0} library " +
                        @"entries (clean source-protein accessions from the manifest's " +
                        @"`proteins` column)",
                        manifestStats.NProteinsReplaced));
                }
                if (manifestStats.NNewlyMarkedDecoy > 0)
                {
                    // Manifest classified entries as decoy that were loaded as targets (the
                    // predictor stripped the decoy prefix). Update the decoy count so the
                    // pairing fraction is honest.
                    log.LogInfo(string.Format(
                        @"Library-decoy mode: manifest classified {0} additional library " +
                        @"entries as decoys (their protein accessions lacked a decoy prefix)",
                        manifestStats.NNewlyMarkedDecoy));
                    LibraryDecoyPairing.CountTargetsAndDecoys(library,
                        out nTargetsForStats, out nDecoysForStats);
                    pairingStats.NTargets = nTargetsForStats;
                    pairingStats.NDecoys = nDecoysForStats;
                }
            }
            else
            {
                log.LogInfo(
                    @"Pairing library decoys to targets by amino-acid composition " +
                    @"(no manifest provided).");
            }
            pairingStats.NPairedViaComposition =
                LibraryDecoyPairing.PairLibraryDecoysByComposition(
                    library, config.DecoyPrefixes, pairingState);
            pairingStats.NPaired = pairingStats.NPairedViaManifest +
                pairingStats.NPairedViaComposition;
            // Defense-in-depth saturating subtract (matches Rust's saturating_sub intent; not
            // load-bearing).
            pairingStats.NUnpairedDecoys = Math.Max(0,
                pairingStats.NDecoys - pairingStats.NPaired);
            pairingStats.NUnpairedTargets = Math.Max(0,
                pairingStats.NTargets - pairingState.ClaimedTargets.Count);
            LogPairingSummary(pairingStats, log);
            if (pairingStats.PairedFraction < config.DecoyPairMinFraction)
            {
                error = string.Format(
                    @"Library-decoy pairing failed: only {0:F1}% of decoys paired with a target " +
                    @"(threshold: {1:F0}%). FDR estimates would be unreliable without proper " +
                    @"target-decoy competition. Either supply a pairing manifest, ensure the " +
                    @"library uses matching protein accessions with one of `decoy_prefixes` " +
                    @"({2}), or unset `decoys_in_library` so Osprey generates its own decoys.",
                    pairingStats.PairedFraction * 100.0,
                    config.DecoyPairMinFraction * 100.0,
                    FormatPrefixList(config.DecoyPrefixes));
                return false;
            }
            return true;
        }

        /// <summary>
        /// The pairing summary line, shared by the path that PAIRS and the path that loads an
        /// already-paired cache. A cached load does no pairing work, but the operator still
        /// needs the fraction - it is how a library with poor target-decoy correspondence is
        /// noticed - so the cached path recovers the same numbers from the finished library
        /// rather than going quiet on every run after the first.
        /// </summary>
        private static void LogPairingSummary(PairingStats stats, IOspreyLog log)
        {
            log.LogInfo(string.Format(
                @"Library-decoy pairing: paired {0}/{1} decoys ({2:F1}%); " +
                @"manifest={3}, composition={4}; {5} unpaired decoys, {6} unpaired targets",
                stats.NPaired, stats.NDecoys, stats.PairedFraction * 100.0,
                stats.NPairedViaManifest, stats.NPairedViaComposition,
                stats.NUnpairedDecoys, stats.NUnpairedTargets));
        }

        /// <summary>
        /// The same summary for a library that arrived ALREADY paired from the cache. Its own
        /// line, and not the one above, because the manifest / composition split is the one
        /// thing a finished library does not record: reporting the total under "composition"
        /// would say the manifest paired nothing, and on the CHS cohort that reads as
        /// <c>manifest=0, composition=3085757</c> against the cold run's
        /// <c>manifest=3085756, composition=1</c> - the same 99.9% arrived at, apparently, by a
        /// different mechanism. An operator diffing two runs would chase that. The total is
        /// recoverable and is what the threshold is about, so the total is what this reports.
        /// </summary>
        private static void LogCachedPairingSummary(PairingStats stats, IOspreyLog log)
        {
            log.LogInfo(string.Format(
                @"Library-decoy pairing: paired {0}/{1} decoys ({2:F1}%) - from the library " +
                @"cache, which records the pairing but not which mechanism made it; " +
                @"{3} unpaired decoys, {4} unpaired targets",
                stats.NPaired, stats.NDecoys, stats.PairedFraction * 100.0,
                stats.NUnpairedDecoys, stats.NUnpairedTargets));
        }

        /// <summary>
        /// Recover the pairing statistics from a library that is ALREADY paired, so a cache hit
        /// reports what the cache-miss path reported. Paired-ness is readable from the finished
        /// library: a decoy is paired exactly when its Id is <c>target | DECOY_ID_BIT</c> for a
        /// target present in the same library, which is what pairing wrote.
        ///
        /// <para>The manifest / composition split is NOT recoverable - the finished library does
        /// not record which mechanism claimed each pair - so those are reported as the total
        /// under composition, and the summary line says "from cache" so the two are not confused
        /// with a fresh pairing's split.</para>
        /// </summary>
        private static PairingStats RecoverPairingStats(List<LibraryEntry> library)
        {
            var targetIds = new HashSet<uint>();
            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                    targetIds.Add(entry.Id);
            }
            var stats = new PairingStats();
            int claimedTargets = 0;
            var claimed = new HashSet<uint>();
            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                {
                    stats.NTargets++;
                    continue;
                }
                stats.NDecoys++;
                uint targetId = entry.Id & ~LibraryEntry.DECOY_ID_BIT;
                if ((entry.Id & LibraryEntry.DECOY_ID_BIT) != 0 && targetIds.Contains(targetId))
                {
                    stats.NPaired++;
                    if (claimed.Add(targetId))
                        claimedTargets++;
                }
            }
            stats.NPairedViaComposition = stats.NPaired;
            stats.NUnpairedDecoys = Math.Max(0, stats.NDecoys - stats.NPaired);
            stats.NUnpairedTargets = Math.Max(0, stats.NTargets - claimedTargets);
            return stats;
        }

        private static string FormatPrefixList(IList<string> prefixes)
        {
            var sb = new StringBuilder("[");
            if (prefixes != null)
            {
                for (int i = 0; i < prefixes.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('"').Append(prefixes[i] ?? string.Empty).Append('"');
                }
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Identity of the FINISHED library: the source file (name, size, mtime - the recipe
        /// <c>.scores.parquet</c> uses) plus everything that decides how the load finishes it.
        /// Stamped into the <c>.libcache</c> header and checked on read.
        ///
        /// <para>The decoy terms are here because the cache now contains their effects. A
        /// supplied-decoy library's cached rows carry marked <c>IsDecoy</c>, paired
        /// <c>Id</c>s and manifest-rewritten <c>ProteinIds</c>; change the prefixes, the
        /// manifest, or whether decoys come from the library at all, and those bytes are wrong
        /// for the new configuration while remaining perfectly readable. The manifest is
        /// identified the same way the library is, so editing it in place invalidates the cache
        /// without anyone having to remember to.</para>
        /// </summary>
        private static string LibraryCompositionHash(OspreyConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("library:{0}\n", config.Identity.LibraryIdentityHash());
            sb.AppendFormat(CultureInfo.InvariantCulture, "decoys_in_library:{0}\n",
                config.DecoysInLibrary);
            sb.AppendFormat("decoy_method:{0}\n", config.DecoyMethod);
            sb.AppendFormat("decoy_prefixes:{0}\n", FormatPrefixList(config.DecoyPrefixes));
            AppendFileIdentity(sb, @"pairing_manifest", config.DecoyPairingManifestPath);
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var result = new StringBuilder(64);
                for (int i = 0; i < hashBytes.Length; i++)
                    result.Append(hashBytes[i].ToString(@"x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        /// <summary>Name, size and mtime of one file, or just its name when it is absent.</summary>
        private static void AppendFileIdentity(StringBuilder sb, string label, string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                sb.AppendFormat("{0}:none\n", label);
                return;
            }
            sb.AppendFormat("{0}_name:{1}\n", label, Path.GetFileName(filePath));
            if (!File.Exists(filePath))
                return;
            var info = new FileInfo(filePath);
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0}_size:{1}\n", label, info.Length);
            long mtimeSecs = (long)(info.LastWriteTimeUtc
                - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0}_mtime:{1}\n", label, mtimeSecs);
        }

        /// <summary>
        /// Whether the library supplies its own decoys, so the load must mark and pair them.
        /// The same predicate the caller used to apply - <c>DecoyMethod.FromLibrary</c> is a
        /// synonym for <c>DecoysInLibrary</c>, and treating it as one is what fixed library-decoy
        /// mode silently falling through to Reverse generation.
        /// </summary>
        private static bool LibrarySuppliesDecoys(OspreyConfig config)
        {
            return config.DecoysInLibrary || config.DecoyMethod == DecoyMethod.FromLibrary;
        }
    }
}
