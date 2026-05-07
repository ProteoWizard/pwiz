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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
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
    /// so a rehydrated worker couldn't reproduce in-process compaction
    /// when <c>--protein-fdr</c> is set. v3 closes that gap.
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
            string parent = Path.GetDirectoryName(inputPath);
            string filename = string.Format("{0}.{1}.fdr_scores.bin", stem, passLabel);
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
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

            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            // Stage to sibling .tmp file then rename. Avoids partial-file
            // corruption if the writer is interrupted; not strictly
            // atomic on overwrite (delete + move), see Write() doc.
            string tmpPath = path + ".tmp";
            try
            {
                using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    // Header
                    bw.Write(Magic);                                  // [0..8]
                    bw.Write(FormatVersion);                          // [8]
                    bw.Write((byte)pass);                             // [9]
                    bw.Write(new byte[6]);                            // [10..16] reserved
                    bw.Write((ulong)entries.Count);                   // [16..24]
                    bw.Write(new byte[8]);                            // [24..32] reserved

                    // Body: 60 bytes per entry (entry_id + 7 f64s)
                    foreach (var e in entries)
                    {
                        bw.Write(e.EntryId);                          // [0..4]
                        bw.Write(e.Score);                            // [4..12]
                        bw.Write(e.RunPrecursorQvalue);               // [12..20]
                        bw.Write(e.RunPeptideQvalue);                 // [20..28]
                        bw.Write(e.ExperimentPrecursorQvalue);        // [28..36]
                        bw.Write(e.ExperimentPeptideQvalue);          // [36..44]
                        bw.Write(e.Pep);                              // [44..52]
                        bw.Write(e.RunProteinQvalue);                 // [52..60]
                    }
                }

                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmpPath, path);
            }
            finally
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }
        }

        /// <summary>
        /// Read per-file FDR scores from <paramref name="path"/> into
        /// <paramref name="entries"/>. Returns true on success, false on
        /// any of: missing file, bad magic, unsupported version, pass-byte
        /// mismatch against <paramref name="expectedPass"/>, count
        /// mismatch, or size mismatch. Records are positional, so the
        /// caller's <paramref name="entries"/> list must already be sized
        /// to match the FdrEntry sequence the sidecar was written from
        /// (pre-compaction at the Stage 5 → Stage 6 boundary).
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
            if (headerCount != (ulong)entries.Count)
                return false;

            int expectedLen = HeaderLength + entries.Count * RecordLength;
            if (data.Length != expectedLen)
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                int off = HeaderLength + i * RecordLength;
                var e = entries[i];
                uint recordEntryId = BitConverter.ToUInt32(data, off + 0);
                if (recordEntryId != e.EntryId)
                    return false;
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
