/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
    /// Reader / writer for the per-file <c>.&lt;phase&gt;-pass.fdr_scores.bin</c>
    /// sidecar: the v3 binary format that persists the full FDR statistics
    /// for an entry (SVM discriminant + 4 q-values + PEP +
    /// <c>run_protein_qvalue</c>). Used at the Stage 5 → Stage 6 boundary
    /// so a Stage 6 worker can run without re-running first-pass Percolator
    /// AND apply the same protein-rescue compaction predicate the in-process
    /// pipeline uses.
    ///
    /// Mirrors <c>write_fdr_scores_sidecar</c> + <c>load_fdr_scores_sidecar</c>
    /// in <c>osprey/crates/osprey/src/pipeline.rs</c>. Cross-impl byte
    /// parity is verified by a separate harness script via the
    /// <c>OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT</c> test hook.
    ///
    /// Format (32-byte header + N × 60-byte records, all little-endian):
    /// <code>
    ///   magic         [0..8]   = b"OSPRYFDR"
    ///   version       [8]      = u8 (= 3)
    ///   pass          [9]      = u8 (1 = first-pass, 2 = second-pass)
    ///   reserved      [10..16] = 6 bytes (zero)
    ///   entry_count   [16..24] = u64
    ///   reserved      [24..32] = 8 bytes (zero)
    ///   body          [32..]   = entry_count * 60 bytes:
    ///                            [0..4]   u32 entry_id
    ///                            [4..12]  f64 svm_score
    ///                            [12..20] f64 run_precursor_qvalue
    ///                            [20..28] f64 run_peptide_qvalue
    ///                            [28..36] f64 experiment_precursor_qvalue
    ///                            [36..44] f64 experiment_peptide_qvalue
    ///                            [44..52] f64 pep
    ///                            [52..60] f64 run_protein_qvalue
    /// </code>
    /// Records are written pre-compaction but POST first-pass protein
    /// FDR at the Stage 5 → Stage 6 boundary: every input entry
    /// contributes one record so q-values are preserved even for
    /// entries that may not survive later compaction, AND so
    /// <c>run_protein_qvalue</c> carries real values rather than the
    /// default 1.0. Mirrors the post-protein-FDR
    /// <c>persist_fdr_scores</c> call site in Rust's
    /// <c>pipeline.rs</c>. Each record carries the entry's
    /// <c>entry_id</c> for identity verification (the per-position
    /// <c>entries[i].EntryId == record.entry_id</c> check during load
    /// doubles as a corruption detector); the loader matches records
    /// to stubs by position + count rather than by joining on
    /// <c>entry_id</c>. A Stage 6 worker therefore consumes the
    /// sidecar by reloading the same FdrEntry sequence from the
    /// per-file parquet cache and applying records in order. The
    /// loader also rejects mismatches on the header <c>pass</c> byte
    /// so a 2nd-pass sidecar can never silently scramble 1st-pass
    /// stubs (or vice versa).
    ///
    /// v2 → v3 (2026-05-02): added <c>run_protein_qvalue</c> to
    /// support the Stage 6 worker's compaction step. The in-process
    /// pipeline filters pre-Stage-6 entries by
    /// <c>run_peptide_qvalue ≤ 0.01</c> OR
    /// <c>run_protein_qvalue ≤ 0.01</c> (the protein-rescue branch);
    /// the v2 sidecar carried only the first half of that predicate,
    /// so a rehydrated worker couldn't reproduce the protein-rescue
    /// half of in-process compaction. v3 closes that gap.
    /// </summary>
    public static class FdrScoresSidecar
    {
        // 8-byte magic. ASCII "OSPRYFDR" — same as Rust.
        private static readonly byte[] Magic =
            { (byte)'O', (byte)'S', (byte)'P', (byte)'R', (byte)'Y', (byte)'F', (byte)'D', (byte)'R' };

        public const byte FormatVersion = 3;
        public const int HeaderLength = 32;
        public const int RecordLength = 60;

        /// <summary>
        /// Pass identifier embedded in the header. Mirrors the Rust pass
        /// byte semantics: 1 = first-pass Percolator, 2 = second-pass
        /// Percolator.
        /// </summary>
        public enum Pass : byte
        {
            FirstPass = 1,
            SecondPass = 2,
        }

        /// <summary>
        /// Compute <c>HeaderLength + headerCount * RecordLength</c> with
        /// overflow detection. Returns false if the result would not fit
        /// in <see cref="int"/> (a corrupt or malicious sidecar with a
        /// huge headerCount would otherwise wrap silently and let the
        /// size check pass spuriously, leading to out-of-bounds reads in
        /// the record loop).
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
        /// Path for the first-pass FDR scores sidecar of a given input
        /// file: <c>&lt;dir&gt;/&lt;stem&gt;.1st-pass.fdr_scores.bin</c>.
        /// Mirrors Rust's <c>fdr_scores_path_pass1</c>.
        /// </summary>
        public static string Pass1Path(string inputPath)
        {
            return ScoresPath(inputPath, "1st-pass");
        }

        /// <summary>Path for the second-pass FDR scores sidecar.</summary>
        public static string Pass2Path(string inputPath)
        {
            return ScoresPath(inputPath, "2nd-pass");
        }

        private static string ScoresPath(string inputPath, string passLabel)
        {
            string stem = Path.GetFileNameWithoutExtension(inputPath) ?? "unknown";
            // Route through ArtifactPaths so the sidecar follows the scores
            // parquet into --output-dir (default = the input's own directory).
            // Every caller -- straight-through writes, resume reads, and the
            // resume-check iterators -- shares this, so they stay consistent.
            string parent = ArtifactPaths.ResolveOutputDir(inputPath);
            string filename = string.Format("{0}.{1}.fdr_scores.bin", stem, passLabel);
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
        }

        /// <summary>
        /// Read every record's (entry_id, raw SVM score) from a scores sidecar into flat
        /// arrays, in file (write) order. entry_id is unique per file (one FdrEntry per
        /// precursor per file), so callers can key by (file, entry_id). Used by
        /// OSPREY_PASS2_QVALUE=transfer-compete to recompete over the FULL 1st-pass population
        /// from the persisted scalars without re-reading features -- only the entry_id [0..4]
        /// and score [4..12] fields are read; the trailing q-values are skipped.
        /// </summary>
        public static void ReadScalars(string path, out uint[] entryIds, out double[] scores)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long len = fs.Length;
                if (len < HeaderLength)
                    throw new IOException(string.Format(
                        "FdrScoresSidecar too short ({0} bytes): {1}", len, path));
                int n = (int)((len - HeaderLength) / RecordLength);
                entryIds = new uint[n];
                scores = new double[n];
                var header = new byte[HeaderLength];
                if (!ReadFully(fs, header, HeaderLength))
                    throw new IOException("FdrScoresSidecar header truncated: " + path);
                var rec = new byte[RecordLength];
                for (int i = 0; i < n; i++)
                {
                    if (!ReadFully(fs, rec, RecordLength))
                        throw new IOException(string.Format(
                            "FdrScoresSidecar truncated at record {0}: {1}", i, path));
                    entryIds[i] = BitConverter.ToUInt32(rec, 0);
                    scores[i] = BitConverter.ToDouble(rec, 4);
                }
            }
        }

        /// <summary>
        /// Write per-file FDR scores to <paramref name="path"/>. Stages
        /// through a sibling <c>.tmp</c> file in the same directory and
        /// renames into place; this avoids leaving a partially-written
        /// destination on writer failure, but the rename is not strictly
        /// atomic when the destination already exists (the existing file
        /// is removed first). A crash between the remove and the rename
        /// leaves the <c>.tmp</c> next to the missing destination, which
        /// the next run either overwrites or — if the writer fails
        /// identically — leaves recoverable by hand. The pass byte
        /// distinguishes first- vs second-pass outputs at the Percolator
        /// level.
        /// </summary>
        public static void Write(string path, IReadOnlyList<FdrEntry> entries, Pass pass)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            WriteInternal(path, entries.Count, pass, bw =>
            {
                foreach (var e in entries)
                {
                    WriteRecord(bw, e.EntryId, e.Score,
                        e.RunPrecursorQvalue, e.RunPeptideQvalue,
                        e.ExperimentPrecursorQvalue, e.ExperimentPeptideQvalue,
                        e.Pep, e.RunProteinQvalue);
                }
            });
        }

        /// <summary>
        /// Projection-buffer counterpart of
        /// <see cref="Write(string, IReadOnlyList{FdrEntry}, Pass)"/> (issue #4355
        /// struct-shrink S0): write the per-file sidecar from pre-assembled
        /// <see cref="FdrScoreRecord"/>s. Because the lean <c>FdrProjection</c> no longer
        /// carries the q-value outputs, the projection sidecar writers assemble each
        /// record from the lean row's EntryId + Score plus the parked / streamed
        /// q-values (1st pass) or the streamed q-values + the survivor's
        /// <c>RunProteinQvalue</c> lookup (2nd pass), then pass them here. Single-phase
        /// write producing byte-identical 60-byte records in the given
        /// (per-file, projection) order (risk #8). Header + record layout are
        /// single-sourced with the FdrEntry overload via
        /// <see cref="WriteInternal"/> / <see cref="WriteRecord"/>.
        /// </summary>
        public static void Write(string path, IReadOnlyList<FdrScoreRecord> records, Pass pass)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (records == null) throw new ArgumentNullException(nameof(records));

            WriteInternal(path, records.Count, pass, bw =>
            {
                foreach (var r in records)
                {
                    WriteRecord(bw, r.EntryId, r.Score,
                        r.RunPrecursorQvalue, r.RunPeptideQvalue,
                        r.ExperimentPrecursorQvalue, r.ExperimentPeptideQvalue,
                        r.Pep, r.RunProteinQvalue);
                }
            });
        }

        /// <summary>
        /// Phase 2 of the two-phase 1st-pass sidecar write (issue #4355 struct-shrink
        /// S1): patch each record's <c>run_protein_qvalue</c> field <c>[52..60]</c> in
        /// place on an already-written (phase-1 partial) sidecar, locating each record
        /// by its <c>entry_id</c> at <c>[0..4]</c> in
        /// <paramref name="runProteinByEntryId"/>. The phase-1 write (via the second
        /// <see cref="Write(string, IReadOnlyList{FdrScoreRecord}, Pass)"/> overload)
        /// emits a byte-identical file EXCEPT for the <c>run_protein_qvalue</c> column,
        /// which carries the 1.0 placeholder until first-pass protein FDR is known; this
        /// patch overwrites only those 8 bytes per record with the finalized value, so
        /// the resulting file is byte-identical to a single-phase <c>Write</c> whose
        /// records already carried the real <c>run_protein_qvalue</c> (risk R2). Records
        /// whose <c>entry_id</c> is absent from the map keep their placeholder (every
        /// 1st-pass row has a resident <c>run_protein_qvalue</c>, so that is defensive
        /// only). The 8-byte little-endian f64 encoding matches
        /// <see cref="WriteRecord"/>'s <c>BinaryWriter.Write(double)</c> (the platform is
        /// little-endian, as the <see cref="BitConverter.ToDouble(byte[], int)"/> reads
        /// in <see cref="TryRead"/> already assume). Same header validation as
        /// <see cref="TryRead"/> (magic / version / pass / size); returns <c>false</c> on
        /// any mismatch or IO failure, leaving the file unchanged. Streams the source one
        /// 60-byte record at a time straight into the <see cref="FileSaver"/> temp stream
        /// (bounded memory -- one record resident, not an O(file-size) whole-file buffer;
        /// issue #4355) and promotes it atomically on Commit, matching the <c>Write</c> path.
        /// </summary>
        public static bool PatchRunProteinQvalues(
            string path,
            IReadOnlyDictionary<uint, double> runProteinByEntryId,
            Pass expectedPass)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (runProteinByEntryId == null) throw new ArgumentNullException(nameof(runProteinByEntryId));

            try
            {
                using (var saver = new FileSaver(path))
                {
                    // Source stays open only while streaming; the FileSaver Commit that
                    // deletes+replaces the source runs AFTER this block closes src, so the
                    // source is never locked at rename time.
                    using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var dst = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        long fileLen = src.Length;
                        if (fileLen < HeaderLength)
                            return false;

                        // Read + validate the 32-byte header, then copy it through
                        // byte-for-byte. Same checks (magic / version / pass / size) as
                        // TryRead; on any mismatch we return false before Commit, so the
                        // source file is left unchanged (the temp is discarded on dispose).
                        var header = new byte[HeaderLength];
                        if (!ReadFully(src, header, HeaderLength))
                            return false;
                        for (int i = 0; i < Magic.Length; i++)
                        {
                            if (header[i] != Magic[i])
                                return false;
                        }
                        if (header[8] != FormatVersion)
                            return false;
                        // Reject a mismatched pass byte for the same reason TryRead does: a
                        // 2nd-pass sidecar must never be patched as if it were 1st-pass.
                        if (header[9] != (byte)expectedPass)
                            return false;
                        ulong headerCount = BitConverter.ToUInt64(header, 16);
                        if (!TryComputeExpectedLen(headerCount, out int expectedLen))
                            return false;
                        if (fileLen != expectedLen)
                            return false;

                        dst.Write(header, 0, HeaderLength);

                        // Stream one 60-byte record at a time: overwrite ONLY the
                        // run_protein_qvalue bytes [52..60] with the finalized value
                        // looked up by entry_id [0..4], in the identical little-endian f64
                        // encoding BinaryWriter.Write(double) produced for every other
                        // field; every other byte is copied straight through. A record
                        // whose entry_id is absent from the map keeps its placeholder
                        // (defensive -- every 1st-pass row has a resident value). The
                        // output is byte-identical to a single-phase Write carrying the
                        // real value, but only one record is resident at a time instead of
                        // the whole file (issue #4355).
                        var record = new byte[RecordLength];
                        for (int rec = 0; rec < (int)headerCount; rec++)
                        {
                            if (!ReadFully(src, record, RecordLength))
                                return false;
                            uint recordEntryId = BitConverter.ToUInt32(record, 0);
                            if (runProteinByEntryId.TryGetValue(recordEntryId, out double runProteinQvalue))
                            {
                                byte[] bytes = BitConverter.GetBytes(runProteinQvalue);
                                Buffer.BlockCopy(bytes, 0, record, 52, 8);
                            }
                            dst.Write(record, 0, RecordLength);
                        }
                    }
                    saver.Commit();
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Fill <paramref name="buffer"/> with exactly <paramref name="count"/> bytes from
        /// <paramref name="stream"/>, looping because a single
        /// <see cref="Stream.Read(byte[],int,int)"/> may return fewer bytes than
        /// requested. Returns <c>false</c> if the stream ends first (a truncated or
        /// corrupt sidecar), matching the whole-file loader's size-mismatch rejection.
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

        /// <summary>
        /// Shared header + atomic-write scaffold for both <c>Write</c>
        /// overloads. The caller supplies the body writer, which emits exactly
        /// <paramref name="entryCount"/> 60-byte records via
        /// <see cref="WriteRecord"/>. Atomic write via FileSaver: write to a unique
        /// sibling temp file and promote it to the destination on Commit; on
        /// exception the FileSaver disposes and deletes the temp without touching
        /// the destination. The FileStream is disposed before Commit so the file is
        /// unlocked when File.Move runs.
        /// </summary>
        private static void WriteInternal(
            string path, int entryCount, Pass pass, Action<BinaryWriter> writeBody)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            using (var saver = new FileSaver(path))
            {
                using (var fs = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    // Header
                    bw.Write(Magic);                                  // [0..8]
                    bw.Write(FormatVersion);                          // [8]
                    bw.Write((byte)pass);                             // [9]
                    bw.Write(new byte[6]);                            // [10..16] reserved
                    bw.Write((ulong)entryCount);                      // [16..24]
                    bw.Write(new byte[8]);                            // [24..32] reserved

                    writeBody(bw);
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Write one 60-byte record (entry_id + 7 f64s, little-endian) in the exact
        /// v3 field order. Single-sourced so the FdrEntry and FdrProjection write
        /// paths cannot drift on byte layout.
        /// </summary>
        private static void WriteRecord(
            BinaryWriter bw, uint entryId, double score,
            double runPrecursorQvalue, double runPeptideQvalue,
            double experimentPrecursorQvalue, double experimentPeptideQvalue,
            double pep, double runProteinQvalue)
        {
            bw.Write(entryId);                          // [0..4]
            bw.Write(score);                            // [4..12]
            bw.Write(runPrecursorQvalue);               // [12..20]
            bw.Write(runPeptideQvalue);                 // [20..28]
            bw.Write(experimentPrecursorQvalue);        // [28..36]
            bw.Write(experimentPeptideQvalue);          // [36..44]
            bw.Write(pep);                              // [44..52]
            bw.Write(runProteinQvalue);                 // [52..60]
        }

        /// <summary>
        /// Read per-file FDR scores from <paramref name="path"/> into
        /// <paramref name="entries"/>. Returns true on success, false on
        /// any of: missing file, bad magic, unsupported version, pass-byte
        /// mismatch against <paramref name="expectedPass"/>, or size
        /// mismatch (file length doesn't match header count + record
        /// width).
        ///
        /// Records are matched to entries by <c>entry_id</c>, NOT by
        /// position. This handles two multi-file realities:
        /// <list type="bullet">
        /// <item>The 1st-pass sidecar is written PRE-gap-fill (stage 5
        /// boundary), so its row count is smaller than the reconciled
        /// parquet's row count after Stage 6 appends gap-fill stubs.
        /// Gap-fill stubs in <paramref name="entries"/> get no record
        /// applied — they keep their default (Score=0, q=1) values.</item>
        /// <item>The 2nd-pass sidecar is written POST-compaction +
        /// post-rescore + post-gap-fill, so its row count is smaller
        /// than the full reconciled parquet's row count. Entries not
        /// in the sidecar get no record applied.</item>
        /// </list>
        /// Single-file (or any case where sidecar count == entry count)
        /// degenerates to position-based loading because the entry_id
        /// dictionary lookup matches one-to-one. The original strict
        /// position+entry_id check this method previously enforced was
        /// hiding the post-compaction / post-gap-fill mismatch on
        /// multi-file runs (1644-row delta on Stellar 3-file
        /// stage6_rescored.tsv was rooted in the 1st-pass loader
        /// rejecting Rust's pre-gap-fill sidecar against the post-gap-
        /// fill parquet load).
        /// </summary>
        public static bool TryRead(string path, IList<FdrEntry> entries, Pass expectedPass)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch
            {
                return false;
            }

            if (data.Length < HeaderLength)
                return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                    return false;
            }
            byte version = data[8];
            if (version != FormatVersion)
                return false;
            // Reject mismatched pass bytes so a 2nd-pass sidecar can never
            // be silently loaded into 1st-pass stubs (or vice versa) — the
            // q-values would scramble without any visible error.
            byte passByte = data[9];
            if (passByte != (byte)expectedPass)
                return false;
            // bytes 10..16 reserved, ignored
            ulong headerCount = BitConverter.ToUInt64(data, 16);
            // Reject sidecars whose declared count exceeds physical
            // record capacity. (headerCount can validly be < entries
            // count — see comment above on pre-gap-fill / post-
            // compaction sidecars.) Use checked arithmetic so a
            // corrupt or malicious sidecar with a huge headerCount
            // is rejected loudly instead of wrapping int silently.
            if (!TryComputeExpectedLen(headerCount, out int expectedLen))
                return false;
            if (data.Length != expectedLen)
                return false;

            // Build lookup so position-skewed entries align by entry_id.
            // Single-file degenerates to a 1:1 map (no perf cost vs the
            // old positional walk).
            var byEntryId = new Dictionary<uint, int>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                byEntryId[entries[i].EntryId] = i;

            for (int rec = 0; rec < (int)headerCount; rec++)
            {
                int off = HeaderLength + rec * RecordLength;
                uint recordEntryId = BitConverter.ToUInt32(data, off + 0);
                if (!byEntryId.TryGetValue(recordEntryId, out int entryIdx))
                {
                    // Sidecar carries an entry the caller's stub list
                    // doesn't contain. The caller is expected to pass
                    // a SUPERSET of the sidecar's entries (the post-
                    // rescore parquet for the 1st-pass sidecar, for
                    // example) — a record that fails to find its
                    // entry_id signals the sidecar was written from a
                    // different parquet (or from a different binary
                    // version with different entry_id assignment). That
                    // is corruption, not the gap-fill or post-compaction
                    // case we tolerate, and must be rejected.
                    return false;
                }
                var e = entries[entryIdx];
                e.Score                       = BitConverter.ToDouble(data, off + 4);
                e.RunPrecursorQvalue          = BitConverter.ToDouble(data, off + 12);
                e.RunPeptideQvalue            = BitConverter.ToDouble(data, off + 20);
                e.ExperimentPrecursorQvalue   = BitConverter.ToDouble(data, off + 28);
                e.ExperimentPeptideQvalue     = BitConverter.ToDouble(data, off + 36);
                e.Pep                         = BitConverter.ToDouble(data, off + 44);
                e.RunProteinQvalue            = BitConverter.ToDouble(data, off + 52);
            }
            return true;
        }

        /// <summary>
        /// Overlay sidecar scores onto <paramref name="entriesByEntryId"/>
        /// without requiring a parquet stub list. Same validation rules as
        /// <see cref="TryRead(string,IList{FdrEntry},Pass)"/>, but the
        /// caller supplies an entry_id-keyed dictionary directly so we
        /// skip rereading the source parquet just to size-check the
        /// sidecar. Used by --task SecondPassFDR Stage 7 where the compacted
        /// entry list already covers every sidecar record we care about.
        /// </summary>
        public static bool TryReadOverlay(string path,
            IDictionary<uint, FdrEntry> entriesByEntryId, Pass expectedPass)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (entriesByEntryId == null) throw new ArgumentNullException(nameof(entriesByEntryId));

            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch
            {
                return false;
            }

            if (data.Length < HeaderLength)
                return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                    return false;
            }
            byte version = data[8];
            if (version != FormatVersion)
                return false;
            byte passByte = data[9];
            if (passByte != (byte)expectedPass)
                return false;
            ulong headerCount = BitConverter.ToUInt64(data, 16);
            if (!TryComputeExpectedLen(headerCount, out int expectedLen))
                return false;
            if (data.Length != expectedLen)
                return false;

            for (int rec = 0; rec < (int)headerCount; rec++)
            {
                int off = HeaderLength + rec * RecordLength;
                uint recordEntryId = BitConverter.ToUInt32(data, off + 0);
                if (!entriesByEntryId.TryGetValue(recordEntryId, out FdrEntry e))
                {
                    // Sidecar can carry entries not in the (possibly
                    // compacted) caller dict — that's expected for
                    // --task SecondPassFDR where compaction has already
                    // dropped failing precursors. Skip silently.
                    continue;
                }
                e.Score                       = BitConverter.ToDouble(data, off + 4);
                e.RunPrecursorQvalue          = BitConverter.ToDouble(data, off + 12);
                e.RunPeptideQvalue            = BitConverter.ToDouble(data, off + 20);
                e.ExperimentPrecursorQvalue   = BitConverter.ToDouble(data, off + 28);
                e.ExperimentPeptideQvalue     = BitConverter.ToDouble(data, off + 36);
                e.Pep                         = BitConverter.ToDouble(data, off + 44);
                e.RunProteinQvalue            = BitConverter.ToDouble(data, off + 52);
            }
            return true;
        }
    }
}
