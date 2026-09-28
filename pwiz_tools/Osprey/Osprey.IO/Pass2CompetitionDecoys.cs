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

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Reader / writer for the per-input-file
    /// <c>&lt;stem&gt;.2nd-pass.fdr_decoys.bin</c> artifact: THE DECOY SIDE OF THAT FILE'S
    /// SECOND-PASS COMPETITION, one record per decoy base_id, carrying the winning decoy
    /// observation's entry_id and its composite score in this file.
    ///
    /// <para><b>Why it exists (issue #4486).</b> The per-run <c>.2nd-pass.fdr_scores.bin</c> is a
    /// faithful image of the file's Stage 6 POOL. The second-pass competition is not run over the
    /// pool - it is run over the file's pre-compaction population, which is the correctness fix
    /// issue #4436 shipped: competing over the reported pool re-estimates q against a null
    /// compaction has already stripped most decoys from, which measured 1.08% (Stellar) and 1.24%
    /// (Astral) true FDP at a nominal 1%. So the winning DECOY of a base_id is routinely an
    /// observation the pool does not hold, and the pool image structurally cannot carry it.</para>
    ///
    /// <para>While the per-file half of the second pass ran inside Stage 7, that cost nothing:
    /// the join opened each file's own <c>.1st-pass.fdr_scores.bin</c> and competed there. Moving
    /// the per-file half into the rescore worker is precisely what stops the join reading those
    /// files - 52.3 GB of them on a 257-file analysis - so the answer the worker already computes
    /// has to be WRITTEN DOWN rather than recomputed by a node that may not even be the same
    /// machine. This file is that transmission. It is not new knowledge and not a new
    /// computation: it is a serialization of <c>StreamingFdr.FileCompetition.BestDecoy</c>,
    /// which the worker produces and used to throw away.</para>
    ///
    /// <para><b>The population is whatever the competition used - this artifact must not
    /// know.</b> Under the shipped <c>protein-compact</c> default the competition is restricted
    /// to the protein stratum; under <c>transfer-compete</c> it is the full pre-compaction
    /// population; under a future fix for issue #4581 it is whatever that fix decides. The
    /// producer serializes the map the competition RETURNED. Rebuilding the set here - from the
    /// 1st-pass sidecar, from the stratum, or from any description of what the population
    /// "should" be - is where that coupling would reappear, and no gate could see it: a golden
    /// comparison cannot see calibration, and an entrapment oracle cannot audit a protein-grouping
    /// prior with a per-peptide entrapment set (issue #4581).</para>
    ///
    /// <para><b>Complete, not a delta against the pool.</b> Writing only the decoys the pool does
    /// not hold would assume <c>pool + delta == competition population</c>, which is the same
    /// coupling in another form. The complete decoy side makes the file self-contained and makes
    /// the selection rule OBSERVABLE: present here means it competed, so set-differencing this
    /// against the file's 1st-pass decoy population IS the stratum-selection measurement, at any
    /// scale, without re-running to instrument. Cost is ~2 MB per Stellar file against an 11 MB
    /// per-run sidecar.</para>
    ///
    /// <para><b>Only the decoy side.</b> Run q is per observation and already in the per-run
    /// sidecar; the winning TARGET of a base_id is always one of the file's survivors - the
    /// experiment-fold scope invariant Stage 7 enforces with a throw - so it has a pool record
    /// and is recoverable from one. The decoy half is the only third of a
    /// <c>FileCompetition</c> the pool image cannot supply.</para>
    ///
    /// Format (32-byte header + N x 12-byte records, all little-endian):
    /// <code>
    ///   magic         [0..8]   = b"OSPRYDCY"
    ///   version       [8]      = u8 (= 1)
    ///   pass          [9]      = u8 (2 = second-pass)
    ///   reserved      [10..16] = 6 bytes (zero)
    ///   entry_count   [16..24] = u64
    ///   reserved      [24..32] = 8 bytes (zero)
    ///   body          [32..]   = entry_count * 12 bytes:
    ///                            [0..4]  u32 decoy entry_id (base_id = entry_id &amp; 0x7FFFFFFF)
    ///                            [4..12] f64 best composite score for that base_id in this file
    /// </code>
    ///
    /// <para>The magic differs from <c>OSPRYFDR</c> and <c>OSPRYEXP</c> for the reason those two
    /// differ from each other: these files share a header shape, so identical magic would let one
    /// be read as another and decode into plausible garbage instead of a rejection.</para>
    ///
    /// <para>No base_id column. The record's entry_id IS a decoy observation, so its base_id is
    /// the low 31 bits and storing it would be storing the same fact twice - and the two copies
    /// could then disagree. <see cref="Write"/> rejects a target entry_id rather than writing a
    /// record whose key cannot be recovered.</para>
    ///
    /// <para>Records are written in ASCENDING entry_id order, which for a file of decoys is also
    /// ascending base_id order (every decoy shares the same high bit). Canonical order makes the
    /// file a function of its contents rather than of the order the producer's map enumerated,
    /// which is what lets the straight-through and distributed routes be compared byte for
    /// byte.</para>
    /// </summary>
    public static class Pass2CompetitionDecoys
    {
        // 8-byte magic. ASCII "OSPRYDCY".
        private static readonly byte[] Magic =
            { (byte)'O', (byte)'S', (byte)'P', (byte)'R', (byte)'Y', (byte)'D', (byte)'C', (byte)'Y' };

        public const byte FormatVersion = 1;
        public const int HeaderLength = 32;
        public const int RecordLength = 12;

        /// <summary>The decoy high bit: an entry_id with it set is a decoy observation.</summary>
        private const uint DECOY_BIT = 0x80000000;

        /// <summary>
        /// Path for one input file's second-pass competition decoys:
        /// <c>&lt;dir&gt;/&lt;stem&gt;.2nd-pass.fdr_decoys.bin</c>, resolved through the same
        /// <see cref="ArtifactPaths.ResolveOutputDir"/> the per-file sidecars use so the two
        /// always land together and every <c>--task</c> phase agrees on where.
        /// </summary>
        public static string PathFor(string inputPath)
        {
            string stem = Path.GetFileNameWithoutExtension(inputPath) ?? @"unknown";
            string parent = ArtifactPaths.ResolveOutputDir(inputPath);
            string filename = string.Format("{0}.2nd-pass.fdr_decoys.bin", stem);
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
        }

        /// <summary>
        /// Whether <paramref name="path"/> is an artifact this build can consume: it exists,
        /// carries the expected magic, the current <see cref="FormatVersion"/> and the
        /// second-pass byte, and its length matches its own header count. Never throws - a
        /// missing, short, or foreign file is simply false. Same contract, and the same reasons
        /// for it, as <see cref="FdrScoresSidecar.IsCurrentFormat"/>.
        /// </summary>
        public static bool IsCurrentFormat(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return false;
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < HeaderLength)
                    return false;
                var header = new byte[HeaderLength];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!ReadFully(fs, header, HeaderLength))
                        return false;
                }
                if (!HeaderOk(header, out ulong headerCount))
                    return false;
                return TryComputeExpectedLen(headerCount, out int expectedLen) &&
                       info.Length == expectedLen;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        /// <summary>
        /// Write one file's competition decoys, one record per base_id, in ascending entry_id
        /// order. Atomic via <see cref="FileSaver"/>: the temp file is promoted on Commit, so a
        /// failure leaves any existing destination untouched.
        ///
        /// <para><paramref name="bestDecoy"/> is
        /// <c>StreamingFdr.FileCompetition.BestDecoy</c> exactly as the competition returned it -
        /// keyed by base_id, valued by the winning observation's score and entry_id. Passing a
        /// map assembled any other way is the one error this format cannot survive and no gate
        /// can see; see the remarks on the class.</para>
        /// </summary>
        public static void Write(
            string path, IReadOnlyDictionary<uint, (double score, uint entryId)> bestDecoy)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (bestDecoy == null) throw new ArgumentNullException(nameof(bestDecoy));

            // Canonical order, so the file is a function of its contents and not of the order the
            // producer's dictionary happened to enumerate. Sorting the ENTRY IDS (rather than the
            // base_id keys) is what the reader re-derives its keys from, so the two orders cannot
            // disagree; for a file of decoys the two sorts are the same permutation anyway.
            var ids = new uint[bestDecoy.Count];
            var scores = new double[bestDecoy.Count];
            int n = 0;
            foreach (var kv in bestDecoy)
            {
                uint entryId = kv.Value.entryId;
                // A TARGET here would silently key the reader's map to the wrong base_id -
                // (entryId & ~DECOY_BIT) would equal a target's own base_id, so the file would
                // claim a decoy best that is a target observation. That is the exact shape of
                // "the file looks plausible and holds the wrong population", so it fails the run.
                if ((entryId & DECOY_BIT) == 0u)
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass competition decoys for {0}: base_id {1} names entry_id {2}, " +
                        @"which is not a decoy observation. This artifact carries the decoy side " +
                        @"of the competition only. See issue #4486.",
                        path, kv.Key, entryId));
                }
                // The key is redundant with the entry_id and is therefore not stored - but the
                // producer computed them independently, so disagreement means the map was
                // assembled rather than serialized, and the reader would key on the wrong
                // base_id for the rest of the run.
                if ((entryId & ~DECOY_BIT) != kv.Key)
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass competition decoys for {0}: entry_id {1} does not belong to " +
                        @"base_id {2} it is filed under. See issue #4486.",
                        path, entryId, kv.Key));
                }
                ids[n] = entryId;
                scores[n] = kv.Value.score;
                n++;
            }
            Array.Sort(ids, scores); // Array.Sort OK: one record per base_id and the decoy bit is constant, so the keys are distinct and never tie

            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            using (var saver = new FileSaver(path))
            {
                using (var fs = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(Magic);                // [0..8]
                    bw.Write(FormatVersion);        // [8]
                    bw.Write((byte)FdrScoresSidecar.Pass.SecondPass); // [9]
                    bw.Write(new byte[6]);          // [10..16] reserved
                    bw.Write((ulong)ids.Length);    // [16..24]
                    bw.Write(new byte[8]);          // [24..32] reserved

                    for (int i = 0; i < ids.Length; i++)
                    {
                        bw.Write(ids[i]);           // [0..4]
                        bw.Write(scores[i]);        // [4..12]
                    }
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Read one file's competition decoys back into the base_id-keyed shape
        /// <c>StreamingFdr.FileCompetition.BestDecoy</c> has. Returns null on a missing file or
        /// any header / size mismatch, so a caller cannot mistake an unreadable artifact for a
        /// file whose competition found no decoys - the two have to be distinguishable, because
        /// the second silently empties the null the experiment-wide q is computed against.
        /// </summary>
        public static Dictionary<uint, (double score, uint entryId)> ReadMap(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            try
            {
                using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (src.Length < HeaderLength)
                        return null;
                    var header = new byte[HeaderLength];
                    if (!ReadFully(src, header, HeaderLength))
                        return null;
                    if (!HeaderOk(header, out ulong headerCount))
                        return null;
                    if (!TryComputeExpectedLen(headerCount, out int expectedLen) ||
                        src.Length != expectedLen)
                    {
                        return null;
                    }
                    var map = new Dictionary<uint, (double score, uint entryId)>((int)headerCount);
                    var record = new byte[RecordLength];
                    for (ulong rec = 0; rec < headerCount; rec++)
                    {
                        if (!ReadFully(src, record, RecordLength))
                            return null;
                        uint entryId = BitConverter.ToUInt32(record, 0);
                        map[entryId & ~DECOY_BIT] = (BitConverter.ToDouble(record, 4), entryId);
                    }
                    return map;
                }
            }
            // NOT a bare catch, for the reason every reader in FdrScoresSidecar gives: an
            // OutOfMemoryException reported as a MISSING artifact would let the run continue with
            // a decoy-depleted null and report the q-values it produced.
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return null;
            }
        }

        /// <summary>
        /// Validate the 32-byte header: magic, current version, second-pass byte. Shared by every
        /// entry point so they cannot drift on what they accept.
        /// </summary>
        private static bool HeaderOk(byte[] header, out ulong headerCount)
        {
            headerCount = 0;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (header[i] != Magic[i])
                    return false;
            }
            if (header[8] != FormatVersion ||
                header[9] != (byte)FdrScoresSidecar.Pass.SecondPass)
            {
                return false;
            }
            headerCount = BitConverter.ToUInt64(header, 16);
            return true;
        }

        /// <summary>
        /// Compute <c>HeaderLength + headerCount * RecordLength</c> with overflow detection, so a
        /// corrupt count cannot wrap int and let the size check pass spuriously.
        /// </summary>
        private static bool TryComputeExpectedLen(ulong headerCount, out int expectedLen)
        {
            try
            {
                expectedLen = checked(HeaderLength + (int)headerCount * RecordLength);
                return true;
            }
            catch (OverflowException)
            {
                expectedLen = 0;
                return false;
            }
        }

        /// <summary>
        /// Fill <paramref name="buffer"/> with exactly <paramref name="count"/> bytes, looping
        /// because one <see cref="Stream.Read(byte[],int,int)"/> may return fewer than asked.
        /// </summary>
        private static bool ReadFully(Stream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, count - read);
                if (n <= 0)
                    return false;
                read += n;
            }
            return true;
        }
    }
}
