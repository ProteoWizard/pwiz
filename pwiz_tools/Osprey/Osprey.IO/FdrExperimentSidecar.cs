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
    /// Reader / writer for the ANALYSIS-wide <c>&lt;blib-stem&gt;.&lt;phase&gt;-pass.fdr_experiment.bin</c>
    /// sidecar: one record per DISTINCT entry_id carrying the four EXPERIMENT-scope columns
    /// (<see cref="FdrExperimentRecord"/>). The scope counterpart of
    /// <see cref="FdrScoresSidecar"/>, which carries the RUN-scope columns once per OBSERVATION
    /// in a per-input-file sidecar.
    ///
    /// <para>Why the split (issue #4486). An experiment q-value is a property of the precursor
    /// for the whole analysis, so storing it beside the run-scope columns wrote the same number
    /// once per run the precursor appeared in. On a 257-file CHS analysis that is 768 M records
    /// of duplication - 52.3 GB of 1st-pass sidecars - against ~12.3 M distinct entry_ids
    /// (targets + decoys over a 6.18 M-entry library), 0.44 GB, here. It also made the per-file
    /// sidecar MUTABLE: the experiment columns are the only ones not knowable when a file's
    /// records are written, so the pipeline wrote each file and then rewrote it, twice, to push
    /// experiment values back in. With the scope split those rewrites are gone and a per-file
    /// sidecar is written once and never revisited.</para>
    ///
    /// <para><b>Naming.</b> The file takes the OUTPUT BLIB's base name and sits beside the blib,
    /// because it belongs to the analysis rather than to any input file, and a fixed prefix
    /// (<c>experiment.</c>) would collide the moment two analyses share an output directory -
    /// which the standard run layout does. <c>OspreyConfig.OutputBlib</c> is a run parameter
    /// every node already has, so a distributed <c>--task</c> worker locates this file with no
    /// new plumbing. The <c>fdr_experiment</c> token (rather than reusing <c>fdr_scores</c>)
    /// makes a name collision with a per-file sidecar structurally impossible even when the blib
    /// is named after one of the input files - which is a real configuration, and a guard that
    /// cannot fire is better than one that has to be checked.</para>
    ///
    /// Format (32-byte header + N x 36-byte records, all little-endian):
    /// <code>
    ///   magic         [0..8]   = b"OSPRYEXP"
    ///   version       [8]      = u8 (= 1)
    ///   pass          [9]      = u8 (1 = first-pass, 2 = second-pass)
    ///   reserved      [10..16] = 6 bytes (zero)
    ///   entry_count   [16..24] = u64
    ///   reserved      [24..32] = 8 bytes (zero)
    ///   body          [32..]   = entry_count * 36 bytes:
    ///                            [0..4]   u32 entry_id
    ///                            [4..12]  f64 experiment_precursor_qvalue
    ///                            [12..20] f64 experiment_peptide_qvalue
    ///                            [20..28] f64 experiment_protein_qvalue
    ///                            [28..36] f64 experiment_aggregate_score
    /// </code>
    ///
    /// <para>The magic differs from the per-file sidecar's <c>OSPRYFDR</c> deliberately: the two
    /// files share a header shape and a pass byte, so identical magic would let one be read as
    /// the other and decode into plausible garbage rather than a rejection.</para>
    ///
    /// <para>Records are written in ASCENDING entry_id order, which is what makes the file a
    /// function of its contents rather than of the order the producer happened to accumulate
    /// them. The straight-through pipeline and the distributed task chain build the map by
    /// walking files in different orders, and the regression gate compares the two artifacts
    /// byte for byte.</para>
    /// </summary>
    public static class FdrExperimentSidecar
    {
        // 8-byte magic. ASCII "OSPRYEXP".
        private static readonly byte[] Magic =
            { (byte)'O', (byte)'S', (byte)'P', (byte)'R', (byte)'Y', (byte)'E', (byte)'X', (byte)'P' };

        public const byte FormatVersion = 2;
        public const int HeaderLength = 32;
        public const int RecordLength = 44;

        /// <summary>
        /// Path for the experiment-wide sidecar of one pass: named after the output blib's stem,
        /// in the SAME directory the per-file sidecars go to. Returns null when the analysis has
        /// no output blib, which is the caller's signal that there is no experiment-scope
        /// artifact to read or write.
        ///
        /// <para><b>The NAME comes from the blib; the DIRECTORY does not.</b> The blib names the
        /// analysis, which is what stops two analyses sharing an output directory from
        /// colliding. But the blib's own directory is not a stable location: every distributed
        /// <c>--task</c> phase runs in its own working directory with the same relative
        /// <c>-o output.blib</c>, so a file placed beside the blib is written by one phase into
        /// a directory the next phase never looks in. That is not hypothetical - it is what the
        /// first version of this method did, and the HPC-chain leg of the regression gate caught
        /// it: phase 2 wrote the 1st-pass file into its own phase directory and phase 3 looked
        /// beside its own blib and found nothing.</para>
        ///
        /// <para>So the directory is resolved through
        /// <see cref="ArtifactPaths.ResolveOutputDir"/> off
        /// <paramref name="siblingArtifactPath"/> - any input file or scores parquet of this
        /// analysis - which is the identical resolution <c>FdrScoresSidecar.Pass1Path</c> uses.
        /// The two artifacts therefore always land together, and every phase agrees on
        /// where.</para>
        /// </summary>
        /// <param name="outputBlib">The run's output blib; supplies the NAME only.</param>
        /// <param name="siblingArtifactPath">Any input / scores-parquet path of this analysis;
        /// supplies the DIRECTORY, via the same resolution the per-file sidecars use.</param>
        /// <param name="pass">Which pass's experiment sidecar.</param>
        public static string PathFor(string outputBlib, string siblingArtifactPath,
            FdrScoresSidecar.Pass pass)
        {
            if (string.IsNullOrEmpty(outputBlib) || string.IsNullOrEmpty(siblingArtifactPath))
                return null;
            string stem = Path.GetFileNameWithoutExtension(outputBlib);
            if (string.IsNullOrEmpty(stem))
                return null;
            string filename = string.Format("{0}.{1}.fdr_experiment.bin",
                stem, pass == FdrScoresSidecar.Pass.FirstPass ? "1st-pass" : "2nd-pass");
            string parent = ArtifactPaths.ResolveOutputDir(siblingArtifactPath);
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
        }

        /// <summary>
        /// Whether <paramref name="path"/> is an experiment sidecar this build can consume: it
        /// exists, carries the expected magic, the current <see cref="FormatVersion"/> and the
        /// <paramref name="expectedPass"/> byte, and its length matches its own header count.
        /// Never throws - a missing, short, or foreign file is simply false. Same contract, and
        /// the same reasons for it, as <see cref="FdrScoresSidecar.IsCurrentFormat"/>.
        /// </summary>
        public static bool IsCurrentFormat(string path, FdrScoresSidecar.Pass expectedPass)
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
                if (!HeaderOk(header, expectedPass, out ulong headerCount))
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
        /// Write the experiment-wide sidecar, one record per distinct entry_id, in ascending
        /// entry_id order. Atomic via <see cref="FileSaver"/>: the temp file is promoted on
        /// Commit, so a failure leaves any existing destination untouched.
        /// </summary>
        public static void Write(string path,
            IReadOnlyDictionary<uint, FdrExperimentRecord> byEntryId, FdrScoresSidecar.Pass pass)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (byEntryId == null) throw new ArgumentNullException(nameof(byEntryId));

            // Canonical order, so the file is a function of its contents and not of the order
            // the producer walked its inputs (the straight-through and distributed routes do
            // not agree on that, and the gate compares their outputs byte for byte).
            var ids = new uint[byEntryId.Count];
            int idIdx = 0;
            foreach (uint id in byEntryId.Keys)
                ids[idIdx++] = id;
            Array.Sort(ids); // Array.Sort OK: distinct entry_ids, so the comparison never ties

            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            using (var saver = new FileSaver(path))
            {
                using (var fs = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(Magic);                    // [0..8]
                    bw.Write(FormatVersion);            // [8]
                    bw.Write((byte)pass);               // [9]
                    bw.Write(new byte[6]);              // [10..16] reserved
                    bw.Write((ulong)ids.Length);        // [16..24]
                    bw.Write(new byte[8]);              // [24..32] reserved

                    foreach (uint id in ids)
                    {
                        var r = byEntryId[id];
                        bw.Write(r.EntryId);                        // [0..4]
                        bw.Write(r.ExperimentPrecursorQvalue);      // [4..12]
                        bw.Write(r.ExperimentPeptideQvalue);        // [12..20]
                        bw.Write(r.ExperimentProteinQvalue);        // [20..28]
                        bw.Write(r.ExperimentAggregateScore);       // [28..36]
                        bw.Write(r.Pep);                            // [36..44]
                    }
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Stream every record to <paramref name="onRecord"/> in stored (ascending entry_id)
        /// order, one 36-byte record resident at a time. Returns false - with whatever partial
        /// callback effects the caller must then discard - on a missing file or any header /
        /// size mismatch.
        /// </summary>
        public static bool ReadRecords(string path, FdrScoresSidecar.Pass expectedPass,
            Action<FdrExperimentRecord> onRecord)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (onRecord == null) throw new ArgumentNullException(nameof(onRecord));

            try
            {
                using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (src.Length < HeaderLength)
                        return false;
                    var header = new byte[HeaderLength];
                    if (!ReadFully(src, header, HeaderLength))
                        return false;
                    if (!HeaderOk(header, expectedPass, out ulong headerCount))
                        return false;
                    if (!TryComputeExpectedLen(headerCount, out int expectedLen) ||
                        src.Length != expectedLen)
                    {
                        return false;
                    }

                    var record = new byte[RecordLength];
                    for (ulong rec = 0; rec < headerCount; rec++)
                    {
                        if (!ReadFully(src, record, RecordLength))
                            return false;
                        onRecord(new FdrExperimentRecord(
                            BitConverter.ToUInt32(record, 0),
                            BitConverter.ToDouble(record, 4),
                            BitConverter.ToDouble(record, 12),
                            BitConverter.ToDouble(record, 20),
                            BitConverter.ToDouble(record, 28),
                            BitConverter.ToDouble(record, 36)));
                    }
                }
            }
            // NOT a bare catch, for the reason every reader in FdrScoresSidecar gives: an
            // OutOfMemoryException reported as a MISSING sidecar leaves the consuming entries at
            // their defaults, and a q-value default of 1.0 is a value the run will then report
            // rather than fail on. Let it propagate and kill the run instead.
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Read the whole experiment sidecar into an entry_id-keyed map. Returns null on any
        /// failure <see cref="ReadRecords"/> reports, so a caller cannot mistake an unreadable
        /// file for an empty analysis.
        /// </summary>
        public static Dictionary<uint, FdrExperimentRecord> ReadMap(
            string path, FdrScoresSidecar.Pass expectedPass)
        {
            // A null / empty path is "this analysis has no experiment sidecar", which is the
            // same answer as an unreadable one - PathFor returns null when there is no output
            // blib to name the file after, and a caller that passes it straight through should
            // get null rather than an argument fault.
            if (string.IsNullOrEmpty(path))
                return null;
            var map = new Dictionary<uint, FdrExperimentRecord>();
            return ReadRecords(path, expectedPass, rec => map[rec.EntryId] = rec) ? map : null;
        }

        /// <summary>
        /// Validate the 32-byte header: magic, current version, expected pass byte. Shared by
        /// every entry point so they cannot drift on what they accept.
        /// </summary>
        private static bool HeaderOk(byte[] header, FdrScoresSidecar.Pass expectedPass,
            out ulong headerCount)
        {
            headerCount = 0;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (header[i] != Magic[i])
                    return false;
            }
            if (header[8] != FormatVersion || header[9] != (byte)expectedPass)
                return false;
            headerCount = BitConverter.ToUInt64(header, 16);
            return true;
        }

        /// <summary>
        /// Compute <c>HeaderLength + headerCount * RecordLength</c> with overflow detection, so
        /// a corrupt count cannot wrap int and let the size check pass spuriously.
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
