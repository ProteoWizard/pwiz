/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// <c>--task CompactPerFileRescoring</c>: rewrite each input's
    /// <c>.scores-reconciled.parquet</c> from the pre-#4486 full-row shape into the Stage 5
    /// survivor subset, in place, and write nothing else.
    ///
    /// <para>Recovery for cohorts staged by an older build. Re-running Stage 6 to gain the new
    /// format costs hours at cohort scale; this reads each file once and writes back roughly a
    /// seventh of it.</para>
    ///
    /// <para><b>Why this is its own task rather than a step inside Stage 7.</b> The survivor
    /// pool is materialized by <c>PerFileScoringTask.Rehydrate</c>, so ANY task that demands a
    /// published byproduct - including the parquet paths this conversion needs - pulls tens of
    /// GB in behind it before the first file is converted. Three attempts to position the
    /// conversion inside <see cref="SecondPassFdrTask"/> all died that way, at 47-51 GB on a
    /// 63.7 GB box, without reaching a single file. Standing outside the canonical pipeline is
    /// not a convenience here; it is the only place the work fits in memory.</para>
    ///
    /// <para>Peak residency is one file's survivors plus one parquet row group, so the cohort
    /// size does not enter into it. Mirrors <see cref="SpectraCacheTask"/>, the other task that
    /// stages data rather than analyzing it and runs a one-task pipeline of its own.</para>
    /// </summary>
    internal sealed class CompactPerFileRescoreTask : OspreyTask
    {
        public override string Name => @"CompactPerFileRescoring";

        public override bool IsIncluded(PipelineContext ctx)
        {
            return ctx.Config.CompactReconciledOnly;
        }

        /// <summary>
        /// Declares no inputs. The reconciled parquets are discovered from the score paths at
        /// run time, and declaring them would invite the driver to reason about freshness for
        /// a task whose whole job is to rewrite them.
        /// </summary>
        public override IEnumerable<string> Inputs(PipelineContext ctx) => Array.Empty<string>();

        /// <summary>
        /// Declares no outputs: it rewrites files in place rather than producing new ones, and
        /// an output list would make the driver's already-done check compare a file against
        /// itself.
        /// </summary>
        public override IEnumerable<string> Outputs(PipelineContext ctx) => Array.Empty<string>();

        /// <summary>
        /// Nothing to rehydrate: this task publishes no byproduct and reads no pipeline state.
        /// That is the point - rehydrating is what materializes the survivor pool.
        /// </summary>
        public override bool Rehydrate(PipelineContext ctx) => true;

        public override bool Run(PipelineContext ctx)
        {
            var config = ctx.Config;
            var scoresPaths = ResolveScoresPaths(config);
            if (scoresPaths.Count == 0)
            {
                ctx.LogError(
                    @"--task CompactPerFileRescoring needs the per-file score parquets. Pass them " +
                    @"with --input-scores (the same paths a --task SecondPassFDR run would take).");
                ctx.ExitCode = 1;
                return false;
            }

            // Which files still carry the old shape. A footer read is cheap, so the ordinary
            // case - everything already compacted - costs one metadata read per file.
            //
            // The footer's library hash is checked in the same pass, BEFORE the library is
            // loaded and before any file is rewritten. The rewrite re-derives each row's
            // sequence, precursor m/z and protein_ids from the library BY ENTRY ID, and entry
            // ids are assigned at load, so pointing this task at a different build of "the
            // same" library silently rewrites every row with another peptide's identity. It
            // is not a hypothetical: two SEA-AD entrapment libraries of the same file name
            // differ by 149,311 entries, and the wrong one converted 72 CHS files before the
            // entry counts in two run logs gave it away. The hash is file name + size + mtime,
            // so this costs a stat, not a read.
            string expectedLibraryHash = config.Identity.LibraryIdentityHash();
            var stale = new List<KeyValuePair<string, string>>();
            foreach (string scoresPath in scoresPaths)
            {
                string reconciledPath = ParquetScoreCache.ReconciledPathFromScoresPath(scoresPath);
                if (!File.Exists(reconciledPath))
                    continue;
                var footer = ParquetScoreCache.LoadFooterMetadata(reconciledPath);
                footer.TryGetValue(@"osprey.reconciled", out string marker);
                if (string.Equals(marker, ParquetScoreCache.RECONCILED_SURVIVORS, StringComparison.Ordinal))
                    continue;
                if (!VerifyLibraryMatches(reconciledPath, footer, expectedLibraryHash, config, ctx))
                    return false;
                string fileName = Path.GetFileNameWithoutExtension(
                    RescoreHydration.SyntheticInputFromParquet(scoresPath));
                stale.Add(new KeyValuePair<string, string>(fileName, reconciledPath));
            }
            if (stale.Count == 0)
            {
                ctx.LogInfo(
                    @"Every reconciled parquet is already in the survivor subset format; nothing to do.");
                return true;
            }

            // The library, loaded here rather than pulled from the pipeline: the write
            // re-derives each row's sequence / precursor m/z / protein_ids from it, and with a
            // null library those columns would silently write empty. Decoys included, because
            // the reconciled parquet carries decoy rows too.
            // Through a throwaway PerFileScoringTask instance rather than a static call: the
            // loader caches into that task's own fields, which this task has no use for. Reusing
            // it is still right - decoy generation and supplied-decoy pairing have to match the
            // run that wrote these parquets, and a second implementation would be free to drift.
            if (!new PerFileScoringTask().LoadLibraryAndDecoys(config, out var fullLibrary, ctx))
                return false;
            var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var libEntry in fullLibrary)
                libraryById[libEntry.Id] = libEntry;

            // The retain set from the planner envelopes, NOT from a survivor pool - the whole
            // reason this task exists. Small and quick: the envelopes are read, not the parquets.
            var retainBaseIds = RescoreHydration.BuildRetainBaseIds(scoresPaths, Name);
            ctx.LogInfo(string.Format(
                @"Compacting {0} of {1} reconciled parquet(s) to the survivor subset ({2} retained base_ids)...",
                stale.Count, scoresPaths.Count, retainBaseIds.Count));

            // ONE line per file, emitted after it completes, carrying the counter and the
            // result together - and no ProgressReporter at any level, which is why the writer
            // is called with a null progress indent. No percentage: N/M already says it.
            //
            // Two reporters at one indent was the defect: the writer announces itself on its
            // first Report, so its heading printed BELOW the outer banner's first percent, and
            // both then emitted bare "N%" lines that no reader could attribute to a level. A
            // file takes seconds, so intra-file progress buys nothing that the completion line
            // does not say better. The general rule: never emit an OUTER counter inside an
            // inner block.
            long rowsBefore = 0, rowsAfter = 0;
            int done = 0;
            foreach (var kv in stale)
            {
                var result = CompactOneFile(kv.Key, kv.Value, retainBaseIds, libraryById, ctx);
                rowsBefore += result.OrigRowCount;
                rowsAfter += result.NWritten;
                done++;
                ctx.LogInfo(string.Format(
                    @"Compacted file {0}/{1}: {2} - kept {3:N0} of {4:N0} rows",
                    done, stale.Count, kv.Key, result.NWritten, result.OrigRowCount));
            }
            ctx.LogInfo(string.Format(
                @"Compacted {0} reconciled parquet(s): {1:N0} rows kept of {2:N0}.",
                done, rowsAfter, rowsBefore));
            return true;
        }

        /// <summary>
        /// Refuse to rewrite a reconciled parquet with a library other than the one that
        /// produced it.
        ///
        /// <para>Hard failure rather than a warning, and before the first write rather than
        /// per file: the damage is silent. The rewrite re-derives sequence / precursor m/z /
        /// protein_ids from the library by entry id, so a mismatched library produces a
        /// well-formed parquet in which every row names the wrong peptide, and the run that
        /// consumes it exits 0 with a confidently wrong answer. A footer with no library hash
        /// is refused for the same reason - "cannot verify" and "verified" are not the same
        /// answer when the operation is destructive and in place.</para>
        /// </summary>
        private static bool VerifyLibraryMatches(string reconciledPath,
            IReadOnlyDictionary<string, string> footer, string expectedLibraryHash,
            OspreyConfig config, PipelineContext ctx)
        {
            footer.TryGetValue(@"osprey.library_hash", out string actual);
            if (string.Equals(actual, expectedLibraryHash, StringComparison.Ordinal))
                return true;
            string libraryPath = config.LibrarySource?.Path ?? string.Empty;
            ctx.LogError(string.Format(
                string.IsNullOrEmpty(actual)
                    ? @"--task CompactPerFileRescoring: {0} carries no osprey.library_hash, so " +
                      @"the library it was produced with cannot be verified. Refusing to rewrite " +
                      @"it: the compaction re-derives every row's sequence and protein_ids from " +
                      @"the library by entry id, and entry ids are assigned at library load."
                    : @"--task CompactPerFileRescoring: {0} was produced with a DIFFERENT " +
                      @"library (footer osprey.library_hash={1}) than the one passed with -l " +
                      @"({2}, hash {3}). Refusing to rewrite it: the compaction re-derives every " +
                      @"row's sequence and protein_ids from the library by entry id, and entry " +
                      @"ids are assigned at library load, so this would rewrite every row with " +
                      @"another peptide's identity. Pass the library the run was searched with.",
                reconciledPath, actual, libraryPath, expectedLibraryHash));
            ctx.ExitCode = 1;
            return false;
        }

        /// <summary>
        /// The score parquets this run was pointed at. <c>--input-scores</c> is the form a
        /// post-Stage-6 entry point takes; a run given ordinary inputs derives the sibling
        /// score path from each one.
        /// </summary>
        private static List<string> ResolveScoresPaths(OspreyConfig config)
        {
            var paths = new List<string>();
            if (config.InputScores != null && config.InputScores.Count > 0)
            {
                paths.AddRange(config.InputScores);
                return paths;
            }
            if (config.InputFiles != null)
            {
                foreach (string input in config.InputFiles)
                    paths.Add(ParquetScoreCache.GetScoresPath(input));
            }
            return paths;
        }

        /// <summary>
        /// Compact one file: select its survivors, stream the parquet through the same writer
        /// Stage 6 uses, and swap the result in.
        ///
        /// <para>The swap is Move -> Move -> Delete rather than Delete -> Move so that a crash
        /// between the steps leaves BOTH copies rather than neither. The footer is preserved
        /// key for key except <c>osprey.reconciled</c>, so the version stamp and the search /
        /// library / reconciliation hashes still describe the run that produced the rows.</para>
        /// </summary>
        private static (int NWritten, int OrigRowCount) CompactOneFile(string fileName,
            string reconciledPath, ICollection<uint> retainBaseIds,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById, PipelineContext ctx)
        {
            var metadata = ParquetScoreCache.LoadFooterMetadata(reconciledPath);
            metadata[@"osprey.reconciled"] = ParquetScoreCache.RECONCILED_SURVIVORS;

            // ONE file's survivors resident at a time, selected by the same base_id predicate
            // Stage 5's compaction applied.
            var survivors = ParquetScoreCache.LoadFdrStubsFromParquet(reconciledPath,
                entryId => retainBaseIds.Contains(entryId & ScoringTaskShared.BASE_ID_MASK));
            var keepIdentities = new HashSet<(uint, byte, uint)>(survivors.Count);
            foreach (var entry in survivors)
                keepIdentities.Add((entry.EntryId, entry.Charge, entry.ScanNumber));
            survivors.Clear();

            string compactedPath = reconciledPath + @".compacted";
            var result = ParquetScoreCache.StreamReconciledScoresParquet(
                reconciledPath, compactedPath, null, null, metadata, libraryById,
                fileName, keepIdentities, null, ctx.LogWarning);

            string retiredPath = reconciledPath + @".retired";
            File.Move(reconciledPath, retiredPath);
            File.Move(compactedPath, reconciledPath);
            File.Delete(retiredPath);

            return (result.NWritten, result.OrigRowCount);
        }
    }
}
