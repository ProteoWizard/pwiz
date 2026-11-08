/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Reader / writer for the per-input-file <c>&lt;stem&gt;.1st-pass.protein_best.bin</c>
    /// artifact: the protein-best column (<c>--protein-best</c>) one run's first-pass score
    /// pass scored with, one value per entry_id, and the run's floor.
    ///
    /// <para><b>Why it exists.</b> The first-pass model of a protein-best run was trained on the
    /// raw features AND this column, so the second pass cannot score a rescored row from its
    /// reconciled features alone. The column is computed from round 1's models, which are not
    /// saved, and from the run's sibling scores, so a later process cannot recompute it; it is
    /// written down here instead, by FirstPassFDR, beside the run's first-pass scores.</para>
    ///
    /// Format (32-byte header + N x 12-byte records + an 8-byte extension + the validity stamp,
    /// all little-endian; see <see cref="BinarySidecarStamp"/>):
    /// <code>
    ///   magic         [0..8]   = b"OSPRYPBC"
    ///   version       [8]      = u8 (= 1)
    ///   pass          [9]      = u8 (1 = first-pass)
    ///   reserved      [10..16] = 6 bytes (zero)
    ///   entry_count   [16..24] = u64
    ///   stamp_length  [24..28] = u32
    ///   ext_length    [28..32] = u32 (= 8)
    ///   body          [32..]   = entry_count * 12 bytes:
    ///                            [0..4]  u32 entry_id
    ///                            [4..12] f64 the column for that entry_id in this run
    ///   extension     [..+8]   = f64 the run's floor
    ///   stamp         [..end]  = stamp_length bytes, the writing task's <see cref="ArtifactStamp"/>
    /// </code>
    ///
    /// <para>Records are in ascending entry_id order, distinct, so the file is a function of its
    /// contents rather than of the order a lane produced them in.</para>
    /// </summary>
    public static class ProteinBestSidecar
    {
        public const string EXT = @".protein_best.bin";

        // 8-byte magic. ASCII "OSPRYPBC".
        private static readonly byte[] Magic =
            { (byte)'O', (byte)'S', (byte)'P', (byte)'R', (byte)'Y', (byte)'P', (byte)'B', (byte)'C' };

        public const byte FormatVersion = 1;
        public const int HeaderLength = BinarySidecarStamp.HEADER_LENGTH;
        public const int RecordLength = 12;
        private const int EXTENSION_LENGTH = 8;

        /// <summary>
        /// One run's protein-best values: ascending distinct entry_ids, their values and the floor.
        /// </summary>
        public sealed class RunColumn
        {
            public RunColumn(uint[] entryIds, double[] values, double floor)
            {
                EntryIds = entryIds;
                Values = values;
                Floor = floor;
            }

            public uint[] EntryIds { get; }
            public double[] Values { get; }
            public double Floor { get; }
        }

        /// <summary>
        /// Path for one input file's column:
        /// <c>&lt;dir&gt;/&lt;stem&gt;.1st-pass.protein_best.bin</c>, beside its
        /// <see cref="FdrScoresSidecar.Pass1Path"/> wherever that resolves.
        /// </summary>
        public static string PathFor(string inputPath)
        {
            return PathBesidePass1(FdrScoresSidecar.Pass1Path(inputPath));
        }

        /// <summary>The column's path beside a run's first-pass FDR scores file.</summary>
        public static string PathBesidePass1(string pass1SidecarPath)
        {
            if (pass1SidecarPath == null)
                throw new ArgumentNullException(nameof(pass1SidecarPath));
            if (!pass1SidecarPath.EndsWith(FdrScoresSidecar.EXT, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(string.Format(@"{0} is not a first-pass FDR scores file.", pass1SidecarPath),
                    nameof(pass1SidecarPath));
            }
            return pass1SidecarPath.Substring(0, pass1SidecarPath.Length - FdrScoresSidecar.EXT.Length) + EXT;
        }

        /// <summary>
        /// Write one run's column atomically (<see cref="SimpleFileSaver"/>), so the file is absent
        /// or complete-and-stamped, never one without the other.
        /// </summary>
        public static void Write(string path, IReadOnlyList<uint> entryIds, IReadOnlyList<double> values, double floor,
            ArtifactStamp stamp)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (entryIds == null) throw new ArgumentNullException(nameof(entryIds));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (entryIds.Count != values.Count)
                throw new ArgumentException(@"One value per entry_id.", nameof(values));
            for (int i = 1; i < entryIds.Count; i++)
            {
                if (entryIds[i] <= entryIds[i - 1])
                    throw new ArgumentException(@"The entry_ids must be strictly ascending.", nameof(entryIds));
            }
            byte[] stampBytes = BinarySidecarStamp.Encode(stamp);

            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            using (var saver = new SimpleFileSaver(path))
            {
                using (var fs = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(Magic);                // [0..8]
                    bw.Write(FormatVersion);        // [8]
                    bw.Write((byte)FdrScoresSidecar.Pass.FirstPass); // [9]
                    bw.Write(new byte[6]);          // [10..16] reserved
                    bw.Write((ulong)entryIds.Count); // [16..24]
                    BinarySidecarStamp.WriteHeaderField(bw, stampBytes, EXTENSION_LENGTH); // [24..32]
                    for (int i = 0; i < entryIds.Count; i++)
                    {
                        bw.Write(entryIds[i]);      // [0..4]
                        bw.Write(values[i]);        // [4..12]
                    }
                    bw.Write(floor);                // extension
                    bw.Write(stampBytes);
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Read one run's column, or null on a missing file or any header / size mismatch, so a
        /// caller cannot mistake an unreadable column for a run whose every row is at the floor.
        /// </summary>
        public static RunColumn Read(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            try
            {
                using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var header = ReadHeader(src);
                    if (header == null ||
                        !BinarySidecarStamp.TryComputeExpectedLength(header, RecordLength, out int count, out long expectedLen) ||
                        src.Length != expectedLen)
                    {
                        return null;
                    }
                    var body = new byte[checked(count * RecordLength + EXTENSION_LENGTH)];
                    if (!ReadFully(src, body, body.Length))
                        return null;
                    var entryIds = new uint[count];
                    var values = new double[count];
                    for (int i = 0; i < count; i++)
                    {
                        entryIds[i] = BitConverter.ToUInt32(body, i * RecordLength);
                        values[i] = BitConverter.ToDouble(body, i * RecordLength + 4);
                        if (i > 0 && entryIds[i] <= entryIds[i - 1])
                            return null;
                    }
                    double floor = BitConverter.ToDouble(body, count * RecordLength);
                    return new RunColumn(entryIds, values, floor);
                }
            }
            // NOT a bare catch: an OutOfMemoryException reported as a missing artifact would read
            // as a run to recompute rather than a machine that ran out of memory.
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return null;
            }
        }

        /// <summary>
        /// The validity stamp of the column at <paramref name="path"/>, or null when it is
        /// missing or not in the current format.
        /// </summary>
        public static ArtifactStamp ReadStamp(string path)
        {
            return IsCurrentFormat(path) ? BinarySidecarStamp.TryRead(path, RecordLength) : null;
        }

        /// <summary>
        /// Whether <paramref name="path"/> is a column this build can read: it exists, carries the
        /// magic, the current version and the first-pass byte, and its length matches its header.
        /// Never throws.
        /// </summary>
        public static bool IsCurrentFormat(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            try
            {
                using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var header = ReadHeader(src);
                    return header != null &&
                           BinarySidecarStamp.TryComputeExpectedLength(header, RecordLength, out _, out long expectedLen) &&
                           src.Length == expectedLen;
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return false;
            }
        }

        /// <summary>The 32-byte header when it is this format's, else null.</summary>
        private static byte[] ReadHeader(Stream src)
        {
            if (src.Length < HeaderLength)
                return null;
            var header = new byte[HeaderLength];
            if (!ReadFully(src, header, HeaderLength))
                return null;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (header[i] != Magic[i])
                    return null;
            }
            if (header[8] != FormatVersion || header[9] != (byte)FdrScoresSidecar.Pass.FirstPass ||
                BinarySidecarStamp.ReadExtensionLength(header) != EXTENSION_LENGTH)
            {
                return null;
            }
            return header;
        }

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
