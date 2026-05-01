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
    /// sidecar: the v2 binary format that persists the full FDR statistics
    /// for an entry (SVM discriminant + 4 q-values + PEP). Used at the
    /// Stage 5 → Stage 6 boundary so a Stage 6 worker can run without
    /// re-running first-pass Percolator.
    ///
    /// Mirrors <c>write_fdr_scores_sidecar</c> + <c>load_fdr_scores_sidecar</c>
    /// in <c>osprey/crates/osprey/src/pipeline.rs</c>. Cross-impl byte
    /// parity is verified by a separate harness script.
    ///
    /// Format (32-byte header + N × 52-byte records, all little-endian):
    /// <code>
    ///   magic         [0..8]   = b"OSPRYFDR"
    ///   version       [8]      = u8 (= 2)
    ///   pass          [9]      = u8 (1 = first-pass, 2 = second-pass)
    ///   reserved      [10..16] = 6 bytes (zero)
    ///   entry_count   [16..24] = u64
    ///   reserved      [24..32] = 8 bytes (zero)
    ///   body          [32..]   = entry_count * 52 bytes:
    ///                            u32 entry_id
    ///                            f64 svm_score
    ///                            f64 run_precursor_qvalue
    ///                            f64 run_peptide_qvalue
    ///                            f64 experiment_precursor_qvalue
    ///                            f64 experiment_peptide_qvalue
    ///                            f64 pep
    /// </code>
    /// Records are post-compaction: every record corresponds to a stub
    /// that passed first-pass FDR. The <c>entry_id</c> on each record
    /// lets a Stage 6 worker assemble the post-compaction stub set by
    /// joining the full parquet against the sidecar's entry_id set —
    /// the worker does not need to re-run Percolator. In skip-Percolator
    /// mode the order also matches the in-memory list position-for-
    /// position, so the per-position
    /// <c>entries[i].EntryId == record.entry_id</c> check doubles as a
    /// corruption detector.
    /// </summary>
    public static class FdrScoresSidecar
    {
        // 8-byte magic. ASCII "OSPRYFDR" — same as Rust.
        private static readonly byte[] Magic =
            { (byte)'O', (byte)'S', (byte)'P', (byte)'R', (byte)'Y', (byte)'F', (byte)'D', (byte)'R' };

        public const byte FormatVersion = 2;
        public const int HeaderLength = 32;
        public const int RecordLength = 52;

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
        /// through a temp file in the same directory, then atomically
        /// renames. The pass byte distinguishes first- vs second-pass
        /// outputs at the Percolator level.
        /// </summary>
        public static void Write(string path, IReadOnlyList<FdrEntry> entries, Pass pass)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            // Stage to sibling tmp file then atomically rename. Avoids
            // partial-file corruption if the writer is interrupted.
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

                    // Body: 52 bytes per entry (entry_id + 6 f64s)
                    foreach (var e in entries)
                    {
                        bw.Write(e.EntryId);                          // [0..4]
                        bw.Write(e.Score);                            // [4..12]
                        bw.Write(e.RunPrecursorQvalue);               // [12..20]
                        bw.Write(e.RunPeptideQvalue);                 // [20..28]
                        bw.Write(e.ExperimentPrecursorQvalue);        // [28..36]
                        bw.Write(e.ExperimentPeptideQvalue);          // [36..44]
                        bw.Write(e.Pep);                              // [44..52]
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
        /// any of: missing file, bad magic, unsupported version, count
        /// mismatch, or size mismatch. Records are positional, so the
        /// caller's <paramref name="entries"/> Vec must already be sized
        /// to match the post-compaction stub set.
        /// </summary>
        public static bool TryRead(string path, IList<FdrEntry> entries)
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
            }
            return true;
        }
    }
}
